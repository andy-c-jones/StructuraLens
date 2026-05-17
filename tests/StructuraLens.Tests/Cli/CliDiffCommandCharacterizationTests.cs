using System.Diagnostics;
using System.Text.Json;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Cli;

public sealed class CliDiffCommandCharacterizationTests
{
    [Test]
    public async Task DiffCommand_WithOutOption_WritesJsonToFile()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var basePath = Path.Combine(tempDirectory, "base.json");
            var headPath = Path.Combine(tempDirectory, "head.json");
            var outputPath = Path.Combine(tempDirectory, "diff.json");
            await WriteReportPairAsync(basePath, headPath);

            var result = await RunCliAsync(
                "diff",
                "--base", basePath,
                "--head", headPath,
                "--format", "json",
                "--out", outputPath);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(File.Exists(outputPath)).IsTrue();

            var outputJson = await File.ReadAllTextAsync(outputPath);
            await Assert.That(outputJson).Contains("\"totals\"");
            await Assert.That(outputJson).Contains("\"projectsDelta\": 0");
            await Assert.That(result.StdOut).DoesNotContain("\"totals\"");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task DiffCommand_WithoutOutOption_WritesJsonToStdout()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var basePath = Path.Combine(tempDirectory, "base.json");
            var headPath = Path.Combine(tempDirectory, "head.json");
            await WriteReportPairAsync(basePath, headPath);

            var result = await RunCliAsync(
                "diff",
                "--base", basePath,
                "--head", headPath,
                "--format", "json");

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(result.StdOut).Contains("\"totals\"");
            await Assert.That(result.StdOut).Contains("\"projectsDelta\": 0");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static async Task WriteReportPairAsync(string basePath, string headPath)
    {
        var baseReport = CreateReport(cyclomaticComplexity: 1, linesOfCode: 3);
        var headReport = CreateReport(cyclomaticComplexity: 2, linesOfCode: 4);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await File.WriteAllTextAsync(basePath, JsonSerializer.Serialize(baseReport, jsonOptions));
        await File.WriteAllTextAsync(headPath, JsonSerializer.Serialize(headReport, jsonOptions));
    }

    private static AnalysisReport CreateReport(int cyclomaticComplexity, int linesOfCode)
    {
        var method = new MethodMetrics(
            FullName: "Sample.Project.SampleType.SampleMethod()",
            FilePath: "SampleType.cs",
            StartLine: 1,
            EndLine: 5,
            CyclomaticComplexity: cyclomaticComplexity,
            LinesOfExecutableCode: linesOfCode,
            HalsteadVolume: 10,
            MaintainabilityIndex: 80);

        var type = new TypeMetrics(
            FullName: "Sample.Project.SampleType",
            FilePath: "SampleType.cs",
            DepthOfInheritance: 0,
            Methods: [method]);

        var project = new ProjectMetrics(
            Name: "Sample.Project",
            FilePath: "Sample.Project.csproj",
            Types: [type]);

        return new AnalysisReport(
            SolutionPath: "Sample.sln",
            AnalyzedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Projects: [project],
            Warnings: [],
            ToolVersion: "test");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"StructuraLensCliDiff_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task<CliResult> RunCliAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add(GetCliAssemblyPath());
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet process.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new CliResult(process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string GetCliAssemblyPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "StructuraLens.Cli.dll");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Could not find StructuraLens.Cli.dll in the test output directory.", path);
        }

        return path;
    }

    private sealed record CliResult(int ExitCode, string StdOut, string StdErr);
}
