using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Export;

/// <summary>
/// Converts AnalysisReport to compact format optimized for size and visualization.
/// </summary>
public sealed class CompactReportExporter : IReportExporter
{
    /// <inheritdoc />
    public CompactReport Export(
        AnalysisReport report,
        bool includeMethodDetails = false,
        bool includeTypeDetails = false)
    {
        return ExportCore(report, includeMethodDetails, includeTypeDetails, useNamespaceHierarchy: false);
    }

    /// <summary>
    /// Exports the report with hierarchical namespace structure for the HTML report.
    /// </summary>
    public CompactReport ExportHierarchical(
        AnalysisReport report,
        bool includeMethodDetails = false,
        bool includeTypeDetails = false)
    {
        return ExportCore(report, includeMethodDetails, includeTypeDetails, useNamespaceHierarchy: true);
    }

    private static CompactReport ExportCore(
        AnalysisReport report,
        bool includeMethodDetails,
        bool includeTypeDetails,
        bool useNamespaceHierarchy)
    {
        return new CompactReport
        {
            Version = 1,
            Path = report.SolutionPath,
            Timestamp = new DateTimeOffset(report.AnalyzedAt).ToUnixTimeMilliseconds(),
            Projects = CompactProjectMapper.Export(report, includeTypeDetails, includeMethodDetails, useNamespaceHierarchy),
            Graph = CompactGraphBuilder.Build(report),
            Diagnostics = CompactDiagnosticsMapper.Export(report),
            GitCommitSha = report.GitInfo?.CommitSha,
            GitBranch = report.GitInfo?.BranchName,
            GitRemoteUrl = report.GitInfo?.RemoteUrl,
            GitIsDirty = report.GitInfo?.IsDirty ?? false,
            ToolVersion = report.ToolVersion,
            HasComplexityMetrics = report.AnalysisMode == AnalysisMode.Full
        };
    }
}
