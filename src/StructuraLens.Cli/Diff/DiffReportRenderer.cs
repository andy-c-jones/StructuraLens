using System.Text;
using StructuraLens.Core.Models;

namespace StructuraLens.Cli.Diff;

public sealed class DiffReportRenderer
{
    // Thresholds for highlighting significant changes
    private const int SignificantComplexityAbsolute = 50;
    private const double SignificantComplexityPercent = 0.10; // 10%
    private const double SevereMaintainabilityDrop = -10.0;
    private const double ModerateMaintainabilityDrop = -5.0;

    public string RenderMarkdown(AnalysisDiffReport diff, int maxProjects = 10)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## StructuraLens Diff Summary");
        sb.AppendLine();
        sb.AppendLine($"Base: `{ShortSha(diff.Base.CommitSha)}` {diff.Base.BranchName}");
        sb.AppendLine($"Head: `{ShortSha(diff.Head.CommitSha)}` {diff.Head.BranchName}");
        sb.AppendLine();

        // Section 1: Diagnostics (most critical - errors and warnings)
        sb.AppendLine("### Diagnostics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Base | Head | Delta |");
        sb.AppendLine("| --- | ---: | ---: | ---: |");
        sb.AppendLine(BuildRow("Errors", 
            diff.Totals.BaseErrors, 
            diff.Totals.HeadErrors, 
            diff.Totals.ErrorsDelta,
            diff.Totals.ErrorsDelta > 0 ? DeltaSemantic.CriticalIncrease 
                : diff.Totals.ErrorsDelta < 0 ? DeltaSemantic.GoodDecrease 
                : DeltaSemantic.Neutral));
        sb.AppendLine(BuildRow("Warnings", 
            diff.Totals.BaseWarnings, 
            diff.Totals.HeadWarnings, 
            diff.Totals.WarningsDelta,
            diff.Totals.WarningsDelta > 0 ? DeltaSemantic.BadIncrease 
                : diff.Totals.WarningsDelta < 0 ? DeltaSemantic.GoodDecrease 
                : DeltaSemantic.Neutral));
        sb.AppendLine(BuildRow("Info", diff.Totals.BaseInfo, diff.Totals.HeadInfo, diff.Totals.InfoDelta));
        sb.AppendLine(BuildRow("Hidden", diff.Totals.BaseHidden, diff.Totals.HeadHidden, diff.Totals.HiddenDelta));
        sb.AppendLine();

        // Section 2: Top Maintainability Changes (per-project breakdown)
        if (diff.Projects.Count > 0)
        {
            sb.AppendLine("### Top Maintainability Changes");
            sb.AppendLine();
            sb.AppendLine("| Project | MI (Base) | MI (Head) | Delta | Complexity Δ | LOC Δ | Warnings Δ |");
            sb.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");

            foreach (var project in diff.Projects
                .Where(p => !p.IsAdded && !p.IsRemoved)
                .OrderByDescending(p => Math.Abs(p.MaintainabilityDelta))
                .Take(maxProjects))
            {
                // Determine MI delta severity
                var miDeltaSemantic = project.MaintainabilityDelta <= SevereMaintainabilityDrop 
                    ? DeltaSemantic.SevereDecrease
                    : project.MaintainabilityDelta <= ModerateMaintainabilityDrop 
                    ? DeltaSemantic.ModerateDecrease
                    : project.MaintainabilityDelta > 0 
                    ? DeltaSemantic.GoodIncrease
                    : DeltaSemantic.Neutral;

                // Determine warnings delta semantic
                var warningsSemantic = project.WarningsDelta > 0 
                    ? DeltaSemantic.BadIncrease 
                    : project.WarningsDelta < 0 
                    ? DeltaSemantic.GoodDecrease 
                    : DeltaSemantic.Neutral;

                sb.AppendLine(
                    $"| {Escape(project.Name)} | {project.Base.AvgMaintainabilityIndex:0.0} | {project.Head.AvgMaintainabilityIndex:0.0} | {FormatDelta(project.MaintainabilityDelta, miDeltaSemantic)} | {FormatDelta(project.CyclomaticComplexityDelta)} | {FormatDelta(project.LinesOfCodeDelta)} | {FormatDelta(project.WarningsDelta, warningsSemantic)} |");
            }
            sb.AppendLine();
        }

        // Section 3: Top Level Metrics (overall statistics)
        sb.AppendLine("### Top Level Metrics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Base | Head | Delta |");
        sb.AppendLine("| --- | ---: | ---: | ---: |");
        sb.AppendLine(BuildRow("Projects", diff.Totals.BaseProjects, diff.Totals.HeadProjects, diff.Totals.ProjectsDelta));
        sb.AppendLine(BuildRow("Types", diff.Totals.BaseTypes, diff.Totals.HeadTypes, diff.Totals.TypesDelta));
        sb.AppendLine(BuildRow("Methods", diff.Totals.BaseMethods, diff.Totals.HeadMethods, diff.Totals.MethodsDelta));
        
        // Highlight significant complexity increases
        var complexityDelta = diff.Totals.CyclomaticComplexityDelta;
        var complexityPercent = diff.Totals.BaseCyclomaticComplexity > 0 
            ? (double)complexityDelta / diff.Totals.BaseCyclomaticComplexity 
            : 0;
        var isSignificantComplexity = complexityDelta > SignificantComplexityAbsolute 
            || complexityPercent > SignificantComplexityPercent;
        sb.AppendLine(BuildRow("Cyclomatic Complexity", 
            diff.Totals.BaseCyclomaticComplexity, 
            diff.Totals.HeadCyclomaticComplexity, 
            diff.Totals.CyclomaticComplexityDelta,
            isSignificantComplexity ? DeltaSemantic.BadIncrease : DeltaSemantic.Neutral));
        
        sb.AppendLine(BuildRow("Lines of Code", diff.Totals.BaseLinesOfCode, diff.Totals.HeadLinesOfCode, diff.Totals.LinesOfCodeDelta));
        
        // Highlight significant maintainability drops
        var miDelta = diff.Totals.AvgMaintainabilityDelta;
        var miSemantic = miDelta <= SevereMaintainabilityDrop ? DeltaSemantic.SevereDecrease
            : miDelta <= ModerateMaintainabilityDrop ? DeltaSemantic.ModerateDecrease
            : miDelta > 0 ? DeltaSemantic.GoodIncrease
            : DeltaSemantic.Neutral;
        sb.AppendLine(BuildRow("Avg Maintainability", 
            diff.Totals.BaseAvgMaintainabilityIndex, 
            diff.Totals.HeadAvgMaintainabilityIndex, 
            diff.Totals.AvgMaintainabilityDelta,
            miSemantic));
        sb.AppendLine();

        return sb.ToString();
    }

    private static string BuildRow(string label, int baseValue, int headValue, int delta, DeltaSemantic semantic = DeltaSemantic.Neutral)
    {
        return $"| {label} | {baseValue} | {headValue} | {FormatDelta(delta, semantic)} |";
    }

    private static string BuildRow(string label, double baseValue, double headValue, double delta, DeltaSemantic semantic = DeltaSemantic.Neutral)
    {
        return $"| {label} | {baseValue:0.0} | {headValue:0.0} | {FormatDelta(delta, semantic)} |";
    }

    private static string FormatDelta(int value, DeltaSemantic semantic = DeltaSemantic.Neutral)
    {
        if (value == 0) return "0";
        
        var sign = value > 0 ? "+" : "";
        var formatted = $"{sign}{value}";

        return ApplySemanticFormatting(formatted, semantic);
    }

    private static string FormatDelta(double value, DeltaSemantic semantic = DeltaSemantic.Neutral)
    {
        if (Math.Abs(value) < 0.0001) return "0";
        
        var sign = value > 0 ? "+" : "";
        var formatted = $"{sign}{value:0.0}";

        return ApplySemanticFormatting(formatted, semantic);
    }

    private static string ApplySemanticFormatting(string value, DeltaSemantic semantic)
    {
        return semantic switch
        {
            DeltaSemantic.CriticalIncrease => $"🚨 **{value}**",
            DeltaSemantic.BadIncrease => $"⚠️ **{value}**",
            DeltaSemantic.SevereDecrease => $"🔴 **{value}**",
            DeltaSemantic.ModerateDecrease => $"⚠️ **{value}**",
            DeltaSemantic.GoodDecrease => $"✅ {value}",
            DeltaSemantic.GoodIncrease => $"✅ {value}",
            _ => value
        };
    }

    private static string ShortSha(string? sha)
    {
        if (string.IsNullOrWhiteSpace(sha)) return "unknown";
        return sha.Length > 7 ? sha[..7] : sha;
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|");
    }
}

/// <summary>
/// Semantic meaning for delta values to determine appropriate visual emphasis.
/// </summary>
internal enum DeltaSemantic
{
    /// <summary>
    /// No special emphasis needed (neutral change).
    /// </summary>
    Neutral,

    /// <summary>
    /// Critical increase (e.g., errors increased) - uses 🚨 emoji and bold.
    /// </summary>
    CriticalIncrease,

    /// <summary>
    /// Bad increase (e.g., warnings or complexity increased significantly) - uses ⚠️ emoji and bold.
    /// </summary>
    BadIncrease,

    /// <summary>
    /// Severe decrease (e.g., maintainability dropped >10 points) - uses 🔴 emoji and bold.
    /// </summary>
    SevereDecrease,

    /// <summary>
    /// Moderate decrease (e.g., maintainability dropped 5-10 points) - uses ⚠️ emoji and bold.
    /// </summary>
    ModerateDecrease,

    /// <summary>
    /// Good decrease (e.g., errors or warnings decreased) - uses ✅ emoji.
    /// </summary>
    GoodDecrease,

    /// <summary>
    /// Good increase (e.g., maintainability increased) - uses ✅ emoji.
    /// </summary>
    GoodIncrease
}
