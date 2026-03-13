using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Export;

/// <summary>
/// Converts AnalysisReport to compact format optimized for size and visualization.
/// </summary>
public sealed class CompactReportExporter : IReportExporter
{
    /// <inheritdoc />
    public CompactReport Export(
        AnalysisReport report,
        bool includeMethodDetails = false,
        bool includeTypeDetails = false)
    {
        // Default to flat structure (backward compatible)
        var projects = ExportProjects(report, includeTypeDetails, includeMethodDetails, useNamespaceHierarchy: false);
        var graph = BuildGraph(report);
        var diagnostics = ExportDiagnostics(report);

        return new CompactReport
        {
            Version = 1,
            Path = report.SolutionPath,
            Timestamp = new DateTimeOffset(report.AnalyzedAt).ToUnixTimeMilliseconds(),
            Projects = projects,
            Graph = graph,
            Diagnostics = diagnostics,
            GitCommitSha = report.GitInfo?.CommitSha,
            GitBranch = report.GitInfo?.BranchName,
            GitRemoteUrl = report.GitInfo?.RemoteUrl,
            GitIsDirty = report.GitInfo?.IsDirty ?? false,
            ToolVersion = report.ToolVersion
        };
    }

    /// <summary>
    /// Exports the report with hierarchical namespace structure for the HTML report.
    /// </summary>
    public CompactReport ExportHierarchical(
        AnalysisReport report,
        bool includeMethodDetails = false,
        bool includeTypeDetails = false)
    {
        var projects = ExportProjects(report, includeTypeDetails, includeMethodDetails, useNamespaceHierarchy: true);
        var graph = BuildGraph(report);
        var diagnostics = ExportDiagnostics(report);

        return new CompactReport
        {
            Version = 1,
            Path = report.SolutionPath,
            Timestamp = new DateTimeOffset(report.AnalyzedAt).ToUnixTimeMilliseconds(),
            Projects = projects,
            Graph = graph,
            Diagnostics = diagnostics,
            GitCommitSha = report.GitInfo?.CommitSha,
            GitBranch = report.GitInfo?.BranchName,
            GitRemoteUrl = report.GitInfo?.RemoteUrl,
            GitIsDirty = report.GitInfo?.IsDirty ?? false,
            ToolVersion = report.ToolVersion
        };
    }

    private List<CompactProject> ExportProjects(
        AnalysisReport report,
        bool includeTypes,
        bool includeMethods,
        bool useNamespaceHierarchy = true)
    {
        var projects = new List<CompactProject>();

        // Pre-build lookup dictionary for O(1) access to project coupling metrics
        var projectCouplingLookup = report.CouplingAnalysis?.ProjectCoupling
            .ToDictionary(pc => pc.EntityName, pc => pc)
            ?? new Dictionary<string, CouplingMetrics>();

        foreach (var project in report.Projects)
        {
            var allMethods = project.Types.GetAllMethods();
            var avgMI = allMethods.CalculateAverageMaintainabilityIndex();

            // O(1) lookup instead of O(n) FirstOrDefault
            projectCouplingLookup.TryGetValue(project.Name, out var projectCoupling);

            // Derive external dependency counts from top-level PackageReferences in .csproj
            var packageRefs = project.PackageReferences;
            var totalPackages = packageRefs.Count;
            var msftPackages = packageRefs.Count(p =>
                p.StartsWith("System.", StringComparison.Ordinal) ||
                p.Equals("System", StringComparison.Ordinal) ||
                p.StartsWith("Microsoft.", StringComparison.Ordinal));
            var thirdPartyPackages = totalPackages - msftPackages;

            var compactProject = new CompactProject
            {
                Name = project.Name,
                TypeCount = project.Types.Count,
                MethodCount = allMethods.Count,
                CyclomaticComplexity = project.TotalCyclomaticComplexity,
                LinesOfCode = project.TotalLinesOfExecutableCode,
                MaxDepthOfInheritance = project.MaxDepthOfInheritance,
                AvgMaintainabilityIndex = Math.Round(avgMI, 1),
                InternalDependencies = projectCoupling?.InternalDependencies ?? 0,
                InternalDependents = projectCoupling?.InternalDependents ?? 0,
                DependencyRatio = Math.Round(projectCoupling?.DependencyRatio ?? 0, 2),
                ExternalDependencies = totalPackages,
                ExternalBclDependencies = msftPackages,
                ExternalPackageDependencies = thirdPartyPackages,
                Errors = project.Diagnostics?.ErrorCount ?? 0,
                Warnings = project.Diagnostics?.WarningCount ?? 0,
                Types = (!useNamespaceHierarchy && includeTypes) ? ExportTypes(project.Types, includeMethods) : null,
                Namespaces = (useNamespaceHierarchy && includeTypes) ? ExportNamespaces(project, includeMethods) : null
            };

            projects.Add(compactProject);
        }

        return projects;
    }

    private List<CompactType> ExportTypes(IReadOnlyList<TypeMetrics> types, bool includeMethods)
    {
        return types.Select(t =>
        {
            var avgMI = t.Methods.CalculateAverageMaintainabilityIndex();

            return new CompactType
            {
                Name = GetShortName(t.FullName),
                FullName = t.FullName,
                DepthOfInheritance = t.DepthOfInheritance,
                CyclomaticComplexity = t.TotalCyclomaticComplexity,
                LinesOfCode = t.TotalLinesOfExecutableCode,
                AvgMaintainabilityIndex = Math.Round(avgMI, 1),
                Methods = includeMethods ? ExportMethods(t.Methods) : null
            };
        }).ToList();
    }

    private List<CompactNamespace> ExportNamespaces(ProjectMetrics project, bool includeMethods)
    {
        var namespaceGroups = project.Types
            .GroupBy(t => t.Namespace)
            .OrderBy(g => g.Key);

        return namespaceGroups.Select(group =>
        {
            var types = group.ToList();
            var avgMI = types.CalculateAverageMaintainabilityIndex();

            return new CompactNamespace
            {
                Name = group.Key,
                TypeCount = types.Count,
                MethodCount = types.CountTotalMethods(),
                CyclomaticComplexity = types.CalculateTotalCyclomaticComplexity(),
                LinesOfCode = types.CalculateTotalLinesOfCode(),
                MaxDepthOfInheritance = types.CalculateMaxDepthOfInheritance(),
                AvgMaintainabilityIndex = Math.Round(avgMI, 1),
                Types = ExportTypes(types, includeMethods)
            };
        }).ToList();
    }

    private List<CompactMethod> ExportMethods(IReadOnlyList<MethodMetrics> methods)
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

    private CompactGraph BuildGraph(AnalysisReport report)
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

    private GraphLayer BuildProjectGraph(
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

    private GraphLayer BuildNamespaceGraph(
        IReadOnlyList<ProjectMetrics> projects,
        CouplingAnalysis coupling)
    {
        // Get internal namespaces from our projects and build complete metrics
        var namespaceMetrics = new Dictionary<string, (int TypeCount, int MethodCount, int CC, int LOC, double MI)>();

        foreach (var project in projects)
        {
            foreach (var type in project.Types)
            {
                var ns = type.FullName.GetNamespace();
                if (!string.IsNullOrEmpty(ns))
                {
                    if (!namespaceMetrics.ContainsKey(ns))
                    {
                        namespaceMetrics[ns] = (0, 0, 0, 0, 0.0);
                    }

                    var current = namespaceMetrics[ns];
                    namespaceMetrics[ns] = (
                        TypeCount: current.TypeCount + 1,
                        MethodCount: current.MethodCount + type.Methods.Count,
                        CC: current.CC + type.TotalCyclomaticComplexity,
                        LOC: current.LOC + type.TotalLinesOfExecutableCode,
                        MI: current.MI + type.Methods.Sum(m => m.MaintainabilityIndex)
                    );
                }
            }
        }

        // Calculate average MI for each namespace
        var namespaceMetricsWithAvgMI = namespaceMetrics.ToDictionary(
            kvp => kvp.Key,
            kvp => (
                kvp.Value.TypeCount,
                kvp.Value.MethodCount,
                kvp.Value.CC,
                kvp.Value.LOC,
                AvgMI: kvp.Value.MethodCount > 0 ? kvp.Value.MI / kvp.Value.MethodCount : 0.0
            )
        );

        var internalNamespaces = namespaceMetricsWithAvgMI.Keys.ToHashSet();

        // Build edges first to calculate coupling metrics
        var nsDeps = coupling.AllDependencies
            .Where(d => d.Type == DependencyType.NamespaceReference)
            .Where(d => internalNamespaces.Contains(d.FromEntity) && internalNamespaces.Contains(d.ToEntity))
            .Where(d => d.FromEntity != d.ToEntity) // No self-loops
            .GroupBy(d => (d.FromEntity, d.ToEntity))
            .ToList();

        // Get coupling metrics from the analysis (already calculated with internal/external split)
        var namespaceCouplingLookup = coupling.NamespaceCoupling
            .ToDictionary(c => c.EntityName, c => c);

        var nodeIndex = new Dictionary<string, int>();
        var nodes = new List<object[]>();

        // Create nodes for internal namespaces with full metrics
        // Node format: [id, name, loc, cc, mi, tc, mc, id, idx, dr, ed]
        int id = 0;
        foreach (var ns in internalNamespaces.OrderBy(n => n))
        {
            nodeIndex[ns] = id;
            var metrics = namespaceMetricsWithAvgMI[ns];
            namespaceCouplingLookup.TryGetValue(ns, out var couplingData);

            nodes.Add(new object[]
            {
                id,
                ns,
                metrics.LOC,
                metrics.CC,
                Math.Round(metrics.AvgMI, 1),
                metrics.TypeCount,
                metrics.MethodCount,
                couplingData?.InternalDependencies ?? 0,
                couplingData?.InternalDependents ?? 0,
                Math.Round(couplingData?.DependencyRatio ?? 0, 2),
                couplingData?.TotalExternalDependencies ?? 0
            });
            id++;
        }

        // Create edges for namespace-to-namespace dependencies (internal only)
        var edges = new List<int[]>();
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


    private string GetShortName(string fullName)
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

    private CompactDiagnostics? ExportDiagnostics(AnalysisReport report)
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
}
