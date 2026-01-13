using Microsoft.Extensions.Logging;

namespace StructuraLens.Cli.Logging;

/// <summary>
/// Source-generated high-performance logging methods for CLI Program.
/// </summary>
internal static partial class ProgramLog
{
    // CLI operation events (4000-4099)
    
    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Information,
        Message = "StructuraLens v{version}")]
    public static partial void ApplicationStartup(ILogger logger, string version);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Analyzing: {path}")]
    public static partial void AnalyzingPath(ILogger logger, string path);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Information,
        Message = "Coupling mode: {mode}")]
    public static partial void CouplingModeEnabled(ILogger logger, string mode);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Information,
        Message = "Analysis completed successfully")]
    public static partial void AnalysisCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Information,
        Message = "Report written to: {outputPath}")]
    public static partial void ReportWritten(ILogger logger, string outputPath);

    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Information,
        Message = "Compact report written to: {outputPath} ({sizeBytes} bytes)")]
    public static partial void CompactReportWritten(ILogger logger, string outputPath, long sizeBytes);

    [LoggerMessage(
        EventId = 4006,
        Level = LogLevel.Information,
        Message = "HTML report written to: {outputPath} ({sizeBytes} bytes)")]
    public static partial void HtmlReportWritten(ILogger logger, string outputPath, long sizeBytes);

    // Warning events (4100-4199)
    
    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Warning,
        Message = "{warningMessage}")]
    public static partial void AnalysisWarning(ILogger logger, string warningMessage);

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Warning,
        Message = "... and {additionalWarningCount} more warnings")]
    public static partial void AdditionalWarnings(ILogger logger, int additionalWarningCount);

    // Error events (4200-4299)
    
    [LoggerMessage(
        EventId = 4200,
        Level = LogLevel.Error,
        Message = "{errorMessage}")]
    public static partial void AnalysisError(ILogger logger, string errorMessage);

    [LoggerMessage(
        EventId = 4201,
        Level = LogLevel.Error,
        Message = "Fatal error during analysis")]
    public static partial void FatalError(ILogger logger, Exception exception);
}
