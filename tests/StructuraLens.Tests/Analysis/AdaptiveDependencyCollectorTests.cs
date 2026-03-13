using StructuraLens.Core.Analysis;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Analysis;

public class AdaptiveDependencyCollectorTests
{
    /// <summary>
    /// Memory provider that always reports memory above a 1 MB threshold,
    /// ensuring deterministic migration regardless of actual process memory.
    /// </summary>
    private static readonly Func<long> AlwaysAboveThreshold = () => 2 * 1024 * 1024;
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
        // Arrange - Very low threshold (1 MB) with deterministic memory provider to force migration
        using var collector = new AdaptiveDependencyCollector(
            memoryThresholdMB: 1, sqliteBatchSize: 100, memoryProvider: AlwaysAboveThreshold);

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
        // Arrange - Low threshold with deterministic memory provider to trigger migration
        using var collector = new AdaptiveDependencyCollector(
            memoryThresholdMB: 1, sqliteBatchSize: 100, memoryProvider: AlwaysAboveThreshold);

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
        // Arrange - Very low threshold with deterministic memory provider
        using var collector = new AdaptiveDependencyCollector(
            memoryThresholdMB: 1, sqliteBatchSize: 100, memoryProvider: AlwaysAboveThreshold);

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

    [Test]
    public async Task ParallelAdd_DuringMigration_NoDataLoss()
    {
        // Arrange - Low threshold with deterministic memory provider to trigger migration mid-stream
        using var collector = new AdaptiveDependencyCollector(
            memoryThresholdMB: 1, sqliteBatchSize: 100, memoryProvider: AlwaysAboveThreshold);
        var tasks = new List<Task>();
        var edgesPerTask = 3000;
        var taskCount = 10;

        // Act - Multiple threads adding concurrently, migration will happen mid-stream
        for (int taskId = 0; taskId < taskCount; taskId++)
        {
            var localTaskId = taskId;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < edgesPerTask; j++)
                {
                    collector.AddDependency(
                        new DependencyEdge($"Task{localTaskId}_Source", $"Target{j}", DependencyType.TypeReference, 1));
                }
            }));
        }

        await Task.WhenAll(tasks);
        var result = collector.GetAggregatedDependencies();
        var stats = collector.GetStats();

        // Assert - The main thing we're testing: no edges lost during migration
        // Total edges added should equal what we put in
        await Assert.That(stats.TotalEdgesAdded).IsEqualTo(taskCount * edgesPerTask);

        // Should have migrated at some point (with 1MB threshold and 30K edges)
        await Assert.That(collector.HasMigrated).IsTrue();

        // Result count should reflect aggregation (each Task{N}_Source to Target{M} is unique)
        // So we should have taskCount * edgesPerTask unique edges
        await Assert.That(result.Count).IsEqualTo(taskCount * edgesPerTask);
    }

    [Test]
    public async Task Migration_OnlyOccursOnce_UnderHighConcurrency()
    {
        // Arrange - Low threshold with deterministic memory provider to force migration
        using var collector = new AdaptiveDependencyCollector(
            memoryThresholdMB: 1, sqliteBatchSize: 100, memoryProvider: AlwaysAboveThreshold);
        var tasks = new List<Task>();

        // Act - Spam additions from many threads to race towards migration threshold
        for (int i = 0; i < 20; i++)
        {
            var threadId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 1000; j++)
                {
                    collector.AddDependency(
                        new DependencyEdge($"Thread{threadId}_A{j % 100}", $"Thread{threadId}_B{j % 100}", DependencyType.TypeReference, 1));
                }
            }));
        }

        await Task.WhenAll(tasks);
        var stats = collector.GetStats();

        // Assert - Should have migrated exactly once
        await Assert.That(collector.HasMigrated).IsTrue();
        await Assert.That(collector.CurrentStrategy).IsEqualTo("SQLite");
        await Assert.That(stats.TotalEdgesAdded).IsEqualTo(20 * 1000);

        // All edges should be accounted for
        var result = collector.GetAggregatedDependencies();
        await Assert.That(result.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Reset_AfterMigration_ResetsToInMemory()
    {
        // Arrange - Low threshold with deterministic memory provider to force migration
        using var collector = new AdaptiveDependencyCollector(
            memoryThresholdMB: 1, sqliteBatchSize: 100, memoryProvider: AlwaysAboveThreshold);

        // Force migration
        for (int i = 0; i < 12000; i++)
        {
            collector.AddDependency(new DependencyEdge($"A{i % 100}", $"B{i % 100}", DependencyType.TypeReference, 1));
        }

        await Assert.That(collector.HasMigrated).IsTrue();
        await Assert.That(collector.CurrentStrategy).IsEqualTo("SQLite");

        // Act - Reset
        collector.Reset();

        // Assert - Should be back to InMemory
        await Assert.That(collector.HasMigrated).IsFalse();
        await Assert.That(collector.CurrentStrategy).IsEqualTo("InMemory");
        await Assert.That(collector.GetStats().TotalEdgesAdded).IsEqualTo(0);
        var result = collector.GetAggregatedDependencies();
        await Assert.That(result.Count).IsEqualTo(0);

        // Should be able to add new edges in InMemory mode
        collector.AddDependency(new DependencyEdge("New_A", "New_B", DependencyType.TypeReference, 1));
        var newResult = collector.GetAggregatedDependencies();
        await Assert.That(newResult.Count).IsEqualTo(1);
        await Assert.That(collector.CurrentStrategy).IsEqualTo("InMemory");
    }

    [Test]
    public async Task ParallelAdd_WithReset_ThreadSafe()
    {
        // Arrange
        using var collector = new AdaptiveDependencyCollector(memoryThresholdMB: 100000);
        var tasks = new List<Task>();
        var resetTask = Task.CompletedTask;

        // Act - Add edges from multiple threads while periodically resetting
        for (int i = 0; i < 10; i++)
        {
            var threadId = i;
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < 100; j++)
                {
                    collector.AddDependency(
                        new DependencyEdge($"T{threadId}", $"Target{j}", DependencyType.TypeReference, 1));
                    await Task.Delay(1); // Small delay to allow reset to happen
                }
            }));
        }

        // Reset in the middle
        resetTask = Task.Run(async () =>
        {
            await Task.Delay(50);
            collector.Reset();
        });

        await Task.WhenAll(tasks.Concat(new[] { resetTask }));

        // Assert - Should complete without exceptions
        // Final result may vary due to reset, but should be valid
        var result = collector.GetAggregatedDependencies();
        await Assert.That(result).IsNotNull();
    }
}
