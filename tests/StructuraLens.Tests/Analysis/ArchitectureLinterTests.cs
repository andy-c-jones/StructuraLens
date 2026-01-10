using StructuraLens.Core.Analysis;
using StructuraLens.Core.Configuration;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Analysis;

public class ArchitectureLinterTests
{
    private static CouplingAnalysis CreateTestCoupling(params DependencyEdge[] dependencies)
    {
        return new CouplingAnalysis("Test", DateTime.UtcNow)
        {
            AllDependencies = dependencies.ToList(),
            ProjectCoupling = [],
            NamespaceCoupling = [],
            TypeCoupling = []
        };
    }

    [Test]
    public async Task Evaluate_NoRules_ReturnsEmptyResults()
    {
        var coupling = CreateTestCoupling(
            new DependencyEdge("MyApp", "System.Console", DependencyType.TypeReference, 1)
        );

        var result = ArchitectureLinter.Evaluate(coupling, []);

        await Assert.That(result.RulesEvaluated).IsEqualTo(0);
        await Assert.That(result.Violations).IsEmpty();
        await Assert.That(result.Passed).IsTrue();
    }

    [Test]
    public async Task Evaluate_DisabledRule_IsSkipped()
    {
        var coupling = CreateTestCoupling(
            new DependencyEdge("MyApp", "ForbiddenLib", DependencyType.TypeReference, 1)
        );

        var rules = new List<ArchitectureRule>
        {
            new()
            {
                Id = "NO-FORBIDDEN",
                Enabled = false,
                Disallow = ["ForbiddenLib*"]
            }
        };

        var result = ArchitectureLinter.Evaluate(coupling, rules);

        await Assert.That(result.RulesEvaluated).IsEqualTo(0);
        await Assert.That(result.Violations).IsEmpty();
    }

    [Test]
    public async Task Evaluate_DisallowRule_DetectsViolation()
    {
        var coupling = CreateTestCoupling(
            new DependencyEdge("MyApp.Services", "BadLibrary.Core", DependencyType.TypeReference, 1),
            new DependencyEdge("MyApp.Services", "GoodLibrary.Core", DependencyType.TypeReference, 1)
        );

        var rules = new List<ArchitectureRule>
        {
            new()
            {
                Id = "NO-BAD-LIB",
                Description = "Do not use BadLibrary",
                Severity = RuleSeverity.Error,
                Disallow = ["BadLibrary.*"]
            }
        };

        var result = ArchitectureLinter.Evaluate(coupling, rules);

        await Assert.That(result.RulesEvaluated).IsEqualTo(1);
        await Assert.That(result.Violations.Count).IsEqualTo(1);
        await Assert.That(result.ErrorCount).IsEqualTo(1);
        await Assert.That(result.Passed).IsFalse();
        await Assert.That(result.Violations[0].RuleId).IsEqualTo("NO-BAD-LIB");
        await Assert.That(result.Violations[0].ToEntity).IsEqualTo("BadLibrary.Core");
    }

    [Test]
    public async Task Evaluate_AllowRule_DetectsUnauthorizedDependency()
    {
        var coupling = CreateTestCoupling(
            new DependencyEdge("MyApp.Core", "ApprovedLib.Core", DependencyType.TypeReference, 1),
            new DependencyEdge("MyApp.Core", "RandomLib.Stuff", DependencyType.TypeReference, 1)
        );

        var rules = new List<ArchitectureRule>
        {
            new()
            {
                Id = "ONLY-APPROVED",
                Description = "Only approved libraries allowed",
                Severity = RuleSeverity.Warning,
                From = "MyApp.*",
                Allow = ["ApprovedLib.*", "MyApp.*"]
            }
        };

        var result = ArchitectureLinter.Evaluate(coupling, rules);

        await Assert.That(result.Violations.Count).IsEqualTo(1);
        await Assert.That(result.WarningCount).IsEqualTo(1);
        await Assert.That(result.Passed).IsTrue(); // Warnings don't fail
        await Assert.That(result.Violations[0].ToEntity).IsEqualTo("RandomLib.Stuff");
    }

    [Test]
    public async Task Evaluate_FromPattern_FiltersDependencies()
    {
        var coupling = CreateTestCoupling(
            new DependencyEdge("MyApp.UI", "Database.Core", DependencyType.TypeReference, 1),
            new DependencyEdge("MyApp.Services", "Database.Core", DependencyType.TypeReference, 1)
        );

        var rules = new List<ArchitectureRule>
        {
            new()
            {
                Id = "UI-NO-DB",
                Description = "UI layer should not access database directly",
                Severity = RuleSeverity.Error,
                From = "*.UI",
                Disallow = ["Database.*"]
            }
        };

        var result = ArchitectureLinter.Evaluate(coupling, rules);

        // Only UI dependency should be flagged
        await Assert.That(result.Violations.Count).IsEqualTo(1);
        await Assert.That(result.Violations[0].FromEntity).IsEqualTo("MyApp.UI");
    }

    [Test]
    public async Task Evaluate_DependencyTypeFilter_OnlyMatchesSpecifiedType()
    {
        var coupling = CreateTestCoupling(
            new DependencyEdge("MyApp", "External", DependencyType.ProjectReference, 1),
            new DependencyEdge("MyApp", "External", DependencyType.TypeReference, 1)
        );

        var rules = new List<ArchitectureRule>
        {
            new()
            {
                Id = "NO-PROJECT-REF",
                Severity = RuleSeverity.Error,
                DependencyType = RuleDependencyType.Project,
                Disallow = ["External"]
            }
        };

        var result = ArchitectureLinter.Evaluate(coupling, rules);

        // Only the project reference should be flagged
        await Assert.That(result.Violations.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Evaluate_WildcardPatterns_MatchCorrectly()
    {
        var coupling = CreateTestCoupling(
            new DependencyEdge("MyApp", "System.Collections.Generic", DependencyType.NamespaceReference, 1),
            new DependencyEdge("MyApp", "System.Text.Json", DependencyType.NamespaceReference, 1),
            new DependencyEdge("MyApp", "Newtonsoft.Json", DependencyType.NamespaceReference, 1)
        );

        var rules = new List<ArchitectureRule>
        {
            new()
            {
                Id = "NO-NEWTONSOFT",
                Description = "Use System.Text.Json instead of Newtonsoft",
                Severity = RuleSeverity.Warning,
                Disallow = ["Newtonsoft.*"]
            }
        };

        var result = ArchitectureLinter.Evaluate(coupling, rules);

        await Assert.That(result.Violations.Count).IsEqualTo(1);
        await Assert.That(result.Violations[0].ToEntity).IsEqualTo("Newtonsoft.Json");
    }

    [Test]
    public async Task Evaluate_MultipleRules_AllEvaluated()
    {
        var coupling = CreateTestCoupling(
            new DependencyEdge("MyApp.UI", "Database.Core", DependencyType.TypeReference, 1),
            new DependencyEdge("MyApp.Core", "LegacyLib.Old", DependencyType.TypeReference, 1)
        );

        var rules = new List<ArchitectureRule>
        {
            new()
            {
                Id = "UI-NO-DB",
                Severity = RuleSeverity.Error,
                From = "*.UI",
                Disallow = ["Database.*"]
            },
            new()
            {
                Id = "NO-LEGACY",
                Severity = RuleSeverity.Warning,
                Disallow = ["LegacyLib.*"]
            }
        };

        var result = ArchitectureLinter.Evaluate(coupling, rules);

        await Assert.That(result.RulesEvaluated).IsEqualTo(2);
        await Assert.That(result.Violations.Count).IsEqualTo(2);
        await Assert.That(result.ErrorCount).IsEqualTo(1);
        await Assert.That(result.WarningCount).IsEqualTo(1);
    }

    [Test]
    public async Task Evaluate_AllDependenciesAllowed_NoViolations()
    {
        var coupling = CreateTestCoupling(
            new DependencyEdge("MyApp.Core", "MyApp.Models", DependencyType.TypeReference, 1),
            new DependencyEdge("MyApp.Core", "System.String", DependencyType.TypeReference, 1)
        );

        var rules = new List<ArchitectureRule>
        {
            new()
            {
                Id = "CORE-DEPS",
                From = "MyApp.Core",
                Allow = ["MyApp.*", "System.*"]
            }
        };

        var result = ArchitectureLinter.Evaluate(coupling, rules);

        await Assert.That(result.Violations).IsEmpty();
        await Assert.That(result.Passed).IsTrue();
    }

    [Test]
    public async Task Evaluate_InfoSeverity_CountedCorrectly()
    {
        var coupling = CreateTestCoupling(
            new DependencyEdge("MyApp", "DeprecatedLib", DependencyType.TypeReference, 1)
        );

        var rules = new List<ArchitectureRule>
        {
            new()
            {
                Id = "DEPRECATED-WARNING",
                Severity = RuleSeverity.Info,
                Disallow = ["DeprecatedLib*"]
            }
        };

        var result = ArchitectureLinter.Evaluate(coupling, rules);

        await Assert.That(result.InfoCount).IsEqualTo(1);
        await Assert.That(result.ErrorCount).IsEqualTo(0);
        await Assert.That(result.WarningCount).IsEqualTo(0);
        await Assert.That(result.Passed).IsTrue(); // Info doesn't fail
    }
}
