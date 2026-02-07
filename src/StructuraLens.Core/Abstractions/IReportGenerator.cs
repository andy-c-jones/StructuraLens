using StructuraLens.Core.Models;

namespace StructuraLens.Core.Abstractions;

/// <summary>
/// Generates HTML reports from analysis data.
/// </summary>
public interface IReportGenerator
{
    /// <summary>
    /// Generates an HTML report from an analysis report.
    /// </summary>
    /// <param name="report">The analysis report to convert to HTML.</param>
    /// <returns>HTML string containing the interactive report.</returns>
    string GenerateHtml(AnalysisReport report);

    /// <summary>
    /// Generates an HTML report with diff data.
    /// </summary>
    /// <param name="report">The analysis report to convert to HTML.</param>
    /// <param name="diff">Diff report between base and head analysis.</param>
    /// <returns>HTML string containing the interactive report.</returns>
    string GenerateHtml(AnalysisReport report, AnalysisDiffReport diff);
}
