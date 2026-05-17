using Microsoft.Extensions.Logging;

namespace StructuraLens.Core.Infrastructure.Logging;

/// <summary>
/// Source-generated high-performance logging methods for GitRepositoryService.
/// </summary>
internal static partial class GitRepositoryServiceLog
{
    // Git repository events (3200-3299)

    [LoggerMessage(
        EventId = 3200,
        Level = LogLevel.Debug,
        Message = "Failed to discover git repository for path: {path}")]
    public static partial void FailedToDiscoverRepository(ILogger logger, Exception exception, string path);

    [LoggerMessage(
        EventId = 3201,
        Level = LogLevel.Debug,
        Message = "Git metadata retrieved: Commit={commit}, Branch={branch}, Dirty={dirty}")]
    public static partial void GitMetadataRetrieved(ILogger logger, string commit, string branch, bool dirty);

    [LoggerMessage(
        EventId = 3210,
        Level = LogLevel.Warning,
        Message = "Git repository found but HEAD is null or has no commits")]
    public static partial void GitHeadUnavailable(ILogger logger);

    [LoggerMessage(
        EventId = 3211,
        Level = LogLevel.Warning,
        Message = "Failed to retrieve git metadata for path: {path}")]
    public static partial void FailedToRetrieveMetadata(ILogger logger, Exception exception, string path);
}
