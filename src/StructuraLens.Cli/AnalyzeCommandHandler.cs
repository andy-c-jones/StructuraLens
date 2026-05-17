using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StructuraLens.Cli.Logging;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Analysis;
using StructuraLens.Core.Models;

namespace StructuraLens.Cli;

internal sealed class AnalyzeCommandHandler
{
    private readonly IServiceProvider _serviceProvider;

    public AnalyzeCommandHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<int> ExecuteAsync(
        string path,
        string? output,
        string format,
        AnalysisOptions analysisOptions,
        CancellationToken cancellationToken)
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<global::Program>>();

        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                ProgramLog.ApplicationStartup(logger, analysisOptions.ToolVersion);
            }
            ProgramLog.AnalyzingPath(logger, path);
            ProgramLog.CouplingModeEnabled(logger, "All");
            LogAnalysisOptions(logger, analysisOptions);

            var analyzer = CreateAnalyzer(analysisOptions);
            var report = await AnalyzeAsync(analyzer, path, cancellationToken);

            LogWarnings(logger, report);
            LogDirtyWorkingTree(logger, report);

            var effectiveOutput = output;
            if (string.IsNullOrEmpty(output) && format != "summary")
            {
                effectiveOutput = OutputFilenameGenerator.GenerateDefaultFilename(report, format, logger);
            }

            LogAggregationStats(logger, report);

            await WriteReportAsync(report, format, effectiveOutput, logger, cancellationToken);

            return 0;
        }
        catch (Exception ex)
        {
            ProgramLog.AnalysisError(logger, ex.Message);
            return 1;
        }
    }

    private SolutionAnalyzer CreateAnalyzer(AnalysisOptions analysisOptions)
    {
        return new SolutionAnalyzer(
            _serviceProvider.GetRequiredService<ILogger<SolutionAnalyzer>>(),
            _serviceProvider.GetRequiredService<INuGetRestorer>(),
            _serviceProvider.GetRequiredService<IMSBuildWorkspaceFactory>(),
            _serviceProvider.GetRequiredService<ICouplingAnalyzer>(),
            _serviceProvider.GetRequiredService<IMetricsCalculator>(),
            _serviceProvider.GetRequiredService<IFileSystemService>(),
            _serviceProvider.GetRequiredService<IGitRepositoryService>(),
            analysisOptions);
    }

    private static Task<AnalysisReport> AnalyzeAsync(
        SolutionAnalyzer analyzer,
        string path,
        CancellationToken cancellationToken)
    {
        return path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? analyzer.AnalyzeProjectAsync(path, cancellationToken)
            : analyzer.AnalyzeSolutionAsync(path, cancellationToken);
    }

    private static void LogAnalysisOptions(ILogger logger, AnalysisOptions analysisOptions)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            ProgramLog.AggregationStrategy(logger, analysisOptions.AggregationStrategy);
        }
        if (analysisOptions.AggregationStrategy == DependencyAggregationStrategy.Adaptive)
        {
            ProgramLog.MemoryThreshold(logger, analysisOptions.MemoryThresholdMB);
        }
    }

    private static void LogWarnings(ILogger logger, AnalysisReport report)
    {
        if (report.Warnings.Count == 0)
        {
            return;
        }

        foreach (var warning in report.Warnings.Take(10))
        {
            ProgramLog.AnalysisWarning(logger, warning);
        }

        if (report.Warnings.Count > 10)
        {
            ProgramLog.AdditionalWarnings(logger, report.Warnings.Count - 10);
        }
    }

    private static void LogDirtyWorkingTree(ILogger logger, AnalysisReport report)
    {
        if (report.GitInfo?.IsDirty == true)
        {
            ProgramLog.DirtyWorkingTree(logger);
        }
    }

    private static void LogAggregationStats(ILogger logger, AnalysisReport report)
    {
        if (report.AggregationStats is null)
        {
            return;
        }

        var stats = report.AggregationStats;
        ProgramLog.AggregationStatsHeader(logger);
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

    private async Task WriteReportAsync(
        AnalysisReport report,
        string format,
        string? output,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (format == "summary")
        {
            SummaryReportRenderer.RenderAnalysis(report, logger);
            return;
        }

        if (format == "compact")
        {
            var exporter = _serviceProvider.GetRequiredService<IReportExporter>();
            var compactReport = exporter.Export(report);
            var json = JsonSerializer.Serialize(compactReport, CliJsonOptions.CompactOutput);

            await ReportOutputWriter.WriteOrPrintAsync(json, output, cancellationToken);
            if (!string.IsNullOrEmpty(output))
            {
                ProgramLog.CompactReportWritten(logger, output, json.Length);
            }

            return;
        }

        if (format == "html")
        {
            var generator = _serviceProvider.GetRequiredService<IReportGenerator>();
            var html = generator.GenerateHtml(report);

            await ReportOutputWriter.WriteOrPrintAsync(html, output, cancellationToken);
            if (!string.IsNullOrEmpty(output))
            {
                ProgramLog.HtmlReportWritten(logger, output, html.Length);
            }

            return;
        }

        var reportJson = JsonSerializer.Serialize(report, CliJsonOptions.DefaultOutput);

        await ReportOutputWriter.WriteOrPrintAsync(reportJson, output, cancellationToken);
        if (!string.IsNullOrEmpty(output))
        {
            ProgramLog.ReportWritten(logger, output);
        }
    }
}
