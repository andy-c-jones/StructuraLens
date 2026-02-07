using System.Text;
using StructuraLens.Core.Models;

namespace StructuraLens.Cli.Diff;

public sealed class DiffReportRenderer
{
    public string RenderMarkdown(AnalysisDiffReport diff, int maxProjects = 10)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## StructuraLens Diff Summary");
        sb.AppendLine();
        sb.AppendLine($"Base: `{ShortSha(diff.Base.CommitSha)}` {diff.Base.BranchName}");
        sb.AppendLine($"Head: `{ShortSha(diff.Head.CommitSha)}` {diff.Head.BranchName}");
        sb.AppendLine();

        sb.AppendLine("### Totals");
        sb.AppendLine();
        sb.AppendLine("| Metric | Base | Head | Delta |");
        sb.AppendLine("| --- | ---: | ---: | ---: |");
        sb.AppendLine(BuildRow("Projects", diff.Totals.BaseProjects, diff.Totals.HeadProjects, diff.Totals.ProjectsDelta));
        sb.AppendLine(BuildRow("Types", diff.Totals.BaseTypes, diff.Totals.HeadTypes, diff.Totals.TypesDelta));
        sb.AppendLine(BuildRow("Methods", diff.Totals.BaseMethods, diff.Totals.HeadMethods, diff.Totals.MethodsDelta));
        sb.AppendLine(BuildRow("Cyclomatic Complexity", diff.Totals.BaseCyclomaticComplexity, diff.Totals.HeadCyclomaticComplexity, diff.Totals.CyclomaticComplexityDelta));
        sb.AppendLine(BuildRow("Lines of Code", diff.Totals.BaseLinesOfCode, diff.Totals.HeadLinesOfCode, diff.Totals.LinesOfCodeDelta));
        sb.AppendLine(BuildRow("Avg Maintainability", diff.Totals.BaseAvgMaintainabilityIndex, diff.Totals.HeadAvgMaintainabilityIndex, diff.Totals.AvgMaintainabilityDelta));
        sb.AppendLine();

        sb.AppendLine("### Diagnostics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Base | Head | Delta |");
        sb.AppendLine("| --- | ---: | ---: | ---: |");
        sb.AppendLine(BuildRow("Errors", diff.Totals.BaseErrors, diff.Totals.HeadErrors, diff.Totals.ErrorsDelta));
        sb.AppendLine(BuildRow("Warnings", diff.Totals.BaseWarnings, diff.Totals.HeadWarnings, diff.Totals.WarningsDelta));
        sb.AppendLine(BuildRow("Info", diff.Totals.BaseInfo, diff.Totals.HeadInfo, diff.Totals.InfoDelta));
        sb.AppendLine(BuildRow("Hidden", diff.Totals.BaseHidden, diff.Totals.HeadHidden, diff.Totals.HiddenDelta));
        sb.AppendLine();

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
                sb.AppendLine(
                    $"| {Escape(project.Name)} | {project.Base.AvgMaintainabilityIndex:0.0} | {project.Head.AvgMaintainabilityIndex:0.0} | {FormatDelta(project.MaintainabilityDelta)} | {FormatDelta(project.CyclomaticComplexityDelta)} | {FormatDelta(project.LinesOfCodeDelta)} | {FormatDelta(project.WarningsDelta)} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildRow(string label, int baseValue, int headValue, int delta)
    {
        return $"| {label} | {baseValue} | {headValue} | {FormatDelta(delta)} |";
    }

    private static string BuildRow(string label, double baseValue, double headValue, double delta)
    {
        return $"| {label} | {baseValue:0.0} | {headValue:0.0} | {FormatDelta(delta)} |";
    }

    private static string FormatDelta(int value)
    {
        return value > 0 ? $"+{value}" : value.ToString();
    }

    private static string FormatDelta(double value)
    {
        if (Math.Abs(value) < 0.0001) return "0";
        return value > 0 ? $"+{value:0.0}" : value.ToString("0.0");
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
