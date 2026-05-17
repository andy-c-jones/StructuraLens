using StructuraLens.Core.Models;

namespace StructuraLens.Core.Diff;

internal static class ProjectDiffBuilder
{
    public static List<ProjectDiff> Build(AnalysisReport baseReport, AnalysisReport headReport)
    {
        var baseProjects = baseReport.Projects.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var headProjects = headReport.Projects.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        return baseProjects.Keys
            .Union(headProjects.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(name => BuildProjectDiff(name, baseProjects, headProjects, baseReport, headReport))
            .ToList();
    }

    private static ProjectDiff BuildProjectDiff(
        string name,
        Dictionary<string, ProjectMetrics> baseProjects,
        Dictionary<string, ProjectMetrics> headProjects,
        AnalysisReport baseReport,
        AnalysisReport headReport)
    {
        baseProjects.TryGetValue(name, out var baseProject);
        headProjects.TryGetValue(name, out var headProject);

        var baseMetrics = baseProject != null ? ToMetrics(baseProject, baseReport) : new ProjectDiffMetrics();
        var headMetrics = headProject != null ? ToMetrics(headProject, headReport) : new ProjectDiffMetrics();

        var (addedBcl, removedBcl) = GetDependencyChanges(
            baseMetrics.ExternalBclDependencyNames,
            headMetrics.ExternalBclDependencyNames);
        var (addedPackages, removedPackages) = GetDependencyChanges(
            baseMetrics.ExternalPackageDependencyNames,
            headMetrics.ExternalPackageDependencyNames);
        var (addedProjectRefs, removedProjectRefs) = GetProjectReferenceChanges(
            baseMetrics.ProjectReferenceNames,
            headMetrics.ProjectReferenceNames);

        return new ProjectDiff
        {
            Name = name,
            IsAdded = baseProject == null && headProject != null,
            IsRemoved = baseProject != null && headProject == null,
            Base = baseMetrics,
            Head = headMetrics,
            AddedBclDependencies = addedBcl,
            RemovedBclDependencies = removedBcl,
            AddedPackageDependencies = addedPackages,
            RemovedPackageDependencies = removedPackages,
            AddedProjectReferences = addedProjectRefs,
            RemovedProjectReferences = removedProjectRefs
        };
    }

    private static ProjectDiffMetrics ToMetrics(ProjectMetrics project, AnalysisReport report)
    {
        var allMethods = project.Types.GetAllMethods();
        var avgMi = allMethods.CalculateAverageMaintainabilityIndex();

        var projectCoupling = report.CouplingAnalysis?.ProjectCoupling
            .FirstOrDefault(pc => string.Equals(pc.EntityName, project.Name, StringComparison.OrdinalIgnoreCase));

        var bclPackages = new List<string>();
        var thirdPartyPackages = new List<string>();

        foreach (var package in project.PackageReferences)
        {
            if (DependencyClassifier.IsBclDependency(package))
            {
                bclPackages.Add(package);
            }
            else
            {
                thirdPartyPackages.Add(package);
            }
        }

        return new ProjectDiffMetrics
        {
            TypeCount = project.Types.Count,
            MethodCount = allMethods.Count,
            CyclomaticComplexity = project.TotalCyclomaticComplexity,
            LinesOfCode = project.TotalLinesOfExecutableCode,
            MaxDepthOfInheritance = project.MaxDepthOfInheritance,
            AvgMaintainabilityIndex = Math.Round(avgMi, 1),
            InternalDependencies = projectCoupling?.InternalDependencies ?? 0,
            InternalDependents = projectCoupling?.InternalDependents ?? 0,
            DependencyRatio = Math.Round(projectCoupling?.DependencyRatio ?? 0, 2),
            ExternalDependencies = projectCoupling?.TotalExternalDependencies ?? 0,
            ExternalBclDependencies = bclPackages.Count,
            ExternalPackageDependencies = thirdPartyPackages.Count,
            ExternalBclDependencyNames = bclPackages,
            ExternalPackageDependencyNames = thirdPartyPackages,
            ProjectReferenceNames = project.ProjectReferences,
            Errors = project.Diagnostics?.ErrorCount ?? 0,
            Warnings = project.Diagnostics?.WarningCount ?? 0
        };
    }

    private static (List<string> Added, List<string> Removed) GetDependencyChanges(
        IReadOnlyList<string> baseDependencies,
        IReadOnlyList<string> headDependencies)
    {
        var baseSet = baseDependencies.ToHashSet();
        var headSet = headDependencies.ToHashSet();
        return (
            headSet.Except(baseSet).OrderBy(x => x).ToList(),
            baseSet.Except(headSet).OrderBy(x => x).ToList());
    }

    private static (List<string> Added, List<string> Removed) GetProjectReferenceChanges(
        IReadOnlyList<string> baseProjectRefs,
        IReadOnlyList<string> headProjectRefs)
    {
        var baseSet = baseProjectRefs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var headSet = headProjectRefs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (
            headSet.Except(baseSet).OrderBy(x => x).ToList(),
            baseSet.Except(headSet).OrderBy(x => x).ToList());
    }
}
