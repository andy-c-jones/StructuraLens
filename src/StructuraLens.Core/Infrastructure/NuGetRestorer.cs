using System.Diagnostics;
using Microsoft.Extensions.Logging;
using StructuraLens.Core.Abstractions;

namespace StructuraLens.Core.Infrastructure;

/// <summary>
/// Restores NuGet packages using the dotnet CLI.
/// </summary>
public sealed class NuGetRestorer : INuGetRestorer
{
    private readonly ILogger<NuGetRestorer> _logger;

    public NuGetRestorer(ILogger<NuGetRestorer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task RestorePackagesAsync(string projectOrSolutionPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        _logger.LogDebug("Starting package restore for {Path}", projectOrSolutionPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"restore \"{projectOrSolutionPath}\" --verbosity normal --interactive",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            _logger.LogError("Failed to start dotnet restore process. Ensure the .NET SDK is installed and 'dotnet' is available in PATH.");
            return;
        }

        // Read stdout and stderr concurrently to avoid deadlocks
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError("Package restore failed with exit code {ExitCode} for {Path}", process.ExitCode, projectOrSolutionPath);

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                _logger.LogError("Restore stderr: {Error}", stderr);
            }

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                _logger.LogError("Restore stdout: {Output}", stdout);
            }

            // Log common troubleshooting hints
            if (stderr.Contains("401") || stdout.Contains("401") ||
                stderr.Contains("Unable to load the service index") || stdout.Contains("Unable to load the service index"))
            {
                _logger.LogError("Authentication failure detected. For private NuGet feeds, ensure credentials are configured. " +
                    "Options: (1) Use 'dotnet nuget add source' with credentials, (2) Configure nuget.config with credentials, " +
                    "(3) Use Azure Artifacts Credential Provider or similar for your feed type. " +
                    "See: https://learn.microsoft.com/en-us/nuget/consume-packages/consuming-packages-authenticated-feeds");
            }
        }
        else
        {
            _logger.LogDebug("Package restore completed successfully for {Path}", projectOrSolutionPath);
        }
    }
}
