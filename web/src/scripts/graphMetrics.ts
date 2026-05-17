import type { CompactReport, GraphNode } from "./types";

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

export function getNodeColor(
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
      id: pm.id,
      idx: pm.idx,
      ed: pm.ed,
      edb: pm.edb,
      edp: pm.edp,
      err: pm.err ?? 0,
      warn: pm.warn ?? 0,
    };
    allMetrics = reportData.prj.map((p) => ({
      cc: p.cc,
      loc: p.loc,
      mi: p.mi,
      id: p.id,
      idx: p.idx,
      ed: p.ed,
      edb: p.edb,
      edp: p.edp,
      err: p.err ?? 0,
      warn: p.warn ?? 0,
    }));
  } else {
    const nsNode = node as {
      cc?: number;
      loc?: number;
      mi?: number;
      internalDependencies?: number;
      internalDependents?: number;
      externalDependencies?: number;
    };
    metrics = {
      cc: nsNode.cc ?? 0,
      loc: nsNode.loc ?? 0,
      mi: nsNode.mi ?? 0,
      id: nsNode.internalDependencies ?? 0,
      idx: nsNode.internalDependents ?? 0,
      ed: nsNode.externalDependencies ?? 0,
    };
    allMetrics = reportData.g.ns.n.map(
      ([, , loc, cc, mi, , , id, idx, , ed]: (number | string)[]) => ({
        cc: cc as number,
        loc: loc as number,
        mi: mi as number,
        id: id as number,
        idx: idx as number,
        ed: ed as number,
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
      value = metrics.id || 0;
      values = allMetrics.map((m) => m.id || 0);
      break;
    case "external-deps":
      value = metrics.ed || 0;
      values = allMetrics.map((m) => m.ed || 0);
      break;
    case "bcl-deps":
      if (graphType === "namespace") return "var(--node-default)";
      value = (metrics as { edb?: number }).edb || 0;
      values = allMetrics.map((m) => (m as { edb?: number }).edb || 0);
      break;
    case "package-deps":
      if (graphType === "namespace") return "var(--node-default)";
      value = (metrics as { edp?: number }).edp || 0;
      values = allMetrics.map((m) => (m as { edp?: number }).edp || 0);
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

export function getNodeSizeValue(
  node: GraphNode,
  sizeMetric: string,
  graphType: string,
  outboundCount: number,
): number {
  if (sizeMetric === "dependencies") return outboundCount;

  let metrics: Record<string, number> | null = null;

  if (graphType === "project") {
    metrics = {
      cc: (node as { cc?: number }).cc ?? 0,
      loc: (node as { loc?: number }).loc ?? node.size ?? 0,
      mi: (node as { mi?: number }).mi ?? 0,
      id: (node as { internalDependencies?: number }).internalDependencies ?? 0,
      ed: (node as { externalDependencies?: number }).externalDependencies ?? 0,
      edb: (node as { externalBclDependencies?: number }).externalBclDependencies ?? 0,
      edp: (node as { externalPackageDependencies?: number }).externalPackageDependencies ?? 0,
      err: (node as { err?: number }).err ?? 0,
      warn: (node as { warn?: number }).warn ?? 0,
    };
  } else {
    const nsNode = node as {
      cc?: number;
      loc?: number;
      mi?: number;
      internalDependencies?: number;
      internalDependents?: number;
      externalDependencies?: number;
    };
    metrics = {
      cc: nsNode.cc ?? 0,
      loc: nsNode.loc ?? 0,
      mi: nsNode.mi ?? 0,
      id: nsNode.internalDependencies ?? 0,
      idx: nsNode.internalDependents ?? 0,
      ed: nsNode.externalDependencies ?? 0,
    };
  }

  if (!metrics) return outboundCount;

  switch (sizeMetric) {
    case "diagnostics":
      if (graphType === "namespace") return outboundCount;
      return (metrics.err || 0) * 5 + (metrics.warn || 0);
    case "coupling":
      return metrics.id || 0;
    case "external-deps":
      return metrics.ed || 0;
    case "bcl-deps":
      if (graphType === "namespace") return outboundCount;
      return (metrics as { edb?: number }).edb || 0;
    case "package-deps":
      if (graphType === "namespace") return outboundCount;
      return (metrics as { edp?: number }).edp || 0;
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
