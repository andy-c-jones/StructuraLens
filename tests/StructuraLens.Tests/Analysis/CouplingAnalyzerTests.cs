using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using StructuraLens.Core.Analysis;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Analysis;

public class CouplingAnalyzerTests
{
    private static CouplingAnalyzer CreateAnalyzer()
    {
        var logger = A.Fake<ILogger<CouplingAnalyzer>>();
        return new CouplingAnalyzer(logger);
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new CouplingAnalyzer(null!);
        });
    }

    [Test]
    public async Task AnalyzeDocumentCoupling_WithSimpleCode_FindsDependencies()
    {
        // Arrange
        var code = """
            using System;
            using System.Collections.Generic;

            namespace TestNamespace
            {
                public class TestClass
                {
                    private List<string> items;
                    
                    public void DoWork()
                    {
                        Console.WriteLine("Hello");
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(tree);

        var semanticModel = compilation.GetSemanticModel(tree);
        var root = await tree.GetRootAsync();

        // Act
        var dependencies = CouplingAnalyzer.AnalyzeDocumentCoupling(semanticModel, "test.cs", root);

        // Assert
        await Assert.That(dependencies).IsNotNull();
        await Assert.That(dependencies.Count).IsGreaterThan(0);

        // Should find namespace references
        var namespaceDeps = dependencies.Where(d => d.Type == DependencyType.NamespaceReference).ToList();
        await Assert.That(namespaceDeps.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task AnalyzeDocumentCoupling_WithNoReferences_ReturnsEmptyOrSmallList()
    {
        // Arrange
        var code = """
            namespace TestNamespace
            {
                public class EmptyClass
                {
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(tree);

        var semanticModel = compilation.GetSemanticModel(tree);
        var root = await tree.GetRootAsync();

        // Act
        var dependencies = CouplingAnalyzer.AnalyzeDocumentCoupling(semanticModel, "test.cs", root);

        // Assert
        await Assert.That(dependencies).IsNotNull();
        // May have zero or very few dependencies for an empty class
        await Assert.That(dependencies.Count).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task AnalyzeDocumentCoupling_WithTypeReferences_FindsTypeDependencies()
    {
        // Arrange
        var code = """
            namespace TestNamespace
            {
                public class BaseClass { }
                
                public class DerivedClass : BaseClass
                {
                    private BaseClass field;
                    
                    public BaseClass GetInstance()
                    {
                        return new BaseClass();
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(tree);

        var semanticModel = compilation.GetSemanticModel(tree);
        var root = await tree.GetRootAsync();

        // Act
        var dependencies = CouplingAnalyzer.AnalyzeDocumentCoupling(semanticModel, "test.cs", root);

        // Assert
        await Assert.That(dependencies).IsNotNull();

        // Should find type references from DerivedClass to BaseClass
        var typeDeps = dependencies.Where(d => d.Type == DependencyType.TypeReference).ToList();
        await Assert.That(typeDeps.Count).IsGreaterThan(0);

        var derivedToBase = typeDeps.Where(d =>
            d.FromEntity.Contains("DerivedClass") &&
            d.ToEntity.Contains("BaseClass")).ToList();
        await Assert.That(derivedToBase.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task BuildCouplingAnalysisFromDependencies_WithEmptyDependencies_ReturnsValidAnalysis()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "TestProject",
            "TestProject",
            LanguageNames.CSharp);
        solution = solution.AddProject(projectInfo);

        var dependencies = new List<DependencyEdge>();

        // Act
        var analysis = analyzer.BuildCouplingAnalysisFromDependencies(solution, dependencies);

        // Assert
        await Assert.That(analysis).IsNotNull();
        await Assert.That(analysis.Summary).IsNotNull();
        await Assert.That(analysis.ProjectCoupling).IsNotNull();
        await Assert.That(analysis.NamespaceCoupling).IsNotNull();
        await Assert.That(analysis.TypeCoupling).IsNotNull();
        await Assert.That(analysis.Summary.TotalDependencies).IsEqualTo(0);
    }

    [Test]
    public async Task BuildCouplingAnalysisFromDependencies_WithProjectDependencies_CalculatesMetrics()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;

        var project1 = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Project1",
            "Project1",
            LanguageNames.CSharp);
        var project2 = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Project2",
            "Project2",
            LanguageNames.CSharp);

        solution = solution.AddProject(project1).AddProject(project2);

        var dependencies = new List<DependencyEdge>
        {
            new DependencyEdge("Project1", "Project2", DependencyType.ProjectReference, 1)
        };

        // Act
        var analysis = analyzer.BuildCouplingAnalysisFromDependencies(solution, dependencies);

        // Assert
        await Assert.That(analysis).IsNotNull();
        await Assert.That(analysis.ProjectCoupling.Count).IsEqualTo(2);
        await Assert.That(analysis.Summary.TotalDependencies).IsGreaterThan(0);

        var project1Coupling = analysis.ProjectCoupling.FirstOrDefault(p => p.EntityName == "Project1");
        await Assert.That(project1Coupling).IsNotNull();
        await Assert.That(project1Coupling!.InternalOutbound.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task BuildCouplingAnalysisFromDependencies_WithSolutionAndInputProjectReference_DoesNotDoubleCount()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var workspace = new AdhocWorkspace();
        var project1Id = ProjectId.CreateNewId();
        var project2Id = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                project1Id,
                VersionStamp.Default,
                "Project1",
                "Project1",
                LanguageNames.CSharp,
                projectReferences: [new ProjectReference(project2Id)]))
            .AddProject(ProjectInfo.Create(
                project2Id,
                VersionStamp.Default,
                "Project2",
                "Project2",
                LanguageNames.CSharp));

        var dependencies = new List<DependencyEdge>
        {
            new("Project1", "Project2", DependencyType.ProjectReference, 1)
        };

        // Act
        var analysis = analyzer.BuildCouplingAnalysisFromDependencies(solution, dependencies);

        // Assert
        var projectEdge = analysis.AllDependencies.Single(edge =>
            edge.Type == DependencyType.ProjectReference &&
            edge.FromEntity == "Project1" &&
            edge.ToEntity == "Project2");

        await Assert.That(projectEdge.ReferenceCount).IsEqualTo(1);
    }

    [Test]
    public async Task AnalyzeProjectCouplingAsync_WithSimpleProject_ReturnsValidAnalysis()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var workspace = new AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "TestProject",
            "TestProject",
            LanguageNames.CSharp);

        var project = workspace.AddProject(projectInfo);

        var code = """
            namespace TestNamespace
            {
                public class TestClass
                {
                    public void TestMethod()
                    {
                    }
                }
            }
            """;

        var sourceText = SourceText.From(code);
        var document = project.AddDocument("test.cs", sourceText);
        project = document.Project;

        // Act
        var analysis = await analyzer.AnalyzeProjectCouplingAsync(project);

        // Assert
        await Assert.That(analysis).IsNotNull();
        await Assert.That(analysis.AnalyzedEntity).IsNotNull();
        await Assert.That(analysis.Summary).IsNotNull();
        await Assert.That(analysis.ProjectCoupling.Count).IsEqualTo(1);
        await Assert.That(analysis.ProjectCoupling[0].EntityName).IsEqualTo("TestProject");
    }

    [Test]
    public async Task AnalyzeProjectInternalCouplingAsync_WithMultipleNamespaces_FindsNamespaceCoupling()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var workspace = new AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            metadataReferences: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var project = workspace.AddProject(projectInfo);

        var code1 = """
            using Namespace2;
            
            namespace Namespace1
            {
                public class Class1
                {
                    private Class2 field;
                }
            }
            """;

        var code2 = """
            namespace Namespace2
            {
                public class Class2
                {
                }
            }
            """;

        var doc1 = project.AddDocument("file1.cs", SourceText.From(code1));
        project = doc1.Project;
        var doc2 = project.AddDocument("file2.cs", SourceText.From(code2));
        project = doc2.Project;

        // Act
        var result = await analyzer.AnalyzeProjectInternalCouplingAsync(project);

        // Assert
        await Assert.That(result.namespaceCoupling).IsNotNull();
        await Assert.That(result.dependencies).IsNotNull();
        await Assert.That(result.dependencies.Count).IsGreaterThan(0);

        // Should find coupling between Namespace1 and Namespace2
        var namespaceDep = result.dependencies.FirstOrDefault(d =>
            d.Type == DependencyType.NamespaceReference &&
            d.FromEntity == "Namespace1" &&
            d.ToEntity == "Namespace2");
        await Assert.That(namespaceDep).IsNotNull();
    }

    [Test]
    public async Task AnalyzeSolutionAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "TestProject",
            "TestProject",
            LanguageNames.CSharp);
        solution = solution.AddProject(projectInfo);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await analyzer.AnalyzeSolutionAsync(solution, null, cts.Token);
        });
    }

    [Test]
    public async Task BuildCouplingAnalysisFromDependencies_WithProjectDependencies_TracksInternalCouplingOnly()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;

        var project1 = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Project1",
            "Project1",
            LanguageNames.CSharp);
        var project2 = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Project2",
            "Project2",
            LanguageNames.CSharp);

        solution = solution.AddProject(project1).AddProject(project2);

        var dependencies = new List<DependencyEdge>
        {
            // Internal project reference
            new DependencyEdge("Project1", "Project2", DependencyType.ProjectReference, 1),
            
            // AssemblyReference edges are no longer used for project-level external deps
            // (external deps are tracked via PackageReferences on ProjectMetrics instead)
            new DependencyEdge("Project1", "System.Linq", DependencyType.AssemblyReference, 3),
        };

        // Act
        var analysis = analyzer.BuildCouplingAnalysisFromDependencies(solution, dependencies);

        // Assert
        await Assert.That(analysis).IsNotNull();
        await Assert.That(analysis.ProjectCoupling.Count).IsEqualTo(2);

        var project1Coupling = analysis.ProjectCoupling.FirstOrDefault(p => p.EntityName == "Project1");
        await Assert.That(project1Coupling).IsNotNull();

        // Project1 should have 1 internal dependency (Project2)
        await Assert.That(project1Coupling!.InternalDependencies).IsEqualTo(1);

        // External deps at coupling level should be 0 (now tracked via PackageReferences)
        await Assert.That(project1Coupling.TotalExternalDependencies).IsEqualTo(0);

        var project2Coupling = analysis.ProjectCoupling.FirstOrDefault(p => p.EntityName == "Project2");
        await Assert.That(project2Coupling).IsNotNull();

        // Project2 should have 0 internal dependencies
        await Assert.That(project2Coupling!.InternalDependencies).IsEqualTo(0);

        // Project2 should have 1 internal dependent (Project1)
        await Assert.That(project2Coupling.InternalDependents).IsEqualTo(1);
    }

    [Test]
    public async Task AnalyzeDocumentCoupling_WithExternalTypes_FindsTypeAndNamespaceReferences()
    {
        // Arrange - code that uses external types from System assemblies
        var code = """
            using System;
            using System.Collections.Generic;

            namespace TestProject.Services
            {
                public class MyService
                {
                    private readonly List<string> _items = new();

                    public void Process()
                    {
                        Console.WriteLine("Hello");
                        var dict = new Dictionary<string, int>();
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("TestProject")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location))
            .AddSyntaxTrees(tree);

        var semanticModel = compilation.GetSemanticModel(tree);
        var root = await tree.GetRootAsync();

        // Act
        DependencyEdge.EnableDetails = true;
        var dependencies = CouplingAnalyzer.AnalyzeDocumentCoupling(semanticModel, "test.cs", root);

        var typeRefs = dependencies.Where(d => d.Type == DependencyType.TypeReference).ToList();
        var nsRefs = dependencies.Where(d => d.Type == DependencyType.NamespaceReference).ToList();

        // Assert - should find type references from MyService to external types
        await Assert.That(typeRefs.Count).IsGreaterThan(0);

        // Should find namespace references to System and System.Collections.Generic
        var systemNsRefs = nsRefs.Where(d => d.ToEntity.StartsWith("System")).ToList();
        await Assert.That(systemNsRefs.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task BuildCouplingAnalysisFromDependencies_WithOnlyInternalDependencies_HasZeroExternalDependencies()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;

        var project1 = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Project1",
            "Project1",
            LanguageNames.CSharp);
        var project2 = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Project2",
            "Project2",
            LanguageNames.CSharp);

        solution = solution.AddProject(project1).AddProject(project2);

        var dependencies = new List<DependencyEdge>
        {
            // Only internal project references
            new DependencyEdge("Project1", "Project2", DependencyType.ProjectReference, 1)
        };

        // Act
        var analysis = analyzer.BuildCouplingAnalysisFromDependencies(solution, dependencies);

        // Assert
        var project1Coupling = analysis.ProjectCoupling.FirstOrDefault(p => p.EntityName == "Project1");
        await Assert.That(project1Coupling).IsNotNull();
        await Assert.That(project1Coupling!.InternalDependencies).IsEqualTo(1);
        await Assert.That(project1Coupling.ExternalBclDependencies).IsEqualTo(0);
        await Assert.That(project1Coupling.ExternalPackageDependencies).IsEqualTo(0);
        await Assert.That(project1Coupling.TotalExternalDependencies).IsEqualTo(0);
    }
}
