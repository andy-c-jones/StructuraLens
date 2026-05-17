using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
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
    private readonly DocumentMetricsAnalyzer _documentMetricsAnalyzer;
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
        _documentMetricsAnalyzer = new DocumentMetricsAnalyzer(metricsCalculator);
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
                _options.SQLiteBatchSize,
                migrationLogger: message => SolutionAnalyzerLog.DependencyCollectorMigration(_logger, message)),
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
        RegisterWorkspaceWarnings(workspace);

        var solution = await workspace.OpenSolutionAsync(fullPath, cancellationToken: cancellationToken);
        var csharpProjects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp).ToList();
        SolutionAnalyzerLog.LoadedSolutionWithProjects(_logger, csharpProjects.Count);

        var compilationCache = await CompilationCacheBuilder.BuildAsync(
            csharpProjects,
            _warnings,
            _logger,
            cancellationToken);

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

        return CreateReport(
            fullPath,
            projectMetricsList.ToList(),
            couplingAnalysis,
            collectorStats);
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
        RegisterWorkspaceWarnings(workspace);

        var project = await workspace.OpenProjectAsync(fullPath, cancellationToken: cancellationToken);
        SolutionAnalyzerLog.AnalyzingProjectSingle(_logger, project.Name);

        // For single project, create an empty cache (compilation will be fetched on demand)
        var compilationCache = new ConcurrentDictionary<string, Compilation>();
        var projectMetrics = IsDiagnosticsAndReferencesMode
            ? await AnalyzeProjectDiagnosticsAndReferencesAsync(project, compilationCache, cancellationToken, concurrentAnalyzerExecution: true)
            : await AnalyzeProjectAsync(project, compilationCache, cancellationToken);

        CouplingAnalysis? couplingAnalysis = null;
        if (!IsDiagnosticsAndReferencesMode)
        {
            SolutionAnalyzerLog.AnalyzingProjectCoupling(_logger);
            couplingAnalysis = await _couplingAnalyzer.AnalyzeProjectCouplingAsync(project, cancellationToken);
        }

        return CreateReport(fullPath, [projectMetrics], couplingAnalysis);
    }

    private async Task<ProjectMetrics> AnalyzeProjectWithCouplingAsync(
        Project project,
        ConcurrentDictionary<string, Compilation> compilationCache,
        IDependencyCollector dependencyCollector,
        CancellationToken cancellationToken,
        bool concurrentAnalyzerExecution)
    {
        var compilation = await GetCompilationAsync(project, compilationCache, cancellationToken);
        if (compilation == null)
        {
            return CreateEmptyProjectMetrics(project);
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

            foreach (var typeMetrics in _documentMetricsAnalyzer.Analyze(root, semanticModel, filePath))
            {
                typeMetricsBag.Add(typeMetrics);
            }
        });

        var typeMetricsList = typeMetricsBag.ToList();
        var packageReferences = ReadPackageReferences(project.FilePath);
        var projectReferences = ProjectReferenceResolver.GetProjectReferenceNames(project);

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

    private async Task<ProjectMetrics> AnalyzeProjectAsync(Project project, ConcurrentDictionary<string, Compilation> compilationCache, CancellationToken cancellationToken)
    {
        // For single project analysis, create a temporary collector that we don't use
        using var tempCollector = new InMemoryDependencyCollector();
        return await AnalyzeProjectWithCouplingAsync(project, compilationCache, tempCollector, cancellationToken, concurrentAnalyzerExecution: true);
    }

    private async Task<ProjectMetrics> AnalyzeProjectDiagnosticsAndReferencesAsync(
        Project project,
        ConcurrentDictionary<string, Compilation> compilationCache,
        CancellationToken cancellationToken,
        bool concurrentAnalyzerExecution)
    {
        var compilation = await GetCompilationAsync(project, compilationCache, cancellationToken);
        if (compilation == null)
        {
            return CreateEmptyProjectMetrics(project);
        }

        var diagnosticSummary = await DiagnosticCollector.CollectAsync(project, compilation, cancellationToken, concurrentAnalyzerExecution);
        var packageReferences = ReadPackageReferences(project.FilePath);
        var projectReferences = ProjectReferenceResolver.GetProjectReferenceNames(project);

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

    private void RegisterWorkspaceWarnings(MSBuildWorkspace workspace)
    {
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning)
            {
                _warnings.Add($"Workspace warning: {e.Diagnostic.Message}");
                SolutionAnalyzerLog.WorkspaceWarning(_logger, e.Diagnostic.Message);
            }
        });
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
            return ProjectReferenceResolver.GetDirectReferenceIncludes(projectFilePath, "PackageReference");
        }
        catch (Exception ex)
        {
            _warnings.Add($"Could not read package references from {projectFilePath}: {ex.Message}");
            return [];
        }
    }

    private async Task<Compilation?> GetCompilationAsync(
        Project project,
        ConcurrentDictionary<string, Compilation> compilationCache,
        CancellationToken cancellationToken)
    {
        if (compilationCache.TryGetValue(project.Name, out var compilation))
        {
            return compilation;
        }

        SolutionAnalyzerLog.GettingCompilationForProject(_logger, project.Name);
        compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null)
        {
            _warnings.Add($"Could not get compilation for project: {project.Name}");
            SolutionAnalyzerLog.CouldNotGetCompilation(_logger, project.Name);
        }

        return compilation;
    }

    private static ProjectMetrics CreateEmptyProjectMetrics(Project project)
    {
        return new ProjectMetrics(project.Name, project.FilePath ?? "", []);
    }

    private AnalysisReport CreateReport(
        string fullPath,
        IReadOnlyList<ProjectMetrics> projects,
        CouplingAnalysis? couplingAnalysis,
        DependencyCollectorStats? collectorStats = null)
    {
        return new AnalysisReport(
            SolutionPath: fullPath,
            AnalyzedAt: DateTime.UtcNow,
            Projects: projects,
            Warnings: _warnings.ToList(),
            ToolVersion: _options.ToolVersion,
            AnalysisMode: _options.AnalysisMode)
        {
            CouplingAnalysis = couplingAnalysis,
            AggregationStats = collectorStats,
            GitInfo = CreateGitRepositoryInfo(fullPath)
        };
    }

    private GitRepositoryInfo? CreateGitRepositoryInfo(string fullPath)
    {
        var gitMetadata = _gitService.GetGitMetadata(fullPath);
        if (gitMetadata == null)
        {
            return null;
        }

        return new GitRepositoryInfo(
            CommitSha: gitMetadata.CommitSha,
            BranchName: gitMetadata.BranchName,
            RemoteUrl: gitMetadata.RemoteUrl,
            IsDirty: gitMetadata.IsDirty);
    }
}
