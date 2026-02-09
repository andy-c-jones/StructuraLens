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
    private IDependencyCollector _current;
    private readonly long _memoryThresholdBytes;
    private readonly int _sqliteBatchSize;
    private int _edgesSinceLastCheck;
    private long _totalEdgesAdded;
    private bool _hasMigrated;
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
        lock (_migrationLock)
        {
            _totalEdgesAdded++;
            
            // Periodically check memory pressure
            if (!_hasMigrated)
            {
                _edgesSinceLastCheck++;
                if (_edgesSinceLastCheck >= CheckInterval)
                {
                    _edgesSinceLastCheck = 0;
                    CheckMemoryPressure();
                }
            }
            
            _current.AddDependency(edge);
        }
    }
    
    /// <inheritdoc />
    public void AddDependencies(IEnumerable<DependencyEdge> edges)
    {
        foreach (var edge in edges)
            AddDependency(edge);
    }
    
    /// <summary>
    /// Checks current memory usage and migrates to SQLite if threshold exceeded.
    /// Must be called while holding _migrationLock.
    /// </summary>
    private void CheckMemoryPressure()
    {
        if (_hasMigrated)
            return;
        
        var currentMemory = GC.GetTotalMemory(false);
        
        if (currentMemory <= _memoryThresholdBytes)
            return;
        
        if (_current is not InMemoryDependencyCollector inMemory)
            return;
        
        MigrateToSQLite(inMemory, currentMemory);
    }
    
    /// <summary>
    /// Migrates from in-memory collector to SQLite collector.
    /// Must be called while holding _migrationLock.
    /// </summary>
    private void MigrateToSQLite(InMemoryDependencyCollector inMemory, long currentMemory)
    {
        Console.WriteLine($"[StructuraLens] Memory threshold exceeded ({currentMemory / 1024 / 1024} MB / {_memoryThresholdBytes / 1024 / 1024} MB). Migrating to SQLite...");
        
        // Create SQLite collector
        var sqlite = new SQLiteDependencyCollector(null, _sqliteBatchSize);
        
        // Snapshot existing edges while we hold the lock - no concurrent writes possible
        var existingEdges = inMemory.GetAggregatedDependencies();
        Console.WriteLine($"[StructuraLens] Migrating {existingEdges.Count} unique edges to disk...");
        
        // Migrate data to SQLite
        sqlite.AddDependencies(existingEdges);
        
        // Switch to new collector - all subsequent AddDependency calls will use SQLite
        _current = sqlite;
        _hasMigrated = true;
        
        // Safe to dispose old collector since no threads can be writing to it (we hold the lock)
        inMemory.Dispose();
        
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
    {
        lock (_migrationLock)
        {
            return _current.GetAggregatedDependencies();
        }
    }
    
    /// <inheritdoc />
    public IReadOnlyList<DependencyEdge> GetAggregatedDependencies(DependencyType type)
    {
        lock (_migrationLock)
        {
            return _current.GetAggregatedDependencies(type);
        }
    }
    
    /// <inheritdoc />
    public DependencyCollectorStats GetStats()
    {
        lock (_migrationLock)
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
    public bool HasMigrated
    {
        get
        {
            lock (_migrationLock)
            {
                return _hasMigrated;
            }
        }
    }
    
    /// <summary>
    /// Gets the current strategy being used (InMemory or SQLite).
    /// </summary>
    public string CurrentStrategy
    {
        get
        {
            lock (_migrationLock)
            {
                return _current switch
                {
                    InMemoryDependencyCollector => "InMemory",
                    SQLiteDependencyCollector => "SQLite",
                    _ => "Unknown"
                };
            }
        }
    }
}
