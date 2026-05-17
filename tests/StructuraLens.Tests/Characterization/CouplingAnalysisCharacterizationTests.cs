using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using StructuraLens.Core.Analysis;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Characterization;

public sealed class CouplingAnalysisCharacterizationTests
{
    [Test]
    public async Task BuildCouplingAnalysisFromDependencies_SmallFixtureAggregatesDuplicateEdges()
    {
        var analyzer = new CouplingAnalyzer(A.Fake<ILogger<CouplingAnalyzer>>());
        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Default,
                name: "App",
                assemblyName: "App",
                language: LanguageNames.CSharp))
            .AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Default,
                name: "Library",
                assemblyName: "Library",
                language: LanguageNames.CSharp));

        var dependencies = new List<DependencyEdge>
        {
            new("App", "Library", DependencyType.ProjectReference, 1),
            new("App", "Library", DependencyType.ProjectReference, 3),
            new("App.Core", "Library.Core", DependencyType.NamespaceReference, 1),
            new("App.Core", "Library.Core", DependencyType.NamespaceReference, 2),
            new("Library.Core", "System", DependencyType.NamespaceReference, 1),
            new("App.Core.Controller", "Library.Core.Service", DependencyType.TypeReference, 5),
        };

        var analysis = analyzer.BuildCouplingAnalysisFromDependencies(solution, dependencies);

        await Assert.That(analysis.AllDependencies.Count).IsEqualTo(4);
        await Assert.That(analysis.Summary.TotalDependencies).IsEqualTo(4);

        var projectEdge = analysis.AllDependencies.Single(edge => edge.Type == DependencyType.ProjectReference);
        await Assert.That(projectEdge.ReferenceCount).IsEqualTo(4);

        var namespaceEdge = analysis.AllDependencies.Single(edge =>
            edge.Type == DependencyType.NamespaceReference &&
            edge.FromEntity == "App.Core" &&
            edge.ToEntity == "Library.Core");
        await Assert.That(namespaceEdge.ReferenceCount).IsEqualTo(3);

        var appProject = analysis.ProjectCoupling.Single(metric => metric.EntityName == "App");
        await Assert.That(appProject.InternalDependencies).IsEqualTo(1);
        await Assert.That(appProject.InternalDependents).IsEqualTo(0);
        await Assert.That(appProject.DependencyRatio).IsEqualTo(1);

        var libraryProject = analysis.ProjectCoupling.Single(metric => metric.EntityName == "Library");
        await Assert.That(libraryProject.InternalDependencies).IsEqualTo(0);
        await Assert.That(libraryProject.InternalDependents).IsEqualTo(1);
        await Assert.That(libraryProject.DependencyRatio).IsEqualTo(0);

        var appNamespace = analysis.NamespaceCoupling.Single(metric => metric.EntityName == "App.Core");
        await Assert.That(appNamespace.InternalDependencies).IsEqualTo(1);
        await Assert.That(appNamespace.TotalExternalDependencies).IsEqualTo(0);

        var libraryNamespace = analysis.NamespaceCoupling.Single(metric => metric.EntityName == "Library.Core");
        await Assert.That(libraryNamespace.InternalDependents).IsEqualTo(1);
        await Assert.That(libraryNamespace.ExternalBclDependencies).IsEqualTo(1);
    }
}
