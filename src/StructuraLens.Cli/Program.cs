using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using StructuraLens.Core.Analysis;
using StructuraLens.Core.Models;

// Create options
var outputOption = new Option<string?>("--out", "-o")
{
    Description = "Output file path for the JSON report"
};

var formatOption = new Option<string>("--format", "-f")
{
    Description = "Output format (json, summary)",
    DefaultValueFactory = _ => "json"
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

analyzeCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var path = parseResult.GetValue(pathArgument)!;
    var output = parseResult.GetValue(outputOption);
    var format = parseResult.GetValue(formatOption) ?? "json";

    try
    {
        Console.WriteLine("StructuraLens v0.1.0");
        Console.WriteLine($"Analyzing: {path}");
        Console.WriteLine();

        var analyzer = new SolutionAnalyzer();
        var report = path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? await analyzer.AnalyzeProjectAsync(path, cancellationToken)
            : await analyzer.AnalyzeSolutionAsync(path, cancellationToken);

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

rootCommand.SetAction(_ =>
{
    Console.WriteLine("StructuraLens v0.1.0");
    Console.WriteLine("Usage: structuralens analyze <path> [--out <file>] [--format json|summary]");
    Console.WriteLine();
    Console.WriteLine("Run this tool after building your solution for full analysis.");
    Console.WriteLine("If DLLs are not present, source-only analysis will be performed.");
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

