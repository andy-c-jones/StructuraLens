using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StructuraLens.Cli.Diff;
using StructuraLens.Cli.Logging;
using StructuraLens.Core.Diff;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Models;

namespace StructuraLens.Cli;

internal sealed class DiffCommandHandler
{
    private readonly IServiceProvider _serviceProvider;

    public DiffCommandHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<int> ExecuteAsync(
        string basePath,
        string headPath,
        string? output,
        string format,
        int maxProjects,
        DiagnosticLevel minDiagnosticLevel,
        CancellationToken cancellationToken)
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<global::Program>>();

        try
        {
            if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(headPath))
            {
                Console.Error.WriteLine("Both --base and --head reports are required for diff.");
                return 1;
            }

            var outputLabel = string.IsNullOrWhiteSpace(output) ? "(stdout)" : output;
            ProgramLog.DiffStarted(logger, basePath, headPath, format, outputLabel, maxProjects);

            var baseReport = await ReadReportAsync(basePath, cancellationToken);
            var headReport = await ReadReportAsync(headPath, cancellationToken);

            if (baseReport == null || headReport == null)
            {
                Console.Error.WriteLine("Unable to parse base or head report JSON.");
                return 1;
            }

            var diff = new DiffCalculator().Compare(baseReport, headReport);

            if (format == "summary")
            {
                SummaryReportRenderer.RenderDiff(diff);
                ProgramLog.DiffCompleted(logger, format, outputLabel);
                return 0;
            }

            if (format != "json" && format != "html" && format != "markdown")
            {
                Console.Error.WriteLine("Unsupported diff format. Use json, html, markdown, or summary.");
                return 1;
            }

            await WriteDiffAsync(diff, headReport, output, format, maxProjects, minDiagnosticLevel, cancellationToken);
            ProgramLog.DiffCompleted(logger, format, outputLabel);

            return 0;
        }
        catch (Exception ex)
        {
            ProgramLog.DiffFailed(logger, ex.Message);
            return 1;
        }
    }

    private static async Task<AnalysisReport?> ReadReportAsync(string path, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<AnalysisReport>(json, CliJsonOptions.Input);
    }

    private async Task WriteDiffAsync(
        AnalysisDiffReport diff,
        AnalysisReport headReport,
        string? output,
        string format,
        int maxProjects,
        DiagnosticLevel minDiagnosticLevel,
        CancellationToken cancellationToken)
    {
        if (format == "markdown")
        {
            var renderer = new DiffReportRenderer();
            var markdown = renderer.RenderMarkdown(diff, maxProjects, minDiagnosticLevel);
            await ReportOutputWriter.WriteOrPrintAsync(markdown, output, cancellationToken);
            return;
        }

        if (format == "html")
        {
            var generator = _serviceProvider.GetRequiredService<IReportGenerator>();
            var html = generator.GenerateHtml(headReport, diff);
            await ReportOutputWriter.WriteOrPrintAsync(html, output, cancellationToken);
            return;
        }

        var diffJson = JsonSerializer.Serialize(diff, CliJsonOptions.DefaultOutput);
        await ReportOutputWriter.WriteOrPrintAsync(diffJson, output, cancellationToken);
    }
}
