namespace StructuraLens.Core.Abstractions;

/// <summary>
/// Abstracts NuGet package restoration for testability.
/// </summary>
public interface INuGetRestorer
{
    /// <summary>
    /// Restores NuGet packages for the specified project or solution.
    /// </summary>
    /// <param name="projectOrSolutionPath">Path to the project or solution file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RestorePackagesAsync(string projectOrSolutionPath, CancellationToken cancellationToken = default);
}
