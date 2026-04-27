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
                BranchName = baseReport.GitInfo?.BranchName,
                AnalysisMode = baseReport.AnalysisMode
            },
            Head = new DiffMetadata
            {
                SolutionPath = headReport.SolutionPath,
                AnalyzedAt = headReport.AnalyzedAt,
                CommitSha = headReport.GitInfo?.CommitSha,
                BranchName = headReport.GitInfo?.BranchName,
                AnalysisMode = headReport.AnalysisMode
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

        var (unmatchedBaseItems, unmatchedHeadItems) = RemoveExactMatches(baseSummary.Items, headSummary.Items);
        var (movedItems, resolvedItems, newItems) = MatchMovedDiagnostics(unmatchedBaseItems, unmatchedHeadItems);

        var newErrors = newItems.Count(i => i.Severity == "error");
        var newWarnings = newItems.Count(i => i.Severity == "warning");
        var newInfo = newItems.Count(i => i.Severity == "info");
        var newHidden = newItems.Count(i => i.Severity == "hidden");
        var resolvedErrors = resolvedItems.Count(i => i.Severity == "error");
        var resolvedWarnings = resolvedItems.Count(i => i.Severity == "warning");
        var resolvedInfo = resolvedItems.Count(i => i.Severity == "info");
        var resolvedHidden = resolvedItems.Count(i => i.Severity == "hidden");
        var movedErrors = movedItems.Count(i => i.Severity == "error");
        var movedWarnings = movedItems.Count(i => i.Severity == "warning");
        var movedInfo = movedItems.Count(i => i.Severity == "info");
        var movedHidden = movedItems.Count(i => i.Severity == "hidden");

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
            MovedErrors = movedErrors,
            NewWarnings = newWarnings,
            ResolvedWarnings = resolvedWarnings,
            MovedWarnings = movedWarnings,
            NewInfo = newInfo,
            ResolvedInfo = resolvedInfo,
            MovedInfo = movedInfo,
            NewHidden = newHidden,
            ResolvedHidden = resolvedHidden,
            MovedHidden = movedHidden,
            AddedDiagnostics = newItems,
            ResolvedDiagnostics = resolvedItems,
            MovedDiagnostics = movedItems
        };
    }

    private static (List<DiagnosticDiffItem> Base, List<DiagnosticDiffItem> Head) RemoveExactMatches(
        IReadOnlyList<DiagnosticDiffItem> baseItems,
        IReadOnlyList<DiagnosticDiffItem> headItems)
    {
        var unmatchedBaseItems = new List<DiagnosticDiffItem>();
        var headByExactKey = BuildLookup(headItems, ExactKeyFor);

        foreach (var baseItem in baseItems)
        {
            if (!TryTake(headByExactKey, ExactKeyFor(baseItem), out _))
            {
                unmatchedBaseItems.Add(baseItem);
            }
        }

        var unmatchedHeadItems = headByExactKey.Values.SelectMany(q => q).ToList();
        return (unmatchedBaseItems, unmatchedHeadItems);
    }

    private static (List<DiagnosticMoveDiffItem> Moved, List<DiagnosticDiffItem> Resolved, List<DiagnosticDiffItem> Added) MatchMovedDiagnostics(
        IReadOnlyList<DiagnosticDiffItem> baseItems,
        IReadOnlyList<DiagnosticDiffItem> headItems)
    {
        var movedItems = new List<DiagnosticMoveDiffItem>();
        var resolvedItems = new List<DiagnosticDiffItem>();
        var headByMoveKey = BuildLookup(headItems, MoveKeyFor);

        foreach (var baseItem in baseItems)
        {
            var moveKey = MoveKeyFor(baseItem);
            if (!headByMoveKey.TryGetValue(moveKey, out var candidates) || candidates.Count == 0)
            {
                resolvedItems.Add(baseItem);
                continue;
            }

            var headItem = TakeClosest(baseItem, candidates);
            movedItems.Add(new DiagnosticMoveDiffItem
            {
                Project = baseItem.Project,
                Id = baseItem.Id,
                Severity = baseItem.Severity,
                Message = baseItem.Message,
                File = baseItem.File,
                BaseLine = baseItem.Line,
                BaseColumn = baseItem.Column,
                HeadLine = headItem.Line,
                HeadColumn = headItem.Column
            });
        }

        var addedItems = headByMoveKey.Values.SelectMany(q => q).ToList();
        return (movedItems, resolvedItems, addedItems);
    }

    private static Dictionary<string, Queue<DiagnosticDiffItem>> BuildLookup(
        IEnumerable<DiagnosticDiffItem> items,
        Func<DiagnosticDiffItem, string> keySelector)
    {
        var lookup = new Dictionary<string, Queue<DiagnosticDiffItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var key = keySelector(item);
            if (!lookup.TryGetValue(key, out var queue))
            {
                queue = new Queue<DiagnosticDiffItem>();
                lookup.Add(key, queue);
            }

            queue.Enqueue(item);
        }

        return lookup;
    }

    private static bool TryTake(
        Dictionary<string, Queue<DiagnosticDiffItem>> lookup,
        string key,
        out DiagnosticDiffItem? item)
    {
        if (lookup.TryGetValue(key, out var queue) && queue.TryDequeue(out item))
        {
            if (queue.Count == 0)
            {
                lookup.Remove(key);
            }

            return true;
        }

        item = null;
        return false;
    }

    private static DiagnosticDiffItem TakeClosest(DiagnosticDiffItem baseItem, Queue<DiagnosticDiffItem> candidates)
    {
        var candidateList = candidates.ToList();
        var closest = candidateList
            .OrderBy(i => Math.Abs(i.Line - baseItem.Line))
            .ThenBy(i => Math.Abs(i.Column - baseItem.Column))
            .ThenBy(i => i.Line)
            .ThenBy(i => i.Column)
            .First();

        candidates.Clear();
        foreach (var candidate in candidateList.Where(i => !ReferenceEquals(i, closest)))
        {
            candidates.Enqueue(candidate);
        }

        return closest;
    }

    private static string ExactKeyFor(DiagnosticDiffItem item)
    {
        return string.Join("|", item.Project, item.Id, item.Severity, item.Message, item.File, item.Line, item.Column);
    }

    private static string MoveKeyFor(DiagnosticDiffItem item)
    {
        return string.Join("|", item.Project, item.Id, item.Severity, item.Message, item.File);
    }

    private static bool IsBclNamespace(string packageName)
    {
        return packageName.StartsWith("System.", StringComparison.Ordinal) ||
               packageName.Equals("System", StringComparison.Ordinal) ||
               packageName.StartsWith("Microsoft.", StringComparison.Ordinal);
    }
}
