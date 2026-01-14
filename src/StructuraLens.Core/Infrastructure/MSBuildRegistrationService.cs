using Microsoft.Build.Locator;
using StructuraLens.Core.Abstractions;

namespace StructuraLens.Core.Infrastructure;

/// <summary>
/// Manages MSBuild registration using MSBuildLocator.
/// Thread-safe and idempotent.
/// </summary>
public sealed class MSBuildRegistrationService : IMSBuildRegistrationService
{
    private static bool _msBuildRegistered;
    private static readonly object _lock = new();

    /// <inheritdoc />
    public void EnsureMSBuildRegistered()
    {
        if (_msBuildRegistered) return;

        lock (_lock)
        {
            if (_msBuildRegistered) return;

            var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
            if (instances.Count > 0)
            {
                MSBuildLocator.RegisterInstance(instances.OrderByDescending(i => i.Version).First());
            }
            else
            {
                MSBuildLocator.RegisterDefaults();
            }
            _msBuildRegistered = true;
        }
    }
}
