using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Analysis.Logging;
using StructuraLens.Core.Infrastructure;
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
    private readonly IGitRepositoryService _gitService;
    private readonly AnalysisOptions _options;
    private bool IsDiagnosticsAndReferencesMode => _options.AnalysisMode == AnalysisMode.DiagnosticsAndReferences;

    public SolutionAnalyzer(
        ILogger<SolutionAnalyzer> logger,
        INuGetRestorer nugetRestorer,
        IMSBuildWorkspaceFactory workspaceFactory,
        ICouplingAnalyzer couplingAnalyzer,
        IMetricsCalculator metricsCalculator,
        IFileSystemService fileSystem,
        IGitRepositoryService gitService,
        AnalysisOptions? options = null)
    {
        _logger = logger;
        _nugetRestorer = nugetRestorer;
        _workspaceFactory = workspaceFactory;
        _couplingAnalyzer = couplingAnalyzer;
        _metricsCalculator = metricsCalculator;
        _fileSystem = fileSystem;
        _gitService = gitService;
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
        // Use streaming dependency collector to reduce memory usage in full mode only.
        using var dependencyCollector = IsDiagnosticsAndReferencesMode ? null : CreateDependencyCollector();
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

            var metrics = IsDiagnosticsAndReferencesMode
                ? await AnalyzeProjectDiagnosticsAndReferencesAsync(project, compilationCache, ct, concurrentAnalyzerExecution: false)
                : await AnalyzeProjectWithCouplingAsync(project, compilationCache, dependencyCollector!, ct, concurrentAnalyzerExecution: false);
            projectMetricsList.Add(metrics);

            if (IsDiagnosticsAndReferencesMode)
            {
                SolutionAnalyzerLog.CompletedProjectLightweight(_logger, project.Name);
            }
            else
            {
                SolutionAnalyzerLog.CompletedProject(_logger, project.Name, metrics.Types.Count, metrics.TotalMethods);
            }
        });

        CouplingAnalysis? couplingAnalysis = null;
        DependencyCollectorStats? collectorStats = null;
        if (!IsDiagnosticsAndReferencesMode && dependencyCollector != null)
        {
            // Build coupling analysis from streaming collector (already aggregated)
            collectorStats = dependencyCollector.GetStats();
            SolutionAnalyzerLog.BuildingCouplingAnalysis(_logger, (int)collectorStats.UniqueEdgesCount);
            couplingAnalysis = _couplingAnalyzer.BuildCouplingAnalysisFromCollector(solution, dependencyCollector);
        }

        if (IsDiagnosticsAndReferencesMode)
        {
            SolutionAnalyzerLog.AnalysisCompleteLightweight(_logger, projectMetricsList.Count);
        }
        else
        {
            SolutionAnalyzerLog.AnalysisComplete(_logger, projectMetricsList.Count, projectMetricsList.Sum(p => p.Types.Count), projectMetricsList.Sum(p => p.TotalMethods));
        }

        // Collect git metadata for the analyzed solution
        var gitMetadata = _gitService.GetGitMetadata(fullPath);
        GitRepositoryInfo? gitInfo = null;
        if (gitMetadata != null)
        {
            gitInfo = new GitRepositoryInfo(
                CommitSha: gitMetadata.CommitSha,
                BranchName: gitMetadata.BranchName,
                RemoteUrl: gitMetadata.RemoteUrl,
                IsDirty: gitMetadata.IsDirty);
        }

        return new AnalysisReport(
            SolutionPath: fullPath,
            AnalyzedAt: DateTime.UtcNow,
            Projects: projectMetricsList.ToList(),
            Warnings: _warnings.ToList(),
            ToolVersion: _options.ToolVersion,
            AnalysisMode: _options.AnalysisMode)
        {
            CouplingAnalysis = couplingAnalysis,
            AggregationStats = collectorStats,
            GitInfo = gitInfo
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
        var projectMetrics = IsDiagnosticsAndReferencesMode
            ? await AnalyzeProjectDiagnosticsAndReferencesAsync(project, compilationCache, cancellationToken, concurrentAnalyzerExecution: true)
            : await AnalyzeProjectAsync(project, compilationCache, cancellationToken);

        CouplingAnalysis? couplingAnalysis = null;
        if (!IsDiagnosticsAndReferencesMode)
        {
            SolutionAnalyzerLog.AnalyzingProjectCoupling(_logger);
            couplingAnalysis = await _couplingAnalyzer.AnalyzeProjectCouplingAsync(project, cancellationToken);
        }

        // Collect git metadata for the analyzed project
        var gitMetadata = _gitService.GetGitMetadata(fullPath);
        GitRepositoryInfo? gitInfo = null;
        if (gitMetadata != null)
        {
            gitInfo = new GitRepositoryInfo(
                CommitSha: gitMetadata.CommitSha,
                BranchName: gitMetadata.BranchName,
                RemoteUrl: gitMetadata.RemoteUrl,
                IsDirty: gitMetadata.IsDirty);
        }

        return new AnalysisReport(
            SolutionPath: fullPath,
            AnalyzedAt: DateTime.UtcNow,
            Projects: [projectMetrics],
            Warnings: _warnings.ToList(),
            ToolVersion: _options.ToolVersion,
            AnalysisMode: _options.AnalysisMode)
        {
            CouplingAnalysis = couplingAnalysis,
            GitInfo = gitInfo
        };
    }

    private async Task<ProjectMetrics> AnalyzeProjectWithCouplingAsync(
        Project project,
        System.Collections.Concurrent.ConcurrentDictionary<string, Compilation> compilationCache,
        IDependencyCollector dependencyCollector,
        CancellationToken cancellationToken,
        bool concurrentAnalyzerExecution)
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
        var diagnosticSummary = await DiagnosticCollector.CollectAsync(project, compilation, cancellationToken, concurrentAnalyzerExecution);

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
        var packageReferences = ReadPackageReferences(project.FilePath);
        var projectReferences = GetProjectReferenceNames(project);

        var projectMetrics = new ProjectMetrics(
            Name: project.Name,
            FilePath: project.FilePath ?? "",
            Types: typeMetricsList)
        {
            Diagnostics = diagnosticSummary,
            PackageReferences = packageReferences,
            ProjectReferences = projectReferences
        };

        return projectMetrics;
    }

    private async Task<ProjectMetrics> AnalyzeProjectAsync(Project project, System.Collections.Concurrent.ConcurrentDictionary<string, Compilation> compilationCache, CancellationToken cancellationToken)
    {
        // For single project analysis, create a temporary collector that we don't use
        using var tempCollector = new InMemoryDependencyCollector();
        return await AnalyzeProjectWithCouplingAsync(project, compilationCache, tempCollector, cancellationToken, concurrentAnalyzerExecution: true);
    }

    private async Task<ProjectMetrics> AnalyzeProjectDiagnosticsAndReferencesAsync(
        Project project,
        System.Collections.Concurrent.ConcurrentDictionary<string, Compilation> compilationCache,
        CancellationToken cancellationToken,
        bool concurrentAnalyzerExecution)
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

        var diagnosticSummary = await DiagnosticCollector.CollectAsync(project, compilation, cancellationToken, concurrentAnalyzerExecution);
        var packageReferences = ReadPackageReferences(project.FilePath);
        var projectReferences = GetProjectReferenceNames(project);

        return new ProjectMetrics(
            Name: project.Name,
            FilePath: project.FilePath ?? "",
            Types: [])
        {
            Diagnostics = diagnosticSummary,
            PackageReferences = packageReferences,
            ProjectReferences = projectReferences
        };
    }

    private static List<string> GetProjectReferenceNames(Project project)
    {
        var projectFilePath = project.FilePath;
        if (string.IsNullOrWhiteSpace(projectFilePath) || !File.Exists(projectFilePath))
        {
            return [];
        }

        var solutionProjectsByPath = project.Solution.Projects
            .Where(p => !string.IsNullOrWhiteSpace(p.FilePath))
            .ToDictionary(
                p => Path.GetFullPath(p.FilePath!),
                p => p.Name,
                StringComparer.OrdinalIgnoreCase);

        var projectDirectory = Path.GetDirectoryName(projectFilePath)!;

        return GetDirectReferenceIncludes(projectFilePath, "ProjectReference")
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include)))
            .Select(path => solutionProjectsByPath.TryGetValue(path, out var name)
                ? name
                : Path.GetFileNameWithoutExtension(path))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Reads direct PackageReference items from a .csproj file.
    /// </summary>
    private List<string> ReadPackageReferences(string? projectFilePath)
    {
        if (string.IsNullOrEmpty(projectFilePath) || !File.Exists(projectFilePath))
        {
            return [];
        }

        try
        {
            return GetDirectReferenceIncludes(projectFilePath, "PackageReference");
        }
        catch (Exception ex)
        {
            _warnings.Add($"Could not read package references from {projectFilePath}: {ex.Message}");
            return [];
        }
    }

    private static List<string> GetDirectReferenceIncludes(string projectFilePath, string itemName)
    {
        var document = XDocument.Load(projectFilePath);

        return document.Descendants()
            .Where(e => string.Equals(e.Name.LocalName, itemName, StringComparison.Ordinal))
            .Select(e => e.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

        var (cc, loc, halsteadVolume, mi) = CalculateCodeMetrics(
            root,
            hasBody: topLevelStatements.Count > 0,
            expressionBodiedFallbackLoc: 1);

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

        var (cc, loc, halsteadVolume, mi) = CalculateCodeMetrics(
            method,
            hasBody: method.Body != null || method.ExpressionBody != null,
            expressionBodiedFallbackLoc: method.Body != null ? 0 : 1);

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

        var (cc, loc, halsteadVolume, mi) = CalculateCodeMetrics(
            ctor,
            hasBody: ctor.Body != null || ctor.ExpressionBody != null,
            expressionBodiedFallbackLoc: ctor.Body != null ? 0 : 1);

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

        var (cc, loc, halsteadVolume, mi) = CalculateCodeMetrics(
            localFunc,
            hasBody: localFunc.Body != null || localFunc.ExpressionBody != null,
            expressionBodiedFallbackLoc: localFunc.Body != null ? 0 : 1);

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

    private (int CyclomaticComplexity, int LinesOfCode, double HalsteadVolume, double MaintainabilityIndex) CalculateCodeMetrics(
        SyntaxNode node,
        bool hasBody,
        int expressionBodiedFallbackLoc)
    {
        if (IsDiagnosticsAndReferencesMode)
        {
            // Lightweight mode intentionally avoids unified metrics traversal.
            return (0, 0, 0, 0);
        }

        if (!hasBody)
        {
            return (1, 0, 0, 100.0);
        }

        var metrics = _metricsCalculator.CalculateUnifiedMetrics(node);
        var loc = metrics.LinesOfCode > 0 ? metrics.LinesOfCode : expressionBodiedFallbackLoc;
        return (
            metrics.CyclomaticComplexity,
            loc,
            metrics.HalsteadVolume,
            metrics.MaintainabilityIndex);
    }

}
