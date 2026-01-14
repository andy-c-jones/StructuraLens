namespace StructuraLens.Core.Models;

/// <summary>
/// Statistics about dependency collection and aggregation.
/// </summary>
public record DependencyCollectorStats(
    long TotalEdgesAdded,
    long UniqueEdgesCount,
    long MemoryUsageBytes,
    string Strategy,
    string? DatabasePath = null)
{
    /// <summary>
    /// Calculates the deduplication ratio (higher is better).
    /// </summary>
    public double DeduplicationRatio => TotalEdgesAdded > 0 
        ? 1.0 - ((double)UniqueEdgesCount / TotalEdgesAdded) 
        : 0;
    
    /// <summary>
    /// Gets memory usage in megabytes.
    /// </summary>
    public double MemoryUsageMB => MemoryUsageBytes / (1024.0 * 1024.0);
}
