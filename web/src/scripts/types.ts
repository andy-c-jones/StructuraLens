/** Matches the C# CompactReport JSON structure. */
export interface CompactReport {
	v: number;
	p: string;
	t: number;
	prj: CompactProject[];
	g: CompactGraph;
	diag?: CompactDiagnostics;
	gitSha?: string;
	gitBranch?: string;
	gitRemote?: string;
	gitDirty?: boolean;
	/** Linting summary (optional). */
	l?: LintSummary;
}

export interface LintSummary {
	ok: boolean;
	r: number;
	e: number;
	w: number;
}

export interface CompactProject {
	n: string;
	tc: number;
	mc: number;
	cc: number;
	loc: number;
	dit: number;
	mi: number;
	id: number;
	idx: number;
	dr: number;
	ed: number;
	edb: number;
	edp: number;
	err?: number;
	warn?: number;
	types?: CompactType[];
	ns?: CompactNamespace[];
}

export interface CompactNamespace {
	n: string;
	tc: number;
	mc: number;
	cc: number;
	loc: number;
	dit: number;
	mi: number;
	types?: CompactType[];
}

export interface CompactType {
	n: string;
	fn?: string;
	dit: number;
	cc: number;
	loc: number;
	mi: number;
	m?: CompactMethod[];
}

export interface CompactMethod {
	n: string;
	cc: number;
	loc: number;
	hv: number;
	mi: number;
	sl: number;
	el: number;
}

export interface CompactGraph {
	p: GraphLayer;
	ns: GraphLayer;
}

export interface GraphLayer {
	n: (number | string)[][];
	e: number[][];
}

export interface CompactDiagnostics {
	e: number;
	w: number;
	i: number;
	d: (string | number)[][];
}

/** Diagnostics item used by the report (flat format from BuildDiagnosticsJson). */
export interface DiagnosticItem {
	project: string;
	id: string;
	message: string;
	severity: string;
	file: string;
	line: number;
	category: string;
}

/** Diff report structure matching C# AnalysisDiffReport. */
export interface DiffReport {
	base: DiffMetadata;
	head: DiffMetadata;
	totals: DiffTotals;
	projects: ProjectDiff[];
	diagnostics: DiagnosticDiffSummary;
}

export interface DiffMetadata {
	solutionPath: string;
	analyzedAt: string;
	commitSha?: string;
	branchName?: string;
}

export interface DiffTotals {
	baseProjects: number;
	headProjects: number;
	projectsDelta: number;
	baseTypes: number;
	headTypes: number;
	typesDelta: number;
	baseMethods: number;
	headMethods: number;
	methodsDelta: number;
	baseCyclomaticComplexity: number;
	headCyclomaticComplexity: number;
	cyclomaticComplexityDelta: number;
	baseLinesOfCode: number;
	headLinesOfCode: number;
	linesOfCodeDelta: number;
	baseAvgMaintainabilityIndex: number;
	headAvgMaintainabilityIndex: number;
	avgMaintainabilityDelta: number;
	baseErrors: number;
	headErrors: number;
	errorsDelta: number;
	baseWarnings: number;
	headWarnings: number;
	warningsDelta: number;
}

export interface ProjectDiff {
	name: string;
	isAdded: boolean;
	isRemoved: boolean;
	head: ProjectDiffMetrics;
	base: ProjectDiffMetrics;
	maintainabilityDelta: number;
	cyclomaticComplexityDelta: number;
	linesOfCodeDelta: number;
	warningsDelta: number;
	[key: string]: unknown;
}

export interface ProjectDiffMetrics {
	typeCount: number;
	methodCount: number;
	cyclomaticComplexity: number;
	linesOfCode: number;
	maxDepthOfInheritance: number;
	avgMaintainabilityIndex: number;
	efferentCoupling: number;
	afferentCoupling: number;
	instability: number;
	errors: number;
	warnings: number;
}

export interface DiagnosticDiffSummary {
	newErrors: number;
	resolvedErrors: number;
	newWarnings: number;
	resolvedWarnings: number;
	[key: string]: unknown;
}

/**
 * Parsed namespace node from the graph layer.
 * Namespace nodes: [id, name, loc, cc, mi, tc, mc, id, idx, dr, ed]
 */
export interface NamespaceNode {
	id: number;
	name: string;
	loc: number;
	cc: number;
	mi: number;
	tc: number;
	mc: number;
	internalDependencies: number;
	internalDependents: number;
	dependencyRatio: number;
	externalDependencies: number;
	size: number;
	depCount: number;
	radius?: number;
	sizeValue?: number;
	x?: number;
	y?: number;
	fx?: number | null;
	fy?: number | null;
}

/** Parsed project node from the graph layer. */
export interface ProjectNode {
	id: number;
	name: string;
	size: number;
	depCount: number;
	radius?: number;
	sizeValue?: number;
	x?: number;
	y?: number;
	fx?: number | null;
	fy?: number | null;
}

export type GraphNode = ProjectNode | NamespaceNode;
