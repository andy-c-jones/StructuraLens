using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Main analyzer that loads a solution and computes metrics for all projects.
/// </summary>
public sealed class SolutionAnalyzer
{
    private static bool _msBuildRegistered;
    private static readonly object _lock = new();

    private readonly System.Collections.Concurrent.ConcurrentBag<string> _warnings = [];
    private readonly ILogger _logger;

    public SolutionAnalyzer(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public static void EnsureMSBuildRegistered()
    {
        if (_msBuildRegistered) return;

        lock (_lock)
        {
            if (_msBuildRegistered) return;

            var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
            if (instances.Count > 0)
            {
                MSBuildLocator.RegisterInstance(instances.OrderByDescending(i => i.Version).First());
            }
            else
            {
                MSBuildLocator.RegisterDefaults();
            }
            _msBuildRegistered = true;
        }
    }

    /// <summary>
    /// Analyzes a solution.
    /// </summary>
    public async Task<AnalysisReport> AnalyzeSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
    {     
        _logger.LogInformation("Starting solution analysis: {SolutionPath}", solutionPath);
        EnsureMSBuildRegistered();

        var fullPath = Path.GetFullPath(solutionPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Solution file not found: {fullPath}");
        }

        // Restore NuGet packages to ensure all references are available
        _logger.LogInformation("Restoring NuGet packages...");
        await RestorePackagesAsync(fullPath, cancellationToken);

        _logger.LogInformation("Loading solution into MSBuild workspace...");
        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning)
            {
                _warnings.Add($"Workspace warning: {e.Diagnostic.Message}");
                _logger.LogWarning("Workspace warning: {Message}", e.Diagnostic.Message);
            }
        });

        var solution = await workspace.OpenSolutionAsync(fullPath, cancellationToken: cancellationToken);
        var csharpProjects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp).ToList();
        _logger.LogInformation("Loaded solution with {ProjectCount} C# projects", csharpProjects.Count);

        // Pre-fetch all compilations in parallel and cache them for reuse
        _logger.LogInformation("Pre-fetching compilations for all projects...");
        var compilationCache = new System.Collections.Concurrent.ConcurrentDictionary<string, Compilation>();
        
        await Parallel.ForEachAsync(csharpProjects, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        }, async (project, ct) =>
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation != null)
            {
                compilationCache[project.Name] = compilation;
            }
            else
            {
                _warnings.Add($"Could not get compilation for project: {project.Name}");
                _logger.LogWarning("Could not get compilation for project: {ProjectName}", project.Name);
            }
        });

        _logger.LogInformation("Cached {Count} compilations", compilationCache.Count);

        // Analyze projects in parallel for performance on large solutions
        // Collect both metrics AND coupling dependencies in a single pass
        var projectResultsBag = new System.Collections.Concurrent.ConcurrentBag<(ProjectMetrics metrics, List<DependencyEdge> dependencies)>();
        var completedCount = 0;
        var totalProjects = csharpProjects.Count;

        await Parallel.ForEachAsync(csharpProjects, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        }, async (project, ct) =>
        {
            var currentIndex = Interlocked.Increment(ref completedCount);
            _logger.LogInformation("Analyzing project {Index}/{Total}: {ProjectName}", currentIndex, totalProjects, project.Name);
            
            var result = await AnalyzeProjectWithCouplingAsync(project, compilationCache, ct);
            projectResultsBag.Add(result);
            
            _logger.LogInformation("Completed {ProjectName}: {TypeCount} types, {MethodCount} methods, {DepCount} dependencies", 
                project.Name, 
                result.metrics.Types.Count, 
                result.metrics.TotalMethods,
                result.dependencies.Count);
        });

        // Extract metrics and dependencies from combined results
        var projectMetricsList = new List<ProjectMetrics>();
        var allDependencies = new List<DependencyEdge>();
        foreach (var (metrics, dependencies) in projectResultsBag)
        {
            projectMetricsList.Add(metrics);
            allDependencies.AddRange(dependencies);
        }

        // Build coupling analysis from pre-collected dependencies (no separate document pass needed)
        _logger.LogInformation("Building coupling analysis from {DepCount} dependencies", allDependencies.Count);
        var couplingAnalysis = CouplingAnalyzer.BuildCouplingAnalysisFromDependencies(solution, allDependencies);

        _logger.LogInformation("Analysis complete. Total: {ProjectCount} projects, {TypeCount} types, {MethodCount} methods",
            projectMetricsList.Count,
            projectMetricsList.Sum(p => p.Types.Count),
            projectMetricsList.Sum(p => p.TotalMethods));

        return new AnalysisReport(
            SolutionPath: fullPath,
            AnalyzedAt: DateTime.UtcNow,
            Projects: projectMetricsList,
            Warnings: _warnings.ToList())
        {
            CouplingAnalysis = couplingAnalysis
        };
    }

    /// <summary>
    /// Analyzes a project.
    /// </summary>
    public async Task<AnalysisReport> AnalyzeProjectAsync(string projectPath, CancellationToken cancellationToken = default)
    {     
        _logger.LogInformation("Starting project analysis: {ProjectPath}", projectPath);
        EnsureMSBuildRegistered();

        var fullPath = Path.GetFullPath(projectPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Project file not found: {fullPath}");
        }

        // Restore NuGet packages to ensure all references are available
        _logger.LogInformation("Restoring NuGet packages...");
        await RestorePackagesAsync(fullPath, cancellationToken);

        _logger.LogInformation("Loading project into MSBuild workspace...");
        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning)
            {
                _warnings.Add($"Workspace warning: {e.Diagnostic.Message}");
                _logger.LogWarning("Workspace warning: {Message}", e.Diagnostic.Message);
            }
        });

        var project = await workspace.OpenProjectAsync(fullPath, cancellationToken: cancellationToken);
        _logger.LogInformation("Analyzing project: {ProjectName}", project.Name);
        
        // For single project, create an empty cache (compilation will be fetched on demand)
        var compilationCache = new System.Collections.Concurrent.ConcurrentDictionary<string, Compilation>();
        var projectMetrics = await AnalyzeProjectAsync(project, compilationCache, cancellationToken);

        _logger.LogInformation("Analyzing project coupling");
        var couplingAnalysis = await CouplingAnalyzer.AnalyzeProjectCouplingAsync(project, _logger, cancellationToken);

        return new AnalysisReport(
            SolutionPath: fullPath,
            AnalyzedAt: DateTime.UtcNow,
            Projects: [projectMetrics],
            Warnings: _warnings.ToList())
        {
            CouplingAnalysis = couplingAnalysis
        };
    }

    private async Task<(ProjectMetrics metrics, List<DependencyEdge> dependencies)> AnalyzeProjectWithCouplingAsync(
        Project project, 
        System.Collections.Concurrent.ConcurrentDictionary<string, Compilation> compilationCache, 
        CancellationToken cancellationToken)
    {
        // Use cached compilation if available
        if (!compilationCache.TryGetValue(project.Name, out var compilation))
        {
            _logger.LogDebug("Getting compilation for project: {ProjectName}", project.Name);
            compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation == null)
            {
                _warnings.Add($"Could not get compilation for project: {project.Name}");
                _logger.LogWarning("Could not get compilation for project: {ProjectName}", project.Name);
                return (new ProjectMetrics(project.Name, project.FilePath ?? "", []), []);
            }
        }

        // Collect diagnostics from compilation
        var diagnosticSummary = CollectDiagnostics(compilation);

        var documents = project.Documents.Where(d => d.SourceCodeKind == SourceCodeKind.Regular).ToList();
        var documentCount = documents.Count;

        _logger.LogDebug("Analyzing {DocumentCount} documents in project {ProjectName}", documentCount, project.Name);

        // Analyze documents in parallel for performance - collect both metrics AND dependencies in single pass
        var typeMetricsBag = new System.Collections.Concurrent.ConcurrentBag<TypeMetrics>();
        var dependenciesBag = new System.Collections.Concurrent.ConcurrentBag<List<DependencyEdge>>();
        var processedCount = 0;

        await Parallel.ForEachAsync(documents, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        }, async (document, ct) =>
        {
            var currentCount = Interlocked.Increment(ref processedCount);
            if (currentCount % 50 == 0)
            {
                _logger.LogDebug("Progress: {DocumentIndex}/{DocumentCount} documents processed in {ProjectName}", 
                    currentCount, documentCount, project.Name);
            }

            var syntaxTree = await document.GetSyntaxTreeAsync(ct);
            var semanticModel = await document.GetSemanticModelAsync(ct);

            if (syntaxTree == null || semanticModel == null)
                return;

            var root = await syntaxTree.GetRootAsync(ct);
            var filePath = document.FilePath ?? "";
            
            // Analyze coupling dependencies in same pass
            var docDependencies = CouplingAnalyzer.AnalyzeDocumentCoupling(semanticModel, filePath, root);
            dependenciesBag.Add(docDependencies.ToList());
            
            // Analyze traditional type declarations
            var typeDeclarations = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in typeDeclarations)
            {
                var typeMetrics = AnalyzeTypeDeclaration(typeDecl, semanticModel, filePath);
                typeMetricsBag.Add(typeMetrics);
            }

            // Analyze top-level statements (C# 9+ feature)
            var topLevelStatements = root.DescendantNodes()
                .OfType<GlobalStatementSyntax>()
                .ToList();

            if (topLevelStatements.Count > 0)
            {
                var topLevelMetrics = AnalyzeTopLevelStatements(root, topLevelStatements, semanticModel, filePath);
                if (topLevelMetrics != null)
                {
                    typeMetricsBag.Add(topLevelMetrics);
                }
            }
        });

        var typeMetricsList = typeMetricsBag.ToList();
        
        // Merge all dependencies from this project
        var allDependencies = new List<DependencyEdge>();
        foreach (var deps in dependenciesBag)
        {
            allDependencies.AddRange(deps);
        }

        var projectMetrics = new ProjectMetrics(
            Name: project.Name,
            FilePath: project.FilePath ?? "",
            Types: typeMetricsList)
        {
            Diagnostics = diagnosticSummary
        };

        return (projectMetrics, allDependencies);
    }

    private async Task<ProjectMetrics> AnalyzeProjectAsync(Project project, System.Collections.Concurrent.ConcurrentDictionary<string, Compilation> compilationCache, CancellationToken cancellationToken)
    {
        var (metrics, _) = await AnalyzeProjectWithCouplingAsync(project, compilationCache, cancellationToken);
        return metrics;
    }

    private static DiagnosticSummary CollectDiagnostics(Compilation compilation)
    {
        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden || d.Id.StartsWith("CS"))
            .Select(d => new DiagnosticInfo(
                Id: d.Id,
                Message: d.GetMessage(),
                Severity: MapSeverity(d.Severity),
                FilePath: d.Location.SourceTree?.FilePath ?? "",
                Line: d.Location.GetLineSpan().StartLinePosition.Line + 1,
                Column: d.Location.GetLineSpan().StartLinePosition.Character + 1)
            {
                Category = d.Descriptor.Category,
                HelpLink = d.Descriptor.HelpLinkUri
            })
            .ToList();

        return new DiagnosticSummary
        {
            ErrorCount = diagnostics.Count(d => d.Severity == DiagnosticLevel.Error),
            WarningCount = diagnostics.Count(d => d.Severity == DiagnosticLevel.Warning),
            InfoCount = diagnostics.Count(d => d.Severity == DiagnosticLevel.Info),
            HiddenCount = diagnostics.Count(d => d.Severity == DiagnosticLevel.Hidden),
            Diagnostics = diagnostics
        };
    }

    private static DiagnosticLevel MapSeverity(Microsoft.CodeAnalysis.DiagnosticSeverity severity)
    {
        return severity switch
        {
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error => DiagnosticLevel.Error,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => DiagnosticLevel.Warning,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Info => DiagnosticLevel.Info,
            _ => DiagnosticLevel.Hidden
        };
    }

    private TypeMetrics AnalyzeTypeDeclaration(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, string filePath)
    {
        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        var dit = DepthOfInheritanceCalculator.Calculate(typeSymbol);

        var methodMetricsList = new List<MethodMetrics>();

        var methods = typeDecl.DescendantNodes()
            .OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var metrics = AnalyzeMethod(method, semanticModel, filePath);
            methodMetricsList.Add(metrics);
        }

        var constructors = typeDecl.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>();

        foreach (var ctor in constructors)
        {
            var metrics = AnalyzeConstructor(ctor, typeDecl, semanticModel, filePath);
            methodMetricsList.Add(metrics);
        }

        var typeName = typeSymbol?.ToDisplayString() ?? typeDecl.Identifier.Text;

        return new TypeMetrics(
            FullName: typeName,
            FilePath: filePath,
            DepthOfInheritance: dit,
            Methods: methodMetricsList);
    }

    private TypeMetrics? AnalyzeTopLevelStatements(SyntaxNode root, List<GlobalStatementSyntax> topLevelStatements, SemanticModel semanticModel, string filePath)
    {
        var methodMetricsList = new List<MethodMetrics>();

        // Analyze the top-level code as a single "Main" method using unified calculator
        var firstStatement = topLevelStatements.First();
        var lastStatement = topLevelStatements.Last();

        var metrics = UnifiedMetricsCalculator.Calculate(root);
        var cc = metrics.CyclomaticComplexity;
        var loc = metrics.LinesOfCode;
        var halsteadVolume = metrics.HalsteadVolume;
        var mi = metrics.MaintainabilityIndex;

        var startLine = firstStatement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var endLine = lastStatement.GetLocation().GetLineSpan().EndLinePosition.Line + 1;

        methodMetricsList.Add(new MethodMetrics(
            FullName: "<Program>$.Main(string[])",
            FilePath: filePath,
            StartLine: startLine,
            EndLine: endLine,
            CyclomaticComplexity: cc,
            LinesOfExecutableCode: loc,
            HalsteadVolume: halsteadVolume,
            MaintainabilityIndex: mi));

        // Analyze local functions defined in top-level code
        var localFunctions = root.DescendantNodes()
            .OfType<LocalFunctionStatementSyntax>()
            .Where(lf => !lf.Ancestors().OfType<TypeDeclarationSyntax>().Any());

        foreach (var localFunc in localFunctions)
        {
            var localMetrics = AnalyzeLocalFunction(localFunc, filePath);
            methodMetricsList.Add(localMetrics);
        }

        return new TypeMetrics(
            FullName: "<Program>$",
            FilePath: filePath,
            DepthOfInheritance: 0,
            Methods: methodMetricsList);
    }

    private MethodMetrics AnalyzeMethod(MethodDeclarationSyntax method, SemanticModel semanticModel, string filePath)
    {
        var methodSymbol = semanticModel.GetDeclaredSymbol(method);
        var fullName = methodSymbol?.ToDisplayString() ?? method.Identifier.Text;

        int cc, loc;
        double halsteadVolume, mi;

        if (method.Body != null || method.ExpressionBody != null)
        {
            var metrics = UnifiedMetricsCalculator.Calculate(method);
            cc = metrics.CyclomaticComplexity;
            loc = method.Body != null ? metrics.LinesOfCode : 1;
            halsteadVolume = metrics.HalsteadVolume;
            mi = metrics.MaintainabilityIndex;
        }
        else
        {
            cc = 1;
            loc = 0;
            halsteadVolume = 0;
            mi = 100.0;
        }

        var lineSpan = method.GetLocation().GetLineSpan();

        return new MethodMetrics(
            FullName: fullName,
            FilePath: filePath,
            StartLine: lineSpan.StartLinePosition.Line + 1,
            EndLine: lineSpan.EndLinePosition.Line + 1,
            CyclomaticComplexity: cc,
            LinesOfExecutableCode: loc,
            HalsteadVolume: halsteadVolume,
            MaintainabilityIndex: mi);
    }

    private MethodMetrics AnalyzeConstructor(ConstructorDeclarationSyntax ctor, TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, string filePath)
    {
        var ctorSymbol = semanticModel.GetDeclaredSymbol(ctor);
        var fullName = ctorSymbol?.ToDisplayString() ?? $"{typeDecl.Identifier.Text}.ctor";

        int cc, loc;
        double halsteadVolume, mi;

        if (ctor.Body != null || ctor.ExpressionBody != null)
        {
            var metrics = UnifiedMetricsCalculator.Calculate(ctor);
            cc = metrics.CyclomaticComplexity;
            loc = ctor.Body != null ? metrics.LinesOfCode : 1;
            halsteadVolume = metrics.HalsteadVolume;
            mi = metrics.MaintainabilityIndex;
        }
        else
        {
            cc = 1;
            loc = 0;
            halsteadVolume = 0;
            mi = 100.0;
        }

        var lineSpan = ctor.GetLocation().GetLineSpan();

        return new MethodMetrics(
            FullName: fullName,
            FilePath: filePath,
            StartLine: lineSpan.StartLinePosition.Line + 1,
            EndLine: lineSpan.EndLinePosition.Line + 1,
            CyclomaticComplexity: cc,
            LinesOfExecutableCode: loc,
            HalsteadVolume: halsteadVolume,
            MaintainabilityIndex: mi);
    }

    private MethodMetrics AnalyzeLocalFunction(LocalFunctionStatementSyntax localFunc, string filePath)
    {
        var fullName = $"<Program>$.{localFunc.Identifier.Text}()";

        int cc, loc;
        double halsteadVolume, mi;

        if (localFunc.Body != null || localFunc.ExpressionBody != null)
        {
            var metrics = UnifiedMetricsCalculator.Calculate(localFunc);
            cc = metrics.CyclomaticComplexity;
            loc = localFunc.Body != null ? metrics.LinesOfCode : 1;
            halsteadVolume = metrics.HalsteadVolume;
            mi = metrics.MaintainabilityIndex;
        }
        else
        {
            cc = 1;
            loc = 0;
            halsteadVolume = 0;
            mi = 100.0;
        }

        var lineSpan = localFunc.GetLocation().GetLineSpan();

        return new MethodMetrics(
            FullName: fullName,
            FilePath: filePath,
            StartLine: lineSpan.StartLinePosition.Line + 1,
            EndLine: lineSpan.EndLinePosition.Line + 1,
            CyclomaticComplexity: cc,
            LinesOfExecutableCode: loc,
            HalsteadVolume: halsteadVolume,
            MaintainabilityIndex: mi);
    }

    private async Task RestorePackagesAsync(string projectOrSolutionPath, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting package restore for {Path}", projectOrSolutionPath);
        
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"restore \"{projectOrSolutionPath}\" --verbosity normal --interactive",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
        {
            _logger.LogError("Failed to start dotnet restore process. Ensure the .NET SDK is installed and 'dotnet' is available in PATH.");
            return;
        }

        // Read stdout and stderr concurrently to avoid deadlocks
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        
        await process.WaitForExitAsync(cancellationToken);
        
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        
        if (process.ExitCode != 0)
        {
            _logger.LogError("Package restore failed with exit code {ExitCode} for {Path}", process.ExitCode, projectOrSolutionPath);
            
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                _logger.LogError("Restore stderr: {Error}", stderr);
            }
            
            if (!string.IsNullOrWhiteSpace(stdout))
            {
                _logger.LogError("Restore stdout: {Output}", stdout);
            }
            
            // Log common troubleshooting hints
            if (stderr.Contains("401") || stdout.Contains("401") || 
                stderr.Contains("Unable to load the service index") || stdout.Contains("Unable to load the service index"))
            {
                _logger.LogError("Authentication failure detected. For private NuGet feeds, ensure credentials are configured. " +
                    "Options: (1) Use 'dotnet nuget add source' with credentials, (2) Configure nuget.config with credentials, " +
                    "(3) Use Azure Artifacts Credential Provider or similar for your feed type. " +
                    "See: https://learn.microsoft.com/en-us/nuget/consume-packages/consuming-packages-authenticated-feeds");
            }
        }
        else
        {
            _logger.LogDebug("Package restore completed successfully for {Path}", projectOrSolutionPath);
        }
    }
}
