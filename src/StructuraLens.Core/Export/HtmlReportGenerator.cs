using System.Text;
using System.Text.Json;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Export;

/// <summary>
/// Generates a single-file interactive HTML report.
/// </summary>
public sealed class HtmlReportGenerator : IReportGenerator
{
    private readonly IReportExporter _reportExporter;

    public HtmlReportGenerator(IReportExporter reportExporter)
    {
        _reportExporter = reportExporter;
    }

    /// <inheritdoc />
    public string GenerateHtml(AnalysisReport report)
    {
        // Cast to CompactReportExporter to access the hierarchical export method
        var exporter = _reportExporter as CompactReportExporter;
        var compactReport = exporter?.ExportHierarchical(report, includeMethodDetails: true, includeTypeDetails: true)
                            ?? _reportExporter.Export(report, includeMethodDetails: true, includeTypeDetails: true);
        
        var jsonData = JsonSerializer.Serialize(compactReport, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        // Build full diagnostics data for the HTML report
        var diagnosticsData = BuildDiagnosticsJson(report);

        return GenerateHtml(report, jsonData, diagnosticsData);
    }

    private string BuildDiagnosticsJson(AnalysisReport report)
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

    private string GenerateHtml(AnalysisReport report, string compactJson, string diagnosticsJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>StructuraLens Report - {Path.GetFileName(report.SolutionPath)}</title>");
        sb.AppendLine("  <script src=\"https://d3js.org/d3.v7.min.js\"></script>");
        sb.AppendLine(GenerateStyles());
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");
        sb.AppendLine(GenerateHeader(report));
        sb.AppendLine(GenerateTabs());
        sb.AppendLine(GenerateTabContents());
        sb.AppendLine("  </div>");
        sb.AppendLine("  <footer class=\"copyright\">&copy; " + DateTime.UtcNow.Year + " Dark Peak Development. All rights reserved.</footer>");
        sb.AppendLine($"  <script>const reportData = {compactJson};</script>");
        sb.AppendLine($"  <script>const diagnosticsData = {diagnosticsJson};</script>");
        sb.AppendLine(GenerateJavaScript());
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private string GenerateStyles()
    {
        return """
  <style>
    :root {
      --bg: #1a1a2e;
      --bg-card: #16213e;
      --bg-hover: #1f3460;
      --text: #eee;
      --text-muted: #888;
      --accent: #4cc9f0;
      --success: #4ade80;
      --warning: #fbbf24;
      --error: #f87171;
      --border: #334;
      --node-default: #1f3460;
      --heat-green: #2d5a3d;
      --heat-yellow: #6b5b2d;
      --heat-red: #5a2d2d;
      --node-text: #eee;
    }
    @media (prefers-color-scheme: light) {
      :root {
        --bg: #f5f5f7;
        --bg-card: #fff;
        --bg-hover: #e8e8ec;
        --text: #1d1d1f;
        --text-muted: #6e6e73;
        --accent: #0066cc;
        --success: #28a745;
        --warning: #f0ad4e;
        --error: #dc3545;
        --border: #d2d2d7;
        --node-default: #a0b4d4;
        --heat-green: #a8d5ba;
        --heat-yellow: #f0e6b8;
        --heat-red: #e8b8b8;
        --node-text: #1d1d1f;
      }
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: system-ui, -apple-system, sans-serif; background: var(--bg); color: var(--text); line-height: 1.5; }
    .container { width: 100%; margin: 0 auto; padding: 20px; }
    h1 { font-size: 1.5rem; font-weight: 600; }
    h2 { font-size: 1.2rem; font-weight: 500; margin-bottom: 1rem; color: var(--accent); }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; padding-bottom: 15px; border-bottom: 1px solid var(--border); }
    .header-info { font-size: 0.85rem; color: var(--text-muted); }
    .tabs { display: flex; gap: 5px; flex-wrap: wrap; }
    .tab { padding: 10px 20px; background: var(--bg-card); border: 1px solid var(--border); border-bottom: none; border-radius: 6px 6px 0 0; cursor: pointer; transition: all 0.2s; margin-bottom: -1px; }
    .tab:hover { background: var(--bg-hover); }
    .tab.active { background: var(--accent); color: var(--bg); border-color: var(--accent); }
    .tab-content { display: none; background: var(--bg-card); border: 1px solid var(--border); border-radius: 0 0 6px 6px; padding: 20px; }
    .tab-content.active { display: block; }
    .filter-bar { margin-bottom: 20px; display: flex; gap: 15px; align-items: center; flex-wrap: wrap; }
    .filter-bar label { font-size: 0.9rem; color: var(--text-muted); }
    .filter-bar select { padding: 8px 12px; background: var(--bg); border: 1px solid var(--border); color: var(--text); border-radius: 4px; }
    .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px; margin-bottom: 20px; }
    .card { background: var(--bg); padding: 15px; border-radius: 8px; border: 1px solid var(--border); }
    .card-value { font-size: 1.8rem; font-weight: 700; color: var(--accent); }
    .card-label { font-size: 0.8rem; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.5px; }
    table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
    th, td { padding: 10px 12px; text-align: left; border-bottom: 1px solid var(--border); }
    th { background: var(--bg); color: var(--text-muted); font-weight: 500; text-transform: uppercase; font-size: 0.75rem; letter-spacing: 0.5px; cursor: pointer; user-select: none; }
    th:hover { color: var(--accent); }
    th.sort-asc::after { content: ' ▲'; color: var(--accent); }
    th.sort-desc::after { content: ' ▼'; color: var(--accent); }
    tr:hover { background: var(--bg-hover); }
    .badge { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 0.75rem; font-weight: 500; }
    .badge-error { background: var(--error); color: #fff; }
    .badge-warning { background: var(--warning); color: #000; }
    .badge-info { background: var(--accent); color: #000; }
    .badge-success { background: var(--success); color: #000; }
    .graph-container { width: 100%; height: 500px; background: var(--bg); border-radius: 8px; border: 1px solid var(--border); overflow: hidden; }
    .graph-fullpage { height: calc(100vh - 200px); min-height: 500px; }
    .graph-container svg { width: 100%; height: 100%; }
    .node circle { stroke: var(--accent); stroke-width: 2px; cursor: pointer; }
    .node text { fill: var(--node-text); font-size: 11px; pointer-events: none; font-weight: 500; }
    .link { stroke: var(--border); stroke-opacity: 0.6; }
    .tooltip { position: absolute; background: var(--bg-card); border: 1px solid var(--border); padding: 10px; border-radius: 6px; font-size: 0.85rem; pointer-events: none; z-index: 100; }
    .section { margin-bottom: 30px; }
    .passed { color: var(--success); }
    .failed { color: var(--error); }
    .metric-bar { height: 8px; background: var(--bg); border-radius: 4px; overflow: hidden; margin-top: 5px; }
    .metric-bar-fill { height: 100%; border-radius: 4px; transition: width 0.3s; }
    .mi-good { background: var(--success); }
    .mi-medium { background: var(--warning); }
    .mi-poor { background: var(--error); }
    .copyright { text-align: center; padding: 20px; color: var(--text-muted); font-size: 12px; border-top: 1px solid var(--border); margin-top: 30px; }
    
    /* Tree structure styles */
    .tree { list-style: none; padding-left: 0; }
    .tree-node { margin: 0; padding: 0; }
    .tree-item { display: flex; align-items: center; padding: 8px 12px; cursor: pointer; border-radius: 4px; transition: background 0.2s; }
    .tree-item:hover { background: var(--bg-hover); }
    .tree-toggle { margin-right: 8px; color: var(--text-muted); font-size: 12px; width: 16px; text-align: center; user-select: none; }
    .tree-toggle.expandable { cursor: pointer; }
    .tree-toggle.expanded::before { content: '▼'; }
    .tree-toggle.collapsed::before { content: '▶'; }
    .tree-label { flex: 1; display: flex; align-items: center; gap: 8px; font-size: 0.9rem; }
    .tree-label-text { font-weight: 500; }
    .tree-metrics { display: flex; gap: 12px; font-size: 0.85rem; color: var(--text-muted); }
    .tree-metric { display: flex; align-items: center; gap: 4px; }
    .tree-metric-label { font-weight: 400; }
    .tree-metric-value { font-weight: 500; color: var(--text); }
    .tree-children { list-style: none; padding-left: 24px; display: none; }
    .tree-children.expanded { display: block; }
    .tree-icon { color: var(--accent); font-size: 14px; }
    .tree-level-project .tree-label-text { color: var(--accent); font-weight: 600; font-size: 1rem; }
    .tree-level-namespace .tree-label-text { color: var(--text); }
    .tree-level-type .tree-label-text { color: var(--text); font-weight: 400; }
    .tree-level-method .tree-label-text { color: var(--text-muted); font-weight: 400; font-size: 0.85rem; }
    
    @media (max-width: 768px) {
      .cards { grid-template-columns: repeat(2, 1fr); }
      .header { flex-direction: column; align-items: flex-start; gap: 10px; }
      .tree-metrics { flex-direction: column; gap: 4px; }
    }
  </style>
""";
    }

    private string GenerateHeader(AnalysisReport report)
    {
        var timestamp = report.AnalyzedAt.ToString("yyyy-MM-dd HH:mm:ss UTC");
        var solutionName = Path.GetFileName(report.SolutionPath);
        return $"""
    <div class="header">
      <h1>📊 StructuraLens Report</h1>
      <div class="header-info">
        <div><strong>{solutionName}</strong></div>
        <div>Analyzed: {timestamp}</div>
      </div>
    </div>
""";
    }

    private string GenerateTabs()
    {
        return """
    <div class="tabs">
      <div class="tab active" data-tab="summary">Summary</div>
      <div class="tab" data-tab="projects">Projects</div>
      <div class="tab" data-tab="coupling">Coupling</div>
      <div class="tab" data-tab="graph">Graph</div>
      <div class="tab" data-tab="diagnostics">Diagnostics</div>
    </div>
""";
    }

    private string GenerateTabContents()
    {
        return """
    <div id="summary" class="tab-content active"></div>
    <div id="projects" class="tab-content"></div>
    <div id="coupling" class="tab-content"></div>
    <div id="graph" class="tab-content"></div>
    <div id="diagnostics" class="tab-content"></div>
""";
    }

    private string GenerateJavaScript()
    {
        return """
  <script>
    // Tab switching
    let graphRendered = false;
    document.querySelectorAll('.tab').forEach(tab => {
      tab.addEventListener('click', () => {
        document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
        document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
        tab.classList.add('active');
        document.getElementById(tab.dataset.tab).classList.add('active');
        // Render graph when tab becomes visible
        if (tab.dataset.tab === 'graph' && !graphRendered) {
          graphRendered = true;
          setTimeout(() => renderCurrentGraph(), 50);
        }
      });
    });

    // Sortable tables
    function makeSortable(table) {
      const headers = table.querySelectorAll('th');
      const tbody = table.querySelector('tbody');
      if (!tbody) return;
      
      headers.forEach((header, index) => {
        header.addEventListener('click', () => {
          const rows = Array.from(tbody.querySelectorAll('tr'));
          const isAsc = header.classList.contains('sort-asc');
          
          // Clear other sort indicators
          headers.forEach(h => h.classList.remove('sort-asc', 'sort-desc'));
          header.classList.add(isAsc ? 'sort-desc' : 'sort-asc');
          
          rows.sort((a, b) => {
            const aCell = a.cells[index];
            const bCell = b.cells[index];
            let aVal = aCell.textContent.trim();
            let bVal = bCell.textContent.trim();
            
            // Try numeric comparison
            const aNum = parseFloat(aVal.replace(/[^0-9.-]/g, ''));
            const bNum = parseFloat(bVal.replace(/[^0-9.-]/g, ''));
            
            if (!isNaN(aNum) && !isNaN(bNum)) {
              return isAsc ? bNum - aNum : aNum - bNum;
            }
            // String comparison
            return isAsc ? bVal.localeCompare(aVal) : aVal.localeCompare(bVal);
          });
          
          rows.forEach(row => tbody.appendChild(row));
        });
      });
    }

    // Apply sorting to all tables after render
    function enableSorting() {
      document.querySelectorAll('table').forEach(makeSortable);
    }

    // Render summary tab
    function renderSummary() {
      const d = reportData;
      const totalErrors = d.prj.reduce((s, p) => s + (p.err || 0), 0);
      const totalWarnings = d.prj.reduce((s, p) => s + (p.warn || 0), 0);
      const totalCC = d.prj.reduce((s, p) => s + p.cc, 0);
      const totalLOC = d.prj.reduce((s, p) => s + p.loc, 0);
      const avgMI = d.prj.length > 0 ? (d.prj.reduce((s, p) => s + p.mi, 0) / d.prj.length).toFixed(1) : 0;
      
      document.getElementById('summary').innerHTML = `
        <div class="cards">
          <div class="card"><div class="card-value">${d.prj.length}</div><div class="card-label">Projects</div></div>
          <div class="card"><div class="card-value">${d.prj.reduce((s,p) => s + p.tc, 0)}</div><div class="card-label">Types</div></div>
          <div class="card"><div class="card-value">${d.prj.reduce((s,p) => s + p.mc, 0)}</div><div class="card-label">Methods</div></div>
          <div class="card"><div class="card-value">${totalCC}</div><div class="card-label">Total Complexity</div></div>
          <div class="card"><div class="card-value">${totalLOC.toLocaleString()}</div><div class="card-label">Lines of Code</div></div>
          <div class="card"><div class="card-value">${avgMI}</div><div class="card-label">Avg Maintainability</div></div>
          <div class="card"><div class="card-value" style="color: ${totalErrors > 0 ? 'var(--error)' : 'var(--success)'}">${totalErrors}</div><div class="card-label">Compiler Errors</div></div>
          <div class="card"><div class="card-value" style="color: ${totalWarnings > 0 ? 'var(--warning)' : 'var(--success)'}">${totalWarnings}</div><div class="card-label">Compiler Warnings</div></div>
        </div>
        ${d.l ? `
        <div class="section">
          <h2>Architecture Linting</h2>
          <p class="${d.l.ok ? 'passed' : 'failed'}">${d.l.ok ? '✅ PASSED' : '❌ FAILED'} - ${d.l.r} rules evaluated, ${d.l.e} errors, ${d.l.w} warnings</p>
        </div>` : ''}
        <div class="section">
          <h2>Projects Overview</h2>
          <table>
             <thead><tr><th>Project</th><th>Types</th><th>Methods</th><th>Cyclomatic Complexity</th><th>Lines of Code</th><th>Maintainability Index</th><th>Instability</th><th>Issues</th></tr></thead>
            <tbody>${d.prj.map(p => `
              <tr>
                <td>${p.n}</td>
                <td>${p.tc}</td>
                <td>${p.mc}</td>
                <td>${p.cc}</td>
                <td>${p.loc}</td>
                <td>
                  <span>${p.mi}</span>
                  <div class="metric-bar"><div class="metric-bar-fill ${p.mi >= 60 ? 'mi-good' : p.mi >= 40 ? 'mi-medium' : 'mi-poor'}" style="width: ${p.mi}%"></div></div>
                </td>
                <td>${p.i.toFixed(2)}</td>
                <td>${(p.err || 0) > 0 ? `<span class="badge badge-error">${p.err} errors</span> ` : ''}${(p.warn || 0) > 0 ? `<span class="badge badge-warning">${p.warn} warnings</span>` : (p.err || 0) === 0 ? '<span class="badge badge-success">Clean</span>' : ''}</td>
              </tr>`).join('')}
            </tbody>
          </table>
        </div>
      `;
    }

    // Render projects tab
    function renderProjects() {
      const d = reportData;
      document.getElementById('projects').innerHTML = `
        <div class="filter-bar">
          <label>Filter by project:</label>
          <select id="projectFilter">
            <option value="">All Projects</option>
            ${d.prj.map(p => `<option value="${p.n}">${p.n}</option>`).join('')}
          </select>
          <label style="margin-left: 20px;">View:</label>
          <select id="viewMode">
            <option value="tree">Tree View (Recommended)</option>
            <option value="table">Table View</option>
          </select>
          <button id="expandAll" style="margin-left: 10px; padding: 6px 12px; background: var(--bg-card); border: 1px solid var(--border); border-radius: 4px; color: var(--text); cursor: pointer;">Expand All</button>
          <button id="collapseAll" style="margin-left: 5px; padding: 6px 12px; background: var(--bg-card); border: 1px solid var(--border); border-radius: 4px; color: var(--text); cursor: pointer;">Collapse All</button>
        </div>
        <div id="projectsTree"></div>
        <div id="projectsTable" style="display: none;"></div>
      `;
      document.getElementById('projectFilter').addEventListener('change', updateProjectsView);
      document.getElementById('viewMode').addEventListener('change', updateProjectsView);
      document.getElementById('expandAll').addEventListener('click', () => expandCollapseAll(true));
      document.getElementById('collapseAll').addEventListener('click', () => expandCollapseAll(false));
      updateProjectsView();
    }

    function updateProjectsView() {
      const viewMode = document.getElementById('viewMode').value;
      const treeContainer = document.getElementById('projectsTree');
      const tableContainer = document.getElementById('projectsTable');
      const expandBtn = document.getElementById('expandAll');
      const collapseBtn = document.getElementById('collapseAll');
      
      if (viewMode === 'tree') {
        treeContainer.style.display = 'block';
        tableContainer.style.display = 'none';
        expandBtn.style.display = 'inline-block';
        collapseBtn.style.display = 'inline-block';
        updateProjectsTree();
      } else {
        treeContainer.style.display = 'none';
        tableContainer.style.display = 'block';
        expandBtn.style.display = 'none';
        collapseBtn.style.display = 'none';
        updateProjectsTable();
      }
    }

    function updateProjectsTree() {
      const filter = document.getElementById('projectFilter').value;
      const projects = filter ? reportData.prj.filter(p => p.n === filter) : reportData.prj;
      document.getElementById('projectsTree').innerHTML = renderTree(projects);
      attachTreeHandlers();
    }

    function updateProjectsTable() {
      const filter = document.getElementById('projectFilter').value;
      const projects = filter ? reportData.prj.filter(p => p.n === filter) : reportData.prj;
      document.getElementById('projectsTable').innerHTML = `
        <table>
           <thead><tr><th>Project</th><th>Types</th><th>Methods</th><th>Cyclomatic Complexity</th><th>Lines of Code</th><th>Max Depth of Inheritance</th><th>Maintainability Index</th><th>Efferent (Ce)</th><th>Afferent (Ca)</th><th>Instability</th></tr></thead>
          <tbody>${projects.map(p => `
            <tr>
              <td><strong>${p.n}</strong></td>
              <td>${p.tc}</td>
              <td>${p.mc}</td>
              <td>${p.cc}</td>
              <td>${p.loc.toLocaleString()}</td>
              <td>${p.dit}</td>
              <td>${p.mi}</td>
              <td>${p.ce}</td>
              <td>${p.ca}</td>
              <td>${p.i.toFixed(2)}</td>
            </tr>`).join('')}
          </tbody>
        </table>
      `;
      enableSorting();
    }

    function renderTree(projects) {
      return `<ul class="tree">${projects.map(p => renderProjectNode(p)).join('')}</ul>`;
    }

    function renderProjectNode(project) {
      const hasNamespaces = project.ns && project.ns.length > 0;
      const hasTypes = project.types && project.types.length > 0;
      const hasChildren = hasNamespaces || hasTypes;
      
      return `
        <li class="tree-node tree-level-project">
          <div class="tree-item" data-level="project">
            <span class="tree-toggle ${hasChildren ? 'expandable collapsed' : ''}" data-node-id="p-${project.n}"></span>
            <div class="tree-label">
              <span class="tree-icon">📦</span>
              <span class="tree-label-text">${project.n}</span>
               <div class="tree-metrics">
                <span class="tree-metric"><span class="tree-metric-label">Types:</span> <span class="tree-metric-value">${project.tc}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Methods:</span> <span class="tree-metric-value">${project.mc}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Cyclomatic Complexity:</span> <span class="tree-metric-value">${project.cc}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Lines of Code:</span> <span class="tree-metric-value">${project.loc.toLocaleString()}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Maintainability Index:</span> <span class="tree-metric-value">${project.mi}</span></span>
              </div>
            </div>
          </div>
          ${hasChildren ? `<ul class="tree-children" data-parent="p-${project.n}">
            ${hasNamespaces ? project.ns.map(ns => renderNamespaceNode(ns, project.n)).join('') : ''}
            ${hasTypes ? project.types.map(t => renderTypeNode(t, project.n)).join('') : ''}
          </ul>` : ''}
        </li>`;
    }

    function renderNamespaceNode(namespace, projectName) {
      const hasTypes = namespace.types && namespace.types.length > 0;
      const nodeId = `ns-${projectName}-${namespace.n}`;
      
      return `
        <li class="tree-node tree-level-namespace">
          <div class="tree-item" data-level="namespace">
            <span class="tree-toggle ${hasTypes ? 'expandable collapsed' : ''}" data-node-id="${nodeId}"></span>
            <div class="tree-label">
              <span class="tree-icon">📁</span>
              <span class="tree-label-text">${namespace.n}</span>
               <div class="tree-metrics">
                <span class="tree-metric"><span class="tree-metric-label">Types:</span> <span class="tree-metric-value">${namespace.tc}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Methods:</span> <span class="tree-metric-value">${namespace.mc}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Cyclomatic Complexity:</span> <span class="tree-metric-value">${namespace.cc}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Lines of Code:</span> <span class="tree-metric-value">${namespace.loc.toLocaleString()}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Maintainability Index:</span> <span class="tree-metric-value">${namespace.mi}</span></span>
              </div>
            </div>
          </div>
          ${hasTypes ? `<ul class="tree-children" data-parent="${nodeId}">
            ${namespace.types.map(t => renderTypeNode(t, nodeId)).join('')}
          </ul>` : ''}
        </li>`;
    }

    function renderTypeNode(type, parentId) {
      const hasMethods = type.m && type.m.length > 0;
      const fullName = type.fn || type.n;
      const nodeId = `t-${parentId}-${fullName.replace(/[^a-zA-Z0-9]/g, '_')}`;
      
      return `
        <li class="tree-node tree-level-type">
          <div class="tree-item" data-level="type">
            <span class="tree-toggle ${hasMethods ? 'expandable collapsed' : ''}" data-node-id="${nodeId}"></span>
            <div class="tree-label">
              <span class="tree-icon">🔷</span>
              <span class="tree-label-text">${type.n}</span>
               <div class="tree-metrics">
                <span class="tree-metric"><span class="tree-metric-label">Methods:</span> <span class="tree-metric-value">${hasMethods ? type.m.length : 0}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Cyclomatic Complexity:</span> <span class="tree-metric-value">${type.cc}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Lines of Code:</span> <span class="tree-metric-value">${type.loc.toLocaleString()}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Maintainability Index:</span> <span class="tree-metric-value">${type.mi}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Depth of Inheritance:</span> <span class="tree-metric-value">${type.dit}</span></span>
              </div>
            </div>
          </div>
          ${hasMethods ? `<ul class="tree-children" data-parent="${nodeId}">
            ${type.m.map(m => renderMethodNode(m)).join('')}
          </ul>` : ''}
        </li>`;
    }

    function renderMethodNode(method) {
      return `
        <li class="tree-node tree-level-method">
          <div class="tree-item" data-level="method">
            <span class="tree-toggle"></span>
            <div class="tree-label">
              <span class="tree-icon">⚡</span>
              <span class="tree-label-text">${method.n}</span>
               <div class="tree-metrics">
                <span class="tree-metric"><span class="tree-metric-label">Cyclomatic Complexity:</span> <span class="tree-metric-value">${method.cc}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Lines of Code:</span> <span class="tree-metric-value">${method.loc}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Maintainability Index:</span> <span class="tree-metric-value">${method.mi}</span></span>
                <span class="tree-metric"><span class="tree-metric-label">Lines:</span> <span class="tree-metric-value">${method.sl}-${method.el}</span></span>
              </div>
            </div>
          </div>
        </li>`;
    }

    function attachTreeHandlers() {
      document.querySelectorAll('.tree-toggle.expandable').forEach(toggle => {
        toggle.addEventListener('click', (e) => {
          e.stopPropagation();
          const nodeId = toggle.getAttribute('data-node-id');
          const children = document.querySelector(`[data-parent="${nodeId}"]`);
          
          if (children) {
            const isExpanded = toggle.classList.contains('expanded');
            if (isExpanded) {
              toggle.classList.remove('expanded');
              toggle.classList.add('collapsed');
              children.classList.remove('expanded');
            } else {
              toggle.classList.remove('collapsed');
              toggle.classList.add('expanded');
              children.classList.add('expanded');
            }
          }
        });
      });
    }

    function expandCollapseAll(expand) {
      const toggles = document.querySelectorAll('.tree-toggle.expandable');
      toggles.forEach(toggle => {
        const nodeId = toggle.getAttribute('data-node-id');
        const children = document.querySelector(`[data-parent="${nodeId}"]`);
        
        if (children) {
          if (expand) {
            toggle.classList.remove('collapsed');
            toggle.classList.add('expanded');
            children.classList.add('expanded');
          } else {
            toggle.classList.remove('expanded');
            toggle.classList.add('collapsed');
            children.classList.remove('expanded');
          }
        }
      });
    }

    // Render coupling tab with table data
    function renderCoupling() {
      const g = reportData.g;
      // Build coupling table from graph edges
      const projectEdges = g.p.e.map(([src, tgt, weight]) => {
        const srcNode = g.p.n.find(n => n[0] === src);
        const tgtNode = g.p.n.find(n => n[0] === tgt);
        return { from: srcNode ? srcNode[1] : src, to: tgtNode ? tgtNode[1] : tgt, weight };
      });
      const namespaceEdges = g.ns.e.map(([src, tgt, weight]) => {
        const srcNode = g.ns.n.find(n => n[0] === src);
        const tgtNode = g.ns.n.find(n => n[0] === tgt);
        return { from: srcNode ? srcNode[1] : src, to: tgtNode ? tgtNode[1] : tgt, weight };
      });

      document.getElementById('coupling').innerHTML = `
        <div class="cards">
          <div class="card"><div class="card-value">${g.p.n.length}</div><div class="card-label">Projects</div></div>
          <div class="card"><div class="card-value">${g.p.e.length}</div><div class="card-label">Project Dependencies</div></div>
          <div class="card"><div class="card-value">${g.ns.n.length}</div><div class="card-label">Namespaces</div></div>
          <div class="card"><div class="card-value">${g.ns.e.length}</div><div class="card-label">Namespace Dependencies</div></div>
        </div>
        <div class="section">
          <h2>Project Dependencies</h2>
          ${projectEdges.length > 0 ? `
          <table>
            <thead><tr><th>From</th><th>To</th><th>References</th></tr></thead>
            <tbody>${projectEdges.map(e => `
              <tr><td>${e.from}</td><td>${e.to}</td><td>${e.weight}</td></tr>`).join('')}
            </tbody>
          </table>` : '<p style="color:var(--text-muted)">No project dependencies</p>'}
        </div>
        <div class="section">
          <h2>Namespace Dependencies</h2>
          ${namespaceEdges.length > 0 ? `
          <table>
            <thead><tr><th>From</th><th>To</th><th>References</th></tr></thead>
            <tbody>${namespaceEdges.slice(0, 100).map(e => `
              <tr><td>${e.from}</td><td>${e.to}</td><td>${e.weight}</td></tr>`).join('')}
            </tbody>
          </table>
          ${namespaceEdges.length > 100 ? `<p style="color:var(--text-muted);margin-top:10px">Showing first 100 of ${namespaceEdges.length} dependencies</p>` : ''}` : '<p style="color:var(--text-muted)">No namespace dependencies</p>'}
        </div>
      `;
      enableSorting();
    }

    // Render graph tab with visualization
    function renderGraphTab() {
      document.getElementById('graph').innerHTML = `
        <div class="filter-bar">
          <label>View:</label>
          <select id="graphSelector">
            <option value="project">Project Dependencies</option>
            <option value="namespace">Namespace Dependencies</option>
          </select>
           <label>Color by:</label>
          <select id="colorMetric">
            <option value="none">None</option>
            <option value="diagnostics" data-project-only="true">Diagnostics Count</option>
            <option value="coupling">Efferent Coupling (Ce)</option>
            <option value="complexity">Cyclomatic Complexity</option>
            <option value="loc">Lines of Code</option>
            <option value="maintainability">Maintainability Index</option>
          </select>
          <label>Size by:</label>
          <select id="sizeMetric">
            <option value="dependencies">Dependencies</option>
            <option value="diagnostics" data-project-only="true">Diagnostics Count</option>
            <option value="coupling">Efferent Coupling (Ce)</option>
            <option value="complexity">Cyclomatic Complexity</option>
            <option value="loc">Lines of Code</option>
            <option value="maintainability">Maintainability Index</option>
          </select>
        </div>
        <div id="graphContainer" class="graph-container graph-fullpage"></div>
      `;
      document.getElementById('graphSelector').addEventListener('change', () => {
        updateMetricOptions();
        renderCurrentGraph();
      });
      document.getElementById('colorMetric').addEventListener('change', () => {
        renderCurrentGraph();
      });
      document.getElementById('sizeMetric').addEventListener('change', () => {
        renderCurrentGraph();
      });
      updateMetricOptions();
    }

    function updateMetricOptions() {
      const graphType = document.getElementById('graphSelector').value;
      const isNamespace = graphType === 'namespace';
      
      // Hide/show diagnostics options based on graph type
      const colorSelect = document.getElementById('colorMetric');
      const sizeSelect = document.getElementById('sizeMetric');
      
      [colorSelect, sizeSelect].forEach(select => {
        const options = select.querySelectorAll('option');
        options.forEach(option => {
          if (option.dataset.projectOnly === 'true') {
            option.style.display = isNamespace ? 'none' : '';
            if (isNamespace && option.selected) {
              // Switch to a different option if diagnostics was selected
              select.value = 'none';
            }
          }
        });
      });
    }

    function renderCurrentGraph() {
      const container = document.getElementById('graphContainer');
      const selector = document.getElementById('graphSelector');
      const colorMetric = document.getElementById('colorMetric').value;
      const sizeMetric = document.getElementById('sizeMetric').value;
      const graphData = selector.value === 'project' ? reportData.g.p : reportData.g.ns;
      container.innerHTML = '';
      renderGraph('graphContainer', graphData, colorMetric, selector.value, sizeMetric);
    }

    // Get color on green-yellow-red scale with muted saturation
    // ratio 0 = green, 0.5 = yellow, 1 = red
    // Interpolates smoothly between the three colors
    function getHeatColor(ratio) {
      ratio = Math.max(0, Math.min(1, ratio));
      
      // Get computed CSS colors for interpolation
      const isDark = window.matchMedia('(prefers-color-scheme: dark)').matches || 
                     !window.matchMedia('(prefers-color-scheme: light)').matches;
      
      // RGB values for dark and light modes
      const colors = isDark ? {
        green: [45, 90, 61],    // #2d5a3d
        yellow: [107, 91, 45],  // #6b5b2d
        red: [90, 45, 45]       // #5a2d2d
      } : {
        green: [168, 213, 186], // #a8d5ba
        yellow: [240, 230, 184], // #f0e6b8
        red: [232, 184, 184]    // #e8b8b8
      };
      
      let r, g, b;
      if (ratio < 0.5) {
        // Interpolate green to yellow
        const t = ratio * 2;
        r = Math.round(colors.green[0] + t * (colors.yellow[0] - colors.green[0]));
        g = Math.round(colors.green[1] + t * (colors.yellow[1] - colors.green[1]));
        b = Math.round(colors.green[2] + t * (colors.yellow[2] - colors.green[2]));
      } else {
        // Interpolate yellow to red
        const t = (ratio - 0.5) * 2;
        r = Math.round(colors.yellow[0] + t * (colors.red[0] - colors.yellow[0]));
        g = Math.round(colors.yellow[1] + t * (colors.red[1] - colors.yellow[1]));
        b = Math.round(colors.yellow[2] + t * (colors.red[2] - colors.yellow[2]));
      }
      
      return `rgb(${r}, ${g}, ${b})`;
    }

    function getProjectMetrics(name) {
      return reportData.prj.find(p => p.n === name);
    }

    function getNodeColor(node, colorMetric, graphType) {
      if (colorMetric === 'none') return 'var(--node-default)';
      
      let metrics = null;
      let allMetrics = [];
      
      if (graphType === 'project') {
        metrics = getProjectMetrics(node.name);
        allMetrics = reportData.prj;
      } else {
        // For namespace view, use the metrics directly from the node
        metrics = {
          cc: node.cc,
          loc: node.loc,
          mi: node.mi,
          ce: node.ce,
          ca: node.ca,
          instability: node.instability
        };
        // Get all namespace metrics for normalization
        allMetrics = reportData.g.ns.n.map(([id, name, loc, cc, mi, tc, mc, ce, ca, instability]) => 
          ({ cc, loc, mi, ce, ca, instability })
        );
      }
      
      if (!metrics) return 'var(--node-default)';
      
      let value, values;
      
      switch (colorMetric) {
        case 'diagnostics':
          // Diagnostics not available for namespaces
          if (graphType === 'namespace') return 'var(--node-default)';
          value = (metrics.err || 0) * 5 + (metrics.warn || 0); // Errors weighted 5x
          values = allMetrics.map(p => (p.err || 0) * 5 + (p.warn || 0));
          break;
        case 'coupling':
          value = metrics.ce || 0;
          values = allMetrics.map(m => m.ce || 0);
          break;
        case 'complexity':
          value = metrics.cc || 0;
          values = allMetrics.map(m => m.cc || 0);
          break;
        case 'loc':
          value = metrics.loc || 0;
          values = allMetrics.map(m => m.loc || 0);
          break;
        case 'maintainability':
          // Maintainability is inverse - lower MI is worse
          value = 100 - (metrics.mi || 100);
          values = allMetrics.map(m => 100 - (m.mi || 100));
          break;
        default:
          return 'var(--node-default)';
      }
      
      const min = Math.min(...values);
      const max = Math.max(...values);
      if (max === min) return getHeatColor(0);
      const ratio = (value - min) / (max - min);
      return getHeatColor(ratio);
    }

    function getNodeSizeValue(node, sizeMetric, graphType, outboundCount) {
      if (sizeMetric === 'dependencies') return outboundCount;
      
      let metrics = null;
      
      if (graphType === 'project') {
        metrics = getProjectMetrics(node.name);
      } else {
        // For namespace view, use the metrics directly from the node
        metrics = {
          cc: node.cc,
          loc: node.loc,
          mi: node.mi,
          ce: node.ce,
          ca: node.ca,
          instability: node.instability
        };
      }
      
      if (!metrics) return outboundCount; // Fallback to dependencies
      
      switch (sizeMetric) {
        case 'diagnostics':
          // Diagnostics not available for namespaces
          if (graphType === 'namespace') return outboundCount;
          return (metrics.err || 0) * 5 + (metrics.warn || 0);
        case 'coupling':
          return metrics.ce || 0;
        case 'complexity':
          return metrics.cc || 0;
        case 'loc':
          return metrics.loc || 0;
        case 'maintainability':
          return 100 - (metrics.mi || 100); // Lower MI = bigger node
        default:
          return outboundCount;
      }
    }

    function renderGraph(containerId, graphData, colorMetric = 'none', graphType = 'project', sizeMetric = 'dependencies') {
      const container = document.getElementById(containerId);
      const width = container.clientWidth || 800;
      const height = container.clientHeight || 600;
      
      if (!graphData.n || graphData.n.length === 0) {
        container.innerHTML = '<div style="display:flex;align-items:center;justify-content:center;height:100%;color:var(--text-muted)">No dependencies to display</div>';
        return;
      }

      const links = graphData.e.map(([source, target, weight]) => ({ source, target, weight }));
      
      // Count outbound dependencies for each node (how many things it depends on)
      const outboundCounts = {};
      links.forEach(l => {
        outboundCounts[l.source] = (outboundCounts[l.source] || 0) + l.weight;
      });
      
      // Parse node data based on graph type
      // Project nodes: [id, name, size]
      // Namespace nodes: [id, name, loc, cc, mi, tc, mc, ce, ca, instability]
      const prelimNodes = graphData.n.map(nodeData => {
        if (graphType === 'project') {
          const [id, name, size] = nodeData;
          return { id, name, size, depCount: outboundCounts[id] || 0 };
        } else {
          const [id, name, loc, cc, mi, tc, mc, ce, ca, instability] = nodeData;
          return { 
            id, name, 
            loc, cc, mi, tc, mc, ce, ca, instability,
            size: loc, // Default size to LOC for compatibility
            depCount: outboundCounts[id] || 0 
          };
        }
      });
      
      // Calculate size values based on selected metric
      const sizeValues = prelimNodes.map(n => getNodeSizeValue(n, sizeMetric, graphType, n.depCount));
      const minValue = Math.min(...sizeValues, 0);
      const maxValue = Math.max(...sizeValues, 1);
      const minRadius = 15;
      const maxRadius = 200;
      
      const nodes = prelimNodes.map((n, i) => {
        const value = sizeValues[i];
        const ratio = maxValue === minValue ? 0.5 : (value - minValue) / (maxValue - minValue);
        return { ...n, radius: minRadius + ratio * (maxRadius - minRadius), sizeValue: value };
      });

      const svg = d3.select(`#${containerId}`).append('svg')
        .attr('width', width)
        .attr('height', height);
      
      // Add zoom behavior
      const g = svg.append('g');
      const zoom = d3.zoom()
        .scaleExtent([0.1, 4])
        .on('zoom', (e) => g.attr('transform', e.transform));
      svg.call(zoom);
      
      // Calculate average radius for link distance scaling
      const avgRadius = nodes.reduce((sum, n) => sum + n.radius, 0) / nodes.length;
      
      const simulation = d3.forceSimulation(nodes)
        .force('link', d3.forceLink(links).id(d => d.id)
          .distance(d => {
            const sourceNode = nodes.find(n => n.id === (d.source.id ?? d.source));
            const targetNode = nodes.find(n => n.id === (d.target.id ?? d.target));
            const sourceR = sourceNode?.radius || avgRadius;
            const targetR = targetNode?.radius || avgRadius;
            return sourceR + targetR + 100; // Base gap between node edges
          }))
        .force('charge', d3.forceManyBody().strength(-800).distanceMax(600))
        .force('center', d3.forceCenter(width / 2, height / 2))
        .force('collision', d3.forceCollide().radius(d => d.radius + 30).strength(1))
        .force('x', d3.forceX(width / 2).strength(0.03))
        .force('y', d3.forceY(height / 2).strength(0.03));

      // Arrow marker - fixed size regardless of stroke width
      svg.append('defs').append('marker')
        .attr('id', `arrow-${containerId}`)
        .attr('viewBox', '0 -5 10 10')
        .attr('refX', 10)
        .attr('refY', 0)
        .attr('markerWidth', 10)
        .attr('markerHeight', 10)
        .attr('markerUnits', 'userSpaceOnUse')
        .attr('orient', 'auto')
        .append('path')
        .attr('fill', 'var(--accent)')
        .attr('d', 'M0,-5L10,0L0,5');

      const link = g.append('g')
        .selectAll('line')
        .data(links)
        .join('line')
        .attr('class', 'link')
        .attr('stroke', 'var(--accent)')
        .attr('stroke-opacity', 0.6)
        .attr('stroke-width', d => Math.min(Math.max(d.weight / 2, 1), 4))
        .attr('marker-end', `url(#arrow-${containerId})`);

      const node = g.append('g')
        .selectAll('g')
        .data(nodes)
        .join('g')
        .attr('class', 'node')
        .call(d3.drag()
          .on('start', (e, d) => { if (!e.active) simulation.alphaTarget(0.3).restart(); d.fx = d.x; d.fy = d.y; })
          .on('drag', (e, d) => { d.fx = e.x; d.fy = e.y; })
          .on('end', (e, d) => { if (!e.active) simulation.alphaTarget(0); d.fx = null; d.fy = null; }));

      node.append('circle')
        .attr('r', d => d.radius)
        .attr('fill', d => getNodeColor(d, colorMetric, graphType));

      node.append('text')
        .attr('dy', 4)
        .attr('text-anchor', 'middle')
        .attr('font-size', d => Math.max(9, Math.min(12, d.radius / 3)))
        .text(d => d.name);

      node.append('title').text(d => `${d.name}\nOutbound deps: ${d.depCount}\nSize metric: ${d.sizeValue}\nLOC: ${d.size}`);

      // Position arrows at edge of target circle
      simulation.on('tick', () => {
        link.each(function(d) {
          const dx = d.target.x - d.source.x;
          const dy = d.target.y - d.source.y;
          const dist = Math.sqrt(dx * dx + dy * dy);
          if (dist === 0) return;
          const targetRadius = d.target.radius || 20;
          const ratio = (dist - targetRadius - 5) / dist;
          d.targetX = d.source.x + dx * ratio;
          d.targetY = d.source.y + dy * ratio;
        });
        link.attr('x1', d => d.source.x).attr('y1', d => d.source.y)
            .attr('x2', d => d.targetX || d.target.x).attr('y2', d => d.targetY || d.target.y);
        node.attr('transform', d => `translate(${d.x},${d.y})`);
      });
    }


    // Render diagnostics tab
    function renderDiagnostics() {
      if (!diagnosticsData || diagnosticsData.length === 0) {
        document.getElementById('diagnostics').innerHTML = '<p class="passed">✅ No compiler diagnostics</p>';
        return;
      }
      const projects = [...new Set(diagnosticsData.map(d => d.project))];
      document.getElementById('diagnostics').innerHTML = `
        <div class="filter-bar">
          <label>Filter by project:</label>
          <select id="diagProjectFilter">
            <option value="">All Projects</option>
            ${projects.map(p => `<option value="${p}">${p}</option>`).join('')}
          </select>
          <label>Severity:</label>
          <select id="diagSeverityFilter">
            <option value="">All</option>
            <option value="error">Errors</option>
            <option value="warning">Warnings</option>
            <option value="info">Info</option>
          </select>
        </div>
        <div id="diagnosticsTable"></div>
      `;
      document.getElementById('diagProjectFilter').addEventListener('change', updateDiagnosticsTable);
      document.getElementById('diagSeverityFilter').addEventListener('change', updateDiagnosticsTable);
      updateDiagnosticsTable();
    }

    function updateDiagnosticsTable() {
      const projFilter = document.getElementById('diagProjectFilter').value;
      const sevFilter = document.getElementById('diagSeverityFilter').value;
      let filtered = diagnosticsData;
      if (projFilter) filtered = filtered.filter(d => d.project === projFilter);
      if (sevFilter) filtered = filtered.filter(d => d.severity === sevFilter);
      
      document.getElementById('diagnosticsTable').innerHTML = `
        <p style="color:var(--text-muted);margin-bottom:10px">${filtered.length} diagnostics</p>
        <table>
          <thead><tr><th>Project</th><th>ID</th><th>Severity</th><th>Message</th><th>File</th><th>Line</th></tr></thead>
          <tbody>${filtered.slice(0, 100).map(d => `
            <tr>
              <td>${d.project}</td>
              <td><code>${d.id}</code></td>
              <td><span class="badge badge-${d.severity}">${d.severity}</span></td>
              <td>${d.message}</td>
              <td>${d.file}</td>
              <td>${d.line}</td>
            </tr>`).join('')}
          </tbody>
        </table>
        ${filtered.length > 100 ? `<p style="color:var(--text-muted);margin-top:10px">Showing first 100 of ${filtered.length} diagnostics</p>` : ''}
      `;
      enableSorting();
    }

    // Initialize
    renderSummary();
    renderProjects();
    renderCoupling();
    renderGraphTab();
    renderLinting();
    renderDiagnostics();
    enableSorting();
  </script>
""";
    }
}
