using Microsoft.Extensions.Logging;
using StructuraLens.Cli.Logging;
using StructuraLens.Core.Models;

namespace StructuraLens.Cli;

internal static class SummaryReportRenderer
{
    public static void RenderAnalysis(AnalysisReport report, ILogger logger)
    {
        Console.WriteLine("=== Analysis Summary ===");
        Console.WriteLine($"Tool Version: v{report.ToolVersion}");
        Console.WriteLine($"Analysis Mode: {report.AnalysisMode}");
        Console.WriteLine($"Solution: {report.SolutionPath}");
        Console.WriteLine($"Analyzed at: {report.AnalyzedAt:O}");
        Console.WriteLine();
        Console.WriteLine($"Projects: {report.TotalProjects}");
        Console.WriteLine($"Types: {report.TotalTypes}");
        Console.WriteLine($"Methods: {report.TotalMethods}");
        if (report.AnalysisMode == AnalysisMode.Full)
        {
            Console.WriteLine($"Total Cyclomatic Complexity: {report.TotalCyclomaticComplexity}");
            Console.WriteLine($"Total Lines of Executable Code: {report.TotalLinesOfExecutableCode}");
        }

        RenderCouplingSummary(report);
        RenderAggregationStats(report, logger);

        Console.WriteLine();

        foreach (var project in report.Projects)
        {
            RenderProjectSummary(project, report);
        }
    }

    public static void RenderDiff(AnalysisDiffReport diff)
    {
        Console.WriteLine("=== Diff Summary ===");
        Console.WriteLine($"Base: {diff.Base.BranchName ?? "(unknown)"} @ {diff.Base.CommitSha ?? "(unknown)"}");
        Console.WriteLine($"Head: {diff.Head.BranchName ?? "(unknown)"} @ {diff.Head.CommitSha ?? "(unknown)"}");
        Console.WriteLine();
        Console.WriteLine($"Projects: {diff.Totals.HeadProjects} (Δ {diff.Totals.ProjectsDelta:+#;-#;0})");
        Console.WriteLine($"Types: {diff.Totals.HeadTypes} (Δ {diff.Totals.TypesDelta:+#;-#;0})");
        Console.WriteLine($"Methods: {diff.Totals.HeadMethods} (Δ {diff.Totals.MethodsDelta:+#;-#;0})");
        if (diff.HasComplexityMetrics)
        {
            Console.WriteLine($"Cyclomatic Complexity: {diff.Totals.HeadCyclomaticComplexity} (Δ {diff.Totals.CyclomaticComplexityDelta:+#;-#;0})");
            Console.WriteLine($"Lines of Code: {diff.Totals.HeadLinesOfCode} (Δ {diff.Totals.LinesOfCodeDelta:+#;-#;0})");
            Console.WriteLine($"Avg Maintainability: {diff.Totals.HeadAvgMaintainabilityIndex:0.0} (Δ {diff.Totals.AvgMaintainabilityDelta:+0.0;-0.0;0.0})");
        }

        Console.WriteLine();
        Console.WriteLine($"Errors: {diff.Totals.HeadErrors} (Δ {diff.Totals.ErrorsDelta:+#;-#;0})");
        Console.WriteLine($"Warnings: {diff.Totals.HeadWarnings} (Δ {diff.Totals.WarningsDelta:+#;-#;0})");
        Console.WriteLine($"Info: {diff.Totals.HeadInfo} (Δ {diff.Totals.InfoDelta:+#;-#;0})");
    }

    private static void RenderCouplingSummary(AnalysisReport report)
    {
        if (report.CouplingAnalysis == null)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("=== Coupling Summary ===");
        var coupling = report.CouplingAnalysis.Summary;
        Console.WriteLine($"Mode: {coupling.CouplingMode}");
        Console.WriteLine($"Total Dependencies: {coupling.TotalDependencies}");
        Console.WriteLine($"Average Internal Dependencies: {coupling.AverageInternalDependencies:F1}");
        Console.WriteLine($"Average Internal Dependents: {coupling.AverageInternalDependents:F1}");
        Console.WriteLine($"Average Dependency Ratio: {coupling.AverageDependencyRatio:F2}");
        Console.WriteLine($"Average External Dependencies: {coupling.AverageExternalDependencies:F1}");
        Console.WriteLine($"  - BCL (System/Microsoft): {coupling.AverageExternalBclDependencies:F1}");
        Console.WriteLine($"  - Third-party Packages: {coupling.AverageExternalPackageDependencies:F1}");

        if (!string.IsNullOrEmpty(coupling.MostCoupledEntity))
        {
            Console.WriteLine($"Most Coupled Entity: {coupling.MostCoupledEntity}");
        }

        if (!string.IsNullOrEmpty(coupling.MostDependentEntity))
        {
            Console.WriteLine($"Most Referenced Component: {coupling.MostDependentEntity}");
        }

        if (!string.IsNullOrEmpty(coupling.HighestConsumerEntity))
        {
            Console.WriteLine($"Highest-Level Consumer: {coupling.HighestConsumerEntity}");
        }
    }

    private static void RenderAggregationStats(AnalysisReport report, ILogger logger)
    {
        if (report.AggregationStats == null)
        {
            return;
        }

        Console.WriteLine();
        ProgramLog.AggregationStatsHeader(logger);
        var stats = report.AggregationStats;
        ProgramLog.AggregationStatsStrategy(logger, stats.Strategy);
        ProgramLog.AggregationStatsTotalEdges(logger, stats.TotalEdgesAdded);
        ProgramLog.AggregationStatsUniqueEdges(logger, stats.UniqueEdgesCount);
        ProgramLog.AggregationStatsDeduplication(logger, stats.DeduplicationRatio);
        ProgramLog.AggregationStatsMemory(logger, stats.MemoryUsageMB);
        if (stats.DatabasePath != null)
        {
            ProgramLog.AggregationStatsDatabase(logger, stats.DatabasePath);
        }
    }

    private static void RenderProjectSummary(ProjectMetrics project, AnalysisReport report)
    {
        Console.WriteLine($"Project: {project.Name}");
        Console.WriteLine($"  Types: {project.Types.Count}");
        if (report.AnalysisMode == AnalysisMode.Full)
        {
            Console.WriteLine($"  Total CC: {project.TotalCyclomaticComplexity}");
            Console.WriteLine($"  Total LOC: {project.TotalLinesOfExecutableCode}");
        }

        Console.WriteLine($"  Max DIT: {project.MaxDepthOfInheritance}");

        var allMethods = project.Types.GetAllMethods();

        if (allMethods.Count > 0 && report.AnalysisMode == AnalysisMode.Full)
        {
            var avgMI = allMethods.CalculateAverageMaintainabilityIndex();
            Console.WriteLine($"  Avg Maintainability Index: {avgMI:F1}");
        }

        RenderProjectDiagnostics(project);
        RenderProjectCoupling(project, report);

        if (report.AnalysisMode == AnalysisMode.Full)
        {
            RenderHighComplexityMethods(allMethods);
            RenderLowMaintainabilityMethods(allMethods);
        }

        Console.WriteLine();
    }

    private static void RenderProjectDiagnostics(ProjectMetrics project)
    {
        if (project.Diagnostics == null)
        {
            return;
        }

        var diag = project.Diagnostics;
        if (diag.ErrorCount == 0 && diag.WarningCount == 0)
        {
            return;
        }

        Console.Write("  Diagnostics: ");
        if (diag.ErrorCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"{diag.ErrorCount} errors");
            Console.ResetColor();
            if (diag.WarningCount > 0)
            {
                Console.Write(", ");
            }
        }

        if (diag.WarningCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"{diag.WarningCount} warnings");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    private static void RenderProjectCoupling(ProjectMetrics project, AnalysisReport report)
    {
        if (report.CouplingAnalysis == null)
        {
            return;
        }

        var projectCoupling = report.CouplingAnalysis.ProjectCoupling
            .FirstOrDefault(pc => pc.EntityName == project.Name);

        if (projectCoupling == null)
        {
            return;
        }

        Console.WriteLine($"  Internal Dependencies: {projectCoupling.InternalDependencies}");
        Console.WriteLine($"  Internal Dependents: {projectCoupling.InternalDependents}");
        Console.WriteLine($"  Dependency Ratio: {projectCoupling.DependencyRatio:F2}");
        Console.WriteLine($"  External Dependencies: {projectCoupling.TotalExternalDependencies}");
        Console.WriteLine($"    - BCL: {projectCoupling.ExternalBclDependencies}");
        Console.WriteLine($"    - Packages: {projectCoupling.ExternalPackageDependencies}");
    }

    private static void RenderHighComplexityMethods(IReadOnlyList<MethodMetrics> allMethods)
    {
        var highComplexityMethods = allMethods
            .Where(m => m.CyclomaticComplexity > 10)
            .OrderByDescending(m => m.CyclomaticComplexity)
            .Take(5)
            .ToList();

        if (highComplexityMethods.Count == 0)
        {
            return;
        }

        Console.WriteLine("  High complexity methods (CC > 10):");
        foreach (var method in highComplexityMethods)
        {
            Console.WriteLine($"    - {method.FullName}: CC={method.CyclomaticComplexity}");
        }
    }

    private static void RenderLowMaintainabilityMethods(IReadOnlyList<MethodMetrics> allMethods)
    {
        var lowMIMethods = allMethods
            .Where(m => m.MaintainabilityIndex < 40)
            .OrderBy(m => m.MaintainabilityIndex)
            .Take(5)
            .ToList();

        if (lowMIMethods.Count == 0)
        {
            return;
        }

        Console.WriteLine("  Low maintainability methods (MI < 40):");
        foreach (var method in lowMIMethods)
        {
            Console.WriteLine($"    - {method.FullName}: MI={method.MaintainabilityIndex:F1}");
        }
    }
}
