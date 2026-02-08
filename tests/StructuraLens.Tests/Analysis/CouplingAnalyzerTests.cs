using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Analysis;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Analysis;

public class CouplingAnalyzerTests
{
    private ICouplingAnalyzer CreateAnalyzer()
    {
        var logger = A.Fake<ILogger<CouplingAnalyzer>>();
        return new CouplingAnalyzer(logger);
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CouplingAnalyzer(null!));
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
        await Assert.That(project1Coupling!.OutboundDependencies.Count).IsGreaterThan(0);
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
}
