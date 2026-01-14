using StructuraLens.Core.Analysis;
using StructuraLens.Core.Models;
using TUnit.Core;

namespace StructuraLens.Tests.Analysis;

public class InMemoryDependencyCollectorTests
{
    [Test]
    public async Task AddDependency_SingleEdge_StoresCorrectly()
    {
        // Arrange
        using var collector = new InMemoryDependencyCollector();
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
        using var collector = new InMemoryDependencyCollector();
        
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
        using var collector = new InMemoryDependencyCollector();
        
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
        using var collector = new InMemoryDependencyCollector();
        
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
        using var collector = new InMemoryDependencyCollector();
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
        using var collector = new InMemoryDependencyCollector();
        
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
        using var collector = new InMemoryDependencyCollector();
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
        using var collector = new InMemoryDependencyCollector();
        
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        collector.AddDependency(new DependencyEdge("C", "D", DependencyType.TypeReference, 1));
        
        // Act
        var stats = collector.GetStats();
        
        // Assert
        await Assert.That(stats.TotalEdgesAdded).IsEqualTo(3);
        await Assert.That(stats.UniqueEdgesCount).IsEqualTo(2);
        await Assert.That(stats.Strategy).IsEqualTo("InMemory");
        await Assert.That(stats.MemoryUsageBytes).IsGreaterThan(0);
        await Assert.That(stats.DeduplicationRatio).IsGreaterThan(0); // (3-2)/3 = 0.33
    }
    
    [Test]
    public async Task Reset_ClearsAllData()
    {
        // Arrange
        using var collector = new InMemoryDependencyCollector();
        
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
}
