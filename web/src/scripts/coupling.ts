import type { CompactReport } from "./types";
import {
  enableSorting,
  renderPagination,
  attachPaginationListeners,
} from "./tables";
import { renderCardsGrid, renderSection, type CardProps } from "./componentRenderers";

interface NamespaceMetrics {
  name: string;
  typeCount: number;
  loc: number;
  internalDeps: number;
  internalDependents: number;
  depRatio: number;
}

let nsMetricsCurrentPage = 1;
let nsMetricsSearchQuery = "";
const nsMetricsPageSize = 100;
let namespaceMetricsData: NamespaceMetrics[] = [];

function updateNamespaceMetricsTable(): void {
  const searchQuery = nsMetricsSearchQuery.toLowerCase();
  let filtered = namespaceMetricsData;

  if (searchQuery) {
    filtered = filtered.filter(
      (ns) =>
        ns.name.toLowerCase().includes(searchQuery) ||
        ns.typeCount.toString().includes(searchQuery) ||
        ns.loc.toString().includes(searchQuery) ||
        ns.internalDeps.toString().includes(searchQuery) ||
        ns.internalDependents.toString().includes(searchQuery) ||
        ns.depRatio.toString().includes(searchQuery),
    );
  }

  const totalPages = Math.ceil(filtered.length / nsMetricsPageSize);
  const startIdx = (nsMetricsCurrentPage - 1) * nsMetricsPageSize;
  const endIdx = startIdx + nsMetricsPageSize;
  const pageData = filtered.slice(startIdx, endIdx);

  const tableEl = document.getElementById("namespaceMetricsTable");
  if (!tableEl) return;

  tableEl.innerHTML = `
    <p style="color:var(--text-muted);margin-bottom:10px">
      ${filtered.length} namespace${filtered.length !== 1 ? "s" : ""}
      ${filtered.length > nsMetricsPageSize ? ` (showing ${startIdx + 1}-${Math.min(endIdx, filtered.length)})` : ""}
    </p>
    <table>
      <thead>
        <tr>
          <th>Namespace</th>
          <th>Type Count</th>
          <th>LOC</th>
          <th>Internal Dependencies</th>
          <th>Internal Dependents</th>
          <th>Dependency Ratio</th>
        </tr>
      </thead>
      <tbody>
        ${pageData.map(ns => `
          <tr>
            <td>${ns.name}</td>
            <td>${ns.typeCount}</td>
            <td>${ns.loc}</td>
            <td>${ns.internalDeps}</td>
            <td>${ns.internalDependents}</td>
            <td>${ns.depRatio.toFixed(2)}</td>
          </tr>
        `).join('')}
      </tbody>
    </table>
    ${renderPagination(nsMetricsCurrentPage, totalPages)}
  `;

  attachPaginationListeners("namespaceMetricsTable", (page) => {
    nsMetricsCurrentPage = page;
    updateNamespaceMetricsTable();
  });

  enableSorting();
}

export function renderCoupling(reportData: CompactReport): void {
  const el = document.getElementById("coupling");
  if (!el) return;

  const g = reportData.g;

  // Transform namespace nodes to metrics
  // Node format: [id, name, loc, cc, mi, tc, mc, id, idx, dr, ed]
  const namespaceMetrics: NamespaceMetrics[] = g.ns.n.map(node => ({
    name: String(node[1]),
    typeCount: Number(node[5]),
    loc: Number(node[2]),
    internalDeps: Number(node[7]),
    internalDependents: Number(node[8]),
    depRatio: Number(node[9])
  }));

  // Calculate total internal and external dependencies
  const totalInternalDeps = reportData.prj.reduce((sum, p) => sum + (p.id || 0), 0);
  const totalExternalDeps = reportData.prj.reduce((sum, p) => sum + (p.ed || 0), 0);
  const totalBclDeps = reportData.prj.reduce((sum, p) => sum + (p.edb || 0), 0);
  const totalPackageDeps = reportData.prj.reduce((sum, p) => sum + (p.edp || 0), 0);

  const couplingCards: CardProps[] = [
    { value: g.p.n.length, label: 'Projects' },
    { value: totalInternalDeps, label: 'Internal Dependencies' },
    { value: totalExternalDeps, label: 'External Dependencies' },
    { value: totalBclDeps, label: 'BCL Dependencies' },
    { value: totalPackageDeps, label: 'Package Dependencies' }
  ];

  // Internal dependencies by project
  const internalProjectDepsTable = reportData.prj.length > 0
    ? `<table>
        <thead>
          <tr>
            <th>Project</th>
            <th>Internal Dependencies</th>
            <th>Internal Dependents</th>
            <th>Dependency Ratio</th>
          </tr>
        </thead>
        <tbody>
          ${reportData.prj.map(p => `
            <tr>
              <td>${p.n}</td>
              <td>${p.id || 0}</td>
              <td>${p.idx || 0}</td>
              <td>${(p.dr || 0).toFixed(2)}</td>
            </tr>
          `).join('')}
        </tbody>
      </table>`
    : '<p style="color:var(--text-muted)">No projects found</p>';

  // External dependencies by project
  const externalDepsTable = reportData.prj.length > 0 && reportData.prj.some(p => (p.ed || 0) > 0)
    ? `<table>
        <thead><tr><th>Project</th><th>BCL Dependencies</th><th>Package Dependencies</th><th>Total External</th></tr></thead>
        <tbody>${reportData.prj
          .filter(p => (p.ed || 0) > 0)
          .map(
            (p) => `
          <tr><td>${p.n}</td><td>${p.edb || 0}</td><td>${p.edp || 0}</td><td>${p.ed || 0}</td></tr>`,
          )
          .join("")}
        </tbody>
      </table>`
    : '<p style="color:var(--text-muted)">No external dependencies</p>';

  // Internal dependencies by namespace (with search and pagination)
  const namespaceMetricsSection = g.ns.n.length > 0
    ? `<div class="filter-bar" style="margin-bottom: 15px;">
        <label>Search:</label>
        <input type="text" id="nsMetricsSearchBox" class="search-box" placeholder="Search namespaces..." value="${nsMetricsSearchQuery}">
      </div>
      <div id="namespaceMetricsTable"></div>`
    : '<p style="color:var(--text-muted)">No namespaces found</p>';

  el.innerHTML = `
    ${renderCardsGrid(couplingCards)}
    ${renderSection('Internal Dependencies by Project', internalProjectDepsTable)}
    ${renderSection('External Dependencies by Project', externalDepsTable)}
    ${renderSection('Internal Dependencies by Namespace', namespaceMetricsSection)}
  `;

  if (g.ns.n.length > 0) {
    namespaceMetricsData = namespaceMetrics;

    document.getElementById("nsMetricsSearchBox")?.addEventListener("input", (e) => {
      nsMetricsSearchQuery = (e.target as HTMLInputElement).value;
      nsMetricsCurrentPage = 1;
      updateNamespaceMetricsTable();
    });

    updateNamespaceMetricsTable();
  }

  enableSorting();
}
