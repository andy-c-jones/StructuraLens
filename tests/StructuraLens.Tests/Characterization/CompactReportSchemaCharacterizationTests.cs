using System.Text.Json;
using System.Text.Json.Serialization;
using StructuraLens.Core.Export;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Characterization;

public sealed class CompactReportSchemaCharacterizationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Test]
    public async Task Export_SerializedCompactReport_UsesShortStablePropertyNames()
    {
        var report = CreateReport();
        var exporter = new CompactReportExporter();

        var compactReport = exporter.Export(report, includeMethodDetails: true, includeTypeDetails: true);
        var json = JsonSerializer.Serialize(compactReport, JsonOptions);

        await Assert.That(json).Contains("\"v\":1");
        await Assert.That(json).Contains("\"p\":\"Sample.sln\"");
        await Assert.That(json).Contains("\"prj\"");
        await Assert.That(json).Contains("\"g\"");
        await Assert.That(json).Contains("\"types\"");
        await Assert.That(json).Contains("\"m\"");
        await Assert.That(json).DoesNotContain("\"version\"");
        await Assert.That(json).DoesNotContain("\"projects\"");
        await Assert.That(json).DoesNotContain("\"graph\"");
    }

    [Test]
    public async Task Export_ProjectGraph_UsesIdNameSizeNodesAndSourceTargetWeightEdges()
    {
        var report = CreateReport();
        var exporter = new CompactReportExporter();

        var compactReport = exporter.Export(report);

        await Assert.That(compactReport.Graph.Projects.Nodes.Count).IsEqualTo(2);
        await Assert.That(compactReport.Graph.Projects.Edges.Count).IsEqualTo(1);

        var appNode = compactReport.Graph.Projects.Nodes.First(node => (string)node[1] == "App");
        var libraryNode = compactReport.Graph.Projects.Nodes.First(node => (string)node[1] == "Library");

        await Assert.That(appNode.Length).IsEqualTo(3);
        await Assert.That(libraryNode.Length).IsEqualTo(3);
        await Assert.That((int)appNode[2]).IsEqualTo(3);
        await Assert.That((int)libraryNode[2]).IsEqualTo(5);

        var edge = compactReport.Graph.Projects.Edges.Single();
        await Assert.That(edge.Length).IsEqualTo(3);
        await Assert.That(edge[0]).IsEqualTo((int)appNode[0]);
        await Assert.That(edge[1]).IsEqualTo((int)libraryNode[0]);
        await Assert.That(edge[2]).IsEqualTo(4);
    }

    private static AnalysisReport CreateReport()
    {
        var appType = CreateType("App.Program", "Program.cs", linesOfCode: 3);
        var libraryType = CreateType("Library.Service", "Service.cs", linesOfCode: 5);

        var app = new ProjectMetrics("App", "App.csproj", [appType]);
        var library = new ProjectMetrics("Library", "Library.csproj", [libraryType]);

        return new AnalysisReport(
            SolutionPath: "Sample.sln",
            AnalyzedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Projects: [app, library],
            Warnings: [],
            ToolVersion: "test")
        {
            CouplingAnalysis = new CouplingAnalysis("Sample.sln", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            {
                ProjectCoupling = [],
                NamespaceCoupling = [],
                TypeCoupling = [],
                AllDependencies =
                [
                    new DependencyEdge("App", "Library", DependencyType.ProjectReference, 4)
                ],
                Summary = new CouplingSummary { TotalDependencies = 1 }
            }
        };
    }

    private static TypeMetrics CreateType(string fullName, string filePath, int linesOfCode)
    {
        var method = new MethodMetrics(
            FullName: $"{fullName}.Run()",
            FilePath: filePath,
            StartLine: 1,
            EndLine: linesOfCode,
            CyclomaticComplexity: 1,
            LinesOfExecutableCode: linesOfCode,
            HalsteadVolume: 5,
            MaintainabilityIndex: 90);

        return new TypeMetrics(fullName, filePath, DepthOfInheritance: 0, Methods: [method]);
    }
}
