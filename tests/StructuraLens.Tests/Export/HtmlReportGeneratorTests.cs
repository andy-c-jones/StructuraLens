using StructuraLens.Core.Export;
using StructuraLens.Core.Models;
using TUnit.Core;

namespace StructuraLens.Tests.Export;

public class HtmlReportGeneratorTests
{
    private HtmlReportGenerator CreateGenerator()
    {
        return new HtmlReportGenerator(new CompactReportExporter());
    }

    private AnalysisReport CreateMinimalReport(
        string solutionPath = "TestSolution.sln",
        DateTime? analyzedAt = null,
        GitRepositoryInfo? gitInfo = null,
        DiagnosticSummary? diagnostics = null)
    {
        var method = new MethodMetrics(
            FullName: "TestProject.TestClass.TestMethod()",
            FilePath: "TestClass.cs",
            StartLine: 10,
            EndLine: 20,
            CyclomaticComplexity: 3,
            LinesOfExecutableCode: 8,
            HalsteadVolume: 30.0,
            MaintainabilityIndex: 72.5);

        var type = new TypeMetrics(
            FullName: "TestProject.TestClass",
            FilePath: "TestClass.cs",
            DepthOfInheritance: 1,
            Methods: [method]);

        var project = new ProjectMetrics(
            Name: "TestProject",
            FilePath: "TestProject.csproj",
            Types: [type])
        {
            Diagnostics = diagnostics,
        };

        return new AnalysisReport(
            SolutionPath: solutionPath,
            AnalyzedAt: analyzedAt ?? new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc),
            Projects: [project],
            Warnings: [])
        {
            GitInfo = gitInfo,
        };
    }

    private AnalysisDiffReport CreateMinimalDiff()
    {
        return new AnalysisDiffReport
        {
            Base = new DiffMetadata
            {
                SolutionPath = "TestSolution.sln",
                AnalyzedAt = new DateTime(2025, 6, 14, 10, 0, 0, DateTimeKind.Utc),
                CommitSha = "aaa1111",
                BranchName = "main",
            },
            Head = new DiffMetadata
            {
                SolutionPath = "TestSolution.sln",
                AnalyzedAt = new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc),
                CommitSha = "bbb2222",
                BranchName = "feature/test",
            },
            Totals = new DiffTotals
            {
                BaseProjects = 1,
                HeadProjects = 1,
                BaseTypes = 1,
                HeadTypes = 2,
                BaseMethods = 1,
                HeadMethods = 3,
            },
        };
    }

    // ---------------------------------------------------------------
    // Template loading & basic structure
    // ---------------------------------------------------------------

    [Test]
    public async Task GenerateHtml_ReturnsValidHtmlDocument()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport();

        // Act
        var html = generator.GenerateHtml(report);

        // Assert
        await Assert.That(html).Contains("<!DOCTYPE html>");
        await Assert.That(html).Contains("<html");
        await Assert.That(html).Contains("</html>");
    }

    [Test]
    public async Task GenerateHtml_ContainsNoUnreplacedPlaceholders()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport();

        // Act
        var html = generator.GenerateHtml(report);

        // Assert — none of the 8 placeholder tokens should remain
        await Assert.That(html).DoesNotContain("{{REPORT_DATA}}");
        await Assert.That(html).DoesNotContain("{{DIAGNOSTICS_DATA}}");
        await Assert.That(html).DoesNotContain("{{DIFF_DATA}}");
        await Assert.That(html).DoesNotContain("{{REPORT_TITLE}}");
        await Assert.That(html).DoesNotContain("{{SOLUTION_NAME}}");
        await Assert.That(html).DoesNotContain("{{ANALYZED_AT}}");
        await Assert.That(html).DoesNotContain("{{GIT_INFO_HTML}}");
        await Assert.That(html).DoesNotContain("{{COPYRIGHT_YEAR}}");
    }

    // ---------------------------------------------------------------
    // Title placeholder
    // ---------------------------------------------------------------

    [Test]
    public async Task GenerateHtml_SetsReportTitle()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport(solutionPath: "/path/to/MySolution.sln");

        // Act
        var html = generator.GenerateHtml(report);

        // Assert
        await Assert.That(html).Contains("<title>StructuraLens Report - MySolution.sln</title>");
    }

    // ---------------------------------------------------------------
    // Solution name placeholder
    // ---------------------------------------------------------------

    [Test]
    public async Task GenerateHtml_SetsSolutionName()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport(solutionPath: "/repo/MyApp.sln");

        // Act
        var html = generator.GenerateHtml(report);

        // Assert
        await Assert.That(html).Contains("<strong>MyApp.sln</strong>");
    }

    // ---------------------------------------------------------------
    // Analyzed-at placeholder
    // ---------------------------------------------------------------

    [Test]
    public async Task GenerateHtml_SetsAnalyzedTimestamp()
    {
        // Arrange
        var generator = CreateGenerator();
        var at = new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var report = CreateMinimalReport(analyzedAt: at);

        // Act
        var html = generator.GenerateHtml(report);

        // Assert
        await Assert.That(html).Contains("2025-06-15 14:30:00 UTC");
    }

    // ---------------------------------------------------------------
    // Copyright year placeholder
    // ---------------------------------------------------------------

    [Test]
    public async Task GenerateHtml_SetsCopyrightYear()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport();

        // Act
        var html = generator.GenerateHtml(report);

        // Assert
        var expectedYear = DateTime.UtcNow.Year.ToString();
        await Assert.That(html).Contains(expectedYear + " Dark Peak Development");
    }

    // ---------------------------------------------------------------
    // Git info placeholder
    // ---------------------------------------------------------------

    [Test]
    public async Task GenerateHtml_WithGitInfo_RendersGitDetails()
    {
        // Arrange
        var generator = CreateGenerator();
        var gitInfo = new GitRepositoryInfo(
            CommitSha: "abc1234567890def",
            BranchName: "feature/my-branch",
            IsDirty: false);
        var report = CreateMinimalReport(gitInfo: gitInfo);

        // Act
        var html = generator.GenerateHtml(report);

        // Assert
        await Assert.That(html).Contains("feature/my-branch");
        await Assert.That(html).Contains("abc1234");
        await Assert.That(html).DoesNotContain("Uncommitted Changes");
    }

    [Test]
    public async Task GenerateHtml_WithDirtyGitInfo_ShowsUncommittedBadge()
    {
        // Arrange
        var generator = CreateGenerator();
        var gitInfo = new GitRepositoryInfo(
            CommitSha: "def5678901234abc",
            BranchName: "main",
            IsDirty: true);
        var report = CreateMinimalReport(gitInfo: gitInfo);

        // Act
        var html = generator.GenerateHtml(report);

        // Assert
        await Assert.That(html).Contains("main");
        await Assert.That(html).Contains("def5678");
        await Assert.That(html).Contains("Uncommitted Changes");
    }

    [Test]
    public async Task GenerateHtml_WithoutGitInfo_OmitsGitSection()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport(gitInfo: null);

        // Act
        var html = generator.GenerateHtml(report);

        // Assert — no git branch/commit details in the output
        await Assert.That(html).DoesNotContain("<strong>Git:</strong>");
    }

    // ---------------------------------------------------------------
    // Report data placeholder (JSON injection)
    // ---------------------------------------------------------------

    [Test]
    public async Task GenerateHtml_InjectsReportJsonData()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport();

        // Act
        var html = generator.GenerateHtml(report);

        // Assert — the compact report JSON should contain the project name
        // It will be escaped for JS string context, so quotes become \"
        await Assert.That(html).Contains("TestProject");
    }

    // ---------------------------------------------------------------
    // Diagnostics data placeholder
    // ---------------------------------------------------------------

    [Test]
    public async Task GenerateHtml_WithDiagnostics_InjectsDiagnosticsJson()
    {
        // Arrange
        var generator = CreateGenerator();
        var diagnostics = new DiagnosticSummary
        {
            ErrorCount = 1,
            WarningCount = 0,
            Diagnostics =
            [
                new DiagnosticInfo(
                    Id: "CS0001",
                    Message: "Test error message",
                    Severity: DiagnosticLevel.Error,
                    FilePath: "/src/TestClass.cs",
                    Line: 42,
                    Column: 5)
            ],
        };
        var report = CreateMinimalReport(diagnostics: diagnostics);

        // Act
        var html = generator.GenerateHtml(report);

        // Assert — diagnostic details should appear in the injected JSON
        await Assert.That(html).Contains("CS0001");
        await Assert.That(html).Contains("Test error message");
    }

    [Test]
    public async Task GenerateHtml_WithNoDiagnostics_InjectsEmptyArray()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport(diagnostics: null);

        // Act
        var html = generator.GenerateHtml(report);

        // Assert — diagnostics data should be an empty array
        await Assert.That(html).Contains("[]");
    }

    // ---------------------------------------------------------------
    // Diff tab presence / absence
    // ---------------------------------------------------------------

    [Test]
    public async Task GenerateHtml_WithoutDiff_RemovesDiffTab()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport();

        // Act
        var html = generator.GenerateHtml(report);

        // Assert — the diff tab button should be removed
        await Assert.That(html).DoesNotContain("""data-tab="diff">Diff</div>""");
    }

    [Test]
    public async Task GenerateHtml_WithDiff_IncludesDiffTab()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport();
        var diff = CreateMinimalDiff();

        // Act
        var html = generator.GenerateHtml(report, diff);

        // Assert — the diff tab button should be present
        await Assert.That(html).Contains("""data-tab="diff">Diff</div>""");
    }

    [Test]
    public async Task GenerateHtml_WithDiff_ContainsNoUnreplacedPlaceholders()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport();
        var diff = CreateMinimalDiff();

        // Act
        var html = generator.GenerateHtml(report, diff);

        // Assert
        await Assert.That(html).DoesNotContain("{{REPORT_DATA}}");
        await Assert.That(html).DoesNotContain("{{DIAGNOSTICS_DATA}}");
        await Assert.That(html).DoesNotContain("{{DIFF_DATA}}");
        await Assert.That(html).DoesNotContain("{{REPORT_TITLE}}");
        await Assert.That(html).DoesNotContain("{{SOLUTION_NAME}}");
        await Assert.That(html).DoesNotContain("{{ANALYZED_AT}}");
        await Assert.That(html).DoesNotContain("{{GIT_INFO_HTML}}");
        await Assert.That(html).DoesNotContain("{{COPYRIGHT_YEAR}}");
    }

    [Test]
    public async Task GenerateHtml_WithDiff_InjectsDiffJson()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport();
        var diff = CreateMinimalDiff();

        // Act
        var html = generator.GenerateHtml(report, diff);

        // Assert — diff JSON should contain head/base branch names
        await Assert.That(html).Contains("feature/test");
    }

    [Test]
    public async Task GenerateHtml_WithoutDiff_InjectsNullForDiffData()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport();

        // Act
        var html = generator.GenerateHtml(report);

        // Assert — diff data placeholder should be replaced with "null"
        await Assert.That(html).Contains("null");
    }

    // ---------------------------------------------------------------
    // JSON escaping
    // ---------------------------------------------------------------

    [Test]
    public async Task GenerateHtml_EscapesQuotesInJson()
    {
        // Arrange — method name with quotes to verify JS string escaping
        var method = new MethodMetrics(
            FullName: """TestProject.TestClass.Method("arg")""",
            FilePath: "TestClass.cs",
            StartLine: 1,
            EndLine: 5,
            CyclomaticComplexity: 1,
            LinesOfExecutableCode: 3,
            HalsteadVolume: 10.0,
            MaintainabilityIndex: 90.0);

        var type = new TypeMetrics(
            FullName: "TestProject.TestClass",
            FilePath: "TestClass.cs",
            DepthOfInheritance: 1,
            Methods: [method]);

        var project = new ProjectMetrics(
            Name: "TestProject",
            FilePath: "TestProject.csproj",
            Types: [type]);

        var report = new AnalysisReport(
            SolutionPath: "test.sln",
            AnalyzedAt: DateTime.UtcNow,
            Projects: [project],
            Warnings: []);

        var generator = CreateGenerator();

        // Act
        var html = generator.GenerateHtml(report);

        // Assert — the HTML should be well-formed (no unescaped quotes breaking JS).
        // System.Text.Json escapes " as \u0022 in JSON output. After our JS string
        // escaping the backslash becomes \\, so the final output is \\u0022.
        await Assert.That(html).Contains(@"\\u0022arg\\u0022");
    }

    // ---------------------------------------------------------------
    // All 6 tabs present in non-diff output
    // ---------------------------------------------------------------

    [Test]
    public async Task GenerateHtml_ContainsAllStandardTabs()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport();

        // Act
        var html = generator.GenerateHtml(report);

        // Assert
        await Assert.That(html).Contains("""data-tab="summary">Summary</div>""");
        await Assert.That(html).Contains("""data-tab="projects">Projects</div>""");
        await Assert.That(html).Contains("""data-tab="coupling">Coupling</div>""");
        await Assert.That(html).Contains("""data-tab="graph">Graph</div>""");
        await Assert.That(html).Contains("""data-tab="diagnostics">Diagnostics</div>""");
    }

    // ---------------------------------------------------------------
    // Embedded template integrity
    // ---------------------------------------------------------------

    [Test]
    public async Task GenerateHtml_ContainsInlinedCssAndJs()
    {
        // Arrange
        var generator = CreateGenerator();
        var report = CreateMinimalReport();

        // Act
        var html = generator.GenerateHtml(report);

        // Assert — CSS custom properties from theme.css should be inlined
        await Assert.That(html).Contains("--bg:");
        await Assert.That(html).Contains("--accent:");

        // Assert — compiled JS should be present (the client script references window globals)
        await Assert.That(html).Contains("__STRUCTURALENS_REPORT__");
    }

    [Test]
    public async Task GenerateHtml_DoesNotReferenceCdn()
    {
        // Arrange — D3 should be bundled via npm, not loaded from CDN
        var generator = CreateGenerator();
        var report = CreateMinimalReport();

        // Act
        var html = generator.GenerateHtml(report);

        // Assert
        await Assert.That(html).DoesNotContain("d3js.org");
        await Assert.That(html).DoesNotContain("cdn.");
    }
}
