namespace StructuraLens.Core.Models;

/// <summary>
/// Represents code metrics for a single method.
/// </summary>
public record MethodMetrics(
    string FullName,
    string FilePath,
    int StartLine,
    int EndLine,
    int CyclomaticComplexity,
    int LinesOfExecutableCode,
    double HalsteadVolume,
    double MaintainabilityIndex);

/// <summary>
/// Represents code metrics for a single type (class, struct, etc.).
/// </summary>
public record TypeMetrics(
    string FullName,
    string FilePath,
    int DepthOfInheritance,
    IReadOnlyList<MethodMetrics> Methods)
{
    public int TotalCyclomaticComplexity => Methods.Sum(m => m.CyclomaticComplexity);
    public int TotalLinesOfExecutableCode => Methods.Sum(m => m.LinesOfExecutableCode);
    public string Namespace => FullName.GetNamespace("(global)");
}

/// <summary>
/// Represents code metrics for a namespace within a project.
/// </summary>
public record NamespaceMetrics(
    string Name,
    IReadOnlyList<TypeMetrics> Types)
{
    public int TotalCyclomaticComplexity => Types.CalculateTotalCyclomaticComplexity();
    public int TotalLinesOfExecutableCode => Types.CalculateTotalLinesOfCode();
    public int TotalMethods => Types.CountTotalMethods();
    public int MaxDepthOfInheritance => Types.CalculateMaxDepthOfInheritance();
    public double AvgMaintainabilityIndex => Types.CalculateAverageMaintainabilityIndex();
}

/// <summary>
/// Represents code metrics for a single project.
/// </summary>
public record ProjectMetrics(
    string Name,
    string FilePath,
    IReadOnlyList<TypeMetrics> Types)
{
    public int TotalCyclomaticComplexity => Types.CalculateTotalCyclomaticComplexity();
    public int TotalLinesOfExecutableCode => Types.CalculateTotalLinesOfCode();
    public int MaxDepthOfInheritance => Types.CalculateMaxDepthOfInheritance();
    public int TotalMethods => Types.CountTotalMethods();

    /// <summary>Compiler diagnostics for this project.</summary>
    public DiagnosticSummary? Diagnostics { get; init; }

    /// <summary>
    /// Groups types by namespace for hierarchical reporting.
    /// </summary>
    public IReadOnlyList<NamespaceMetrics> GetNamespaceMetrics()
    {
        return Types
            .GroupBy(t => t.Namespace)
            .Select(g => new NamespaceMetrics(g.Key, [.. g]))
            .OrderBy(n => n.Name)
            .ToList();
    }
}

/// <summary>
/// Represents the complete analysis report.
/// </summary>
public record AnalysisReport(
    string SolutionPath,
    DateTime AnalyzedAt,
    IReadOnlyList<ProjectMetrics> Projects,
    IReadOnlyList<string> Warnings)
{
    public int TotalProjects => Projects.Count;
    public int TotalTypes => Projects.Sum(p => p.Types.Count);
    public int TotalMethods => Projects.Sum(p => p.TotalMethods);
    public int TotalCyclomaticComplexity => Projects.Sum(p => p.TotalCyclomaticComplexity);
    public int TotalLinesOfExecutableCode => Projects.Sum(p => p.TotalLinesOfExecutableCode);

    /// <summary>
    /// Coupling analysis results for the entire solution.
    /// </summary>
    public CouplingAnalysis? CouplingAnalysis { get; init; }

    /// <summary>
    /// Statistics about dependency aggregation performance.
    /// </summary>
    public DependencyCollectorStats? AggregationStats { get; init; }

    /// <summary>
    /// Git repository metadata if analyzed path is in a git repository.
    /// </summary>
    public GitRepositoryInfo? GitInfo { get; init; }
}

/// <summary>
/// Represents git repository metadata for an analysis report.
/// </summary>
public record GitRepositoryInfo(
    string CommitSha,
    string BranchName,
    string? RemoteUrl = null,
    bool IsDirty = false);
