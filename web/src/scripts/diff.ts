import type { DiffReport } from "./types";
import { enableSorting } from "./tables";

export function formatDelta(
  value: number,
  inverseGood: boolean = false,
): string {
  if (value === 0) return '<span class="delta delta-flat">0</span>';
  const isDown = value < 0;
  const className =
    (isDown && !inverseGood) || (!isDown && inverseGood)
      ? "delta-down"
      : "delta-up";
  const sign = value > 0 ? "+" : "";
  return `<span class="delta ${className}">${sign}${value}</span>`;
}

export function renderDiffTab(diffData: DiffReport | null): void {
  const el = document.getElementById("diff");
  if (!el) return;

  if (!diffData) {
    el.innerHTML =
      '<p style="color:var(--text-muted)">No diff data available.</p>';
    return;
  }

  const totals = diffData.totals;
  const projects = diffData.projects || [];
  const diagnostics = diffData.diagnostics || {};

  const topMiChanges = projects
    .filter((p) => !p.isAdded && !p.isRemoved)
    .sort(
      (a, b) =>
        Math.abs(b.maintainabilityDelta) - Math.abs(a.maintainabilityDelta),
    )
    .slice(0, 10);

  el.innerHTML = `
    <div class="section">
      <h2>Summary Deltas</h2>
      <div class="cards">
        <div class="card"><div class="card-value">${totals.headProjects}</div><div class="card-label">Projects ${formatDelta(totals.projectsDelta)}</div></div>
        <div class="card"><div class="card-value">${totals.headTypes}</div><div class="card-label">Types ${formatDelta(totals.typesDelta)}</div></div>
        <div class="card"><div class="card-value">${totals.headMethods}</div><div class="card-label">Methods ${formatDelta(totals.methodsDelta)}</div></div>
        <div class="card"><div class="card-value">${totals.headCyclomaticComplexity}</div><div class="card-label">Total Complexity ${formatDelta(totals.cyclomaticComplexityDelta)}</div></div>
        <div class="card"><div class="card-value">${totals.headLinesOfCode.toLocaleString()}</div><div class="card-label">Lines of Code ${formatDelta(totals.linesOfCodeDelta)}</div></div>
        <div class="card"><div class="card-value">${totals.headAvgMaintainabilityIndex}</div><div class="card-label">Avg Maintainability ${formatDelta(totals.avgMaintainabilityDelta, true)}</div></div>
        <div class="card"><div class="card-value" style="color:${totals.headErrors > 0 ? "var(--error)" : "var(--success)"}">${totals.headErrors}</div><div class="card-label">Errors ${formatDelta(totals.errorsDelta, true)}</div></div>
        <div class="card"><div class="card-value" style="color:${totals.headWarnings > 0 ? "var(--warning)" : "var(--success)"}">${totals.headWarnings}</div><div class="card-label">Warnings ${formatDelta(totals.warningsDelta, true)}</div></div>
      </div>
    </div>
    <div class="section">
      <h2>Projects with Biggest Maintainability Changes</h2>
      ${
        topMiChanges.length > 0
          ? `
      <table>
        <thead><tr><th>Project</th><th>MI</th><th>Delta</th><th>Complexity &#916;</th><th>LOC &#916;</th><th>Warnings &#916;</th></tr></thead>
        <tbody>${topMiChanges
          .map(
            (p) => `
          <tr>
            <td>${p.name}</td>
            <td>${p.head.avgMaintainabilityIndex}</td>
            <td>${formatDelta(p.maintainabilityDelta, true)}</td>
            <td>${formatDelta(p.cyclomaticComplexityDelta)}</td>
            <td>${formatDelta(p.linesOfCodeDelta)}</td>
            <td>${formatDelta(p.warningsDelta, true)}</td>
          </tr>`,
          )
          .join("")}
        </tbody>
      </table>`
          : '<p style="color:var(--text-muted)">No project deltas available.</p>'
      }
    </div>
    <div class="section">
      <h2>Diagnostics Changes</h2>
      <div class="cards">
        <div class="card"><div class="card-value">${diagnostics.newErrors || 0}</div><div class="card-label">New Errors</div></div>
        <div class="card"><div class="card-value">${diagnostics.resolvedErrors || 0}</div><div class="card-label">Resolved Errors</div></div>
        <div class="card"><div class="card-value">${diagnostics.newWarnings || 0}</div><div class="card-label">New Warnings</div></div>
        <div class="card"><div class="card-value">${diagnostics.resolvedWarnings || 0}</div><div class="card-label">Resolved Warnings</div></div>
      </div>
    </div>
  `;
  enableSorting();
}
