using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Analysis.Logging;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Main analyzer that loads a solution and computes metrics for all projects.
/// </summary>
public sealed class SolutionAnalyzer : ISolutionAnalyzer
{
    private readonly System.Collections.Concurrent.ConcurrentBag<string> _warnings = [];
    private readonly ILogger<SolutionAnalyzer> _logger;
    private readonly INuGetRestorer _nugetRestorer;
    private readonly IMSBuildWorkspaceFactory _workspaceFactory;
    private readonly ICouplingAnalyzer _couplingAnalyzer;
    private readonly IMetricsCalculator _metricsCalculator;
    private readonly IFileSystemService _fileSystem;
    private readonly AnalysisOptions _options;

    public SolutionAnalyzer(
        ILogger<SolutionAnalyzer> logger,
        INuGetRestorer nugetRestorer,
        IMSBuildWorkspaceFactory workspaceFactory,
        ICouplingAnalyzer couplingAnalyzer,
        IMetricsCalculator metricsCalculator,
        IFileSystemService fileSystem,
        AnalysisOptions? options = null)
    {
        _logger = logger;
        _nugetRestorer = nugetRestorer;
        _workspaceFactory = workspaceFactory;
        _couplingAnalyzer = couplingAnalyzer;
        _metricsCalculator = metricsCalculator;
        _fileSystem = fileSystem;
        _options = options ?? new AnalysisOptions();
    }

    /// <summary>
    /// Creates a dependency collector based on the configured aggregation strategy.
    /// </summary>
    private IDependencyCollector CreateDependencyCollector()
    {
        return _options.AggregationStrategy switch
        {
            DependencyAggregationStrategy.InMemory => new InMemoryDependencyCollector(),
            DependencyAggregationStrategy.SQLite => new SQLiteDependencyCollector(
                _options.SQLiteDatabasePath,
                _options.SQLiteBatchSize),
            DependencyAggregationStrategy.Adaptive => new AdaptiveDependencyCollector(
                _options.MemoryThresholdMB,
                _options.SQLiteBatchSize),
            _ => throw new ArgumentException($"Unknown aggregation strategy: {_options.AggregationStrategy}")
        };
    }

    /// <inheritdoc />
    public async Task<AnalysisReport> AnalyzeSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
    {     
        SolutionAnalyzerLog.StartingSolutionAnalysis(_logger, solutionPath);

        var fullPath = _fileSystem.GetFullPath(solutionPath);
        if (!_fileSystem.FileExists(fullPath))
        {
            throw new FileNotFoundException($"Solution file not found: {fullPath}");
        }

        // Restore NuGet packages to ensure all references are available
        SolutionAnalyzerLog.RestoringNuGetPackages(_logger);
        await _nugetRestorer.RestorePackagesAsync(fullPath, cancellationToken);

        SolutionAnalyzerLog.LoadingSolutionIntoWorkspace(_logger);
        using var workspace = _workspaceFactory.Create();
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning)
            {
                _warnings.Add($"Workspace warning: {e.Diagnostic.Message}");
                SolutionAnalyzerLog.WorkspaceWarning(_logger, e.Diagnostic.Message);
            }
        });

        var solution = await workspace.OpenSolutionAsync(fullPath, cancellationToken: cancellationToken);
        var csharpProjects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp).ToList();
        SolutionAnalyzerLog.LoadedSolutionWithProjects(_logger, csharpProjects.Count);

        // Pre-fetch all compilations in parallel and cache them for reuse
        SolutionAnalyzerLog.PreFetchingCompilations(_logger);
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
                SolutionAnalyzerLog.CouldNotGetCompilation(_logger, project.Name);
            }
        });

        SolutionAnalyzerLog.CachedCompilations(_logger, compilationCache.Count);

        // Analyze projects in parallel for performance on large solutions
        // Use streaming dependency collector to reduce memory usage
        using var dependencyCollector = CreateDependencyCollector();
        var projectMetricsList = new System.Collections.Concurrent.ConcurrentBag<ProjectMetrics>();
        var completedCount = 0;
        var totalProjects = csharpProjects.Count;

        await Parallel.ForEachAsync(csharpProjects, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        }, async (project, ct) =>
        {
            var currentIndex = Interlocked.Increment(ref completedCount);
            SolutionAnalyzerLog.AnalyzingProject(_logger, currentIndex, totalProjects, project.Name);
            
            var metrics = await AnalyzeProjectWithCouplingAsync(project, compilationCache, dependencyCollector, ct);
            projectMetricsList.Add(metrics);
            
            SolutionAnalyzerLog.CompletedProject(_logger, project.Name, metrics.Types.Count, metrics.TotalMethods, 0);
        });

        // Build coupling analysis from streaming collector (already aggregated)
        var collectorStats = dependencyCollector.GetStats();
        SolutionAnalyzerLog.BuildingCouplingAnalysis(_logger, (int)collectorStats.UniqueEdgesCount);
        var couplingAnalysis = _couplingAnalyzer.BuildCouplingAnalysisFromCollector(solution, dependencyCollector);

        SolutionAnalyzerLog.AnalysisComplete(_logger, projectMetricsList.Count, projectMetricsList.Sum(p => p.Types.Count), projectMetricsList.Sum(p => p.TotalMethods));

        return new AnalysisReport(
            SolutionPath: fullPath,
            AnalyzedAt: DateTime.UtcNow,
            Projects: projectMetricsList.ToList(),
            Warnings: _warnings.ToList())
        {
            CouplingAnalysis = couplingAnalysis,
            AggregationStats = collectorStats
        };
    }

    /// <inheritdoc />
    public async Task<AnalysisReport> AnalyzeProjectAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        SolutionAnalyzerLog.StartingProjectAnalysis(_logger, projectPath);

        var fullPath = _fileSystem.GetFullPath(projectPath);
        if (!_fileSystem.FileExists(fullPath))
        {
            throw new FileNotFoundException($"Project file not found: {fullPath}");
        }

        // Restore NuGet packages
        SolutionAnalyzerLog.RestoringNuGetPackages(_logger);
        await _nugetRestorer.RestorePackagesAsync(fullPath, cancellationToken);

        SolutionAnalyzerLog.LoadingProjectIntoWorkspace(_logger);
        using var workspace = _workspaceFactory.Create();
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning)
            {
                _warnings.Add($"Workspace warning: {e.Diagnostic.Message}");
                SolutionAnalyzerLog.WorkspaceWarning(_logger, e.Diagnostic.Message);
            }
        });

        var project = await workspace.OpenProjectAsync(fullPath, cancellationToken: cancellationToken);
        SolutionAnalyzerLog.AnalyzingProjectSingle(_logger, project.Name);
        
        // For single project, create an empty cache (compilation will be fetched on demand)
        var compilationCache = new System.Collections.Concurrent.ConcurrentDictionary<string, Compilation>();
        var projectMetrics = await AnalyzeProjectAsync(project, compilationCache, cancellationToken);

        SolutionAnalyzerLog.AnalyzingProjectCoupling(_logger);
        var couplingAnalysis = await _couplingAnalyzer.AnalyzeProjectCouplingAsync(project, cancellationToken);

        return new AnalysisReport(
            SolutionPath: fullPath,
            AnalyzedAt: DateTime.UtcNow,
            Projects: [projectMetrics],
            Warnings: _warnings.ToList())
        {
            CouplingAnalysis = couplingAnalysis
        };
    }

    private async Task<ProjectMetrics> AnalyzeProjectWithCouplingAsync(
        Project project, 
        System.Collections.Concurrent.ConcurrentDictionary<string, Compilation> compilationCache,
        IDependencyCollector dependencyCollector,
        CancellationToken cancellationToken)
    {
        // Use cached compilation if available
        if (!compilationCache.TryGetValue(project.Name, out var compilation))
        {
            SolutionAnalyzerLog.GettingCompilationForProject(_logger, project.Name);
            compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation == null)
            {
                _warnings.Add($"Could not get compilation for project: {project.Name}");
                SolutionAnalyzerLog.CouldNotGetCompilation(_logger, project.Name);
                return new ProjectMetrics(project.Name, project.FilePath ?? "", []);
            }
        }

        // Collect diagnostics from compilation
        var diagnosticSummary = CollectDiagnostics(compilation);

        var documents = project.Documents.Where(d => d.SourceCodeKind == SourceCodeKind.Regular).ToList();
        var documentCount = documents.Count;

        SolutionAnalyzerLog.AnalyzingDocumentsInProject(_logger, documentCount, project.Name);

        // Analyze documents in parallel - stream dependencies directly to collector
        var typeMetricsBag = new System.Collections.Concurrent.ConcurrentBag<TypeMetrics>();
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
                SolutionAnalyzerLog.DocumentProcessingProgress(_logger, currentCount, documentCount, project.Name);
            }

            var syntaxTree = await document.GetSyntaxTreeAsync(ct);
            var semanticModel = await document.GetSemanticModelAsync(ct);

            if (syntaxTree == null || semanticModel == null)
                return;

            var root = await syntaxTree.GetRootAsync(ct);
            var filePath = document.FilePath ?? "";
            
            // Analyze coupling dependencies - stream directly to shared collector
            CouplingAnalyzer.AnalyzeDocumentCouplingStreaming(semanticModel, filePath, root, dependencyCollector);
            
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

        var projectMetrics = new ProjectMetrics(
            Name: project.Name,
            FilePath: project.FilePath ?? "",
            Types: typeMetricsList)
        {
            Diagnostics = diagnosticSummary
        };

        return projectMetrics;
    }

    private async Task<ProjectMetrics> AnalyzeProjectAsync(Project project, System.Collections.Concurrent.ConcurrentDictionary<string, Compilation> compilationCache, CancellationToken cancellationToken)
    {
        // For single project analysis, create a temporary collector that we don't use
        using var tempCollector = new InMemoryDependencyCollector();
        return await AnalyzeProjectWithCouplingAsync(project, compilationCache, tempCollector, cancellationToken);
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
        var dit = _metricsCalculator.CalculateDepthOfInheritance(typeSymbol);

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

        var metrics = _metricsCalculator.CalculateUnifiedMetrics(root);
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
            var metrics = _metricsCalculator.CalculateUnifiedMetrics(method);
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
            var metrics = _metricsCalculator.CalculateUnifiedMetrics(ctor);
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
            var metrics = _metricsCalculator.CalculateUnifiedMetrics(localFunc);
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

}
