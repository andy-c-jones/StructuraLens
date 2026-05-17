using StructuraLens.Core.Models;

namespace StructuraLens.Core.Export;

internal static class CompactGraphBuilder
{
    public static CompactGraph Build(AnalysisReport report)
    {
        var coupling = report.CouplingAnalysis;
        if (coupling == null)
        {
            return new CompactGraph();
        }

        return new CompactGraph
        {
            Projects = BuildProjectGraph(report.Projects, coupling),
            Namespaces = BuildNamespaceGraph(report.Projects, coupling)
        };
    }

    private static GraphLayer BuildProjectGraph(
        IReadOnlyList<ProjectMetrics> projects,
        CouplingAnalysis coupling)
    {
        var projectNames = projects.Select(p => p.Name).ToHashSet();
        var nodeIndex = new Dictionary<string, int>();
        var nodes = new List<object[]>();

        for (int i = 0; i < projects.Count; i++)
        {
            var project = projects[i];
            nodeIndex[project.Name] = i;
            nodes.Add(new object[] { i, project.Name, project.TotalLinesOfExecutableCode });
        }

        var edges = coupling.AllDependencies
            .Where(d => d.Type == DependencyType.ProjectReference)
            .Where(d => projectNames.Contains(d.FromEntity) && projectNames.Contains(d.ToEntity))
            .GroupBy(d => (d.FromEntity, d.ToEntity))
            .Select(group => CreateEdge(group, nodeIndex))
            .Where(edge => edge != null)
            .Select(edge => edge!)
            .ToList();

        return new GraphLayer { Nodes = nodes, Edges = edges };
    }

    private static GraphLayer BuildNamespaceGraph(
        IReadOnlyList<ProjectMetrics> projects,
        CouplingAnalysis coupling)
    {
        var namespaceMetrics = BuildNamespaceMetrics(projects);
        var internalNamespaces = namespaceMetrics.Keys.ToHashSet();
        var namespaceCouplingLookup = coupling.NamespaceCoupling
            .ToDictionary(c => c.EntityName, c => c);

        var nodeIndex = new Dictionary<string, int>();
        var nodes = BuildNamespaceNodes(namespaceMetrics, namespaceCouplingLookup, nodeIndex);
        var edges = BuildNamespaceEdges(coupling, internalNamespaces, nodeIndex);

        return new GraphLayer { Nodes = nodes, Edges = edges };
    }

    private static Dictionary<string, NamespaceGraphMetrics> BuildNamespaceMetrics(IReadOnlyList<ProjectMetrics> projects)
    {
        var namespaceMetrics = new Dictionary<string, NamespaceGraphMetrics>();

        foreach (var project in projects)
        {
            foreach (var type in project.Types)
            {
                var ns = type.FullName.GetNamespace();
                if (string.IsNullOrEmpty(ns))
                {
                    continue;
                }

                namespaceMetrics.TryGetValue(ns, out var current);
                namespaceMetrics[ns] = current.Add(type);
            }
        }

        return namespaceMetrics;
    }

    private static List<object[]> BuildNamespaceNodes(
        Dictionary<string, NamespaceGraphMetrics> namespaceMetrics,
        Dictionary<string, CouplingMetrics> namespaceCouplingLookup,
        Dictionary<string, int> nodeIndex)
    {
        var nodes = new List<object[]>();
        var id = 0;

        foreach (var ns in namespaceMetrics.Keys.OrderBy(n => n))
        {
            nodeIndex[ns] = id;
            var metrics = namespaceMetrics[ns];
            namespaceCouplingLookup.TryGetValue(ns, out var couplingData);

            nodes.Add(new object[]
            {
                id,
                ns,
                metrics.LinesOfCode,
                metrics.CyclomaticComplexity,
                Math.Round(metrics.AvgMaintainabilityIndex, 1),
                metrics.TypeCount,
                metrics.MethodCount,
                couplingData?.InternalDependencies ?? 0,
                couplingData?.InternalDependents ?? 0,
                Math.Round(couplingData?.DependencyRatio ?? 0, 2),
                couplingData?.TotalExternalDependencies ?? 0
            });
            id++;
        }

        return nodes;
    }

    private static List<int[]> BuildNamespaceEdges(
        CouplingAnalysis coupling,
        HashSet<string> internalNamespaces,
        IReadOnlyDictionary<string, int> nodeIndex)
    {
        return coupling.AllDependencies
            .Where(d => d.Type == DependencyType.NamespaceReference)
            .Where(d => internalNamespaces.Contains(d.FromEntity) && internalNamespaces.Contains(d.ToEntity))
            .Where(d => d.FromEntity != d.ToEntity)
            .GroupBy(d => (d.FromEntity, d.ToEntity))
            .Select(group => CreateEdge(group, nodeIndex))
            .Where(edge => edge != null)
            .Select(edge => edge!)
            .ToList();
    }

    private static int[]? CreateEdge(
        IGrouping<(string FromEntity, string ToEntity), DependencyEdge> group,
        IReadOnlyDictionary<string, int> nodeIndex)
    {
        return nodeIndex.TryGetValue(group.Key.FromEntity, out var fromId) &&
            nodeIndex.TryGetValue(group.Key.ToEntity, out var toId)
            ? [fromId, toId, group.Sum(d => d.ReferenceCount)]
            : null;
    }

    private readonly record struct NamespaceGraphMetrics(
        int TypeCount,
        int MethodCount,
        int CyclomaticComplexity,
        int LinesOfCode,
        double MaintainabilityIndexTotal)
    {
        public double AvgMaintainabilityIndex => MethodCount > 0 ? MaintainabilityIndexTotal / MethodCount : 0.0;

        public NamespaceGraphMetrics Add(TypeMetrics type)
        {
            return new NamespaceGraphMetrics(
                TypeCount + 1,
                MethodCount + type.Methods.Count,
                CyclomaticComplexity + type.TotalCyclomaticComplexity,
                LinesOfCode + type.TotalLinesOfExecutableCode,
                MaintainabilityIndexTotal + type.Methods.Sum(m => m.MaintainabilityIndex));
        }
    }
}
