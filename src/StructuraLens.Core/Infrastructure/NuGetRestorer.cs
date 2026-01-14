using System.Diagnostics;
using Microsoft.Extensions.Logging;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Infrastructure.Logging;

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
        
        NuGetRestorerLog.StartingPackageRestore(_logger, projectOrSolutionPath);

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
            NuGetRestorerLog.FailedToStartRestoreProcess(_logger);
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
            NuGetRestorerLog.PackageRestoreFailed(_logger, process.ExitCode, projectOrSolutionPath);

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                NuGetRestorerLog.RestoreStderr(_logger, stderr);
            }

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                NuGetRestorerLog.RestoreStdout(_logger, stdout);
            }

            // Log common troubleshooting hints
            if (stderr.Contains("401") || stdout.Contains("401") ||
                stderr.Contains("Unable to load the service index") || stdout.Contains("Unable to load the service index"))
            {
                NuGetRestorerLog.AuthenticationFailureDetected(_logger);
            }
        }
        else
        {
            NuGetRestorerLog.PackageRestoreCompleted(_logger, projectOrSolutionPath);
        }
    }
}
