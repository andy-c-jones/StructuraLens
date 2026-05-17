using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        var projectNames = solution.Projects.Select(p => p.Name).ToList();

        // Analyze project-to-project dependencies
        var projectDependencies = AnalyzeProjectDependencies(solution);

        // Analyze each project for internal coupling in parallel
        var csharpProjects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp).ToList();
        var dependenciesBag = new System.Collections.Concurrent.ConcurrentBag<List<DependencyEdge>>();
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
        var projectNames = solution.Projects.Select(p => p.Name).ToList();

        // Add project-to-project dependencies if not already included
        var projectDependencies = AnalyzeProjectDependencies(solution);
        var combinedDependencies = new List<DependencyEdge>(projectDependencies);
        combinedDependencies.AddRange(allDependencies);

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
        var projectNames = new List<string> { project.Name };

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
        var dependenciesBag = new System.Collections.Concurrent.ConcurrentBag<List<DependencyEdge>>();
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
        var namespaceDeps = allDependencies.Where(d => d.Type == DependencyType.NamespaceReference).ToList();

        // Determine which namespaces are "internal" (appear as FromEntity, meaning they're in our codebase)
        var internalNamespaces = namespaceDeps.Select(d => d.FromEntity).Distinct().ToHashSet();

        // Pre-group by FromEntity and ToEntity for O(1) lookups
        var outboundByEntity = namespaceDeps
            .GroupBy(d => d.FromEntity)
            .ToDictionary(g => g.Key, g => g.ToList());
        var inboundByEntity = namespaceDeps
            .GroupBy(d => d.ToEntity)
            .ToDictionary(g => g.Key, g => g.ToList());

        var namespaces = outboundByEntity.Keys.Union(inboundByEntity.Keys).ToList();

        return namespaces.Select(ns =>
        {
            var outbound = outboundByEntity.GetValueOrDefault(ns, []);
            var inbound = inboundByEntity.GetValueOrDefault(ns, []);

            // Split into internal and external
            var internalOut = outbound.Where(d => internalNamespaces.Contains(d.ToEntity)).ToList();
            var externalOut = outbound.Where(d => !internalNamespaces.Contains(d.ToEntity)).ToList();
            var internalIn = inbound.Where(d => internalNamespaces.Contains(d.FromEntity)).ToList();

            return new CouplingMetrics(ns, DependencyType.NamespaceReference)
            {
                InternalOutbound = internalOut,
                InternalInbound = internalIn,
                ExternalOutbound = externalOut
            };
        }).ToList();
    }

    private List<CouplingMetrics> BuildTypeCouplingMetrics(List<DependencyEdge> allDependencies)
    {
        var typeDeps = allDependencies.Where(d => d.Type == DependencyType.TypeReference).ToList();

        // Determine which types are "internal" (appear as FromEntity, meaning they're in our codebase)
        var internalTypes = typeDeps.Select(d => d.FromEntity).Distinct().ToHashSet();

        // Pre-group by FromEntity and ToEntity for O(1) lookups
        var outboundByEntity = typeDeps
            .GroupBy(d => d.FromEntity)
            .ToDictionary(g => g.Key, g => g.ToList());
        var inboundByEntity = typeDeps
            .GroupBy(d => d.ToEntity)
            .ToDictionary(g => g.Key, g => g.ToList());

        var types = outboundByEntity.Keys.Union(inboundByEntity.Keys).ToList();

        return types.Select(type =>
        {
            var outbound = outboundByEntity.GetValueOrDefault(type, []);
            var inbound = inboundByEntity.GetValueOrDefault(type, []);

            // Split into internal and external
            var internalOut = outbound.Where(d => internalTypes.Contains(d.ToEntity)).ToList();
            var externalOut = outbound.Where(d => !internalTypes.Contains(d.ToEntity)).ToList();
            var internalIn = inbound.Where(d => internalTypes.Contains(d.FromEntity)).ToList();

            return new CouplingMetrics(type, DependencyType.TypeReference)
            {
                InternalOutbound = internalOut,
                InternalInbound = internalIn,
                ExternalOutbound = externalOut
            };
        }).ToList();
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

/// <summary>
/// Syntax walker that analyzes coupling within a single document.
/// </summary>
internal sealed class DocumentCouplingAnalyzer : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly string _filePath;
    private readonly List<DependencyEdge>? _dependencies;
    private readonly IDependencyCollector? _collector;
    private readonly string? _primaryNamespace;

    // Cache for ToDisplayString() results to avoid repeated expensive calls
    private readonly Dictionary<ISymbol, string> _symbolDisplayCache = new(SymbolEqualityComparer.Default);
    // Cache containing type per TypeDeclarationSyntax to avoid repeated lookups
    private readonly Dictionary<TypeDeclarationSyntax, string?> _containingTypeCache = [];

    /// <summary>
    /// Gets collected dependencies. Only valid when using list-based collection (not streaming).
    /// </summary>
    public IReadOnlyList<DependencyEdge> Dependencies => _dependencies ?? [];

    /// <summary>
    /// Constructor for list-based collection (backward compatibility).
    /// </summary>
    public DocumentCouplingAnalyzer(SemanticModel semanticModel, string filePath, SyntaxNode root)
        : this(semanticModel, filePath, root, null)
    {
    }

    /// <summary>
    /// Constructor for streaming collection (memory-efficient).
    /// </summary>
    public DocumentCouplingAnalyzer(
        SemanticModel semanticModel,
        string filePath,
        SyntaxNode root,
        IDependencyCollector? collector)
    {
        _semanticModel = semanticModel;
        _filePath = filePath;
        _collector = collector;

        // Only create list if not using collector
        if (collector == null)
        {
            _dependencies = [];
        }

        // Pre-scan for file-level namespace (file-scoped or first traditional namespace)
        _primaryNamespace = root.DescendantNodes()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault()?.Name.ToString()
            ?? root.DescendantNodes()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault()?.Name.ToString();
    }

    /// <summary>
    /// Adds a dependency edge, routing to collector or list based on mode.
    /// </summary>
    private void AddDependencyEdge(DependencyEdge edge)
    {
        if (_collector != null)
        {
            _collector.AddDependency(edge);
        }
        else
        {
            _dependencies?.Add(edge);
        }
    }

    /// <summary>
    /// Gets cached display string for a symbol, or computes and caches it.
    /// </summary>
    private string GetDisplayString(ISymbol symbol)
    {
        if (_symbolDisplayCache.TryGetValue(symbol, out var cached))
            return cached;

        var displayString = symbol.ToDisplayString();
        _symbolDisplayCache[symbol] = displayString;
        return displayString;
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        if (node.Name != null)
        {
            var namespaceName = node.Name.ToString();
            var containingNamespace = GetContainingNamespace(node);

            if (!string.IsNullOrEmpty(containingNamespace) && containingNamespace != namespaceName)
            {
                AddDependencyEdge(new DependencyEdge(
                    FromEntity: containingNamespace,
                    ToEntity: namespaceName,
                    Type: DependencyType.NamespaceReference,
                    ReferenceCount: 1)
                {
                    SourceLocation = DependencyEdge.EnableDetails
                        ? $"{_filePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"
                        : null
                });
            }
        }

        base.VisitUsingDirective(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is ITypeSymbol typeSymbol)
        {
            AnalyzeTypeReference(node, typeSymbol);
        }

        base.VisitIdentifierName(node);
    }

    public override void VisitQualifiedName(QualifiedNameSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is ITypeSymbol typeSymbol)
        {
            AnalyzeTypeReference(node, typeSymbol);
        }

        base.VisitQualifiedName(node);
    }

    public override void VisitGenericName(GenericNameSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is ITypeSymbol typeSymbol)
        {
            AnalyzeTypeReference(node, typeSymbol);
        }

        base.VisitGenericName(node);
    }

    private void AnalyzeTypeReference(SyntaxNode node, ITypeSymbol typeSymbol)
    {
        var fromType = GetContainingType(node);
        var toType = GetDisplayString(typeSymbol);

        if (fromType != null && fromType != toType)
        {
            AddDependencyEdge(new DependencyEdge(
                FromEntity: fromType,
                ToEntity: toType,
                Type: DependencyType.TypeReference,
                ReferenceCount: 1)
            {
                SourceLocation = DependencyEdge.EnableDetails
                    ? $"{_filePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"
                    : null,
                ReferencedSymbol = DependencyEdge.EnableDetails ? typeSymbol.Name : null
            });

            var fromNamespace = GetNamespace(fromType);
            var toNamespace = typeSymbol.ContainingNamespace != null
                ? GetDisplayString(typeSymbol.ContainingNamespace)
                : "";

            if (fromNamespace != toNamespace && !string.IsNullOrEmpty(toNamespace))
            {
                AddDependencyEdge(new DependencyEdge(
                    FromEntity: fromNamespace,
                    ToEntity: toNamespace,
                    Type: DependencyType.NamespaceReference,
                    ReferenceCount: 1)
                {
                    SourceLocation = DependencyEdge.EnableDetails
                        ? $"{_filePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"
                        : null,
                    ReferencedSymbol = DependencyEdge.EnableDetails ? typeSymbol.Name : null
                });
            }
        }
    }

    private string GetContainingNamespace(SyntaxNode node)
    {
        // First check ancestors (for traditional namespace blocks or nested contexts)
        var namespaceDecl = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDecl != null)
        {
            return namespaceDecl.Name.ToString();
        }

        // Fall back to file's primary namespace
        return _primaryNamespace ?? "";
    }

    private string? GetContainingType(SyntaxNode node)
    {
        var typeDecl = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDecl == null) return null;

        // Check cache first
        if (_containingTypeCache.TryGetValue(typeDecl, out var cached))
            return cached;

        var typeSymbol = _semanticModel.GetDeclaredSymbol(typeDecl) as ITypeSymbol;
        var result = typeSymbol != null ? GetDisplayString(typeSymbol) : null;
        _containingTypeCache[typeDecl] = result;
        return result;
    }

    private string GetNamespace(string fullTypeName)
    {
        var lastDotIndex = fullTypeName.LastIndexOf('.');
        return lastDotIndex > 0 ? fullTypeName[..lastDotIndex] : "";
    }
}

/// <summary>
/// Extension methods for symbol analysis.
/// </summary>
internal static class SymbolExtensions
{
    /// <summary>
    /// Checks if a type symbol is from source code (not external libraries).
    /// </summary>
    public static bool IsFromSource(this ITypeSymbol typeSymbol)
    {
        return typeSymbol.Locations.Any(loc => loc.IsInSource);
    }
}
