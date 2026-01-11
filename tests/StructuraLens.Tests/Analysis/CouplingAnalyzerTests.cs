using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging.Abstractions;
using StructuraLens.Core.Analysis;
using StructuraLens.Core.Configuration;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Analysis;

public class CouplingAnalyzerTests
{
    private static StructuraLensConfig AllModeCouplingConfig => new()
    {
        Coupling = new CouplingConfig { Mode = CouplingMode.All }
    };

    [Test]
    public async Task AnalyzeProjectCouplingAsync_EmptyProject_ReturnsEmptyAnalysis()
    {
        var project = CreateTestProject("EmptyProject", "");
        
        var result = await CouplingAnalyzer.AnalyzeProjectCouplingAsync(project, ConfigurationLoader.CreateDefaultConfig(), NullLogger.Instance);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.AnalyzedEntity).Contains("EmptyProject");
        await Assert.That(result.AllDependencies.Count).IsEqualTo(0);
        await Assert.That(result.ProjectCoupling.Count).IsEqualTo(1); // The project itself
        await Assert.That(result.NamespaceCoupling.Count).IsEqualTo(0);
        await Assert.That(result.TypeCoupling.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AnalyzeProjectCouplingAsync_SimpleClass_AnalyzesTypeDependencies()
    {
        var code = @"
using System;
using System.Collections.Generic;

namespace TestProject.Models
{
    public class TestClass
    {
        public List<string> Items { get; set; } = new List<string>();
        
        public void ProcessItems()
        {
            Console.WriteLine($""Processing {Items.Count} items"");
        }
    }
}";

        var project = CreateTestProject("TestProject", code);
        
        var result = await CouplingAnalyzer.AnalyzeProjectCouplingAsync(project, ConfigurationLoader.CreateDefaultConfig(), NullLogger.Instance);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.AllDependencies.Count).IsGreaterThan(0);
        
        // Should find namespace dependencies (using statements)
        var namespaceDeps = result.AllDependencies.Where(d => d.Type == DependencyType.NamespaceReference).ToList();
        await Assert.That(namespaceDeps).IsNotEmpty();
        
        // Should find dependencies on System namespace
        var systemDeps = namespaceDeps.Where(d => d.ToEntity == "System").ToList();
        await Assert.That(systemDeps).IsNotEmpty();
    }

    [Test]
    public async Task AnalyzeProjectCouplingAsync_MultipleNamespaces_AnalyzesNamespaceCoupling()
    {
        var code = @"
namespace TestProject.Services
{
    using TestProject.Models;
    
    public class TestService
    {
        public void ProcessModel(TestModel model) { }
    }
}

namespace TestProject.Models
{
    public class TestModel
    {
        public string Name { get; set; } = """";
    }
}";

        var project = CreateTestProject("TestProject", code);
        
        var result = await CouplingAnalyzer.AnalyzeProjectCouplingAsync(project, ConfigurationLoader.CreateDefaultConfig(), NullLogger.Instance);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.NamespaceCoupling.Count).IsGreaterThanOrEqualTo(2);
        
        // Should find Services namespace
        var servicesNamespace = result.NamespaceCoupling.FirstOrDefault(nc => nc.EntityName == "TestProject.Services");
        await Assert.That(servicesNamespace).IsNotNull();
        
        // Services should depend on Models
        var outboundDeps = servicesNamespace!.OutboundDependencies;
        var dependsOnModels = outboundDeps.Any(d => d.ToEntity == "TestProject.Models");
        await Assert.That(dependsOnModels).IsTrue();
    }

    [Test]
    public async Task CouplingMetrics_CalculatesInstabilityCorrectly()
    {
        var outbound = new List<DependencyEdge>
        {
            new("A", "B", DependencyType.TypeReference, 1),
            new("A", "C", DependencyType.TypeReference, 1)
        };
        
        var inbound = new List<DependencyEdge>
        {
            new("D", "A", DependencyType.TypeReference, 1)
        };

        var metrics = new CouplingMetrics("A", DependencyType.TypeReference)
        {
            OutboundDependencies = outbound,
            InboundDependencies = inbound
        };

        // Ce = 2 (depends on B and C), Ca = 1 (D depends on A)
        // Instability = Ce / (Ce + Ca) = 2 / 3 = 0.67
        await Assert.That(metrics.EfferentCoupling).IsEqualTo(2);
        await Assert.That(metrics.AfferentCoupling).IsEqualTo(1);
        await Assert.That(Math.Abs(metrics.Instability - 0.6666666666666666)).IsLessThan(0.001);
    }

    [Test]
    public async Task CouplingMetrics_ZeroCoupling_ReturnsZeroInstability()
    {
        var metrics = new CouplingMetrics("A", DependencyType.TypeReference);

        await Assert.That(metrics.EfferentCoupling).IsEqualTo(0);
        await Assert.That(metrics.AfferentCoupling).IsEqualTo(0);
        await Assert.That(metrics.Instability).IsEqualTo(0);
        await Assert.That(metrics.TotalCouplingStrength).IsEqualTo(0);
    }

    [Test]
    public async Task DependencyEdge_CreationWithOptionalFields_StoresAllData()
    {
        var edge = new DependencyEdge(
            FromEntity: "NamespaceA",
            ToEntity: "NamespaceB", 
            Type: DependencyType.NamespaceReference,
            ReferenceCount: 5)
        {
            SourceLocation = "TestFile.cs:42",
            ReferencedSymbol = "TestClass"
        };

        await Assert.That(edge.FromEntity).IsEqualTo("NamespaceA");
        await Assert.That(edge.ToEntity).IsEqualTo("NamespaceB");
        await Assert.That(edge.Type).IsEqualTo(DependencyType.NamespaceReference);
        await Assert.That(edge.ReferenceCount).IsEqualTo(5);
        await Assert.That(edge.SourceLocation).IsEqualTo("TestFile.cs:42");
        await Assert.That(edge.ReferencedSymbol).IsEqualTo("TestClass");
    }

    [Test]
    public async Task CouplingSummary_CalculatesAveragesCorrectly()
    {
        var projectMetrics = new List<CouplingMetrics>
        {
            new("Project1", DependencyType.ProjectReference)
            {
                OutboundDependencies = [new("Project1", "Lib1", DependencyType.ProjectReference, 1)],
                InboundDependencies = []
            }
        };

        var namespaceMetrics = new List<CouplingMetrics>
        {
            new("NS1", DependencyType.NamespaceReference)
            {
                OutboundDependencies = [new("NS1", "NS2", DependencyType.NamespaceReference, 2)],
                InboundDependencies = [new("NS3", "NS1", DependencyType.NamespaceReference, 1)]
            },
            new("NS2", DependencyType.NamespaceReference)
            {
                OutboundDependencies = [],
                InboundDependencies = [new("NS1", "NS2", DependencyType.NamespaceReference, 2)]
            }
        };

        var allDependencies = new List<DependencyEdge>
        {
            new("Project1", "Lib1", DependencyType.ProjectReference, 1),
            new("NS1", "NS2", DependencyType.NamespaceReference, 2),
            new("NS3", "NS1", DependencyType.NamespaceReference, 1)
        };

        var summary = new CouplingSummary
        {
            TotalDependencies = allDependencies.Count,
            AverageEfferentCoupling = projectMetrics.Concat(namespaceMetrics).Average(m => m.EfferentCoupling),
            AverageAfferentCoupling = projectMetrics.Concat(namespaceMetrics).Average(m => m.AfferentCoupling),
            AverageInstability = projectMetrics.Concat(namespaceMetrics).Average(m => m.Instability),
            MostCoupledEntity = projectMetrics.Concat(namespaceMetrics).OrderByDescending(m => m.TotalCouplingStrength).First().EntityName,
            MostUnstableEntity = projectMetrics.Concat(namespaceMetrics).OrderByDescending(m => m.Instability).First().EntityName
        };

        await Assert.That(summary.TotalDependencies).IsEqualTo(3);
        await Assert.That(summary.MostCoupledEntity).IsEqualTo("NS1"); // Has 3 total coupling strength
        await Assert.That(summary.MostUnstableEntity).IsEqualTo("Project1"); // Instability = 1.0
    }

    [Test]
    public async Task AnalyzeProjectCouplingAsync_FileScopedNamespace_CollectsNamespaceDependencies()
    {
        var code = @"
using System;
using System.Collections.Generic;

namespace TestProject.Services;

public class MyService
{
    public List<string> GetItems() => new List<string>();
}";

        var project = CreateTestProject("TestProject", code);
        
        var result = await CouplingAnalyzer.AnalyzeProjectCouplingAsync(project, AllModeCouplingConfig, NullLogger.Instance);

        await Assert.That(result).IsNotNull();
        
        // Should find namespace dependencies from using statements
        var namespaceDeps = result.AllDependencies.Where(d => d.Type == DependencyType.NamespaceReference).ToList();
        await Assert.That(namespaceDeps).IsNotEmpty();
        
        // Should have deps from TestProject.Services to System namespaces
        var fromServices = namespaceDeps.Where(d => d.FromEntity == "TestProject.Services").ToList();
        await Assert.That(fromServices).IsNotEmpty();
        await Assert.That(fromServices.Any(d => d.ToEntity == "System")).IsTrue();
        await Assert.That(fromServices.Any(d => d.ToEntity == "System.Collections.Generic")).IsTrue();
    }

    [Test]
    public async Task AnalyzeProjectCouplingAsync_InternalTypeReference_CreatesNamespaceDependency()
    {
        var code = @"
namespace TestProject.Models
{
    public class Person { public string Name { get; set; } = """"; }
}

namespace TestProject.Services
{
    public class PersonService
    {
        public Models.Person GetPerson() => new Models.Person();
    }
}";

        var project = CreateTestProject("TestProject", code);
        
        var result = await CouplingAnalyzer.AnalyzeProjectCouplingAsync(project, ConfigurationLoader.CreateDefaultConfig(), NullLogger.Instance);

        await Assert.That(result).IsNotNull();
        
        // Should find type references
        var typeDeps = result.AllDependencies.Where(d => d.Type == DependencyType.TypeReference).ToList();
        await Assert.That(typeDeps).IsNotEmpty();
        
        // Should find namespace coupling from Services to Models
        var servicesNs = result.NamespaceCoupling.FirstOrDefault(nc => nc.EntityName == "TestProject.Services");
        await Assert.That(servicesNs).IsNotNull();
        await Assert.That(servicesNs!.OutboundDependencies.Any(d => d.ToEntity == "TestProject.Models")).IsTrue();
    }

    [Test]
    public async Task AnalyzeProjectCouplingAsync_UsingInsideNamespace_CollectsDependencies()
    {
        var code = @"
namespace TestProject.Services
{
    using System;
    using System.Text;
    
    public class StringService
    {
        public string Build() => new StringBuilder().ToString();
    }
}";

        var project = CreateTestProject("TestProject", code);
        
        var result = await CouplingAnalyzer.AnalyzeProjectCouplingAsync(project, AllModeCouplingConfig, NullLogger.Instance);

        await Assert.That(result).IsNotNull();
        
        var namespaceDeps = result.AllDependencies.Where(d => d.Type == DependencyType.NamespaceReference).ToList();
        await Assert.That(namespaceDeps).IsNotEmpty();
        
        // Usings inside namespace should still be collected
        var fromServices = namespaceDeps.Where(d => d.FromEntity == "TestProject.Services").ToList();
        await Assert.That(fromServices.Any(d => d.ToEntity == "System")).IsTrue();
        await Assert.That(fromServices.Any(d => d.ToEntity == "System.Text")).IsTrue();
    }

    [Test]
    public async Task AnalyzeProjectCouplingAsync_NoSelfLoops_InNamespaceDependencies()
    {
        var code = @"
namespace TestProject.Models
{
    public class Person { public string Name { get; set; } = """"; }
    public class Employee : Person { public int Id { get; set; } }
}";

        var project = CreateTestProject("TestProject", code);
        
        var result = await CouplingAnalyzer.AnalyzeProjectCouplingAsync(project, ConfigurationLoader.CreateDefaultConfig(), NullLogger.Instance);

        await Assert.That(result).IsNotNull();
        
        // Should not have self-referencing namespace dependencies
        var selfLoops = result.AllDependencies
            .Where(d => d.Type == DependencyType.NamespaceReference)
            .Where(d => d.FromEntity == d.ToEntity)
            .ToList();
        await Assert.That(selfLoops).IsEmpty();
    }

    private static Project CreateTestProject(string projectName, string code)
    {
        var projectId = ProjectId.CreateNewId();
        var solution = new AdhocWorkspace().CurrentSolution
            .AddProject(projectId, projectName, projectName, LanguageNames.CSharp);

        if (!string.IsNullOrEmpty(code))
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, "TestFile.cs", SourceText.From(code));
        }

        return solution.GetProject(projectId)!;
    }
}