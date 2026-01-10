using System.Text.Json.Serialization;

namespace StructuraLens.Core.Models;

/// <summary>
/// Compact report format optimized for size and parsing performance.
/// Uses short property names and array-based structures.
/// </summary>
public record CompactReport
{
    /// <summary>Version of the compact report format.</summary>
    [JsonPropertyName("v")]
    public int Version { get; init; } = 1;

    /// <summary>Solution/project path analyzed.</summary>
    [JsonPropertyName("p")]
    public string Path { get; init; } = "";

    /// <summary>Analysis timestamp (Unix milliseconds).</summary>
    [JsonPropertyName("t")]
    public long Timestamp { get; init; }

    /// <summary>Project metrics array.</summary>
    [JsonPropertyName("prj")]
    public IReadOnlyList<CompactProject> Projects { get; init; } = [];

    /// <summary>Graph data for visualization.</summary>
    [JsonPropertyName("g")]
    public CompactGraph Graph { get; init; } = new();

    /// <summary>Linting results.</summary>
    [JsonPropertyName("l")]
    public CompactLinting? Linting { get; init; }
}

/// <summary>Compact project metrics.</summary>
public record CompactProject
{
    /// <summary>Project name.</summary>
    [JsonPropertyName("n")]
    public string Name { get; init; } = "";

    /// <summary>Type count.</summary>
    [JsonPropertyName("tc")]
    public int TypeCount { get; init; }

    /// <summary>Method count.</summary>
    [JsonPropertyName("mc")]
    public int MethodCount { get; init; }

    /// <summary>Total cyclomatic complexity.</summary>
    [JsonPropertyName("cc")]
    public int CyclomaticComplexity { get; init; }

    /// <summary>Total lines of executable code.</summary>
    [JsonPropertyName("loc")]
    public int LinesOfCode { get; init; }

    /// <summary>Max depth of inheritance.</summary>
    [JsonPropertyName("dit")]
    public int MaxDepthOfInheritance { get; init; }

    /// <summary>Average maintainability index.</summary>
    [JsonPropertyName("mi")]
    public double AvgMaintainabilityIndex { get; init; }

    /// <summary>Efferent coupling.</summary>
    [JsonPropertyName("ce")]
    public int EfferentCoupling { get; init; }

    /// <summary>Afferent coupling.</summary>
    [JsonPropertyName("ca")]
    public int AfferentCoupling { get; init; }

    /// <summary>Instability (0-1).</summary>
    [JsonPropertyName("i")]
    public double Instability { get; init; }

    /// <summary>Compiler errors count.</summary>
    [JsonPropertyName("err")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Errors { get; init; }

    /// <summary>Compiler warnings count.</summary>
    [JsonPropertyName("warn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Warnings { get; init; }

    /// <summary>Type metrics (optional, for detailed reports).</summary>
    [JsonPropertyName("types")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CompactType>? Types { get; init; }
}

/// <summary>Compact type metrics.</summary>
public record CompactType
{
    /// <summary>Type name (short form, not fully qualified).</summary>
    [JsonPropertyName("n")]
    public string Name { get; init; } = "";

    /// <summary>Depth of inheritance.</summary>
    [JsonPropertyName("dit")]
    public int DepthOfInheritance { get; init; }

    /// <summary>Total cyclomatic complexity.</summary>
    [JsonPropertyName("cc")]
    public int CyclomaticComplexity { get; init; }

    /// <summary>Total lines of code.</summary>
    [JsonPropertyName("loc")]
    public int LinesOfCode { get; init; }

    /// <summary>Average maintainability index.</summary>
    [JsonPropertyName("mi")]
    public double AvgMaintainabilityIndex { get; init; }

    /// <summary>Method metrics (optional).</summary>
    [JsonPropertyName("m")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CompactMethod>? Methods { get; init; }
}

/// <summary>Compact method metrics.</summary>
public record CompactMethod
{
    /// <summary>Method name (short form).</summary>
    [JsonPropertyName("n")]
    public string Name { get; init; } = "";

    /// <summary>Cyclomatic complexity.</summary>
    [JsonPropertyName("cc")]
    public int CyclomaticComplexity { get; init; }

    /// <summary>Lines of code.</summary>
    [JsonPropertyName("loc")]
    public int LinesOfCode { get; init; }

    /// <summary>Halstead volume.</summary>
    [JsonPropertyName("hv")]
    public double HalsteadVolume { get; init; }

    /// <summary>Maintainability index.</summary>
    [JsonPropertyName("mi")]
    public double MaintainabilityIndex { get; init; }

    /// <summary>Start line.</summary>
    [JsonPropertyName("sl")]
    public int StartLine { get; init; }

    /// <summary>End line.</summary>
    [JsonPropertyName("el")]
    public int EndLine { get; init; }
}

/// <summary>Graph structure for d3.js visualization.</summary>
public record CompactGraph
{
    /// <summary>Project-level nodes and edges.</summary>
    [JsonPropertyName("p")]
    public GraphLayer Projects { get; init; } = new();

    /// <summary>Namespace-level nodes and edges.</summary>
    [JsonPropertyName("ns")]
    public GraphLayer Namespaces { get; init; } = new();
}

/// <summary>A layer of the dependency graph (nodes + edges).</summary>
public record GraphLayer
{
    /// <summary>Nodes: [id, name, size]. Size = LOC or type count.</summary>
    [JsonPropertyName("n")]
    public IReadOnlyList<object[]> Nodes { get; init; } = [];

    /// <summary>Edges: [sourceId, targetId, weight].</summary>
    [JsonPropertyName("e")]
    public IReadOnlyList<int[]> Edges { get; init; } = [];
}

/// <summary>Compact linting results.</summary>
public record CompactLinting
{
    /// <summary>Rules evaluated.</summary>
    [JsonPropertyName("r")]
    public int RulesEvaluated { get; init; }

    /// <summary>Error count.</summary>
    [JsonPropertyName("e")]
    public int Errors { get; init; }

    /// <summary>Warning count.</summary>
    [JsonPropertyName("w")]
    public int Warnings { get; init; }

    /// <summary>Passed (no errors).</summary>
    [JsonPropertyName("ok")]
    public bool Passed { get; init; }

    /// <summary>Violations: [ruleId, severity, from, to]. Severity: 0=info, 1=warn, 2=error.</summary>
    [JsonPropertyName("v")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<object[]>? Violations { get; init; }
}
