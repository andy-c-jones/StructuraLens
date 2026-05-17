using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StructuraLens.Cli;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Analysis;
using StructuraLens.Core.Export;
using StructuraLens.Core.Infrastructure;
using StructuraLens.Core.Models;

// Configure DI container with default logging
var serviceProvider = ConfigureServices(LogLevel.Information);

// Create options
var outputOption = new Option<string?>("--out", "-o")
{
    Description = "Output file path for the report"
};

var formatOption = new Option<string>("--format", "-f")
{
    Description = "Output format: json, compact, html, summary",
    DefaultValueFactory = _ => "json"
};

var verboseOption = new Option<bool>("--verbose", "-v")
{
    Description = "Enable verbose logging output"
};

var aggregationStrategyOption = new Option<string>("--aggregation-strategy")
{
    Description = "Dependency aggregation strategy: InMemory, SQLite, or Adaptive (default: Adaptive)",
    DefaultValueFactory = _ => "Adaptive"
};

var memoryThresholdOption = new Option<long>("--memory-threshold")
{
    Description = "Memory threshold in MB for adaptive strategy (default: 1024)",
    DefaultValueFactory = _ => 1024L
};

var sqliteBatchSizeOption = new Option<int>("--sqlite-batch-size")
{
    Description = "Batch size for SQLite collector (default: 1000)",
    DefaultValueFactory = _ => 1000
};

var analysisModeOption = new Option<string>("--analysis-mode")
{
    Description = "Analysis mode: Full or DiagnosticsAndReferences (default: Full)",
    DefaultValueFactory = _ => AnalysisMode.Full.ToString()
};

// Diff options
var baseReportOption = new Option<string>("--base")
{
    Description = "Path to base JSON report"
};

var headReportOption = new Option<string>("--head")
{
    Description = "Path to head JSON report"
};

var diffFormatOption = new Option<string>("--format", "-f")
{
    Description = "Diff output format: json, html, summary, markdown",
    DefaultValueFactory = _ => "json"
};

var diffMaxProjectsOption = new Option<int>("--max-projects")
{
    Description = "Max number of projects to include in markdown diff",
    DefaultValueFactory = _ => 10
};

var diffMinDiagnosticLevelOption = new Option<string>("--min-diagnostic-level")
{
    Description = "Minimum diagnostic severity to include in added/resolved tables: Hidden, Info, Warning, Error (default: Info)",
    DefaultValueFactory = _ => "Info"
};

// Create path argument
var pathArgument = new Argument<string>("path")
{
    Description = "Path to solution (.sln/.slnx) or project (.csproj) file"
};

// Create analyze subcommand
var analyzeCommand = new Command("analyze", "Analyze a solution or project for code metrics");
analyzeCommand.Arguments.Add(pathArgument);
analyzeCommand.Options.Add(outputOption);
analyzeCommand.Options.Add(formatOption);

analyzeCommand.Options.Add(verboseOption);
analyzeCommand.Options.Add(aggregationStrategyOption);
analyzeCommand.Options.Add(memoryThresholdOption);
analyzeCommand.Options.Add(sqliteBatchSizeOption);
analyzeCommand.Options.Add(analysisModeOption);

analyzeCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var path = parseResult.GetValue(pathArgument)!;
    var output = parseResult.GetValue(outputOption);
    var format = parseResult.GetValue(formatOption) ?? "json";
    var verbose = parseResult.GetValue(verboseOption);
    var aggregationStrategy = parseResult.GetValue(aggregationStrategyOption) ?? "Adaptive";
    var memoryThreshold = parseResult.GetValue(memoryThresholdOption);
    var sqliteBatchSize = parseResult.GetValue(sqliteBatchSizeOption);
    var analysisMode = parseResult.GetValue(analysisModeOption) ?? AnalysisMode.Full.ToString();

    var analysisOptions = new AnalysisOptions
    {
        AnalysisMode = Enum.Parse<AnalysisMode>(analysisMode, ignoreCase: true),
        AggregationStrategy = Enum.Parse<DependencyAggregationStrategy>(aggregationStrategy, ignoreCase: true),
        MemoryThresholdMB = memoryThreshold,
        SQLiteBatchSize = sqliteBatchSize,
        ToolVersion = VersionProvider.GetVersion()
    };

    var executionServiceProvider = verbose
        ? ConfigureServices(LogLevel.Debug)
        : serviceProvider;

    var handler = new AnalyzeCommandHandler(executionServiceProvider);
    return await handler.ExecuteAsync(path, output, format, analysisOptions, cancellationToken);
});

// Create diff subcommand
var diffCommand = new Command("diff", "Compare two JSON analysis reports and produce a diff");
diffCommand.Options.Add(baseReportOption);
diffCommand.Options.Add(headReportOption);
diffCommand.Options.Add(outputOption);
diffCommand.Options.Add(diffFormatOption);
diffCommand.Options.Add(diffMaxProjectsOption);
diffCommand.Options.Add(diffMinDiagnosticLevelOption);

diffCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var basePath = parseResult.GetValue(baseReportOption);
    var headPath = parseResult.GetValue(headReportOption);
    var output = parseResult.GetValue(outputOption);
    var format = parseResult.GetValue(diffFormatOption) ?? "json";
    var maxProjects = parseResult.GetValue(diffMaxProjectsOption);
    var minDiagnosticLevelStr = parseResult.GetValue(diffMinDiagnosticLevelOption) ?? "Info";
    var minDiagnosticLevel = Enum.TryParse<DiagnosticLevel>(minDiagnosticLevelStr, ignoreCase: true, out var parsedLevel)
        ? parsedLevel
        : DiagnosticLevel.Info;
    format = format.ToLowerInvariant();

    var handler = new DiffCommandHandler(serviceProvider);
    return await handler.ExecuteAsync(
        basePath ?? string.Empty,
        headPath ?? string.Empty,
        output,
        format,
        maxProjects,
        minDiagnosticLevel,
        cancellationToken);
});

// Create root command
var rootCommand = new RootCommand("StructuraLens - C# code complexity analyzer");

// Add --version option
var versionOption = new Option<bool>("--version")
{
    Description = "Display version information"
};
rootCommand.Options.Add(versionOption);

rootCommand.Subcommands.Add(analyzeCommand);
rootCommand.Subcommands.Add(diffCommand);

rootCommand.SetAction((parseResult) =>
{
    if (parseResult.GetValue(versionOption))
    {
        Console.WriteLine($"StructuraLens v{VersionProvider.GetVersion()}");
        return 0;
    }

    Console.WriteLine($"StructuraLens v{VersionProvider.GetVersion()}");
    Console.WriteLine("Usage: structuralens <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  analyze <path>   Analyze a solution or project for code metrics");
    Console.WriteLine("  diff             Compare two JSON analysis reports");
    Console.WriteLine();
    Console.WriteLine("Run 'structuralens <command> --help' for more information on a command.");
    return 0;
});

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

static IServiceProvider ConfigureServices(LogLevel logLevel)
{
    var services = new ServiceCollection();

    // Logging configuration
    services.AddLogging(builder =>
    {
        builder
            .AddConsole(options =>
            {
                options.FormatterName = "simple";
            })
            .SetMinimumLevel(logLevel);
    });

    // CLI logging (for Program class)
    services.AddSingleton<ILogger<Program>>(sp =>
        sp.GetRequiredService<ILoggerFactory>().CreateLogger<Program>());

    // Core services
    services.AddSingleton<IMetricsCalculator, MetricsCalculator>();
    services.AddSingleton<ICouplingAnalyzer, CouplingAnalyzer>();
    services.AddSingleton<ISolutionAnalyzer, SolutionAnalyzer>();

    // Infrastructure services
    services.AddSingleton<INuGetRestorer, NuGetRestorer>();
    services.AddSingleton<IMSBuildRegistrationService, MSBuildRegistrationService>();
    services.AddSingleton<IMSBuildWorkspaceFactory, MSBuildWorkspaceFactory>();
    services.AddSingleton<IFileSystemService, FileSystemService>();
    services.AddSingleton<IGitRepositoryService, GitRepositoryService>();

    // Export services
    services.AddSingleton<IReportExporter, CompactReportExporter>();
    services.AddSingleton<IReportGenerator, HtmlReportGenerator>();

    return services.BuildServiceProvider();
}
