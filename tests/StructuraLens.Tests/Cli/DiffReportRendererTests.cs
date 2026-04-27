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
        await Assert.That(markdown).Contains("🚨 **5**");
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
        await Assert.That(markdown).Contains("| Errors | 10 | 3 | 7 | 0 |");
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
        await Assert.That(markdown).Contains("⚠️ **10**");
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
        await Assert.That(markdown).Contains("| Warnings | 20 | 5 | 15 | 0 |");
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
        await Assert.That(markdown).DoesNotContain("| Hidden |");
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
            headComplexity: 180);
        diff = diff with
        {
            HasComplexityMetrics = false,
            Head = diff.Head with { AnalysisMode = AnalysisMode.DiagnosticsAndReferences }
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).DoesNotContain("### Maintainability Changes");
        await Assert.That(markdown).DoesNotContain("| Cyclomatic Complexity |");
        await Assert.That(markdown).DoesNotContain("| Lines of Code |");
        await Assert.That(markdown).DoesNotContain("| Avg Maintainability |");
        await Assert.That(markdown).DoesNotContain("| Types |");
        await Assert.That(markdown).DoesNotContain("| Methods |");
        await Assert.That(markdown).Contains("Analysis mode: `DiagnosticsAndReferences`");
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
    public async Task RenderMarkdown_WithNewDiagnostics_ShowsAddedAndResolvedDiagnosticTables()
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
                NewErrors = 2,
                NewWarnings = 3,
                AddedDiagnostics =
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
                    },
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
        await Assert.That(markdown).Contains("#### Added Diagnostics");
        await Assert.That(markdown).Contains("| Error | CS0103 |");
        await Assert.That(markdown).Contains("| Error | CS0246 |");
        await Assert.That(markdown).Contains("| Warning | CS8602 |");
        await Assert.That(markdown).Contains("#### Resolved Diagnostics");
        await Assert.That(markdown).Contains("None");
    }

    [Test]
    public async Task RenderMarkdown_AlwaysShowsDiagnosticsChangeTables()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffReport();

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        await Assert.That(markdown).Contains("#### Added Diagnostics");
        await Assert.That(markdown).Contains("#### Resolved Diagnostics");
    }

    [Test]
    public async Task RenderMarkdown_WithManyNewDiagnostics_ShowsAllRowsInTable()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var addedDiagnostics = Enumerable.Range(1, 10).Select(i => new DiagnosticDiffItem
        {
            Project = $"Project{i}",
            Id = $"CS{i:D4}",
            Severity = "Error",
            Message = $"Error message {i}",
            File = $"File{i}.cs",
            Line = i,
            Column = 1
        }).Concat(Enumerable.Range(11, 15).Select(i => new DiagnosticDiffItem
        {
            Project = $"Project{i}",
            Id = $"CS{i:D4}",
            Severity = "Warning",
            Message = $"Warning message {i}",
            File = $"File{i}.cs",
            Line = i,
            Column = 1
        })).ToList();

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
                NewErrors = 10,
                NewWarnings = 15,
                AddedDiagnostics = addedDiagnostics
            }
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        await Assert.That(markdown).Contains("#### Added Diagnostics");
        await Assert.That(markdown).Contains("| Error | CS0001 |");
        await Assert.That(markdown).Contains("| Error | CS0010 |");
        await Assert.That(markdown).Contains("| Warning | CS0011 |");
        await Assert.That(markdown).Contains("| Warning | CS0025 |");
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
                    AddedProjectReferences = ["ProviderProject"],
                    RemovedProjectReferences = []
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
                    AddedProjectReferences = [],
                    RemovedProjectReferences = []
                }
            ]
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).Contains("### Project References Changes");
        await Assert.That(markdown).Contains("`ConsumerProject` → `ProviderProject`");
        await Assert.That(markdown).DoesNotContain("#### ProviderProject");
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
        await Assert.That(markdown).DoesNotContain("### Project References Changes");
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
        await Assert.That(markdown).DoesNotContain("### Project References Changes");
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
                    AddedProjectReferences = ["SharedProject"],
                    RemovedProjectReferences = []
                }
            ],
            Diagnostics = new DiagnosticDiffSummary
            {
                BaseErrors = 0,
                HeadErrors = 1,
                BaseWarnings = 0,
                HeadWarnings = 2,
                AddedDiagnostics =
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
                    },
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
        var internalDependenciesIndex = markdown.IndexOf("### Project References Changes");
        var topChangesIndex = markdown.IndexOf("### Maintainability Changes");
        var metricsIndex = markdown.IndexOf("### Overall Metrics");

        // Order should be: Diagnostics -> Project References -> Maintainability Changes -> Overall Metrics
        await Assert.That(diagnosticsIndex).IsGreaterThan(0);
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
        await Assert.That(markdown).Contains("### NuGet Dependencies Changes");
        await Assert.That(markdown).Contains("`TestProject` → BCL: `Microsoft.Extensions.Logging`, `System.Text.Json`");
        await Assert.That(markdown).Contains("`System.Text.Json`");
        await Assert.That(markdown).Contains("`Microsoft.Extensions.Logging`");
    }

    [Test]
    public async Task RenderMarkdown_ExternalPackageDependenciesDecrease_OmitsNuGetSection()
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

        // Assert - removal-only changes are intentionally omitted
        await Assert.That(markdown).DoesNotContain("### NuGet Dependencies Changes");
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
        await Assert.That(markdown).DoesNotContain("### NuGet Dependencies Changes");
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
                    AddedProjectReferences = ["SharedProject"],
                    RemovedProjectReferences = []
                }
            ]
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - Verify section ordering
        var internalDepsIndex = markdown.IndexOf("### Project References Changes");
        var externalDepsIndex = markdown.IndexOf("### NuGet Dependencies Changes");
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
        await Assert.That(markdown).Contains("`TestProject` → BCL: `Microsoft.Extensions.Logging`, `System.Text.Json`; Packages: `AutoMapper`, `Newtonsoft.Json`, `Serilog`");
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
        await Assert.That(markdown).DoesNotContain("### NuGet Dependencies Changes");
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

    [Test]
    public async Task RenderMarkdown_WithNewDiagnostics_ColumnOrderIsCorrect()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals { BaseProjects = 1, HeadProjects = 1, HeadErrors = 1 },
            Projects = [],
            Diagnostics = new DiagnosticDiffSummary
            {
                NewErrors = 1,
                AddedDiagnostics =
                [
                    new DiagnosticDiffItem
                    {
                        Project = "TestProject",
                        Id = "CS0103",
                        Severity = "Error",
                        Message = "The name 'foo' does not exist",
                        File = "Program.cs",
                        Line = 42,
                        Column = 10
                    }
                ]
            }
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert - header order: Severity | Code | Description | Location | File
        await Assert.That(markdown).Contains("| Severity | Code | Description | Location | File |");
        // Assert - row order matches header
        await Assert.That(markdown).Contains("| Error | CS0103 | The name 'foo' does not exist | 42:10 | Program.cs |");
    }

    [Test]
    public async Task RenderMarkdown_WithMovedDiagnostics_HidesMovedDiagnostics()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals { BaseProjects = 1, HeadProjects = 1, BaseWarnings = 1, HeadWarnings = 1 },
            Projects = [],
            Diagnostics = new DiagnosticDiffSummary
            {
                MovedWarnings = 1,
                MovedDiagnostics =
                [
                    new DiagnosticMoveDiffItem
                    {
                        Project = "TestProject",
                        Id = "NSDEPCOP01",
                        Severity = "warning",
                        Message = "Illegal namespace reference",
                        File = "Controller.cs",
                        BaseLine = 289,
                        BaseColumn = 26,
                        HeadLine = 291,
                        HeadColumn = 26
                    }
                ]
            }
        };

        // Act
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).Contains("| Metric | Base | Head | Solved | Added |");
        await Assert.That(markdown).Contains("| Warnings | 1 | 1 | 0 | 0 |");
        await Assert.That(markdown).DoesNotContain("Moved");
        await Assert.That(markdown).DoesNotContain("#### Moved Diagnostics");
        await Assert.That(markdown).DoesNotContain("| warning | NSDEPCOP01 | Illegal namespace reference | 289:26 | 291:26 | Controller.cs |");
        await Assert.That(markdown).DoesNotContain("| warning | NSDEPCOP01 | Illegal namespace reference | 291:26 | Controller.cs |");
    }

    [Test]
    public async Task RenderMarkdown_DefaultLevel_FiltersOutHiddenDiagnostics()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffWithDiagnostics(
        [
            MakeDiagnostic("CS0001", "Error", "An error"),
            MakeDiagnostic("CS0002", "Hidden", "A hidden diagnostic")
        ]);

        // Act - default minDiagnosticLevel is Info, so Hidden should be excluded
        var markdown = renderer.RenderMarkdown(diff);

        // Assert
        await Assert.That(markdown).Contains("| Error | CS0001 |");
        await Assert.That(markdown).DoesNotContain("| Hidden | CS0002 |");
    }

    [Test]
    public async Task RenderMarkdown_WithMinLevelWarning_FiltersOutInfoDiagnostics()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffWithDiagnostics(
        [
            MakeDiagnostic("CS0001", "Error", "An error"),
            MakeDiagnostic("CS8019", "Warning", "A warning"),
            MakeDiagnostic("CS0060", "Info", "An info message"),
            MakeDiagnostic("CS9001", "Hidden", "A hidden message")
        ]);

        // Act
        var markdown = renderer.RenderMarkdown(diff, minDiagnosticLevel: DiagnosticLevel.Warning);

        // Assert
        await Assert.That(markdown).Contains("| Error | CS0001 |");
        await Assert.That(markdown).Contains("| Warning | CS8019 |");
        await Assert.That(markdown).DoesNotContain("| Info | CS0060 |");
        await Assert.That(markdown).DoesNotContain("| Hidden |");
    }

    [Test]
    public async Task RenderMarkdown_WithMinLevelError_FiltersOutWarningAndBelowDiagnostics()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffWithDiagnostics(
        [
            MakeDiagnostic("CS0001", "Error", "An error"),
            MakeDiagnostic("CS8019", "Warning", "A warning"),
            MakeDiagnostic("CS0060", "Info", "An info message")
        ]);

        // Act
        var markdown = renderer.RenderMarkdown(diff, minDiagnosticLevel: DiagnosticLevel.Error);

        // Assert
        await Assert.That(markdown).Contains("| Error | CS0001 |");
        await Assert.That(markdown).DoesNotContain("| Warning | CS8019 |");
        await Assert.That(markdown).DoesNotContain("| Info | CS0060 |");
    }

    [Test]
    public async Task RenderMarkdown_WithMinLevelHidden_ShowsAllDiagnostics()
    {
        // Arrange
        var renderer = new DiffReportRenderer();
        var diff = CreateDiffWithDiagnostics(
        [
            MakeDiagnostic("CS0001", "Error", "An error"),
            MakeDiagnostic("CS8019", "Warning", "A warning"),
            MakeDiagnostic("CS0060", "Info", "An info message"),
            MakeDiagnostic("CS9999", "Hidden", "A hidden diagnostic")
        ]);

        // Act
        var markdown = renderer.RenderMarkdown(diff, minDiagnosticLevel: DiagnosticLevel.Hidden);

        // Assert - all severities should appear
        await Assert.That(markdown).Contains("| Error | CS0001 |");
        await Assert.That(markdown).Contains("| Warning | CS8019 |");
        await Assert.That(markdown).Contains("| Info | CS0060 |");
        await Assert.That(markdown).Contains("| Hidden | CS9999 |");
    }

    private static AnalysisDiffReport CreateDiffWithDiagnostics(IReadOnlyList<DiagnosticDiffItem> addedDiagnostics)
    {
        return new AnalysisDiffReport
        {
            Base = new DiffMetadata { CommitSha = "abc123", BranchName = "main", AnalyzedAt = DateTime.UtcNow },
            Head = new DiffMetadata { CommitSha = "def456", BranchName = "feature", AnalyzedAt = DateTime.UtcNow },
            Totals = new DiffTotals { BaseProjects = 1, HeadProjects = 1 },
            Projects = [],
            Diagnostics = new DiagnosticDiffSummary { AddedDiagnostics = addedDiagnostics }
        };
    }

    private static DiagnosticDiffItem MakeDiagnostic(string id, string severity, string message) =>
        new() { Project = "TestProject", Id = id, Severity = severity, Message = message, File = "Test.cs", Line = 1, Column = 1 };
}
