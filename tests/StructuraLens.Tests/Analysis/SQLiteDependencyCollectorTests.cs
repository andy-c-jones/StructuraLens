using StructuraLens.Core.Analysis;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Analysis;

public class SQLiteDependencyCollectorTests
{
    [Test]
    public async Task AddDependency_SingleEdge_StoresCorrectly()
    {
        // Arrange
        using var collector = new SQLiteDependencyCollector();
        var edge = new DependencyEdge("A", "B", DependencyType.TypeReference, 1);
        
        // Act
        collector.AddDependency(edge);
        var result = collector.GetAggregatedDependencies();
        
        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].FromEntity).IsEqualTo("A");
        await Assert.That(result[0].ToEntity).IsEqualTo("B");
        await Assert.That(result[0].Type).IsEqualTo(DependencyType.TypeReference);
        await Assert.That(result[0].ReferenceCount).IsEqualTo(1);
    }
    
    [Test]
    public async Task AddDependency_DuplicateEdges_Aggregates()
    {
        // Arrange
        using var collector = new SQLiteDependencyCollector();
        
        // Act
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        
        var result = collector.GetAggregatedDependencies();
        
        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].ReferenceCount).IsEqualTo(3);
    }
    
    [Test]
    public async Task AddDependency_DifferentTypes_KeepsSeparate()
    {
        // Arrange
        using var collector = new SQLiteDependencyCollector();
        
        // Act
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.NamespaceReference, 1));
        
        var result = collector.GetAggregatedDependencies();
        
        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
    }
    
    [Test]
    public async Task AddDependency_MultipleEdges_AggregatesCorrectly()
    {
        // Arrange
        using var collector = new SQLiteDependencyCollector();
        
        // Act
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("A", "C", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("B", "C", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 2)); // Duplicate with count 2
        
        var result = collector.GetAggregatedDependencies();
        
        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
        
        var aToB = result.First(e => e.FromEntity == "A" && e.ToEntity == "B");
        await Assert.That(aToB.ReferenceCount).IsEqualTo(3); // 1 + 2
    }
    
    [Test]
    public async Task AddDependency_ParallelAdds_ThreadSafe()
    {
        // Arrange
        using var collector = new SQLiteDependencyCollector();
        var tasks = new List<Task>();
        
        // Act - 100 tasks, each adding 1000 identical edges
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 1000; j++)
                {
                    collector.AddDependency(
                        new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        var result = collector.GetAggregatedDependencies();
        
        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].ReferenceCount).IsEqualTo(100_000);
    }
    
    [Test]
    public async Task GetAggregatedDependencies_ByType_FiltersCorrectly()
    {
        // Arrange
        using var collector = new SQLiteDependencyCollector();
        
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("C", "D", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("X", "Y", DependencyType.NamespaceReference, 1));
        collector.AddDependency(new DependencyEdge("P", "Q", DependencyType.ProjectReference, 1));
        
        // Act
        var typeRefs = collector.GetAggregatedDependencies(DependencyType.TypeReference);
        var namespaceRefs = collector.GetAggregatedDependencies(DependencyType.NamespaceReference);
        var projectRefs = collector.GetAggregatedDependencies(DependencyType.ProjectReference);
        
        // Assert
        await Assert.That(typeRefs.Count).IsEqualTo(2);
        await Assert.That(namespaceRefs.Count).IsEqualTo(1);
        await Assert.That(projectRefs.Count).IsEqualTo(1);
        
        foreach (var edge in typeRefs)
        {
            await Assert.That(edge.Type).IsEqualTo(DependencyType.TypeReference);
        }
    }
    
    [Test]
    public async Task AddDependencies_Batch_WorksCorrectly()
    {
        // Arrange
        using var collector = new SQLiteDependencyCollector();
        var edges = new[]
        {
            new DependencyEdge("A", "B", DependencyType.TypeReference, 1),
            new DependencyEdge("B", "C", DependencyType.TypeReference, 1),
            new DependencyEdge("C", "D", DependencyType.TypeReference, 1)
        };
        
        // Act
        collector.AddDependencies(edges);
        var result = collector.GetAggregatedDependencies();
        
        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
    }
    
    [Test]
    public async Task GetStats_ReturnsCorrectInformation()
    {
        // Arrange
        using var collector = new SQLiteDependencyCollector();
        
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("C", "D", DependencyType.TypeReference, 1));
        
        // Act
        var stats = collector.GetStats();
        
        // Assert
        await Assert.That(stats.TotalEdgesAdded).IsEqualTo(3);
        await Assert.That(stats.UniqueEdgesCount).IsEqualTo(2);
        await Assert.That(stats.Strategy).IsEqualTo("SQLite");
        await Assert.That(stats.MemoryUsageBytes).IsGreaterThan(0);
        await Assert.That(stats.DeduplicationRatio).IsGreaterThan(0); // (3-2)/3 = 0.33
    }
    
    [Test]
    public async Task Reset_ClearsAllData()
    {
        // Arrange
        using var collector = new SQLiteDependencyCollector();
        
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("C", "D", DependencyType.TypeReference, 1));
        
        // Act
        collector.Reset();
        var result = collector.GetAggregatedDependencies();
        var stats = collector.GetStats();
        
        // Assert
        await Assert.That(result.Count).IsEqualTo(0);
        await Assert.That(stats.TotalEdgesAdded).IsEqualTo(0);
        await Assert.That(stats.UniqueEdgesCount).IsEqualTo(0);
    }
    
    [Test]
    public async Task AddDependency_LargeDataset_HandlesEfficiently()
    {
        // Arrange
        using var collector = new SQLiteDependencyCollector(batchSize: 500);
        
        // Act - Add 10,000 edges with some duplicates
        for (int i = 0; i < 10_000; i++)
        {
            // Create pattern where some edges are duplicates
            var from = $"Entity_{i % 1000}";
            var to = $"Entity_{(i + 1) % 1000}";
            collector.AddDependency(new DependencyEdge(from, to, DependencyType.TypeReference, 1));
        }
        
        var result = collector.GetAggregatedDependencies();
        var stats = collector.GetStats();
        
        // Assert - Should have aggregated duplicates
        await Assert.That(stats.TotalEdgesAdded).IsEqualTo(10_000);
        await Assert.That(result.Count).IsLessThan(10_000); // Many duplicates
        await Assert.That(result.Count).IsGreaterThan(0);
    }
    
    [Test]
    public async Task AddDependency_BatchFlush_TriggersCorrectly()
    {
        // Arrange - Small batch size to test flushing
        using var collector = new SQLiteDependencyCollector(batchSize: 3);
        
        // Act - Add exactly batch size + 1 to trigger flush
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("B", "C", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("C", "D", DependencyType.TypeReference, 1));
        // This should trigger a flush ^
        collector.AddDependency(new DependencyEdge("D", "E", DependencyType.TypeReference, 1));
        
        var result = collector.GetAggregatedDependencies();
        
        // Assert - All edges should be stored, even across batch boundaries
        await Assert.That(result.Count).IsEqualTo(4);
    }
    
    [Test]
    public async Task Dispose_CleansUpTempDatabase()
    {
        // Arrange
        string? dbPath = null;
        
        {
            using var collector = new SQLiteDependencyCollector();
            collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
            
            // Get the database path from stats
            var stats = collector.GetStats();
            dbPath = stats.DatabasePath;
            
            // Database should exist while collector is alive
            await Assert.That(File.Exists(dbPath)).IsTrue();
        } // Dispose happens here
        
        // Assert - Database should be cleaned up after dispose
        // Note: There may be a slight delay for file deletion
        await Task.Delay(100);
        await Assert.That(File.Exists(dbPath)).IsFalse();
    }
    
    [Test]
    public async Task CustomDatabasePath_UsesPersistentFile()
    {
        // Arrange
        var customPath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid()}.db");
        
        try
        {
            // Act
            using (var collector = new SQLiteDependencyCollector(customPath))
            {
                collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
                
                // Database should exist
                await Assert.That(File.Exists(customPath)).IsTrue();
            }
            
            // Assert - Custom path database should NOT be auto-deleted
            await Assert.That(File.Exists(customPath)).IsTrue();
            
            // Can reopen and read data
            using (var collector2 = new SQLiteDependencyCollector(customPath))
            {
                var result = collector2.GetAggregatedDependencies();
                await Assert.That(result.Count).IsEqualTo(1);
                await Assert.That(result[0].FromEntity).IsEqualTo("A");
            }
        }
        finally
        {
            // Cleanup - SQLite may have WAL files that take a moment to close
            await Task.Delay(200);
            
            try
            {
                if (File.Exists(customPath))
                {
                    File.Delete(customPath);
                }
                
                // Also delete WAL and SHM files if they exist
                var walPath = customPath + "-wal";
                var shmPath = customPath + "-shm";
                if (File.Exists(walPath)) File.Delete(walPath);
                if (File.Exists(shmPath)) File.Delete(shmPath);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
