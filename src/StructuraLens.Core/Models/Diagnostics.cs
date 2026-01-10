using System.Text.Json.Serialization;

namespace StructuraLens.Core.Models;

/// <summary>
/// Represents a single compiler diagnostic (error, warning, info, etc.).
/// </summary>
public record DiagnosticInfo(
    string Id,
    string Message,
    DiagnosticLevel Severity,
    string FilePath,
    int Line,
    int Column)
{
    /// <summary>Category of the diagnostic (e.g., "Compiler", "Style", "Design").</summary>
    public string? Category { get; init; }

    /// <summary>Help link URL if available.</summary>
    public string? HelpLink { get; init; }
}

/// <summary>
/// Diagnostic severity levels matching Roslyn's DiagnosticSeverity.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiagnosticLevel
{
    /// <summary>Hidden diagnostic (not shown by default).</summary>
    Hidden = 0,

    /// <summary>Informational message.</summary>
    Info = 1,

    /// <summary>Warning that doesn't prevent compilation.</summary>
    Warning = 2,

    /// <summary>Error that prevents successful compilation.</summary>
    Error = 3
}

/// <summary>
/// Summary of diagnostics for a project.
/// </summary>
public record DiagnosticSummary
{
    /// <summary>Total number of errors.</summary>
    public int ErrorCount { get; init; }

    /// <summary>Total number of warnings.</summary>
    public int WarningCount { get; init; }

    /// <summary>Total number of info/suggestion messages.</summary>
    public int InfoCount { get; init; }

    /// <summary>Total number of hidden diagnostics.</summary>
    public int HiddenCount { get; init; }

    /// <summary>All diagnostics for this project.</summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];

    /// <summary>Whether the project compiled successfully (no errors).</summary>
    public bool CompilationSucceeded => ErrorCount == 0;
}
