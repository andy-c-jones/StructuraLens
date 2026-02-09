using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Logging;

namespace StructuraLens.Core.Infrastructure;

/// <summary>
/// Reads package references from project files using MSBuild APIs.
/// Supports Directory.Build.props, Directory.Packages.props, and Central Package Management (CPM).
/// </summary>
internal sealed partial class PackageReferenceReader
{
    private readonly ILogger _logger;

    public PackageReferenceReader(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Reads all package references for a project, including those inherited from
    /// Directory.Build.props and Directory.Packages.props files.
    /// </summary>
    /// <param name="projectFilePath">Path to the .csproj file.</param>
    /// <param name="globalProperties">MSBuild global properties (e.g., Configuration, Platform).</param>
    /// <returns>Distinct, ordered list of package names.</returns>
    public List<string> ReadPackageReferences(string projectFilePath, IDictionary<string, string>? globalProperties = null)
    {
        if (string.IsNullOrEmpty(projectFilePath) || !File.Exists(projectFilePath))
        {
            return [];
        }

        try
        {
            // Use isolated ProjectCollection to avoid cross-project contamination
            using var projectCollection = new ProjectCollection();
            
            // Set default global properties for evaluation
            var props = globalProperties ?? new Dictionary<string, string>
            {
                ["Configuration"] = "Release",
                ["Platform"] = "AnyCPU"
            };

            // Load and evaluate the project with MSBuild
            var project = new Project(
                projectFilePath,
                props,
                toolsVersion: null, // Use default
                projectCollection);

            // Extract all evaluated PackageReference items
            var packageReferences = project.GetItems("PackageReference")
                .Select(item => item.EvaluatedInclude)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            LogPackageDiscovery(projectFilePath, packageReferences.Count);

            // Clean up
            projectCollection.UnloadProject(project);

            return packageReferences;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Could not read package references from {ProjectPath} using MSBuild: {Message}",
                projectFilePath,
                ex.Message);
            return [];
        }
    }

    /// <summary>
    /// Finds the applicable Directory.Build.props file for a project by walking up the directory tree.
    /// </summary>
    /// <param name="projectDirectory">Starting directory (typically the project's directory).</param>
    /// <returns>Path to Directory.Build.props if found, otherwise null.</returns>
    public static string? FindDirectoryBuildProps(string projectDirectory)
    {
        return FindFileInAncestors(projectDirectory, "Directory.Build.props");
    }

    /// <summary>
    /// Finds the applicable Directory.Packages.props file for a project by walking up the directory tree.
    /// </summary>
    /// <param name="projectDirectory">Starting directory (typically the project's directory).</param>
    /// <returns>Path to Directory.Packages.props if found, otherwise null.</returns>
    public static string? FindDirectoryPackagesProps(string projectDirectory)
    {
        return FindFileInAncestors(projectDirectory, "Directory.Packages.props");
    }

    /// <summary>
    /// Walks up the directory tree to find a file with the given name.
    /// Matches MSBuild's hierarchical import behavior.
    /// </summary>
    private static string? FindFileInAncestors(string startDirectory, string fileName)
    {
        if (string.IsNullOrEmpty(startDirectory) || !Directory.Exists(startDirectory))
        {
            return null;
        }

        var currentDir = new DirectoryInfo(startDirectory);

        // Walk up until we find the file or hit the root
        while (currentDir != null)
        {
            var candidatePath = Path.Combine(currentDir.FullName, fileName);
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }

    [LoggerMessage(
        EventId = 4050,
        Level = LogLevel.Debug,
        Message = "Discovered {Count} package reference(s) from {ProjectPath}")]
    partial void LogPackageDiscovery(string projectPath, int count);
}
