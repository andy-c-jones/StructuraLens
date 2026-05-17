using StructuraLens.Core.Models;

namespace StructuraLens.Core.Diff;

internal static class DiffMetadataFactory
{
    public static DiffMetadata Create(AnalysisReport report)
    {
        return new DiffMetadata
        {
            SolutionPath = report.SolutionPath,
            AnalyzedAt = report.AnalyzedAt,
            CommitSha = report.GitInfo?.CommitSha,
            BranchName = report.GitInfo?.BranchName,
            AnalysisMode = report.AnalysisMode
        };
    }
}
