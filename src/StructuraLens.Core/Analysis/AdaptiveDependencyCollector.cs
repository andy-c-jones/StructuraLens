using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Adaptive dependency collector that automatically switches from in-memory to SQLite
/// when memory threshold is exceeded, providing optimal performance for all codebase sizes.
/// </summary>
public sealed class AdaptiveDependencyCollector : IDependencyCollector
{
    private IDependencyCollector _current;
    private readonly long _memoryThresholdBytes;
    private readonly int _sqliteBatchSize;
    private int _edgesSinceLastCheck;
    private long _totalEdgesAdded; // Track total ourselves
    private bool _hasMigrated;
    private const int CheckInterval = 10000; // Check memory every 10K edges
    
    /// <summary>
    /// Creates a new adaptive dependency collector.
    /// </summary>
    /// <param name="memoryThresholdMB">Memory threshold in MB. When exceeded, migrates to SQLite.</param>
    /// <param name="sqliteBatchSize">Batch size for SQLite collector after migration.</param>
    public AdaptiveDependencyCollector(long memoryThresholdMB = 1024, int sqliteBatchSize = 1000)
    {
        _memoryThresholdBytes = memoryThresholdMB * 1024 * 1024;
        _sqliteBatchSize = sqliteBatchSize;
        _current = new InMemoryDependencyCollector();
        _hasMigrated = false;
        _totalEdgesAdded = 0;
    }
    
    /// <inheritdoc />
    public void AddDependency(DependencyEdge edge)
    {
        Interlocked.Increment(ref _totalEdgesAdded);
        
        // Periodically check memory pressure
        if (!_hasMigrated && ++_edgesSinceLastCheck >= CheckInterval)
        {
            CheckMemoryPressure();
            _edgesSinceLastCheck = 0;
        }
        
        _current.AddDependency(edge);
    }
    
    /// <inheritdoc />
    public void AddDependencies(IEnumerable<DependencyEdge> edges)
    {
        foreach (var edge in edges)
            AddDependency(edge);
    }
    
    /// <summary>
    /// Checks current memory usage and migrates to SQLite if threshold exceeded.
    /// </summary>
    private void CheckMemoryPressure()
    {
        var currentMemory = GC.GetTotalMemory(false);
        
        // Only migrate if still using InMemory and threshold exceeded
        if (currentMemory > _memoryThresholdBytes && _current is InMemoryDependencyCollector inMemory)
        {
            MigrateToSQLite(inMemory, currentMemory);
        }
    }
    
    /// <summary>
    /// Migrates from in-memory collector to SQLite collector.
    /// </summary>
    private void MigrateToSQLite(InMemoryDependencyCollector inMemory, long currentMemory)
    {
        Console.WriteLine($"[StructuraLens] Memory threshold exceeded ({currentMemory / 1024 / 1024} MB / {_memoryThresholdBytes / 1024 / 1024} MB). Migrating to SQLite...");
        
        // Create SQLite collector
        var sqlite = new SQLiteDependencyCollector(null, _sqliteBatchSize);
        
        // Migrate existing edges
        var existingEdges = inMemory.GetAggregatedDependencies();
        Console.WriteLine($"[StructuraLens] Migrating {existingEdges.Count} unique edges to disk...");
        sqlite.AddDependencies(existingEdges);
        
        // Switch collectors
        inMemory.Dispose();
        _current = sqlite;
        _hasMigrated = true;
        
        // Force garbage collection to reclaim memory
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        
        var afterMemory = GC.GetTotalMemory(false);
        var reclaimed = currentMemory - afterMemory;
        Console.WriteLine($"[StructuraLens] Migration complete. Memory: {afterMemory / 1024 / 1024} MB (reclaimed {reclaimed / 1024 / 1024} MB)");
    }
    
    /// <inheritdoc />
    public IReadOnlyList<DependencyEdge> GetAggregatedDependencies()
        => _current.GetAggregatedDependencies();
    
    /// <inheritdoc />
    public IReadOnlyList<DependencyEdge> GetAggregatedDependencies(DependencyType type)
        => _current.GetAggregatedDependencies(type);
    
    /// <inheritdoc />
    public DependencyCollectorStats GetStats()
    {
        var stats = _current.GetStats();
        // Override with our own total count, but keep the unique count from the current collector
        return new DependencyCollectorStats(
            TotalEdgesAdded: _totalEdgesAdded,
            UniqueEdgesCount: stats.UniqueEdgesCount,
            MemoryUsageBytes: stats.MemoryUsageBytes,
            Strategy: $"Adaptive-{stats.Strategy}",
            DatabasePath: stats.DatabasePath
        );
    }
    
    /// <inheritdoc />
    public void Reset()
    {
        _current.Reset();
        _edgesSinceLastCheck = 0;
        _hasMigrated = false;
        _totalEdgesAdded = 0;
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        _current?.Dispose();
    }
    
    /// <summary>
    /// Gets whether this collector has migrated to SQLite.
    /// </summary>
    public bool HasMigrated => _hasMigrated;
    
    /// <summary>
    /// Gets the current strategy being used (InMemory or SQLite).
    /// </summary>
    public string CurrentStrategy => _current switch
    {
        InMemoryDependencyCollector => "InMemory",
        SQLiteDependencyCollector => "SQLite",
        _ => "Unknown"
    };
}
