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
    /// <param name="batchSize">Number of edges to batch before writing to disk.</param>
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
            _batch.AddRange(edges);
            
            // Update counter
            var count = _batch.Count - (_totalAdded > 0 ? 0 : _batch.Count);
            Interlocked.Add(ref _totalAdded, count);
            
            if (_batch.Count >= _batchSize)
                FlushBatch();
        }
    }
    
    private void FlushBatch()
    {
        if (_batch.Count == 0) return;
        
        using var transaction = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            INSERT INTO dependencies (from_entity, to_entity, type, reference_count)
            VALUES (@from, @to, @type, @count)
        ";
        
        var paramFrom = cmd.Parameters.Add("@from", SqliteType.Text);
        var paramTo = cmd.Parameters.Add("@to", SqliteType.Text);
        var paramType = cmd.Parameters.Add("@type", SqliteType.Integer);
        var paramCount = cmd.Parameters.Add("@count", SqliteType.Integer);
        
        foreach (var edge in _batch)
        {
            paramFrom.Value = edge.FromEntity;
            paramTo.Value = edge.ToEntity;
            paramType.Value = (int)edge.Type;
            paramCount.Value = edge.ReferenceCount;
            cmd.ExecuteNonQuery();
        }
        
        transaction.Commit();
        _batch.Clear();
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
            try
            {
                // Give a moment for file handles to be released
                System.Threading.Thread.Sleep(100);
                
                // Delete main database and WAL files
                if (File.Exists(_dbPath))
                    File.Delete(_dbPath);
                    
                var walPath = _dbPath + "-wal";
                if (File.Exists(walPath))
                    File.Delete(walPath);
                    
                var shmPath = _dbPath + "-shm";
                if (File.Exists(shmPath))
                    File.Delete(shmPath);
            }
            catch
            {
                // Ignore cleanup errors - best effort
            }
        }
    }
}
