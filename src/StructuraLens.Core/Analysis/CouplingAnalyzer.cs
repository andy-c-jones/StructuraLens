using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StructuraLens.Core.Configuration;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Analyzes coupling between projects, assemblies, namespaces, and types.
/// </summary>
public static class CouplingAnalyzer
{
    /// <summary>
    /// Analyzes coupling for an entire solution using default configuration.
    /// </summary>
    public static Task<CouplingAnalysis> AnalyzeSolutionAsync(Solution solution, CancellationToken cancellationToken = default)
    {
        return AnalyzeSolutionAsync(solution, ConfigurationLoader.CreateDefaultConfig(), NullLogger.Instance, cancellationToken);
    }

    /// <summary>
    /// Analyzes coupling for an entire solution with specified configuration.
    /// </summary>
    public static async Task<CouplingAnalysis> AnalyzeSolutionAsync(Solution solution, StructuraLensConfig config, ILogger logger, CancellationToken cancellationToken = default)
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
            logger.LogDebug("Analyzing coupling in project {Index}/{Total}: {ProjectName}", currentIndex, totalProjects, project.Name);
            
            var projectCoupling = await AnalyzeProjectInternalCouplingAsync(project, logger, ct);
            dependenciesBag.Add(projectCoupling.dependencies);
            
            logger.LogDebug("Project {ProjectName}: {DependencyCount} dependencies found", project.Name, projectCoupling.dependencies.Count);
        });

        // Merge all dependencies
        var allDependencies = new List<DependencyEdge>(projectDependencies);
        foreach (var deps in dependenciesBag)
        {
            allDependencies.AddRange(deps);
        }

        // Apply filtering based on configuration
        var filteredDependencies = DependencyFilter.FilterDependencies(allDependencies, config.Coupling, projectNames);

        // Build coupling metrics from filtered dependencies
        var projectCouplingMetrics = BuildProjectCouplingMetrics(solution, filteredDependencies);
        var namespaceCouplingMetrics = BuildNamespaceCouplingMetrics(filteredDependencies);
        var typeCouplingMetrics = BuildTypeCouplingMetrics(filteredDependencies);

        var summary = BuildCouplingSummary(projectCouplingMetrics, namespaceCouplingMetrics, typeCouplingMetrics, filteredDependencies, config);

        return new CouplingAnalysis(
            AnalyzedEntity: solution.FilePath ?? "Solution",
            AnalyzedAt: DateTime.UtcNow)
        {
            ProjectCoupling = projectCouplingMetrics,
            NamespaceCoupling = namespaceCouplingMetrics,
            TypeCoupling = typeCouplingMetrics,
            AllDependencies = filteredDependencies,
            Summary = summary
        };
    }

    /// <summary>
    /// Analyzes coupling for a single project using default configuration.
    /// </summary>
    public static Task<CouplingAnalysis> AnalyzeProjectCouplingAsync(Project project, CancellationToken cancellationToken = default)
    {
        return AnalyzeProjectCouplingAsync(project, ConfigurationLoader.CreateDefaultConfig(), NullLogger.Instance, cancellationToken);
    }

    /// <summary>
    /// Analyzes coupling for a single project with specified configuration.
    /// </summary>
    public static async Task<CouplingAnalysis> AnalyzeProjectCouplingAsync(Project project, StructuraLensConfig config, ILogger logger, CancellationToken cancellationToken = default)
    {
        var allDependencies = new List<DependencyEdge>();
        var projectNames = new List<string> { project.Name };

        // Analyze internal coupling within the project
        var projectCoupling = await AnalyzeProjectInternalCouplingAsync(project, logger, cancellationToken);
        allDependencies.AddRange(projectCoupling.dependencies);

        // Apply filtering based on configuration
        var filteredDependencies = DependencyFilter.FilterDependencies(allDependencies, config.Coupling, projectNames);

        // Build coupling metrics from filtered dependencies
        var namespaceCouplingMetrics = BuildNamespaceCouplingMetrics(filteredDependencies);
        var typeCouplingMetrics = BuildTypeCouplingMetrics(filteredDependencies);

        // For single project, create a simple project coupling metric
        var projectCouplingMetrics = new List<CouplingMetrics>
        {
            new(project.Name, DependencyType.ProjectReference)
            {
                OutboundDependencies = [],
                InboundDependencies = []
            }
        };

        var summary = BuildCouplingSummary(projectCouplingMetrics, namespaceCouplingMetrics, typeCouplingMetrics, filteredDependencies, config);

        return new CouplingAnalysis(
            AnalyzedEntity: project.FilePath ?? project.Name,
            AnalyzedAt: DateTime.UtcNow)
        {
            ProjectCoupling = projectCouplingMetrics,
            NamespaceCoupling = namespaceCouplingMetrics,
            TypeCoupling = typeCouplingMetrics,
            AllDependencies = filteredDependencies,
            Summary = summary
        };
    }

    /// <summary>
    /// Analyzes project-to-project dependencies based on project references.
    /// </summary>
    private static List<DependencyEdge> AnalyzeProjectDependencies(Solution solution)
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

    /// <summary>
    /// Analyzes internal coupling within a single project.
    /// </summary>
    private static async Task<(List<DependencyEdge> dependencies, List<CouplingMetrics> namespaceCoupling, List<CouplingMetrics> typeCoupling)>
        AnalyzeProjectInternalCouplingAsync(Project project, ILogger logger, CancellationToken cancellationToken)
    {
        var dependencies = new List<DependencyEdge>();
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null)
        {
            logger.LogWarning("Could not get compilation for project: {ProjectName}", project.Name);
            return (dependencies, [], []);
        }

        var documents = project.Documents.Where(d => d.SourceCodeKind == SourceCodeKind.Regular).ToList();
        var documentIndex = 0;
        
        logger.LogDebug("Analyzing {DocumentCount} documents for coupling in {ProjectName}", documents.Count, project.Name);

        foreach (var document in documents)
        {
            documentIndex++;
            
            if (documentIndex % 100 == 0)
            {
                logger.LogDebug("Coupling analysis progress: {DocumentIndex}/{DocumentCount} documents in {ProjectName}", 
                    documentIndex, documents.Count, project.Name);
            }

            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (syntaxTree == null || semanticModel == null) continue;

            var root = await syntaxTree.GetRootAsync(cancellationToken);
            var analyzer = new DocumentCouplingAnalyzer(semanticModel, document.FilePath ?? "", root);
            analyzer.Visit(root);
            dependencies.AddRange(analyzer.Dependencies);
        }

        // Group and build metrics at namespace and type level
        var namespaceCoupling = BuildNamespaceCouplingMetrics(dependencies);
        var typeCoupling = BuildTypeCouplingMetrics(dependencies);

        return (dependencies, namespaceCoupling, typeCoupling);
    }

    private static List<CouplingMetrics> BuildProjectCouplingMetrics(Solution solution, List<DependencyEdge> allDependencies)
    {
        var projectNames = solution.Projects.Select(p => p.Name).ToHashSet();
        var metrics = new List<CouplingMetrics>();

        foreach (var projectName in projectNames)
        {
            var outbound = allDependencies
                .Where(d => d.FromEntity == projectName || d.FromEntity.StartsWith($"{projectName}."))
                .ToList();
            
            var inbound = allDependencies
                .Where(d => d.ToEntity == projectName || d.ToEntity.StartsWith($"{projectName}."))
                .ToList();

            metrics.Add(new CouplingMetrics(projectName, DependencyType.ProjectReference)
            {
                OutboundDependencies = outbound,
                InboundDependencies = inbound
            });
        }

        return metrics;
    }

    private static List<CouplingMetrics> BuildNamespaceCouplingMetrics(List<DependencyEdge> allDependencies)
    {
        var namespaceDeps = allDependencies.Where(d => d.Type == DependencyType.NamespaceReference).ToList();
        var namespaces = namespaceDeps.Select(d => d.FromEntity).Union(namespaceDeps.Select(d => d.ToEntity)).Distinct().ToList();

        return namespaces.Select(ns => new CouplingMetrics(ns, DependencyType.NamespaceReference)
        {
            OutboundDependencies = namespaceDeps.Where(d => d.FromEntity == ns).ToList(),
            InboundDependencies = namespaceDeps.Where(d => d.ToEntity == ns).ToList()
        }).ToList();
    }

    private static List<CouplingMetrics> BuildTypeCouplingMetrics(List<DependencyEdge> allDependencies)
    {
        var typeDeps = allDependencies.Where(d => d.Type == DependencyType.TypeReference).ToList();
        var types = typeDeps.Select(d => d.FromEntity).Union(typeDeps.Select(d => d.ToEntity)).Distinct().ToList();

        return types.Select(type => new CouplingMetrics(type, DependencyType.TypeReference)
        {
            OutboundDependencies = typeDeps.Where(d => d.FromEntity == type).ToList(),
            InboundDependencies = typeDeps.Where(d => d.ToEntity == type).ToList()
        }).ToList();
    }

    private static CouplingSummary BuildCouplingSummary(
        List<CouplingMetrics> projectCoupling,
        List<CouplingMetrics> namespaceCoupling, 
        List<CouplingMetrics> typeCoupling,
        List<DependencyEdge> allDependencies,
        StructuraLensConfig config)
    {
        var allMetrics = projectCoupling.Concat(namespaceCoupling).Concat(typeCoupling).ToList();
        
        return new CouplingSummary
        {
            TotalDependencies = allDependencies.Count,
            AverageEfferentCoupling = allMetrics.Count > 0 ? allMetrics.Average(m => m.EfferentCoupling) : 0,
            AverageAfferentCoupling = allMetrics.Count > 0 ? allMetrics.Average(m => m.AfferentCoupling) : 0,
            AverageInstability = allMetrics.Count > 0 ? allMetrics.Average(m => m.Instability) : 0,
            MostCoupledEntity = allMetrics.OrderByDescending(m => m.TotalCouplingStrength).FirstOrDefault()?.EntityName,
            MostUnstableEntity = allMetrics.OrderByDescending(m => m.Instability).FirstOrDefault()?.EntityName,
            CouplingMode = config.Coupling.Mode.ToString()
        };
    }
}

/// <summary>
/// Syntax walker that analyzes coupling within a single document.
/// </summary>
internal sealed class DocumentCouplingAnalyzer : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly string _filePath;
    private readonly List<DependencyEdge> _dependencies = [];
    private readonly string? _primaryNamespace;

    public IReadOnlyList<DependencyEdge> Dependencies => _dependencies;

    public DocumentCouplingAnalyzer(SemanticModel semanticModel, string filePath, SyntaxNode root)
    {
        _semanticModel = semanticModel;
        _filePath = filePath;
        
        // Pre-scan for file-level namespace (file-scoped or first traditional namespace)
        _primaryNamespace = root.DescendantNodes()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault()?.Name.ToString()
            ?? root.DescendantNodes()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault()?.Name.ToString();
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        if (node.Name != null)
        {
            var namespaceName = node.Name.ToString();
            var containingNamespace = GetContainingNamespace(node);

            if (!string.IsNullOrEmpty(containingNamespace) && containingNamespace != namespaceName)
            {
                _dependencies.Add(new DependencyEdge(
                    FromEntity: containingNamespace,
                    ToEntity: namespaceName,
                    Type: DependencyType.NamespaceReference,
                    ReferenceCount: 1)
                {
                    SourceLocation = $"{_filePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"
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

    private void AnalyzeTypeReference(SyntaxNode node, ITypeSymbol typeSymbol)
    {
        var fromType = GetContainingType(node);
        var toType = typeSymbol.ToDisplayString();

        if (fromType != null && fromType != toType)
        {
            _dependencies.Add(new DependencyEdge(
                FromEntity: fromType,
                ToEntity: toType,
                Type: DependencyType.TypeReference,
                ReferenceCount: 1)
            {
                SourceLocation = $"{_filePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}",
                ReferencedSymbol = typeSymbol.Name
            });

            var fromNamespace = GetNamespace(fromType);
            var toNamespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? "";
            
            if (fromNamespace != toNamespace && !string.IsNullOrEmpty(toNamespace))
            {
                _dependencies.Add(new DependencyEdge(
                    FromEntity: fromNamespace,
                    ToEntity: toNamespace,
                    Type: DependencyType.NamespaceReference,
                    ReferenceCount: 1)
                {
                    SourceLocation = $"{_filePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}",
                    ReferencedSymbol = typeSymbol.Name
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

        var typeSymbol = _semanticModel.GetDeclaredSymbol(typeDecl) as ITypeSymbol;
        return typeSymbol?.ToDisplayString();
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
