using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StructuraLens.Core.Analysis;
using StructuraLens.Core.Configuration;
using StructuraLens.Core.Export;
using StructuraLens.Core.Models;

// Configure logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole(options =>
        {
            options.FormatterName = "simple";
        })
        .SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger("StructuraLens");

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

var couplingModeOption = new Option<string?>("--coupling-mode", "-c")
{
    Description = "Coupling analysis mode: internal (own code only), filtered (exclude System/Microsoft), all (everything)"
};

var configOption = new Option<string?>("--config")
{
    Description = "Path to structuralens.json configuration file"
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
analyzeCommand.Options.Add(couplingModeOption);
analyzeCommand.Options.Add(configOption);
analyzeCommand.Options.Add(verboseOption);

analyzeCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var path = parseResult.GetValue(pathArgument)!;
    var output = parseResult.GetValue(outputOption);
    var format = parseResult.GetValue(formatOption) ?? "json";
    var couplingMode = parseResult.GetValue(couplingModeOption);
    var configPath = parseResult.GetValue(configOption);
    var verbose = parseResult.GetValue(verboseOption);

    // Adjust logging level based on verbose flag
    if (verbose)
    {
        loggerFactory.Dispose();
        using var verboseLoggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole(options =>
                {
                    options.FormatterName = "simple";
                })
                .SetMinimumLevel(LogLevel.Debug);
        });
        var verboseLogger = verboseLoggerFactory.CreateLogger("StructuraLens");
        return await ExecuteAnalysisAsync(path, output, format, couplingMode, configPath, verboseLogger, cancellationToken);
    }

    return await ExecuteAnalysisAsync(path, output, format, couplingMode, configPath, logger, cancellationToken);
});

static async Task<int> ExecuteAnalysisAsync(
    string path,
    string? output,
    string format,
    string? couplingMode,
    string? configPath,
    ILogger logger,
    CancellationToken cancellationToken)
{
    try
    {
        Console.WriteLine("StructuraLens v0.1.0");
        Console.WriteLine($"Analyzing: {path}");
        Console.WriteLine();

        // Build configuration: explicit path > auto-discovery > defaults
        StructuraLensConfig config;
        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
            config = System.Text.Json.JsonSerializer.Deserialize<StructuraLensConfig>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            }) ?? ConfigurationLoader.CreateDefaultConfig();
            Console.WriteLine($"Using config: {configPath}");
        }
        else
        {
            // Auto-discover configuration from solution/project directory
            config = await ConfigurationLoader.LoadSolutionConfigAsync(path, cancellationToken);
            if (config.Rules.Count > 0 || config.Coupling.Mode != CouplingMode.Filtered)
            {
                Console.WriteLine("Using auto-discovered configuration");
            }
        }

        // Override coupling mode from CLI if explicitly specified
        if (!string.IsNullOrEmpty(couplingMode))
        {
            config.Coupling.Mode = couplingMode.ToLowerInvariant() switch
            {
                "internal" => CouplingMode.Internal,
                "all" => CouplingMode.All,
                _ => CouplingMode.Filtered
            };
        }

        Console.WriteLine($"Coupling mode: {config.Coupling.Mode}");
        if (config.Rules.Count > 0)
        {
            Console.WriteLine($"Architecture rules: {config.Rules.Count}");
        }
        Console.WriteLine();

        var analyzer = new SolutionAnalyzer(logger);
        var report = path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? await analyzer.AnalyzeProjectAsync(path, config, cancellationToken)
            : await analyzer.AnalyzeSolutionAsync(path, config, cancellationToken);

        if (report.Warnings.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            foreach (var warning in report.Warnings.Take(10))
            {
                Console.WriteLine($"Warning: {warning}");
            }
            if (report.Warnings.Count > 10)
            {
                Console.WriteLine($"... and {report.Warnings.Count - 10} more warnings");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        if (format == "summary")
        {
            PrintSummary(report);
        }
        else if (format == "compact")
        {
            var compactReport = CompactReportExporter.Export(report);
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(compactReport, jsonOptions);

            if (!string.IsNullOrEmpty(output))
            {
                await File.WriteAllTextAsync(output, json, cancellationToken);
                Console.WriteLine($"Compact report written to: {output} ({json.Length:N0} bytes)");
            }
            else
            {
                Console.WriteLine(json);
            }
        }
        else if (format == "html")
        {
            var html = HtmlReportGenerator.Generate(report);

            if (!string.IsNullOrEmpty(output))
            {
                await File.WriteAllTextAsync(output, html, cancellationToken);
                Console.WriteLine($"HTML report written to: {output} ({html.Length:N0} bytes)");
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
                Console.WriteLine($"Report written to: {output}");
            }
            else
            {
                Console.WriteLine(json);
            }
        }

        // Return non-zero exit code if there are linting errors
        if (report.LintingResults != null && report.LintingResults.ErrorCount > 0)
        {
            return 1;
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
        return 1;
    }
}

// Create init subcommand
var initPathArgument = new Argument<string?>("path")
{
    Description = "Directory to create structuralens.json in (default: current directory)",
    Arity = ArgumentArity.ZeroOrOne
};

var initCommand = new Command("init", "Create a default structuralens.json configuration file");
initCommand.Arguments.Add(initPathArgument);

initCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var path = parseResult.GetValue(initPathArgument) ?? Directory.GetCurrentDirectory();
    var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
    var configPath = Path.Combine(directory, "structuralens.json");

    if (File.Exists(configPath))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Configuration file already exists: {configPath}");
        Console.ResetColor();
        return 1;
    }

    try
    {
        await ConfigurationLoader.CreateDefaultConfigAsync(directory, cancellationToken);
        Console.WriteLine($"Created: {configPath}");
        Console.WriteLine();
        Console.WriteLine("Edit this file to configure:");
        Console.WriteLine("  - Coupling analysis mode and filters");
        Console.WriteLine("  - Architecture linting rules");
        Console.WriteLine("  - Output preferences");
        Console.WriteLine();
        Console.WriteLine("See: https://github.com/your-org/structuralens/blob/main/docs/configuration.md");
        return 0;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
        return 1;
    }
});

// Create root command
var rootCommand = new RootCommand("StructuraLens - C# code complexity analyzer");
rootCommand.Subcommands.Add(analyzeCommand);
rootCommand.Subcommands.Add(initCommand);

rootCommand.SetAction(_ =>
{
    Console.WriteLine("StructuraLens v0.1.0");
    Console.WriteLine("Usage: structuralens <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  analyze <path>   Analyze a solution or project for code metrics");
    Console.WriteLine("  init [path]      Create a default structuralens.json configuration file");
    Console.WriteLine();
    Console.WriteLine("Run 'structuralens <command> --help' for more information on a command.");
    return 0;
});

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

static void PrintSummary(AnalysisReport report)
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

    // Print linting results
    if (report.LintingResults != null)
    {
        Console.WriteLine();
        Console.WriteLine("=== Architecture Linting ===");
        Console.WriteLine($"Rules Evaluated: {report.LintingResults.RulesEvaluated}");
        Console.WriteLine($"Errors: {report.LintingResults.ErrorCount}");
        Console.WriteLine($"Warnings: {report.LintingResults.WarningCount}");
        Console.WriteLine($"Info: {report.LintingResults.InfoCount}");
        Console.WriteLine($"Status: {(report.LintingResults.Passed ? "PASSED" : "FAILED")}");

        if (report.LintingResults.Violations.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Violations:");
            foreach (var violation in report.LintingResults.Violations.Take(20))
            {
                var color = violation.Severity switch
                {
                    StructuraLens.Core.Configuration.RuleSeverity.Error => ConsoleColor.Red,
                    StructuraLens.Core.Configuration.RuleSeverity.Warning => ConsoleColor.Yellow,
                    _ => ConsoleColor.Cyan
                };
                Console.ForegroundColor = color;
                var severityLabel = violation.Severity.ToString().ToUpper();
                Console.Write($"  [{severityLabel}] ");
                Console.ResetColor();
                Console.WriteLine($"{violation.RuleId}: {violation.Message}");
                if (violation.FromEntity != null && violation.ToEntity != null)
                {
                    Console.WriteLine($"         {violation.FromEntity} → {violation.ToEntity}");
                }
            }

            if (report.LintingResults.Violations.Count > 20)
            {
                Console.WriteLine($"  ... and {report.LintingResults.Violations.Count - 20} more violations");
            }
        }
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

