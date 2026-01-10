using StructuraLens.Core.Analysis;
using StructuraLens.Core.Configuration;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Analysis;

public class DependencyFilterTests
{
    [Test]
    public async Task FilterDependencies_InternalMode_ExcludesExternalDependencies()
    {
        var config = new CouplingConfig { Mode = CouplingMode.Internal };
        var projectNames = new[] { "MyProject" };

        var dependencies = new List<DependencyEdge>
        {
            new("MyProject.Services", "MyProject.Models", DependencyType.NamespaceReference, 1),
            new("MyProject.Services", "System.Collections.Generic", DependencyType.NamespaceReference, 1),
            new("MyProject.Services", "Newtonsoft.Json", DependencyType.NamespaceReference, 1)
        };

        var filtered = DependencyFilter.FilterDependencies(dependencies, config, projectNames);

        // Only internal dependencies should remain
        await Assert.That(filtered.Count).IsEqualTo(1);
        await Assert.That(filtered[0].ToEntity).IsEqualTo("MyProject.Models");
    }

    [Test]
    public async Task FilterDependencies_FilteredMode_ExcludesSystemAndMicrosoft()
    {
        var config = new CouplingConfig
        {
            Mode = CouplingMode.Filtered,
            ExcludePatterns = ["System.*", "Microsoft.*"]
        };
        var projectNames = new[] { "MyProject" };

        var dependencies = new List<DependencyEdge>
        {
            new("MyProject.Services", "MyProject.Models", DependencyType.NamespaceReference, 1),
            new("MyProject.Services", "System.Collections.Generic", DependencyType.NamespaceReference, 1),
            new("MyProject.Services", "Microsoft.Extensions.Logging", DependencyType.NamespaceReference, 1),
            new("MyProject.Services", "Newtonsoft.Json", DependencyType.NamespaceReference, 1)
        };

        var filtered = DependencyFilter.FilterDependencies(dependencies, config, projectNames);

        // System and Microsoft should be excluded, but Newtonsoft.Json should remain
        await Assert.That(filtered.Count).IsEqualTo(2);
        await Assert.That(filtered.Any(d => d.ToEntity == "MyProject.Models")).IsTrue();
        await Assert.That(filtered.Any(d => d.ToEntity == "Newtonsoft.Json")).IsTrue();
        await Assert.That(filtered.Any(d => d.ToEntity.StartsWith("System"))).IsFalse();
        await Assert.That(filtered.Any(d => d.ToEntity.StartsWith("Microsoft"))).IsFalse();
    }

    [Test]
    public async Task FilterDependencies_AllMode_IncludesEverything()
    {
        var config = new CouplingConfig { Mode = CouplingMode.All };
        var projectNames = new[] { "MyProject" };

        var dependencies = new List<DependencyEdge>
        {
            new("MyProject.Services", "MyProject.Models", DependencyType.NamespaceReference, 1),
            new("MyProject.Services", "System.Collections.Generic", DependencyType.NamespaceReference, 1),
            new("MyProject.Services", "Microsoft.Extensions.Logging", DependencyType.NamespaceReference, 1),
            new("MyProject.Services", "Newtonsoft.Json", DependencyType.NamespaceReference, 1)
        };

        var filtered = DependencyFilter.FilterDependencies(dependencies, config, projectNames);

        await Assert.That(filtered.Count).IsEqualTo(4);
    }

    [Test]
    public async Task FilterDependencies_CustomExcludePattern_WorksWithWildcard()
    {
        var config = new CouplingConfig
        {
            Mode = CouplingMode.Filtered,
            ExcludePatterns = ["*.Tests", "Test*"],
            PatternType = PatternType.Wildcard
        };
        var projectNames = new[] { "MyProject" };

        var dependencies = new List<DependencyEdge>
        {
            new("MyProject", "MyProject.Tests", DependencyType.NamespaceReference, 1),
            new("MyProject", "TestHelper", DependencyType.NamespaceReference, 1),
            new("MyProject", "MyProject.Core", DependencyType.NamespaceReference, 1)
        };

        var filtered = DependencyFilter.FilterDependencies(dependencies, config, projectNames);

        await Assert.That(filtered.Count).IsEqualTo(1);
        await Assert.That(filtered[0].ToEntity).IsEqualTo("MyProject.Core");
    }

    [Test]
    public async Task FilterDependencies_IncludePatterns_OverrideExcludePatterns()
    {
        var config = new CouplingConfig
        {
            Mode = CouplingMode.Filtered,
            ExcludePatterns = ["System.*"],
            IncludePatterns = ["System.Text.Json"]
        };
        var projectNames = new[] { "MyProject" };

        var dependencies = new List<DependencyEdge>
        {
            new("MyProject", "System.Collections.Generic", DependencyType.NamespaceReference, 1),
            new("MyProject", "System.Text.Json", DependencyType.NamespaceReference, 1),
            new("MyProject", "System.Linq", DependencyType.NamespaceReference, 1)
        };

        var filtered = DependencyFilter.FilterDependencies(dependencies, config, projectNames);

        // Only System.Text.Json should be included because it's in includePatterns
        await Assert.That(filtered.Count).IsEqualTo(1);
        await Assert.That(filtered[0].ToEntity).IsEqualTo("System.Text.Json");
    }

    [Test]
    public async Task FilterDependencies_ProjectReferences_AlwaysIncluded()
    {
        var config = new CouplingConfig { Mode = CouplingMode.Internal };
        var projectNames = new[] { "ProjectA", "ProjectB" };

        var dependencies = new List<DependencyEdge>
        {
            new("ProjectA", "ProjectB", DependencyType.ProjectReference, 1)
        };

        var filtered = DependencyFilter.FilterDependencies(dependencies, config, projectNames);

        await Assert.That(filtered.Count).IsEqualTo(1);
    }

    [Test]
    public async Task FilterDependencies_EmptyDependencies_ReturnsEmpty()
    {
        var config = new CouplingConfig { Mode = CouplingMode.All };
        var projectNames = new[] { "MyProject" };

        var filtered = DependencyFilter.FilterDependencies([], config, projectNames);

        await Assert.That(filtered.Count).IsEqualTo(0);
    }
}
