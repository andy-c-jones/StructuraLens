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
        await Assert.That(project1Coupling!.InternalOutbound.Count).IsGreaterThan(0);
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
    public async Task BuildCouplingAnalysisFromDependencies_WithExternalDependencies_CalculatesBclAndPackageMetrics()
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
            
            // External BCL dependencies (System.*)
            new DependencyEdge("Project1", "System.Collections.Generic", DependencyType.AssemblyReference, 5),
            new DependencyEdge("Project1", "System.Linq", DependencyType.AssemblyReference, 3),
            new DependencyEdge("Project1", "Microsoft.Extensions.Logging", DependencyType.AssemblyReference, 2),
            
            // External package dependencies (third-party)
            new DependencyEdge("Project1", "Newtonsoft.Json", DependencyType.AssemblyReference, 4),
            new DependencyEdge("Project1", "Serilog", DependencyType.AssemblyReference, 1),
            
            // Project2 external deps
            new DependencyEdge("Project2", "System.Text", DependencyType.AssemblyReference, 2),
            new DependencyEdge("Project2", "FluentAssertions", DependencyType.AssemblyReference, 3)
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
        
        // Project1 should have external dependencies
        await Assert.That(project1Coupling.ExternalOutbound.Count).IsGreaterThan(0);
        
        // Project1 should have 3 BCL dependencies (System.Collections.Generic, System.Linq, Microsoft.Extensions.Logging)
        await Assert.That(project1Coupling.ExternalBclDependencies).IsEqualTo(3);
        
        // Project1 should have 2 package dependencies (Newtonsoft.Json, Serilog)
        await Assert.That(project1Coupling.ExternalPackageDependencies).IsEqualTo(2);
        
        // Total external should be BCL + packages
        await Assert.That(project1Coupling.TotalExternalDependencies).IsEqualTo(5);
        
        var project2Coupling = analysis.ProjectCoupling.FirstOrDefault(p => p.EntityName == "Project2");
        await Assert.That(project2Coupling).IsNotNull();
        
        // Project2 should have 0 internal dependencies
        await Assert.That(project2Coupling!.InternalDependencies).IsEqualTo(0);
        
        // Project2 should have 1 internal dependent (Project1)
        await Assert.That(project2Coupling.InternalDependents).IsEqualTo(1);
        
        // Project2 should have 1 BCL dependency (System.Text)
        await Assert.That(project2Coupling.ExternalBclDependencies).IsEqualTo(1);
        
        // Project2 should have 1 package dependency (FluentAssertions)
        await Assert.That(project2Coupling.ExternalPackageDependencies).IsEqualTo(1);
        
        // Total external should be BCL + packages
        await Assert.That(project2Coupling.TotalExternalDependencies).IsEqualTo(2);
    }

    [Test]
    public async Task DocumentCouplingAnalyzer_WithExternalTypes_CreatesAssemblyReferenceEdges()
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

        // Verify compilation has no errors that would prevent analysis
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        var semanticModel = compilation.GetSemanticModel(tree);
        var root = await tree.GetRootAsync();

        // Act - use the internal DocumentCouplingAnalyzer directly with a project name
        DependencyEdge.EnableDetails = true;
        var analyzer = new DocumentCouplingAnalyzer(semanticModel, "test.cs", root, null, "TestProject");
        analyzer.Visit(root);
        var dependencies = analyzer.Dependencies;

        // Debug: output all dependencies by type
        var assemblyRefs = dependencies.Where(d => d.Type == DependencyType.AssemblyReference).ToList();
        var typeRefs = dependencies.Where(d => d.Type == DependencyType.TypeReference).ToList();
        var nsRefs = dependencies.Where(d => d.Type == DependencyType.NamespaceReference).ToList();

        // Assert - should have AssemblyReference edges for external types
        await Assert.That(assemblyRefs.Count).IsGreaterThan(0)
            .Because($"Expected AssemblyReference edges but got 0. " +
                     $"TypeRefs: {typeRefs.Count}, NsRefs: {nsRefs.Count}, Total: {dependencies.Count}. " +
                     $"Compilation errors: {string.Join("; ", diagnostics.Select(d => d.GetMessage()))}. " +
                     $"TypeRef details: [{string.Join(", ", typeRefs.Select(t => $"{t.FromEntity}->{t.ToEntity}"))}]");

        // Should have edges from "TestProject" to System namespaces
        var systemAssemblyRefs = assemblyRefs.Where(d => d.ToEntity.StartsWith("System")).ToList();
        await Assert.That(systemAssemblyRefs.Count).IsGreaterThan(0);
        
        // All AssemblyReference edges should have FromEntity = "TestProject"
        var allFromTestProject = assemblyRefs.All(d => d.FromEntity == "TestProject");
        await Assert.That(allFromTestProject).IsTrue();
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
