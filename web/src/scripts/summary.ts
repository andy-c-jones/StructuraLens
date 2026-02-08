import type { CompactReport } from "./types";
import { enableSorting } from "./tables";
import { 
  renderCardsGrid, 
  renderSection,
  renderMetricBar,
  renderBadge,
  type CardProps 
} from "./componentRenderers";

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

  const summaryCards: CardProps[] = [
    { value: d.prj.length, label: 'Projects' },
    { value: d.prj.reduce((s, p) => s + p.tc, 0), label: 'Types' },
    { value: d.prj.reduce((s, p) => s + p.mc, 0), label: 'Methods' },
    { value: totalCC, label: 'Total Complexity' },
    { value: totalLOC.toLocaleString(), label: 'Lines of Code' },
    { value: avgMI, label: 'Avg Maintainability' },
    { 
      value: totalErrors, 
      label: 'Compiler Errors',
      valueColor: totalErrors > 0 ? 'var(--error)' : 'var(--success)'
    },
    { 
      value: totalWarnings, 
      label: 'Compiler Warnings',
      valueColor: totalWarnings > 0 ? 'var(--warning)' : 'var(--success)'
    }
  ];

  const lintingSection = d.l
    ? `<div class="section">
      <h2>Architecture Linting</h2>
      <p class="${d.l.ok ? "passed" : "failed"}">${d.l.ok ? "&#10003; PASSED" : "&#10007; FAILED"} - ${d.l.r} rules evaluated, ${d.l.e} errors, ${d.l.w} warnings</p>
    </div>`
    : "";

  const projectsTable = `<table>
        <thead><tr><th>Project</th><th>Types</th><th>Methods</th><th>Cyclomatic Complexity</th><th>Lines of Code</th><th>Maintainability Index</th><th>Dependency Ratio</th><th>Issues</th></tr></thead>
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
              ${renderMetricBar({ value: p.mi })}
            </td>
            <td>${p.dr.toFixed(2)}</td>
            <td>${(p.err || 0) > 0 ? renderBadge({ type: 'error', text: `${p.err} errors` }) + ' ' : ''}${(p.warn || 0) > 0 ? renderBadge({ type: 'warning', text: `${p.warn} warnings` }) : (p.err || 0) === 0 ? renderBadge({ type: 'success', text: 'Clean' }) : ''}</td>
          </tr>`,
          )
          .join("")}
        </tbody>
      </table>`;

  el.innerHTML = `
    ${renderCardsGrid(summaryCards)}
    ${lintingSection}
    ${renderSection('Projects Overview', projectsTable)}
  `;

  enableSorting();
}
