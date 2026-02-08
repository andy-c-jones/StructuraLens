import type { CompactReport } from "./types";
import {
  enableSorting,
  renderPagination,
  attachPaginationListeners,
} from "./tables";
import { renderCardsGrid, renderSection, type CardProps } from "./componentRenderers";

interface CouplingEdge {
  from: string;
  to: string;
  weight: number;
}

let nsCurrentPage = 1;
let nsSearchQuery = "";
const nsPageSize = 100;
let namespaceEdgesData: CouplingEdge[] = [];

function updateNamespaceDepsTable(): void {
  const searchQuery = nsSearchQuery.toLowerCase();
  let filtered = namespaceEdgesData;

  if (searchQuery) {
    filtered = filtered.filter(
      (e) =>
        e.from.toLowerCase().includes(searchQuery) ||
        e.to.toLowerCase().includes(searchQuery) ||
        e.weight.toString().includes(searchQuery),
    );
  }

  const totalPages = Math.ceil(filtered.length / nsPageSize);
  const startIdx = (nsCurrentPage - 1) * nsPageSize;
  const endIdx = startIdx + nsPageSize;
  const pageData = filtered.slice(startIdx, endIdx);

  const tableEl = document.getElementById("namespaceDepsTable");
  if (!tableEl) return;

  tableEl.innerHTML = `
    <p style="color:var(--text-muted);margin-bottom:10px">
      ${filtered.length} dependenc${filtered.length !== 1 ? "ies" : "y"}
      ${filtered.length > nsPageSize ? ` (showing ${startIdx + 1}-${Math.min(endIdx, filtered.length)})` : ""}
    </p>
    <table>
      <thead><tr><th>From</th><th>To</th><th>References</th></tr></thead>
      <tbody>${pageData
        .map(
          (e) => `
        <tr><td>${e.from}</td><td>${e.to}</td><td>${e.weight}</td></tr>`,
        )
        .join("")}
      </tbody>
    </table>
    ${renderPagination(nsCurrentPage, totalPages)}
  `;

  attachPaginationListeners("namespaceDepsTable", (page) => {
    nsCurrentPage = page;
    updateNamespaceDepsTable();
  });

  enableSorting();
}

export function renderCoupling(reportData: CompactReport): void {
  const el = document.getElementById("coupling");
  if (!el) return;

  const g = reportData.g;

  const projectEdges: CouplingEdge[] = g.p.e.map(
    ([src, tgt, weight]) => {
      const srcNode = g.p.n.find((n) => n[0] === src);
      const tgtNode = g.p.n.find((n) => n[0] === tgt);
      return {
        from: srcNode ? String(srcNode[1]) : String(src),
        to: tgtNode ? String(tgtNode[1]) : String(tgt),
        weight,
      };
    },
  );

  const namespaceEdges: CouplingEdge[] = g.ns.e.map(
    ([src, tgt, weight]) => {
      const srcNode = g.ns.n.find((n) => n[0] === src);
      const tgtNode = g.ns.n.find((n) => n[0] === tgt);
      return {
        from: srcNode ? String(srcNode[1]) : String(src),
        to: tgtNode ? String(tgtNode[1]) : String(tgt),
        weight,
      };
    },
  );

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

  const projectDepsTable = projectEdges.length > 0
    ? `<table>
        <thead><tr><th>From</th><th>To</th><th>References</th></tr></thead>
        <tbody>${projectEdges
          .map(
            (e) => `
          <tr><td>${e.from}</td><td>${e.to}</td><td>${e.weight}</td></tr>`,
          )
          .join("")}
        </tbody>
      </table>`
    : '<p style="color:var(--text-muted)">No internal project dependencies</p>';

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

  const namespaceDepsSection = namespaceEdges.length > 0
    ? `<div class="filter-bar" style="margin-bottom: 15px;">
        <label>Search:</label>
        <input type="text" id="nsSearchBox" class="search-box" placeholder="Search in any column..." value="${nsSearchQuery}">
      </div>
      <div id="namespaceDepsTable"></div>`
    : '<p style="color:var(--text-muted)">No namespace dependencies</p>';

  el.innerHTML = `
    ${renderCardsGrid(couplingCards)}
    ${renderSection('Internal Project Dependencies', projectDepsTable)}
    ${renderSection('External Dependencies by Project', externalDepsTable)}
    ${renderSection('Namespace Dependencies', namespaceDepsSection)}
  `;

  if (namespaceEdges.length > 0) {
    namespaceEdgesData = namespaceEdges;

    document.getElementById("nsSearchBox")?.addEventListener("input", (e) => {
      nsSearchQuery = (e.target as HTMLInputElement).value;
      nsCurrentPage = 1;
      updateNamespaceDepsTable();
    });

    updateNamespaceDepsTable();
  }

  enableSorting();
}
