using Microsoft.Extensions.Logging;
using StructuraLens.Cli.Logging;
using StructuraLens.Core.Models;

namespace StructuraLens.Cli;

internal static class OutputFilenameGenerator
{
    private static readonly char[] InvalidBranchNameChars =
    [
        '<', '>', ':', '"', '|', '?', '*', '/', '\\', '\0',
        .. Enumerable.Range(1, 31).Select(static value => (char)value)
    ];

    public static string GenerateDefaultFilename(AnalysisReport report, string format, ILogger logger)
    {
        if (report.GitInfo != null)
        {
            var sanitizedBranch = SanitizeBranchName(report.GitInfo.BranchName);
            var extension = GetExtension(format);
            var shortSha = report.GitInfo.CommitSha[..7];
            var output = $"{shortSha}-{sanitizedBranch}.{extension}";
            ProgramLog.GitRepositoryDetected(logger, report.GitInfo.BranchName, shortSha);
            ProgramLog.GeneratedDefaultFilename(logger, output);
            return output;
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
        var fallbackExtension = GetExtension(format);
        var fallbackOutput = $"report-{timestamp}.{fallbackExtension}";
        ProgramLog.NotInGitRepository(logger);
        ProgramLog.GeneratedDefaultFilename(logger, fallbackOutput);
        return fallbackOutput;
    }

    public static string SanitizeBranchName(string branchName)
    {
        var result = branchName;
        foreach (char c in InvalidBranchNameChars)
        {
            result = result.Replace(c, '_');
        }

        return result;
    }

    private static string GetExtension(string format)
    {
        return format switch
        {
            "html" => "html",
            "compact" => "slr",
            "json" => "json",
            _ => "json"
        };
    }
}
