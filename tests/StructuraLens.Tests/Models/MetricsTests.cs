using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Models;

public class MetricsTests
{
    [Test]
    public async Task MethodMetrics_RecordCreation_StoresValuesCorrectly()
    {
        var metrics = new MethodMetrics(
            FullName: "TestClass.TestMethod()",
            FilePath: "/path/to/file.cs",
            StartLine: 10,
            EndLine: 20,
            CyclomaticComplexity: 5,
            LinesOfExecutableCode: 15);

        await Assert.That(metrics.FullName).IsEqualTo("TestClass.TestMethod()");
        await Assert.That(metrics.FilePath).IsEqualTo("/path/to/file.cs");
        await Assert.That(metrics.StartLine).IsEqualTo(10);
        await Assert.That(metrics.EndLine).IsEqualTo(20);
        await Assert.That(metrics.CyclomaticComplexity).IsEqualTo(5);
        await Assert.That(metrics.LinesOfExecutableCode).IsEqualTo(15);
    }

    [Test]
    public async Task TypeMetrics_TotalCyclomaticComplexity_SumsMethodComplexities()
    {
        var methods = new List<MethodMetrics>
        {
            new("Method1", "/file.cs", 1, 5, CyclomaticComplexity: 3, LinesOfExecutableCode: 10),
            new("Method2", "/file.cs", 6, 10, CyclomaticComplexity: 5, LinesOfExecutableCode: 8),
            new("Method3", "/file.cs", 11, 15, CyclomaticComplexity: 2, LinesOfExecutableCode: 5)
        };

        var typeMetrics = new TypeMetrics(
            FullName: "TestClass",
            FilePath: "/file.cs",
            DepthOfInheritance: 1,
            Methods: methods);

        await Assert.That(typeMetrics.TotalCyclomaticComplexity).IsEqualTo(10);
    }

    [Test]
    public async Task TypeMetrics_TotalLinesOfExecutableCode_SumsMethodLOC()
    {
        var methods = new List<MethodMetrics>
        {
            new("Method1", "/file.cs", 1, 5, CyclomaticComplexity: 3, LinesOfExecutableCode: 10),
            new("Method2", "/file.cs", 6, 10, CyclomaticComplexity: 5, LinesOfExecutableCode: 8),
            new("Method3", "/file.cs", 11, 15, CyclomaticComplexity: 2, LinesOfExecutableCode: 5)
        };

        var typeMetrics = new TypeMetrics(
            FullName: "TestClass",
            FilePath: "/file.cs",
            DepthOfInheritance: 1,
            Methods: methods);

        await Assert.That(typeMetrics.TotalLinesOfExecutableCode).IsEqualTo(23);
    }

    [Test]
    public async Task TypeMetrics_EmptyMethods_ReturnsZeroTotals()
    {
        var typeMetrics = new TypeMetrics(
            FullName: "EmptyClass",
            FilePath: "/file.cs",
            DepthOfInheritance: 0,
            Methods: []);

        await Assert.That(typeMetrics.TotalCyclomaticComplexity).IsEqualTo(0);
        await Assert.That(typeMetrics.TotalLinesOfExecutableCode).IsEqualTo(0);
    }

    [Test]
    public async Task ProjectMetrics_TotalCyclomaticComplexity_SumsTypeComplexities()
    {
        var types = new List<TypeMetrics>
        {
            new("Class1", "/file1.cs", 0, new List<MethodMetrics>
            {
                new("Method1", "/file1.cs", 1, 5, 3, 10)
            }),
            new("Class2", "/file2.cs", 1, new List<MethodMetrics>
            {
                new("Method2", "/file2.cs", 1, 5, 7, 20)
            })
        };

        var projectMetrics = new ProjectMetrics(
            Name: "TestProject",
            FilePath: "/project.csproj",
            Types: types);

        await Assert.That(projectMetrics.TotalCyclomaticComplexity).IsEqualTo(10);
    }

    [Test]
    public async Task ProjectMetrics_MaxDepthOfInheritance_ReturnsMaxDIT()
    {
        var types = new List<TypeMetrics>
        {
            new("Class1", "/file1.cs", DepthOfInheritance: 0, Methods: []),
            new("Class2", "/file2.cs", DepthOfInheritance: 3, Methods: []),
            new("Class3", "/file3.cs", DepthOfInheritance: 1, Methods: [])
        };

        var projectMetrics = new ProjectMetrics(
            Name: "TestProject",
            FilePath: "/project.csproj",
            Types: types);

        await Assert.That(projectMetrics.MaxDepthOfInheritance).IsEqualTo(3);
    }

    [Test]
    public async Task ProjectMetrics_EmptyTypes_ReturnsZeroMaxDIT()
    {
        var projectMetrics = new ProjectMetrics(
            Name: "EmptyProject",
            FilePath: "/project.csproj",
            Types: []);

        await Assert.That(projectMetrics.MaxDepthOfInheritance).IsEqualTo(0);
    }

    [Test]
    public async Task AnalysisReport_TotalProjects_ReturnsProjectCount()
    {
        var projects = new List<ProjectMetrics>
        {
            new("Project1", "/p1.csproj", []),
            new("Project2", "/p2.csproj", []),
            new("Project3", "/p3.csproj", [])
        };

        var report = new AnalysisReport(
            SolutionPath: "/solution.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: projects,
            Warnings: []);

        await Assert.That(report.TotalProjects).IsEqualTo(3);
    }

    [Test]
    public async Task AnalysisReport_TotalTypes_SumsTypesAcrossProjects()
    {
        var projects = new List<ProjectMetrics>
        {
            new("Project1", "/p1.csproj", new List<TypeMetrics>
            {
                new("Class1", "/f1.cs", 0, []),
                new("Class2", "/f2.cs", 0, [])
            }),
            new("Project2", "/p2.csproj", new List<TypeMetrics>
            {
                new("Class3", "/f3.cs", 0, [])
            })
        };

        var report = new AnalysisReport(
            SolutionPath: "/solution.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: projects,
            Warnings: []);

        await Assert.That(report.TotalTypes).IsEqualTo(3);
    }

    [Test]
    public async Task AnalysisReport_TotalMethods_SumsMethodsAcrossAllTypes()
    {
        var projects = new List<ProjectMetrics>
        {
            new("Project1", "/p1.csproj", new List<TypeMetrics>
            {
                new("Class1", "/f1.cs", 0, new List<MethodMetrics>
                {
                    new("M1", "/f1.cs", 1, 5, 1, 1),
                    new("M2", "/f1.cs", 6, 10, 1, 1)
                })
            }),
            new("Project2", "/p2.csproj", new List<TypeMetrics>
            {
                new("Class2", "/f2.cs", 0, new List<MethodMetrics>
                {
                    new("M3", "/f2.cs", 1, 5, 1, 1)
                })
            })
        };

        var report = new AnalysisReport(
            SolutionPath: "/solution.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: projects,
            Warnings: []);

        await Assert.That(report.TotalMethods).IsEqualTo(3);
    }
}
