using StructuraLens.Core.Diff;
using StructuraLens.Core.Export;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Characterization;

public sealed class DependencyClassificationCharacterizationTests
{
    [Test]
    public async Task CouplingMetrics_ClassifiesSystemAndMicrosoftNamespacesAsBclDependencies()
    {
        var metrics = new CouplingMetrics("Consumer", DependencyType.NamespaceReference)
        {
            ExternalOutbound =
            [
                new DependencyEdge("Consumer", "System", DependencyType.NamespaceReference, 1),
                new DependencyEdge("Consumer", "System.Collections.Generic", DependencyType.NamespaceReference, 1),
                new DependencyEdge("Consumer", "Microsoft.Extensions.Logging", DependencyType.NamespaceReference, 1),
                new DependencyEdge("Consumer", "Newtonsoft.Json", DependencyType.NamespaceReference, 1),
                new DependencyEdge("Consumer", "Company.Systematic", DependencyType.NamespaceReference, 1),
                new DependencyEdge("Consumer", "System.Collections.Generic", DependencyType.NamespaceReference, 3),
            ]
        };

        await Assert.That(metrics.ExternalBclDependencies).IsEqualTo(3);
        await Assert.That(metrics.ExternalPackageDependencies).IsEqualTo(2);
        await Assert.That(metrics.TotalExternalDependencies).IsEqualTo(5);
    }

    [Test]
    public async Task CompactExporter_ClassifiesPackageReferencesIntoBclAndThirdPartyCounts()
    {
        var report = CreateReportWithPackageReferences(
            "System",
            "System.CommandLine",
            "Microsoft.Extensions.Logging",
            "LibGit2Sharp",
            "Company.Systematic");

        var compactReport = new CompactReportExporter().Export(report);
        var project = compactReport.Projects.Single();

        await Assert.That(project.ExternalDependencies).IsEqualTo(5);
        await Assert.That(project.ExternalBclDependencies).IsEqualTo(3);
        await Assert.That(project.ExternalPackageDependencies).IsEqualTo(2);
    }

    [Test]
    public async Task DiffCalculator_ClassifiesAddedPackageReferencesIntoBclAndThirdPartyLists()
    {
        var baseReport = CreateReportWithPackageReferences();
        var headReport = CreateReportWithPackageReferences(
            "System",
            "System.CommandLine",
            "Microsoft.Extensions.Logging",
            "LibGit2Sharp",
            "Company.Systematic");

        var diff = new DiffCalculator().Compare(baseReport, headReport);
        var project = diff.Projects.Single();

        await Assert.That(project.AddedBclDependencies).IsEquivalentTo([
            "Microsoft.Extensions.Logging",
            "System",
            "System.CommandLine"
        ]);
        await Assert.That(project.AddedPackageDependencies).IsEquivalentTo([
            "Company.Systematic",
            "LibGit2Sharp"
        ]);
        await Assert.That(diff.NewToSolution).Contains("System.CommandLine");
        await Assert.That(diff.NewToSolution).Contains("LibGit2Sharp");
    }

    private static AnalysisReport CreateReportWithPackageReferences(params string[] packageReferences)
    {
        var project = new ProjectMetrics(
            Name: "Consumer",
            FilePath: "Consumer.csproj",
            Types: [])
        {
            PackageReferences = packageReferences
        };

        return new AnalysisReport(
            SolutionPath: "Sample.sln",
            AnalyzedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Projects: [project],
            Warnings: [],
            ToolVersion: "test");
    }
}
