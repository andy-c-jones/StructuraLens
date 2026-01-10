using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Analyzes coupling between projects, assemblies, namespaces, and types.
/// </summary>
public static class CouplingAnalyzer
{
    /// <summary>
    /// Analyzes coupling for an entire solution.
    /// </summary>
    public static async Task<CouplingAnalysis> AnalyzeSolutionAsync(Solution solution, CancellationToken cancellationToken = default)
    {
        var allDependencies = new List<DependencyEdge>();
        var projectCouplingMetrics = new List<CouplingMetrics>();
        var namespaceCouplingMetrics = new List<CouplingMetrics>();
        var typeCouplingMetrics = new List<CouplingMetrics>();

        // Analyze project-to-project dependencies
        var projectDependencies = AnalyzeProjectDependencies(solution);
        allDependencies.AddRange(projectDependencies);

        // Analyze each project for internal coupling
        foreach (var project in solution.Projects.Where(p => p.Language == LanguageNames.CSharp))
        {
            var projectCoupling = await AnalyzeProjectInternalCouplingAsync(project, cancellationToken);
            allDependencies.AddRange(projectCoupling.dependencies);
            namespaceCouplingMetrics.AddRange(projectCoupling.namespaceCoupling);
            typeCouplingMetrics.AddRange(projectCoupling.typeCoupling);
        }

        // Build project-level coupling metrics
        projectCouplingMetrics = BuildProjectCouplingMetrics(solution, allDependencies);

        // Build complete namespace and type coupling (including cross-project)
        namespaceCouplingMetrics = BuildNamespaceCouplingMetrics(allDependencies);
        typeCouplingMetrics = BuildTypeCouplingMetrics(allDependencies);

        var summary = BuildCouplingSummary(projectCouplingMetrics, namespaceCouplingMetrics, typeCouplingMetrics, allDependencies);

        return new CouplingAnalysis(
            AnalyzedEntity: solution.FilePath ?? "Solution",
            AnalyzedAt: DateTime.UtcNow)
        {
            ProjectCoupling = projectCouplingMetrics,
            NamespaceCoupling = namespaceCouplingMetrics,
            TypeCoupling = typeCouplingMetrics,
            AllDependencies = allDependencies,
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
    /// Analyzes coupling for a single project (internal coupling only).
    /// </summary>
    public static async Task<CouplingAnalysis> AnalyzeProjectCouplingAsync(Project project, CancellationToken cancellationToken = default)
    {
        var allDependencies = new List<DependencyEdge>();
        var namespaceCouplingMetrics = new List<CouplingMetrics>();
        var typeCouplingMetrics = new List<CouplingMetrics>();

        // Analyze internal coupling within the project
        var projectCoupling = await AnalyzeProjectInternalCouplingAsync(project, cancellationToken);
        allDependencies.AddRange(projectCoupling.dependencies);
        namespaceCouplingMetrics.AddRange(projectCoupling.namespaceCoupling);
        typeCouplingMetrics.AddRange(projectCoupling.typeCoupling);

        // For single project, create a simple project coupling metric
        var projectCouplingMetrics = new List<CouplingMetrics>
        {
            new(project.Name, DependencyType.ProjectReference)
            {
                OutboundDependencies = [],
                InboundDependencies = []
            }
        };

        var summary = BuildCouplingSummary(projectCouplingMetrics, namespaceCouplingMetrics, typeCouplingMetrics, allDependencies);

        return new CouplingAnalysis(
            AnalyzedEntity: project.FilePath ?? project.Name,
            AnalyzedAt: DateTime.UtcNow)
        {
            ProjectCoupling = projectCouplingMetrics,
            NamespaceCoupling = namespaceCouplingMetrics,
            TypeCoupling = typeCouplingMetrics,
            AllDependencies = allDependencies,
            Summary = summary
        };
    }
    private static async Task<(List<DependencyEdge> dependencies, List<CouplingMetrics> namespaceCoupling, List<CouplingMetrics> typeCoupling)>
        AnalyzeProjectInternalCouplingAsync(Project project, CancellationToken cancellationToken)
    {
        var dependencies = new List<DependencyEdge>();
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null) return (dependencies, [], []);

        foreach (var document in project.Documents)
        {
            if (document.SourceCodeKind != SourceCodeKind.Regular) continue;

            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (syntaxTree == null || semanticModel == null) continue;

            var root = await syntaxTree.GetRootAsync(cancellationToken);
            var analyzer = new DocumentCouplingAnalyzer(semanticModel, document.FilePath ?? "");
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
        List<DependencyEdge> allDependencies)
    {
        var allMetrics = projectCoupling.Concat(namespaceCoupling).Concat(typeCoupling).ToList();
        
        return new CouplingSummary
        {
            TotalDependencies = allDependencies.Count,
            AverageEfferentCoupling = allMetrics.Count > 0 ? allMetrics.Average(m => m.EfferentCoupling) : 0,
            AverageAfferentCoupling = allMetrics.Count > 0 ? allMetrics.Average(m => m.AfferentCoupling) : 0,
            AverageInstability = allMetrics.Count > 0 ? allMetrics.Average(m => m.Instability) : 0,
            MostCoupledEntity = allMetrics.OrderByDescending(m => m.TotalCouplingStrength).FirstOrDefault()?.EntityName,
            MostUnstableEntity = allMetrics.OrderByDescending(m => m.Instability).FirstOrDefault()?.EntityName
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

    public IReadOnlyList<DependencyEdge> Dependencies => _dependencies;

    public DocumentCouplingAnalyzer(SemanticModel semanticModel, string filePath)
    {
        _semanticModel = semanticModel;
        _filePath = filePath;
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        if (node.Name != null)
        {
            var namespaceName = node.Name.ToString();
            var containingNamespace = GetContainingNamespace(node);

            if (containingNamespace != namespaceName) // Don't count self-references
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
        if (symbolInfo.Symbol is ITypeSymbol typeSymbol && !typeSymbol.IsFromSource())
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

            // Also add namespace-level dependency
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
        var namespaceDecl = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        return namespaceDecl?.Name?.ToString() ?? "";
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