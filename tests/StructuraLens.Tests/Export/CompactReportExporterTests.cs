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
}
