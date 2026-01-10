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
}

/// <summary>
/// Represents code metrics for a single project.
/// </summary>
public record ProjectMetrics(
    string Name,
    string FilePath,
    IReadOnlyList<TypeMetrics> Types)
{
    public int TotalCyclomaticComplexity => Types.Sum(t => t.TotalCyclomaticComplexity);
    public int TotalLinesOfExecutableCode => Types.Sum(t => t.TotalLinesOfExecutableCode);
    public int MaxDepthOfInheritance => Types.Count > 0 ? Types.Max(t => t.DepthOfInheritance) : 0;
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
    public int TotalMethods => Projects.Sum(p => p.Types.Sum(t => t.Methods.Count));
    public int TotalCyclomaticComplexity => Projects.Sum(p => p.TotalCyclomaticComplexity);
    public int TotalLinesOfExecutableCode => Projects.Sum(p => p.TotalLinesOfExecutableCode);
}
