using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using StructuraLens.Core.Configuration;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Filters dependencies based on configuration rules.
/// </summary>
public static class DependencyFilter
{
    /// <summary>
    /// Filters a list of dependency edges based on the coupling configuration.
    /// </summary>
    public static List<DependencyEdge> FilterDependencies(
        IEnumerable<DependencyEdge> dependencies, 
        CouplingConfig config,
        IEnumerable<string>? projectNames = null)
    {
        var projectNameSet = projectNames?.ToHashSet() ?? new HashSet<string>();
        var filtered = new List<DependencyEdge>();

        foreach (var dependency in dependencies)
        {
            if (ShouldIncludeDependency(dependency, config, projectNameSet))
            {
                filtered.Add(dependency);
            }
        }

        return filtered;
    }

    /// <summary>
    /// Determines if a dependency should be included based on the configuration.
    /// </summary>
    private static bool ShouldIncludeDependency(
        DependencyEdge dependency, 
        CouplingConfig config, 
        HashSet<string> projectNames)
    {
        // For project references, always include if both projects are in our solution
        if (dependency.Type == DependencyType.ProjectReference)
        {
            return projectNames.Contains(dependency.FromEntity) && projectNames.Contains(dependency.ToEntity);
        }

        // Apply mode-based filtering
        switch (config.Mode)
        {
            case CouplingMode.Internal:
                return IsInternalDependency(dependency, projectNames);

            case CouplingMode.Filtered:
                if (!config.TrackExternalDependencies && !IsInternalDependency(dependency, projectNames))
                    return false;
                return ApplyPatternFilters(dependency, config);

            case CouplingMode.All:
                return true;

            default:
                return true;
        }
    }

    /// <summary>
    /// Checks if a dependency is internal (between project's own code).
    /// </summary>
    private static bool IsInternalDependency(DependencyEdge dependency, HashSet<string> projectNames)
    {
        // For namespace/type dependencies, check if both entities belong to our projects
        // This is a heuristic - we assume our project namespaces start with project names
        if (dependency.Type == DependencyType.NamespaceReference || dependency.Type == DependencyType.TypeReference)
        {
            var fromProject = GetProjectFromEntityName(dependency.FromEntity, projectNames);
            var toProject = GetProjectFromEntityName(dependency.ToEntity, projectNames);
            
            return !string.IsNullOrEmpty(fromProject) && !string.IsNullOrEmpty(toProject);
        }

        return false;
    }

    /// <summary>
    /// Attempts to determine which project an entity belongs to based on naming conventions.
    /// </summary>
    private static string? GetProjectFromEntityName(string entityName, HashSet<string> projectNames)
    {
        // Try exact match first
        if (projectNames.Contains(entityName))
            return entityName;

        // Try to find project name as prefix
        return projectNames.FirstOrDefault(p => entityName.StartsWith(p + "."));
    }

    /// <summary>
    /// Applies include/exclude pattern filters to a dependency.
    /// </summary>
    private static bool ApplyPatternFilters(DependencyEdge dependency, CouplingConfig config)
    {
        var entityToCheck = dependency.ToEntity;

        // Check include patterns first (they override exclude patterns)
        if (config.IncludePatterns.Count > 0)
        {
            foreach (var pattern in config.IncludePatterns)
            {
                if (MatchesPattern(entityToCheck, pattern, config.PatternType))
                    return true;
            }
            // If include patterns are specified but none match, exclude
            return false;
        }

        // Check exclude patterns
        foreach (var pattern in config.ExcludePatterns)
        {
            if (MatchesPattern(entityToCheck, pattern, config.PatternType))
                return false;
        }

        // If no exclude patterns match, include
        return true;
    }

    /// <summary>
    /// Checks if a string matches a pattern based on the pattern type.
    /// </summary>
    private static bool MatchesPattern(string input, string pattern, PatternType patternType)
    {
        return patternType switch
        {
            PatternType.Exact => string.Equals(input, pattern, StringComparison.OrdinalIgnoreCase),
            PatternType.Wildcard => MatchesWildcard(input, pattern),
            PatternType.Regex => MatchesRegex(input, pattern),
            _ => false
        };
    }

    /// <summary>
    /// Checks if a string matches a wildcard pattern (* and ?).
    /// </summary>
    private static bool MatchesWildcard(string input, string pattern)
    {
        // Convert wildcard to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";

        try
        {
            return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
        }
        catch (RegexParseException)
        {
            // If regex is invalid, fall back to exact match
            return string.Equals(input, pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Checks if a string matches a regular expression pattern.
    /// </summary>
    private static bool MatchesRegex(string input, string pattern)
    {
        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
        }
        catch (RegexParseException)
        {
            // If regex is invalid, exclude the dependency for safety
            return false;
        }
    }

    /// <summary>
    /// Filters coupling metrics based on configuration.
    /// </summary>
    public static List<CouplingMetrics> FilterCouplingMetrics(
        IEnumerable<CouplingMetrics> metrics,
        CouplingConfig config,
        IEnumerable<string>? projectNames = null)
    {
        var projectNameSet = projectNames?.ToHashSet() ?? new HashSet<string>();
        var filtered = new List<CouplingMetrics>();

        foreach (var metric in metrics)
        {
            var filteredOutbound = FilterDependencies(metric.OutboundDependencies, config, projectNames);
            var filteredInbound = FilterDependencies(metric.InboundDependencies, config, projectNames);

            // Only include metrics that still have dependencies after filtering
            if (filteredOutbound.Count > 0 || filteredInbound.Count > 0 || ShouldIncludeEntity(metric.EntityName, config, projectNameSet))
            {
                filtered.Add(new CouplingMetrics(metric.EntityName, metric.EntityType)
                {
                    OutboundDependencies = filteredOutbound,
                    InboundDependencies = filteredInbound
                });
            }
        }

        return filtered;
    }

    /// <summary>
    /// Determines if an entity should be included based on its name and configuration.
    /// </summary>
    private static bool ShouldIncludeEntity(string entityName, CouplingConfig config, HashSet<string> projectNames)
    {
        // Always include project entities
        if (projectNames.Contains(entityName) || projectNames.Any(p => entityName.StartsWith(p + ".")))
            return true;

        // For external entities, apply the same filtering logic
        if (config.Mode == CouplingMode.Internal)
            return false;

        if (config.Mode == CouplingMode.Filtered)
        {
            return ApplyPatternFiltersToEntity(entityName, config);
        }

        return true;
    }

    /// <summary>
    /// Applies pattern filters to an entity name.
    /// </summary>
    private static bool ApplyPatternFiltersToEntity(string entityName, CouplingConfig config)
    {
        // Check include patterns first
        if (config.IncludePatterns.Count > 0)
        {
            return config.IncludePatterns.Any(pattern => MatchesPattern(entityName, pattern, config.PatternType));
        }

        // Check exclude patterns
        return !config.ExcludePatterns.Any(pattern => MatchesPattern(entityName, pattern, config.PatternType));
    }
}