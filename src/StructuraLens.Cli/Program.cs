using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StructuraLens.Cli.Logging;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Analysis;
using StructuraLens.Core.Export;
using StructuraLens.Core.Infrastructure;
using StructuraLens.Core.Models;

// Configure DI container
var services = new ServiceCollection();

// Logging configuration
services.AddLogging(builder =>
{
    builder
        .AddConsole(options =>
        {
            options.FormatterName = "simple";
        })
        .SetMinimumLevel(LogLevel.Information);
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

// Export services
services.AddSingleton<IReportExporter, CompactReportExporter>();
services.AddSingleton<IReportGenerator, HtmlReportGenerator>();

var serviceProvider = services.BuildServiceProvider();

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

analyzeCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var path = parseResult.GetValue(pathArgument)!;
    var output = parseResult.GetValue(outputOption);
    var format = parseResult.GetValue(formatOption) ?? "json";
    var verbose = parseResult.GetValue(verboseOption);

    // Adjust logging level based on verbose flag
    IServiceProvider executionServiceProvider;
    if (verbose)
    {
        var verboseServices = new ServiceCollection();
        verboseServices.AddLogging(builder =>
        {
            builder
                .AddConsole(options =>
                {
                    options.FormatterName = "simple";
                })
                .SetMinimumLevel(LogLevel.Debug);
        });
        
        // CLI logging (for Program class)
        verboseServices.AddSingleton<ILogger<Program>>(sp => 
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<Program>());
        
        // Register all other services
        verboseServices.AddSingleton<IMetricsCalculator, MetricsCalculator>();
        verboseServices.AddSingleton<ICouplingAnalyzer, CouplingAnalyzer>();
        verboseServices.AddSingleton<ISolutionAnalyzer, SolutionAnalyzer>();
        verboseServices.AddSingleton<INuGetRestorer, NuGetRestorer>();
        verboseServices.AddSingleton<IMSBuildRegistrationService, MSBuildRegistrationService>();
        verboseServices.AddSingleton<IMSBuildWorkspaceFactory, MSBuildWorkspaceFactory>();
        verboseServices.AddSingleton<IFileSystemService, FileSystemService>();
        verboseServices.AddSingleton<IReportExporter, CompactReportExporter>();
        verboseServices.AddSingleton<IReportGenerator, HtmlReportGenerator>();
        
        executionServiceProvider = verboseServices.BuildServiceProvider();
    }
    else
    {
        executionServiceProvider = serviceProvider;
    }

    return await ExecuteAnalysisAsync(path, output, format, executionServiceProvider, cancellationToken);
});

static async Task<int> ExecuteAnalysisAsync(
    string path,
    string? output,
    string format,
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken)
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        ProgramLog.ApplicationStartup(logger, "0.1.0");
        ProgramLog.AnalyzingPath(logger, path);
        ProgramLog.CouplingModeEnabled(logger, "All");

        var analyzer = serviceProvider.GetRequiredService<ISolutionAnalyzer>();
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

        if (format == "summary")
        {
            PrintSummary(report, logger);
        }
        else if (format == "compact")
        {
            var exporter = serviceProvider.GetRequiredService<IReportExporter>();
            var compactReport = exporter.Export(report);
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(compactReport, jsonOptions);

            if (!string.IsNullOrEmpty(output))
            {
                await File.WriteAllTextAsync(output, json, cancellationToken);
                ProgramLog.CompactReportWritten(logger, output, json.Length);
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

            if (!string.IsNullOrEmpty(output))
            {
                await File.WriteAllTextAsync(output, html, cancellationToken);
                ProgramLog.HtmlReportWritten(logger, output, html.Length);
            }
            else
            {
                Console.WriteLine(html);
            }
        }
        else
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(report, jsonOptions);

            if (!string.IsNullOrEmpty(output))
            {
                await File.WriteAllTextAsync(output, json, cancellationToken);
                ProgramLog.ReportWritten(logger, output);
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


// Create root command
var rootCommand = new RootCommand("StructuraLens - C# code complexity analyzer");
rootCommand.Subcommands.Add(analyzeCommand);

rootCommand.SetAction(_ =>
{
    Console.WriteLine("StructuraLens v0.1.0");
    Console.WriteLine("Usage: structuralens <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  analyze <path>   Analyze a solution or project for code metrics");
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


    Console.WriteLine();

    foreach (var project in report.Projects)
    {
        Console.WriteLine($"Project: {project.Name}");
        Console.WriteLine($"  Types: {project.Types.Count}");
        Console.WriteLine($"  Total CC: {project.TotalCyclomaticComplexity}");
        Console.WriteLine($"  Total LOC: {project.TotalLinesOfExecutableCode}");
        Console.WriteLine($"  Max DIT: {project.MaxDepthOfInheritance}");

        var allMethods = project.Types.SelectMany(t => t.Methods).ToList();

        if (allMethods.Count > 0)
        {
            var avgMI = allMethods.Average(m => m.MaintainabilityIndex);
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

