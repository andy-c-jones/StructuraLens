using StructuraLens.Core.Models;

namespace StructuraLens.Core.Export;

/// <summary>
/// Converts AnalysisReport to compact format optimized for size and visualization.
/// </summary>
public static class CompactReportExporter
{
    /// <summary>
    /// Converts an AnalysisReport to compact format.
    /// </summary>
    /// <param name="report">The full analysis report.</param>
    /// <param name="includeMethodDetails">Include individual method metrics.</param>
    /// <param name="includeTypeDetails">Include individual type metrics.</param>
    public static CompactReport Export(
        AnalysisReport report,
        bool includeMethodDetails = false,
        bool includeTypeDetails = false)
    {
        var projects = ExportProjects(report, includeTypeDetails, includeMethodDetails);
        var graph = BuildGraph(report);
        var diagnostics = ExportDiagnostics(report);

        return new CompactReport
        {
            Version = 1,
            Path = report.SolutionPath,
            Timestamp = new DateTimeOffset(report.AnalyzedAt).ToUnixTimeMilliseconds(),
            Projects = projects,
            Graph = graph,
            Diagnostics = diagnostics
        };
    }

    private static List<CompactProject> ExportProjects(
        AnalysisReport report,
        bool includeTypes,
        bool includeMethods)
    {
        var projects = new List<CompactProject>();

        // Pre-build lookup dictionary for O(1) access to project coupling metrics
        var projectCouplingLookup = report.CouplingAnalysis?.ProjectCoupling
            .ToDictionary(pc => pc.EntityName, pc => pc) 
            ?? new Dictionary<string, CouplingMetrics>();

        foreach (var project in report.Projects)
        {
            var allMethods = project.Types.SelectMany(t => t.Methods).ToList();
            var avgMI = allMethods.Count > 0 ? allMethods.Average(m => m.MaintainabilityIndex) : 0;

            // O(1) lookup instead of O(n) FirstOrDefault
            projectCouplingLookup.TryGetValue(project.Name, out var projectCoupling);

            var compactProject = new CompactProject
            {
                Name = project.Name,
                TypeCount = project.Types.Count,
                MethodCount = allMethods.Count,
                CyclomaticComplexity = project.TotalCyclomaticComplexity,
                LinesOfCode = project.TotalLinesOfExecutableCode,
                MaxDepthOfInheritance = project.MaxDepthOfInheritance,
                AvgMaintainabilityIndex = Math.Round(avgMI, 1),
                EfferentCoupling = projectCoupling?.EfferentCoupling ?? 0,
                AfferentCoupling = projectCoupling?.AfferentCoupling ?? 0,
                Instability = Math.Round(projectCoupling?.Instability ?? 0, 2),
                Errors = project.Diagnostics?.ErrorCount ?? 0,
                Warnings = project.Diagnostics?.WarningCount ?? 0,
                Types = includeTypes ? ExportTypes(project.Types, includeMethods) : null
            };

            projects.Add(compactProject);
        }

        return projects;
    }

    private static List<CompactType> ExportTypes(IReadOnlyList<TypeMetrics> types, bool includeMethods)
    {
        return types.Select(t =>
        {
            var avgMI = t.Methods.Count > 0 ? t.Methods.Average(m => m.MaintainabilityIndex) : 0;

            return new CompactType
            {
                Name = GetShortName(t.FullName),
                DepthOfInheritance = t.DepthOfInheritance,
                CyclomaticComplexity = t.TotalCyclomaticComplexity,
                LinesOfCode = t.TotalLinesOfExecutableCode,
                AvgMaintainabilityIndex = Math.Round(avgMI, 1),
                Methods = includeMethods ? ExportMethods(t.Methods) : null
            };
        }).ToList();
    }

    private static List<CompactMethod> ExportMethods(IReadOnlyList<MethodMetrics> methods)
    {
        return methods.Select(m => new CompactMethod
        {
            Name = GetShortName(m.FullName),
            CyclomaticComplexity = m.CyclomaticComplexity,
            LinesOfCode = m.LinesOfExecutableCode,
            HalsteadVolume = Math.Round(m.HalsteadVolume, 1),
            MaintainabilityIndex = Math.Round(m.MaintainabilityIndex, 1),
            StartLine = m.StartLine,
            EndLine = m.EndLine
        }).ToList();
    }

    private static CompactGraph BuildGraph(AnalysisReport report)
    {
        var coupling = report.CouplingAnalysis;
        if (coupling == null)
        {
            return new CompactGraph();
        }

        // Build project-level graph (internal only)
        var projectGraph = BuildProjectGraph(report.Projects, coupling);

        // Build namespace-level graph (internal only)
        var namespaceGraph = BuildNamespaceGraph(report.Projects, coupling);

        return new CompactGraph
        {
            Projects = projectGraph,
            Namespaces = namespaceGraph
        };
    }

    private static GraphLayer BuildProjectGraph(
        IReadOnlyList<ProjectMetrics> projects,
        CouplingAnalysis coupling)
    {
        var projectNames = projects.Select(p => p.Name).ToHashSet();
        var nodeIndex = new Dictionary<string, int>();
        var nodes = new List<object[]>();

        // Create nodes for each project
        for (int i = 0; i < projects.Count; i++)
        {
            var project = projects[i];
            nodeIndex[project.Name] = i;
            nodes.Add(new object[] { i, project.Name, project.TotalLinesOfExecutableCode });
        }

        // Create edges for project-to-project dependencies (internal only)
        var edges = new List<int[]>();
        var projectDeps = coupling.AllDependencies
            .Where(d => d.Type == DependencyType.ProjectReference)
            .Where(d => projectNames.Contains(d.FromEntity) && projectNames.Contains(d.ToEntity))
            .GroupBy(d => (d.FromEntity, d.ToEntity))
            .ToList();

        foreach (var group in projectDeps)
        {
            if (nodeIndex.TryGetValue(group.Key.FromEntity, out var fromId) &&
                nodeIndex.TryGetValue(group.Key.ToEntity, out var toId))
            {
                edges.Add(new[] { fromId, toId, group.Sum(d => d.ReferenceCount) });
            }
        }

        return new GraphLayer { Nodes = nodes, Edges = edges };
    }

    private static GraphLayer BuildNamespaceGraph(
        IReadOnlyList<ProjectMetrics> projects,
        CouplingAnalysis coupling)
    {
        // Get internal namespaces from our projects
        var internalNamespaces = new HashSet<string>();
        foreach (var project in projects)
        {
            foreach (var type in project.Types)
            {
                var ns = GetNamespace(type.FullName);
                if (!string.IsNullOrEmpty(ns))
                {
                    internalNamespaces.Add(ns);
                }
            }
        }

        // Build namespace -> LOC mapping for node sizes
        var namespaceLoc = new Dictionary<string, int>();
        foreach (var project in projects)
        {
            foreach (var type in project.Types)
            {
                var ns = GetNamespace(type.FullName);
                if (!string.IsNullOrEmpty(ns))
                {
                    namespaceLoc.TryAdd(ns, 0);
                    namespaceLoc[ns] += type.TotalLinesOfExecutableCode;
                }
            }
        }

        var nodeIndex = new Dictionary<string, int>();
        var nodes = new List<object[]>();

        // Create nodes for internal namespaces
        int id = 0;
        foreach (var ns in internalNamespaces.OrderBy(n => n))
        {
            nodeIndex[ns] = id;
            nodes.Add(new object[] { id, ns, namespaceLoc.GetValueOrDefault(ns, 0) });
            id++;
        }

        // Create edges for namespace-to-namespace dependencies (internal only)
        var edges = new List<int[]>();
        var nsDeps = coupling.AllDependencies
            .Where(d => d.Type == DependencyType.NamespaceReference)
            .Where(d => internalNamespaces.Contains(d.FromEntity) && internalNamespaces.Contains(d.ToEntity))
            .Where(d => d.FromEntity != d.ToEntity) // No self-loops
            .GroupBy(d => (d.FromEntity, d.ToEntity))
            .ToList();

        foreach (var group in nsDeps)
        {
            if (nodeIndex.TryGetValue(group.Key.FromEntity, out var fromId) &&
                nodeIndex.TryGetValue(group.Key.ToEntity, out var toId))
            {
                edges.Add(new[] { fromId, toId, group.Sum(d => d.ReferenceCount) });
            }
        }

        return new GraphLayer { Nodes = nodes, Edges = edges };
    }


    private static string GetShortName(string fullName)
    {
        // Extract just the method/type name without namespace
        var parenIndex = fullName.IndexOf('(');
        var nameWithoutParams = parenIndex > 0 ? fullName[..parenIndex] : fullName;
        
        var lastDot = nameWithoutParams.LastIndexOf('.');
        var shortName = lastDot > 0 ? nameWithoutParams[(lastDot + 1)..] : nameWithoutParams;

        // Re-add params if present
        if (parenIndex > 0)
        {
            shortName += fullName[parenIndex..];
        }

        return shortName;
    }

    private static CompactDiagnostics? ExportDiagnostics(AnalysisReport report)
    {
        var allDiagnostics = report.Projects
            .Where(p => p.Diagnostics != null)
            .SelectMany(p => p.Diagnostics!.Diagnostics.Select(d => new { Project = p.Name, Diagnostic = d }))
            .ToList();

        if (allDiagnostics.Count == 0) return null;

        var items = allDiagnostics.Select(x => new object[]
        {
            x.Project,
            x.Diagnostic.Id,
            x.Diagnostic.Severity switch
            {
                DiagnosticLevel.Error => 3,
                DiagnosticLevel.Warning => 2,
                DiagnosticLevel.Info => 1,
                _ => 0
            },
            x.Diagnostic.Message,
            x.Diagnostic.FilePath,
            x.Diagnostic.Line,
            x.Diagnostic.Column
        }).ToList();

        return new CompactDiagnostics
        {
            Errors = allDiagnostics.Count(x => x.Diagnostic.Severity == DiagnosticLevel.Error),
            Warnings = allDiagnostics.Count(x => x.Diagnostic.Severity == DiagnosticLevel.Warning),
            Info = allDiagnostics.Count(x => x.Diagnostic.Severity == DiagnosticLevel.Info),
            Items = items
        };
    }

    private static string GetNamespace(string fullTypeName)
    {
        var lastDot = fullTypeName.LastIndexOf('.');
        return lastDot > 0 ? fullTypeName[..lastDot] : "";
    }
}
