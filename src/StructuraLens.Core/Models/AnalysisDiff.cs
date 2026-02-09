namespace StructuraLens.Core.Models;

/// <summary>
/// Represents a comparison between two analysis reports.
/// </summary>
public record AnalysisDiffReport
{
    public DiffMetadata Base { get; init; } = new();
    public DiffMetadata Head { get; init; } = new();
    public DiffTotals Totals { get; init; } = new();
    public IReadOnlyList<ProjectDiff> Projects { get; init; } = [];
    public DiagnosticDiffSummary Diagnostics { get; init; } = new();
    
    /// <summary>Dependencies that are new to the entire solution (first project to add them).</summary>
    public IReadOnlySet<string> NewToSolution { get; init; } = new HashSet<string>();
    
    /// <summary>Dependencies that are removed from the entire solution (no projects use them anymore).</summary>
    public IReadOnlySet<string> RemovedFromSolution { get; init; } = new HashSet<string>();
}

/// <summary>Metadata for a single analyzed report.</summary>
public record DiffMetadata
{
    public string SolutionPath { get; init; } = "";
    public DateTime AnalyzedAt { get; init; }
    public string? CommitSha { get; init; }
    public string? BranchName { get; init; }
}

/// <summary>Totals comparison for the full solution.</summary>
public record DiffTotals
{
    public int BaseProjects { get; init; }
    public int HeadProjects { get; init; }
    public int ProjectsDelta => HeadProjects - BaseProjects;

    public int BaseTypes { get; init; }
    public int HeadTypes { get; init; }
    public int TypesDelta => HeadTypes - BaseTypes;

    public int BaseMethods { get; init; }
    public int HeadMethods { get; init; }
    public int MethodsDelta => HeadMethods - BaseMethods;

    public int BaseCyclomaticComplexity { get; init; }
    public int HeadCyclomaticComplexity { get; init; }
    public int CyclomaticComplexityDelta => HeadCyclomaticComplexity - BaseCyclomaticComplexity;

    public int BaseLinesOfCode { get; init; }
    public int HeadLinesOfCode { get; init; }
    public int LinesOfCodeDelta => HeadLinesOfCode - BaseLinesOfCode;

    public double BaseAvgMaintainabilityIndex { get; init; }
    public double HeadAvgMaintainabilityIndex { get; init; }
    public double AvgMaintainabilityDelta => Math.Round(HeadAvgMaintainabilityIndex - BaseAvgMaintainabilityIndex, 1);

    public int BaseErrors { get; init; }
    public int HeadErrors { get; init; }
    public int ErrorsDelta => HeadErrors - BaseErrors;

    public int BaseWarnings { get; init; }
    public int HeadWarnings { get; init; }
    public int WarningsDelta => HeadWarnings - BaseWarnings;

    public int BaseInfo { get; init; }
    public int HeadInfo { get; init; }
    public int InfoDelta => HeadInfo - BaseInfo;

    public int BaseHidden { get; init; }
    public int HeadHidden { get; init; }
    public int HiddenDelta => HeadHidden - BaseHidden;
}

/// <summary>Per-project comparison.</summary>
public record ProjectDiff
{
    public string Name { get; init; } = "";
    public bool IsAdded { get; init; }
    public bool IsRemoved { get; init; }
    public ProjectDiffMetrics Base { get; init; } = new();
    public ProjectDiffMetrics Head { get; init; } = new();

    public int TypeDelta => Head.TypeCount - Base.TypeCount;
    public int MethodDelta => Head.MethodCount - Base.MethodCount;
    public int CyclomaticComplexityDelta => Head.CyclomaticComplexity - Base.CyclomaticComplexity;
    public int LinesOfCodeDelta => Head.LinesOfCode - Base.LinesOfCode;
    public int MaxDepthOfInheritanceDelta => Head.MaxDepthOfInheritance - Base.MaxDepthOfInheritance;
    public double MaintainabilityDelta => Math.Round(Head.AvgMaintainabilityIndex - Base.AvgMaintainabilityIndex, 1);
    public int ErrorsDelta => Head.Errors - Base.Errors;
    public int WarningsDelta => Head.Warnings - Base.Warnings;
    public int InternalDependenciesDelta => Head.InternalDependencies - Base.InternalDependencies;
    public int InternalDependentsDelta => Head.InternalDependents - Base.InternalDependents;
    public double DependencyRatioDelta => Math.Round(Head.DependencyRatio - Base.DependencyRatio, 2);
    public int ExternalDependenciesDelta => Head.ExternalDependencies - Base.ExternalDependencies;
    public int ExternalBclDependenciesDelta => Head.ExternalBclDependencies - Base.ExternalBclDependencies;
    public int ExternalPackageDependenciesDelta => Head.ExternalPackageDependencies - Base.ExternalPackageDependencies;
    
    public IReadOnlyList<string> AddedBclDependencies { get; init; } = [];
    public IReadOnlyList<string> RemovedBclDependencies { get; init; } = [];
    public IReadOnlyList<string> AddedPackageDependencies { get; init; } = [];
    public IReadOnlyList<string> RemovedPackageDependencies { get; init; } = [];
    public IReadOnlyList<string> AddedInternalDependencies { get; init; } = [];
    public IReadOnlyList<string> RemovedInternalDependencies { get; init; } = [];
}

/// <summary>Project metrics used in diff output.</summary>
public record ProjectDiffMetrics
{
    public int TypeCount { get; init; }
    public int MethodCount { get; init; }
    public int CyclomaticComplexity { get; init; }
    public int LinesOfCode { get; init; }
    public int MaxDepthOfInheritance { get; init; }
    public double AvgMaintainabilityIndex { get; init; }
    public int InternalDependencies { get; init; }
    public int InternalDependents { get; init; }
    public double DependencyRatio { get; init; }
    public int ExternalDependencies { get; init; }
    public int ExternalBclDependencies { get; init; }
    public int ExternalPackageDependencies { get; init; }
    public IReadOnlyList<string> ExternalBclDependencyNames { get; init; } = [];
    public IReadOnlyList<string> ExternalPackageDependencyNames { get; init; } = [];
    public int Errors { get; init; }
    public int Warnings { get; init; }
}

/// <summary>Diagnostics comparison summary.</summary>
public record DiagnosticDiffSummary
{
    public int BaseErrors { get; init; }
    public int HeadErrors { get; init; }
    public int ErrorDelta => HeadErrors - BaseErrors;

    public int BaseWarnings { get; init; }
    public int HeadWarnings { get; init; }
    public int WarningDelta => HeadWarnings - BaseWarnings;

    public int BaseInfo { get; init; }
    public int HeadInfo { get; init; }
    public int InfoDelta => HeadInfo - BaseInfo;

    public int BaseHidden { get; init; }
    public int HeadHidden { get; init; }
    public int HiddenDelta => HeadHidden - BaseHidden;

    public int NewErrors { get; init; }
    public int ResolvedErrors { get; init; }
    public int NewWarnings { get; init; }
    public int ResolvedWarnings { get; init; }

    public IReadOnlyList<DiagnosticDiffItem> TopNewErrors { get; init; } = [];
    public IReadOnlyList<DiagnosticDiffItem> TopNewWarnings { get; init; } = [];
    public IReadOnlyList<DiagnosticDiffItem> TopResolvedErrors { get; init; } = [];
    public IReadOnlyList<DiagnosticDiffItem> TopResolvedWarnings { get; init; } = [];
}

/// <summary>Diagnostic entry for diff lists.</summary>
public record DiagnosticDiffItem
{
    public string Project { get; init; } = "";
    public string Id { get; init; } = "";
    public string Severity { get; init; } = "";
    public string Message { get; init; } = "";
    public string File { get; init; } = "";
    public int Line { get; init; }
    public int Column { get; init; }
}
