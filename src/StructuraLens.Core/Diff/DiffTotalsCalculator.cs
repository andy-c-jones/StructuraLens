using StructuraLens.Core.Models;

namespace StructuraLens.Core.Diff;

internal static class DiffTotalsCalculator
{
    public static DiffTotals Build(AnalysisReport baseReport, AnalysisReport headReport)
    {
        var baseTotals = BuildSnapshot(baseReport);
        var headTotals = BuildSnapshot(headReport);

        return new DiffTotals
        {
            BaseProjects = baseTotals.Projects,
            HeadProjects = headTotals.Projects,
            BaseTypes = baseTotals.Types,
            HeadTypes = headTotals.Types,
            BaseMethods = baseTotals.Methods,
            HeadMethods = headTotals.Methods,
            BaseCyclomaticComplexity = baseTotals.CyclomaticComplexity,
            HeadCyclomaticComplexity = headTotals.CyclomaticComplexity,
            BaseLinesOfCode = baseTotals.LinesOfCode,
            HeadLinesOfCode = headTotals.LinesOfCode,
            BaseAvgMaintainabilityIndex = baseTotals.AvgMaintainability,
            HeadAvgMaintainabilityIndex = headTotals.AvgMaintainability,
            BaseErrors = baseTotals.Diagnostics.Errors,
            HeadErrors = headTotals.Diagnostics.Errors,
            BaseWarnings = baseTotals.Diagnostics.Warnings,
            HeadWarnings = headTotals.Diagnostics.Warnings,
            BaseInfo = baseTotals.Diagnostics.Info,
            HeadInfo = headTotals.Diagnostics.Info,
            BaseHidden = baseTotals.Diagnostics.Hidden,
            HeadHidden = headTotals.Diagnostics.Hidden
        };
    }

    private static DiffTotalsSnapshot BuildSnapshot(AnalysisReport report)
    {
        var allMethods = report.Projects.SelectMany(p => p.Types).GetAllMethods();
        var avgMi = allMethods.CalculateAverageMaintainabilityIndex();

        return new DiffTotalsSnapshot(
            report.TotalProjects,
            report.TotalTypes,
            report.TotalMethods,
            report.TotalCyclomaticComplexity,
            report.TotalLinesOfExecutableCode,
            Math.Round(avgMi, 1),
            DiagnosticDiffMatcher.BuildSummary(report));
    }

    private readonly record struct DiffTotalsSnapshot(
        int Projects,
        int Types,
        int Methods,
        int CyclomaticComplexity,
        int LinesOfCode,
        double AvgMaintainability,
        DiagnosticDiffSnapshot Diagnostics);
}
