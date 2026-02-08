import type { DiagnosticItem } from "./types";
import {
  enableSorting,
  renderPagination,
  attachPaginationListeners,
} from "./tables";

let diagCurrentPage = 1;
let diagSearchQuery = "";
const diagPageSize = 100;
let storedDiagnostics: DiagnosticItem[] = [];

export function renderDiagnostics(diagnosticsData: DiagnosticItem[]): void {
  storedDiagnostics = diagnosticsData;
  diagCurrentPage = 1;
  diagSearchQuery = "";

  const el = document.getElementById("diagnostics");
  if (!el) return;

  if (!diagnosticsData || diagnosticsData.length === 0) {
    el.innerHTML =
      '<p class="passed">&#10003; No compiler diagnostics</p>';
    return;
  }

  const projects = [...new Set(diagnosticsData.map((d) => d.project))];

  el.innerHTML = `
    <div class="filter-bar">
      <label>Search:</label>
      <input type="text" id="diagSearchBox" class="search-box" placeholder="Search in any column..." value="${diagSearchQuery}">
      <label style="margin-left: 15px;">Filter by project:</label>
      <select id="diagProjectFilter">
        <option value="">All Projects</option>
        ${projects.map((p) => `<option value="${p}">${p}</option>`).join("")}
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

  document
    .getElementById("diagSearchBox")
    ?.addEventListener("input", (e) => {
      diagSearchQuery = (e.target as HTMLInputElement).value;
      diagCurrentPage = 1;
      updateDiagnosticsTable();
    });
  document
    .getElementById("diagProjectFilter")
    ?.addEventListener("change", () => {
      diagCurrentPage = 1;
      updateDiagnosticsTable();
    });
  document
    .getElementById("diagSeverityFilter")
    ?.addEventListener("change", () => {
      diagCurrentPage = 1;
      updateDiagnosticsTable();
    });

  updateDiagnosticsTable();
}

function updateDiagnosticsTable(): void {
  const projFilter = (
    document.getElementById("diagProjectFilter") as HTMLSelectElement | null
  )?.value;
  const sevFilter = (
    document.getElementById("diagSeverityFilter") as HTMLSelectElement | null
  )?.value;
  const searchQuery = diagSearchQuery.toLowerCase();

  let filtered = storedDiagnostics;
  if (projFilter) filtered = filtered.filter((d) => d.project === projFilter);
  if (sevFilter) filtered = filtered.filter((d) => d.severity === sevFilter);
  if (searchQuery) {
    filtered = filtered.filter(
      (d) =>
        d.project.toLowerCase().includes(searchQuery) ||
        d.id.toLowerCase().includes(searchQuery) ||
        d.severity.toLowerCase().includes(searchQuery) ||
        d.message.toLowerCase().includes(searchQuery) ||
        d.file.toLowerCase().includes(searchQuery) ||
        d.line.toString().includes(searchQuery),
    );
  }

  const totalPages = Math.ceil(filtered.length / diagPageSize);
  const startIdx = (diagCurrentPage - 1) * diagPageSize;
  const endIdx = startIdx + diagPageSize;
  const pageData = filtered.slice(startIdx, endIdx);

  const tableEl = document.getElementById("diagnosticsTable");
  if (!tableEl) return;

  tableEl.innerHTML = `
    <p style="color:var(--text-muted);margin-bottom:10px">
      ${filtered.length} diagnostic${filtered.length !== 1 ? "s" : ""}
      ${filtered.length > diagPageSize ? ` (showing ${startIdx + 1}-${Math.min(endIdx, filtered.length)})` : ""}
    </p>
    <table>
      <thead><tr><th>Project</th><th>ID</th><th>Severity</th><th>Message</th><th>File</th><th>Line</th></tr></thead>
      <tbody>${pageData
        .map(
          (d) => `
        <tr>
          <td>${d.project}</td>
          <td><code>${d.id}</code></td>
          <td><span class="badge badge-${d.severity}">${d.severity}</span></td>
          <td>${d.message}</td>
          <td>${d.file}</td>
          <td>${d.line}</td>
        </tr>`,
        )
        .join("")}
      </tbody>
    </table>
    ${renderPagination(diagCurrentPage, totalPages)}
  `;

  attachPaginationListeners("diagnosticsTable", (page) => {
    diagCurrentPage = page;
    updateDiagnosticsTable();
  });

  enableSorting();
}
