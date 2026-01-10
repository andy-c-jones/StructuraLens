using StructuraLens.Core.Configuration;
using StructuraLens.Core.Export;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Export;

public class CompactReportExporterTests
{
    private static AnalysisReport CreateTestReport()
    {
        var methods = new List<MethodMetrics>
        {
            new("MyApp.Services.UserService.GetUser(int)", "UserService.cs", 10, 25, 5, 15, 100.0, 65.0),
            new("MyApp.Services.UserService.SaveUser(User)", "UserService.cs", 30, 50, 8, 20, 150.0, 55.0)
        };

        var types = new List<TypeMetrics>
        {
            new("MyApp.Services.UserService", "UserService.cs", 2, methods)
        };

        var projects = new List<ProjectMetrics>
        {
            new("MyApp.Services", "MyApp.Services.csproj", types)
        };

        var dependencies = new List<DependencyEdge>
        {
            new("MyApp.Services", "MyApp.Core", DependencyType.ProjectReference, 1),
            new("MyApp.Services", "MyApp.Core.Models", DependencyType.NamespaceReference, 5)
        };

        var coupling = new CouplingAnalysis("Test", DateTime.UtcNow)
        {
            AllDependencies = dependencies,
            ProjectCoupling = new List<CouplingMetrics>
            {
                new("MyApp.Services", DependencyType.ProjectReference)
                {
                    OutboundDependencies = dependencies.Where(d => d.Type == DependencyType.ProjectReference).ToList(),
                    InboundDependencies = []
                }
            },
            NamespaceCoupling = [],
            TypeCoupling = []
        };

        return new AnalysisReport(
            SolutionPath: "/test/MyApp.sln",
            AnalyzedAt: new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
            Projects: projects,
            Warnings: [])
        {
            CouplingAnalysis = coupling
        };
    }

    [Test]
    public async Task Export_ProducesValidCompactReport()
    {
        var report = CreateTestReport();

        var compact = CompactReportExporter.Export(report);

        await Assert.That(compact.Version).IsEqualTo(1);
        await Assert.That(compact.Path).IsEqualTo("/test/MyApp.sln");
        await Assert.That(compact.Timestamp).IsGreaterThan(0);
    }

    [Test]
    public async Task Export_ProjectMetricsAreCorrect()
    {
        var report = CreateTestReport();

        var compact = CompactReportExporter.Export(report);

        await Assert.That(compact.Projects.Count).IsEqualTo(1);
        var project = compact.Projects[0];
        await Assert.That(project.Name).IsEqualTo("MyApp.Services");
        await Assert.That(project.TypeCount).IsEqualTo(1);
        await Assert.That(project.MethodCount).IsEqualTo(2);
        await Assert.That(project.CyclomaticComplexity).IsEqualTo(13); // 5 + 8
        await Assert.That(project.LinesOfCode).IsEqualTo(35); // 15 + 20
    }

    [Test]
    public async Task Export_WithoutDetails_OmitsTypesAndMethods()
    {
        var report = CreateTestReport();

        var compact = CompactReportExporter.Export(report, includeTypeDetails: false, includeMethodDetails: false);

        await Assert.That(compact.Projects[0].Types).IsNull();
    }

    [Test]
    public async Task Export_WithTypeDetails_IncludesTypes()
    {
        var report = CreateTestReport();

        var compact = CompactReportExporter.Export(report, includeTypeDetails: true);

        await Assert.That(compact.Projects[0].Types).IsNotNull();
        await Assert.That(compact.Projects[0].Types!.Count).IsEqualTo(1);
        await Assert.That(compact.Projects[0].Types![0].Name).IsEqualTo("UserService");
    }

    [Test]
    public async Task Export_WithMethodDetails_IncludesMethods()
    {
        var report = CreateTestReport();

        var compact = CompactReportExporter.Export(report, includeTypeDetails: true, includeMethodDetails: true);

        var type = compact.Projects[0].Types![0];
        await Assert.That(type.Methods).IsNotNull();
        await Assert.That(type.Methods!.Count).IsEqualTo(2);
        await Assert.That(type.Methods![0].Name).IsEqualTo("GetUser(int)");
    }

    [Test]
    public async Task Export_GraphHasProjectNodes()
    {
        var report = CreateTestReport();

        var compact = CompactReportExporter.Export(report);

        await Assert.That(compact.Graph.Projects.Nodes.Count).IsEqualTo(1);
        var node = compact.Graph.Projects.Nodes[0];
        await Assert.That((int)node[0]).IsEqualTo(0); // id
        await Assert.That((string)node[1]).IsEqualTo("MyApp.Services"); // name
        await Assert.That((int)node[2]).IsEqualTo(35); // LOC
    }

    [Test]
    public async Task Export_LintingResultsIncluded()
    {
        var report = CreateTestReport();
        var linting = new LintingResults(DateTime.UtcNow)
        {
            RulesEvaluated = 3,
            Violations = new List<LintViolation>
            {
                new("RULE-1", "Test violation", RuleSeverity.Error)
                {
                    FromEntity = "A",
                    ToEntity = "B"
                }
            }
        };

        var reportWithLinting = report with { LintingResults = linting };
        var compact = CompactReportExporter.Export(reportWithLinting);

        await Assert.That(compact.Linting).IsNotNull();
        await Assert.That(compact.Linting!.RulesEvaluated).IsEqualTo(3);
        await Assert.That(compact.Linting.Errors).IsEqualTo(1);
        await Assert.That(compact.Linting.Passed).IsFalse();
        await Assert.That(compact.Linting.Violations!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Export_ShortNamesExtractedCorrectly()
    {
        var report = CreateTestReport();

        var compact = CompactReportExporter.Export(report, includeTypeDetails: true, includeMethodDetails: true);

        var method = compact.Projects[0].Types![0].Methods![0];
        // Should be "GetUser(int)" not "MyApp.Services.UserService.GetUser(int)"
        await Assert.That(method.Name).IsEqualTo("GetUser(int)");
    }

    [Test]
    public async Task Export_TimestampIsUnixMilliseconds()
    {
        var report = CreateTestReport();

        var compact = CompactReportExporter.Export(report);

        var expectedMs = new DateTimeOffset(report.AnalyzedAt).ToUnixTimeMilliseconds();
        await Assert.That(compact.Timestamp).IsEqualTo(expectedMs);
    }
}
