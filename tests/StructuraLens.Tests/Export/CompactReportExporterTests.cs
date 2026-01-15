using StructuraLens.Core.Export;
using StructuraLens.Core.Models;
using TUnit.Core;

namespace StructuraLens.Tests.Export;

public class CompactReportExporterTests
{
    private CompactReportExporter CreateExporter()
    {
        return new CompactReportExporter();
    }

    private AnalysisReport CreateMinimalReport()
    {
        var method = new MethodMetrics(
            FullName: "TestProject.TestClass.TestMethod()",
            FilePath: "test.cs",
            StartLine: 10,
            EndLine: 20,
            CyclomaticComplexity: 2,
            LinesOfExecutableCode: 10,
            HalsteadVolume: 25.5,
            MaintainabilityIndex: 75.5);

        var type = new TypeMetrics(
            FullName: "TestProject.TestClass",
            FilePath: "test.cs",
            DepthOfInheritance: 1,
            Methods: new List<MethodMetrics> { method });

        var project = new ProjectMetrics(
            Name: "TestProject",
            FilePath: "TestProject.csproj",
            Types: new List<TypeMetrics> { type });

        return new AnalysisReport(
            SolutionPath: "test.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: new List<ProjectMetrics> { project },
            Warnings: new List<string>());
    }

    [Test]
    public async Task Export_WithBasicReport_ReturnsCompactReport()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport();

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        await Assert.That(compactReport).IsNotNull();
        await Assert.That(compactReport.Version).IsEqualTo(1);
        await Assert.That(compactReport.Path).IsEqualTo("test.sln");
        await Assert.That(compactReport.Projects.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Export_WithoutTypeDetails_ExcludesTypes()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport();

        // Act
        var compactReport = exporter.Export(report, includeMethodDetails: false, includeTypeDetails: false);

        // Assert
        await Assert.That(compactReport).IsNotNull();
        await Assert.That(compactReport.Projects[0].Types).IsNull();
    }

    [Test]
    public async Task Export_WithTypeDetails_IncludesTypes()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport();

        // Act
        var compactReport = exporter.Export(report, includeMethodDetails: false, includeTypeDetails: true);

        // Assert
        await Assert.That(compactReport).IsNotNull();
        await Assert.That(compactReport.Projects[0].Types).IsNotNull();
        await Assert.That(compactReport.Projects[0].Types!.Count).IsEqualTo(1);
        await Assert.That(compactReport.Projects[0].Types![0].Name).IsEqualTo("TestClass");
    }

    [Test]
    public async Task Export_WithMethodDetails_IncludesMethods()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport();

        // Act
        var compactReport = exporter.Export(report, includeMethodDetails: true, includeTypeDetails: true);

        // Assert
        await Assert.That(compactReport).IsNotNull();
        await Assert.That(compactReport.Projects[0].Types).IsNotNull();
        await Assert.That(compactReport.Projects[0].Types![0].Methods).IsNotNull();
        await Assert.That(compactReport.Projects[0].Types![0].Methods!.Count).IsEqualTo(1);
        
        var method = compactReport.Projects[0].Types![0].Methods![0];
        await Assert.That(method.Name).IsEqualTo("TestMethod()");
        await Assert.That(method.CyclomaticComplexity).IsEqualTo(2);
        await Assert.That(method.LinesOfCode).IsEqualTo(10);
        await Assert.That(method.StartLine).IsEqualTo(10);
        await Assert.That(method.EndLine).IsEqualTo(20);
    }

    [Test]
    public async Task Export_WithoutMethodDetails_ExcludesMethods()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport();

        // Act
        var compactReport = exporter.Export(report, includeMethodDetails: false, includeTypeDetails: true);

        // Assert
        await Assert.That(compactReport).IsNotNull();
        await Assert.That(compactReport.Projects[0].Types).IsNotNull();
        await Assert.That(compactReport.Projects[0].Types![0].Methods).IsNull();
    }

    [Test]
    public async Task Export_ProjectMetrics_AreCorrectlyMapped()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport();

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        var project = compactReport.Projects[0];
        await Assert.That(project.Name).IsEqualTo("TestProject");
        await Assert.That(project.TypeCount).IsEqualTo(1);
        await Assert.That(project.MethodCount).IsEqualTo(1);
        await Assert.That(project.CyclomaticComplexity).IsEqualTo(2);
        await Assert.That(project.LinesOfCode).IsEqualTo(10);
        await Assert.That(project.MaxDepthOfInheritance).IsEqualTo(1);
    }

    [Test]
    public async Task Export_WithCouplingAnalysis_IncludesCouplingMetrics()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport() with
        {
            CouplingAnalysis = new CouplingAnalysis("test.sln", DateTime.UtcNow)
            {
                ProjectCoupling = new List<CouplingMetrics>
                {
                    new CouplingMetrics("TestProject", DependencyType.ProjectReference)
                    {
                        OutboundDependencies = new List<DependencyEdge>
                        {
                            new DependencyEdge("TestProject", "OtherProject", DependencyType.ProjectReference, 1)
                        },
                        InboundDependencies = new List<DependencyEdge>()
                    }
                },
                NamespaceCoupling = new List<CouplingMetrics>(),
                TypeCoupling = new List<CouplingMetrics>(),
                AllDependencies = new List<DependencyEdge>(),
                Summary = new CouplingSummary
                {
                    TotalDependencies = 1,
                    AverageEfferentCoupling = 1.0,
                    AverageAfferentCoupling = 0.0,
                    AverageInstability = 1.0
                }
            }
        };

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        var project = compactReport.Projects[0];
        await Assert.That(project.EfferentCoupling).IsEqualTo(1);
        await Assert.That(project.AfferentCoupling).IsEqualTo(0);
        await Assert.That(project.Instability).IsEqualTo(1.0);
    }

    [Test]
    public async Task Export_WithDiagnostics_IncludesDiagnosticsCounts()
    {
        // Arrange
        var exporter = CreateExporter();
        var project = new ProjectMetrics(
            Name: "TestProject",
            FilePath: "TestProject.csproj",
            Types: new List<TypeMetrics>())
        {
            Diagnostics = new DiagnosticSummary
            {
                ErrorCount = 2,
                WarningCount = 5,
                InfoCount = 10,
                Diagnostics = new List<DiagnosticInfo>
                {
                    new DiagnosticInfo(
                        Id: "CS0001",
                        Message: "Test error 1",
                        Severity: DiagnosticLevel.Error,
                        FilePath: "test.cs",
                        Line: 10,
                        Column: 5),
                    new DiagnosticInfo(
                        Id: "CS0002",
                        Message: "Test error 2",
                        Severity: DiagnosticLevel.Error,
                        FilePath: "test.cs",
                        Line: 20,
                        Column: 5),
                    new DiagnosticInfo(
                        Id: "CS1001",
                        Message: "Test warning 1",
                        Severity: DiagnosticLevel.Warning,
                        FilePath: "test.cs",
                        Line: 30,
                        Column: 5),
                    new DiagnosticInfo(
                        Id: "CS1002",
                        Message: "Test warning 2",
                        Severity: DiagnosticLevel.Warning,
                        FilePath: "test.cs",
                        Line: 40,
                        Column: 5),
                    new DiagnosticInfo(
                        Id: "CS1003",
                        Message: "Test warning 3",
                        Severity: DiagnosticLevel.Warning,
                        FilePath: "test.cs",
                        Line: 50,
                        Column: 5),
                    new DiagnosticInfo(
                        Id: "CS1004",
                        Message: "Test warning 4",
                        Severity: DiagnosticLevel.Warning,
                        FilePath: "test.cs",
                        Line: 60,
                        Column: 5),
                    new DiagnosticInfo(
                        Id: "CS1005",
                        Message: "Test warning 5",
                        Severity: DiagnosticLevel.Warning,
                        FilePath: "test.cs",
                        Line: 70,
                        Column: 5)
                }
            }
        };

        var report = new AnalysisReport(
            SolutionPath: "test.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: new List<ProjectMetrics> { project },
            Warnings: new List<string>());

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        var compactProject = compactReport.Projects[0];
        await Assert.That(compactProject.Errors).IsEqualTo(2);
        await Assert.That(compactProject.Warnings).IsEqualTo(5);
        
        await Assert.That(compactReport.Diagnostics).IsNotNull();
        await Assert.That(compactReport.Diagnostics!.Errors).IsEqualTo(2);
        await Assert.That(compactReport.Diagnostics!.Warnings).IsEqualTo(5);
        await Assert.That(compactReport.Diagnostics!.Items.Count).IsEqualTo(7); // 2 errors + 5 warnings
    }

    [Test]
    public async Task Export_WithoutDiagnostics_DiagnosticsIsNull()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport();

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        await Assert.That(compactReport.Diagnostics).IsNull();
    }

    [Test]
    public async Task Export_WithCouplingGraph_BuildsGraphStructure()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport() with
        {
            CouplingAnalysis = new CouplingAnalysis("test.sln", DateTime.UtcNow)
            {
                ProjectCoupling = new List<CouplingMetrics>(),
                NamespaceCoupling = new List<CouplingMetrics>(),
                TypeCoupling = new List<CouplingMetrics>(),
                AllDependencies = new List<DependencyEdge>
                {
                    new DependencyEdge("TestProject", "TestProject", DependencyType.ProjectReference, 1)
                },
                Summary = new CouplingSummary { TotalDependencies = 1 }
            }
        };

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        await Assert.That(compactReport.Graph).IsNotNull();
        await Assert.That(compactReport.Graph.Projects).IsNotNull();
        await Assert.That(compactReport.Graph.Namespaces).IsNotNull();
    }

    [Test]
    public async Task Export_MultipleMethodsInProject_CalculatesCorrectAverageMI()
    {
        // Arrange
        var exporter = CreateExporter();
        var method1 = new MethodMetrics(
            FullName: "Project1.Class1.Method1()",
            FilePath: "test.cs",
            StartLine: 1,
            EndLine: 10,
            CyclomaticComplexity: 1,
            LinesOfExecutableCode: 5,
            HalsteadVolume: 10.0,
            MaintainabilityIndex: 80.0);

        var method2 = new MethodMetrics(
            FullName: "Project1.Class1.Method2()",
            FilePath: "test.cs",
            StartLine: 11,
            EndLine: 20,
            CyclomaticComplexity: 1,
            LinesOfExecutableCode: 5,
            HalsteadVolume: 10.0,
            MaintainabilityIndex: 60.0);

        var type = new TypeMetrics(
            FullName: "Project1.Class1",
            FilePath: "test.cs",
            DepthOfInheritance: 0,
            Methods: new List<MethodMetrics> { method1, method2 });

        var project = new ProjectMetrics(
            Name: "Project1",
            FilePath: "Project1.csproj",
            Types: new List<TypeMetrics> { type });

        var report = new AnalysisReport(
            SolutionPath: "test.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: new List<ProjectMetrics> { project },
            Warnings: new List<string>());

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        var compactProject = compactReport.Projects[0];
        await Assert.That(compactProject.AvgMaintainabilityIndex).IsEqualTo(70.0); // (80 + 60) / 2 = 70
    }

    [Test]
    public async Task Export_EmptyReport_ReturnsValidCompactReport()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = new AnalysisReport(
            SolutionPath: "empty.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: new List<ProjectMetrics>(),
            Warnings: new List<string>());

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        await Assert.That(compactReport).IsNotNull();
        await Assert.That(compactReport.Projects.Count).IsEqualTo(0);
        await Assert.That(compactReport.Diagnostics).IsNull();
    }

    [Test]
    public async Task ExportHierarchical_WithTypeDetails_IncludesNamespaces()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport();

        // Act
        var compactReport = exporter.ExportHierarchical(report, includeMethodDetails: false, includeTypeDetails: true);

        // Assert
        await Assert.That(compactReport).IsNotNull();
        await Assert.That(compactReport.Projects[0].Namespaces).IsNotNull();
        await Assert.That(compactReport.Projects[0].Types).IsNull(); // Mutually exclusive
        await Assert.That(compactReport.Projects[0].Namespaces!.Count).IsEqualTo(1);
        await Assert.That(compactReport.Projects[0].Namespaces![0].Name).IsEqualTo("TestProject");
    }

    [Test]
    public async Task ExportHierarchical_MultipleNamespaces_GroupsCorrectly()
    {
        // Arrange
        var exporter = CreateExporter();
        var types = new List<TypeMetrics>
        {
            new("NS1.Class1", "/f1.cs", 0, new List<MethodMetrics>
            {
                new("M1", "/f1.cs", 1, 5, 1, 10, 50, 80)
            }),
            new("NS1.Class2", "/f2.cs", 0, new List<MethodMetrics>
            {
                new("M2", "/f2.cs", 1, 5, 1, 8, 40, 70)
            }),
            new("NS2.Class3", "/f3.cs", 0, new List<MethodMetrics>
            {
                new("M3", "/f3.cs", 1, 5, 1, 12, 60, 90)
            })
        };

        var project = new ProjectMetrics("TestProject", "/project.csproj", types);
        var report = new AnalysisReport(
            SolutionPath: "test.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: new List<ProjectMetrics> { project },
            Warnings: []);

        // Act
        var compactReport = exporter.ExportHierarchical(report, includeMethodDetails: false, includeTypeDetails: true);

        // Assert
        await Assert.That(compactReport.Projects[0].Namespaces).IsNotNull();
        await Assert.That(compactReport.Projects[0].Namespaces!.Count).IsEqualTo(2);
        await Assert.That(compactReport.Projects[0].Namespaces![0].Name).IsEqualTo("NS1");
        await Assert.That(compactReport.Projects[0].Namespaces![0].Types).IsNotNull();
        await Assert.That(compactReport.Projects[0].Namespaces![0].Types!.Count).IsEqualTo(2);
        await Assert.That(compactReport.Projects[0].Namespaces![1].Name).IsEqualTo("NS2");
        await Assert.That(compactReport.Projects[0].Namespaces![1].Types!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ExportHierarchical_NamespaceMetrics_AreCorrectlyAggregated()
    {
        // Arrange
        var exporter = CreateExporter();
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

        var project = new ProjectMetrics("TestProject", "/project.csproj", types);
        var report = new AnalysisReport(
            SolutionPath: "test.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: new List<ProjectMetrics> { project },
            Warnings: []);

        // Act
        var compactReport = exporter.ExportHierarchical(report, includeMethodDetails: false, includeTypeDetails: true);

        // Assert
        var ns = compactReport.Projects[0].Namespaces![0];
        await Assert.That(ns.Name).IsEqualTo("NS");
        await Assert.That(ns.TypeCount).IsEqualTo(2);
        await Assert.That(ns.MethodCount).IsEqualTo(2);
        await Assert.That(ns.CyclomaticComplexity).IsEqualTo(8); // 3 + 5
        await Assert.That(ns.LinesOfCode).IsEqualTo(25); // 10 + 15
        await Assert.That(ns.MaxDepthOfInheritance).IsEqualTo(2);
        await Assert.That(ns.AvgMaintainabilityIndex).IsEqualTo(70.0); // (80 + 60) / 2
    }

    [Test]
    public async Task ExportHierarchical_WithMethodDetails_IncludesMethodsInTypes()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport();

        // Act
        var compactReport = exporter.ExportHierarchical(report, includeMethodDetails: true, includeTypeDetails: true);

        // Assert
        await Assert.That(compactReport.Projects[0].Namespaces).IsNotNull();
        var ns = compactReport.Projects[0].Namespaces![0];
        await Assert.That(ns.Types).IsNotNull();
        await Assert.That(ns.Types![0].Methods).IsNotNull();
        await Assert.That(ns.Types![0].Methods!.Count).IsEqualTo(1);
        
        var method = ns.Types![0].Methods![0];
        await Assert.That(method.Name).IsEqualTo("TestMethod()");
        await Assert.That(method.CyclomaticComplexity).IsEqualTo(2);
        await Assert.That(method.LinesOfCode).IsEqualTo(10);
    }

    [Test]
    public async Task ExportHierarchical_WithoutMethodDetails_ExcludesMethods()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport();

        // Act
        var compactReport = exporter.ExportHierarchical(report, includeMethodDetails: false, includeTypeDetails: true);

        // Assert
        await Assert.That(compactReport.Projects[0].Namespaces).IsNotNull();
        var ns = compactReport.Projects[0].Namespaces![0];
        await Assert.That(ns.Types).IsNotNull();
        await Assert.That(ns.Types![0].Methods).IsNull();
    }

    [Test]
    public async Task Export_DefaultBehavior_UsesFlatStructure()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport();

        // Act
        var compactReport = exporter.Export(report, includeMethodDetails: false, includeTypeDetails: true);

        // Assert - Backward compatibility: Export() should populate Types, not Namespaces
        await Assert.That(compactReport.Projects[0].Types).IsNotNull();
        await Assert.That(compactReport.Projects[0].Namespaces).IsNull();
        await Assert.That(compactReport.Projects[0].Types!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ExportHierarchical_EmptyNamespace_HandledCorrectly()
    {
        // Arrange
        var exporter = CreateExporter();
        var types = new List<TypeMetrics>
        {
            new("SimpleClass", "/f1.cs", 0, new List<MethodMetrics>
            {
                new("M1", "/f1.cs", 1, 5, 1, 10, 50, 80)
            }),
            new("NS.Class2", "/f2.cs", 0, new List<MethodMetrics>
            {
                new("M2", "/f2.cs", 1, 5, 1, 8, 40, 70)
            })
        };

        var project = new ProjectMetrics("TestProject", "/project.csproj", types);
        var report = new AnalysisReport(
            SolutionPath: "test.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: new List<ProjectMetrics> { project },
            Warnings: []);

        // Act
        var compactReport = exporter.ExportHierarchical(report, includeMethodDetails: false, includeTypeDetails: true);

        // Assert
        await Assert.That(compactReport.Projects[0].Namespaces).IsNotNull();
        await Assert.That(compactReport.Projects[0].Namespaces!.Count).IsEqualTo(2);
        
        // Global namespace should be first alphabetically (starts with '(')
        var globalNs = compactReport.Projects[0].Namespaces!.FirstOrDefault(n => n.Name == "(global)");
        await Assert.That(globalNs).IsNotNull();
        await Assert.That(globalNs!.Types!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ExportHierarchical_TypesIncludeFullName()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport();

        // Act
        var compactReport = exporter.ExportHierarchical(report, includeMethodDetails: false, includeTypeDetails: true);

        // Assert
        var type = compactReport.Projects[0].Namespaces![0].Types![0];
        await Assert.That(type.FullName).IsEqualTo("TestProject.TestClass");
        await Assert.That(type.Name).IsEqualTo("TestClass");
    }

    [Test]
    public async Task Export_WithCouplingGraph_NamespaceNodesIncludeAllMetrics()
    {
        // Arrange
        var exporter = CreateExporter();
        var types = new List<TypeMetrics>
        {
            new("NS1.Class1", "/f1.cs", DepthOfInheritance: 2, new List<MethodMetrics>
            {
                new("M1", "/f1.cs", 1, 5, CyclomaticComplexity: 3, LinesOfExecutableCode: 10, HalsteadVolume: 50, MaintainabilityIndex: 80)
            }),
            new("NS1.Class2", "/f2.cs", DepthOfInheritance: 1, new List<MethodMetrics>
            {
                new("M2", "/f2.cs", 1, 5, CyclomaticComplexity: 5, LinesOfExecutableCode: 15, HalsteadVolume: 60, MaintainabilityIndex: 60)
            }),
            new("NS2.Class3", "/f3.cs", DepthOfInheritance: 0, new List<MethodMetrics>
            {
                new("M3", "/f3.cs", 1, 5, CyclomaticComplexity: 2, LinesOfExecutableCode: 8, HalsteadVolume: 40, MaintainabilityIndex: 90)
            })
        };

        var project = new ProjectMetrics("TestProject", "/project.csproj", types);
        
        // Create coupling with namespace dependencies
        var dependencies = new List<DependencyEdge>
        {
            new("NS1", "NS2", DependencyType.NamespaceReference, 1),
            new("NS1", "NS2", DependencyType.NamespaceReference, 2), // Multiple refs same direction
            new("NS2", "NS1", DependencyType.NamespaceReference, 1)  // Reverse dependency
        };

        var report = new AnalysisReport(
            SolutionPath: "test.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: new List<ProjectMetrics> { project },
            Warnings: [])
        {
            CouplingAnalysis = new CouplingAnalysis("test.sln", DateTime.UtcNow)
            {
                ProjectCoupling = [],
                NamespaceCoupling = [],
                TypeCoupling = [],
                AllDependencies = dependencies,
                Summary = new CouplingSummary { TotalDependencies = 3 }
            }
        };

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        await Assert.That(compactReport.Graph).IsNotNull();
        await Assert.That(compactReport.Graph.Namespaces).IsNotNull();
        await Assert.That(compactReport.Graph.Namespaces.Nodes.Count).IsEqualTo(2); // NS1 and NS2

        // Verify NS1 node format: [id, name, loc, cc, mi, tc, mc, ce, ca, instability]
        var ns1Node = compactReport.Graph.Namespaces.Nodes.FirstOrDefault(n => (string)n[1] == "NS1");
        await Assert.That(ns1Node).IsNotNull();
        await Assert.That(ns1Node!.Length).IsEqualTo(10); // All 10 fields present
        
        // Verify metrics for NS1
        await Assert.That((int)ns1Node[2]).IsEqualTo(25); // LOC: 10 + 15
        await Assert.That((int)ns1Node[3]).IsEqualTo(8);  // CC: 3 + 5
        await Assert.That((double)ns1Node[4]).IsEqualTo(70.0); // MI: (80 + 60) / 2
        await Assert.That((int)ns1Node[5]).IsEqualTo(2);  // Type count
        await Assert.That((int)ns1Node[6]).IsEqualTo(2);  // Method count
        await Assert.That((int)ns1Node[7]).IsEqualTo(1);  // Ce: depends on NS2
        await Assert.That((int)ns1Node[8]).IsEqualTo(1);  // Ca: NS2 depends on NS1
        await Assert.That((double)ns1Node[9]).IsEqualTo(0.5); // Instability: 1/(1+1)
    }

    [Test]
    public async Task Export_NamespaceGraph_CalculatesCorrectCoupling()
    {
        // Arrange
        var exporter = CreateExporter();
        var types = new List<TypeMetrics>
        {
            new("Stable.Class1", "/f1.cs", 0, new List<MethodMetrics>
            {
                new("M1", "/f1.cs", 1, 5, 1, 10, 50, 80)
            }),
            new("Unstable.Class2", "/f2.cs", 0, new List<MethodMetrics>
            {
                new("M2", "/f2.cs", 1, 5, 1, 10, 50, 80)
            }),
            new("Middle.Class3", "/f3.cs", 0, new List<MethodMetrics>
            {
                new("M3", "/f3.cs", 1, 5, 1, 10, 50, 80)
            })
        };

        var project = new ProjectMetrics("TestProject", "/project.csproj", types);
        
        // Stable: Ca=2, Ce=0, Instability=0 (many things depend on it, it depends on nothing)
        // Unstable: Ca=0, Ce=2, Instability=1 (depends on many, nothing depends on it)
        // Middle: Ca=1, Ce=1, Instability=0.5 (balanced)
        var dependencies = new List<DependencyEdge>
        {
            new("Unstable", "Stable", DependencyType.NamespaceReference, 1),
            new("Unstable", "Middle", DependencyType.NamespaceReference, 1),
            new("Middle", "Stable", DependencyType.NamespaceReference, 1)
        };

        var report = new AnalysisReport(
            SolutionPath: "test.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: new List<ProjectMetrics> { project },
            Warnings: [])
        {
            CouplingAnalysis = new CouplingAnalysis("test.sln", DateTime.UtcNow)
            {
                ProjectCoupling = [],
                NamespaceCoupling = [],
                TypeCoupling = [],
                AllDependencies = dependencies,
                Summary = new CouplingSummary { TotalDependencies = 3 }
            }
        };

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        var nodes = compactReport.Graph.Namespaces.Nodes;
        
        var stableNode = nodes.First(n => (string)n[1] == "Stable");
        await Assert.That((int)stableNode[7]).IsEqualTo(0); // Ce: no outbound deps
        await Assert.That((int)stableNode[8]).IsEqualTo(2); // Ca: 2 inbound deps
        await Assert.That((double)stableNode[9]).IsEqualTo(0.0); // Instability: perfectly stable
        
        var unstableNode = nodes.First(n => (string)n[1] == "Unstable");
        await Assert.That((int)unstableNode[7]).IsEqualTo(2); // Ce: 2 outbound deps
        await Assert.That((int)unstableNode[8]).IsEqualTo(0); // Ca: no inbound deps
        await Assert.That((double)unstableNode[9]).IsEqualTo(1.0); // Instability: perfectly unstable
        
        var middleNode = nodes.First(n => (string)n[1] == "Middle");
        await Assert.That((int)middleNode[7]).IsEqualTo(1); // Ce: 1 outbound dep
        await Assert.That((int)middleNode[8]).IsEqualTo(1); // Ca: 1 inbound dep
        await Assert.That((double)middleNode[9]).IsEqualTo(0.5); // Instability: balanced
    }

    [Test]
    public async Task Export_NamespaceGraph_ExcludesSelfLoops()
    {
        // Arrange
        var exporter = CreateExporter();
        var types = new List<TypeMetrics>
        {
            new("NS1.Class1", "/f1.cs", 0, new List<MethodMetrics> { new("M1", "/f1.cs", 1, 5, 1, 10, 50, 80) }),
            new("NS1.Class2", "/f2.cs", 0, new List<MethodMetrics> { new("M2", "/f2.cs", 1, 5, 1, 10, 50, 80) })
        };

        var project = new ProjectMetrics("TestProject", "/project.csproj", types);
        
        // Self-referencing dependency (within same namespace)
        var dependencies = new List<DependencyEdge>
        {
            new("NS1", "NS1", DependencyType.NamespaceReference, 1) // Should be excluded
        };

        var report = new AnalysisReport(
            SolutionPath: "test.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: new List<ProjectMetrics> { project },
            Warnings: [])
        {
            CouplingAnalysis = new CouplingAnalysis("test.sln", DateTime.UtcNow)
            {
                ProjectCoupling = [],
                NamespaceCoupling = [],
                TypeCoupling = [],
                AllDependencies = dependencies,
                Summary = new CouplingSummary { TotalDependencies = 1 }
            }
        };

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        await Assert.That(compactReport.Graph.Namespaces.Edges.Count).IsEqualTo(0); // Self-loop excluded
        
        var ns1Node = compactReport.Graph.Namespaces.Nodes.First(n => (string)n[1] == "NS1");
        await Assert.That((int)ns1Node[7]).IsEqualTo(0); // Ce: no external deps
        await Assert.That((int)ns1Node[8]).IsEqualTo(0); // Ca: no external dependents
        await Assert.That((double)ns1Node[9]).IsEqualTo(0.0); // Instability: 0 (default when total=0)
    }

    [Test]
    public async Task Export_NamespaceGraph_OnlyIncludesInternalNamespaces()
    {
        // Arrange
        var exporter = CreateExporter();
        var types = new List<TypeMetrics>
        {
            new("Internal.Class1", "/f1.cs", 0, new List<MethodMetrics> { new("M1", "/f1.cs", 1, 5, 1, 10, 50, 80) })
        };

        var project = new ProjectMetrics("TestProject", "/project.csproj", types);
        
        // Dependencies to external namespaces should be excluded from graph
        var dependencies = new List<DependencyEdge>
        {
            new("Internal", "External", DependencyType.NamespaceReference, 1), // External target - excluded
            new("External", "Internal", DependencyType.NamespaceReference, 1)  // External source - excluded
        };

        var report = new AnalysisReport(
            SolutionPath: "test.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: new List<ProjectMetrics> { project },
            Warnings: [])
        {
            CouplingAnalysis = new CouplingAnalysis("test.sln", DateTime.UtcNow)
            {
                ProjectCoupling = [],
                NamespaceCoupling = [],
                TypeCoupling = [],
                AllDependencies = dependencies,
                Summary = new CouplingSummary { TotalDependencies = 2 }
            }
        };

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        await Assert.That(compactReport.Graph.Namespaces.Nodes.Count).IsEqualTo(1); // Only Internal
        await Assert.That((string)compactReport.Graph.Namespaces.Nodes[0][1]).IsEqualTo("Internal");
        await Assert.That(compactReport.Graph.Namespaces.Edges.Count).IsEqualTo(0); // No internal-to-internal edges
    }

    [Test]
    public async Task Export_NamespaceGraph_AveragesMaintainabilityIndexCorrectly()
    {
        // Arrange
        var exporter = CreateExporter();
        var types = new List<TypeMetrics>
        {
            new("NS.Class1", "/f1.cs", 0, new List<MethodMetrics>
            {
                new("M1", "/f1.cs", 1, 5, 1, 10, 50, MaintainabilityIndex: 90),
                new("M2", "/f1.cs", 6, 10, 1, 10, 50, MaintainabilityIndex: 80),
                new("M3", "/f1.cs", 11, 15, 1, 10, 50, MaintainabilityIndex: 70)
            }),
            new("NS.Class2", "/f2.cs", 0, new List<MethodMetrics>
            {
                new("M4", "/f2.cs", 1, 5, 1, 10, 50, MaintainabilityIndex: 60)
            })
        };

        var project = new ProjectMetrics("TestProject", "/project.csproj", types);
        var report = new AnalysisReport(
            SolutionPath: "test.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: new List<ProjectMetrics> { project },
            Warnings: [])
        {
            CouplingAnalysis = new CouplingAnalysis("test.sln", DateTime.UtcNow)
            {
                ProjectCoupling = [],
                NamespaceCoupling = [],
                TypeCoupling = [],
                AllDependencies = [],
                Summary = new CouplingSummary { TotalDependencies = 0 }
            }
        };

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        var nsNode = compactReport.Graph.Namespaces.Nodes.First(n => (string)n[1] == "NS");
        // MI should be average across all methods: (90 + 80 + 70 + 60) / 4 = 75.0
        await Assert.That((double)nsNode[4]).IsEqualTo(75.0);
    }

    [Test]
    public async Task Export_NamespaceGraph_EmptyNamespace_HasZeroMetrics()
    {
        // Arrange
        var exporter = CreateExporter();
        var types = new List<TypeMetrics>
        {
            new("Empty.Class1", "/f1.cs", 0, []) // No methods
        };

        var project = new ProjectMetrics("TestProject", "/project.csproj", types);
        var report = new AnalysisReport(
            SolutionPath: "test.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: new List<ProjectMetrics> { project },
            Warnings: [])
        {
            CouplingAnalysis = new CouplingAnalysis("test.sln", DateTime.UtcNow)
            {
                ProjectCoupling = [],
                NamespaceCoupling = [],
                TypeCoupling = [],
                AllDependencies = [],
                Summary = new CouplingSummary { TotalDependencies = 0 }
            }
        };

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        var nsNode = compactReport.Graph.Namespaces.Nodes.First(n => (string)n[1] == "Empty");
        await Assert.That((int)nsNode[2]).IsEqualTo(0);   // LOC
        await Assert.That((int)nsNode[3]).IsEqualTo(0);   // CC
        await Assert.That((double)nsNode[4]).IsEqualTo(0.0); // MI (0 when no methods)
        await Assert.That((int)nsNode[5]).IsEqualTo(1);   // Type count
        await Assert.That((int)nsNode[6]).IsEqualTo(0);   // Method count
    }

    [Test]
    public async Task Export_WithGitInfo_IncludesGitMetadata()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport() with
        {
            GitInfo = new GitRepositoryInfo(
                CommitSha: "1234567890abcdef1234567890abcdef12345678",
                BranchName: "feature/test-branch",
                RemoteUrl: "https://github.com/test/repo.git",
                IsDirty: true)
        };

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        await Assert.That(compactReport.GitCommitSha).IsEqualTo("1234567890abcdef1234567890abcdef12345678");
        await Assert.That(compactReport.GitBranch).IsEqualTo("feature/test-branch");
        await Assert.That(compactReport.GitRemoteUrl).IsEqualTo("https://github.com/test/repo.git");
        await Assert.That(compactReport.GitIsDirty).IsTrue();
    }

    [Test]
    public async Task Export_WithoutGitInfo_GitFieldsAreNull()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport(); // No git info

        // Act
        var compactReport = exporter.Export(report);

        // Assert
        await Assert.That(compactReport.GitCommitSha).IsNull();
        await Assert.That(compactReport.GitBranch).IsNull();
        await Assert.That(compactReport.GitRemoteUrl).IsNull();
        await Assert.That(compactReport.GitIsDirty).IsFalse(); // Default value for bool
    }

    [Test]
    public async Task ExportHierarchical_WithGitInfo_IncludesGitMetadata()
    {
        // Arrange
        var exporter = CreateExporter();
        var report = CreateMinimalReport() with
        {
            GitInfo = new GitRepositoryInfo(
                CommitSha: "abcdef1234567890abcdef1234567890abcdef12",
                BranchName: "main",
                RemoteUrl: null, // Test with null remote
                IsDirty: false)
        };

        // Act
        var compactReport = exporter.ExportHierarchical(report);

        // Assert
        await Assert.That(compactReport.GitCommitSha).IsEqualTo("abcdef1234567890abcdef1234567890abcdef12");
        await Assert.That(compactReport.GitBranch).IsEqualTo("main");
        await Assert.That(compactReport.GitRemoteUrl).IsNull();
        await Assert.That(compactReport.GitIsDirty).IsFalse();
    }
}
