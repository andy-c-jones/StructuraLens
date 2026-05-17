using Microsoft.Extensions.Logging;
using StructuraLens.Core.Models;

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
        EventId = 4007,
        Level = LogLevel.Information,
        Message = "Dependency aggregation strategy: {strategy}")]
    public static partial void AggregationStrategy(ILogger logger, DependencyAggregationStrategy strategy);

    [LoggerMessage(
        EventId = 4008,
        Level = LogLevel.Information,
        Message = "Memory threshold: {thresholdMB} MB")]
    public static partial void MemoryThreshold(ILogger logger, long thresholdMB);

    [LoggerMessage(
        EventId = 4010,
        Level = LogLevel.Information,
        Message = "=== Dependency Aggregation Stats ===")]
    public static partial void AggregationStatsHeader(ILogger logger);

    [LoggerMessage(
        EventId = 4016,
        Level = LogLevel.Debug,
        Message = "Git repository detected: {branch} @ {commit}")]
    public static partial void GitRepositoryDetected(ILogger logger, string branch, string commit);

    [LoggerMessage(
        EventId = 4017,
        Level = LogLevel.Information,
        Message = "Generated default output filename: {filename}")]
    public static partial void GeneratedDefaultFilename(ILogger logger, string filename);

    [LoggerMessage(
        EventId = 4018,
        Level = LogLevel.Debug,
        Message = "Not in a git repository, using timestamp-based filename")]
    public static partial void NotInGitRepository(ILogger logger);

    [LoggerMessage(
        EventId = 4019,
        Level = LogLevel.Warning,
        Message = "Analyzing uncommitted changes (dirty working tree)")]
    public static partial void DirtyWorkingTree(ILogger logger);

    [LoggerMessage(
        EventId = 4011,
        Level = LogLevel.Information,
        Message = "Strategy: {strategy}")]
    public static partial void AggregationStatsStrategy(ILogger logger, string strategy);

    [LoggerMessage(
        EventId = 4011,
        Level = LogLevel.Information,
        Message = "Total edges processed: {totalEdges:N0}")]
    public static partial void AggregationStatsTotalEdges(ILogger logger, long totalEdges);

    [LoggerMessage(
        EventId = 4012,
        Level = LogLevel.Information,
        Message = "Unique edges: {uniqueEdges:N0}")]
    public static partial void AggregationStatsUniqueEdges(ILogger logger, long uniqueEdges);

    [LoggerMessage(
        EventId = 4013,
        Level = LogLevel.Information,
        Message = "Deduplication: {deduplicationRatio:P1}")]
    public static partial void AggregationStatsDeduplication(ILogger logger, double deduplicationRatio);

    [LoggerMessage(
        EventId = 4014,
        Level = LogLevel.Information,
        Message = "Memory usage: {memoryUsageMB:F1} MB")]
    public static partial void AggregationStatsMemory(ILogger logger, double memoryUsageMB);

    [LoggerMessage(
        EventId = 4015,
        Level = LogLevel.Information,
        Message = "Database: {databasePath}")]
    public static partial void AggregationStatsDatabase(ILogger logger, string databasePath);

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

    [LoggerMessage(
        EventId = 4020,
        Level = LogLevel.Information,
        Message = "Diff started: base={basePath} head={headPath} format={format} output={outputPath} maxProjects={maxProjects}")]
    public static partial void DiffStarted(
        ILogger logger,
        string basePath,
        string headPath,
        string format,
        string outputPath,
        int maxProjects);

    [LoggerMessage(
        EventId = 4021,
        Level = LogLevel.Information,
        Message = "Diff completed: format={format} output={outputPath}")]
    public static partial void DiffCompleted(ILogger logger, string format, string outputPath);

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

    [LoggerMessage(
        EventId = 4202,
        Level = LogLevel.Error,
        Message = "Diff failed: {errorMessage}")]
    public static partial void DiffFailed(ILogger logger, string errorMessage);
}
