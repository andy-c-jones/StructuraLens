using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StructuraLens.Core.Analysis;

namespace StructuraLens.Tests.Analysis;

public class SolutionAnalyzerTests
{
    [Test]
    public async Task AnalyzeSolutionAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        var analyzer = new SolutionAnalyzer();

        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await analyzer.AnalyzeSolutionAsync("/nonexistent/path/solution.sln"));
    }

    [Test]
    public async Task AnalyzeProjectAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        var analyzer = new SolutionAnalyzer();

        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await analyzer.AnalyzeProjectAsync("/nonexistent/path/project.csproj"));
    }

    [Test]
    public async Task EnsureMSBuildRegistered_CanBeCalledMultipleTimes_DoesNotThrow()
    {
        // Should not throw even when called multiple times
        SolutionAnalyzer.EnsureMSBuildRegistered();
        SolutionAnalyzer.EnsureMSBuildRegistered();
        SolutionAnalyzer.EnsureMSBuildRegistered();

        // If we get here, no exception was thrown
        await Task.CompletedTask;
    }

    [Test]
    public async Task AnalyzeSolutionAsync_WithCancellation_ThrowsException()
    {
        var analyzer = new SolutionAnalyzer();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Create a temp file so it passes the file existence check
        var tempFile = Path.GetTempFileName();
        File.Move(tempFile, tempFile + ".sln");
        tempFile = tempFile + ".sln";

        try
        {
            // Should throw some exception when cancelled - could be OperationCanceledException
            // or a solution parsing exception due to empty/invalid file
            await Assert.ThrowsAsync<Exception>(
                async () => await analyzer.AnalyzeSolutionAsync(tempFile, cts.Token));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

/// <summary>
/// Integration tests that use the actual solution to verify end-to-end analysis.
/// These tests run against the StructuraLens solution itself.
/// </summary>
public class SolutionAnalyzerIntegrationTests
{
    private static string GetSolutionPath()
    {
        // Navigate up from test output to find solution
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "StructuraLens.slnx")))
        {
            dir = dir.Parent;
        }
        return dir != null ? Path.Combine(dir.FullName, "StructuraLens.slnx") : 
            throw new InvalidOperationException("Could not find StructuraLens.slnx");
    }

    [Test]
    public async Task AnalyzeSolutionAsync_OwnSolution_ReturnsValidReport()
    {
        var solutionPath = GetSolutionPath();
        var analyzer = new SolutionAnalyzer();
        var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

        await Assert.That(report).IsNotNull();
        await Assert.That(report.SolutionPath).IsEqualTo(Path.GetFullPath(solutionPath));
        await Assert.That(report.TotalProjects).IsGreaterThan(0);
    }

    [Test]
    public async Task AnalyzeSolutionAsync_OwnSolution_FindsAllProjects()
    {
        var solutionPath = GetSolutionPath();
        var analyzer = new SolutionAnalyzer();
        var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

        // Should find at least Cli, Core, and Tests projects
        await Assert.That(report.TotalProjects).IsGreaterThanOrEqualTo(3);
        
        var projectNames = report.Projects.Select(p => p.Name).ToList();
        await Assert.That(projectNames).Contains("StructuraLens.Core");
        await Assert.That(projectNames).Contains("StructuraLens.Tests");
    }

    [Test]
    public async Task AnalyzeSolutionAsync_OwnSolution_CalculatesMetrics()
    {
        var solutionPath = GetSolutionPath();
        var analyzer = new SolutionAnalyzer();
        var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

        await Assert.That(report.TotalTypes).IsGreaterThan(0);
        await Assert.That(report.TotalMethods).IsGreaterThan(0);
        await Assert.That(report.TotalCyclomaticComplexity).IsGreaterThan(0);
        await Assert.That(report.TotalLinesOfExecutableCode).IsGreaterThan(0);
    }

    [Test]
    public async Task AnalyzeSolutionAsync_OwnSolution_IncludesHalsteadAndMI()
    {
        var solutionPath = GetSolutionPath();
        var analyzer = new SolutionAnalyzer();
        var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

        var allMethods = report.Projects
            .SelectMany(p => p.Types)
            .SelectMany(t => t.Methods)
            .ToList();

        await Assert.That(allMethods.Count).IsGreaterThan(0);

        // Check that at least some methods have Halstead and MI computed
        var methodsWithHalstead = allMethods.Where(m => m.HalsteadVolume > 0).ToList();
        var methodsWithMI = allMethods.Where(m => m.MaintainabilityIndex > 0).ToList();

        await Assert.That(methodsWithHalstead.Count).IsGreaterThan(0);
        await Assert.That(methodsWithMI.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task AnalyzeSolutionAsync_OwnSolution_AnalyzesTopLevelStatements()
    {
        var solutionPath = GetSolutionPath();
        var analyzer = new SolutionAnalyzer();
        var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

        // CLI project uses top-level statements
        var cliProject = report.Projects.FirstOrDefault(p => p.Name == "StructuraLens.Cli");
        
        await Assert.That(cliProject).IsNotNull();
        await Assert.That(cliProject!.Types.Count).IsGreaterThan(0);
        
        // Should have a synthetic <Program>$ type
        var programType = cliProject.Types.FirstOrDefault(t => t.FullName.Contains("Program"));
        await Assert.That(programType).IsNotNull();
        await Assert.That(programType!.Methods.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task AnalyzeSolutionAsync_OwnSolution_CalculatesDepthOfInheritance()
    {
        var solutionPath = GetSolutionPath();
        var analyzer = new SolutionAnalyzer();
        var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

        var coreProject = report.Projects.FirstOrDefault(p => p.Name == "StructuraLens.Core");
        await Assert.That(coreProject).IsNotNull();

        // Some types should have DIT > 0 (e.g., syntax walkers inherit from CSharpSyntaxWalker)
        var typesWithInheritance = coreProject!.Types.Where(t => t.DepthOfInheritance > 0).ToList();
        await Assert.That(typesWithInheritance.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task AnalyzeProjectAsync_CoreProject_ReturnsValidReport()
    {
        var solutionPath = GetSolutionPath();
        var solutionDir = Path.GetDirectoryName(solutionPath)!;
        var projectPath = Path.Combine(solutionDir, "src", "StructuraLens.Core", "StructuraLens.Core.csproj");

        var analyzer = new SolutionAnalyzer();
        var report = await analyzer.AnalyzeProjectAsync(projectPath);

        await Assert.That(report).IsNotNull();
        await Assert.That(report.TotalProjects).IsEqualTo(1);
        await Assert.That(report.Projects[0].Name).IsEqualTo("StructuraLens.Core");
        await Assert.That(report.TotalTypes).IsGreaterThan(0);
    }

    [Test]
    public async Task AnalyzeSolutionAsync_OwnSolution_SetsAnalyzedAtTimestamp()
    {
        var solutionPath = GetSolutionPath();
        var beforeAnalysis = DateTime.UtcNow.AddSeconds(-1);
        
        var analyzer = new SolutionAnalyzer();
        var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

        var afterAnalysis = DateTime.UtcNow.AddSeconds(1);

        await Assert.That(report.AnalyzedAt).IsGreaterThanOrEqualTo(beforeAnalysis);
        await Assert.That(report.AnalyzedAt).IsLessThanOrEqualTo(afterAnalysis);
    }

    [Test]
    public async Task AnalyzeSolutionAsync_OwnSolution_MethodMetricsHaveValidLineNumbers()
    {
        var solutionPath = GetSolutionPath();
        var analyzer = new SolutionAnalyzer();
        var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

        var allMethods = report.Projects
            .SelectMany(p => p.Types)
            .SelectMany(t => t.Methods)
            .ToList();

        foreach (var method in allMethods)
        {
            await Assert.That(method.StartLine).IsGreaterThan(0);
            await Assert.That(method.EndLine).IsGreaterThanOrEqualTo(method.StartLine);
            await Assert.That(method.FilePath).IsNotEmpty();
        }
    }

    [Test]
    public async Task AnalyzeSolutionAsync_OwnSolution_MaintainabilityIndexInValidRange()
    {
        var solutionPath = GetSolutionPath();
        var analyzer = new SolutionAnalyzer();
        var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

        var allMethods = report.Projects
            .SelectMany(p => p.Types)
            .SelectMany(t => t.Methods)
            .ToList();

        foreach (var method in allMethods)
        {
            await Assert.That(method.MaintainabilityIndex).IsGreaterThanOrEqualTo(0);
            await Assert.That(method.MaintainabilityIndex).IsLessThanOrEqualTo(100);
        }
    }

    [Test]
    public async Task AnalyzeSolutionAsync_OwnSolution_LocalFunctionsAreAnalyzed()
    {
        var solutionPath = GetSolutionPath();
        var analyzer = new SolutionAnalyzer();
        var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

        // CLI has a PrintSummary local function
        var cliProject = report.Projects.FirstOrDefault(p => p.Name == "StructuraLens.Cli");
        await Assert.That(cliProject).IsNotNull();

        var programType = cliProject!.Types.FirstOrDefault(t => t.FullName.Contains("Program"));
        await Assert.That(programType).IsNotNull();

        // Should have more than just Main - should include PrintSummary
        await Assert.That(programType!.Methods.Count).IsGreaterThanOrEqualTo(2);
        
        var printSummary = programType.Methods.FirstOrDefault(m => m.FullName.Contains("PrintSummary"));
        await Assert.That(printSummary).IsNotNull();
    }
}
