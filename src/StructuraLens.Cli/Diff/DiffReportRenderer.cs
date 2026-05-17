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

    public string RenderMarkdown(AnalysisDiffReport diff, int maxProjects = 10, DiagnosticLevel minDiagnosticLevel = DiagnosticLevel.Info)
    {
        var sb = new StringBuilder();
        RenderHeader(diff, sb);
        RenderDiagnosticsSection(diff, sb, minDiagnosticLevel);
        RenderProjectReferencesChanges(diff, sb, maxProjects);
        RenderExternalDependenciesChanges(diff, sb, maxProjects);

        if (diff.HasComplexityMetrics)
        {
            RenderMaintainabilityChanges(diff, sb);
        }

        RenderOverallMetrics(diff, sb);

        return sb.ToString();
    }

    private static void RenderHeader(AnalysisDiffReport diff, StringBuilder sb)
    {
        sb.AppendLine("## StructuraLens Diff Summary");
        sb.AppendLine();
        sb.AppendLine($"Base: `{ShortSha(diff.Base.CommitSha)}` {diff.Base.BranchName}");
        sb.AppendLine($"Head: `{ShortSha(diff.Head.CommitSha)}` {diff.Head.BranchName}");
        sb.AppendLine($"Analysis mode: `{diff.Head.AnalysisMode}`");
        sb.AppendLine();
    }

    private static void RenderDiagnosticsSection(AnalysisDiffReport diff, StringBuilder sb, DiagnosticLevel minDiagnosticLevel)
    {
        AppendDiagnosticsSummaryTable(diff, sb);
        RenderDiagnosticsChangeTables(diff, sb, minDiagnosticLevel);
    }

    private static void AppendDiagnosticsSummaryTable(AnalysisDiffReport diff, StringBuilder sb)
    {
        var (solvedErrors, addedErrors) = NormalizeSolvedAdded(
            diff.Totals.BaseErrors,
            diff.Totals.HeadErrors,
            diff.Diagnostics.ResolvedErrors,
            diff.Diagnostics.NewErrors,
            diff.Diagnostics.MovedErrors);
        var (solvedWarnings, addedWarnings) = NormalizeSolvedAdded(
            diff.Totals.BaseWarnings,
            diff.Totals.HeadWarnings,
            diff.Diagnostics.ResolvedWarnings,
            diff.Diagnostics.NewWarnings,
            diff.Diagnostics.MovedWarnings);
        var (solvedInfo, addedInfo) = NormalizeSolvedAdded(
            diff.Totals.BaseInfo,
            diff.Totals.HeadInfo,
            diff.Diagnostics.ResolvedInfo,
            diff.Diagnostics.NewInfo,
            diff.Diagnostics.MovedInfo);

        sb.AppendLine("### Diagnostics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Base | Head | Solved | Added |");
        sb.AppendLine("| --- | ---: | ---: | ---: | ---: |");
        sb.AppendLine(BuildSolvedAddedRow("Errors",
            diff.Totals.BaseErrors,
            diff.Totals.HeadErrors,
            solvedErrors,
            addedErrors,
            DeltaSemantic.CriticalIncrease));
        sb.AppendLine(BuildSolvedAddedRow("Warnings",
            diff.Totals.BaseWarnings,
            diff.Totals.HeadWarnings,
            solvedWarnings,
            addedWarnings,
            DeltaSemantic.BadIncrease));
        sb.AppendLine(BuildSolvedAddedRow("Info",
            diff.Totals.BaseInfo,
            diff.Totals.HeadInfo,
            solvedInfo,
            addedInfo));
        sb.AppendLine();
    }

    private static void RenderOverallMetrics(AnalysisDiffReport diff, StringBuilder sb)
    {
        sb.AppendLine("### Overall Metrics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Base | Head | Delta |");
        sb.AppendLine("| --- | ---: | ---: | ---: |");
        sb.AppendLine(BuildRow("Projects", diff.Totals.BaseProjects, diff.Totals.HeadProjects, diff.Totals.ProjectsDelta));
        if (diff.HasComplexityMetrics)
        {
            sb.AppendLine(BuildRow("Types", diff.Totals.BaseTypes, diff.Totals.HeadTypes, diff.Totals.TypesDelta));
            sb.AppendLine(BuildRow("Methods", diff.Totals.BaseMethods, diff.Totals.HeadMethods, diff.Totals.MethodsDelta));
        }

        if (diff.HasComplexityMetrics)
        {
            sb.AppendLine(BuildRow("Cyclomatic Complexity",
                diff.Totals.BaseCyclomaticComplexity,
                diff.Totals.HeadCyclomaticComplexity,
                diff.Totals.CyclomaticComplexityDelta,
                GetComplexityDeltaSemantic(diff.Totals)));

            sb.AppendLine(BuildRow("Lines of Code", diff.Totals.BaseLinesOfCode, diff.Totals.HeadLinesOfCode, diff.Totals.LinesOfCodeDelta));

            sb.AppendLine(BuildRow("Avg Maintainability",
                diff.Totals.BaseAvgMaintainabilityIndex,
                diff.Totals.HeadAvgMaintainabilityIndex,
                diff.Totals.AvgMaintainabilityDelta,
                GetMaintainabilityDeltaSemantic(diff.Totals.AvgMaintainabilityDelta)));
        }
        sb.AppendLine();
    }

    private static void RenderDiagnosticsChangeTables(AnalysisDiffReport diff, StringBuilder sb, DiagnosticLevel minDiagnosticLevel)
    {
        RenderDiagnosticItemsTable(sb, "#### Added Diagnostics", diff.Diagnostics.AddedDiagnostics, minDiagnosticLevel);
        RenderDiagnosticItemsTable(sb, "#### Resolved Diagnostics", diff.Diagnostics.ResolvedDiagnostics, minDiagnosticLevel);
    }

    private static void RenderDiagnosticItemsTable(StringBuilder sb, string heading, IReadOnlyList<DiagnosticDiffItem> items, DiagnosticLevel minDiagnosticLevel = DiagnosticLevel.Info)
    {
        var filteredItems = items
            .Where(i => ParseSeverity(i.Severity) >= minDiagnosticLevel)
            .ToList();
        sb.AppendLine(heading);
        sb.AppendLine();

        if (filteredItems.Count == 0)
        {
            sb.AppendLine("None");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Severity | Code | Description | Location | File |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (var item in filteredItems)
        {
            sb.AppendLine($"| {Escape(item.Severity)} | {Escape(item.Id)} | {Escape(item.Message)} | {item.Line}:{item.Column} | {Escape(item.File)} |");
        }

        sb.AppendLine();
    }

    private static void RenderMaintainabilityChanges(AnalysisDiffReport diff, StringBuilder sb)
    {
        // Filter projects to only those with actual changes
        var projectsWithChanges = diff.Projects
            .Where(p => !p.IsAdded && !p.IsRemoved)
            .Where(p => p.MaintainabilityDelta != 0
                || p.CyclomaticComplexityDelta != 0
                || p.LinesOfCodeDelta != 0
                || p.WarningsDelta != 0)
            .OrderByDescending(p => Math.Abs(p.MaintainabilityDelta))
            .ToList();

        if (projectsWithChanges.Count > 0)
        {
            sb.AppendLine("### Maintainability Changes");
            sb.AppendLine();
            sb.AppendLine("| Project | MI (Base) | MI (Head) | Delta | Complexity Δ | LOC Δ | Warnings Δ |");
            sb.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");

            foreach (var project in projectsWithChanges)
            {
                sb.AppendLine(
                    $"| {Escape(project.Name)} | {project.Base.AvgMaintainabilityIndex:0.0} | {project.Head.AvgMaintainabilityIndex:0.0} | {FormatDelta(project.MaintainabilityDelta, GetMaintainabilityDeltaSemantic(project.MaintainabilityDelta))} | {FormatDelta(project.CyclomaticComplexityDelta)} | {FormatDelta(project.LinesOfCodeDelta)} | {FormatDelta(project.WarningsDelta, GetWarningsDeltaSemantic(project.WarningsDelta))} |");
            }
            sb.AppendLine();
        }
    }

    private static void RenderProjectReferencesChanges(AnalysisDiffReport diff, StringBuilder sb, int maxProjects)
    {
        var projectsWithDependencyChanges = diff.Projects
            .Where(p => !p.IsAdded && !p.IsRemoved)
            .Where(p => p.AddedProjectReferences.Count > 0)
            .OrderByDescending(p => p.AddedProjectReferences.Count)
            .ToList();

        if (projectsWithDependencyChanges.Count > 0)
        {
            var totalAdded = projectsWithDependencyChanges.Sum(p => p.AddedProjectReferences.Count);
            var uniqueAdded = projectsWithDependencyChanges
                .SelectMany(p => p.AddedProjectReferences)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            sb.AppendLine("### Project References Changes");
            sb.AppendLine();
            sb.AppendLine($"Added direct project refs in {projectsWithDependencyChanges.Count} project(s), {totalAdded} total change(s), {uniqueAdded} unique.");
            sb.AppendLine();

            foreach (var project in projectsWithDependencyChanges.Take(maxProjects))
            {
                sb.AppendLine($"- `{Escape(project.Name)}` → {FormatDependencySummary(project.AddedProjectReferences, diff.NewToSolution)}");
            }

            if (projectsWithDependencyChanges.Count > maxProjects)
            {
                sb.AppendLine($"- ...and {projectsWithDependencyChanges.Count - maxProjects} more project(s)");
            }

            sb.AppendLine();
        }
    }

    private static void RenderExternalDependenciesChanges(AnalysisDiffReport diff, StringBuilder sb, int maxProjects)
    {
        var projectsWithExternalChanges = diff.Projects
            .Where(p => !p.IsAdded && !p.IsRemoved)
            .Where(p => p.AddedBclDependencies.Count > 0 || p.AddedPackageDependencies.Count > 0)
            .OrderByDescending(p => Math.Abs(p.ExternalDependenciesDelta))
            .ToList();

        if (projectsWithExternalChanges.Count > 0)
        {
            var totalBclAdded = projectsWithExternalChanges.Sum(p => p.AddedBclDependencies.Count);
            var totalPackagesAdded = projectsWithExternalChanges.Sum(p => p.AddedPackageDependencies.Count);

            sb.AppendLine("### NuGet Dependencies Changes");
            sb.AppendLine();
            sb.AppendLine($"Added direct dependencies in {projectsWithExternalChanges.Count} project(s): {totalBclAdded} BCL, {totalPackagesAdded} package.");
            sb.AppendLine();

            foreach (var project in projectsWithExternalChanges.Take(maxProjects))
            {
                var parts = new List<string>();
                if (project.AddedBclDependencies.Count > 0)
                {
                    parts.Add($"BCL: {FormatDependencySummary(project.AddedBclDependencies, diff.NewToSolution)}");
                }

                if (project.AddedPackageDependencies.Count > 0)
                {
                    parts.Add($"Packages: {FormatDependencySummary(project.AddedPackageDependencies, diff.NewToSolution)}");
                }

                sb.AppendLine($"- `{Escape(project.Name)}` → {string.Join("; ", parts)}");
            }

            if (projectsWithExternalChanges.Count > maxProjects)
            {
                sb.AppendLine($"- ...and {projectsWithExternalChanges.Count - maxProjects} more project(s)");
            }

            sb.AppendLine();
        }
    }

    private static string BuildRow(string label, int baseValue, int headValue, int delta, DeltaSemantic semantic = DeltaSemantic.Neutral)
    {
        return $"| {label} | {baseValue} | {headValue} | {FormatDelta(delta, semantic)} |";
    }

    private static string BuildSolvedAddedRow(
        string label,
        int baseValue,
        int headValue,
        int solved,
        int added,
        DeltaSemantic addedSemantic = DeltaSemantic.Neutral)
    {
        return $"| {label} | {baseValue} | {headValue} | {FormatCount(solved)} | {FormatCount(added, addedSemantic)} |";
    }

    private static string BuildRow(string label, double baseValue, double headValue, double delta, DeltaSemantic semantic = DeltaSemantic.Neutral)
    {
        return $"| {label} | {baseValue:0.0} | {headValue:0.0} | {FormatDelta(delta, semantic)} |";
    }

    private static string FormatCount(int value, DeltaSemantic semantic = DeltaSemantic.Neutral)
    {
        if (value == 0) return "0";
        return ApplySemanticFormatting(value.ToString(), semantic);
    }

    private static (int Solved, int Added) NormalizeSolvedAdded(int baseCount, int headCount, int solved, int added, int moved)
    {
        if (solved > 0 || added > 0 || moved > 0) return (solved, added);

        var delta = headCount - baseCount;
        return delta switch
        {
            > 0 => (0, delta),
            < 0 => (-delta, 0),
            _ => (0, 0)
        };
    }

    private static DeltaSemantic GetComplexityDeltaSemantic(DiffTotals totals)
    {
        var complexityDelta = totals.CyclomaticComplexityDelta;
        var complexityPercent = totals.BaseCyclomaticComplexity > 0
            ? (double)complexityDelta / totals.BaseCyclomaticComplexity
            : 0;

        return complexityDelta > SignificantComplexityAbsolute || complexityPercent > SignificantComplexityPercent
            ? DeltaSemantic.BadIncrease
            : DeltaSemantic.Neutral;
    }

    private static DeltaSemantic GetMaintainabilityDeltaSemantic(double delta)
    {
        return delta <= SevereMaintainabilityDrop ? DeltaSemantic.SevereDecrease
            : delta <= ModerateMaintainabilityDrop ? DeltaSemantic.ModerateDecrease
            : delta > 0 ? DeltaSemantic.GoodIncrease
            : DeltaSemantic.Neutral;
    }

    private static DeltaSemantic GetWarningsDeltaSemantic(int delta)
    {
        return delta > 0 ? DeltaSemantic.BadIncrease
            : delta < 0 ? DeltaSemantic.GoodDecrease
            : DeltaSemantic.Neutral;
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
            DeltaSemantic.NeedsReview => $"🔍 **{value}**",
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

    private static DiagnosticLevel ParseSeverity(string severity)
    {
        return Enum.TryParse<DiagnosticLevel>(severity, ignoreCase: true, out var level)
            ? level
            : DiagnosticLevel.Hidden;
    }

    private static string FormatDependencySummary(
        IReadOnlyList<string> dependencies,
        IReadOnlySet<string> newToSolution,
        int maxItems = 3)
    {
        if (dependencies.Count == 0)
        {
            return "none";
        }

        var displayed = dependencies.Take(maxItems)
            .Select(dep =>
            {
                var marker = newToSolution.Contains(dep) ? " 🆕" : "";
                return $"`{Escape(dep)}`{marker}";
            })
            .ToList();

        if (dependencies.Count > maxItems)
        {
            displayed.Add($"+{dependencies.Count - maxItems} more");
        }

        return string.Join(", ", displayed);
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
    GoodIncrease,

    /// <summary>
    /// Needs review/careful consideration (e.g., external dependencies added) - uses 🔍 emoji.
    /// </summary>
    NeedsReview
}
