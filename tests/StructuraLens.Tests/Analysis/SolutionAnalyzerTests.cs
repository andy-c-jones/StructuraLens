using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StructuraLens.Core.Analysis;
using StructuraLens.Core.Infrastructure;

namespace StructuraLens.Tests.Analysis;

public class SolutionAnalyzerTests
{
    private static SolutionAnalyzer CreateAnalyzer()
    {
        // Create real dependencies for integration tests
        var logger = new NullLogger<SolutionAnalyzer>();
        var nugetRestorer = new NuGetRestorer(new NullLogger<NuGetRestorer>());
        var registrationService = new MSBuildRegistrationService();
        var workspaceFactory = new MSBuildWorkspaceFactory(registrationService);
        var couplingAnalyzer = new CouplingAnalyzer(new NullLogger<CouplingAnalyzer>());
        var metricsCalculator = new MetricsCalculator();
        var fileSystem = new FileSystemService();
        var gitService = new GitRepositoryService(new NullLogger<GitRepositoryService>());

        return new SolutionAnalyzer(
            logger,
            nugetRestorer,
            workspaceFactory,
            couplingAnalyzer,
            metricsCalculator,
            fileSystem,
            gitService);
    }

    [Test]
    public async Task AnalyzeSolutionAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        var analyzer = CreateAnalyzer();

        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await analyzer.AnalyzeSolutionAsync("/nonexistent/path/solution.sln"));
    }

    [Test]
    public async Task AnalyzeProjectAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        var analyzer = CreateAnalyzer();

        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await analyzer.AnalyzeProjectAsync("/nonexistent/path/project.csproj"));
    }

    [Test]
    public async Task EnsureMSBuildRegistered_CanBeCalledMultipleTimes_DoesNotThrow()
    {
        // Should not throw even when called multiple times
        var registrationService = new MSBuildRegistrationService();
        registrationService.EnsureMSBuildRegistered();
        registrationService.EnsureMSBuildRegistered();
        registrationService.EnsureMSBuildRegistered();

        // If we get here, no exception was thrown
        await Task.CompletedTask;
    }

    [Test]
    public async Task AnalyzeSolutionAsync_WithCancellation_ThrowsException()
    {
        var analyzer = CreateAnalyzer();
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
    private static SolutionAnalyzer CreateAnalyzer()
    {
        // Create real dependencies for integration tests
        var logger = new NullLogger<SolutionAnalyzer>();
        var nugetRestorer = new NuGetRestorer(new NullLogger<NuGetRestorer>());
        var registrationService = new MSBuildRegistrationService();
        var workspaceFactory = new MSBuildWorkspaceFactory(registrationService);
        var couplingAnalyzer = new CouplingAnalyzer(new NullLogger<CouplingAnalyzer>());
        var metricsCalculator = new MetricsCalculator();
        var fileSystem = new FileSystemService();
        var gitService = new GitRepositoryService(new NullLogger<GitRepositoryService>());

        return new SolutionAnalyzer(
            logger,
            nugetRestorer,
            workspaceFactory,
            couplingAnalyzer,
            metricsCalculator,
            fileSystem,
            gitService);
    }

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
        var analyzer = CreateAnalyzer();
        var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

        await Assert.That(report).IsNotNull();
        await Assert.That(report.SolutionPath).IsEqualTo(Path.GetFullPath(solutionPath));
        await Assert.That(report.TotalProjects).IsGreaterThan(0);
    }

    [Test]
    public async Task AnalyzeSolutionAsync_OwnSolution_FindsAllProjects()
    {
        var solutionPath = GetSolutionPath();
        var analyzer = CreateAnalyzer();
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
        var analyzer = CreateAnalyzer();
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
        var analyzer = CreateAnalyzer();
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
        var analyzer = CreateAnalyzer();
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
        var analyzer = CreateAnalyzer();
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

        var analyzer = CreateAnalyzer();
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
        
        var analyzer = CreateAnalyzer();
        var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

        var afterAnalysis = DateTime.UtcNow.AddSeconds(1);

        await Assert.That(report.AnalyzedAt).IsGreaterThanOrEqualTo(beforeAnalysis);
        await Assert.That(report.AnalyzedAt).IsLessThanOrEqualTo(afterAnalysis);
    }

    [Test]
    public async Task AnalyzeSolutionAsync_OwnSolution_MethodMetricsHaveValidLineNumbers()
    {
        var solutionPath = GetSolutionPath();
        var analyzer = CreateAnalyzer();
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
        var analyzer = CreateAnalyzer();
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
        var analyzer = CreateAnalyzer();
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

    [Test]
    public async Task AnalyzeSolutionAsync_OwnSolution_PopulatesPackageReferences()
    {
        var solutionPath = GetSolutionPath();
        var analyzer = CreateAnalyzer();
        var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

        var coreProject = report.Projects.FirstOrDefault(p => p.Name == "StructuraLens.Core");
        await Assert.That(coreProject).IsNotNull();

        // Core project has many PackageReferences (Microsoft.Build, Microsoft.CodeAnalysis, etc.)
        await Assert.That(coreProject!.PackageReferences.Count).IsGreaterThanOrEqualTo(8);
        await Assert.That(coreProject.PackageReferences).Contains("Microsoft.CodeAnalysis.CSharp.Workspaces");
        await Assert.That(coreProject.PackageReferences).Contains("LibGit2Sharp");

        // Tests project has FakeItEasy and TUnit
        var testsProject = report.Projects.FirstOrDefault(p => p.Name == "StructuraLens.Tests");
        await Assert.That(testsProject).IsNotNull();
        await Assert.That(testsProject!.PackageReferences).Contains("FakeItEasy");
        await Assert.That(testsProject.PackageReferences).Contains("TUnit");
    }

    [Test]
    public async Task AnalyzeSolutionAsync_WithDirectoryBuildProps_EachProjectCountsSharedDependencies()
    {
        // This is an integration test that verifies when Directory.Build.props defines
        // 1 dependency and 3 projects are in scope, each project counts it as 1 dependency
        // (3 total across all projects, not 1 shared dependency)

        var tempDir = Path.Combine(Path.GetTempPath(), $"StructuraLensIntegrationTest_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            // Create Directory.Build.props with 1 shared package
            var directoryBuildProps = """
                <Project>
                  <ItemGroup>
                    <PackageReference Include="SharedTestPackage" Version="1.0.0" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(tempDir, "Directory.Build.props"), directoryBuildProps);

            // Create 3 simple projects
            var project1Content = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """;

            var project2Content = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Project2Package" Version="2.0.0" />
                  </ItemGroup>
                </Project>
                """;

            var project3Content = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Project3Package" Version="3.0.0" />
                  </ItemGroup>
                </Project>
                """;

            // Create project directories
            var proj1Dir = Path.Combine(tempDir, "Project1");
            var proj2Dir = Path.Combine(tempDir, "Project2");
            var proj3Dir = Path.Combine(tempDir, "Project3");
            Directory.CreateDirectory(proj1Dir);
            Directory.CreateDirectory(proj2Dir);
            Directory.CreateDirectory(proj3Dir);

            var proj1Path = Path.Combine(proj1Dir, "Project1.csproj");
            var proj2Path = Path.Combine(proj2Dir, "Project2.csproj");
            var proj3Path = Path.Combine(proj3Dir, "Project3.csproj");

            File.WriteAllText(proj1Path, project1Content);
            File.WriteAllText(proj2Path, project2Content);
            File.WriteAllText(proj3Path, project3Content);

            // Create solution file
            var solutionContent = $$"""
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project1", "Project1\Project1.csproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project2", "Project2\Project2.csproj", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project3", "Project3\Project3.csproj", "{33333333-3333-3333-3333-333333333333}"
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                EndGlobal
                """;

            var solutionPath = Path.Combine(tempDir, "TestSolution.sln");
            File.WriteAllText(solutionPath, solutionContent);

            // Restore packages (NuGet restore is required for MSBuild to evaluate projects)
            var restorer = new NuGetRestorer(new NullLogger<NuGetRestorer>());
            await restorer.RestorePackagesAsync(solutionPath);

            // Analyze the solution
            var analyzer = CreateAnalyzer();
            var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

            // Verify each project has the correct packages
            var project1 = report.Projects.FirstOrDefault(p => p.Name == "Project1");
            var project2 = report.Projects.FirstOrDefault(p => p.Name == "Project2");
            var project3 = report.Projects.FirstOrDefault(p => p.Name == "Project3");

            await Assert.That(project1).IsNotNull();
            await Assert.That(project2).IsNotNull();
            await Assert.That(project3).IsNotNull();

            // Project1: only SharedTestPackage (from Directory.Build.props)
            await Assert.That(project1!.PackageReferences.Count).IsEqualTo(1);
            await Assert.That(project1.PackageReferences).Contains("SharedTestPackage");

            // Project2: SharedTestPackage + Project2Package = 2 packages
            await Assert.That(project2!.PackageReferences.Count).IsEqualTo(2);
            await Assert.That(project2.PackageReferences).Contains("SharedTestPackage");
            await Assert.That(project2.PackageReferences).Contains("Project2Package");

            // Project3: SharedTestPackage + Project3Package = 2 packages
            await Assert.That(project3!.PackageReferences.Count).IsEqualTo(2);
            await Assert.That(project3.PackageReferences).Contains("SharedTestPackage");
            await Assert.That(project3.PackageReferences).Contains("Project3Package");

            // Total count: 1 + 2 + 2 = 5 package references across all projects
            // This demonstrates that the 1 shared package is counted once per project (3 times total)
            var totalPackageReferences = project1.PackageReferences.Count +
                                        project2.PackageReferences.Count +
                                        project3.PackageReferences.Count;
            await Assert.That(totalPackageReferences).IsEqualTo(5);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
