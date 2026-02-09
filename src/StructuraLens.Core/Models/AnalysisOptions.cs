namespace StructuraLens.Core.Models;

/// <summary>
/// Specifies the strategy for aggregating dependency edges during analysis.
/// </summary>
public enum DependencyAggregationStrategy
{
    /// <summary>
    /// Use in-memory concurrent dictionary for aggregation.
    /// Best for small to medium solutions (up to ~50 projects).
    /// Memory usage: Moderate (50-70% reduction from naive approach).
    /// Performance: Fastest.
    /// </summary>
    InMemory,

    /// <summary>
    /// Use SQLite database for aggregation (disk-backed).
    /// Best for large solutions (100+ projects) or memory-constrained environments.
    /// Memory usage: Minimal (95% reduction from naive approach).
    /// Performance: Slightly slower (10-20% overhead), but handles unlimited data.
    /// </summary>
    SQLite,

    /// <summary>
    /// Automatically choose optimal strategy based on memory pressure.
    /// Starts with InMemory, migrates to SQLite when threshold exceeded.
    /// Best for unknown codebase sizes or variable workloads.
    /// Memory usage: Adaptive (optimal for any size).
    /// Performance: Fast for small projects, handles large projects gracefully.
    /// </summary>
    Adaptive
}

/// <summary>
/// Configuration options for solution analysis behavior.
/// </summary>
public class AnalysisOptions
{
    /// <summary>
    /// Gets or sets the strategy for aggregating dependency edges.
    /// Default: InMemory.
    /// </summary>
    public DependencyAggregationStrategy AggregationStrategy { get; set; } = DependencyAggregationStrategy.InMemory;

    /// <summary>
    /// Gets or sets the batch size for SQLite write operations.
    /// Used when AggregationStrategy is SQLite or Adaptive (after migration).
    /// Higher values = better write performance but more memory per transaction.
    /// Default: 1000.
    /// </summary>
    public int SQLiteBatchSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the path for the SQLite database file.
    /// Only used when AggregationStrategy is SQLite.
    /// If null, a temporary database will be created and auto-deleted on completion.
    /// Default: null (use temp file).
    /// </summary>
    public string? SQLiteDatabasePath { get; set; } = null;

    /// <summary>
    /// Gets or sets the memory threshold in MB for adaptive strategy.
    /// When memory usage exceeds this threshold, the adaptive collector
    /// migrates from in-memory to SQLite storage.
    /// Only used when AggregationStrategy is Adaptive.
    /// Default: 1024 MB (1 GB).
    /// </summary>
    public long MemoryThresholdMB { get; set; } = 1024;

    /// <summary>
    /// Gets or sets whether to enable verbose logging for aggregation operations.
    /// Default: false.
    /// </summary>
    public bool VerboseLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets the tool version to include in analysis reports.
    /// Default: "unknown".
    /// </summary>
    public string ToolVersion { get; set; } = "unknown";
}
