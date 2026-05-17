using System.Diagnostics;
using Microsoft.Data.Sqlite;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Disk-backed dependency collector using SQLite for unlimited capacity.
/// Ideal for very large codebases that exceed available memory.
/// </summary>
public sealed class SQLiteDependencyCollector : IDependencyCollector
{
    private readonly SqliteConnection _connection;
    private readonly string _dbPath;
    private readonly bool _isTemporary;
    private readonly List<DependencyEdge> _batch;
    private readonly int _batchSize;
    private long _totalAdded;
    private readonly object _batchLock = new();

    /// <summary>
    /// Creates a new SQLite-backed dependency collector.
    /// </summary>
    /// <param name="dbPath">Path to SQLite database file. If null, uses temp directory.</param>
    /// <param name="batchSize">
    /// Number of edges to batch before writing to disk.
    /// Can be any positive value - larger batches are automatically chunked
    /// to respect SQLite parameter limits (32,766 parameters max).
    /// Default: 1000 (optimal for most scenarios).
    /// </param>
    public SQLiteDependencyCollector(string? dbPath = null, int batchSize = 1000)
    {
        _batchSize = batchSize;
        _batch = new List<DependencyEdge>(batchSize);
        _isTemporary = dbPath == null;
        _dbPath = dbPath ?? Path.Combine(Path.GetTempPath(), $"structuralens_{Guid.NewGuid()}.db");

        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();

        // Enable optimizations for temporary database
        cmd.CommandText = @"
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=OFF;
            PRAGMA cache_size=10000;
            PRAGMA temp_store=MEMORY;
        ";
        cmd.ExecuteNonQuery();

        // Create tables and indexes if they don't exist
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS dependencies (
                from_entity TEXT NOT NULL,
                to_entity TEXT NOT NULL,
                type INTEGER NOT NULL,
                reference_count INTEGER NOT NULL DEFAULT 1
            );
            
            CREATE INDEX IF NOT EXISTS idx_aggregation ON dependencies(from_entity, to_entity, type);
            CREATE INDEX IF NOT EXISTS idx_from ON dependencies(from_entity);
            CREATE INDEX IF NOT EXISTS idx_to ON dependencies(to_entity);
        ";
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public void AddDependency(DependencyEdge edge)
    {
        lock (_batchLock)
        {
            _batch.Add(edge);
            Interlocked.Increment(ref _totalAdded);

            if (_batch.Count >= _batchSize)
                FlushBatch();
        }
    }

    /// <inheritdoc />
    public void AddDependencies(IEnumerable<DependencyEdge> edges)
    {
        lock (_batchLock)
        {
            var count = 0;
            foreach (var edge in edges)
            {
                _batch.Add(edge);
                count++;
            }

            Interlocked.Add(ref _totalAdded, count);

            if (_batch.Count >= _batchSize)
                FlushBatch();
        }
    }

    /// <summary>
    /// Flushes the current batch to the database using bulk INSERT statements.
    /// Automatically chunks large batches to respect SQLite parameter limits.
    /// </summary>
    private void FlushBatch()
    {
        if (_batch.Count == 0) return;

        // SQLite has a parameter limit of 32,766 (SQLITE_MAX_VARIABLE_NUMBER).
        // Each row requires 4 parameters (from_entity, to_entity, type, reference_count).
        // To stay well under the limit, we use a conservative threshold of 32,000 parameters,
        // which allows 8,000 rows per INSERT statement.
        //
        // If the batch exceeds this limit, we automatically chunk it into multiple INSERT
        // statements within the same transaction. This ensures correctness for any batch size
        // while maintaining excellent performance (3x faster than individual INSERTs).
        const int MaxParamsPerInsert = 32_000;  // Conservative margin below SQLite's 32,766 limit
        const int ParamsPerRow = 4;
        const int MaxRowsPerInsert = MaxParamsPerInsert / ParamsPerRow;  // 8,000 rows

        using var transaction = _connection.BeginTransaction();

        try
        {
            // Process batch in chunks if needed (unlikely with default batch size of 1000)
            for (int offset = 0; offset < _batch.Count; offset += MaxRowsPerInsert)
            {
                int chunkSize = Math.Min(MaxRowsPerInsert, _batch.Count - offset);
                var chunk = _batch.Skip(offset).Take(chunkSize).ToList();
                FlushChunk(chunk, transaction);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            _batch.Clear();
        }
    }

    /// <summary>
    /// Flushes a single chunk of edges using a multi-row VALUES INSERT statement.
    /// </summary>
    private void FlushChunk(List<DependencyEdge> edges, SqliteTransaction transaction)
    {
        if (edges.Count == 0) return;

        // Build multi-row VALUES clause with parameterized values
        var valuesClauses = new List<string>(edges.Count);
        var parameters = new List<SqliteParameter>(edges.Count * 4);

        for (int i = 0; i < edges.Count; i++)
        {
            valuesClauses.Add($"(@from{i}, @to{i}, @type{i}, @count{i})");

            parameters.Add(new SqliteParameter($"@from{i}", SqliteType.Text)
            { Value = edges[i].FromEntity });
            parameters.Add(new SqliteParameter($"@to{i}", SqliteType.Text)
            { Value = edges[i].ToEntity });
            parameters.Add(new SqliteParameter($"@type{i}", SqliteType.Integer)
            { Value = (int)edges[i].Type });
            parameters.Add(new SqliteParameter($"@count{i}", SqliteType.Integer)
            { Value = edges[i].ReferenceCount });
        }

        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $@"
            INSERT INTO dependencies (from_entity, to_entity, type, reference_count)
            VALUES {string.Join(",\n                   ", valuesClauses)}";

        cmd.Parameters.AddRange(parameters.ToArray());
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public IReadOnlyList<DependencyEdge> GetAggregatedDependencies()
    {
        lock (_batchLock)
        {
            FlushBatch();
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT from_entity, to_entity, type, SUM(reference_count) as total_count
            FROM dependencies
            GROUP BY from_entity, to_entity, type
        ";

        var result = new List<DependencyEdge>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new DependencyEdge(
                FromEntity: reader.GetString(0),
                ToEntity: reader.GetString(1),
                Type: (DependencyType)reader.GetInt32(2),
                ReferenceCount: reader.GetInt32(3)));
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<DependencyEdge> GetAggregatedDependencies(DependencyType type)
    {
        lock (_batchLock)
        {
            FlushBatch();
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT from_entity, to_entity, type, SUM(reference_count) as total_count
            FROM dependencies
            WHERE type = @type
            GROUP BY from_entity, to_entity, type
        ";
        cmd.Parameters.AddWithValue("@type", (int)type);

        var result = new List<DependencyEdge>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new DependencyEdge(
                FromEntity: reader.GetString(0),
                ToEntity: reader.GetString(1),
                Type: (DependencyType)reader.GetInt32(2),
                ReferenceCount: reader.GetInt32(3)));
        }

        return result;
    }

    /// <inheritdoc />
    public DependencyCollectorStats GetStats()
    {
        lock (_batchLock)
        {
            FlushBatch();
        }

        using var cmd = _connection.CreateCommand();

        // Get accurate unique count using GROUP BY
        cmd.CommandText = @"
            SELECT COUNT(*) FROM (
                SELECT from_entity, to_entity, type
                FROM dependencies
                GROUP BY from_entity, to_entity, type
            )
        ";
        var uniqueCount = Convert.ToInt64(cmd.ExecuteScalar());

        var fileInfo = new FileInfo(_dbPath);

        return new DependencyCollectorStats(
            TotalEdgesAdded: _totalAdded,
            UniqueEdgesCount: uniqueCount,
            MemoryUsageBytes: fileInfo.Exists ? fileInfo.Length : 0,
            Strategy: "SQLite",
            DatabasePath: _dbPath);
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_batchLock)
        {
            _batch.Clear();
            _totalAdded = 0;
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM dependencies";
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_batchLock)
        {
            FlushBatch();
        }

        // Close connection properly to release file locks
        if (_connection != null)
        {
            // Clear any pooled connections to this database
            SqliteConnection.ClearPool(_connection);
            _connection.Close();
            _connection.Dispose();
        }

        // Only delete temp databases
        if (_isTemporary && !string.IsNullOrEmpty(_dbPath))
        {
            DeleteTemporaryDatabaseFiles();
        }
    }

    private void DeleteTemporaryDatabaseFiles()
    {
        TryDeleteTemporaryFile(_dbPath);
        TryDeleteTemporaryFile(_dbPath + "-wal");
        TryDeleteTemporaryFile(_dbPath + "-shm");
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        const int MaxAttempts = 3;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }
            catch (IOException ex) when (attempt < MaxAttempts)
            {
                Trace.TraceWarning("Could not delete temporary SQLite file {0}: {1}", path, ex.Message);
            }
            catch (UnauthorizedAccessException ex) when (attempt < MaxAttempts)
            {
                Trace.TraceWarning("Could not delete temporary SQLite file {0}: {1}", path, ex.Message);
            }
            catch (IOException ex)
            {
                Trace.TraceWarning("Could not delete temporary SQLite file {0}: {1}", path, ex.Message);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                Trace.TraceWarning("Could not delete temporary SQLite file {0}: {1}", path, ex.Message);
                return;
            }
        }
    }
}
