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

  const couplingCards: CardProps[] = [
    { value: g.p.n.length, label: 'Projects' },
    { value: g.p.e.length, label: 'Project Dependencies' },
    { value: g.ns.n.length, label: 'Namespaces' },
    { value: g.ns.e.length, label: 'Namespace Dependencies' }
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
    : '<p style="color:var(--text-muted)">No project dependencies</p>';

  const namespaceDepsSection = namespaceEdges.length > 0
    ? `<div class="filter-bar" style="margin-bottom: 15px;">
        <label>Search:</label>
        <input type="text" id="nsSearchBox" class="search-box" placeholder="Search in any column..." value="${nsSearchQuery}">
      </div>
      <div id="namespaceDepsTable"></div>`
    : '<p style="color:var(--text-muted)">No namespace dependencies</p>';

  el.innerHTML = `
    ${renderCardsGrid(couplingCards)}
    ${renderSection('Project Dependencies', projectDepsTable)}
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
