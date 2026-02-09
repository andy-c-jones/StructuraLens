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

        // Section 1: New Diagnostics (up to 20 most important, with warning if more)
        RenderNewDiagnostics(diff, sb);

        // Section 2: Diagnostics (most critical - errors and warnings)
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

        // Section 3: Internal Dependencies Changes
        RenderInternalDependenciesChanges(diff, sb, maxProjects);

        // Section 4: External Dependencies Changes
        RenderExternalDependenciesChanges(diff, sb, maxProjects);

        // Section 5: Maintainability Changes (per-project breakdown - only projects with changes)
        RenderMaintainabilityChanges(diff, sb);

        // Section 6: Overall Metrics (overall statistics)
        sb.AppendLine("### Overall Metrics");
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

    private static void RenderNewDiagnostics(AnalysisDiffReport diff, StringBuilder sb)
    {
        // Gather all new diagnostics (errors, warnings, info, hidden) with priority weighting
        var allNewDiagnostics = new List<(DiagnosticDiffItem Item, int Priority)>();
        
        // Priority: Error=4, Warning=3, Info=2, Hidden=1
        foreach (var error in diff.Diagnostics.TopNewErrors)
            allNewDiagnostics.Add((error, 4));
        foreach (var warning in diff.Diagnostics.TopNewWarnings)
            allNewDiagnostics.Add((warning, 3));
        
        var totalDiagnosticsCount = allNewDiagnostics.Count;
        
        // Take top 20 by priority, then by project name for stability
        var topDiagnostics = allNewDiagnostics
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Item.Project)
            .Take(20)
            .ToList();

        if (topDiagnostics.Count > 0)
        {
            // Add alarming emoji if more than 20 diagnostics exist
            var title = totalDiagnosticsCount > 20 
                ? "### 🔥 New Diagnostics" 
                : "### New Diagnostics";
            
            sb.AppendLine(title);
            sb.AppendLine();
            
            foreach (var (item, priority) in topDiagnostics)
            {
                var icon = priority switch
                {
                    4 => "🚨",  // Error
                    3 => "⚠️",   // Warning
                    2 => "ℹ️",   // Info
                    _ => "💡"   // Hidden
                };

                sb.AppendLine($"{icon} **{item.Id}** in `{item.Project}`");
                sb.AppendLine($"  - {Escape(item.Message)}");
                sb.AppendLine($"  - Location: `{item.File}:{item.Line}:{item.Column}`");
                sb.AppendLine();
            }
            
            // Add warning if there are more than 20 diagnostics
            if (totalDiagnosticsCount > 20)
            {
                sb.AppendLine($"⚠️ **Too many diagnostic issues added to show all of them** ({totalDiagnosticsCount} total, showing 20)");
                sb.AppendLine();
            }
        }
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
    }

    private static void RenderInternalDependenciesChanges(AnalysisDiffReport diff, StringBuilder sb, int maxProjects)
    {
        // Find projects with internal dependency changes
        var projectsWithDependencyChanges = diff.Projects
            .Where(p => !p.IsAdded && !p.IsRemoved)
            .Where(p => p.AddedInternalDependencies.Count > 0 || p.RemovedInternalDependencies.Count > 0)
            .OrderByDescending(p => p.AddedInternalDependencies.Count + p.RemovedInternalDependencies.Count)
            .Take(maxProjects)
            .ToList();

        if (projectsWithDependencyChanges.Count > 0)
        {
            sb.AppendLine("### Internal Dependencies Changes");
            sb.AppendLine();

            foreach (var project in projectsWithDependencyChanges)
            {
                sb.AppendLine($"#### {Escape(project.Name)}");
                sb.AppendLine();

                // Show added internal dependencies
                if (project.AddedInternalDependencies.Count > 0)
                {
                    sb.AppendLine($"**🔍 Added Internal Dependencies ({project.AddedInternalDependencies.Count}):**");
                    foreach (var dep in project.AddedInternalDependencies)
                    {
                        var isNewToSolution = diff.NewToSolution.Contains(dep);
                        var marker = isNewToSolution ? " 🆕 (new to solution)" : "";
                        sb.AppendLine($"- `{dep}`{marker}");
                    }
                    sb.AppendLine();
                }

                // Show removed internal dependencies
                if (project.RemovedInternalDependencies.Count > 0)
                {
                    sb.AppendLine($"**✅ Removed Internal Dependencies ({project.RemovedInternalDependencies.Count}):**");
                    foreach (var dep in project.RemovedInternalDependencies)
                    {
                        var isRemovedFromSolution = diff.RemovedFromSolution.Contains(dep);
                        var marker = isRemovedFromSolution ? " 🗑️ (removed from solution)" : "";
                        sb.AppendLine($"- `{dep}`{marker}");
                    }
                    sb.AppendLine();
                }
            }
        }
    }

    private static void RenderExternalDependenciesChanges(AnalysisDiffReport diff, StringBuilder sb, int maxProjects)
    {
        // Find projects with external dependency changes
        var projectsWithExternalChanges = diff.Projects
            .Where(p => !p.IsAdded && !p.IsRemoved)
            .Where(p => p.ExternalBclDependenciesDelta != 0 || p.ExternalPackageDependenciesDelta != 0)
            .OrderByDescending(p => Math.Abs(p.ExternalDependenciesDelta))
            .Take(maxProjects)
            .ToList();

        if (projectsWithExternalChanges.Count > 0)
        {
            sb.AppendLine("### External Dependencies Changes");
            sb.AppendLine();

            foreach (var project in projectsWithExternalChanges)
            {
                sb.AppendLine($"#### {Escape(project.Name)}");
                sb.AppendLine();

                // Show added BCL dependencies
                if (project.AddedBclDependencies.Count > 0)
                {
                    sb.AppendLine($"**🔍 Added BCL Dependencies ({project.AddedBclDependencies.Count}):**");
                    foreach (var dep in project.AddedBclDependencies)
                    {
                        var isNewToSolution = diff.NewToSolution.Contains(dep);
                        var marker = isNewToSolution ? " 🆕 (new to solution)" : "";
                        sb.AppendLine($"- `{dep}`{marker}");
                    }
                    sb.AppendLine();
                }

                // Show removed BCL dependencies
                if (project.RemovedBclDependencies.Count > 0)
                {
                    sb.AppendLine($"**✅ Removed BCL Dependencies ({project.RemovedBclDependencies.Count}):**");
                    foreach (var dep in project.RemovedBclDependencies)
                    {
                        var isRemovedFromSolution = diff.RemovedFromSolution.Contains(dep);
                        var marker = isRemovedFromSolution ? " 🗑️ (removed from solution)" : "";
                        sb.AppendLine($"- `{dep}`{marker}");
                    }
                    sb.AppendLine();
                }

                // Show added packages
                if (project.AddedPackageDependencies.Count > 0)
                {
                    sb.AppendLine($"**🔍 Added Third-Party Packages ({project.AddedPackageDependencies.Count}):**");
                    foreach (var dep in project.AddedPackageDependencies)
                    {
                        var isNewToSolution = diff.NewToSolution.Contains(dep);
                        var marker = isNewToSolution ? " 🆕 (new to solution)" : "";
                        sb.AppendLine($"- `{dep}`{marker}");
                    }
                    sb.AppendLine();
                }

                // Show removed packages
                if (project.RemovedPackageDependencies.Count > 0)
                {
                    sb.AppendLine($"**✅ Removed Third-Party Packages ({project.RemovedPackageDependencies.Count}):**");
                    foreach (var dep in project.RemovedPackageDependencies)
                    {
                        var isRemovedFromSolution = diff.RemovedFromSolution.Contains(dep);
                        var marker = isRemovedFromSolution ? " 🗑️ (removed from solution)" : "";
                        sb.AppendLine($"- `{dep}`{marker}");
                    }
                    sb.AppendLine();
                }
            }
        }
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
