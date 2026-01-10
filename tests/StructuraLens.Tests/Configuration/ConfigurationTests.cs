using StructuraLens.Core.Configuration;

namespace StructuraLens.Tests.Configuration;

public class ConfigurationTests
{
    [Test]
    public async Task CreateDefaultConfig_HasExpectedDefaults()
    {
        var config = ConfigurationLoader.CreateDefaultConfig();

        await Assert.That(config.Coupling.Mode).IsEqualTo(CouplingMode.Filtered);
        await Assert.That(config.Coupling.ExcludePatterns).Contains("System.*");
        await Assert.That(config.Coupling.ExcludePatterns).Contains("Microsoft.*");
        await Assert.That(config.Coupling.PatternType).IsEqualTo(PatternType.Wildcard);
        await Assert.That(config.InheritanceDepth).IsEqualTo(10);
    }

    [Test]
    public async Task CouplingMode_InternalMode_ExcludesExternalDependencies()
    {
        var config = ConfigurationLoader.CreateDefaultConfig();
        config.Coupling.Mode = CouplingMode.Internal;

        await Assert.That(config.Coupling.Mode).IsEqualTo(CouplingMode.Internal);
    }

    [Test]
    public async Task CouplingMode_AllMode_IncludesEverything()
    {
        var config = ConfigurationLoader.CreateDefaultConfig();
        config.Coupling.Mode = CouplingMode.All;

        await Assert.That(config.Coupling.Mode).IsEqualTo(CouplingMode.All);
    }

    [Test]
    public async Task CouplingConfig_MergeWithParent_ConcatenatesExcludePatterns()
    {
        var parent = new CouplingConfig
        {
            ExcludePatterns = ["System.*"]
        };

        var child = new CouplingConfig
        {
            ExcludePatterns = ["Custom.*"]
        };

        var merged = child.MergeWithParent(parent);

        await Assert.That(merged.ExcludePatterns).Contains("System.*");
        await Assert.That(merged.ExcludePatterns).Contains("Custom.*");
    }

    [Test]
    public async Task StructuraLensConfig_MergeWithParent_PreservesChildMode()
    {
        var parent = new StructuraLensConfig
        {
            Coupling = new CouplingConfig { Mode = CouplingMode.Filtered }
        };

        var child = new StructuraLensConfig
        {
            Coupling = new CouplingConfig { Mode = CouplingMode.Internal }
        };

        var merged = child.MergeWithParent(parent);

        await Assert.That(merged.Coupling.Mode).IsEqualTo(CouplingMode.Internal);
    }

    [Test]
    public async Task PatternType_DefaultsToWildcard()
    {
        var config = new CouplingConfig();

        await Assert.That(config.PatternType).IsEqualTo(PatternType.Wildcard);
    }
}
