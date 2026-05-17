using StructuraLens.Core.Models;

namespace StructuraLens.Core.Diff;

internal static class SolutionDependencyPresenceCalculator
{
    public static (IReadOnlySet<string> NewToSolution, IReadOnlySet<string> RemovedFromSolution) Compute(
        AnalysisReport baseReport,
        AnalysisReport headReport)
    {
        var baseDependencies = BuildDependencySet(baseReport);
        var headDependencies = BuildDependencySet(headReport);

        return (
            headDependencies.Except(baseDependencies).ToHashSet(StringComparer.OrdinalIgnoreCase),
            baseDependencies.Except(headDependencies).ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private static HashSet<string> BuildDependencySet(AnalysisReport report)
    {
        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in report.Projects)
        {
            foreach (var dependency in project.ProjectReferences)
            {
                dependencies.Add(dependency);
            }

            foreach (var dependency in project.PackageReferences)
            {
                dependencies.Add(dependency);
            }
        }

        return dependencies;
    }
}
