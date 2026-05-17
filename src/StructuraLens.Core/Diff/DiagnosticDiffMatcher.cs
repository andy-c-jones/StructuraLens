using StructuraLens.Core.Models;

namespace StructuraLens.Core.Diff;

internal static class DiagnosticDiffMatcher
{
    public static DiagnosticDiffSummary Build(AnalysisReport baseReport, AnalysisReport headReport)
    {
        var baseSummary = BuildSummary(baseReport);
        var headSummary = BuildSummary(headReport);

        var (unmatchedBaseItems, unmatchedHeadItems) = RemoveExactMatches(baseSummary.Items, headSummary.Items);
        var (movedItems, resolvedItems, newItems) = MatchMovedDiagnostics(unmatchedBaseItems, unmatchedHeadItems);

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
            NewErrors = CountSeverity(newItems, "error"),
            ResolvedErrors = CountSeverity(resolvedItems, "error"),
            MovedErrors = CountSeverity(movedItems, "error"),
            NewWarnings = CountSeverity(newItems, "warning"),
            ResolvedWarnings = CountSeverity(resolvedItems, "warning"),
            MovedWarnings = CountSeverity(movedItems, "warning"),
            NewInfo = CountSeverity(newItems, "info"),
            ResolvedInfo = CountSeverity(resolvedItems, "info"),
            MovedInfo = CountSeverity(movedItems, "info"),
            NewHidden = CountSeverity(newItems, "hidden"),
            ResolvedHidden = CountSeverity(resolvedItems, "hidden"),
            MovedHidden = CountSeverity(movedItems, "hidden"),
            AddedDiagnostics = newItems,
            ResolvedDiagnostics = resolvedItems,
            MovedDiagnostics = movedItems
        };
    }

    public static DiagnosticDiffSnapshot BuildSummary(AnalysisReport report)
    {
        var items = new List<DiagnosticDiffItem>();
        var errors = 0;
        var warnings = 0;
        var info = 0;
        var hidden = 0;

        foreach (var project in report.Projects)
        {
            var diagnostics = project.Diagnostics;
            if (diagnostics == null)
            {
                continue;
            }

            errors += diagnostics.ErrorCount;
            warnings += diagnostics.WarningCount;
            info += diagnostics.InfoCount;
            hidden += diagnostics.HiddenCount;

            foreach (var diagnostic in diagnostics.Diagnostics)
            {
                items.Add(new DiagnosticDiffItem
                {
                    Project = project.Name,
                    Id = diagnostic.Id,
                    Severity = diagnostic.Severity.ToString().ToLowerInvariant(),
                    Message = diagnostic.Message,
                    File = diagnostic.FilePath,
                    Line = diagnostic.Line,
                    Column = diagnostic.Column
                });
            }
        }

        return new DiagnosticDiffSnapshot(errors, warnings, info, hidden, items);
    }

    private static int CountSeverity(IEnumerable<DiagnosticDiffItem> items, string severity)
    {
        return items.Count(i => i.Severity == severity);
    }

    private static int CountSeverity(IEnumerable<DiagnosticMoveDiffItem> items, string severity)
    {
        return items.Count(i => i.Severity == severity);
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
}

internal readonly record struct DiagnosticDiffSnapshot(
    int Errors,
    int Warnings,
    int Info,
    int Hidden,
    List<DiagnosticDiffItem> Items);
