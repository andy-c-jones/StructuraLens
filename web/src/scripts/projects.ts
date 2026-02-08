import type {
  CompactReport,
  CompactProject,
  CompactNamespace,
  CompactType,
  CompactMethod,
} from "./types";
import { enableSorting } from "./tables";

// ---------- Tree rendering helpers ----------

function renderMethodNode(method: CompactMethod): string {
  return `
    <li class="tree-node tree-level-method">
      <div class="tree-item" data-level="method">
        <span class="tree-toggle"></span>
        <div class="tree-label">
          <span class="tree-icon">&#9889;</span>
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

function renderTypeNode(type: CompactType, parentId: string): string {
  const hasMethods = type.m && type.m.length > 0;
  const fullName = type.fn || type.n;
  const nodeId = `t-${parentId}-${fullName.replace(/[^a-zA-Z0-9]/g, "_")}`;

  return `
    <li class="tree-node tree-level-type">
      <div class="tree-item" data-level="type">
        <span class="tree-toggle ${hasMethods ? "expandable collapsed" : ""}" data-node-id="${nodeId}"></span>
        <div class="tree-label">
          <span class="tree-icon">&#128311;</span>
          <span class="tree-label-text">${type.n}</span>
          <div class="tree-metrics">
            <span class="tree-metric"><span class="tree-metric-label">Methods:</span> <span class="tree-metric-value">${hasMethods ? type.m!.length : 0}</span></span>
            <span class="tree-metric"><span class="tree-metric-label">Cyclomatic Complexity:</span> <span class="tree-metric-value">${type.cc}</span></span>
            <span class="tree-metric"><span class="tree-metric-label">Lines of Code:</span> <span class="tree-metric-value">${type.loc.toLocaleString()}</span></span>
            <span class="tree-metric"><span class="tree-metric-label">Maintainability Index:</span> <span class="tree-metric-value">${type.mi}</span></span>
            <span class="tree-metric"><span class="tree-metric-label">Depth of Inheritance:</span> <span class="tree-metric-value">${type.dit}</span></span>
          </div>
        </div>
      </div>
      ${
        hasMethods
          ? `<ul class="tree-children" data-parent="${nodeId}">
        ${type.m!.map((m) => renderMethodNode(m)).join("")}
      </ul>`
          : ""
      }
    </li>`;
}

function renderNamespaceNode(
  namespace: CompactNamespace,
  projectName: string,
): string {
  const hasTypes = namespace.types && namespace.types.length > 0;
  const nodeId = `ns-${projectName}-${namespace.n}`;

  return `
    <li class="tree-node tree-level-namespace">
      <div class="tree-item" data-level="namespace">
        <span class="tree-toggle ${hasTypes ? "expandable collapsed" : ""}" data-node-id="${nodeId}"></span>
        <div class="tree-label">
          <span class="tree-icon">&#128193;</span>
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
      ${
        hasTypes
          ? `<ul class="tree-children" data-parent="${nodeId}">
        ${namespace.types!.map((t) => renderTypeNode(t, nodeId)).join("")}
      </ul>`
          : ""
      }
    </li>`;
}

function renderProjectNode(project: CompactProject): string {
  const hasNamespaces = project.ns && project.ns.length > 0;
  const hasTypes = project.types && project.types.length > 0;
  const hasChildren = hasNamespaces || hasTypes;

  return `
    <li class="tree-node tree-level-project">
      <div class="tree-item" data-level="project">
        <span class="tree-toggle ${hasChildren ? "expandable collapsed" : ""}" data-node-id="p-${project.n}"></span>
        <div class="tree-label">
          <span class="tree-icon">&#128230;</span>
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
      ${
        hasChildren
          ? `<ul class="tree-children" data-parent="p-${project.n}">
        ${hasNamespaces ? project.ns!.map((ns) => renderNamespaceNode(ns, project.n)).join("") : ""}
        ${hasTypes ? project.types!.map((t) => renderTypeNode(t, project.n)).join("") : ""}
      </ul>`
          : ""
      }
    </li>`;
}

function renderTree(projects: CompactProject[]): string {
  return `<ul class="tree">${projects.map((p) => renderProjectNode(p)).join("")}</ul>`;
}

// ---------- Tree interaction ----------

function attachTreeHandlers(): void {
  document
    .querySelectorAll<HTMLElement>(".tree-toggle.expandable")
    .forEach((toggle) => {
      toggle.addEventListener("click", (e) => {
        e.stopPropagation();
        const nodeId = toggle.getAttribute("data-node-id");
        const children = document.querySelector(
          `[data-parent="${nodeId}"]`,
        );

        if (children) {
          const isExpanded = toggle.classList.contains("expanded");
          if (isExpanded) {
            toggle.classList.remove("expanded");
            toggle.classList.add("collapsed");
            children.classList.remove("expanded");
          } else {
            toggle.classList.remove("collapsed");
            toggle.classList.add("expanded");
            children.classList.add("expanded");
          }
        }
      });
    });
}

function expandCollapseAll(expand: boolean): void {
  const toggles =
    document.querySelectorAll<HTMLElement>(".tree-toggle.expandable");
  toggles.forEach((toggle) => {
    const nodeId = toggle.getAttribute("data-node-id");
    const children = document.querySelector(`[data-parent="${nodeId}"]`);

    if (children) {
      if (expand) {
        toggle.classList.remove("collapsed");
        toggle.classList.add("expanded");
        children.classList.add("expanded");
      } else {
        toggle.classList.remove("expanded");
        toggle.classList.add("collapsed");
        children.classList.remove("expanded");
      }
    }
  });
}

// ---------- View update helpers ----------

function getFilteredProjects(reportData: CompactReport): CompactProject[] {
  const filter = (
    document.getElementById("projectFilter") as HTMLSelectElement | null
  )?.value;
  return filter
    ? reportData.prj.filter((p) => p.n === filter)
    : reportData.prj;
}

function updateProjectsTree(reportData: CompactReport): void {
  const projects = getFilteredProjects(reportData);
  const treeEl = document.getElementById("projectsTree");
  if (treeEl) {
    treeEl.innerHTML = renderTree(projects);
    attachTreeHandlers();
  }
}

function updateProjectsTable(reportData: CompactReport): void {
  const projects = getFilteredProjects(reportData);
  const tableEl = document.getElementById("projectsTable");
  if (!tableEl) return;

  tableEl.innerHTML = `
    <table>
      <thead><tr><th>Project</th><th>Types</th><th>Methods</th><th>Cyclomatic Complexity</th><th>Lines of Code</th><th>Max Depth of Inheritance</th><th>Maintainability Index</th><th>Efferent (Ce)</th><th>Afferent (Ca)</th><th>Instability</th></tr></thead>
      <tbody>${projects
        .map(
          (p) => `
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
        </tr>`,
        )
        .join("")}
      </tbody>
    </table>
  `;
  enableSorting();
}

function updateProjectsView(reportData: CompactReport): void {
  const viewMode = (
    document.getElementById("viewMode") as HTMLSelectElement | null
  )?.value;
  const treeContainer = document.getElementById("projectsTree");
  const tableContainer = document.getElementById("projectsTable");
  const expandBtn = document.getElementById("expandAll");
  const collapseBtn = document.getElementById("collapseAll");

  if (viewMode === "tree") {
    if (treeContainer) treeContainer.style.display = "block";
    if (tableContainer) tableContainer.style.display = "none";
    if (expandBtn) expandBtn.style.display = "inline-block";
    if (collapseBtn) collapseBtn.style.display = "inline-block";
    updateProjectsTree(reportData);
  } else {
    if (treeContainer) treeContainer.style.display = "none";
    if (tableContainer) tableContainer.style.display = "block";
    if (expandBtn) expandBtn.style.display = "none";
    if (collapseBtn) collapseBtn.style.display = "none";
    updateProjectsTable(reportData);
  }
}

// ---------- Public API ----------

export function renderProjects(reportData: CompactReport): void {
  const el = document.getElementById("projects");
  if (!el) return;

  const d = reportData;

  el.innerHTML = `
    <div class="filter-bar">
      <label>Filter by project:</label>
      <select id="projectFilter">
        <option value="">All Projects</option>
        ${d.prj.map((p) => `<option value="${p.n}">${p.n}</option>`).join("")}
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

  const update = () => updateProjectsView(reportData);
  document.getElementById("projectFilter")?.addEventListener("change", update);
  document.getElementById("viewMode")?.addEventListener("change", update);
  document
    .getElementById("expandAll")
    ?.addEventListener("click", () => expandCollapseAll(true));
  document
    .getElementById("collapseAll")
    ?.addEventListener("click", () => expandCollapseAll(false));

  updateProjectsView(reportData);
}
