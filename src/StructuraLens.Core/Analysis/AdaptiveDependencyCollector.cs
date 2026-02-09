using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Adaptive dependency collector that automatically switches from in-memory to SQLite
/// when memory threshold is exceeded, providing optimal performance for all codebase sizes.
/// Thread-safe: All public methods can be called concurrently from multiple threads.
/// Migration is guaranteed to happen at most once, even under high concurrency.
/// </summary>
public sealed class AdaptiveDependencyCollector : IDependencyCollector
{
    private volatile IDependencyCollector _current;
    private readonly long _memoryThresholdBytes;
    private readonly int _sqliteBatchSize;
    private int _edgesSinceLastCheck;
    private long _totalEdgesAdded; // Track total ourselves
    private volatile bool _hasMigrated;
    private readonly object _migrationLock = new();
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
        // Capture reference first to avoid race with migration switching _current
        var collector = _current;
        
        Interlocked.Increment(ref _totalEdgesAdded);
        
        // Periodically check memory pressure
        if (!_hasMigrated)
        {
            var currentCount = Interlocked.Increment(ref _edgesSinceLastCheck);
            if (currentCount >= CheckInterval)
            {
                // Atomic reset: only the thread that sees its own value wins
                if (Interlocked.CompareExchange(ref _edgesSinceLastCheck, 0, currentCount) == currentCount)
                {
                    CheckMemoryPressure();
                }
                
                // Refresh after potential migration so we don't add to the old collector
                collector = _current;
            }
        }
        
        collector.AddDependency(edge);
    }
    
    /// <inheritdoc />
    /// <remarks>
    /// Bulk addition optimized for performance: memory pressure is only checked
    /// once (on the first edge) rather than per edge. This is intentional to
    /// reduce overhead during large batch operations.
    /// </remarks>
    public void AddDependencies(IEnumerable<DependencyEdge> edges)
    {
        foreach (var edge in edges)
            AddDependency(edge);
    }
    
    /// <summary>
    /// Checks current memory usage and migrates to SQLite if threshold exceeded.
    /// Thread-safe using double-checked locking pattern.
    /// </summary>
    private void CheckMemoryPressure()
    {
        // Fast path: skip if already migrated (volatile read, no lock)
        if (_hasMigrated)
            return;
        
        var currentMemory = GC.GetTotalMemory(false);
        
        // Only proceed if threshold exceeded
        if (currentMemory <= _memoryThresholdBytes)
            return;
        
        // Double-checked locking pattern
        lock (_migrationLock)
        {
            // Check again after acquiring lock - another thread may have migrated
            if (_hasMigrated)
                return;
            
            // Verify still using InMemory (defensive check)
            if (_current is not InMemoryDependencyCollector inMemory)
                return;
            
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
        
        // CRITICAL: Switch to new collector FIRST, before migrating old data
        // This ensures all new AddDependency calls go to SQLite during migration
        _current = sqlite;
        _hasMigrated = true;
        
        // Now migrate existing edges from old collector
        // Any additions during this time go to the new SQLite collector
        var existingEdges = inMemory.GetAggregatedDependencies();
        Console.WriteLine($"[StructuraLens] Migrating {existingEdges.Count} unique edges to disk...");
        sqlite.AddDependencies(existingEdges);
        
        // Do NOT dispose the old collector - threads that captured a local reference
        // before migration may still be calling AddDependency on it. Let GC handle
        // cleanup when all references are gone.
        // Note: InMemoryDependencyCollector.Dispose() is a no-op anyway.
        
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
        lock (_migrationLock)
        {
            // If currently using SQLite, dispose and reset to InMemory
            if (_hasMigrated)
            {
                _current?.Dispose();
                _current = new InMemoryDependencyCollector();
                _hasMigrated = false;
            }
            else
            {
                _current.Reset();
            }
            
            _edgesSinceLastCheck = 0;
            _totalEdgesAdded = 0;
        }
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        lock (_migrationLock)
        {
            _current?.Dispose();
        }
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
