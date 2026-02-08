namespace StructuraLens.Core.Models;

/// <summary>
/// Extension methods for calculating aggregated metrics across collections.
/// Eliminates duplication of common aggregation patterns throughout the codebase.
/// </summary>
public static class MetricsExtensions
{
    /// <summary>
    /// Calculates the average Maintainability Index across a collection of methods.
    /// Returns 0 if the collection is empty.
    /// </summary>
    public static double CalculateAverageMaintainabilityIndex(
        this IEnumerable<MethodMetrics> methods)
    {
        var methodList = methods.ToList();
        return methodList.Count > 0 
            ? methodList.Average(m => m.MaintainabilityIndex) 
            : 0;
    }

    /// <summary>
    /// Calculates the average Maintainability Index across all methods in a collection of types.
    /// Returns 0 if there are no methods.
    /// </summary>
    public static double CalculateAverageMaintainabilityIndex(
        this IEnumerable<TypeMetrics> types)
    {
        return types.SelectMany(t => t.Methods)
            .CalculateAverageMaintainabilityIndex();
    }

    /// <summary>
    /// Calculates the total Cyclomatic Complexity across a collection of types.
    /// </summary>
    public static int CalculateTotalCyclomaticComplexity(
        this IEnumerable<TypeMetrics> types)
    {
        return types.Sum(t => t.TotalCyclomaticComplexity);
    }

    /// <summary>
    /// Calculates the total Lines of Executable Code across a collection of types.
    /// </summary>
    public static int CalculateTotalLinesOfCode(
        this IEnumerable<TypeMetrics> types)
    {
        return types.Sum(t => t.TotalLinesOfExecutableCode);
    }

    /// <summary>
    /// Gets the maximum Depth of Inheritance across a collection of types.
    /// Returns 0 if the collection is empty.
    /// </summary>
    public static int CalculateMaxDepthOfInheritance(
        this IEnumerable<TypeMetrics> types)
    {
        var typeList = types.ToList();
        return typeList.Count > 0 
            ? typeList.Max(t => t.DepthOfInheritance) 
            : 0;
    }

    /// <summary>
    /// Counts the total number of methods across a collection of types.
    /// </summary>
    public static int CountTotalMethods(
        this IEnumerable<TypeMetrics> types)
    {
        return types.Sum(t => t.Methods.Count);
    }

    /// <summary>
    /// Gets all methods from a collection of types as a flat list.
    /// </summary>
    public static IReadOnlyList<MethodMetrics> GetAllMethods(
        this IEnumerable<TypeMetrics> types)
    {
        return types.SelectMany(t => t.Methods).ToList();
    }
}
