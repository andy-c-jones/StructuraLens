import type { CompactReport } from "./types";
import { enableSorting } from "./tables";

export function renderSummary(reportData: CompactReport): void {
  const el = document.getElementById("summary");
  if (!el) return;

  const d = reportData;
  const totalErrors = d.prj.reduce((s, p) => s + (p.err || 0), 0);
  const totalWarnings = d.prj.reduce((s, p) => s + (p.warn || 0), 0);
  const totalCC = d.prj.reduce((s, p) => s + p.cc, 0);
  const totalLOC = d.prj.reduce((s, p) => s + p.loc, 0);
  const avgMI =
    d.prj.length > 0
      ? (d.prj.reduce((s, p) => s + p.mi, 0) / d.prj.length).toFixed(1)
      : 0;

  el.innerHTML = `
    <div class="cards">
      <div class="card"><div class="card-value">${d.prj.length}</div><div class="card-label">Projects</div></div>
      <div class="card"><div class="card-value">${d.prj.reduce((s, p) => s + p.tc, 0)}</div><div class="card-label">Types</div></div>
      <div class="card"><div class="card-value">${d.prj.reduce((s, p) => s + p.mc, 0)}</div><div class="card-label">Methods</div></div>
      <div class="card"><div class="card-value">${totalCC}</div><div class="card-label">Total Complexity</div></div>
      <div class="card"><div class="card-value">${totalLOC.toLocaleString()}</div><div class="card-label">Lines of Code</div></div>
      <div class="card"><div class="card-value">${avgMI}</div><div class="card-label">Avg Maintainability</div></div>
      <div class="card"><div class="card-value" style="color: ${totalErrors > 0 ? "var(--error)" : "var(--success)"}">${totalErrors}</div><div class="card-label">Compiler Errors</div></div>
      <div class="card"><div class="card-value" style="color: ${totalWarnings > 0 ? "var(--warning)" : "var(--success)"}">${totalWarnings}</div><div class="card-label">Compiler Warnings</div></div>
    </div>
    ${
      d.l
        ? `
    <div class="section">
      <h2>Architecture Linting</h2>
      <p class="${d.l.ok ? "passed" : "failed"}">${d.l.ok ? "&#10003; PASSED" : "&#10007; FAILED"} - ${d.l.r} rules evaluated, ${d.l.e} errors, ${d.l.w} warnings</p>
    </div>`
        : ""
    }
    <div class="section">
      <h2>Projects Overview</h2>
      <table>
        <thead><tr><th>Project</th><th>Types</th><th>Methods</th><th>Cyclomatic Complexity</th><th>Lines of Code</th><th>Maintainability Index</th><th>Instability</th><th>Issues</th></tr></thead>
        <tbody>${d.prj
          .map(
            (p) => `
          <tr>
            <td>${p.n}</td>
            <td>${p.tc}</td>
            <td>${p.mc}</td>
            <td>${p.cc}</td>
            <td>${p.loc}</td>
            <td>
              <span>${p.mi}</span>
              <div class="metric-bar"><div class="metric-bar-fill ${p.mi >= 60 ? "mi-good" : p.mi >= 40 ? "mi-medium" : "mi-poor"}" style="width: ${p.mi}%"></div></div>
            </td>
            <td>${p.i.toFixed(2)}</td>
            <td>${(p.err || 0) > 0 ? `<span class="badge badge-error">${p.err} errors</span> ` : ""}${(p.warn || 0) > 0 ? `<span class="badge badge-warning">${p.warn} warnings</span>` : (p.err || 0) === 0 ? '<span class="badge badge-success">Clean</span>' : ""}</td>
          </tr>`,
          )
          .join("")}
        </tbody>
      </table>
    </div>
  `;

  enableSorting();
}
