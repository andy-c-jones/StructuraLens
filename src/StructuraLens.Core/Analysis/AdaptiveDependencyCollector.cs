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
    private IDependencyCollector? _current;
    private readonly long _memoryThresholdBytes;
    private readonly int _sqliteBatchSize;
    private readonly Func<long> _memoryProvider;
    private readonly Action<string>? _migrationLogger;
    private int _edgesSinceLastCheck;
    private long _totalEdgesAdded;
    private bool _hasMigrated;
    private bool _disposed;
    private readonly ReaderWriterLockSlim _lock = new();
    private const int CheckInterval = 10000; // Check memory every 10K edges

    /// <summary>
    /// Creates a new adaptive dependency collector.
    /// </summary>
    /// <param name="memoryThresholdMB">Memory threshold in MB. When exceeded, migrates to SQLite.</param>
    /// <param name="sqliteBatchSize">Batch size for SQLite collector after migration.</param>
    /// <param name="memoryProvider">
    /// Optional function that returns current memory usage in bytes.
    /// Defaults to <c>GC.GetTotalMemory(false)</c>. Override in tests for deterministic behavior.
    /// </param>
    public AdaptiveDependencyCollector(
        long memoryThresholdMB = 1024,
        int sqliteBatchSize = 1000,
        Func<long>? memoryProvider = null,
        Action<string>? migrationLogger = null)
    {
        _memoryThresholdBytes = memoryThresholdMB * 1024 * 1024;
        _sqliteBatchSize = sqliteBatchSize;
        _memoryProvider = memoryProvider ?? (() => GC.GetTotalMemory(false));
        _migrationLogger = migrationLogger;
        _current = new InMemoryDependencyCollector();
        _hasMigrated = false;
        _totalEdgesAdded = 0;
    }

    /// <inheritdoc />
    public void AddDependency(DependencyEdge edge)
    {
        // Fast path: read lock allows concurrent adds since underlying collectors are thread-safe.
        // Migration check uses Interlocked to detect when the threshold is hit; only then
        // do we escalate to a write lock.
        _lock.EnterReadLock();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            Interlocked.Increment(ref _totalEdgesAdded);

            // Periodically check memory pressure
            if (!_hasMigrated)
            {
                var count = Interlocked.Increment(ref _edgesSinceLastCheck);
                if (count >= CheckInterval)
                {
                    // Release read lock before acquiring write lock for migration check
                    _lock.ExitReadLock();
                    try
                    {
                        CheckAndMigrateIfNeeded();
                    }
                    finally
                    {
                        _lock.EnterReadLock();
                    }

                    // Re-check disposed after reacquiring (Reset/Dispose could have run)
                    ObjectDisposedException.ThrowIf(_disposed, this);
                }
            }

            _current!.AddDependency(edge);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public void AddDependencies(IEnumerable<DependencyEdge> edges)
    {
        foreach (var edge in edges)
            AddDependency(edge);
    }

    /// <summary>
    /// Acquires write lock, checks memory pressure, and migrates if needed.
    /// Called when the edge counter hits the check interval.
    /// </summary>
    private void CheckAndMigrateIfNeeded()
    {
        long currentMemory;
        int edgeCount;

        _lock.EnterWriteLock();
        try
        {
            // Reset counter under write lock so only one thread triggers the next check
            _edgesSinceLastCheck = 0;

            if (_hasMigrated || _disposed)
                return;

            currentMemory = _memoryProvider();

            if (currentMemory <= _memoryThresholdBytes)
                return;

            if (_current is not InMemoryDependencyCollector inMemory)
                return;

            edgeCount = MigrateToSQLite(inMemory);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        // Log and GC outside the write lock to avoid blocking concurrent operations
        LogMigration($"Memory threshold exceeded ({currentMemory / 1024 / 1024} MB / {_memoryThresholdBytes / 1024 / 1024} MB). Migrating to SQLite...");
        LogMigration($"Migrated {edgeCount} unique edges to disk.");

        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        var afterMemory = GC.GetTotalMemory(false);
        var reclaimed = currentMemory - afterMemory;
        LogMigration($"Migration complete. Memory: {afterMemory / 1024 / 1024} MB (reclaimed {reclaimed / 1024 / 1024} MB)");
    }

    private void LogMigration(string message)
    {
        _migrationLogger?.Invoke(message);
    }

    /// <summary>
    /// Migrates from in-memory collector to SQLite collector.
    /// Must be called while holding the write lock.
    /// </summary>
    /// <returns>The number of unique edges migrated.</returns>
    private int MigrateToSQLite(InMemoryDependencyCollector inMemory)
    {
        // Create SQLite collector
        var sqlite = new SQLiteDependencyCollector(null, _sqliteBatchSize);

        // Snapshot existing edges while we hold the write lock - no concurrent writes possible
        var existingEdges = inMemory.GetAggregatedDependencies();

        // Migrate data to SQLite
        sqlite.AddDependencies(existingEdges);

        // Switch to new collector - all subsequent AddDependency calls will use SQLite
        _current = sqlite;
        _hasMigrated = true;

        // Safe to dispose old collector since no threads can be writing to it (we hold the write lock)
        inMemory.Dispose();

        return existingEdges.Count;
    }

    /// <inheritdoc />
    public IReadOnlyList<DependencyEdge> GetAggregatedDependencies()
    {
        _lock.EnterReadLock();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _current!.GetAggregatedDependencies();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DependencyEdge> GetAggregatedDependencies(DependencyType type)
    {
        _lock.EnterReadLock();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _current!.GetAggregatedDependencies(type);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public DependencyCollectorStats GetStats()
    {
        _lock.EnterReadLock();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var stats = _current!.GetStats();
            // Override with our own total count, but keep the unique count from the current collector
            return new DependencyCollectorStats(
                TotalEdgesAdded: Interlocked.Read(ref _totalEdgesAdded),
                UniqueEdgesCount: stats.UniqueEdgesCount,
                MemoryUsageBytes: stats.MemoryUsageBytes,
                Strategy: $"Adaptive-{stats.Strategy}",
                DatabasePath: stats.DatabasePath
            );
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        _lock.EnterWriteLock();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // If currently using SQLite, dispose and reset to InMemory
            if (_hasMigrated)
            {
                _current?.Dispose();
                _current = new InMemoryDependencyCollector();
                _hasMigrated = false;
            }
            else
            {
                _current!.Reset();
            }

            _edgesSinceLastCheck = 0;
            Interlocked.Exchange(ref _totalEdgesAdded, 0);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _lock.EnterWriteLock();
        try
        {
            if (_disposed)
                return;

            _current?.Dispose();
            _current = null;
            _disposed = true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        _lock.Dispose();
    }

    /// <summary>
    /// Gets whether this collector has migrated to SQLite.
    /// </summary>
    public bool HasMigrated
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _hasMigrated;
            }
            finally
            {
                _lock.ExitReadLock();
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
            _lock.EnterReadLock();
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _current switch
                {
                    InMemoryDependencyCollector => "InMemory",
                    SQLiteDependencyCollector => "SQLite",
                    _ => "Unknown"
                };
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}
