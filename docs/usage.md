# StructuraLens Usage Guide

StructuraLens is a CLI tool for analyzing C# codebases to produce code complexity metrics, coupling analysis, and architecture insights.

## Installation

### Using Docker

```bash
docker pull ghcr.io/your-org/structuralens:latest
docker run -v $(pwd):/repo structuralens analyze /repo/YourSolution.sln
```

### Building from Source

```bash
git clone https://github.com/your-org/structuralens.git
cd structuralens
dotnet build -c Release
dotnet run --project src/StructuraLens.Cli -- analyze YourSolution.sln
```

## CLI Reference

### Basic Usage

```bash
structuralens analyze <path> [options]
```

Where `<path>` is the path to a solution (`.sln`, `.slnx`) or project (`.csproj`) file.

### Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--out` | `-o` | Output file path for the JSON report | stdout |
| `--format` | `-f` | Output format: `json` or `summary` | `json` |
| `--coupling-mode` | `-c` | Coupling analysis mode: `internal`, `filtered`, `all` | `filtered` |
| `--config` | | Path to `structuralens.json` configuration file | auto-discover |

### Examples

#### Analyze a solution with summary output
```bash
structuralens analyze MyProject.sln --format summary
```

#### Analyze with internal-only coupling
```bash
structuralens analyze MyProject.sln --coupling-mode internal --format summary
```

#### Save JSON report to file
```bash
structuralens analyze MyProject.sln --out report.json
```

#### Use custom configuration
```bash
structuralens analyze MyProject.sln --config ./my-config.json
```

## Coupling Modes

StructuraLens supports three coupling analysis modes to help you focus on the dependencies that matter:

### Internal Mode (`--coupling-mode internal`)

Only tracks dependencies between your own code. External libraries (NuGet packages, framework libraries) are excluded.

**Use when:** You want to focus purely on your internal architecture without noise from external dependencies.

```bash
structuralens analyze MyProject.sln --coupling-mode internal --format summary
```

Example output:
```
=== Coupling Summary ===
Mode: Internal
Total Dependencies: 45
Average Efferent Coupling: 2.1
```

### Filtered Mode (`--coupling-mode filtered`) - Default

Tracks dependencies on external libraries but excludes common framework namespaces like `System.*` and `Microsoft.*`. This is the default mode.

**Use when:** You want to see dependencies on third-party NuGet packages (like Newtonsoft.Json, Serilog) while filtering out framework noise.

Default exclude patterns:
- `System.*`
- `Microsoft.*`

```bash
structuralens analyze MyProject.sln --format summary
# or explicitly:
structuralens analyze MyProject.sln --coupling-mode filtered --format summary
```

### All Mode (`--coupling-mode all`)

Tracks all dependencies including framework libraries. This gives the complete picture but can be noisy.

**Use when:** You need comprehensive dependency analysis or want to audit all external dependencies.

```bash
structuralens analyze MyProject.sln --coupling-mode all --format summary
```

## Output Formats

### JSON Format (Default)

The JSON format provides complete structured data suitable for tooling integration:

```bash
structuralens analyze MyProject.sln --out report.json
```

```json
{
  "solutionPath": "/path/to/MyProject.sln",
  "analyzedAt": "2026-01-10T15:00:00Z",
  "projects": [
    {
      "name": "MyProject.Core",
      "types": [...],
      "totalCyclomaticComplexity": 150,
      "totalLinesOfExecutableCode": 500
    }
  ],
  "couplingAnalysis": {
    "summary": {
      "couplingMode": "Filtered",
      "totalDependencies": 200,
      "averageInstability": 0.45
    }
  }
}
```

### Compact Format (.slr)

The compact format is optimized for size and machine parsing. It uses short property names and includes graph data for visualization with d3.js or similar libraries. Use the `.slr` (StructuraLens Report) extension.

```bash
structuralens analyze MyProject.sln --format compact --out report.slr
```

**Size comparison:** ~99% smaller than full JSON (1KB vs 1.4MB for a typical project)

See [Compact Format Specification](compact-format.md) for complete documentation.

```json
{
  "v": 1,
  "p": "/path/to/MyProject.sln",
  "t": 1768063200000,
  "prj": [
    {"n":"MyProject.Core","tc":20,"mc":100,"cc":150,"loc":500,"dit":3,"mi":72.5,"ce":5,"ca":15,"i":0.25}
  ],
  "g": {
    "p": {
      "n": [[0,"MyProject.Core",500],[1,"MyProject.Api",300]],
      "e": [[1,0,1]]
    },
    "ns": {
      "n": [[0,"MyProject.Core.Services",200]],
      "e": []
    }
  },
  "l": {"r":5,"e":0,"w":0,"ok":true}
}
```

#### Compact Format Schema

| Field | Description |
|-------|-------------|
| `v` | Format version |
| `p` | Solution/project path |
| `t` | Timestamp (Unix milliseconds) |
| `prj` | Array of project metrics |
| `g` | Graph data for visualization |
| `g.p` | Project dependency graph (nodes + edges) |
| `g.ns` | Namespace dependency graph (internal only) |
| `l` | Linting results |

**Project metrics (`prj[]`):**
- `n`: Name, `tc`: Type count, `mc`: Method count
- `cc`: Cyclomatic complexity, `loc`: Lines of code
- `dit`: Max depth of inheritance, `mi`: Avg maintainability index
- `ce`: Efferent coupling, `ca`: Afferent coupling, `i`: Instability

**Graph nodes (`g.p.n`, `g.ns.n`):** `[id, name, size]`

**Graph edges (`g.p.e`, `g.ns.e`):** `[sourceId, targetId, weight]`

### Summary Format

Human-readable summary for quick analysis:

```bash
structuralens analyze MyProject.sln --format summary
```

```
StructuraLens v0.1.0
Analyzing: MyProject.sln
Coupling mode: Filtered

=== Analysis Summary ===
Solution: /path/to/MyProject.sln
Analyzed at: 2026-01-10T15:00:00Z

Projects: 3
Types: 50
Methods: 200
Total Cyclomatic Complexity: 450
Total Lines of Executable Code: 1500

=== Coupling Summary ===
Mode: Filtered
Total Dependencies: 200
Average Efferent Coupling: 3.2
Average Afferent Coupling: 2.1
Average Instability: 0.45
Most Coupled Entity: MyProject.Services
Most Unstable Entity: MyProject.Api

Project: MyProject.Core
  Types: 20
  Total CC: 150
  Total LOC: 500
  Max DIT: 3
  Avg Maintainability Index: 72.5
  Efferent Coupling (Ce): 5
  Afferent Coupling (Ca): 15
  Instability (I): 0.25
```

## Metrics Explained

### Code Complexity Metrics

| Metric | Description | Formula |
|--------|-------------|---------|
| **Cyclomatic Complexity (CC)** | Number of independent paths through code | `decision_points + 1` |
| **Lines of Executable Code (LOC)** | Count of executable statements | Statement count |
| **Halstead Volume (V)** | Program size based on operators/operands | `N * log2(n)` |
| **Depth of Inheritance (DIT)** | Levels in inheritance hierarchy | Base type chain length |
| **Maintainability Index (MI)** | Overall maintainability score (0-100) | See below |

#### Maintainability Index Formula

```
MI = max(0, 100 * (171 - 5.2*ln(V) - 0.23*CC - 16.2*ln(LOC)) / 171)
```

| Score | Interpretation |
|-------|----------------|
| 0-9 | Unmaintainable |
| 10-19 | Difficult to maintain |
| 20-39 | Moderate maintainability |
| 40-100 | Good maintainability |

### Coupling Metrics

| Metric | Description |
|--------|-------------|
| **Efferent Coupling (Ce)** | Number of dependencies going OUT (this depends on others) |
| **Afferent Coupling (Ca)** | Number of dependencies coming IN (others depend on this) |
| **Instability (I)** | `Ce / (Ca + Ce)` — ranges from 0 (stable) to 1 (unstable) |

#### Instability Interpretation

| Score | Meaning |
|-------|---------|
| 0.0 | Completely stable - many depend on it, hard to change |
| 0.5 | Balanced |
| 1.0 | Completely unstable - depends on others, easy to change |

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success (no linting errors) |
| 1 | Error (file not found, analysis failure, or linting errors) |

**Note:** Architecture linting violations with `error` severity cause exit code 1. Warnings and info do not affect the exit code.

## Architecture Linting

StructuraLens can enforce dependency rules via configuration. See [Configuration](configuration.md#architecture-linting-rules) for details.

When linting rules are configured, the summary output includes:

```
=== Architecture Linting ===
Rules Evaluated: 5
Errors: 0
Warnings: 2
Info: 0
Status: PASSED

Violations:
  [WARNING] NO-LEGACY: Avoid legacy library
         MyApp.Services → LegacyLib.Core
```

### Example: Enforce Layer Architecture

```json
{
  "rules": [
    {
      "id": "UI-NO-DB",
      "description": "UI layer must not access database directly",
      "severity": "error",
      "from": "*.UI*",
      "disallow": ["*.Data*", "EntityFramework*"]
    }
  ]
}
```

## Tips for Best Results

1. **Build before analyzing**: Run `dotnet build` before analysis to ensure all binaries are present for accurate coupling analysis.

2. **Use filtered mode for practical insights**: The default `filtered` mode excludes framework noise while showing third-party dependencies.

3. **Focus on high-complexity methods**: Methods with CC > 10 or MI < 40 are candidates for refactoring.

4. **Watch for unstable core components**: Core/shared libraries should have low instability (I < 0.5).

5. **Use configuration files for teams**: Create a `structuralens.json` at the solution root for consistent analysis across the team.
