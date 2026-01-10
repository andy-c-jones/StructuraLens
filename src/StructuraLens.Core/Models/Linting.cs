using StructuraLens.Core.Configuration;

namespace StructuraLens.Core.Models;

/// <summary>
/// Represents a violation of an architecture rule.
/// </summary>
public record LintViolation(
    string RuleId,
    string Message,
    RuleSeverity Severity)
{
    /// <summary>The entity that caused the violation.</summary>
    public string? FromEntity { get; init; }

    /// <summary>The target entity that was improperly referenced.</summary>
    public string? ToEntity { get; init; }

    /// <summary>Source file location where the violation was found.</summary>
    public string? SourceLocation { get; init; }
}

/// <summary>
/// Results of architecture linting.
/// </summary>
public record LintingResults(DateTime AnalyzedAt)
{
    /// <summary>All violations found during linting.</summary>
    public IReadOnlyList<LintViolation> Violations { get; init; } = [];

    /// <summary>Number of rules that were evaluated.</summary>
    public int RulesEvaluated { get; init; }

    /// <summary>Number of error-level violations.</summary>
    public int ErrorCount => Violations.Count(v => v.Severity == RuleSeverity.Error);

    /// <summary>Number of warning-level violations.</summary>
    public int WarningCount => Violations.Count(v => v.Severity == RuleSeverity.Warning);

    /// <summary>Number of info-level violations.</summary>
    public int InfoCount => Violations.Count(v => v.Severity == RuleSeverity.Info);

    /// <summary>Whether linting passed (no errors).</summary>
    public bool Passed => ErrorCount == 0;
}
