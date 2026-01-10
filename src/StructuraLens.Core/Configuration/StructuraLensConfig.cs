using System.Text.Json.Serialization;

namespace StructuraLens.Core.Configuration;

/// <summary>
/// Root configuration for StructuraLens analysis.
/// </summary>
public class StructuraLensConfig
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("inheritanceDepth")]
    public int InheritanceDepth { get; set; } = 10;

    [JsonPropertyName("coupling")]
    public CouplingConfig Coupling { get; set; } = new();

    [JsonPropertyName("metrics")]
    public MetricsConfig Metrics { get; set; } = new();

    [JsonPropertyName("output")]
    public OutputConfig Output { get; set; } = new();

    [JsonPropertyName("rules")]
    public List<ArchitectureRule> Rules { get; set; } = new();

    /// <summary>
    /// Merges this configuration with a parent configuration.
    /// Child values override parent values for scalars.
    /// Arrays are concatenated (parent first, then child).
    /// </summary>
    public StructuraLensConfig MergeWithParent(StructuraLensConfig parent)
    {
        return new StructuraLensConfig
        {
            Schema = Schema ?? parent.Schema,
            InheritanceDepth = InheritanceDepth != 10 ? InheritanceDepth : parent.InheritanceDepth,
            Coupling = Coupling.MergeWithParent(parent.Coupling),
            Metrics = Metrics.MergeWithParent(parent.Metrics),
            Output = Output.MergeWithParent(parent.Output),
            Rules = parent.Rules.Concat(Rules).ToList()
        };
    }
}

/// <summary>
/// Configuration for coupling analysis.
/// </summary>
public class CouplingConfig
{
    [JsonPropertyName("mode")]
    public CouplingMode Mode { get; set; } = CouplingMode.Filtered;

    [JsonPropertyName("excludePatterns")]
    public List<string> ExcludePatterns { get; set; } = new() { "System.*", "Microsoft.*" };

    [JsonPropertyName("includePatterns")]
    public List<string> IncludePatterns { get; set; } = new();

    [JsonPropertyName("patternType")]
    public PatternType PatternType { get; set; } = PatternType.Wildcard;

    [JsonPropertyName("trackExternalDependencies")]
    public bool TrackExternalDependencies { get; set; } = true;

    [JsonPropertyName("groupByAssembly")]
    public bool GroupByAssembly { get; set; } = false;

    public CouplingConfig MergeWithParent(CouplingConfig parent)
    {
        return new CouplingConfig
        {
            Mode = Mode != CouplingMode.Filtered ? Mode : parent.Mode,
            ExcludePatterns = parent.ExcludePatterns.Concat(ExcludePatterns).Distinct().ToList(),
            IncludePatterns = parent.IncludePatterns.Concat(IncludePatterns).Distinct().ToList(),
            PatternType = PatternType != PatternType.Wildcard ? PatternType : parent.PatternType,
            TrackExternalDependencies = TrackExternalDependencies != true ? TrackExternalDependencies : parent.TrackExternalDependencies,
            GroupByAssembly = GroupByAssembly != false ? GroupByAssembly : parent.GroupByAssembly
        };
    }
}

/// <summary>
/// Coupling analysis modes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CouplingMode
{
    /// <summary>Only track coupling between project's own code (no external dependencies).</summary>
    Internal,
    
    /// <summary>Track external dependencies but apply exclude/include filters.</summary>
    Filtered,
    
    /// <summary>Track all dependencies regardless of source.</summary>
    All
}

/// <summary>
/// Pattern matching types for filters.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PatternType
{
    /// <summary>Wildcard matching (* and ?).</summary>
    Wildcard,
    
    /// <summary>Regular expression matching.</summary>
    Regex,
    
    /// <summary>Exact string matching.</summary>
    Exact
}

/// <summary>
/// Configuration for metrics calculation.
/// </summary>
public class MetricsConfig
{
    [JsonPropertyName("includeTests")]
    public bool IncludeTests { get; set; } = true;

    [JsonPropertyName("excludeGenerated")]
    public bool ExcludeGenerated { get; set; } = true;

    public MetricsConfig MergeWithParent(MetricsConfig parent)
    {
        return new MetricsConfig
        {
            IncludeTests = IncludeTests != true ? IncludeTests : parent.IncludeTests,
            ExcludeGenerated = ExcludeGenerated != true ? ExcludeGenerated : parent.ExcludeGenerated
        };
    }
}

/// <summary>
/// Configuration for analysis output.
/// </summary>
public class OutputConfig
{
    [JsonPropertyName("includeSourceLocations")]
    public bool IncludeSourceLocations { get; set; } = false;

    [JsonPropertyName("maxDependenciesInSummary")]
    public int MaxDependenciesInSummary { get; set; } = 10;

    public OutputConfig MergeWithParent(OutputConfig parent)
    {
        return new OutputConfig
        {
            IncludeSourceLocations = IncludeSourceLocations != false ? IncludeSourceLocations : parent.IncludeSourceLocations,
            MaxDependenciesInSummary = MaxDependenciesInSummary != 10 ? MaxDependenciesInSummary : parent.MaxDependenciesInSummary
        };
    }
}

/// <summary>
/// Architecture rule for future use.
/// </summary>
public class ArchitectureRule
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "warning";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}