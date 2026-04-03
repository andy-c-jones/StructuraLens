using StructuraLens.Core.Models;

namespace StructuraLens.Core.Diff;

public sealed class DiffCalculator
{
    public AnalysisDiffReport Compare(AnalysisReport baseReport, AnalysisReport headReport)
    {
        var hasComplexityMetrics =
            baseReport.AnalysisMode != AnalysisMode.DiagnosticsAndReferences &&
            headReport.AnalysisMode != AnalysisMode.DiagnosticsAndReferences;

        var baseProjects = baseReport.Projects.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var headProjects = headReport.Projects.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var projectNames = baseProjects.Keys
            .Union(headProjects.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var projectDiffs = new List<ProjectDiff>();
        foreach (var name in projectNames)
        {
            baseProjects.TryGetValue(name, out var baseProject);
            headProjects.TryGetValue(name, out var headProject);

            var baseMetrics = baseProject != null ? ToMetrics(baseProject, baseReport) : new ProjectDiffMetrics();
            var headMetrics = headProject != null ? ToMetrics(headProject, headReport) : new ProjectDiffMetrics();

            // Compute added/removed packages
            var baseBclSet = baseMetrics.ExternalBclDependencyNames.ToHashSet();
            var headBclSet = headMetrics.ExternalBclDependencyNames.ToHashSet();
            var addedBcl = headBclSet.Except(baseBclSet).OrderBy(x => x).ToList();
            var removedBcl = baseBclSet.Except(headBclSet).OrderBy(x => x).ToList();

            var basePackageSet = baseMetrics.ExternalPackageDependencyNames.ToHashSet();
            var headPackageSet = headMetrics.ExternalPackageDependencyNames.ToHashSet();
            var addedPackages = headPackageSet.Except(basePackageSet).OrderBy(x => x).ToList();
            var removedPackages = basePackageSet.Except(headPackageSet).OrderBy(x => x).ToList();

            // Compute added/removed declared project references
            var baseProjectRefs = baseMetrics.ProjectReferenceNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var headProjectRefs = headMetrics.ProjectReferenceNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var addedProjectRefs = headProjectRefs.Except(baseProjectRefs).OrderBy(x => x).ToList();
            var removedProjectRefs = baseProjectRefs.Except(headProjectRefs).OrderBy(x => x).ToList();

            projectDiffs.Add(new ProjectDiff
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
            });
        }

        var baseTotals = BuildTotals(baseReport);
        var headTotals = BuildTotals(headReport);

        var diagnostics = BuildDiagnosticsDiff(baseReport, headReport);

        // Compute solution-level presence tracking
        var (newToSolution, removedFromSolution) = ComputeSolutionLevelPresence(projectDiffs, baseReport, headReport);

        return new AnalysisDiffReport
        {
            Base = new DiffMetadata
            {
                SolutionPath = baseReport.SolutionPath,
                AnalyzedAt = baseReport.AnalyzedAt,
                CommitSha = baseReport.GitInfo?.CommitSha,
                BranchName = baseReport.GitInfo?.BranchName
            },
            Head = new DiffMetadata
            {
                SolutionPath = headReport.SolutionPath,
                AnalyzedAt = headReport.AnalyzedAt,
                CommitSha = headReport.GitInfo?.CommitSha,
                BranchName = headReport.GitInfo?.BranchName
            },
            HasComplexityMetrics = hasComplexityMetrics,
            Totals = new DiffTotals
            {
                BaseProjects = baseTotals.Projects,
                HeadProjects = headTotals.Projects,
                BaseTypes = baseTotals.Types,
                HeadTypes = headTotals.Types,
                BaseMethods = baseTotals.Methods,
                HeadMethods = headTotals.Methods,
                BaseCyclomaticComplexity = baseTotals.CyclomaticComplexity,
                HeadCyclomaticComplexity = headTotals.CyclomaticComplexity,
                BaseLinesOfCode = baseTotals.LinesOfCode,
                HeadLinesOfCode = headTotals.LinesOfCode,
                BaseAvgMaintainabilityIndex = baseTotals.AvgMaintainability,
                HeadAvgMaintainabilityIndex = headTotals.AvgMaintainability,
                BaseErrors = baseTotals.Errors,
                HeadErrors = headTotals.Errors,
                BaseWarnings = baseTotals.Warnings,
                HeadWarnings = headTotals.Warnings,
                BaseInfo = baseTotals.Info,
                HeadInfo = headTotals.Info,
                BaseHidden = baseTotals.Hidden,
                HeadHidden = headTotals.Hidden
            },
            Projects = projectDiffs,
            Diagnostics = diagnostics,
            NewToSolution = newToSolution,
            RemovedFromSolution = removedFromSolution
        };
    }

    private static (IReadOnlySet<string> NewToSolution, IReadOnlySet<string> RemovedFromSolution) ComputeSolutionLevelPresence(
        List<ProjectDiff> projectDiffs,
        AnalysisReport baseReport,
        AnalysisReport headReport)
    {
        // Collect all dependencies in base and head
        var baseDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var headDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in baseReport.Projects)
        {
            // Declared project references
            foreach (var dep in project.ProjectReferences)
            {
                baseDependencies.Add(dep);
            }

            // External dependencies
            foreach (var dep in project.PackageReferences)
            {
                baseDependencies.Add(dep);
            }
        }

        foreach (var project in headReport.Projects)
        {
            // Declared project references
            foreach (var dep in project.ProjectReferences)
            {
                headDependencies.Add(dep);
            }

            // External dependencies
            foreach (var dep in project.PackageReferences)
            {
                headDependencies.Add(dep);
            }
        }

        var newToSolution = headDependencies.Except(baseDependencies).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removedFromSolution = baseDependencies.Except(headDependencies).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return (newToSolution, removedFromSolution);
    }

    private static ProjectDiffMetrics ToMetrics(ProjectMetrics project, AnalysisReport report)
    {
        var allMethods = project.Types.GetAllMethods();
        var avgMi = allMethods.CalculateAverageMaintainabilityIndex();

        var projectCoupling = report.CouplingAnalysis?.ProjectCoupling
            .FirstOrDefault(pc => string.Equals(pc.EntityName, project.Name, StringComparison.OrdinalIgnoreCase));

        // Extract and categorize package names
        var bclPackages = new List<string>();
        var thirdPartyPackages = new List<string>();

        foreach (var package in project.PackageReferences)
        {
            if (IsBclNamespace(package))
                bclPackages.Add(package);
            else
                thirdPartyPackages.Add(package);
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

    private static (int Projects, int Types, int Methods, int CyclomaticComplexity, int LinesOfCode, double AvgMaintainability, int Errors, int Warnings, int Info, int Hidden) BuildTotals(AnalysisReport report)
    {
        var allMethods = report.Projects.SelectMany(p => p.Types).GetAllMethods();
        var avgMi = allMethods.CalculateAverageMaintainabilityIndex();

        var diagnostics = BuildDiagnosticsSummary(report);

        return (
            report.TotalProjects,
            report.TotalTypes,
            report.TotalMethods,
            report.TotalCyclomaticComplexity,
            report.TotalLinesOfExecutableCode,
            Math.Round(avgMi, 1),
            diagnostics.Errors,
            diagnostics.Warnings,
            diagnostics.Info,
            diagnostics.Hidden);
    }

    private static (int Errors, int Warnings, int Info, int Hidden, List<DiagnosticDiffItem> Items) BuildDiagnosticsSummary(AnalysisReport report)
    {
        var items = new List<DiagnosticDiffItem>();
        var errors = 0;
        var warnings = 0;
        var info = 0;
        var hidden = 0;

        foreach (var project in report.Projects)
        {
            var diagnostics = project.Diagnostics;
            if (diagnostics == null) continue;

            errors += diagnostics.ErrorCount;
            warnings += diagnostics.WarningCount;
            info += diagnostics.InfoCount;
            hidden += diagnostics.HiddenCount;

            foreach (var d in diagnostics.Diagnostics)
            {
                items.Add(new DiagnosticDiffItem
                {
                    Project = project.Name,
                    Id = d.Id,
                    Severity = d.Severity.ToString().ToLowerInvariant(),
                    Message = d.Message,
                    File = d.FilePath,
                    Line = d.Line,
                    Column = d.Column
                });
            }
        }

        return (errors, warnings, info, hidden, items);
    }

    private static DiagnosticDiffSummary BuildDiagnosticsDiff(AnalysisReport baseReport, AnalysisReport headReport)
    {
        var baseSummary = BuildDiagnosticsSummary(baseReport);
        var headSummary = BuildDiagnosticsSummary(headReport);

        var baseSet = new HashSet<string>(baseSummary.Items.Select(KeyFor), StringComparer.OrdinalIgnoreCase);
        var headSet = new HashSet<string>(headSummary.Items.Select(KeyFor), StringComparer.OrdinalIgnoreCase);

        var newItems = headSummary.Items.Where(i => !baseSet.Contains(KeyFor(i))).ToList();
        var resolvedItems = baseSummary.Items.Where(i => !headSet.Contains(KeyFor(i))).ToList();

        var newErrors = newItems.Count(i => i.Severity == "error");
        var newWarnings = newItems.Count(i => i.Severity == "warning");
        var resolvedErrors = resolvedItems.Count(i => i.Severity == "error");
        var resolvedWarnings = resolvedItems.Count(i => i.Severity == "warning");

        return new DiagnosticDiffSummary
        {
            BaseErrors = baseSummary.Errors,
            HeadErrors = headSummary.Errors,
            BaseWarnings = baseSummary.Warnings,
            HeadWarnings = headSummary.Warnings,
            BaseInfo = baseSummary.Info,
            HeadInfo = headSummary.Info,
            BaseHidden = baseSummary.Hidden,
            HeadHidden = headSummary.Hidden,
            NewErrors = newErrors,
            ResolvedErrors = resolvedErrors,
            NewWarnings = newWarnings,
            ResolvedWarnings = resolvedWarnings,
            TopNewErrors = newItems.Where(i => i.Severity == "error").Take(20).ToList(),
            TopNewWarnings = newItems.Where(i => i.Severity == "warning").Take(20).ToList(),
            TopResolvedErrors = resolvedItems.Where(i => i.Severity == "error").Take(20).ToList(),
            TopResolvedWarnings = resolvedItems.Where(i => i.Severity == "warning").Take(20).ToList()
        };
    }

    private static string KeyFor(DiagnosticDiffItem item)
    {
        return string.Join("|", item.Project, item.Id, item.Severity, item.Message, item.File, item.Line, item.Column);
    }

    private static bool IsBclNamespace(string packageName)
    {
        return packageName.StartsWith("System.", StringComparison.Ordinal) ||
               packageName.Equals("System", StringComparison.Ordinal) ||
               packageName.StartsWith("Microsoft.", StringComparison.Ordinal);
    }
}
