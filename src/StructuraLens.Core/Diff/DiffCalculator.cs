using StructuraLens.Core.Models;

namespace StructuraLens.Core.Diff;

public sealed class DiffCalculator
{
    public AnalysisDiffReport Compare(AnalysisReport baseReport, AnalysisReport headReport)
    {
        var projectDiffs = ProjectDiffBuilder.Build(baseReport, headReport);
        var diagnostics = DiagnosticDiffMatcher.Build(baseReport, headReport);
        var (newToSolution, removedFromSolution) = SolutionDependencyPresenceCalculator.Compute(baseReport, headReport);

        return new AnalysisDiffReport
        {
            Base = DiffMetadataFactory.Create(baseReport),
            Head = DiffMetadataFactory.Create(headReport),
            HasComplexityMetrics = HasComplexityMetrics(baseReport, headReport),
            Totals = DiffTotalsCalculator.Build(baseReport, headReport),
            Projects = projectDiffs,
            Diagnostics = diagnostics,
            NewToSolution = newToSolution,
            RemovedFromSolution = removedFromSolution
        };
    }

    private static bool HasComplexityMetrics(AnalysisReport baseReport, AnalysisReport headReport)
    {
        return baseReport.AnalysisMode != AnalysisMode.DiagnosticsAndReferences &&
            headReport.AnalysisMode != AnalysisMode.DiagnosticsAndReferences;
    }
}
