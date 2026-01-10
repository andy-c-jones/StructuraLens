# Configuration discovery and inheritance (StructuraLens)

This document explains how StructuraLens discovers and merges configuration files placed at the solution (root) level and at individual project folders.

Filename and discovery

- Default filename: `structuralens.json` unless `--config <path>` is provided on the CLI (which disables discovery and uses the explicit file only).
- When not provided explicitly, the CLI discovers configs by walking upward from the project folder to the repository/solution root and collecting any `structuralens.json` files found along the path.

Merge semantics (parent -> child)

- Parent-first application: configurations are applied starting at the highest ancestor (closest to repository/solution root) and then merged down toward the project folder; child entries override or extend parent entries.
- `InheritanceDepth` (integer, default 0): limits how many parent folder levels to include; `0` means no inheritance (only the local file is used), `1` includes immediate parent, and so on. If a config file sets `InheritanceDepth`, that value applies only to that file's own merging behavior when used as a child — CLI also accepts a global `--inherit-depth` override.

Merging rules by property type

- Scalar properties (IsEnabled, CheckAssemblyDependencies, MaxIssueCount, AutoLowerMaxIssueCount, InheritanceDepth): child overrides parent when present; otherwise the parent's value is used.
- Arrays (ExcludedFiles, Rules): arrays are concatenated in parent-to-child order. For `Rules`, if a child rule has the same `Id` as a parent rule, the child rule replaces the parent rule (de-dup by `Id`).
- ProjectFilters: `Include` and `Exclude` arrays are merged (union) in parent-to-child order; conflicts are resolved by removing duplicates and applying child entries last.

Evaluation precedence

- During evaluation, any dependency matching a `Disallowed` rule is considered disallowed even if an `Allowed` rule also matches. When multiple `Allowed` rules match, the most specific `From` pattern wins (heuristic: longest literal prefix then fewest wildcards); similarly for `To` when tie-breaking.

Example

- `/` (repo root)
  - `structuralens.json` (contains base Allowed rules for `System.*`)
- `/src/ServiceA/` (project folder)
  - `structuralens.json` (contains Disallowed rule preventing references to `*.Ui`)

Merging result for ServiceA: root rules applied first, then ServiceA rules appended; the Disallowed rule from ServiceA will block any matching dependency even if an Allowed rule exists in the root.

Implementation notes for developers

- The CLI should implement discovery by starting at the project directory and walking parents up to the filesystem root or until a configured solution root is found (e.g., via an explicit `--root` flag or by locating a `.sln` file).
- Loading order must be deterministic: sort ancestor paths from root-to-child, then load files in that order.
- Provide a `--print-config` CLI flag that shows the merged configuration used for a run (useful for debugging inheritance and overrides).

Compatibility and migration

- Existing NsDepCop XML configs can be migrated by transforming rules into the JSON schema; consider providing a small converter in `tools/` to assist teams migrating rules.

