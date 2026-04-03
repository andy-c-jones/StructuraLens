using StructuraLens.Cli.Diff;
using StructuraLens.Core.Models;

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
                BaseProjects = 1,
                HeadProjects = 1,
                BaseTypes = 10,
                HeadTypes = 10,
                BaseMethods = 50,
                HeadMethods = 50,
                BaseCyclomaticComplexity = 100,
                HeadCyclomaticComplexity = 120,
                BaseLinesOfCode = 1000,
                HeadLinesOfCode = 1200,
                BaseAvgMaintainabilityIndex = 85.0,
                HeadAvgMaintainabilityIndex = 70.0,
                BaseErrors = 0,
                HeadErrors = 0,
                BaseWarnings = 5,
                HeadWarnings = 10,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
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
        await Assert.That(markdown).Contains("### Maintainability Changes");
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
    public async Task RenderMarkdown_LightweightMode_HidesComplexityAndMaintainabilitySections()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffReport(
            baseMaintainability: 80.0,
            headMaintainability: 70.0,
            baseComplexity: 100,
            headComplexity: 180)
            with { HasComplexityMetrics = false };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).DoesNotContain("### Maintainability Changes");
        await Assert.That(markdown).DoesNotContain("| Cyclomatic Complexity |");
        await Assert.That(markdown).DoesNotContain("| Lines of Code |");
        await Assert.That(markdown).DoesNotContain("| Avg Maintainability |");
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
        var topChangesIndex = markdown.IndexOf("### Maintainability Changes");
        var metricsIndex = markdown.IndexOf("### Overall Metrics");

        // Diagnostics should come first
        await Assert.That(diagnosticsIndex).IsGreaterThan(0);

        // Overall Metrics should come last
        await Assert.That(metricsIndex).IsGreaterThan(diagnosticsIndex);

        // Maintainability Changes should be between Diagnostics and Overall Metrics (if projects exist)
        // Since CreateDiffReport has no projects, Maintainability Changes won't be present
        // but Overall Metrics should still be there
        await Assert.That(markdown).Contains("### Overall Metrics");
    }

    [Test]
    public async Task RenderMarkdown_WithNewDiagnostics_ShowsTopNewDiagnosticsSection()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals
            {
                BaseProjects = 1,
                HeadProjects = 1,
                BaseTypes = 10,
                HeadTypes = 10,
                BaseMethods = 50,
                HeadMethods = 50,
                BaseCyclomaticComplexity = 100,
                HeadCyclomaticComplexity = 100,
                BaseLinesOfCode = 1000,
                HeadLinesOfCode = 1000,
                BaseAvgMaintainabilityIndex = 80.0,
                HeadAvgMaintainabilityIndex = 80.0,
                BaseErrors = 0,
                HeadErrors = 2,
                BaseWarnings = 0,
                HeadWarnings = 3,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
            },
            Projects = [],
            Diagnostics = new DiagnosticDiffSummary
            {
                BaseErrors = 0,
                HeadErrors = 2,
                BaseWarnings = 0,
                HeadWarnings = 3,
                TopNewErrors =
                [
                    new DiagnosticDiffItem
                    {
                        Project = "TestProject",
                        Id = "CS0103",
                        Severity = "Error",
                        Message = "The name 'foo' does not exist in the current context",
                        File = "Program.cs",
                        Line = 42,
                        Column = 10
                    },
                    new DiagnosticDiffItem
                    {
                        Project = "TestProject",
                        Id = "CS0246",
                        Severity = "Error",
                        Message = "The type or namespace name 'Bar' could not be found",
                        File = "Helper.cs",
                        Line = 15,
                        Column = 5
                    }
                ],
                TopNewWarnings =
                [
                    new DiagnosticDiffItem
                    {
                        Project = "TestProject",
                        Id = "CS8602",
                        Severity = "Warning",
                        Message = "Dereference of a possibly null reference",
                        File = "Service.cs",
                        Line = 100,
                        Column = 20
                    }
                ]
            }
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).Contains("### New Diagnostics");
        await Assert.That(markdown).Contains("🚨 **CS0103** in `TestProject`");
        await Assert.That(markdown).Contains("The name 'foo' does not exist");
        await Assert.That(markdown).Contains("Location: `Program.cs:42:10`");
        await Assert.That(markdown).Contains("🚨 **CS0246** in `TestProject`");
        await Assert.That(markdown).Contains("⚠️ **CS8602** in `TestProject`");
        await Assert.That(markdown).Contains("Dereference of a possibly null reference");
    }

    [Test]
    public async Task RenderMarkdown_WithNoNewDiagnostics_OmitsTopNewDiagnosticsSection()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffReport();

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Should not have the section if no new diagnostics
        await Assert.That(markdown).DoesNotContain("### New Diagnostics");
    }

    [Test]
    public async Task RenderMarkdown_WithMoreThan20NewDiagnostics_ShowsTop20ByPriorityWithWarning()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        // Create 10 errors (1-10)
        var topNewErrors = Enumerable.Range(1, 10).Select(i => new DiagnosticDiffItem
        {
            Project = $"Project{i}",
            Id = $"CS{i:D4}",
            Severity = "Error",
            Message = $"Error message {i}",
            File = $"File{i}.cs",
            Line = i,
            Column = 1
        }).ToList();

        // Create 15 warnings (11-25)
        var topNewWarnings = Enumerable.Range(11, 15).Select(i => new DiagnosticDiffItem
        {
            Project = $"Project{i}",
            Id = $"CS{i:D4}",
            Severity = "Warning",
            Message = $"Warning message {i}",
            File = $"File{i}.cs",
            Line = i,
            Column = 1
        }).ToList();

        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals
            {
                BaseProjects = 1,
                HeadProjects = 1,
                BaseTypes = 10,
                HeadTypes = 10,
                BaseMethods = 50,
                HeadMethods = 50,
                BaseCyclomaticComplexity = 100,
                HeadCyclomaticComplexity = 100,
                BaseLinesOfCode = 1000,
                HeadLinesOfCode = 1000,
                BaseAvgMaintainabilityIndex = 80.0,
                HeadAvgMaintainabilityIndex = 80.0,
                BaseErrors = 0,
                HeadErrors = 10,
                BaseWarnings = 0,
                HeadWarnings = 15,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
            },
            Projects = [],
            Diagnostics = new DiagnosticDiffSummary
            {
                BaseErrors = 0,
                HeadErrors = 10,
                BaseWarnings = 0,
                HeadWarnings = 15,
                TopNewErrors = topNewErrors,
                TopNewWarnings = topNewWarnings
            }
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Should show all 10 errors (priority 4) and only 10 warnings (priority 3) to reach the 20 item limit
        await Assert.That(markdown).Contains("### 🚨 New Diagnostics"); // Alarming emoji when >20
        await Assert.That(markdown).Contains("🚨 **CS0001**"); // Error 1
        await Assert.That(markdown).Contains("🚨 **CS0010**"); // Error 10
        await Assert.That(markdown).Contains("⚠️ **CS0011**"); // Warning 11 (first warning)
        await Assert.That(markdown).Contains("⚠️ **CS0020**"); // Warning 20 (10th warning)

        // Should not show warning 21 and beyond (the 21st+ items)
        await Assert.That(markdown).DoesNotContain("**CS0021**");
        await Assert.That(markdown).DoesNotContain("**CS0025**");

        // Should show the warning message about too many diagnostics
        await Assert.That(markdown).Contains("Too many diagnostic issues added to show all of them");
        await Assert.That(markdown).Contains("(25 total, showing 20)");
    }

    [Test]
    public async Task RenderMarkdown_WithInternalDependencyChanges_ShowsInternalDependenciesSection()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals
            {
                BaseProjects = 2,
                HeadProjects = 2,
                BaseTypes = 20,
                HeadTypes = 20,
                BaseMethods = 100,
                HeadMethods = 100,
                BaseCyclomaticComplexity = 200,
                HeadCyclomaticComplexity = 200,
                BaseLinesOfCode = 2000,
                HeadLinesOfCode = 2000,
                BaseAvgMaintainabilityIndex = 80.0,
                HeadAvgMaintainabilityIndex = 80.0,
                BaseErrors = 0,
                HeadErrors = 0,
                BaseWarnings = 0,
                HeadWarnings = 0,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
            },
            Projects =
            [
                new ProjectDiff
                {
                    Name = "ConsumerProject",
                    Base = new ProjectDiffMetrics
                    {
                        InternalDependencies = 1,
                        InternalDependents = 0,
                        DependencyRatio = 1.0,
                        AvgMaintainabilityIndex = 80.0,
                        CyclomaticComplexity = 100,
                        LinesOfCode = 1000,
                        Warnings = 0
                    },
                    Head = new ProjectDiffMetrics
                    {
                        InternalDependencies = 2,
                        InternalDependents = 0,
                        DependencyRatio = 2.0,
                        AvgMaintainabilityIndex = 80.0,
                        CyclomaticComplexity = 100,
                        LinesOfCode = 1000,
                        Warnings = 0
                    },
                    AddedInternalDependencies = ["ProviderProject"],
                    RemovedInternalDependencies = []
                },
                new ProjectDiff
                {
                    Name = "ProviderProject",
                    Base = new ProjectDiffMetrics
                    {
                        InternalDependencies = 0,
                        InternalDependents = 1,
                        DependencyRatio = 0.0,
                        AvgMaintainabilityIndex = 80.0,
                        CyclomaticComplexity = 100,
                        LinesOfCode = 1000,
                        Warnings = 0
                    },
                    Head = new ProjectDiffMetrics
                    {
                        InternalDependencies = 0,
                        InternalDependents = 2,
                        DependencyRatio = 0.0,
                        AvgMaintainabilityIndex = 80.0,
                        CyclomaticComplexity = 100,
                        LinesOfCode = 1000,
                        Warnings = 0
                    },
                    AddedInternalDependencies = [],
                    RemovedInternalDependencies = []
                }
            ]
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).Contains("### Internal Dependencies Changes");
        await Assert.That(markdown).Contains("ConsumerProject");
        await Assert.That(markdown).Contains("ProviderProject");
    }

    [Test]
    public async Task RenderMarkdown_WithNoDependencyChanges_OmitsInternalDependenciesSection()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals
            {
                BaseProjects = 1,
                HeadProjects = 1,
                BaseTypes = 10,
                HeadTypes = 10,
                BaseMethods = 50,
                HeadMethods = 50,
                BaseCyclomaticComplexity = 100,
                HeadCyclomaticComplexity = 100,
                BaseLinesOfCode = 1000,
                HeadLinesOfCode = 1000,
                BaseAvgMaintainabilityIndex = 80.0,
                HeadAvgMaintainabilityIndex = 80.0,
                BaseErrors = 0,
                HeadErrors = 0,
                BaseWarnings = 0,
                HeadWarnings = 0,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
            },
            Projects =
            [
                new ProjectDiff
                {
                    Name = "StableProject",
                    Base = new ProjectDiffMetrics
                    {
                        InternalDependencies = 1,
                        InternalDependents = 1,
                        DependencyRatio = 1.0,
                        AvgMaintainabilityIndex = 80.0,
                        CyclomaticComplexity = 100,
                        LinesOfCode = 1000,
                        Warnings = 0
                    },
                    Head = new ProjectDiffMetrics
                    {
                        InternalDependencies = 1,
                        InternalDependents = 1,
                        DependencyRatio = 1.0,
                        AvgMaintainabilityIndex = 80.0,
                        CyclomaticComplexity = 100,
                        LinesOfCode = 1000,
                        Warnings = 0
                    }
                }
            ]
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Should not have the section if no dependency changes
        await Assert.That(markdown).DoesNotContain("### Internal Dependencies Changes");
    }

    [Test]
    public async Task RenderMarkdown_WithAddedOrRemovedProjects_ExcludesThemFromDependenciesSection()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals
            {
                BaseProjects = 1,
                HeadProjects = 2,
                BaseTypes = 10,
                HeadTypes = 20,
                BaseMethods = 50,
                HeadMethods = 100,
                BaseCyclomaticComplexity = 100,
                HeadCyclomaticComplexity = 200,
                BaseLinesOfCode = 1000,
                HeadLinesOfCode = 2000,
                BaseAvgMaintainabilityIndex = 80.0,
                HeadAvgMaintainabilityIndex = 80.0,
                BaseErrors = 0,
                HeadErrors = 0,
                BaseWarnings = 0,
                HeadWarnings = 0,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
            },
            Projects =
            [
                new ProjectDiff
                {
                    Name = "AddedProject",
                    IsAdded = true,
                    Base = new ProjectDiffMetrics(),
                    Head = new ProjectDiffMetrics
                    {
                        InternalDependencies = 1,
                        InternalDependents = 0,
                        DependencyRatio = 1.0,
                        AvgMaintainabilityIndex = 80.0,
                        CyclomaticComplexity = 100,
                        LinesOfCode = 1000,
                        Warnings = 0
                    }
                },
                new ProjectDiff
                {
                    Name = "RemovedProject",
                    IsRemoved = true,
                    Base = new ProjectDiffMetrics
                    {
                        InternalDependencies = 1,
                        InternalDependents = 0,
                        DependencyRatio = 1.0,
                        AvgMaintainabilityIndex = 80.0,
                        CyclomaticComplexity = 100,
                        LinesOfCode = 1000,
                        Warnings = 0
                    },
                    Head = new ProjectDiffMetrics()
                }
            ]
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Should not have the section since added/removed projects are excluded
        await Assert.That(markdown).DoesNotContain("### Internal Dependencies Changes");
    }

    [Test]
    public async Task RenderMarkdown_SectionOrdering_IncludesNewSectionsInCorrectOrder()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals
            {
                BaseProjects = 1,
                HeadProjects = 1,
                BaseTypes = 10,
                HeadTypes = 10,
                BaseMethods = 50,
                HeadMethods = 50,
                BaseCyclomaticComplexity = 100,
                HeadCyclomaticComplexity = 120,
                BaseLinesOfCode = 1000,
                HeadLinesOfCode = 1200,
                BaseAvgMaintainabilityIndex = 80.0,
                HeadAvgMaintainabilityIndex = 75.0,
                BaseErrors = 0,
                HeadErrors = 1,
                BaseWarnings = 0,
                HeadWarnings = 2,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
            },
            Projects =
            [
                new ProjectDiff
                {
                    Name = "TestProject",
                    Base = new ProjectDiffMetrics
                    {
                        InternalDependencies = 1,
                        InternalDependents = 0,
                        DependencyRatio = 1.0,
                        AvgMaintainabilityIndex = 80.0,
                        CyclomaticComplexity = 100,
                        LinesOfCode = 1000,
                        Warnings = 0
                    },
                    Head = new ProjectDiffMetrics
                    {
                        InternalDependencies = 2,
                        InternalDependents = 0,
                        DependencyRatio = 2.0,
                        AvgMaintainabilityIndex = 75.0,
                        CyclomaticComplexity = 120,
                        LinesOfCode = 1200,
                        Warnings = 2
                    },
                    AddedInternalDependencies = ["SharedProject"],
                    RemovedInternalDependencies = []
                }
            ],
            Diagnostics = new DiagnosticDiffSummary
            {
                BaseErrors = 0,
                HeadErrors = 1,
                BaseWarnings = 0,
                HeadWarnings = 2,
                TopNewErrors =
                [
                    new DiagnosticDiffItem
                    {
                        Project = "TestProject",
                        Id = "CS0103",
                        Severity = "Error",
                        Message = "Error message",
                        File = "File.cs",
                        Line = 1,
                        Column = 1
                    }
                ],
                TopNewWarnings =
                [
                    new DiagnosticDiffItem
                    {
                        Project = "TestProject",
                        Id = "CS8602",
                        Severity = "Warning",
                        Message = "Warning message",
                        File = "File.cs",
                        Line = 2,
                        Column = 1
                    }
                ]
            }
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Verify all sections are present in the correct order
        var diagnosticsIndex = markdown.IndexOf("### Diagnostics");
        var newDiagnosticsIndex = markdown.IndexOf("### New Diagnostics");
        var internalDependenciesIndex = markdown.IndexOf("### Internal Dependencies Changes");
        var topChangesIndex = markdown.IndexOf("### Maintainability Changes");
        var metricsIndex = markdown.IndexOf("### Overall Metrics");

        // Order should be: New Diagnostics -> Diagnostics -> Internal Dependencies -> Maintainability Changes -> Overall Metrics
        await Assert.That(newDiagnosticsIndex).IsGreaterThan(0);
        await Assert.That(diagnosticsIndex).IsGreaterThan(newDiagnosticsIndex);
        await Assert.That(internalDependenciesIndex).IsGreaterThan(diagnosticsIndex);
        await Assert.That(topChangesIndex).IsGreaterThan(internalDependenciesIndex);
        await Assert.That(metricsIndex).IsGreaterThan(topChangesIndex);
    }

    [Test]
    public async Task RenderMarkdown_ExternalBclDependenciesIncrease_ShowsNeedsReview()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals
            {
                BaseProjects = 1,
                HeadProjects = 1,
                BaseTypes = 10,
                HeadTypes = 10,
                BaseMethods = 50,
                HeadMethods = 50,
                BaseCyclomaticComplexity = 100,
                HeadCyclomaticComplexity = 100,
                BaseLinesOfCode = 1000,
                HeadLinesOfCode = 1000,
                BaseAvgMaintainabilityIndex = 80.0,
                HeadAvgMaintainabilityIndex = 80.0,
                BaseErrors = 0,
                HeadErrors = 0,
                BaseWarnings = 0,
                HeadWarnings = 0,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
            },
            Projects =
            [
                new ProjectDiff
                {
                    Name = "TestProject",
                    Base = new ProjectDiffMetrics
                    {
                        ExternalBclDependencies = 3,
                        ExternalPackageDependencies = 5,
                        ExternalDependencies = 8,
                        ExternalBclDependencyNames = ["System.Collections", "System.Linq", "Microsoft.Extensions.Hosting"],
                        ExternalPackageDependencyNames = ["PackageA", "PackageB", "PackageC", "PackageD", "PackageE"],
                        AvgMaintainabilityIndex = 80.0
                    },
                    Head = new ProjectDiffMetrics
                    {
                        ExternalBclDependencies = 5,
                        ExternalPackageDependencies = 5,
                        ExternalDependencies = 10,
                        ExternalBclDependencyNames = ["System.Collections", "System.Linq", "Microsoft.Extensions.Hosting", "System.Text.Json", "Microsoft.Extensions.Logging"],
                        ExternalPackageDependencyNames = ["PackageA", "PackageB", "PackageC", "PackageD", "PackageE"],
                        AvgMaintainabilityIndex = 80.0
                    },
                    AddedBclDependencies = ["Microsoft.Extensions.Logging", "System.Text.Json"],
                    RemovedBclDependencies = [],
                    AddedPackageDependencies = [],
                    RemovedPackageDependencies = []
                }
            ]
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).Contains("### External Dependencies Changes");
        await Assert.That(markdown).Contains("🔍 Added BCL Dependencies (2)");
        await Assert.That(markdown).Contains("`System.Text.Json`");
        await Assert.That(markdown).Contains("`Microsoft.Extensions.Logging`");
    }

    [Test]
    public async Task RenderMarkdown_ExternalPackageDependenciesDecrease_ShowsSuccess()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals
            {
                BaseProjects = 1,
                HeadProjects = 1,
                BaseTypes = 10,
                HeadTypes = 10,
                BaseMethods = 50,
                HeadMethods = 50,
                BaseCyclomaticComplexity = 100,
                HeadCyclomaticComplexity = 100,
                BaseLinesOfCode = 1000,
                HeadLinesOfCode = 1000,
                BaseAvgMaintainabilityIndex = 80.0,
                HeadAvgMaintainabilityIndex = 80.0,
                BaseErrors = 0,
                HeadErrors = 0,
                BaseWarnings = 0,
                HeadWarnings = 0,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
            },
            Projects =
            [
                new ProjectDiff
                {
                    Name = "TestProject",
                    Base = new ProjectDiffMetrics
                    {
                        ExternalBclDependencies = 3,
                        ExternalPackageDependencies = 8,
                        ExternalDependencies = 11,
                        ExternalBclDependencyNames = ["System.Collections", "System.Linq", "Microsoft.Extensions.Hosting"],
                        ExternalPackageDependencyNames = ["Newtonsoft.Json", "Serilog", "AutoMapper", "FluentValidation", "Dapper", "Polly", "MediatR", "NLog"],
                        AvgMaintainabilityIndex = 80.0
                    },
                    Head = new ProjectDiffMetrics
                    {
                        ExternalBclDependencies = 3,
                        ExternalPackageDependencies = 5,
                        ExternalDependencies = 8,
                        ExternalBclDependencyNames = ["System.Collections", "System.Linq", "Microsoft.Extensions.Hosting"],
                        ExternalPackageDependencyNames = ["Serilog", "AutoMapper", "FluentValidation", "MediatR", "NLog"],
                        AvgMaintainabilityIndex = 80.0
                    },
                    AddedBclDependencies = [],
                    RemovedBclDependencies = [],
                    AddedPackageDependencies = [],
                    RemovedPackageDependencies = ["Dapper", "Newtonsoft.Json", "Polly"]
                }
            ]
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).Contains("### External Dependencies Changes");
        await Assert.That(markdown).Contains("✅ Removed Third-Party Packages (3)");
        await Assert.That(markdown).Contains("`Newtonsoft.Json`");
        await Assert.That(markdown).Contains("`Dapper`");
        await Assert.That(markdown).Contains("`Polly`");
    }

    [Test]
    public async Task RenderMarkdown_NoExternalDependencyChanges_OmitsExternalDependenciesSection()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals
            {
                BaseProjects = 1,
                HeadProjects = 1,
                BaseTypes = 10,
                HeadTypes = 10,
                BaseMethods = 50,
                HeadMethods = 50,
                BaseCyclomaticComplexity = 100,
                HeadCyclomaticComplexity = 100,
                BaseLinesOfCode = 1000,
                HeadLinesOfCode = 1000,
                BaseAvgMaintainabilityIndex = 80.0,
                HeadAvgMaintainabilityIndex = 80.0,
                BaseErrors = 0,
                HeadErrors = 0,
                BaseWarnings = 0,
                HeadWarnings = 0,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
            },
            Projects =
            [
                new ProjectDiff
                {
                    Name = "TestProject",
                    Base = new ProjectDiffMetrics
                    {
                        ExternalBclDependencies = 3,
                        ExternalPackageDependencies = 5,
                        ExternalDependencies = 8,
                        ExternalBclDependencyNames = ["System.Collections", "System.Linq", "Microsoft.Extensions.Hosting"],
                        ExternalPackageDependencyNames = ["PackageA", "PackageB", "PackageC", "PackageD", "PackageE"],
                        AvgMaintainabilityIndex = 80.0
                    },
                    Head = new ProjectDiffMetrics
                    {
                        ExternalBclDependencies = 3,
                        ExternalPackageDependencies = 5,
                        ExternalDependencies = 8,
                        ExternalBclDependencyNames = ["System.Collections", "System.Linq", "Microsoft.Extensions.Hosting"],
                        ExternalPackageDependencyNames = ["PackageA", "PackageB", "PackageC", "PackageD", "PackageE"],
                        AvgMaintainabilityIndex = 80.0
                    },
                    AddedBclDependencies = [],
                    RemovedBclDependencies = [],
                    AddedPackageDependencies = [],
                    RemovedPackageDependencies = []
                }
            ]
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Should not have the section if no external dependency changes
        await Assert.That(markdown).DoesNotContain("### External Dependencies Changes");
    }

    [Test]
    public async Task RenderMarkdown_ExternalDependenciesSectionOrdering_AppearsAfterInternalDependencies()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals
            {
                BaseProjects = 1,
                HeadProjects = 1,
                BaseTypes = 10,
                HeadTypes = 10,
                BaseMethods = 50,
                HeadMethods = 50,
                BaseCyclomaticComplexity = 100,
                HeadCyclomaticComplexity = 100,
                BaseLinesOfCode = 1000,
                HeadLinesOfCode = 1000,
                BaseAvgMaintainabilityIndex = 80.0,
                HeadAvgMaintainabilityIndex = 75.0,
                BaseErrors = 0,
                HeadErrors = 0,
                BaseWarnings = 0,
                HeadWarnings = 0,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
            },
            Projects =
            [
                new ProjectDiff
                {
                    Name = "TestProject",
                    Base = new ProjectDiffMetrics
                    {
                        InternalDependencies = 1,
                        InternalDependents = 0,
                        DependencyRatio = 1.0,
                        ExternalBclDependencies = 3,
                        ExternalPackageDependencies = 5,
                        ExternalDependencies = 8,
                        ExternalBclDependencyNames = ["System.Collections", "System.Linq", "Microsoft.Extensions.Hosting"],
                        ExternalPackageDependencyNames = ["PackageA", "PackageB", "PackageC", "PackageD", "PackageE"],
                        AvgMaintainabilityIndex = 80.0,
                        CyclomaticComplexity = 100,
                        LinesOfCode = 1000,
                        Warnings = 0
                    },
                    Head = new ProjectDiffMetrics
                    {
                        InternalDependencies = 2,
                        InternalDependents = 0,
                        DependencyRatio = 2.0,
                        ExternalBclDependencies = 5,
                        ExternalPackageDependencies = 6,
                        ExternalDependencies = 11,
                        ExternalBclDependencyNames = ["System.Collections", "System.Linq", "Microsoft.Extensions.Hosting", "System.Text.Json", "Microsoft.Extensions.Logging"],
                        ExternalPackageDependencyNames = ["PackageA", "PackageB", "PackageC", "PackageD", "PackageE", "Newtonsoft.Json"],
                        AvgMaintainabilityIndex = 75.0,
                        CyclomaticComplexity = 120,
                        LinesOfCode = 1200,
                        Warnings = 0
                    },
                    AddedBclDependencies = ["Microsoft.Extensions.Logging", "System.Text.Json"],
                    RemovedBclDependencies = [],
                    AddedPackageDependencies = ["Newtonsoft.Json"],
                    RemovedPackageDependencies = [],
                    AddedInternalDependencies = ["SharedProject"],
                    RemovedInternalDependencies = []
                }
            ]
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Verify section ordering
        var internalDepsIndex = markdown.IndexOf("### Internal Dependencies Changes");
        var externalDepsIndex = markdown.IndexOf("### External Dependencies Changes");
        var maintainabilityIndex = markdown.IndexOf("### Maintainability Changes");

        await Assert.That(internalDepsIndex).IsGreaterThan(0);
        await Assert.That(externalDepsIndex).IsGreaterThan(internalDepsIndex);
        await Assert.That(maintainabilityIndex).IsGreaterThan(externalDepsIndex);
    }

    [Test]
    public async Task RenderMarkdown_BothBclAndPackageChanges_ShowsSeparately()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals
            {
                BaseProjects = 1,
                HeadProjects = 1,
                BaseTypes = 10,
                HeadTypes = 10,
                BaseMethods = 50,
                HeadMethods = 50,
                BaseCyclomaticComplexity = 100,
                HeadCyclomaticComplexity = 100,
                BaseLinesOfCode = 1000,
                HeadLinesOfCode = 1000,
                BaseAvgMaintainabilityIndex = 80.0,
                HeadAvgMaintainabilityIndex = 80.0,
                BaseErrors = 0,
                HeadErrors = 0,
                BaseWarnings = 0,
                HeadWarnings = 0,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
            },
            Projects =
            [
                new ProjectDiff
                {
                    Name = "TestProject",
                    Base = new ProjectDiffMetrics
                    {
                        ExternalBclDependencies = 3,
                        ExternalPackageDependencies = 5,
                        ExternalDependencies = 8,
                        ExternalBclDependencyNames = ["System.Collections", "System.Linq", "Microsoft.Extensions.Hosting"],
                        ExternalPackageDependencyNames = ["PackageA", "PackageB", "PackageC", "PackageD", "PackageE"],
                        AvgMaintainabilityIndex = 80.0
                    },
                    Head = new ProjectDiffMetrics
                    {
                        ExternalBclDependencies = 5,
                        ExternalPackageDependencies = 8,
                        ExternalDependencies = 13,
                        ExternalBclDependencyNames = ["System.Collections", "System.Linq", "Microsoft.Extensions.Hosting", "System.Text.Json", "Microsoft.Extensions.Logging"],
                        ExternalPackageDependencyNames = ["PackageA", "PackageB", "PackageC", "PackageD", "PackageE", "Newtonsoft.Json", "Serilog", "AutoMapper"],
                        AvgMaintainabilityIndex = 80.0
                    },
                    AddedBclDependencies = ["Microsoft.Extensions.Logging", "System.Text.Json"],
                    RemovedBclDependencies = [],
                    AddedPackageDependencies = ["AutoMapper", "Newtonsoft.Json", "Serilog"],
                    RemovedPackageDependencies = []
                }
            ]
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Both BCL and Packages should be shown separately with package names
        await Assert.That(markdown).Contains("🔍 Added BCL Dependencies (2)");
        await Assert.That(markdown).Contains("🔍 Added Third-Party Packages (3)");
        await Assert.That(markdown).Contains("`System.Text.Json`");
        await Assert.That(markdown).Contains("`Newtonsoft.Json`");
    }

    [Test]
    public async Task RenderMarkdown_AddedOrRemovedProjects_ExcludesThemFromExternalDependenciesSection()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals
            {
                BaseProjects = 1,
                HeadProjects = 2,
                BaseTypes = 10,
                HeadTypes = 20,
                BaseMethods = 50,
                HeadMethods = 100,
                BaseCyclomaticComplexity = 100,
                HeadCyclomaticComplexity = 200,
                BaseLinesOfCode = 1000,
                HeadLinesOfCode = 2000,
                BaseAvgMaintainabilityIndex = 80.0,
                HeadAvgMaintainabilityIndex = 80.0,
                BaseErrors = 0,
                HeadErrors = 0,
                BaseWarnings = 0,
                HeadWarnings = 0,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
            },
            Projects =
            [
                new ProjectDiff
                {
                    Name = "AddedProject",
                    IsAdded = true,
                    Base = new ProjectDiffMetrics(),
                    Head = new ProjectDiffMetrics
                    {
                        ExternalBclDependencies = 5,
                        ExternalPackageDependencies = 3,
                        ExternalDependencies = 8,
                        ExternalBclDependencyNames = ["System.Collections", "System.Linq", "Microsoft.Extensions.Hosting", "System.Text.Json", "Microsoft.Extensions.Logging"],
                        ExternalPackageDependencyNames = ["PackageA", "PackageB", "PackageC"],
                        AvgMaintainabilityIndex = 80.0
                    },
                    AddedBclDependencies = [],
                    RemovedBclDependencies = [],
                    AddedPackageDependencies = [],
                    RemovedPackageDependencies = []
                },
                new ProjectDiff
                {
                    Name = "RemovedProject",
                    IsRemoved = true,
                    Base = new ProjectDiffMetrics
                    {
                        ExternalBclDependencies = 4,
                        ExternalPackageDependencies = 6,
                        ExternalDependencies = 10,
                        ExternalBclDependencyNames = ["System.Collections", "System.Linq", "System.Threading", "Microsoft.Extensions.Hosting"],
                        ExternalPackageDependencyNames = ["PackageX", "PackageY", "PackageZ", "Serilog", "AutoMapper", "Newtonsoft.Json"],
                        AvgMaintainabilityIndex = 80.0
                    },
                    Head = new ProjectDiffMetrics(),
                    AddedBclDependencies = [],
                    RemovedBclDependencies = [],
                    AddedPackageDependencies = [],
                    RemovedPackageDependencies = []
                }
            ]
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Should not have the section since added/removed projects are excluded
        await Assert.That(markdown).DoesNotContain("### External Dependencies Changes");
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
                BaseProjects = 1,
                HeadProjects = 1,
                BaseTypes = 10,
                HeadTypes = 10,
                BaseMethods = 50,
                HeadMethods = 50,
                BaseCyclomaticComplexity = baseComplexity,
                HeadCyclomaticComplexity = headComplexity,
                BaseLinesOfCode = 1000,
                HeadLinesOfCode = 1000,
                BaseAvgMaintainabilityIndex = baseMaintainability,
                HeadAvgMaintainabilityIndex = headMaintainability,
                BaseErrors = baseErrors,
                HeadErrors = headErrors,
                BaseWarnings = baseWarnings,
                HeadWarnings = headWarnings,
                BaseInfo = 0,
                HeadInfo = 0,
                BaseHidden = 0,
                HeadHidden = 0
            },
            Projects = [],
            Diagnostics = new DiagnosticDiffSummary()
        };
    }
}

