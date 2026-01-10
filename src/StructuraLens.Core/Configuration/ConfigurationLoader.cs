using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace StructuraLens.Core.Configuration;

/// <summary>
/// Loads and manages StructuraLens configuration with inheritance.
/// </summary>
public static class ConfigurationLoader
{
    private const string DefaultConfigFileName = "structuralens.json";

    /// <summary>
    /// Loads configuration for a solution, with inheritance from parent directories.
    /// </summary>
    public static async Task<StructuraLensConfig> LoadSolutionConfigAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath)) ?? "";
        return await LoadConfigWithInheritanceAsync(solutionDirectory, cancellationToken);
    }

    /// <summary>
    /// Loads configuration for a project, with inheritance from parent directories.
    /// </summary>
    public static async Task<StructuraLensConfig> LoadProjectConfigAsync(Project project, StructuraLensConfig? solutionConfig = null, CancellationToken cancellationToken = default)
    {
        var projectDirectory = Path.GetDirectoryName(project.FilePath ?? "") ?? "";
        var projectConfig = await LoadConfigWithInheritanceAsync(projectDirectory, cancellationToken);

        // If we have a solution config, merge it as the root parent
        if (solutionConfig != null)
        {
            projectConfig = projectConfig.MergeWithParent(solutionConfig);
        }

        return projectConfig;
    }

    /// <summary>
    /// Discovers and loads configuration files with inheritance, walking up the directory tree.
    /// </summary>
    private static async Task<StructuraLensConfig> LoadConfigWithInheritanceAsync(string startDirectory, CancellationToken cancellationToken = default)
    {
        var configs = new List<StructuraLensConfig>();
        var currentDir = new DirectoryInfo(startDirectory);
        var depth = 0;
        var maxDepth = 10; // Default, will be updated as we load configs

        // Walk up the directory tree looking for config files
        while (currentDir != null && depth < maxDepth)
        {
            var configPath = Path.Combine(currentDir.FullName, DefaultConfigFileName);
            if (File.Exists(configPath))
            {
                try
                {
                    var config = await LoadSingleConfigAsync(configPath, cancellationToken);
                    configs.Add(config);
                    
                    // Update max depth based on the first config we find
                    if (configs.Count == 1)
                        maxDepth = config.InheritanceDepth;
                }
                catch (Exception ex)
                {
                    // Log warning but continue - invalid configs don't break the process
                    Console.WriteLine($"Warning: Failed to load config from {configPath}: {ex.Message}");
                }
            }

            currentDir = currentDir.Parent;
            depth++;
        }

        // Merge configs from root to leaf (parent-first)
        var mergedConfig = new StructuraLensConfig();
        configs.Reverse(); // Reverse to go from root to leaf

        foreach (var config in configs)
        {
            mergedConfig = mergedConfig.MergeWithParent(config);
        }

        return mergedConfig;
    }

    /// <summary>
    /// Loads a single configuration file.
    /// </summary>
    private static async Task<StructuraLensConfig> LoadSingleConfigAsync(string configPath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(configPath, cancellationToken);
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var config = JsonSerializer.Deserialize<StructuraLensConfig>(json, options);
        return config ?? new StructuraLensConfig();
    }

    /// <summary>
    /// Creates a default configuration file at the specified path.
    /// </summary>
    public static async Task CreateDefaultConfigAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        var configPath = Path.Combine(directoryPath, DefaultConfigFileName);
        if (File.Exists(configPath))
        {
            throw new InvalidOperationException($"Configuration file already exists at {configPath}");
        }

        var defaultConfig = CreateDefaultConfig();
        await SaveConfigAsync(configPath, defaultConfig, cancellationToken);
    }

    /// <summary>
    /// Saves a configuration to the specified path.
    /// </summary>
    public static async Task SaveConfigAsync(string configPath, StructuraLensConfig config, CancellationToken cancellationToken = default)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
        };

        var json = JsonSerializer.Serialize(config, options);
        await File.WriteAllTextAsync(configPath, json, cancellationToken);
    }

    /// <summary>
    /// Creates a default configuration with sensible defaults.
    /// </summary>
    public static StructuraLensConfig CreateDefaultConfig()
    {
        return new StructuraLensConfig
        {
            Schema = "https://raw.githubusercontent.com/your-org/structuralens/main/docs/config.schema.json",
            InheritanceDepth = 10,
            Coupling = new CouplingConfig
            {
                Mode = CouplingMode.Filtered,
                ExcludePatterns = new List<string>
                {
                    "System.*",
                    "Microsoft.*",
                    "Newtonsoft.Json*",
                    "*.Tests",
                    "*.Test"
                },
                IncludePatterns = new List<string>(),
                PatternType = PatternType.Wildcard,
                TrackExternalDependencies = true,
                GroupByAssembly = false
            },
            Metrics = new MetricsConfig
            {
                IncludeTests = true,
                ExcludeGenerated = true
            },
            Output = new OutputConfig
            {
                IncludeSourceLocations = false,
                MaxDependenciesInSummary = 10
            },
            Rules = new List<ArchitectureRule>()
        };
    }
}