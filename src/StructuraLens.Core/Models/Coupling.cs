namespace StructuraLens.Core.Models;

/// <summary>
/// Represents a dependency relationship between two entities.
/// </summary>
public record DependencyEdge(
    string FromEntity,
    string ToEntity,
    DependencyType Type,
    int ReferenceCount)
{
    /// <summary>
    /// Enables skipping per-occurrence details (locations/symbols) in large solutions.
    /// When true, analyzers should avoid populating optional fields.
    /// </summary>
    public static bool EnableDetails { get; set; } = true;

    /// <summary>
    /// Source location where this dependency was found (optional).
    /// </summary>
    public string? SourceLocation { get; init; }

    /// <summary>
    /// Specific symbol or member that caused this dependency (optional).
    /// </summary>
    public string? ReferencedSymbol { get; init; }
}

/// <summary>
/// Types of dependencies that can be tracked.
/// </summary>
public enum DependencyType
{
    /// <summary>Project-to-project reference (.csproj references).</summary>
    ProjectReference,

    /// <summary>Assembly-to-assembly reference (compiled DLL references).</summary>
    AssemblyReference,

    /// <summary>Namespace-to-namespace reference (using statements, qualified names).</summary>
    NamespaceReference,

    /// <summary>Type-to-type reference (inheritance, field/property types, method parameters).</summary>
    TypeReference,

    /// <summary>Method-to-method reference (method calls).</summary>
    MethodReference
}

/// <summary>
/// Coupling metrics for a specific entity (project, namespace, type, etc.).
/// Separates internal (within solution) and external (third-party) dependencies.
/// </summary>
public record CouplingMetrics(
    string EntityName,
    DependencyType EntityType)
{
    /// <summary>Internal dependencies (within solution) - outbound.</summary>
    public IReadOnlyList<DependencyEdge> InternalOutbound { get; init; } = [];

    /// <summary>Internal dependencies (within solution) - inbound.</summary>
    public IReadOnlyList<DependencyEdge> InternalInbound { get; init; } = [];

    /// <summary>External dependencies (third-party) - outbound only.</summary>
    public IReadOnlyList<DependencyEdge> ExternalOutbound { get; init; } = [];

    // Pre-computed metrics (lazily initialized on first access, then cached)
    private int? _internalDependencies;
    private int? _internalDependents;
    private int? _externalBclDependencies;
    private int? _externalPackageDependencies;
    private int? _totalReferenceStrength;

    /// <summary>Number of internal entities this depends on.</summary>
    public int InternalDependencies => _internalDependencies ??= ComputeUniqueTargets(InternalOutbound);

    /// <summary>Number of internal entities that depend on this.</summary>
    public int InternalDependents => _internalDependents ??= ComputeUniqueSources(InternalInbound);

    /// <summary>
    /// Dependency ratio: InternalDependencies / (InternalDependencies + InternalDependents).
    /// Range 0-1, where 0 = pure provider (stable), 1 = pure consumer (unstable).
    /// </summary>
    public double DependencyRatio
    {
        get
        {
            var deps = InternalDependencies;
            var dependents = InternalDependents;
            return deps + dependents > 0 ? (double)deps / (deps + dependents) : 0;
        }
    }

    /// <summary>Number of external BCL dependencies (System.*, Microsoft.*).</summary>
    public int ExternalBclDependencies => _externalBclDependencies ??= ComputeExternalBcl();

    /// <summary>Number of external third-party package dependencies.</summary>
    public int ExternalPackageDependencies => _externalPackageDependencies ??= ComputeExternalPackages();

    /// <summary>Total external dependencies (BCL + packages).</summary>
    public int TotalExternalDependencies => ExternalBclDependencies + ExternalPackageDependencies;

    /// <summary>Total reference strength based on all reference counts.</summary>
    public int TotalReferenceStrength => _totalReferenceStrength ??= ComputeTotalReferenceStrength();

    private int ComputeUniqueTargets(IReadOnlyList<DependencyEdge> edges)
    {
        var seen = new HashSet<string>();
        foreach (var d in edges)
            seen.Add(d.ToEntity);
        return seen.Count;
    }

    private int ComputeUniqueSources(IReadOnlyList<DependencyEdge> edges)
    {
        var seen = new HashSet<string>();
        foreach (var d in edges)
            seen.Add(d.FromEntity);
        return seen.Count;
    }

    private int ComputeExternalBcl()
    {
        var seen = new HashSet<string>();
        foreach (var d in ExternalOutbound)
        {
            if (IsBclNamespace(d.ToEntity))
                seen.Add(d.ToEntity);
        }
        return seen.Count;
    }

    private int ComputeExternalPackages()
    {
        var seen = new HashSet<string>();
        foreach (var d in ExternalOutbound)
        {
            if (!IsBclNamespace(d.ToEntity))
                seen.Add(d.ToEntity);
        }
        return seen.Count;
    }

    private int ComputeTotalReferenceStrength()
    {
        var total = 0;
        foreach (var d in InternalOutbound)
            total += d.ReferenceCount;
        foreach (var d in InternalInbound)
            total += d.ReferenceCount;
        foreach (var d in ExternalOutbound)
            total += d.ReferenceCount;
        return total;
    }

    private static bool IsBclNamespace(string ns)
    {
        return ns.StartsWith("System.") ||
               ns.Equals("System") ||
               ns.StartsWith("Microsoft.");
    }
}

/// <summary>
/// Complete coupling analysis results for a solution/project.
/// </summary>
public record CouplingAnalysis(
    string AnalyzedEntity,
    DateTime AnalyzedAt)
{
    /// <summary>Project-level coupling metrics.</summary>
    public IReadOnlyList<CouplingMetrics> ProjectCoupling { get; init; } = [];

    /// <summary>Namespace-level coupling metrics.</summary>
    public IReadOnlyList<CouplingMetrics> NamespaceCoupling { get; init; } = [];

    /// <summary>Type-level coupling metrics.</summary>
    public IReadOnlyList<CouplingMetrics> TypeCoupling { get; init; } = [];

    /// <summary>All dependency edges found during analysis.</summary>
    public IReadOnlyList<DependencyEdge> AllDependencies { get; init; } = [];

    /// <summary>Summary statistics about the coupling analysis.</summary>
    public CouplingSummary Summary { get; init; } = new();
}

/// <summary>
/// Summary statistics about coupling in the analyzed codebase.
/// </summary>
public record CouplingSummary
{
    /// <summary>Total number of dependency edges.</summary>
    public int TotalDependencies { get; init; }

    /// <summary>Average internal dependencies across all entities.</summary>
    public double AverageInternalDependencies { get; init; }

    /// <summary>Average internal dependents across all entities.</summary>
    public double AverageInternalDependents { get; init; }

    /// <summary>Average dependency ratio across all entities.</summary>
    public double AverageDependencyRatio { get; init; }

    /// <summary>Average external dependencies across all entities.</summary>
    public double AverageExternalDependencies { get; init; }

    /// <summary>Average external BCL dependencies across all entities.</summary>
    public double AverageExternalBclDependencies { get; init; }

    /// <summary>Average external package dependencies across all entities.</summary>
    public double AverageExternalPackageDependencies { get; init; }

    /// <summary>Most coupled entity (highest total reference strength).</summary>
    public string? MostCoupledEntity { get; init; }

    /// <summary>Most referenced/reused entity (highest internal dependents).</summary>
    public string? MostDependentEntity { get; init; }

    /// <summary>Highest-level consumer (highest dependency ratio).</summary>
    public string? HighestConsumerEntity { get; init; }

    /// <summary>The coupling mode used for this analysis.</summary>
    public string CouplingMode { get; init; } = "All";
}