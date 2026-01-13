namespace StructuraLens.Core.Abstractions;

/// <summary>
/// Abstracts MSBuild registration for testability.
/// </summary>
public interface IMSBuildRegistrationService
{
    /// <summary>
    /// Ensures MSBuild is registered with the MSBuildLocator.
    /// Thread-safe and idempotent - can be called multiple times safely.
    /// </summary>
    void EnsureMSBuildRegistered();
}
