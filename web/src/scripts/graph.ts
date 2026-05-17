import * as d3 from "d3";
import type { CompactReport, GraphLayer, GraphNode } from "./types";
import { getNodeColor, getNodeSizeValue } from "./graphMetrics";

interface RenderedGraphNode extends d3.SimulationNodeDatum {
  id: number;
  name: string;
  size: number;
  depCount: number;
  radius?: number;
  sizeValue?: number;
  cc?: number;
  loc?: number;
  mi?: number;
  tc?: number;
  mc?: number;
  internalDependencies?: number;
  internalDependents?: number;
  dependencyRatio?: number;
  externalDependencies?: number;
  externalBclDependencies?: number;
  externalPackageDependencies?: number;
  err?: number;
  warn?: number;
}

interface RenderedGraphLink extends d3.SimulationLinkDatum<RenderedGraphNode> {
  weight: number;
  targetX?: number;
  targetY?: number;
}

interface InitialRenderedGraphLink extends RenderedGraphLink {
  source: number;
  target: number;
}

function renderGraph(
  containerId: string,
  graphData: GraphLayer,
  colorMetric: string,
  graphType: string,
  sizeMetric: string,
  reportData: CompactReport,
): void {
  const container = document.getElementById(containerId);
  if (!container) return;

  const width = container.clientWidth || 800;
  const height = container.clientHeight || 600;

  if (!graphData.n || graphData.n.length === 0) {
    container.innerHTML =
      '<div style="display:flex;align-items:center;justify-content:center;height:100%;color:var(--text-muted)">No dependencies to display</div>';
    return;
  }

  const initialLinks: InitialRenderedGraphLink[] = graphData.e.map(([source, target, weight]) => ({
    source,
    target,
    weight,
  }));

  const outboundCounts: Record<number, number> = {};
  initialLinks.forEach((l) => {
    outboundCounts[l.source] =
      (outboundCounts[l.source] || 0) + l.weight;
  });

  const prelimNodes: RenderedGraphNode[] = graphData.n.map((nodeData) => {
    if (graphType === "project") {
      const id = Number(nodeData[0]);
      const name = String(nodeData[1]);
      const size = Number(nodeData[2] ?? 0);
      const projectMetrics = reportData.prj.find(p => p.n === name);
      return {
        id,
        name,
        size,
        depCount: outboundCounts[id] || 0,
        cc: projectMetrics?.cc ?? 0,
        loc: projectMetrics?.loc ?? size,
        mi: projectMetrics?.mi ?? 0,
        internalDependencies: projectMetrics?.id ?? 0,
        externalDependencies: projectMetrics?.ed ?? 0,
        externalBclDependencies: projectMetrics?.edb ?? 0,
        externalPackageDependencies: projectMetrics?.edp ?? 0,
        err: projectMetrics?.err ?? 0,
        warn: projectMetrics?.warn ?? 0,
      };
    } else {
      const id = Number(nodeData[0]);
      const name = String(nodeData[1]);
      const loc = Number(nodeData[2] ?? 0);
      const cc = Number(nodeData[3] ?? 0);
      const mi = Number(nodeData[4] ?? 0);
      const tc = Number(nodeData[5] ?? 0);
      const mc = Number(nodeData[6] ?? 0);
      const internalDeps = Number(nodeData[7] ?? 0);
      const internalDependents = Number(nodeData[8] ?? 0);
      const depRatio = Number(nodeData[9] ?? 0);
      const externalDeps = Number(nodeData[10] ?? 0);
      return {
        id,
        name,
        loc,
        cc,
        mi,
        tc,
        mc,
        internalDependencies: internalDeps,
        internalDependents,
        dependencyRatio: depRatio,
        externalDependencies: externalDeps,
        size: loc,
        depCount: outboundCounts[id] || 0,
      };
    }
  });

  const sizeValues = prelimNodes.map((n) =>
    getNodeSizeValue(n, sizeMetric, graphType, n.depCount),
  );
  const minValue = Math.min(...sizeValues, 0);
  const maxValue = Math.max(...sizeValues, 1);
  const minRadius = 15;
  const maxRadius = 200;

  const nodes: RenderedGraphNode[] = prelimNodes.map((n, i) => {
    const value = sizeValues[i];
    const ratio =
      maxValue === minValue ? 0.5 : (value - minValue) / (maxValue - minValue);
    return {
      ...n,
      radius: minRadius + ratio * (maxRadius - minRadius),
      sizeValue: value,
    };
  });

  const svg = d3
    .select(`#${containerId}`)
    .append("svg")
    .attr("width", width)
    .attr("height", height);

  const g = svg.append("g");
  const zoom = d3
    .zoom<SVGSVGElement, unknown>()
    .scaleExtent([0.1, 4])
    .on("zoom", (e) => g.attr("transform", e.transform));
  (svg as d3.Selection<SVGSVGElement, unknown, HTMLElement, unknown>).call(zoom);

  const avgRadius = nodes.reduce((sum, n) => sum + (n.radius ?? 0), 0) / nodes.length;
  const links: RenderedGraphLink[] = initialLinks.map((link) => ({ ...link }));

  const simulation = d3
    .forceSimulation<RenderedGraphNode>(nodes)
    .force(
      "link",
      d3
        .forceLink<RenderedGraphNode, RenderedGraphLink>(links)
        .id((d) => d.id)
        .distance((d) => {
          const sourceNode = resolveLinkNode(d.source, nodes);
          const targetNode = resolveLinkNode(d.target, nodes);
          const sourceR = sourceNode?.radius || avgRadius;
          const targetR = targetNode?.radius || avgRadius;
          return sourceR + targetR + 100;
        }),
    )
    .force(
      "charge",
      d3.forceManyBody().strength(-800).distanceMax(600),
    )
    .force("center", d3.forceCenter(width / 2, height / 2))
    .force(
      "collision",
      d3
        .forceCollide<RenderedGraphNode>()
        .radius((d) => (d.radius ?? 0) + 30)
        .strength(1),
    )
    .force("x", d3.forceX(width / 2).strength(0.03))
    .force("y", d3.forceY(height / 2).strength(0.03));

  svg
    .append("defs")
    .append("marker")
    .attr("id", `arrow-${containerId}`)
    .attr("viewBox", "0 -5 10 10")
    .attr("refX", 10)
    .attr("refY", 0)
    .attr("markerWidth", 10)
    .attr("markerHeight", 10)
    .attr("markerUnits", "userSpaceOnUse")
    .attr("orient", "auto")
    .append("path")
    .attr("fill", "var(--accent)")
    .attr("d", "M0,-5L10,0L0,5");

  const link = g
    .append("g")
    .selectAll<SVGLineElement, RenderedGraphLink>("line")
    .data(links)
    .join("line")
    .attr("class", "link")
    .attr("stroke", "var(--accent)")
    .attr("stroke-opacity", 0.6)
    .attr("stroke-width", (d) => Math.min(Math.max(d.weight / 2, 1), 4))
    .attr("marker-end", `url(#arrow-${containerId})`);

  const node = g
    .append("g")
    .selectAll<SVGGElement, RenderedGraphNode>("g")
    .data(nodes)
    .join("g")
    .attr("class", "node")
    .call(
      d3
        .drag<SVGGElement, RenderedGraphNode>()
        .on("start", (e, d) => {
          if (!e.active) simulation.alphaTarget(0.3).restart();
          d.fx = d.x;
          d.fy = d.y;
        })
        .on("drag", (e, d) => {
          d.fx = e.x;
          d.fy = e.y;
        })
        .on("end", (e, d) => {
          if (!e.active) simulation.alphaTarget(0);
          d.fx = null;
          d.fy = null;
        }),
    );

  node
    .append("circle")
    .attr("r", (d) => d.radius ?? 0)
    .attr("fill", (d) =>
      getNodeColor(d as GraphNode, colorMetric, graphType, reportData),
    );

  node
    .append("text")
    .attr("dy", 4)
    .attr("text-anchor", "middle")
    .attr("font-size", (d) => Math.max(9, Math.min(12, (d.radius ?? 0) / 3)))
    .text((d) => d.name);

  node
    .append("title")
    .text((d) => {
      if (graphType === "project") {
        const lines = [
          d.name,
          `Internal Dependencies: ${d.internalDependencies ?? 0}`,
          `External Dependencies: ${d.externalDependencies ?? 0}`,
          `  - BCL: ${d.externalBclDependencies ?? 0}`,
          `  - Packages: ${d.externalPackageDependencies ?? 0}`,
          `Outbound deps: ${d.depCount}`,
          `Size metric: ${d.sizeValue}`,
          `LOC: ${d.loc ?? d.size}`,
          `CC: ${d.cc ?? 0}`,
          `MI: ${d.mi ?? 0}`
        ];
        return lines.join('\n');
      } else {
        return `${d.name}\nInternal Dependencies: ${d.internalDependencies ?? 0}\nInternal Dependents: ${d.internalDependents ?? 0}\nExternal Dependencies: ${d.externalDependencies ?? 0}\nDependency Ratio: ${d.dependencyRatio?.toFixed(2) ?? "0.00"}\nLOC: ${d.loc ?? 0}\nCC: ${d.cc ?? 0}\nMI: ${d.mi ?? 0}`;
      }
    });

  simulation.on("tick", () => {
    link.each((d) => {
      const source = resolveLinkNode(d.source, nodes);
      const target = resolveLinkNode(d.target, nodes);
      if (!source || !target) return;

      const dx = (target.x ?? 0) - (source.x ?? 0);
      const dy = (target.y ?? 0) - (source.y ?? 0);
      const dist = Math.sqrt(dx * dx + dy * dy);
      if (dist === 0) return;
      const targetRadius = target.radius || 20;
      const ratio = (dist - targetRadius - 5) / dist;
      d.targetX = (source.x ?? 0) + dx * ratio;
      d.targetY = (source.y ?? 0) + dy * ratio;
    });
    link
      .attr("x1", (d) => resolveLinkNode(d.source, nodes)?.x ?? 0)
      .attr("y1", (d) => resolveLinkNode(d.source, nodes)?.y ?? 0)
      .attr("x2", (d) => d.targetX || (resolveLinkNode(d.target, nodes)?.x ?? 0))
      .attr("y2", (d) => d.targetY || (resolveLinkNode(d.target, nodes)?.y ?? 0));
    node.attr("transform", (d) => `translate(${d.x ?? 0},${d.y ?? 0})`);
  });
}

function resolveLinkNode(
  endpoint: number | string | RenderedGraphNode,
  nodes: ReadonlyArray<RenderedGraphNode>,
): RenderedGraphNode | undefined {
  if (typeof endpoint === "object") {
    return endpoint;
  }

  const id = Number(endpoint);
  return nodes.find((node) => node.id === id);
}

// ---------- Public API ----------

let graphRendered = false;

export function initGraphTab(reportData: CompactReport): void {
  const el = document.getElementById("graph");
  if (!el) return;
  const hasComplexityMetrics = reportData.hasCm !== false;

  el.innerHTML = `
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
        <option value="coupling">Internal Dependencies</option>
        <option value="external-deps">External Dependencies (Total)</option>
        <option value="bcl-deps" data-project-only="true">BCL Dependencies (System/Microsoft)</option>
        <option value="package-deps" data-project-only="true">Package Dependencies (Third-party)</option>
        ${hasComplexityMetrics ? '<option value="complexity">Cyclomatic Complexity</option>' : ''}
        ${hasComplexityMetrics ? '<option value="loc">Lines of Code</option>' : ''}
        ${hasComplexityMetrics ? '<option value="maintainability">Maintainability Index</option>' : ''}
      </select>
      <label>Size by:</label>
      <select id="sizeMetric">
        <option value="dependencies">Dependencies</option>
        <option value="diagnostics" data-project-only="true">Diagnostics Count</option>
        <option value="coupling">Internal Dependencies</option>
        <option value="external-deps">External Dependencies (Total)</option>
        <option value="bcl-deps" data-project-only="true">BCL Dependencies (System/Microsoft)</option>
        <option value="package-deps" data-project-only="true">Package Dependencies (Third-party)</option>
        ${hasComplexityMetrics ? '<option value="complexity">Cyclomatic Complexity</option>' : ''}
        ${hasComplexityMetrics ? '<option value="loc">Lines of Code</option>' : ''}
        ${hasComplexityMetrics ? '<option value="maintainability">Maintainability Index</option>' : ''}
      </select>
    </div>
    <div id="graphContainer" class="graph-container graph-fullpage"></div>
  `;

  const render = () => renderCurrentGraph(reportData);
  document
    .getElementById("graphSelector")
    ?.addEventListener("change", () => {
      updateMetricOptions();
      render();
    });
  document
    .getElementById("colorMetric")
    ?.addEventListener("change", render);
  document
    .getElementById("sizeMetric")
    ?.addEventListener("change", render);
  updateMetricOptions();
}

function updateMetricOptions(): void {
  const graphType = (
    document.getElementById("graphSelector") as HTMLSelectElement | null
  )?.value;
  const isNamespace = graphType === "namespace";

  const colorSelect = document.getElementById(
    "colorMetric",
  ) as HTMLSelectElement | null;
  const sizeSelect = document.getElementById(
    "sizeMetric",
  ) as HTMLSelectElement | null;

  [colorSelect, sizeSelect].forEach((select) => {
    if (!select) return;
    const options = select.querySelectorAll("option");
    options.forEach((option) => {
      if (option.dataset.projectOnly === "true") {
        option.style.display = isNamespace ? "none" : "";
        if (isNamespace && option.selected) {
          select.value = "none";
        }
      }
    });
  });
}

function renderCurrentGraph(reportData: CompactReport): void {
  const container = document.getElementById("graphContainer");
  const selector = document.getElementById(
    "graphSelector",
  ) as HTMLSelectElement | null;
  const colorMetric = (
    document.getElementById("colorMetric") as HTMLSelectElement | null
  )?.value ?? "none";
  const sizeMetric = (
    document.getElementById("sizeMetric") as HTMLSelectElement | null
  )?.value ?? "dependencies";
  const graphType = selector?.value ?? "project";
  const graphData =
    graphType === "project" ? reportData.g.p : reportData.g.ns;

  if (container) container.innerHTML = "";
  renderGraph(
    "graphContainer",
    graphData,
    colorMetric,
    graphType,
    sizeMetric,
    reportData,
  );
}

export function tryRenderGraph(
  tabId: string,
  reportData: CompactReport,
): void {
  if (tabId === "graph" && !graphRendered) {
    graphRendered = true;
    setTimeout(() => renderCurrentGraph(reportData), 50);
  }
}
