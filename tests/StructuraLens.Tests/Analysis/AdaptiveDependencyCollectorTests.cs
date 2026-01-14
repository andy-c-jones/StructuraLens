using StructuraLens.Core.Analysis;
using StructuraLens.Core.Models;
using TUnit.Core;

namespace StructuraLens.Tests.Analysis;

public class AdaptiveDependencyCollectorTests
{
    [Test]
    public async Task AddDependency_BelowThreshold_StaysInMemory()
    {
        // Arrange - Very high threshold so we never migrate
        using var collector = new AdaptiveDependencyCollector(memoryThresholdMB: 100000);
        var edge = new DependencyEdge("A", "B", DependencyType.TypeReference, 1);
        
        // Act
        collector.AddDependency(edge);
        var result = collector.GetAggregatedDependencies();
        var stats = collector.GetStats();
        
        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(collector.HasMigrated).IsFalse();
        await Assert.That(collector.CurrentStrategy).IsEqualTo("InMemory");
        await Assert.That(stats.Strategy).IsEqualTo("Adaptive-InMemory");
    }
    
    [Test]
    public async Task AddDependency_DuplicateEdges_AggregatesCorrectly()
    {
        // Arrange
        using var collector = new AdaptiveDependencyCollector(memoryThresholdMB: 100000);
        
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
    public async Task AddDependency_MultipleEdges_StoresCorrectly()
    {
        // Arrange
        using var collector = new AdaptiveDependencyCollector(memoryThresholdMB: 100000);
        
        // Act
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("B", "C", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("C", "D", DependencyType.TypeReference, 1));
        
        var result = collector.GetAggregatedDependencies();
        
        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(collector.HasMigrated).IsFalse();
    }
    
    [Test]
    public async Task GetAggregatedDependencies_ByType_FiltersCorrectly()
    {
        // Arrange
        using var collector = new AdaptiveDependencyCollector(memoryThresholdMB: 100000);
        
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("C", "D", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("X", "Y", DependencyType.NamespaceReference, 1));
        collector.AddDependency(new DependencyEdge("P", "Q", DependencyType.ProjectReference, 1));
        
        // Act
        var typeRefs = collector.GetAggregatedDependencies(DependencyType.TypeReference);
        var namespaceRefs = collector.GetAggregatedDependencies(DependencyType.NamespaceReference);
        
        // Assert
        await Assert.That(typeRefs.Count).IsEqualTo(2);
        await Assert.That(namespaceRefs.Count).IsEqualTo(1);
    }
    
    [Test]
    public async Task AddDependencies_Batch_WorksCorrectly()
    {
        // Arrange
        using var collector = new AdaptiveDependencyCollector(memoryThresholdMB: 100000);
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
    public async Task Reset_ClearsAllData()
    {
        // Arrange
        using var collector = new AdaptiveDependencyCollector(memoryThresholdMB: 100000);
        
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("C", "D", DependencyType.TypeReference, 1));
        
        // Act
        collector.Reset();
        var result = collector.GetAggregatedDependencies();
        var stats = collector.GetStats();
        
        // Assert
        await Assert.That(result.Count).IsEqualTo(0);
        await Assert.That(stats.TotalEdgesAdded).IsEqualTo(0);
        await Assert.That(collector.HasMigrated).IsFalse();
    }
    
    [Test]
    public async Task AddDependency_LowThreshold_MigratesToSQLite()
    {
        // Arrange - Very low threshold (1 MB) to force migration
        using var collector = new AdaptiveDependencyCollector(memoryThresholdMB: 1, sqliteBatchSize: 100);
        
        // Act - Add more than CheckInterval (10K) edges to trigger memory check
        for (int i = 0; i < 12000; i++)
        {
            collector.AddDependency(new DependencyEdge($"Entity_{i % 100}", $"Entity_{(i + 1) % 100}", DependencyType.TypeReference, 1));
        }
        
        var result = collector.GetAggregatedDependencies();
        var stats = collector.GetStats();
        
        // Assert - Should have migrated to SQLite
        await Assert.That(collector.HasMigrated).IsTrue();
        await Assert.That(collector.CurrentStrategy).IsEqualTo("SQLite");
        await Assert.That(stats.Strategy).IsEqualTo("Adaptive-SQLite");
        await Assert.That(result.Count).IsGreaterThan(0);
        await Assert.That(stats.TotalEdgesAdded).IsEqualTo(12000);
    }
    
    [Test]
    public async Task Migration_PreservesAllData()
    {
        // Arrange - Low threshold to trigger migration
        using var collector = new AdaptiveDependencyCollector(memoryThresholdMB: 1, sqliteBatchSize: 100);
        
        // Act - Add edges before and after migration
        for (int i = 0; i < 5000; i++)
        {
            collector.AddDependency(new DependencyEdge($"A", $"B_{i}", DependencyType.TypeReference, 1));
        }
        
        var beforeMigration = collector.HasMigrated;
        
        // Add more to trigger migration
        for (int i = 0; i < 7000; i++)
        {
            collector.AddDependency(new DependencyEdge($"C", $"D_{i}", DependencyType.TypeReference, 1));
        }
        
        var afterMigration = collector.HasMigrated;
        var result = collector.GetAggregatedDependencies();
        
        // Assert
        await Assert.That(beforeMigration).IsFalse();
        await Assert.That(afterMigration).IsTrue();
        await Assert.That(result.Count).IsGreaterThan(10000); // Should have preserved all unique edges
    }
    
    [Test]
    public async Task GetStats_ReturnsCorrectStrategy()
    {
        // Arrange
        using var collector = new AdaptiveDependencyCollector(memoryThresholdMB: 100000);
        
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("C", "D", DependencyType.TypeReference, 1));
        
        // Act
        var stats = collector.GetStats();
        
        // Assert
        await Assert.That(stats.TotalEdgesAdded).IsEqualTo(3);
        await Assert.That(stats.UniqueEdgesCount).IsEqualTo(2);
        await Assert.That(stats.Strategy).IsEqualTo("Adaptive-InMemory");
    }
    
    [Test]
    public async Task ParallelAdd_ThreadSafe()
    {
        // Arrange
        using var collector = new AdaptiveDependencyCollector(memoryThresholdMB: 100000);
        var tasks = new List<Task>();
        
        // Act - 50 tasks, each adding 500 identical edges
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 500; j++)
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
        await Assert.That(result[0].ReferenceCount).IsEqualTo(25_000);
    }
    
    [Test]
    public async Task AfterMigration_ContinuesToWork()
    {
        // Arrange - Very low threshold
        using var collector = new AdaptiveDependencyCollector(memoryThresholdMB: 1, sqliteBatchSize: 100);
        
        // Act - Force migration
        for (int i = 0; i < 11000; i++)
        {
            collector.AddDependency(new DependencyEdge($"Pre_{i % 50}", $"Pre_{(i + 1) % 50}", DependencyType.TypeReference, 1));
        }
        
        await Assert.That(collector.HasMigrated).IsTrue();
        
        // Add more after migration
        collector.AddDependency(new DependencyEdge("Post_A", "Post_B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("Post_C", "Post_D", DependencyType.TypeReference, 1));
        
        var result = collector.GetAggregatedDependencies();
        var postEdges = result.Where(e => e.FromEntity.StartsWith("Post_")).ToList();
        
        // Assert
        await Assert.That(postEdges.Count).IsEqualTo(2);
        await Assert.That(collector.CurrentStrategy).IsEqualTo("SQLite");
    }
}
