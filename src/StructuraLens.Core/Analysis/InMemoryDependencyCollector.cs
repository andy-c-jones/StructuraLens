using System.Collections.Concurrent;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// In-memory dependency collector that aggregates edges on-the-fly.
/// Thread-safe implementation using ConcurrentDictionary.
/// </summary>
public sealed class InMemoryDependencyCollector : IDependencyCollector
{
    private readonly ConcurrentDictionary<EdgeKey, int> _aggregatedEdges = new();
    private long _totalAdded;

    /// <summary>
    /// Composite key for aggregating dependency edges.
    /// </summary>
    private readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        public readonly string From;
        public readonly string To;
        public readonly DependencyType Type;

        public EdgeKey(string from, string to, DependencyType type)
        {
            From = from;
            To = to;
            Type = type;
        }

        public bool Equals(EdgeKey other) =>
            From == other.From && To == other.To && Type == other.Type;

        public override bool Equals(object? obj) =>
            obj is EdgeKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(From, To, Type);
    }

    /// <inheritdoc />
    public void AddDependency(DependencyEdge edge)
    {
        Interlocked.Increment(ref _totalAdded);

        var key = new EdgeKey(edge.FromEntity, edge.ToEntity, edge.Type);
        _aggregatedEdges.AddOrUpdate(
            key,
            _ => edge.ReferenceCount,
            (_, existing) => existing + edge.ReferenceCount);
    }

    /// <inheritdoc />
    public void AddDependencies(IEnumerable<DependencyEdge> edges)
    {
        foreach (var edge in edges)
            AddDependency(edge);
    }

    /// <inheritdoc />
    public IReadOnlyList<DependencyEdge> GetAggregatedDependencies()
    {
        var result = new List<DependencyEdge>(_aggregatedEdges.Count);
        foreach (var kvp in _aggregatedEdges)
        {
            result.Add(new DependencyEdge(
                kvp.Key.From,
                kvp.Key.To,
                kvp.Key.Type,
                kvp.Value));
        }
        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<DependencyEdge> GetAggregatedDependencies(DependencyType type)
    {
        var result = new List<DependencyEdge>();
        foreach (var kvp in _aggregatedEdges)
        {
            if (kvp.Key.Type == type)
            {
                result.Add(new DependencyEdge(
                    kvp.Key.From,
                    kvp.Key.To,
                    kvp.Key.Type,
                    kvp.Value));
            }
        }
        return result;
    }

    /// <inheritdoc />
    public DependencyCollectorStats GetStats()
    {
        return new DependencyCollectorStats(
            TotalEdgesAdded: _totalAdded,
            UniqueEdgesCount: _aggregatedEdges.Count,
            MemoryUsageBytes: EstimateMemoryUsage(),
            Strategy: "InMemory");
    }

    /// <summary>
    /// Estimates memory usage based on dictionary size and average string lengths.
    /// </summary>
    private long EstimateMemoryUsage()
    {
        // Rough estimate: each entry = key (2 strings + enum) + value + overhead
        // Average fully-qualified name: ~40 bytes
        // Dictionary entry overhead: ~32 bytes
        const int avgStringBytes = 40;
        const int enumBytes = 4;
        const int intBytes = 4;
        const int dictOverhead = 32;

        return _aggregatedEdges.Count * (avgStringBytes * 2 + enumBytes + intBytes + dictOverhead);
    }

    /// <inheritdoc />
    public void Reset()
    {
        _aggregatedEdges.Clear();
        _totalAdded = 0;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // No unmanaged resources to dispose
    }
}
