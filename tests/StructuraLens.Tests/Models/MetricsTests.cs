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
            LinesOfExecutableCode: 15,
            HalsteadVolume: 100.5,
            MaintainabilityIndex: 75.2);

        await Assert.That(metrics.FullName).IsEqualTo("TestClass.TestMethod()");
        await Assert.That(metrics.FilePath).IsEqualTo("/path/to/file.cs");
        await Assert.That(metrics.StartLine).IsEqualTo(10);
        await Assert.That(metrics.EndLine).IsEqualTo(20);
        await Assert.That(metrics.CyclomaticComplexity).IsEqualTo(5);
        await Assert.That(metrics.LinesOfExecutableCode).IsEqualTo(15);
        await Assert.That(metrics.HalsteadVolume).IsEqualTo(100.5);
        await Assert.That(metrics.MaintainabilityIndex).IsEqualTo(75.2);
    }

    [Test]
    public async Task TypeMetrics_TotalCyclomaticComplexity_SumsMethodComplexities()
    {
        var methods = new List<MethodMetrics>
        {
            new("Method1", "/file.cs", 1, 5, CyclomaticComplexity: 3, LinesOfExecutableCode: 10, HalsteadVolume: 50, MaintainabilityIndex: 80),
            new("Method2", "/file.cs", 6, 10, CyclomaticComplexity: 5, LinesOfExecutableCode: 8, HalsteadVolume: 60, MaintainabilityIndex: 70),
            new("Method3", "/file.cs", 11, 15, CyclomaticComplexity: 2, LinesOfExecutableCode: 5, HalsteadVolume: 30, MaintainabilityIndex: 90)
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
            new("Method1", "/file.cs", 1, 5, CyclomaticComplexity: 3, LinesOfExecutableCode: 10, HalsteadVolume: 50, MaintainabilityIndex: 80),
            new("Method2", "/file.cs", 6, 10, CyclomaticComplexity: 5, LinesOfExecutableCode: 8, HalsteadVolume: 60, MaintainabilityIndex: 70),
            new("Method3", "/file.cs", 11, 15, CyclomaticComplexity: 2, LinesOfExecutableCode: 5, HalsteadVolume: 30, MaintainabilityIndex: 90)
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
                new("Method1", "/file1.cs", 1, 5, 3, 10, 50, 80)
            }),
            new("Class2", "/file2.cs", 1, new List<MethodMetrics>
            {
                new("Method2", "/file2.cs", 1, 5, 7, 20, 100, 60)
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
            Warnings: [],
            ToolVersion: "test");

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
            Warnings: [],
            ToolVersion: "test");

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
                    new("M1", "/f1.cs", 1, 5, 1, 1, 10, 95),
                    new("M2", "/f1.cs", 6, 10, 1, 1, 10, 95)
                })
            }),
            new("Project2", "/p2.csproj", new List<TypeMetrics>
            {
                new("Class2", "/f2.cs", 0, new List<MethodMetrics>
                {
                    new("M3", "/f2.cs", 1, 5, 1, 1, 10, 95)
                })
            })
        };

        var report = new AnalysisReport(
            SolutionPath: "/solution.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: projects,
            Warnings: [],
            ToolVersion: "test");

        await Assert.That(report.TotalMethods).IsEqualTo(3);
    }

    [Test]
    public async Task TypeMetrics_Namespace_ExtractsCorrectly_WithFullyQualifiedName()
    {
        var typeMetrics = new TypeMetrics(
            FullName: "MyProject.Services.UserService",
            FilePath: "/file.cs",
            DepthOfInheritance: 0,
            Methods: []);

        await Assert.That(typeMetrics.Namespace).IsEqualTo("MyProject.Services");
    }

    [Test]
    public async Task TypeMetrics_Namespace_ReturnsGlobal_WhenNoNamespace()
    {
        var typeMetrics = new TypeMetrics(
            FullName: "SimpleClass",
            FilePath: "/file.cs",
            DepthOfInheritance: 0,
            Methods: []);

        await Assert.That(typeMetrics.Namespace).IsEqualTo("(global)");
    }

    [Test]
    public async Task TypeMetrics_Namespace_HandlesNestedTypes()
    {
        var typeMetrics = new TypeMetrics(
            FullName: "Outer.Inner.Nested",
            FilePath: "/file.cs",
            DepthOfInheritance: 0,
            Methods: []);

        await Assert.That(typeMetrics.Namespace).IsEqualTo("Outer.Inner");
    }

    [Test]
    public async Task NamespaceMetrics_TotalCyclomaticComplexity_AggregatesFromAllTypes()
    {
        var types = new List<TypeMetrics>
        {
            new("NS.Class1", "/f1.cs", 0, new List<MethodMetrics>
            {
                new("M1", "/f1.cs", 1, 5, CyclomaticComplexity: 3, LinesOfExecutableCode: 10, HalsteadVolume: 50, MaintainabilityIndex: 80)
            }),
            new("NS.Class2", "/f2.cs", 0, new List<MethodMetrics>
            {
                new("M2", "/f2.cs", 1, 5, CyclomaticComplexity: 5, LinesOfExecutableCode: 8, HalsteadVolume: 60, MaintainabilityIndex: 70)
            })
        };

        var namespaceMetrics = new NamespaceMetrics("NS", types);

        await Assert.That(namespaceMetrics.TotalCyclomaticComplexity).IsEqualTo(8);
    }

    [Test]
    public async Task NamespaceMetrics_TotalLinesOfExecutableCode_AggregatesFromAllTypes()
    {
        var types = new List<TypeMetrics>
        {
            new("NS.Class1", "/f1.cs", 0, new List<MethodMetrics>
            {
                new("M1", "/f1.cs", 1, 5, 1, LinesOfExecutableCode: 10, 50, 80)
            }),
            new("NS.Class2", "/f2.cs", 0, new List<MethodMetrics>
            {
                new("M2", "/f2.cs", 1, 5, 1, LinesOfExecutableCode: 15, 60, 70)
            })
        };

        var namespaceMetrics = new NamespaceMetrics("NS", types);

        await Assert.That(namespaceMetrics.TotalLinesOfExecutableCode).IsEqualTo(25);
    }

    [Test]
    public async Task NamespaceMetrics_TotalMethods_CountsAllMethodsInAllTypes()
    {
        var types = new List<TypeMetrics>
        {
            new("NS.Class1", "/f1.cs", 0, new List<MethodMetrics>
            {
                new("M1", "/f1.cs", 1, 5, 1, 10, 50, 80),
                new("M2", "/f1.cs", 6, 10, 1, 8, 40, 75)
            }),
            new("NS.Class2", "/f2.cs", 0, new List<MethodMetrics>
            {
                new("M3", "/f2.cs", 1, 5, 1, 15, 60, 70)
            })
        };

        var namespaceMetrics = new NamespaceMetrics("NS", types);

        await Assert.That(namespaceMetrics.TotalMethods).IsEqualTo(3);
    }

    [Test]
    public async Task NamespaceMetrics_MaxDepthOfInheritance_ReturnsMaxFromTypes()
    {
        var types = new List<TypeMetrics>
        {
            new("NS.Class1", "/f1.cs", DepthOfInheritance: 1, Methods: []),
            new("NS.Class2", "/f2.cs", DepthOfInheritance: 3, Methods: []),
            new("NS.Class3", "/f3.cs", DepthOfInheritance: 0, Methods: [])
        };

        var namespaceMetrics = new NamespaceMetrics("NS", types);

        await Assert.That(namespaceMetrics.MaxDepthOfInheritance).IsEqualTo(3);
    }

    [Test]
    public async Task NamespaceMetrics_AvgMaintainabilityIndex_CalculatesCorrectly()
    {
        var types = new List<TypeMetrics>
        {
            new("NS.Class1", "/f1.cs", 0, new List<MethodMetrics>
            {
                new("M1", "/f1.cs", 1, 5, 1, 10, 50, MaintainabilityIndex: 80.0),
                new("M2", "/f1.cs", 6, 10, 1, 10, 50, MaintainabilityIndex: 60.0)
            }),
            new("NS.Class2", "/f2.cs", 0, new List<MethodMetrics>
            {
                new("M3", "/f2.cs", 1, 5, 1, 10, 50, MaintainabilityIndex: 70.0)
            })
        };

        var namespaceMetrics = new NamespaceMetrics("NS", types);

        // (80 + 60 + 70) / 3 = 70
        await Assert.That(namespaceMetrics.AvgMaintainabilityIndex).IsEqualTo(70.0);
    }

    [Test]
    public async Task NamespaceMetrics_EmptyTypes_ReturnsZeros()
    {
        var namespaceMetrics = new NamespaceMetrics("EmptyNS", []);

        await Assert.That(namespaceMetrics.TotalCyclomaticComplexity).IsEqualTo(0);
        await Assert.That(namespaceMetrics.TotalLinesOfExecutableCode).IsEqualTo(0);
        await Assert.That(namespaceMetrics.TotalMethods).IsEqualTo(0);
        await Assert.That(namespaceMetrics.MaxDepthOfInheritance).IsEqualTo(0);
        await Assert.That(namespaceMetrics.AvgMaintainabilityIndex).IsEqualTo(0.0);
    }

    [Test]
    public async Task ProjectMetrics_GetNamespaceMetrics_GroupsTypesByNamespace()
    {
        var types = new List<TypeMetrics>
        {
            new("NS1.Class1", "/f1.cs", 0, []),
            new("NS1.Class2", "/f2.cs", 0, []),
            new("NS2.Class3", "/f3.cs", 0, []),
            new("NS2.Class4", "/f4.cs", 0, [])
        };

        var projectMetrics = new ProjectMetrics("TestProject", "/project.csproj", types);
        var namespaces = projectMetrics.GetNamespaceMetrics();

        await Assert.That(namespaces.Count).IsEqualTo(2);
        await Assert.That(namespaces[0].Name).IsEqualTo("NS1");
        await Assert.That(namespaces[0].Types.Count).IsEqualTo(2);
        await Assert.That(namespaces[1].Name).IsEqualTo("NS2");
        await Assert.That(namespaces[1].Types.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ProjectMetrics_GetNamespaceMetrics_SortsNamespacesAlphabetically()
    {
        var types = new List<TypeMetrics>
        {
            new("Zebra.Class1", "/f1.cs", 0, []),
            new("Alpha.Class2", "/f2.cs", 0, []),
            new("Beta.Class3", "/f3.cs", 0, [])
        };

        var projectMetrics = new ProjectMetrics("TestProject", "/project.csproj", types);
        var namespaces = projectMetrics.GetNamespaceMetrics();

        await Assert.That(namespaces.Count).IsEqualTo(3);
        await Assert.That(namespaces[0].Name).IsEqualTo("Alpha");
        await Assert.That(namespaces[1].Name).IsEqualTo("Beta");
        await Assert.That(namespaces[2].Name).IsEqualTo("Zebra");
    }

    [Test]
    public async Task ProjectMetrics_GetNamespaceMetrics_HandlesGlobalNamespace()
    {
        var types = new List<TypeMetrics>
        {
            new("SimpleClass", "/f1.cs", 0, []),
            new("NS.Class2", "/f2.cs", 0, [])
        };

        var projectMetrics = new ProjectMetrics("TestProject", "/project.csproj", types);
        var namespaces = projectMetrics.GetNamespaceMetrics();

        await Assert.That(namespaces.Count).IsEqualTo(2);
        await Assert.That(namespaces[0].Name).IsEqualTo("(global)");
        await Assert.That(namespaces[0].Types.Count).IsEqualTo(1);
        await Assert.That(namespaces[1].Name).IsEqualTo("NS");
        await Assert.That(namespaces[1].Types.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ProjectMetrics_GetNamespaceMetrics_AggregatesMetricsCorrectly()
    {
        var types = new List<TypeMetrics>
        {
            new("NS.Class1", "/f1.cs", DepthOfInheritance: 2, new List<MethodMetrics>
            {
                new("M1", "/f1.cs", 1, 5, CyclomaticComplexity: 3, LinesOfExecutableCode: 10, HalsteadVolume: 50, MaintainabilityIndex: 80)
            }),
            new("NS.Class2", "/f2.cs", DepthOfInheritance: 1, new List<MethodMetrics>
            {
                new("M2", "/f2.cs", 1, 5, CyclomaticComplexity: 5, LinesOfExecutableCode: 15, HalsteadVolume: 60, MaintainabilityIndex: 60)
            })
        };

        var projectMetrics = new ProjectMetrics("TestProject", "/project.csproj", types);
        var namespaces = projectMetrics.GetNamespaceMetrics();

        await Assert.That(namespaces.Count).IsEqualTo(1);
        var ns = namespaces[0];
        await Assert.That(ns.Name).IsEqualTo("NS");
        await Assert.That(ns.TotalCyclomaticComplexity).IsEqualTo(8); // 3 + 5
        await Assert.That(ns.TotalLinesOfExecutableCode).IsEqualTo(25); // 10 + 15
        await Assert.That(ns.TotalMethods).IsEqualTo(2);
        await Assert.That(ns.MaxDepthOfInheritance).IsEqualTo(2);
        await Assert.That(ns.AvgMaintainabilityIndex).IsEqualTo(70.0); // (80 + 60) / 2
    }
}
