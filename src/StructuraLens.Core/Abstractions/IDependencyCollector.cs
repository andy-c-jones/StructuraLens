using StructuraLens.Core.Models;

namespace StructuraLens.Core.Abstractions;

/// <summary>
/// Collects and aggregates dependency edges during analysis.
/// Implementations may use in-memory or disk-backed storage.
/// </summary>
public interface IDependencyCollector : IDisposable
{
    /// <summary>
    /// Adds a single dependency edge. Thread-safe.
    /// </summary>
    void AddDependency(DependencyEdge edge);
    
    /// <summary>
    /// Adds multiple dependency edges. Thread-safe.
    /// </summary>
    void AddDependencies(IEnumerable<DependencyEdge> edges);
    
    /// <summary>
    /// Gets aggregated (deduplicated) dependencies.
    /// Call only after all additions are complete.
    /// </summary>
    IReadOnlyList<DependencyEdge> GetAggregatedDependencies();
    
    /// <summary>
    /// Gets aggregated dependencies filtered by type.
    /// Useful for building coupling metrics incrementally.
    /// </summary>
    IReadOnlyList<DependencyEdge> GetAggregatedDependencies(DependencyType type);
    
    /// <summary>
    /// Clears all collected data and resets state.
    /// </summary>
    void Reset();
    
    /// <summary>
    /// Gets current statistics about collected data.
    /// </summary>
    DependencyCollectorStats GetStats();
}
