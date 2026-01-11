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
/// </summary>
public record CouplingMetrics(
    string EntityName,
    DependencyType EntityType)
{
    /// <summary>Dependencies this entity has on others (outbound coupling).</summary>
    public IReadOnlyList<DependencyEdge> OutboundDependencies { get; init; } = [];
    
    /// <summary>Dependencies other entities have on this one (inbound coupling).</summary>
    public IReadOnlyList<DependencyEdge> InboundDependencies { get; init; } = [];

    // Pre-computed metrics (lazily initialized on first access, then cached)
    private int? _efferentCoupling;
    private int? _afferentCoupling;
    private int? _totalCouplingStrength;

    /// <summary>Efferent coupling (Ce) - number of entities this depends on.</summary>
    public int EfferentCoupling => _efferentCoupling ??= ComputeEfferentCoupling();
    
    /// <summary>Afferent coupling (Ca) - number of entities that depend on this.</summary>
    public int AfferentCoupling => _afferentCoupling ??= ComputeAfferentCoupling();
    
    /// <summary>Instability (I) = Ce / (Ca + Ce). Range 0-1, where 0 = stable, 1 = unstable.</summary>
    public double Instability
    {
        get
        {
            var ce = EfferentCoupling;
            var ca = AfferentCoupling;
            return ce + ca > 0 ? (double)ce / (ce + ca) : 0;
        }
    }
    
    /// <summary>Total coupling strength based on reference counts.</summary>
    public int TotalCouplingStrength => _totalCouplingStrength ??= ComputeTotalCouplingStrength();

    private int ComputeEfferentCoupling()
    {
        var seen = new HashSet<string>();
        foreach (var d in OutboundDependencies)
            seen.Add(d.ToEntity);
        return seen.Count;
    }

    private int ComputeAfferentCoupling()
    {
        var seen = new HashSet<string>();
        foreach (var d in InboundDependencies)
            seen.Add(d.FromEntity);
        return seen.Count;
    }

    private int ComputeTotalCouplingStrength()
    {
        var total = 0;
        foreach (var d in OutboundDependencies)
            total += d.ReferenceCount;
        foreach (var d in InboundDependencies)
            total += d.ReferenceCount;
        return total;
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
    
    /// <summary>Average efferent coupling across all entities.</summary>
    public double AverageEfferentCoupling { get; init; }
    
    /// <summary>Average afferent coupling across all entities.</summary>
    public double AverageAfferentCoupling { get; init; }
    
    /// <summary>Average instability across all entities.</summary>
    public double AverageInstability { get; init; }
    
    /// <summary>Most coupled entity (highest total coupling strength).</summary>
    public string? MostCoupledEntity { get; init; }
    
    /// <summary>Most unstable entity (highest instability score).</summary>
    public string? MostUnstableEntity { get; init; }
    
    /// <summary>The coupling mode used for this analysis.</summary>
    public string CouplingMode { get; init; } = "Filtered";
}