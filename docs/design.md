Design Document: StructuraLens

Executive summary

Build a containerized .NET 10 CLI tool that analyzes C# codebases using Roslyn to produce per-method/type/project complexity metrics (Cyclomatic, Halstead Volume, Lines of Executable Code, Depth of Inheritance), inter-project coupling and architecture linting (NsDepCop-like rules in JSON), and a consolidated Maintainability Index and Roslyn diagnostics report. Output machine-readable JSON (primary) and optional human-friendly HTML/Markdown reports; extensible via additional Roslyn analyzers and metric plugins. First target: Linux (linux-x64); Windows support planned later.

Goals and scope

- Metrics: Cyclomatic Complexity (M = E − N + P), Halstead Volume (V = N * log2(n)), Depth of Inheritance (DIT per class), Lines of Executable Code (LOC for statements), Coupling (inter-project/assembly coupling), Maintainability Index (using Halstead, CC, LOC per Codacy-style equation).
- Architecture linting: configurable JSON allow/deny rules for namespace/assembly/project dependencies (NsDepCop semantics) with project-level and solution-level config inheritance.
- Roslyn diagnostics: collect compiler/IDE/CA diagnostics per project with severity and locations.

High-level architecture

- CLI entrypoint (single binary) that:
  1. Parses CLI args and config (config.json / structuralens.json) and discovers/merges solution/project configs.
  2. Creates a Roslyn Workspace (MSBuildWorkspace or fallback) and loads solution/project set.
  3. Runs analysis pipeline: dependency graph builder, Roslyn analyzer runner, syntax/semantic metric collectors, aggregators, architecture linter.
  4. Serializes outputs (JSON primary) and optionally HTML/Markdown.

Components and responsibilities

- CLI module: argument parsing, logging, output selection, concurrency throttle, config discovery and merge (parent-first with project overrides), --print-config.
- Workspace loader: use Microsoft.Build.Locator + MSBuildWorkspace; provide graceful fallback to project-file parsing if MSBuild unavailable.
- Dependency analyzer: build graph of projects/assemblies/namespaces and compute coupling edges from symbol references and project/assembly references.
- Roslyn diagnostics runner: CompilationWithAnalyzers, load user-provided analyzer assemblies, capture Diagnostics.
- Metric engine: ControlFlowGraph or visitor for Cyclomatic, Syntax/semantic walker for Halstead, statement counting for LOC, BaseType traversal for DIT.
- Linter engine: JSON-rule evaluator supporting exact, wildcard, and regex patterns; Disallowed wins over Allowed; child rules override by Id.
- Output: JSON schema for metrics, diagnostics, architecture issues; HTML via templating.

Configuration discovery & inheritance

- Default filename: structuralens.json (override with --config).
- Discovery: walk from project folder to repo root or .sln file; load ancestor configs root-to-child; apply InheritanceDepth.
- Merge rules: scalars overridden by child, arrays concatenated parent-to-child with Rules de-duplicated by Id (child replaces), ProjectFilters unioned.

Runtime & Containerization

- Use full .NET 10 runtime in the container to ensure compatibility with existing Roslyn analyzers and allow loading analyzer assemblies at runtime.
- Multi-stage Dockerfile using mcr.microsoft.com/dotnet/sdk:10 build stage and produce a self-contained publish output (non-AOT) for linux-x64; final image contains the published app and required runtime files.
- Require or strongly recommend that users build their solution before running StructuraLens so DLLs and PDBs are present for accurate coupling and analysis; if built outputs are missing, the tool will perform source-only analysis but will emit a warning noting some metrics may be incomplete.
- Provide guidance for analyzer/plugin inclusion: allow passing analyzer assemblies via CLI (`--analyzers <dir>`) and load them into CompilationWithAnalyzers at runtime.

CLI UX

- Commands: analyze [path] --config file --out report.json --format json|html --parallel N; lint-arch; --print-config.
- Exit codes: 0 success, nonzero for failures; structured machine-readable output on stdout with exit status.

Extensibility & plugin model

- Analyzer extension: --analyzers <dir> to pass analyzer assemblies into CompilationWithAnalyzers.
- Metric plugins: define IMetricProvider interface; load plugin assemblies dynamically at runtime.

Performance & scaling

- Parallelize per-project and per-file analysis with bounded thread pool; reuse SemanticModel/Compilation caches; stream results to disk to limit memory.
- Provide sampling and incremental analysis options for large monorepos.

Testing and validation

- Unit tests for metric computations, integration tests on sample multi-project solutions, regression tests for config merging and linter rules.

CI/CD and release (GitHub Actions)

- Use GitHub Actions as the canonical CI/CD system for the project. Provide workflows to:
  - Build and test the project across selected runtimes and configurations (net10 build matrix where applicable).
  - Publish self-contained artifacts for linux-x64 using dotnet publish with --self-contained.
  - Build and push Docker images (multi-stage) to container registries (GitHub Container Registry or Docker Hub) with tags for commit SHA and semantic versions.
  - Run integration tests by executing the container image against sample repositories in the CI runner.
  - Run the tool against its own codebase as a self-analysis step.
  - Upload build artifacts (binaries, debug symbols) as workflow artifacts and optionally release attachments.
- CI considerations: cache NuGet packages and MSBuild outputs for speed, and secure registry credentials via GitHub secrets.
- Provide a sample .github/workflows/ci.yml workflow that performs restore, build, test, publish, container build, self-analysis, and artifact upload; include optional scheduled benchmarking job.

Limitations & risk

- Full assembly dependency analysis and some coupling metrics require built outputs (DLLs/PDBs); for source-only scenarios, dependency resolution is best-effort via semantic analysis and a warning is emitted.
- Users should build their solution before running StructuraLens for complete analysis.

Roadmap

1. Finalize JSON schema and config inheritance semantics (done).  
2. Prototype: CLI that loads a solution and computes CC, LOC, DIT.  
3. Add Halstead, coupling analysis, and Roslyn diagnostics capture.  
4. JSON architecture linter.  
5. Dockerfile and CI pipeline; verify end-to-end in CI.  
6. Plugin docs and Windows support plan.

Appendix: key formulas

- Cyclomatic Complexity: M = E − N + P (practical: CC = decision_points + 1).  
- Halstead Volume: V = N * log2(n) where N = N1 + N2 (total operands+operators) and n = n1 + n2 (distinct operands + distinct operators).  
- Maintainability Index: MI = max(0, 100 * (171 − 5.2 * ln(V) − 0.23 * CC − 16.2 * ln(LOC)) / 171).
