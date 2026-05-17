using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Analysis.Logging;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Analyzes coupling between projects, assemblies, namespaces, and types.
/// </summary>
public sealed class CouplingAnalyzer : ICouplingAnalyzer
{
    private readonly ILogger<CouplingAnalyzer> _logger;

    public CouplingAnalyzer(ILogger<CouplingAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CouplingAnalysis> AnalyzeSolutionAsync(
        Solution solution,
        IReadOnlyDictionary<string, Compilation>? compilationCache = null,
        CancellationToken cancellationToken = default)
    {
        // Analyze project-to-project dependencies
        var projectDependencies = AnalyzeProjectDependencies(solution);

        // Analyze each project for internal coupling in parallel
        var csharpProjects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp).ToList();
        var dependenciesBag = new ConcurrentBag<List<DependencyEdge>>();
        var completedCount = 0;
        var totalProjects = csharpProjects.Count;

        await Parallel.ForEachAsync(csharpProjects, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        }, async (project, ct) =>
        {
            var currentIndex = Interlocked.Increment(ref completedCount);
            CouplingAnalyzerLog.AnalyzingCouplingInProject(_logger, currentIndex, totalProjects, project.Name);

            var projectCoupling = await AnalyzeProjectInternalCouplingAsync(project, compilationCache, ct);
            dependenciesBag.Add(projectCoupling.dependencies);

            CouplingAnalyzerLog.ProjectDependenciesFound(_logger, project.Name, projectCoupling.dependencies.Count);
        });

        // Merge all dependencies
        var allDependencies = new List<DependencyEdge>(projectDependencies);
        foreach (var deps in dependenciesBag)
        {
            allDependencies.AddRange(deps);
        }

        return BuildCouplingAnalysisFromDependencies(solution, allDependencies);
    }

    /// <inheritdoc />
    public CouplingAnalysis BuildCouplingAnalysisFromDependencies(
        Solution solution,
        IReadOnlyList<DependencyEdge> allDependencies)
    {
        var projectDependencies = AnalyzeProjectDependencies(solution);
        var combinedDependencies = AddMissingProjectDependencies(projectDependencies, allDependencies);

        // Always analyze all dependencies (no filtering)
        DependencyEdge.EnableDetails = true;

        // Aggregate duplicate edges to reduce memory pressure
        var aggregatedDependencies = AggregateDependencies(combinedDependencies);

        // Build coupling metrics from aggregated dependencies
        var projectCouplingMetrics = BuildProjectCouplingMetrics(solution, aggregatedDependencies);
        var namespaceCouplingMetrics = BuildNamespaceCouplingMetrics(aggregatedDependencies);
        var typeCouplingMetrics = BuildTypeCouplingMetrics(aggregatedDependencies);

        var summary = BuildCouplingSummary(projectCouplingMetrics, namespaceCouplingMetrics, typeCouplingMetrics, aggregatedDependencies);

        return new CouplingAnalysis(
            AnalyzedEntity: solution.FilePath ?? "Solution",
            AnalyzedAt: DateTime.UtcNow)
        {
            ProjectCoupling = projectCouplingMetrics,
            NamespaceCoupling = namespaceCouplingMetrics,
            TypeCoupling = typeCouplingMetrics,
            AllDependencies = aggregatedDependencies,
            Summary = summary
        };
    }

    /// <inheritdoc />
    public CouplingAnalysis BuildCouplingAnalysisFromCollector(
        Solution solution,
        IDependencyCollector collector)
    {
        // Get already-aggregated dependencies from collector
        var aggregatedDependencies = collector.GetAggregatedDependencies();

        // Use existing method to build analysis from these edges
        return BuildCouplingAnalysisFromDependencies(solution, aggregatedDependencies);
    }

    /// <inheritdoc />
    public async Task<CouplingAnalysis> AnalyzeProjectCouplingAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        var allDependencies = new List<DependencyEdge>();
        // Analyze internal coupling within the project (no compilation cache for single project)
        var projectCoupling = await AnalyzeProjectInternalCouplingAsync(project, null, cancellationToken);
        allDependencies.AddRange(projectCoupling.dependencies);

        DependencyEdge.EnableDetails = true;

        // Aggregate duplicate edges to reduce memory pressure
        var aggregatedDependencies = AggregateDependencies(allDependencies);

        // Build coupling metrics from aggregated dependencies
        var namespaceCouplingMetrics = BuildNamespaceCouplingMetrics(aggregatedDependencies);
        var typeCouplingMetrics = BuildTypeCouplingMetrics(aggregatedDependencies);

        // For single project, create a simple project coupling metric
        var projectCouplingMetrics = new List<CouplingMetrics>
        {
            new(project.Name, DependencyType.ProjectReference)
            {
                InternalOutbound = [],
                InternalInbound = [],
                ExternalOutbound = []
            }
        };

        var summary = BuildCouplingSummary(projectCouplingMetrics, namespaceCouplingMetrics, typeCouplingMetrics, aggregatedDependencies);

        return new CouplingAnalysis(
            AnalyzedEntity: project.FilePath ?? project.Name,
            AnalyzedAt: DateTime.UtcNow)
        {
            ProjectCoupling = projectCouplingMetrics,
            NamespaceCoupling = namespaceCouplingMetrics,
            TypeCoupling = typeCouplingMetrics,
            AllDependencies = aggregatedDependencies,
            Summary = summary
        };
    }

    /// <summary>
    /// Analyzes project-to-project dependencies based on project references.
    /// </summary>
    private List<DependencyEdge> AnalyzeProjectDependencies(Solution solution)
    {
        var dependencies = new List<DependencyEdge>();

        foreach (var project in solution.Projects)
        {
            foreach (var projectRef in project.ProjectReferences)
            {
                var referencedProject = solution.GetProject(projectRef.ProjectId);
                if (referencedProject != null)
                {
                    dependencies.Add(new DependencyEdge(
                        FromEntity: project.Name,
                        ToEntity: referencedProject.Name,
                        Type: DependencyType.ProjectReference,
                        ReferenceCount: 1)
                    {
                        SourceLocation = project.FilePath
                    });
                }
            }
        }

        return dependencies;
    }

    private static List<DependencyEdge> AddMissingProjectDependencies(
        IReadOnlyList<DependencyEdge> projectDependencies,
        IReadOnlyList<DependencyEdge> allDependencies)
    {
        var suppliedProjectReferenceKeys = allDependencies
            .Where(d => d.Type == DependencyType.ProjectReference)
            .Select(d => (d.FromEntity, d.ToEntity, d.Type))
            .ToHashSet();

        var combinedDependencies = new List<DependencyEdge>(projectDependencies.Count + allDependencies.Count);
        foreach (var projectDependency in projectDependencies)
        {
            var key = (projectDependency.FromEntity, projectDependency.ToEntity, projectDependency.Type);
            if (!suppliedProjectReferenceKeys.Contains(key))
            {
                combinedDependencies.Add(projectDependency);
            }
        }

        combinedDependencies.AddRange(allDependencies);
        return combinedDependencies;
    }

    /// <inheritdoc />
    public async Task<(List<CouplingMetrics> namespaceCoupling, List<DependencyEdge> dependencies)> AnalyzeProjectInternalCouplingAsync(
        Project project,
        IReadOnlyDictionary<string, Compilation>? compilationCache = null,
        CancellationToken cancellationToken = default)
    {
        // Use cached compilation if available, otherwise fetch it
        Compilation? compilation;
        if (compilationCache != null && compilationCache.TryGetValue(project.Name, out var cachedCompilation))
        {
            compilation = cachedCompilation;
        }
        else
        {
            compilation = await project.GetCompilationAsync(cancellationToken);
        }

        if (compilation == null)
        {
            CouplingAnalyzerLog.CouldNotGetCompilation(_logger, project.Name);
            return ([], []);
        }

        var documents = project.Documents.Where(d => d.SourceCodeKind == SourceCodeKind.Regular).ToList();
        var documentCount = documents.Count;

        CouplingAnalyzerLog.AnalyzingDocumentsForCoupling(_logger, documentCount, project.Name);

        // Analyze documents in parallel for performance
        var dependenciesBag = new ConcurrentBag<List<DependencyEdge>>();
        var processedCount = 0;

        await Parallel.ForEachAsync(documents, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        }, async (document, ct) =>
        {
            var currentCount = Interlocked.Increment(ref processedCount);
            if (currentCount % 100 == 0)
            {
                CouplingAnalyzerLog.CouplingAnalysisProgress(_logger, currentCount, documentCount, project.Name);
            }

            var syntaxTree = await document.GetSyntaxTreeAsync(ct);
            var semanticModel = await document.GetSemanticModelAsync(ct);
            if (syntaxTree == null || semanticModel == null) return;

            var root = await syntaxTree.GetRootAsync(ct);
            var analyzer = new DocumentCouplingAnalyzer(semanticModel, document.FilePath ?? "", root, null);
            analyzer.Visit(root);
            dependenciesBag.Add(analyzer.Dependencies.ToList());
        });

        // Merge all dependencies
        var dependencies = new List<DependencyEdge>();
        foreach (var deps in dependenciesBag)
        {
            dependencies.AddRange(deps);
        }

        // Group and build metrics at namespace level
        var namespaceCoupling = BuildNamespaceCouplingMetrics(dependencies);

        return (namespaceCoupling, dependencies);
    }

    private List<CouplingMetrics> BuildProjectCouplingMetrics(Solution solution, List<DependencyEdge> allDependencies)
    {
        var projectNames = solution.Projects.Select(p => p.Name).ToHashSet();
        var metrics = new List<CouplingMetrics>();

        // Filter to only ProjectReference dependencies for internal project-to-project coupling
        var projectDeps = allDependencies
            .Where(d => d.Type == DependencyType.ProjectReference)
            .ToList();

        // Pre-group dependencies by FromEntity and ToEntity for O(1) lookups
        var outboundByFrom = projectDeps
            .GroupBy(d => d.FromEntity)
            .ToDictionary(g => g.Key, g => g.ToList());
        var inboundByTo = projectDeps
            .GroupBy(d => d.ToEntity)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var projectName in projectNames)
        {
            var outbound = outboundByFrom.GetValueOrDefault(projectName, []);
            var inbound = inboundByTo.GetValueOrDefault(projectName, []);

            var internalOut = outbound.Where(d => projectNames.Contains(d.ToEntity)).ToList();
            var internalIn = inbound.Where(d => projectNames.Contains(d.FromEntity)).ToList();

            // External dependencies are now tracked via PackageReferences on ProjectMetrics,
            // not via AssemblyReference edges. ExternalOutbound stays empty at the coupling level.
            metrics.Add(new CouplingMetrics(projectName, DependencyType.ProjectReference)
            {
                InternalOutbound = internalOut,
                InternalInbound = internalIn,
                ExternalOutbound = []
            });
        }

        return metrics;
    }

    private List<CouplingMetrics> BuildNamespaceCouplingMetrics(List<DependencyEdge> allDependencies)
    {
        return BuildEntityCouplingMetrics(allDependencies, DependencyType.NamespaceReference);
    }

    private List<CouplingMetrics> BuildTypeCouplingMetrics(List<DependencyEdge> allDependencies)
    {
        return BuildEntityCouplingMetrics(allDependencies, DependencyType.TypeReference);
    }

    private static List<CouplingMetrics> BuildEntityCouplingMetrics(
        List<DependencyEdge> allDependencies,
        DependencyType dependencyType)
    {
        var entityDependencies = allDependencies.Where(d => d.Type == dependencyType).ToList();

        var internalEntities = entityDependencies.Select(d => d.FromEntity).Distinct().ToHashSet();

        var outboundByEntity = entityDependencies
            .GroupBy(d => d.FromEntity)
            .ToDictionary(g => g.Key, g => g.ToList());
        var inboundByEntity = entityDependencies
            .GroupBy(d => d.ToEntity)
            .ToDictionary(g => g.Key, g => g.ToList());

        var entities = outboundByEntity.Keys.Union(inboundByEntity.Keys).ToList();

        return entities.Select(entity =>
        {
            var outbound = outboundByEntity.GetValueOrDefault(entity, []);
            var inbound = inboundByEntity.GetValueOrDefault(entity, []);

            var (internalOut, externalOut) = SplitDependenciesByInternal(outbound, internalEntities, d => d.ToEntity);
            var internalIn = inbound.Where(d => internalEntities.Contains(d.FromEntity)).ToList();

            return new CouplingMetrics(entity, dependencyType)
            {
                InternalOutbound = internalOut,
                InternalInbound = internalIn,
                ExternalOutbound = externalOut
            };
        }).ToList();
    }

    /// <summary>
    /// Splits a list of dependencies into internal and external in a single pass.
    /// Avoids multiple iterations through the same list.
    /// </summary>
    private static (List<DependencyEdge> Internal, List<DependencyEdge> External) SplitDependenciesByInternal(
        IEnumerable<DependencyEdge> dependencies,
        HashSet<string> internalEntities,
        Func<DependencyEdge, string> entitySelector)
    {
        var internalList = new List<DependencyEdge>();
        var externalList = new List<DependencyEdge>();

        foreach (var dep in dependencies)
        {
            var entity = entitySelector(dep);
            if (internalEntities.Contains(entity))
                internalList.Add(dep);
            else
                externalList.Add(dep);
        }

        return (internalList, externalList);
    }

    private List<DependencyEdge> AggregateDependencies(List<DependencyEdge> dependencies)
    {
        if (dependencies.Count == 0) return dependencies;

        var aggregated = new Dictionary<(string From, string To, DependencyType Type), int>(dependencies.Count);
        foreach (var d in dependencies)
        {
            var key = (d.FromEntity, d.ToEntity, d.Type);
            if (aggregated.TryGetValue(key, out var current))
                aggregated[key] = current + d.ReferenceCount;
            else
                aggregated[key] = d.ReferenceCount;
        }

        var result = new List<DependencyEdge>(aggregated.Count);
        foreach (var (key, count) in aggregated)
        {
            result.Add(new DependencyEdge(key.From, key.To, key.Type, count));
        }

        return result;
    }

    private CouplingSummary BuildCouplingSummary(
        List<CouplingMetrics> projectCoupling,
        List<CouplingMetrics> namespaceCoupling,
        List<CouplingMetrics> typeCoupling,
        List<DependencyEdge> allDependencies)
    {
        var allMetrics = projectCoupling.Concat(namespaceCoupling).Concat(typeCoupling).ToList();

        return new CouplingSummary
        {
            TotalDependencies = allDependencies.Count,
            AverageInternalDependencies = allMetrics.Count > 0 ? allMetrics.Average(m => m.InternalDependencies) : 0,
            AverageInternalDependents = allMetrics.Count > 0 ? allMetrics.Average(m => m.InternalDependents) : 0,
            AverageDependencyRatio = allMetrics.Count > 0 ? allMetrics.Average(m => m.DependencyRatio) : 0,
            AverageExternalDependencies = allMetrics.Count > 0 ? allMetrics.Average(m => m.TotalExternalDependencies) : 0,
            AverageExternalBclDependencies = allMetrics.Count > 0 ? allMetrics.Average(m => m.ExternalBclDependencies) : 0,
            AverageExternalPackageDependencies = allMetrics.Count > 0 ? allMetrics.Average(m => m.ExternalPackageDependencies) : 0,
            MostCoupledEntity = allMetrics.MaxBy(m => m.TotalReferenceStrength)?.EntityName,
            MostDependentEntity = allMetrics.MaxBy(m => m.InternalDependents)?.EntityName,
            HighestConsumerEntity = allMetrics.MaxBy(m => m.DependencyRatio)?.EntityName,
            CouplingMode = "All"
        };
    }

    /// <summary>
    /// Analyzes a single document for coupling dependencies. Can be called from external analyzers.
    /// This is a static helper method for use in SolutionAnalyzer.
    /// </summary>
    public static IReadOnlyList<DependencyEdge> AnalyzeDocumentCoupling(SemanticModel semanticModel, string filePath, SyntaxNode root)
    {
        var analyzer = new DocumentCouplingAnalyzer(semanticModel, filePath, root, null);
        analyzer.Visit(root);
        return analyzer.Dependencies;
    }

    /// <summary>
    /// Analyzes a single document for coupling dependencies, streaming to a collector.
    /// Memory-efficient version for large codebases.
    /// </summary>
    public static void AnalyzeDocumentCouplingStreaming(
        SemanticModel semanticModel,
        string filePath,
        SyntaxNode root,
        IDependencyCollector collector)
    {
        var analyzer = new DocumentCouplingAnalyzer(semanticModel, filePath, root, collector);
        analyzer.Visit(root);
    }
}
