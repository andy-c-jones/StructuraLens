using StructuraLens.Cli.Diff;
using StructuraLens.Core.Models;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace StructuraLens.Tests.Cli;

public sealed class DiffReportRendererTests
{
    [Test]
    public async Task RenderMarkdown_ErrorsIncrease_ShowsCriticalAlert()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffReport(baseErrors: 0, headErrors: 5);

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).Contains("🚨 **+5**");
    }

    [Test]
    public async Task RenderMarkdown_ErrorsDecrease_ShowsSuccess()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffReport(baseErrors: 10, headErrors: 3);

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).Contains("✅ -7");
    }

    [Test]
    public async Task RenderMarkdown_WarningsIncrease_ShowsWarning()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffReport(baseWarnings: 5, headWarnings: 15);

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).Contains("⚠️ **+10**");
    }

    [Test]
    public async Task RenderMarkdown_WarningsDecrease_ShowsSuccess()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffReport(baseWarnings: 20, headWarnings: 5);

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).Contains("✅ -15");
    }

    [Test]
    public async Task RenderMarkdown_SevereMaintainabilityDrop_ShowsCriticalIndicator()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffReport(baseMaintainability: 85.0, headMaintainability: 70.0);

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Should show severe decrease (🔴) for >10 point drop
        await Assert.That(markdown).Contains("🔴 **-15.0**");
    }

    [Test]
    public async Task RenderMarkdown_ModerateMaintainabilityDrop_ShowsWarning()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffReport(baseMaintainability: 80.0, headMaintainability: 73.0);

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Should show moderate decrease (⚠️) for 5-10 point drop
        await Assert.That(markdown).Contains("⚠️ **-7.0**");
    }

    [Test]
    public async Task RenderMarkdown_MaintainabilityIncrease_ShowsSuccess()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffReport(baseMaintainability: 70.0, headMaintainability: 80.0);

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).Contains("✅ +10.0");
    }

    [Test]
    public async Task RenderMarkdown_SignificantComplexityIncrease_ShowsWarning()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffReport(baseComplexity: 100, headComplexity: 160);

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Should show warning for >10% or >50 absolute increase
        await Assert.That(markdown).Contains("⚠️ **+60**");
    }

    [Test]
    public async Task RenderMarkdown_ProjectWithMaintainabilityDrop_HighlightsInTable()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var baseMetrics = new ProjectDiffMetrics
        {
            AvgMaintainabilityIndex = 85.0,
            CyclomaticComplexity = 100,
            LinesOfCode = 1000,
            Warnings = 5
        };
        var headMetrics = new ProjectDiffMetrics
        {
            AvgMaintainabilityIndex = 70.0,
            CyclomaticComplexity = 120,
            LinesOfCode = 1200,
            Warnings = 10
        };
        
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata
            {
                CommitSha = "abc123",
                BranchName = "main",
                AnalyzedAt = DateTime.UtcNow
            },
            Head = new DiffMetadata
            {
                CommitSha = "def456",
                BranchName = "feature",
                AnalyzedAt = DateTime.UtcNow
            },
            Totals = new DiffTotals
            {
                BaseProjects = 1, HeadProjects = 1,
                BaseTypes = 10, HeadTypes = 10,
                BaseMethods = 50, HeadMethods = 50,
                BaseCyclomaticComplexity = 100, HeadCyclomaticComplexity = 120,
                BaseLinesOfCode = 1000, HeadLinesOfCode = 1200,
                BaseAvgMaintainabilityIndex = 85.0, HeadAvgMaintainabilityIndex = 70.0,
                BaseErrors = 0, HeadErrors = 0,
                BaseWarnings = 5, HeadWarnings = 10,
                BaseInfo = 0, HeadInfo = 0,
                BaseHidden = 0, HeadHidden = 0
            },
            Projects = new[]
            {
                new ProjectDiff
                {
                    Name = "TestProject",
                    Base = baseMetrics,
                    Head = headMetrics
                }
            }
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Project table should show severe MI drop and warning increase
        await Assert.That(markdown).Contains("### Top Maintainability Changes");
        await Assert.That(markdown).Contains("TestProject");
        await Assert.That(markdown).Contains("🔴 **-15.0**"); // MI delta
        await Assert.That(markdown).Contains("⚠️ **+5**"); // Warnings delta in project row
    }

    [Test]
    public async Task RenderMarkdown_NoChanges_ShowsZeroWithoutEmoji()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffReport(
            baseErrors: 5, headErrors: 5,
            baseWarnings: 10, headWarnings: 10,
            baseMaintainability: 80.0, headMaintainability: 80.0
        );

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Should show plain "0" for unchanged metrics
        var lines = markdown.Split('\n');
        var errorLine = lines.First(l => l.Contains("| Errors |"));
        var warningLine = lines.First(l => l.Contains("| Warnings |"));
        var miLine = lines.First(l => l.Contains("| Avg Maintainability |"));
        
        await Assert.That(errorLine).Contains("| 0 |");
        await Assert.That(warningLine).Contains("| 0 |");
        await Assert.That(miLine).Contains("| 0 |");
    }

    [Test]
    public async Task RenderMarkdown_IncludesAllSections_InCorrectOrder()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffReport();

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Verify all sections are present in the correct order
        await Assert.That(markdown).Contains("## StructuraLens Diff Summary");
        
        var diagnosticsIndex = markdown.IndexOf("### Diagnostics");
        var topChangesIndex = markdown.IndexOf("### Top Maintainability Changes");
        var metricsIndex = markdown.IndexOf("### Top Level Metrics");
        
        // Diagnostics should come first
        await Assert.That(diagnosticsIndex).IsGreaterThan(0);
        
        // Top Level Metrics should come last
        await Assert.That(metricsIndex).IsGreaterThan(diagnosticsIndex);
        
        // Top Maintainability Changes should be between Diagnostics and Top Level Metrics (if projects exist)
        // Since CreateDiffReport has no projects, Top Maintainability Changes won't be present
        // but Top Level Metrics should still be there
        await Assert.That(markdown).Contains("### Top Level Metrics");
    }

    private static AnalysisDiffReport CreateDiffReport(
        int baseErrors = 0, int headErrors = 0,
        int baseWarnings = 0, int headWarnings = 0,
        double baseMaintainability = 80.0, double headMaintainability = 80.0,
        int baseComplexity = 100, int headComplexity = 100)
    {
        return new AnalysisDiffReport
        {
            Base = new DiffMetadata
            {
                CommitSha = "abc123",
                BranchName = "main",
                AnalyzedAt = DateTime.UtcNow
            },
            Head = new DiffMetadata
            {
                CommitSha = "def456",
                BranchName = "feature",
                AnalyzedAt = DateTime.UtcNow
            },
            Totals = new DiffTotals
            {
                BaseProjects = 1, HeadProjects = 1,
                BaseTypes = 10, HeadTypes = 10,
                BaseMethods = 50, HeadMethods = 50,
                BaseCyclomaticComplexity = baseComplexity, HeadCyclomaticComplexity = headComplexity,
                BaseLinesOfCode = 1000, HeadLinesOfCode = 1000,
                BaseAvgMaintainabilityIndex = baseMaintainability, HeadAvgMaintainabilityIndex = headMaintainability,
                BaseErrors = baseErrors, HeadErrors = headErrors,
                BaseWarnings = baseWarnings, HeadWarnings = headWarnings,
                BaseInfo = 0, HeadInfo = 0,
                BaseHidden = 0, HeadHidden = 0
            },
            Projects = []
        };
    }
}

