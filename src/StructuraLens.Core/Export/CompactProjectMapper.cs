using StructuraLens.Core.Models;

namespace StructuraLens.Core.Export;

internal static class CompactProjectMapper
{
    public static List<CompactProject> Export(
        AnalysisReport report,
        bool includeTypes,
        bool includeMethods,
        bool useNamespaceHierarchy)
    {
        var projects = new List<CompactProject>();

        var projectCouplingLookup = report.CouplingAnalysis?.ProjectCoupling
            .ToDictionary(pc => pc.EntityName, pc => pc)
            ?? new Dictionary<string, CouplingMetrics>();

        foreach (var project in report.Projects)
        {
            projects.Add(ExportProject(project, projectCouplingLookup, includeTypes, includeMethods, useNamespaceHierarchy));
        }

        return projects;
    }

    private static CompactProject ExportProject(
        ProjectMetrics project,
        Dictionary<string, CouplingMetrics> projectCouplingLookup,
        bool includeTypes,
        bool includeMethods,
        bool useNamespaceHierarchy)
    {
        var allMethods = project.Types.GetAllMethods();
        var avgMi = allMethods.CalculateAverageMaintainabilityIndex();
        projectCouplingLookup.TryGetValue(project.Name, out var projectCoupling);

        var packageRefs = project.PackageReferences;
        var totalPackages = packageRefs.Count;
        var msftPackages = packageRefs.Count(DependencyClassifier.IsBclDependency);
        var thirdPartyPackages = totalPackages - msftPackages;

        return new CompactProject
        {
            Name = project.Name,
            TypeCount = project.Types.Count,
            MethodCount = allMethods.Count,
            CyclomaticComplexity = project.TotalCyclomaticComplexity,
            LinesOfCode = project.TotalLinesOfExecutableCode,
            MaxDepthOfInheritance = project.MaxDepthOfInheritance,
            AvgMaintainabilityIndex = Math.Round(avgMi, 1),
            InternalDependencies = projectCoupling?.InternalDependencies ?? 0,
            InternalDependents = projectCoupling?.InternalDependents ?? 0,
            DependencyRatio = Math.Round(projectCoupling?.DependencyRatio ?? 0, 2),
            ExternalDependencies = totalPackages,
            ExternalBclDependencies = msftPackages,
            ExternalPackageDependencies = thirdPartyPackages,
            Errors = project.Diagnostics?.ErrorCount ?? 0,
            Warnings = project.Diagnostics?.WarningCount ?? 0,
            Types = !useNamespaceHierarchy && includeTypes ? ExportTypes(project.Types, includeMethods) : null,
            Namespaces = useNamespaceHierarchy && includeTypes ? ExportNamespaces(project, includeMethods) : null
        };
    }

    private static List<CompactType> ExportTypes(IReadOnlyList<TypeMetrics> types, bool includeMethods)
    {
        return types.Select(t =>
        {
            var avgMi = t.Methods.CalculateAverageMaintainabilityIndex();

            return new CompactType
            {
                Name = GetShortName(t.FullName),
                FullName = t.FullName,
                DepthOfInheritance = t.DepthOfInheritance,
                CyclomaticComplexity = t.TotalCyclomaticComplexity,
                LinesOfCode = t.TotalLinesOfExecutableCode,
                AvgMaintainabilityIndex = Math.Round(avgMi, 1),
                Methods = includeMethods ? ExportMethods(t.Methods) : null
            };
        }).ToList();
    }

    private static List<CompactNamespace> ExportNamespaces(ProjectMetrics project, bool includeMethods)
    {
        var namespaceGroups = project.Types
            .GroupBy(t => t.Namespace)
            .OrderBy(g => g.Key);

        return namespaceGroups.Select(group =>
        {
            var types = group.ToList();
            var avgMi = types.CalculateAverageMaintainabilityIndex();

            return new CompactNamespace
            {
                Name = group.Key,
                TypeCount = types.Count,
                MethodCount = types.CountTotalMethods(),
                CyclomaticComplexity = types.CalculateTotalCyclomaticComplexity(),
                LinesOfCode = types.CalculateTotalLinesOfCode(),
                MaxDepthOfInheritance = types.CalculateMaxDepthOfInheritance(),
                AvgMaintainabilityIndex = Math.Round(avgMi, 1),
                Types = ExportTypes(types, includeMethods)
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

    private static string GetShortName(string fullName)
    {
        var parenIndex = fullName.IndexOf('(');
        var nameWithoutParams = parenIndex > 0 ? fullName[..parenIndex] : fullName;

        var lastDot = nameWithoutParams.LastIndexOf('.');
        var shortName = lastDot > 0 ? nameWithoutParams[(lastDot + 1)..] : nameWithoutParams;

        if (parenIndex > 0)
        {
            shortName += fullName[parenIndex..];
        }

        return shortName;
    }
}
