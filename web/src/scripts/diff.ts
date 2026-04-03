import type { DiffReport } from "./types";
import { enableSorting } from "./tables";
import { 
  renderCardsGrid, 
  renderSection, 
  renderDeltaIndicator,
  type CardProps 
} from "./componentRenderers";

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
  const hasComplexityMetrics = diffData.hasComplexityMetrics !== false;

  const topMiChanges = hasComplexityMetrics
    ? projects
      .filter((p) => !p.isAdded && !p.isRemoved)
      .sort(
        (a, b) =>
          Math.abs(b.maintainabilityDelta) - Math.abs(a.maintainabilityDelta),
      )
      .slice(0, 10)
    : [];

  const summaryCards: CardProps[] = [
    { 
      value: totals.headProjects, 
      label: `Projects ${renderDeltaIndicator({ value: totals.projectsDelta })}` 
    },
    { 
      value: totals.headErrors, 
      label: `Errors ${renderDeltaIndicator({ value: totals.errorsDelta, inverseGood: true })}`,
      valueColor: totals.headErrors > 0 ? 'var(--error)' : 'var(--success)'
    },
    { 
      value: totals.headWarnings, 
      label: `Warnings ${renderDeltaIndicator({ value: totals.warningsDelta, inverseGood: true })}`,
      valueColor: totals.headWarnings > 0 ? 'var(--warning)' : 'var(--success)'
    }
  ];

  if (hasComplexityMetrics) {
    summaryCards.splice(
      1,
      0,
      { 
        value: totals.headTypes, 
        label: `Types ${renderDeltaIndicator({ value: totals.typesDelta })}` 
      },
      { 
        value: totals.headMethods, 
        label: `Methods ${renderDeltaIndicator({ value: totals.methodsDelta })}` 
      },
    );
  }

  if (hasComplexityMetrics) {
    summaryCards.splice(
      3,
      0,
      {
        value: totals.headCyclomaticComplexity,
        label: `Total Complexity ${renderDeltaIndicator({ value: totals.cyclomaticComplexityDelta })}`,
      },
      {
        value: totals.headLinesOfCode.toLocaleString(),
        label: `Lines of Code ${renderDeltaIndicator({ value: totals.linesOfCodeDelta })}`,
      },
      {
        value: totals.headAvgMaintainabilityIndex,
        label: `Avg Maintainability ${renderDeltaIndicator({ value: totals.avgMaintainabilityDelta, inverseGood: true })}`,
      },
    );
  }

  const miChangesTable = topMiChanges.length > 0
    ? `<table>
        <thead><tr><th>Project</th><th>MI</th><th>Delta</th><th>Complexity &#916;</th><th>LOC &#916;</th><th>Warnings &#916;</th></tr></thead>
        <tbody>${topMiChanges
          .map(
            (p) => `
          <tr>
            <td>${p.name}</td>
            <td>${p.head.avgMaintainabilityIndex}</td>
            <td>${renderDeltaIndicator({ value: p.maintainabilityDelta, inverseGood: true })}</td>
            <td>${renderDeltaIndicator({ value: p.cyclomaticComplexityDelta })}</td>
            <td>${renderDeltaIndicator({ value: p.linesOfCodeDelta })}</td>
            <td>${renderDeltaIndicator({ value: p.warningsDelta, inverseGood: true })}</td>
          </tr>`,
          )
          .join("")}
        </tbody>
      </table>`
    : '<p style="color:var(--text-muted)">No project deltas available.</p>';

  const diagnosticsCards: CardProps[] = [
    { value: diagnostics.newErrors || 0, label: 'New Errors' },
    { value: diagnostics.resolvedErrors || 0, label: 'Resolved Errors' },
    { value: diagnostics.newWarnings || 0, label: 'New Warnings' },
    { value: diagnostics.resolvedWarnings || 0, label: 'Resolved Warnings' }
  ];

  el.innerHTML = `
    ${renderSection('Summary Deltas', renderCardsGrid(summaryCards))}
    ${hasComplexityMetrics ? renderSection('Projects with Biggest Maintainability Changes', miChangesTable) : ''}
    ${renderSection('Diagnostics Changes', renderCardsGrid(diagnosticsCards))}
  `;
  
  enableSorting();
}
