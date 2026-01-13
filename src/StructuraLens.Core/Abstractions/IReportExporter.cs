using StructuraLens.Core.Models;

namespace StructuraLens.Core.Abstractions;

/// <summary>
/// Exports analysis reports to compact format.
/// </summary>
public interface IReportExporter
{
    /// <summary>
    /// Converts an AnalysisReport to compact format optimized for size and visualization.
    /// </summary>
    /// <param name="report">The full analysis report.</param>
    /// <param name="includeMethodDetails">Include individual method metrics.</param>
    /// <param name="includeTypeDetails">Include individual type metrics.</param>
    CompactReport Export(
        AnalysisReport report,
        bool includeMethodDetails = false,
        bool includeTypeDetails = false);
}
