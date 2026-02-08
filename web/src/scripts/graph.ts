import * as d3 from "d3";
import type { CompactReport, GraphLayer, GraphNode } from "./types";

/**
 * Get color on green-yellow-red scale with muted saturation.
 * ratio 0 = green, 0.5 = yellow, 1 = red
 */
function getHeatColor(ratio: number): string {
  ratio = Math.max(0, Math.min(1, ratio));

  const isDark =
    window.matchMedia("(prefers-color-scheme: dark)").matches ||
    !window.matchMedia("(prefers-color-scheme: light)").matches;

  const colors = isDark
    ? {
        green: [45, 90, 61],
        yellow: [107, 91, 45],
        red: [90, 45, 45],
      }
    : {
        green: [168, 213, 186],
        yellow: [240, 230, 184],
        red: [232, 184, 184],
      };

  let r: number, g: number, b: number;
  if (ratio < 0.5) {
    const t = ratio * 2;
    r = Math.round(
      colors.green[0] + t * (colors.yellow[0] - colors.green[0]),
    );
    g = Math.round(
      colors.green[1] + t * (colors.yellow[1] - colors.green[1]),
    );
    b = Math.round(
      colors.green[2] + t * (colors.yellow[2] - colors.green[2]),
    );
  } else {
    const t = (ratio - 0.5) * 2;
    r = Math.round(
      colors.yellow[0] + t * (colors.red[0] - colors.yellow[0]),
    );
    g = Math.round(
      colors.yellow[1] + t * (colors.red[1] - colors.yellow[1]),
    );
    b = Math.round(
      colors.yellow[2] + t * (colors.red[2] - colors.yellow[2]),
    );
  }

  return `rgb(${r}, ${g}, ${b})`;
}

function getProjectMetrics(reportData: CompactReport, name: string) {
  return reportData.prj.find((p) => p.n === name);
}

function getNodeColor(
  node: GraphNode,
  colorMetric: string,
  graphType: string,
  reportData: CompactReport,
): string {
  if (colorMetric === "none") return "var(--node-default)";

  let metrics: Record<string, number> | null = null;
  let allMetrics: Record<string, number>[] = [];

  if (graphType === "project") {
    const pm = getProjectMetrics(reportData, node.name);
    if (!pm) return "var(--node-default)";
    metrics = {
      cc: pm.cc,
      loc: pm.loc,
      mi: pm.mi,
      ce: pm.ce,
      ca: pm.ca,
      err: pm.err ?? 0,
      warn: pm.warn ?? 0,
    };
    allMetrics = reportData.prj.map((p) => ({
      cc: p.cc,
      loc: p.loc,
      mi: p.mi,
      ce: p.ce,
      ca: p.ca,
      err: p.err ?? 0,
      warn: p.warn ?? 0,
    }));
  } else {
    const nsNode = node as {
      cc?: number;
      loc?: number;
      mi?: number;
      ce?: number;
      ca?: number;
    };
    metrics = {
      cc: nsNode.cc ?? 0,
      loc: nsNode.loc ?? 0,
      mi: nsNode.mi ?? 0,
      ce: nsNode.ce ?? 0,
      ca: nsNode.ca ?? 0,
    };
    allMetrics = reportData.g.ns.n.map(
      ([, , loc, cc, mi, , , ce, ca]: (number | string)[]) => ({
        cc: cc as number,
        loc: loc as number,
        mi: mi as number,
        ce: ce as number,
        ca: ca as number,
      }),
    );
  }

  if (!metrics) return "var(--node-default)";

  let value: number;
  let values: number[];

  switch (colorMetric) {
    case "diagnostics":
      if (graphType === "namespace") return "var(--node-default)";
      value = (metrics.err || 0) * 5 + (metrics.warn || 0);
      values = allMetrics.map((m) => (m.err || 0) * 5 + (m.warn || 0));
      break;
    case "coupling":
      value = metrics.ce || 0;
      values = allMetrics.map((m) => m.ce || 0);
      break;
    case "complexity":
      value = metrics.cc || 0;
      values = allMetrics.map((m) => m.cc || 0);
      break;
    case "loc":
      value = metrics.loc || 0;
      values = allMetrics.map((m) => m.loc || 0);
      break;
    case "maintainability":
      value = 100 - (metrics.mi || 100);
      values = allMetrics.map((m) => 100 - (m.mi || 100));
      break;
    default:
      return "var(--node-default)";
  }

  const min = Math.min(...values);
  const max = Math.max(...values);
  if (max === min) return getHeatColor(0);
  const ratio = (value - min) / (max - min);
  return getHeatColor(ratio);
}

function getNodeSizeValue(
  node: GraphNode,
  sizeMetric: string,
  graphType: string,
  outboundCount: number,
): number {
  if (sizeMetric === "dependencies") return outboundCount;

  let metrics: Record<string, number> | null = null;

  if (graphType === "project") {
    // The node object for project type only has id, name, size, depCount
    // We need to find the project metrics from the name
    metrics = {
      cc: (node as { cc?: number }).cc ?? 0,
      loc: (node as { loc?: number }).loc ?? node.size ?? 0,
      mi: (node as { mi?: number }).mi ?? 0,
      ce: (node as { ce?: number }).ce ?? 0,
      err: (node as { err?: number }).err ?? 0,
      warn: (node as { warn?: number }).warn ?? 0,
    };
  } else {
    const nsNode = node as {
      cc?: number;
      loc?: number;
      mi?: number;
      ce?: number;
      ca?: number;
    };
    metrics = {
      cc: nsNode.cc ?? 0,
      loc: nsNode.loc ?? 0,
      mi: nsNode.mi ?? 0,
      ce: nsNode.ce ?? 0,
      ca: nsNode.ca ?? 0,
    };
  }

  if (!metrics) return outboundCount;

  switch (sizeMetric) {
    case "diagnostics":
      if (graphType === "namespace") return outboundCount;
      return (metrics.err || 0) * 5 + (metrics.warn || 0);
    case "coupling":
      return metrics.ce || 0;
    case "complexity":
      return metrics.cc || 0;
    case "loc":
      return metrics.loc || 0;
    case "maintainability":
      return 100 - (metrics.mi || 100);
    default:
      return outboundCount;
  }
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

  const links = graphData.e.map(([source, target, weight]) => ({
    source,
    target,
    weight,
  }));

  const outboundCounts: Record<number, number> = {};
  links.forEach((l) => {
    outboundCounts[l.source] =
      (outboundCounts[l.source] || 0) + l.weight;
  });

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const prelimNodes: any[] = graphData.n.map((nodeData) => {
    if (graphType === "project") {
      const [id, name, size] = nodeData;
      return {
        id,
        name,
        size,
        depCount: outboundCounts[id as number] || 0,
      };
    } else {
      const [id, name, loc, cc, mi, tc, mc, ce, ca, instability] =
        nodeData;
      return {
        id,
        name,
        loc,
        cc,
        mi,
        tc,
        mc,
        ce,
        ca,
        instability,
        size: loc,
        depCount: outboundCounts[id as number] || 0,
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

  const nodes = prelimNodes.map((n, i) => {
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

  const avgRadius =
    nodes.reduce((sum: number, n: { radius: number }) => sum + n.radius, 0) /
    nodes.length;

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const simulation = d3
    .forceSimulation(nodes)
    .force(
      "link",
      d3
        .forceLink(links)
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        .id((d: any) => d.id)
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        .distance((d: any) => {
          const sourceNode = nodes.find(
            (n: { id: number }) => n.id === (d.source.id ?? d.source),
          );
          const targetNode = nodes.find(
            (n: { id: number }) => n.id === (d.target.id ?? d.target),
          );
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
        .forceCollide()
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        .radius((d: any) => d.radius + 30)
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
    .selectAll("line")
    .data(links)
    .join("line")
    .attr("class", "link")
    .attr("stroke", "var(--accent)")
    .attr("stroke-opacity", 0.6)
    .attr("stroke-width", (d) => Math.min(Math.max(d.weight / 2, 1), 4))
    .attr("marker-end", `url(#arrow-${containerId})`);

  const node = g
    .append("g")
    .selectAll("g")
    .data(nodes)
    .join("g")
    .attr("class", "node")
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    .call(
      d3
        .drag<SVGGElement, any>()
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
        }) as any,
    );

  node
    .append("circle")
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    .attr("r", (d: any) => d.radius)
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    .attr("fill", (d: any) =>
      getNodeColor(d, colorMetric, graphType, reportData),
    );

  node
    .append("text")
    .attr("dy", 4)
    .attr("text-anchor", "middle")
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    .attr("font-size", (d: any) => Math.max(9, Math.min(12, d.radius / 3)))
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    .text((d: any) => d.name);

  node
    .append("title")
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    .text(
      (d: any) =>
        `${d.name}\nOutbound deps: ${d.depCount}\nSize metric: ${d.sizeValue}\nLOC: ${d.size}`,
    );

  simulation.on("tick", () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    link.each(function (d: any) {
      const dx = d.target.x - d.source.x;
      const dy = d.target.y - d.source.y;
      const dist = Math.sqrt(dx * dx + dy * dy);
      if (dist === 0) return;
      const targetRadius = d.target.radius || 20;
      const ratio = (dist - targetRadius - 5) / dist;
      d.targetX = d.source.x + dx * ratio;
      d.targetY = d.source.y + dy * ratio;
    });
    link
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      .attr("x1", (d: any) => d.source.x)
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      .attr("y1", (d: any) => d.source.y)
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      .attr("x2", (d: any) => d.targetX || d.target.x)
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      .attr("y2", (d: any) => d.targetY || d.target.y);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    node.attr("transform", (d: any) => `translate(${d.x},${d.y})`);
  });
}

// ---------- Public API ----------

let graphRendered = false;

export function initGraphTab(reportData: CompactReport): void {
  const el = document.getElementById("graph");
  if (!el) return;

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
        <option value="coupling">Efferent Coupling (Ce)</option>
        <option value="complexity">Cyclomatic Complexity</option>
        <option value="loc">Lines of Code</option>
        <option value="maintainability">Maintainability Index</option>
      </select>
      <label>Size by:</label>
      <select id="sizeMetric">
        <option value="dependencies">Dependencies</option>
        <option value="diagnostics" data-project-only="true">Diagnostics Count</option>
        <option value="coupling">Efferent Coupling (Ce)</option>
        <option value="complexity">Cyclomatic Complexity</option>
        <option value="loc">Lines of Code</option>
        <option value="maintainability">Maintainability Index</option>
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
