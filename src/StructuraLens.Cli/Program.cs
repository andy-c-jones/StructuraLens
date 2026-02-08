using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StructuraLens.Cli.Logging;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Analysis;
using StructuraLens.Core.Diff;
using StructuraLens.Cli.Diff;
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

analyzeCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var path = parseResult.GetValue(pathArgument)!;
    var output = parseResult.GetValue(outputOption);
    var format = parseResult.GetValue(formatOption) ?? "json";
    var verbose = parseResult.GetValue(verboseOption);
    var aggregationStrategy = parseResult.GetValue(aggregationStrategyOption) ?? "Adaptive";
    var memoryThreshold = parseResult.GetValue(memoryThresholdOption);
    var sqliteBatchSize = parseResult.GetValue(sqliteBatchSizeOption);

    // Parse analysis options
    var analysisOptions = new AnalysisOptions
    {
        AggregationStrategy = Enum.Parse<DependencyAggregationStrategy>(aggregationStrategy, ignoreCase: true),
        MemoryThresholdMB = memoryThreshold,
        SQLiteBatchSize = sqliteBatchSize
    };

    // Adjust logging level based on verbose flag
    var executionServiceProvider = verbose 
        ? ConfigureServices(LogLevel.Debug)
        : serviceProvider;

    return await ExecuteAnalysisAsync(path, output, format, analysisOptions, executionServiceProvider, cancellationToken);
});

// Create diff subcommand
var diffCommand = new Command("diff", "Compare two JSON analysis reports and produce a diff");
diffCommand.Options.Add(baseReportOption);
diffCommand.Options.Add(headReportOption);
diffCommand.Options.Add(outputOption);
diffCommand.Options.Add(diffFormatOption);
diffCommand.Options.Add(diffMaxProjectsOption);

diffCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var basePath = parseResult.GetValue(baseReportOption);
    var headPath = parseResult.GetValue(headReportOption);
    var output = parseResult.GetValue(outputOption);
    var format = parseResult.GetValue(diffFormatOption) ?? "json";
    var maxProjects = parseResult.GetValue(diffMaxProjectsOption);
    format = format.ToLowerInvariant();

    return await ExecuteDiffAsync(basePath ?? string.Empty, headPath ?? string.Empty, output, format, maxProjects, serviceProvider, cancellationToken);
});

static async Task<int> ExecuteAnalysisAsync(
    string path,
    string? output,
    string format,
    AnalysisOptions analysisOptions,
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken)
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        ProgramLog.ApplicationStartup(logger, "0.1.0");
        ProgramLog.AnalyzingPath(logger, path);
        ProgramLog.CouplingModeEnabled(logger, "All");
        
        // Log aggregation strategy
        ProgramLog.AggregationStrategy(logger, analysisOptions.AggregationStrategy.ToString());
        if (analysisOptions.AggregationStrategy == DependencyAggregationStrategy.Adaptive)
        {
            ProgramLog.MemoryThreshold(logger, analysisOptions.MemoryThresholdMB);
        }

        // Create analyzer with options
        var analyzer = new SolutionAnalyzer(
            serviceProvider.GetRequiredService<ILogger<SolutionAnalyzer>>(),
            serviceProvider.GetRequiredService<INuGetRestorer>(),
            serviceProvider.GetRequiredService<IMSBuildWorkspaceFactory>(),
            serviceProvider.GetRequiredService<ICouplingAnalyzer>(),
            serviceProvider.GetRequiredService<IMetricsCalculator>(),
            serviceProvider.GetRequiredService<IFileSystemService>(),
            serviceProvider.GetRequiredService<IGitRepositoryService>(),
            analysisOptions);
            
        var report = path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? await analyzer.AnalyzeProjectAsync(path, cancellationToken)
            : await analyzer.AnalyzeSolutionAsync(path, cancellationToken);

        if (report.Warnings.Count > 0)
        {
            foreach (var warning in report.Warnings.Take(10))
            {
                ProgramLog.AnalysisWarning(logger, warning);
            }
            if (report.Warnings.Count > 10)
            {
                ProgramLog.AdditionalWarnings(logger, report.Warnings.Count - 10);
            }
        }

        // Warn if analyzing dirty working tree
        if (report.GitInfo?.IsDirty == true)
        {
            ProgramLog.DirtyWorkingTree(logger);
        }

        // Generate default filename if output not specified
        string? effectiveOutput = output;
        if (string.IsNullOrEmpty(output) && format != "summary")
        {
            if (report.GitInfo != null)
            {
                // Use git metadata for filename
                var sanitizedBranch = SanitizeBranchName(report.GitInfo.BranchName);
                var extension = format switch
                {
                    "html" => "html",
                    "compact" => "slr",
                    "json" => "json",
                    _ => "json"
                };
                
                effectiveOutput = $"{report.GitInfo.CommitSha[..7]}-{sanitizedBranch}.{extension}";
                ProgramLog.GitRepositoryDetected(logger, report.GitInfo.BranchName, report.GitInfo.CommitSha[..7]);
                ProgramLog.GeneratedDefaultFilename(logger, effectiveOutput);
            }
            else
            {
                // Fallback to timestamp-based filename
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
                var extension = format switch
                {
                    "html" => "html",
                    "compact" => "slr",
                    "json" => "json",
                    _ => "json"
                };
                
                effectiveOutput = $"report-{timestamp}.{extension}";
                ProgramLog.NotInGitRepository(logger);
                ProgramLog.GeneratedDefaultFilename(logger, effectiveOutput);
            }
        }

        // Display aggregation stats
        if (report.AggregationStats != null)
        {
            var stats = report.AggregationStats;
            ProgramLog.AggregationStatsHeader(logger);
            ProgramLog.AggregationStatsStrategy(logger, stats.Strategy);
            ProgramLog.AggregationStatsTotalEdges(logger, stats.TotalEdgesAdded);
            ProgramLog.AggregationStatsUniqueEdges(logger, stats.UniqueEdgesCount);
            ProgramLog.AggregationStatsDeduplication(logger, stats.DeduplicationRatio);
            ProgramLog.AggregationStatsMemory(logger, stats.MemoryUsageMB);
            if (stats.DatabasePath != null)
                ProgramLog.AggregationStatsDatabase(logger, stats.DatabasePath);
        }

        if (format == "summary")
        {
            PrintSummary(report, logger);
        }
        else if (format == "compact")
        {
            var exporter = serviceProvider.GetRequiredService<IReportExporter>();
            var compactReport = exporter.Export(report);

            var json = JsonSerializer.Serialize(compactReport, JsonOptions.CompactOutput);

            if (!string.IsNullOrEmpty(effectiveOutput))
            {
                await File.WriteAllTextAsync(effectiveOutput, json, cancellationToken);
                ProgramLog.CompactReportWritten(logger, effectiveOutput, json.Length);
            }
            else
            {
                Console.WriteLine(json);
            }
        }
        else if (format == "html")
        {
            var generator = serviceProvider.GetRequiredService<IReportGenerator>();
            var html = generator.GenerateHtml(report);

            if (!string.IsNullOrEmpty(effectiveOutput))
            {
                await File.WriteAllTextAsync(effectiveOutput, html, cancellationToken);
                ProgramLog.HtmlReportWritten(logger, effectiveOutput, html.Length);
            }
            else
            {
                Console.WriteLine(html);
            }
        }
        else
        {
            var json = JsonSerializer.Serialize(report, JsonOptions.DefaultOutput);

            if (!string.IsNullOrEmpty(effectiveOutput))
            {
                await File.WriteAllTextAsync(effectiveOutput, json, cancellationToken);
                ProgramLog.ReportWritten(logger, effectiveOutput);
            }
            else
            {
                Console.WriteLine(json);
            }
        }

        return 0;
    }
    catch (Exception ex)
    {
        ProgramLog.AnalysisError(logger, ex.Message);
        return 1;
    }
}

static async Task<int> ExecuteDiffAsync(
    string basePath,
    string headPath,
    string? output,
    string format,
    int maxProjects,
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken)
{
    try
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(headPath))
        {
            Console.Error.WriteLine("Both --base and --head reports are required for diff.");
            return 1;
        }

        var outputLabel = string.IsNullOrWhiteSpace(output) ? "(stdout)" : output;
        ProgramLog.DiffStarted(logger, basePath, headPath, format, outputLabel, maxProjects);

        var baseJson = await File.ReadAllTextAsync(basePath, cancellationToken);
        var headJson = await File.ReadAllTextAsync(headPath, cancellationToken);

        var baseReport = JsonSerializer.Deserialize<AnalysisReport>(baseJson, JsonOptions.Input);
        var headReport = JsonSerializer.Deserialize<AnalysisReport>(headJson, JsonOptions.Input);

        if (baseReport == null || headReport == null)
        {
            Console.Error.WriteLine("Unable to parse base or head report JSON.");
            return 1;
        }

        var diffCalculator = new DiffCalculator();
        var diff = diffCalculator.Compare(baseReport, headReport);

        if (format == "summary")
        {
            PrintDiffSummary(diff);
            ProgramLog.DiffCompleted(logger, format, outputLabel);
            return 0;
        }

        if (format != "json" && format != "html" && format != "markdown")
        {
            Console.Error.WriteLine("Unsupported diff format. Use json, html, markdown, or summary.");
            return 1;
        }

        if (format == "markdown")
        {
            var renderer = new DiffReportRenderer();
            var markdown = renderer.RenderMarkdown(diff, maxProjects);
            if (!string.IsNullOrEmpty(output))
            {
                await File.WriteAllTextAsync(output, markdown, cancellationToken);
            }
            else
            {
                Console.WriteLine(markdown);
            }
            ProgramLog.DiffCompleted(logger, format, outputLabel);
            return 0;
        }

        if (format == "html")
        {
            var generator = serviceProvider.GetRequiredService<IReportGenerator>();
            var html = generator.GenerateHtml(headReport, diff);
            if (!string.IsNullOrEmpty(output))
            {
                await File.WriteAllTextAsync(output, html, cancellationToken);
            }
            else
            {
                Console.WriteLine(html);
            }
            ProgramLog.DiffCompleted(logger, format, outputLabel);
            return 0;
        }

        var diffJson = JsonSerializer.Serialize(diff, JsonOptions.DefaultOutput);

        if (!string.IsNullOrEmpty(output))
        {
            await File.WriteAllTextAsync(output, diffJson, cancellationToken);
        }
        else
        {
            Console.WriteLine(diffJson);
        }

        ProgramLog.DiffCompleted(logger, format, outputLabel);

        return 0;
    }
    catch (Exception ex)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        ProgramLog.DiffFailed(logger, ex.Message);
        return 1;
    }
}


// Create root command
var rootCommand = new RootCommand("StructuraLens - C# code complexity analyzer");
rootCommand.Subcommands.Add(analyzeCommand);
rootCommand.Subcommands.Add(diffCommand);

rootCommand.SetAction(_ =>
{
    Console.WriteLine("StructuraLens v0.1.0");
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

static void PrintSummary(AnalysisReport report, ILogger logger)
{
    Console.WriteLine("=== Analysis Summary ===");
    Console.WriteLine($"Solution: {report.SolutionPath}");
    Console.WriteLine($"Analyzed at: {report.AnalyzedAt:O}");
    Console.WriteLine();
    Console.WriteLine($"Projects: {report.TotalProjects}");
    Console.WriteLine($"Types: {report.TotalTypes}");
    Console.WriteLine($"Methods: {report.TotalMethods}");
    Console.WriteLine($"Total Cyclomatic Complexity: {report.TotalCyclomaticComplexity}");
    Console.WriteLine($"Total Lines of Executable Code: {report.TotalLinesOfExecutableCode}");
    
    if (report.CouplingAnalysis != null)
    {
        Console.WriteLine();
        Console.WriteLine("=== Coupling Summary ===");
        var coupling = report.CouplingAnalysis.Summary;
        Console.WriteLine($"Mode: {coupling.CouplingMode}");
        Console.WriteLine($"Total Dependencies: {coupling.TotalDependencies}");
        Console.WriteLine($"Average Efferent Coupling: {coupling.AverageEfferentCoupling:F1}");
        Console.WriteLine($"Average Afferent Coupling: {coupling.AverageAfferentCoupling:F1}");
        Console.WriteLine($"Average Instability: {coupling.AverageInstability:F2}");
        
        if (!string.IsNullOrEmpty(coupling.MostCoupledEntity))
            Console.WriteLine($"Most Coupled Entity: {coupling.MostCoupledEntity}");
        if (!string.IsNullOrEmpty(coupling.MostUnstableEntity))
            Console.WriteLine($"Most Unstable Entity: {coupling.MostUnstableEntity}");
    }

    // Display aggregation stats
    if (report.AggregationStats != null)
    {
        Console.WriteLine();
        ProgramLog.AggregationStatsHeader(logger);
        var stats = report.AggregationStats;
        ProgramLog.AggregationStatsStrategy(logger, stats.Strategy);
        ProgramLog.AggregationStatsTotalEdges(logger, stats.TotalEdgesAdded);
        ProgramLog.AggregationStatsUniqueEdges(logger, stats.UniqueEdgesCount);
        ProgramLog.AggregationStatsDeduplication(logger, stats.DeduplicationRatio);
        ProgramLog.AggregationStatsMemory(logger, stats.MemoryUsageMB);
        if (stats.DatabasePath != null)
            ProgramLog.AggregationStatsDatabase(logger, stats.DatabasePath);
    }


    Console.WriteLine();

    foreach (var project in report.Projects)
    {
        Console.WriteLine($"Project: {project.Name}");
        Console.WriteLine($"  Types: {project.Types.Count}");
        Console.WriteLine($"  Total CC: {project.TotalCyclomaticComplexity}");
        Console.WriteLine($"  Total LOC: {project.TotalLinesOfExecutableCode}");
        Console.WriteLine($"  Max DIT: {project.MaxDepthOfInheritance}");

        var allMethods = project.Types.GetAllMethods();

        if (allMethods.Count > 0)
        {
            var avgMI = allMethods.CalculateAverageMaintainabilityIndex();
            Console.WriteLine($"  Avg Maintainability Index: {avgMI:F1}");
        }

        // Show diagnostics for this project
        if (project.Diagnostics != null)
        {
            var diag = project.Diagnostics;
            if (diag.ErrorCount > 0 || diag.WarningCount > 0)
            {
                Console.Write("  Diagnostics: ");
                if (diag.ErrorCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($"{diag.ErrorCount} errors");
                    Console.ResetColor();
                    if (diag.WarningCount > 0) Console.Write(", ");
                }
                if (diag.WarningCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"{diag.WarningCount} warnings");
                    Console.ResetColor();
                }
                Console.WriteLine();
            }
        }

        // Show coupling for this project
        if (report.CouplingAnalysis != null)
        {
            var projectCoupling = report.CouplingAnalysis.ProjectCoupling
                .FirstOrDefault(pc => pc.EntityName == project.Name);
            
            if (projectCoupling != null)
            {
                Console.WriteLine($"  Efferent Coupling (Ce): {projectCoupling.EfferentCoupling}");
                Console.WriteLine($"  Afferent Coupling (Ca): {projectCoupling.AfferentCoupling}");
                Console.WriteLine($"  Instability (I): {projectCoupling.Instability:F2}");
            }
        }

        var highComplexityMethods = allMethods
            .Where(m => m.CyclomaticComplexity > 10)
            .OrderByDescending(m => m.CyclomaticComplexity)
            .Take(5)
            .ToList();

        if (highComplexityMethods.Count > 0)
        {
            Console.WriteLine("  High complexity methods (CC > 10):");
            foreach (var method in highComplexityMethods)
            {
                Console.WriteLine($"    - {method.FullName}: CC={method.CyclomaticComplexity}");
            }
        }

        var lowMIMethods = allMethods
            .Where(m => m.MaintainabilityIndex < 40)
            .OrderBy(m => m.MaintainabilityIndex)
            .Take(5)
            .ToList();

        if (lowMIMethods.Count > 0)
        {
            Console.WriteLine("  Low maintainability methods (MI < 40):");
            foreach (var method in lowMIMethods)
            {
                Console.WriteLine($"    - {method.FullName}: MI={method.MaintainabilityIndex:F1}");
            }
        }
        Console.WriteLine();
    }
}

static void PrintDiffSummary(AnalysisDiffReport diff)
{
    Console.WriteLine("=== Diff Summary ===");
    Console.WriteLine($"Base: {diff.Base.BranchName ?? "(unknown)"} @ {diff.Base.CommitSha ?? "(unknown)"}");
    Console.WriteLine($"Head: {diff.Head.BranchName ?? "(unknown)"} @ {diff.Head.CommitSha ?? "(unknown)"}");
    Console.WriteLine();
    Console.WriteLine($"Projects: {diff.Totals.HeadProjects} (Δ {diff.Totals.ProjectsDelta:+#;-#;0})");
    Console.WriteLine($"Types: {diff.Totals.HeadTypes} (Δ {diff.Totals.TypesDelta:+#;-#;0})");
    Console.WriteLine($"Methods: {diff.Totals.HeadMethods} (Δ {diff.Totals.MethodsDelta:+#;-#;0})");
    Console.WriteLine($"Cyclomatic Complexity: {diff.Totals.HeadCyclomaticComplexity} (Δ {diff.Totals.CyclomaticComplexityDelta:+#;-#;0})");
    Console.WriteLine($"Lines of Code: {diff.Totals.HeadLinesOfCode} (Δ {diff.Totals.LinesOfCodeDelta:+#;-#;0})");
    Console.WriteLine($"Avg Maintainability: {diff.Totals.HeadAvgMaintainabilityIndex:0.0} (Δ {diff.Totals.AvgMaintainabilityDelta:+0.0;-0.0;0.0})");
    Console.WriteLine();
    Console.WriteLine($"Errors: {diff.Totals.HeadErrors} (Δ {diff.Totals.ErrorsDelta:+#;-#;0})");
    Console.WriteLine($"Warnings: {diff.Totals.HeadWarnings} (Δ {diff.Totals.WarningsDelta:+#;-#;0})");
    Console.WriteLine($"Info: {diff.Totals.HeadInfo} (Δ {diff.Totals.InfoDelta:+#;-#;0})");
}

static string SanitizeBranchName(string branchName)
{
    // Use a unified set of invalid characters that works across all platforms
    // This includes all Windows-invalid chars for maximum cross-platform compatibility
    // Characters: < > : " | ? * / \ and control characters (0-31)
    char[] invalidChars = new[] { '<', '>', ':', '"', '|', '?', '*', '/', '\\', '\0' }
        .Concat(Enumerable.Range(1, 31).Select(i => (char)i))
        .Distinct()
        .ToArray();
    
    var result = branchName;
    foreach (char c in invalidChars)
    {
        result = result.Replace(c, '_');
    }
    
    return result;
}

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

static class JsonOptions
{
    public static readonly JsonSerializerOptions DefaultOutput = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    public static readonly JsonSerializerOptions CompactOutput = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    public static readonly JsonSerializerOptions Input = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
