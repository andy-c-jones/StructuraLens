using Microsoft.Extensions.Logging;

namespace StructuraLens.Core.Infrastructure.Logging;

/// <summary>
/// Source-generated high-performance logging methods for NuGetRestorer.
/// </summary>
internal static partial class NuGetRestorerLog
{
    // Operation events (3000-3099)

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Debug,
        Message = "Starting package restore for {path}")]
    public static partial void StartingPackageRestore(ILogger logger, string path);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "Package restore completed successfully for {path}")]
    public static partial void PackageRestoreCompleted(ILogger logger, string path);

    // Error events (3100-3199)

    [LoggerMessage(
        EventId = 3100,
        Level = LogLevel.Error,
        Message = "Failed to start dotnet restore process. Ensure the .NET SDK is installed and 'dotnet' is available in PATH.")]
    public static partial void FailedToStartRestoreProcess(ILogger logger);

    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Error,
        Message = "Package restore failed with exit code {exitCode} for {path}")]
    public static partial void PackageRestoreFailed(ILogger logger, int exitCode, string path);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Error,
        Message = "Restore stderr: {error}")]
    public static partial void RestoreStderr(ILogger logger, string error);

    [LoggerMessage(
        EventId = 3103,
        Level = LogLevel.Error,
        Message = "Restore stdout: {output}")]
    public static partial void RestoreStdout(ILogger logger, string output);

    [LoggerMessage(
        EventId = 3104,
        Level = LogLevel.Error,
        Message = "Authentication failure detected. For private NuGet feeds, ensure credentials are configured. Options: (1) Use 'dotnet nuget add source' with credentials, (2) Configure nuget.config with credentials, (3) Use Azure Artifacts Credential Provider or similar for your feed type. See: https://learn.microsoft.com/en-us/nuget/consume-packages/consuming-packages-authenticated-feeds")]
    public static partial void AuthenticationFailureDetected(ILogger logger);
}
