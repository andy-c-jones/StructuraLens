using System.Reflection;
using System.Text.Json;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Export;

/// <summary>
/// Generates a single-file interactive HTML report by loading the Astro-built
/// template from an embedded resource and replacing placeholder tokens with
/// real analysis data.
/// </summary>
public sealed class HtmlReportGenerator : IReportGenerator
{
    private const string TemplateResourceName = "StructuraLens.Core.report-template.html";

    /// <summary>
    /// The diff tab button markup to remove when no diff data is present.
    /// Must match the exact HTML emitted by the Astro production build.
    /// </summary>
    private const string DiffTabButton = """<div class="tab active" data-tab="diff">Diff</div>""";

    /// <summary>
    /// The diff tab content div to remove when no diff data is present.
    /// </summary>
    private const string DiffTabContent = """<div id="diff" class="tab-content active"></div>""";

    /// <summary>
    /// Class attribute to update when diff tab is removed (make summary active).
    /// </summary>
    private const string SummaryTabInactive = """<div class="tab" data-tab="summary">Summary</div>""";
    private const string SummaryTabActive = """<div class="tab active" data-tab="summary">Summary</div>""";
    private const string SummaryContentInactive = """<div id="summary" class="tab-content"></div>""";
    private const string SummaryContentActive = """<div id="summary" class="tab-content active"></div>""";
    private const string CouplingTabToken = "{{COUPLING_TAB}}";
    private const string GraphTabToken = "{{GRAPH_TAB}}";
    private const string CouplingContentToken = "{{COUPLING_CONTENT}}";
    private const string GraphContentToken = "{{GRAPH_CONTENT}}";
    private const string CouplingTab = """<div class="tab" data-tab="coupling">Coupling</div>""";
    private const string GraphTab = """<div class="tab" data-tab="graph">Graph</div>""";
    private const string CouplingContent = """<div id="coupling" class="tab-content"></div>""";
    private const string GraphContent = """<div id="graph" class="tab-content"></div>""";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IReportExporter _reportExporter;

    public HtmlReportGenerator(IReportExporter reportExporter)
    {
        _reportExporter = reportExporter;
    }

    /// <inheritdoc />
    public string GenerateHtml(AnalysisReport report)
    {
        var (compactJson, diagnosticsJson) = SerializeReportData(report);
        return BuildHtml(report, compactJson, diagnosticsJson, diffJson: null);
    }

    /// <inheritdoc />
    public string GenerateHtml(AnalysisReport report, AnalysisDiffReport diff)
    {
        var (compactJson, diagnosticsJson) = SerializeReportData(report);
        var diffJson = JsonSerializer.Serialize(diff, JsonOptions);
        return BuildHtml(report, compactJson, diagnosticsJson, diffJson, diff);
    }

    private (string compactJson, string diagnosticsJson) SerializeReportData(AnalysisReport report)
    {
        var compactReport = _reportExporter is CompactReportExporter exporter
            ? exporter.ExportHierarchical(report, includeMethodDetails: true, includeTypeDetails: true)
            : _reportExporter.Export(report, includeMethodDetails: true, includeTypeDetails: true);

        var compactJson = JsonSerializer.Serialize(compactReport, JsonOptions);
        var diagnosticsJson = BuildDiagnosticsJson(report);

        return (compactJson, diagnosticsJson);
    }

    private static string BuildDiagnosticsJson(AnalysisReport report)
    {
        var diagnostics = report.Projects
            .Where(p => p.Diagnostics != null)
            .SelectMany(p => p.Diagnostics!.Diagnostics.Select(d => new
            {
                project = p.Name,
                id = d.Id,
                message = d.Message,
                severity = d.Severity.ToString().ToLower(),
                file = Path.GetFileName(d.FilePath),
                line = d.Line,
                category = d.Category ?? ""
            }))
            .ToList();

        return JsonSerializer.Serialize(diagnostics);
    }

    private string BuildHtml(AnalysisReport report, string compactJson, string diagnosticsJson, string? diffJson, AnalysisDiffReport? diff = null)
    {
        var template = LoadTemplate();

        var solutionName = Path.GetFileName(report.SolutionPath);
        var analyzedAt = report.AnalyzedAt.ToString("yyyy-MM-dd HH:mm:ss UTC");
        var gitInfoHtml = diff is not null ? BuildDiffGitInfoHtml(diff) : BuildGitInfoHtml(report);
        var copyrightYear = DateTime.UtcNow.Year.ToString();

        // Replace simple text placeholders
        var html = template
            .Replace("{{REPORT_TITLE}}", $"StructuraLens Report - {solutionName}")
            .Replace("{{SOLUTION_NAME}}", solutionName)
            .Replace("{{ANALYZED_AT}}", analyzedAt)
            .Replace("{{GIT_INFO_HTML}}", gitInfoHtml)
            .Replace("{{COPYRIGHT_YEAR}}", copyrightYear)
            .Replace("{{TOOL_VERSION}}", report.ToolVersion);

        // Replace JSON data placeholders.
        // These sit inside JS double-quoted string literals produced by Astro's
        // define:vars, e.g.: const reportJson = "{{REPORT_DATA}}";
        // We must escape the JSON so it is valid inside a JS "..." literal.
        html = html.Replace("{{REPORT_DATA}}", EscapeForJsString(compactJson));
        html = html.Replace("{{DIAGNOSTICS_DATA}}", EscapeForJsString(diagnosticsJson));
        html = html.Replace("{{DIFF_DATA}}", EscapeForJsString(diffJson ?? "null"));

        var hasComplexityMetrics = report.AnalysisMode == AnalysisMode.Full;
        html = html.Replace(CouplingTabToken, hasComplexityMetrics ? CouplingTab : string.Empty);
        html = html.Replace(GraphTabToken, hasComplexityMetrics ? GraphTab : string.Empty);
        html = html.Replace(CouplingContentToken, hasComplexityMetrics ? CouplingContent : string.Empty);
        html = html.Replace(GraphContentToken, hasComplexityMetrics ? GraphContent : string.Empty);

        // When there is no diff, remove the diff tab button and content,
        // and make the summary tab active instead.
        if (diffJson is null)
        {
            html = html.Replace(DiffTabButton, string.Empty);
            html = html.Replace(DiffTabContent, string.Empty);
            html = html.Replace(SummaryTabInactive, SummaryTabActive);
            html = html.Replace(SummaryContentInactive, SummaryContentActive);
        }

        return html;
    }

    private static string BuildGitInfoHtml(AnalysisReport report)
    {
        if (report.GitInfo is null)
        {
            return string.Empty;
        }

        var dirtyBadge = report.GitInfo.IsDirty
            ? """ <span class="badge badge-warning">Uncommitted Changes</span>"""
            : string.Empty;

        return $"""
            <div style="font-size: 0.85rem; color: var(--text-muted); margin-top: 5px;">
              <div><strong>Git:</strong> {report.GitInfo.BranchName} @ {report.GitInfo.CommitSha[..7]}{dirtyBadge}</div>
            </div>
            """;
    }

    private static string BuildDiffGitInfoHtml(AnalysisDiffReport diff)
    {
        // For diff reports, show base → head comparison without "Uncommitted Changes" badge
        var baseBranch = diff.Base.BranchName ?? "unknown";
        var headBranch = diff.Head.BranchName ?? "unknown";
        var baseSha = diff.Base.CommitSha?[..7] ?? "unknown";
        var headSha = diff.Head.CommitSha?[..7] ?? "unknown";

        return $"""
            <div style="font-size: 0.85rem; color: var(--text-muted); margin-top: 5px;">
              <div><strong>Git:</strong> {baseBranch} @ {baseSha} → {headBranch} @ {headSha}</div>
            </div>
            """;
    }

    /// <summary>
    /// Escapes a string so it can be safely placed inside a JavaScript
    /// double-quoted string literal (&quot;...&quot;).
    /// </summary>
    private static string EscapeForJsString(string value)
    {
        // Order matters: backslash must be escaped first.
        return value
            .Replace(@"\", @"\\")
            .Replace("\"", @"\""")
            .Replace("\n", @"\n")
            .Replace("\r", @"\r")
            .Replace("\t", @"\t");
    }

    private static string LoadTemplate()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{TemplateResourceName}' not found. " +
                "Ensure the web template was built before compiling (run 'npm run build' in the web/ directory).");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
