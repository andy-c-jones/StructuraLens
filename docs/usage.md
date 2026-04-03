# StructuraLens Usage Guide

StructuraLens is a .NET 10 CLI tool for analyzing C# codebases to produce code complexity metrics, coupling analysis, and comprehensive diagnostics reports.

## Installation

### Using Pre-built Binaries

Download the latest release for your platform from the [Releases](https://github.com/your-org/structuralens/releases) page.

**Supported Platforms:**
- Windows (win-x64)
- Linux (linux-x64)
- macOS (osx-x64, osx-arm64)

Extract the archive and add the directory to your PATH, or run the executable directly.

### Building from Source

```bash
git clone https://github.com/your-org/structuralens.git
cd structuralens
dotnet build -c Release
```

The compiled CLI will be in `src/StructuraLens.Cli/bin/Release/net10.0/`.

### System Requirements

- .NET 10 Runtime (or .NET 10 SDK for building from source)
- Windows, Linux, or macOS

## CLI Reference

### analyze Command

Analyzes a C# solution or project file and generates metrics reports.

**Syntax:**
```bash
structuralens analyze <path> [options]
```

**Arguments:**
- `<path>` (required) - Path to a solution file (`.sln`, `.slnx`) or project file (`.csproj`)

**Options:**

| Option | Short | Type | Default | Description |
|--------|-------|------|---------|-------------|
| `--out` | `-o` | string | stdout | Output file path for the report |
| `--format` | `-f` | string | `json` | Output format: `json`, `compact`, `html`, or `summary` |
| `--verbose` | `-v` | flag | false | Enable verbose logging (sets log level to Debug) |
| `--analysis-mode` | | string | `Full` | Analysis mode: `Full` or `DiagnosticsAndReferences` |
| `--aggregation-strategy` | | string | `Adaptive` | Strategy for aggregating dependencies: `InMemory`, `SQLite`, or `Adaptive` |
| `--memory-threshold` | | long | 1024 | Memory threshold in MB for adaptive strategy to migrate from InMemory to SQLite |
| `--sqlite-batch-size` | | int | 1000 | Batch size for SQLite collector operations |

### Examples

#### Basic Analysis with JSON Output

```bash
structuralens analyze MyProject.sln --out report.json
```

Outputs a complete JSON report to `report.json`.

#### Generate Interactive HTML Report

```bash
structuralens analyze MyProject.sln --format html --out report.html
```

Creates a single-file HTML report with interactive dependency graphs and filterable tables. Open `report.html` in any modern browser.

#### Quick Console Summary

```bash
structuralens analyze MyProject.sln --format summary
```

Displays a human-readable summary to the console with key metrics, high-complexity methods, and low-maintainability items.

#### Compact Format for Large Solutions

```bash
structuralens analyze LargeSolution.sln --format compact --out report.slr
```

Generates a highly optimized compact format (~99% smaller than JSON) suitable for visualization tools. Recommended file extension: `.slr` (StructuraLens Report).

#### Analyze with Verbose Logging

```bash
structuralens analyze MyProject.sln --verbose --format summary
```

Enables detailed debug logging showing analysis progress, MSBuild operations, and NuGet restore details.

#### Force SQLite Strategy for Large Codebases

```bash
structuralens analyze MegaSolution.sln --aggregation-strategy SQLite --verbose
```

Uses disk-backed SQLite storage from the start, ideal for solutions with 100+ projects to minimize memory usage.

#### Adjust Memory Threshold for Adaptive Strategy

```bash
structuralens analyze MyProject.sln --memory-threshold 2048 --verbose
```

Increases the memory threshold to 2GB before migrating from InMemory to SQLite (default is 1GB).

#### Analyze a Single Project

```bash
structuralens analyze src/MyLibrary/MyLibrary.csproj --format html --out mylibrary-report.html
```

You can analyze individual projects instead of entire solutions.

#### Lightweight Diagnostics + References Mode

```bash
structuralens analyze MyProject.sln --analysis-mode DiagnosticsAndReferences --format json --out report.json
```

Runs a lightweight profile that skips complexity/maintainability calculations and code-level coupling traversal, while keeping diagnostics plus declared project/NuGet reference change analysis.

## Output Formats

### JSON Format (Default)

The JSON format provides complete structured data suitable for tooling integration, automation, and custom analysis.

**When to use:** Integrating with CI/CD pipelines, custom tooling, or when you need programmatic access to all metrics.

**Example:**
```bash
structuralens analyze MyProject.sln --out report.json
```

**Structure:**
```json
{
  "solutionPath": "/path/to/MyProject.sln",
  "analyzedAt": "2026-01-14T10:00:00Z",
  "projects": [
    {
      "name": "MyProject.Core",
      "path": "/path/to/MyProject.Core.csproj",
      "typeCount": 20,
      "methodCount": 150,
      "totalCyclomaticComplexity": 200,
      "totalLinesOfExecutableCode": 1500,
      "maxDepthOfInheritance": 3,
      "avgMaintainabilityIndex": 72.5,
      "efferentCoupling": 5,
      "afferentCoupling": 15,
      "instability": 0.25,
      "types": [...],
      "methods": [...]
    }
  ],
  "couplingAnalysis": {
    "projectDependencies": [...],
    "namespaceDependencies": [...],
    "typeDependencies": [...]
  },
  "diagnostics": [...]
}
```

**Key fields:**
- `projects[]` - Metrics for each project
- `types[]` - Per-type metrics (CC, LOC, DIT, MI)
- `methods[]` - Per-method metrics
- `couplingAnalysis` - Dependency graphs at project, namespace, and type levels
- `diagnostics[]` - Compiler warnings and errors with locations

### HTML Format

Interactive single-file HTML report with tabs, dependency graphs, and filtering capabilities. No server required - just open in a browser.

**When to use:** Sharing reports with stakeholders, code reviews, or visual exploration of dependencies.

**Example:**
```bash
structuralens analyze MyProject.sln --format html --out report.html
```

**Features:**
- **Summary Tab**: Overview cards with total projects, types, methods, complexity, and maintainability
- **Projects Tab**: Sortable, filterable table of project metrics
- **Coupling Tab**: Interactive D3.js force-directed graphs showing:
  - Project dependencies (how projects reference each other)
  - Namespace dependencies (internal namespace coupling)
- **Diagnostics Tab**: Compiler errors and warnings with severity filters
- **Responsive Design**: Works on desktop and mobile browsers

**Technology:**
- All CSS and JavaScript embedded inline (no external files needed)
- D3.js loaded from CDN for dependency graphs
- Works offline after initial load

### Compact Format (.slr)

Optimized for size and machine parsing with short property names. Includes graph data for D3.js visualization. Recommended file extension: `.slr` (StructuraLens Report).

**When to use:** Large solutions where JSON size is prohibitive, or integrating with visualization tools.

**Example:**
```bash
structuralens analyze MyProject.sln --format compact --out report.slr
```

**Size comparison:** ~99% smaller than full JSON (e.g., 15KB vs 1.4MB for a typical solution)

**Structure:**
```json
{
  "v": 1,
  "p": "/path/to/MyProject.sln",
  "t": 1768063200000,
  "prj": [
    {"n":"MyProject.Core","tc":20,"mc":150,"cc":200,"loc":1500,"dit":3,"mi":72.5,"ce":5,"ca":15,"i":0.25}
  ],
  "g": {
    "p": {
      "n": [[0,"MyProject.Core",1500],[1,"MyProject.Api",800]],
      "e": [[1,0,25]]
    },
    "ns": {
      "n": [[0,"MyProject.Core.Services",500]],
      "e": []
    }
  }
}
```

**Key compact properties:**
- `v` - Format version
- `p` - Solution path
- `t` - Timestamp (Unix milliseconds)
- `prj[]` - Project metrics (n=name, tc=type count, mc=method count, cc=cyclomatic complexity, loc=lines of code, dit=depth of inheritance, mi=maintainability index, ce=efferent coupling, ca=afferent coupling, i=instability)
- `g.p` - Project dependency graph (n=nodes as [id, name, size], e=edges as [sourceId, targetId, weight])
- `g.ns` - Namespace dependency graph (internal only)

See [Compact Format Specification](compact-format.md) for complete details.

### Summary Format

Human-readable console output with key metrics, top complexity items, and maintainability warnings.

**When to use:** Quick analysis during development, CI/CD pipeline logs, or sanity checks.

**Example:**
```bash
structuralens analyze MyProject.sln --format summary
```

**Output:**
```
StructuraLens v0.1.0
Analyzing: MyProject.sln

=== Analysis Summary ===
Solution: /path/to/MyProject.sln
Analyzed at: 2026-01-14T10:00:00Z

Projects: 3
Types: 50
Methods: 200
Total Cyclomatic Complexity: 450
Total Lines of Executable Code: 1500

=== Coupling Summary ===
Total Project Dependencies: 5
Total Namespace Dependencies: 45
Total Type Dependencies: 200
Average Instability: 0.45

=== Project Metrics ===

Project: MyProject.Core
  Types: 20
  Methods: 150
  Total CC: 200
  Total LOC: 1500
  Max Depth of Inheritance: 3
  Avg Maintainability Index: 72.5
  Efferent Coupling (Ce): 5
  Afferent Coupling (Ca): 15
  Instability (I): 0.25 (Stable)

=== High Complexity Methods (CC > 10) ===

MyProject.Core.Services.OrderService.ProcessOrder (CC: 15, LOC: 80, MI: 45)
MyProject.Api.Controllers.ProductController.GetProducts (CC: 12, LOC: 60, MI: 52)

=== Low Maintainability Methods (MI < 40) ===

MyProject.Core.Services.LegacyProcessor.Transform (CC: 20, LOC: 150, MI: 25)
```

**Highlights:**
- Total counts (projects, types, methods)
- Aggregate complexity and LOC
- Per-project metrics with coupling
- Methods with CC > 10 (candidates for refactoring)
- Methods with MI < 40 (difficult to maintain)

## Aggregation Strategies

StructuraLens offers three strategies for handling dependency data during analysis. The strategy affects memory usage and performance for large codebases.

### InMemory Strategy

Uses concurrent dictionaries in memory for fast dependency tracking.

**Best for:** Small to medium solutions (up to ~50 projects)

**Characteristics:**
- Fastest performance
- Moderate memory usage (50-70% reduction from naive approach via deduplication)
- All data kept in RAM

**Usage:**
```bash
structuralens analyze MyProject.sln --aggregation-strategy InMemory
```

### SQLite Strategy

Uses disk-backed SQLite database for dependency tracking.

**Best for:** Large solutions (100+ projects) or memory-constrained environments

**Characteristics:**
- Minimal memory usage (95% reduction compared to InMemory)
- Slightly slower (10-20% overhead)
- Handles unlimited data
- Creates temporary SQLite database file (deleted after analysis)

**Usage:**
```bash
structuralens analyze LargeSolution.sln --aggregation-strategy SQLite
```

### Adaptive Strategy (Default)

Starts with InMemory, automatically migrates to SQLite when memory threshold is exceeded.

**Best for:** Unknown codebase sizes or general-purpose analysis

**Characteristics:**
- Combines benefits of both strategies
- Automatic selection based on memory usage
- Configurable threshold (default: 1024 MB)
- Transparent migration (analysis continues uninterrupted)

**Usage:**
```bash
# Use default threshold (1024 MB)
structuralens analyze MyProject.sln

# Increase threshold to 2048 MB
structuralens analyze MyProject.sln --memory-threshold 2048
```

**Verbose output shows strategy selection:**
```
[INFO] Using Adaptive aggregation strategy (threshold: 1024 MB)
[INFO] Starting with InMemory collector
...
[INFO] Memory threshold exceeded, migrating to SQLite
[INFO] Migration complete, continuing analysis with SQLite
```

## Understanding the Metrics

### Code Complexity Metrics

#### Cyclomatic Complexity (CC)

Measures the number of independent paths through code. Calculated as `decision_points + 1`.

**Decision points:**
- `if`, `else if`, `switch` cases
- Loops (`for`, `foreach`, `while`, `do-while`)
- Logical operators (`&&`, `||`)
- Ternary operators (`? :`)
- `catch` blocks

**Interpretation:**
- CC = 1-10: Simple, easy to test
- CC = 11-20: Moderate complexity, consider refactoring
- CC > 20: High complexity, refactoring recommended

**Example:**
```csharp
public void Method(bool a, bool b)  // CC = 1 (baseline)
{
    if (a)                          // +1
    {
        if (b)                      // +1
        {
            // do something
        }
    }
    else                            // No increment (part of if)
    {
        // do something else
    }
}
// Total CC = 3
```

#### Lines of Executable Code (LOC)

Count of executable statements (not including braces, comments, or whitespace).

**Interpretation:**
- LOC < 50: Small method, easy to understand
- LOC = 50-100: Medium method
- LOC > 100: Large method, consider splitting

#### Halstead Volume (V)

Measures program size based on operators and operands. Formula: `V = N * log2(n)` where:
- N = Total operators + operands
- n = Distinct operators + distinct operands

**Interpretation:**
- Higher volume = more complex program
- Used in Maintainability Index calculation

#### Depth of Inheritance (DIT)

The length of the inheritance chain from a class to the root of the hierarchy.

**Interpretation:**
- DIT = 0: No inheritance (or inherits from Object)
- DIT = 1-3: Reasonable depth
- DIT > 5: Deep hierarchy, may be fragile

**Example:**
```csharp
class Animal { }                    // DIT = 0
class Mammal : Animal { }           // DIT = 1
class Dog : Mammal { }              // DIT = 2
class Labrador : Dog { }            // DIT = 3
```

#### Maintainability Index (MI)

Composite metric combining CC, LOC, and Halstead Volume. Score ranges from 0 (worst) to 100 (best).

**Formula:**
```
MI = max(0, 100 * (171 - 5.2*ln(V) - 0.23*CC - 16.2*ln(LOC)) / 171)
```

**Interpretation:**
- MI = 0-9: Unmaintainable (red flag)
- MI = 10-19: Difficult to maintain
- MI = 20-39: Moderate maintainability
- MI = 40-100: Good maintainability

**Note:** Lower CC, LOC, and Halstead Volume lead to higher MI.

### Coupling Metrics

StructuraLens analyzes dependencies between projects, namespaces, and types, providing metrics that separate internal (within your solution) from external (BCL and third-party packages) dependencies.

#### Internal Dependencies (ID)

Number of distinct internal entities this entity depends on (outbound dependencies within your solution).

**Interpretation:**
- High ID = Entity has many internal dependencies
- Changes in internal dependencies can break this entity
- Goal: Minimize internal dependencies to reduce coupling

**Example:**
```csharp
// MyProject.Services has internal dependencies
using MyProject.Data;              // +1 (internal)
using MyProject.Models;            // +1 (internal)
using ThirdParty.Logging;          // External, not counted in ID
using System.Linq;                 // External, not counted in ID
// ID = 2
```

#### Internal Dependents (IDX)

Number of distinct internal entities that depend on this one (inbound dependencies within your solution).

**Interpretation:**
- High IDX = Many other entities depend on this one
- Changes to this entity can break many dependents
- High IDX in core libraries is expected and acceptable

**Example:**
```csharp
// MyProject.Core has high internal dependents
// Referenced by:
// - MyProject.Services
// - MyProject.Api
// - MyProject.Workers
// IDX = 3
```

#### Dependency Ratio (DR)

Ratio of internal dependencies to total internal coupling: `DR = ID / (ID + IDX)`

**Range:** 0.0 (provider) to 1.0 (consumer)

**Interpretation:**
- DR = 0.0: Pure provider (only incoming dependencies, no outgoing)
- DR = 0.5: Balanced
- DR = 1.0: Pure consumer (only outgoing dependencies, no incoming)

**Recommended patterns:**
- Core/shared libraries: DR < 0.5 (providers, stable foundations)
- Application/UI layers: DR > 0.5 (consumers, depend on core)
- Dependency Inversion: High-level modules should have lower DR than low-level modules

**Example:**
```csharp
// MyProject.Core (foundation library)
// ID = 0 (depends on no other internal projects)
// IDX = 10 (depended on by many projects)
// DR = 0 / (0 + 10) = 0.0 (pure provider) ✓ Good for core library

// MyProject.Api (application layer)
// ID = 5 (depends on Core, Services, Models, etc.)
// IDX = 0 (nothing depends on the API layer)
// DR = 5 / (5 + 0) = 1.0 (pure consumer) ✓ Good for top layer
```

#### External Dependencies

StructuraLens tracks external dependencies separately to give you visibility into third-party usage:

- **External BCL Dependencies (EDB)**: Count of System.* and Microsoft.* namespaces referenced
- **External Package Dependencies (EDP)**: Count of third-party package namespaces referenced
- **Total External Dependencies (ED)**: EDB + EDP

**Note:** External dependencies don't participate in internal coupling calculations (ID, IDX, DR) but are reported separately for dependency management insights.

**Example:**
```csharp
// MyProject.Services
using System.Linq;                 // BCL
using System.Collections.Generic;  // BCL (same namespace, counted once)
using Microsoft.Extensions.Logging; // BCL
using Newtonsoft.Json;             // Package
using Serilog;                     // Package
// EDB = 3 (System.Linq, System.Collections.Generic, Microsoft.Extensions.Logging)
// EDP = 2 (Newtonsoft.Json, Serilog)
// ED = 5
```

## Diagnostics

StructuraLens collects all Roslyn compiler diagnostics (errors, warnings, info messages) during analysis.

**Diagnostics are included in JSON and HTML outputs:**
- **JSON**: `diagnostics[]` array with full details
- **HTML**: Diagnostics tab with severity filters
- **Summary**: Diagnostic count in header

**Diagnostic fields:**
- `id` - Diagnostic code (e.g., CS0103, CA1062)
- `severity` - Error, Warning, Info, Hidden
- `message` - Diagnostic message
- `filePath` - Source file location
- `lineNumber` - Line number in source
- `columnNumber` - Column number in source

**Use cases:**
- Track compiler warnings across the codebase
- Identify code analysis violations (CA rules)
- Find potential bugs before runtime

## Exit Codes

StructuraLens returns the following exit codes:

| Code | Meaning |
|------|---------|
| 0 | Success - Analysis completed without errors |
| 1 | Error - Analysis failed (file not found, unhandled exception, or analysis error) |

**Note:** Compiler diagnostics (errors, warnings) do not affect the exit code. They are reported in the output but don't cause the CLI to exit with code 1.

## Tips for Best Results

### 1. Build Before Analyzing

While StructuraLens can analyze source code directly, building your solution first ensures:
- NuGet packages are restored
- Generated code is available
- Assembly references are resolved

```bash
dotnet build
structuralens analyze MySolution.sln --format html --out report.html
```

### 2. Focus on Refactoring Candidates

Look for methods with:
- **CC > 10** - High complexity, hard to test
- **MI < 40** - Low maintainability, technical debt
- **LOC > 100** - Long methods, consider splitting

The summary format highlights these automatically.

### 3. Watch Core Component Instability

Core/shared libraries should have low instability (I < 0.5):
- Many incoming dependencies (high Ca)
- Few outgoing dependencies (low Ce)

If a core library has high instability, consider:
- Reducing dependencies on other projects
- Moving implementation details to separate projects

### 4. Use HTML Reports for Code Reviews

HTML reports provide visual dependency graphs and sortable metrics, making them ideal for:
- Architecture reviews
- Identifying tightly coupled components
- Tracking technical debt

### 5. Automate Analysis in CI/CD

Integrate StructuraLens into your CI pipeline:

```yaml
# GitHub Actions example
- name: Analyze Code Metrics
  run: |
    dotnet tool restore
    structuralens analyze MySolution.sln --format json --out metrics.json
    
- name: Upload Metrics
  uses: actions/upload-artifact@v3
  with:
    name: code-metrics
    path: metrics.json
```

### 6. Compare Metrics Over Time

Track metrics across commits to:
- Identify increasing complexity
- Monitor technical debt growth
- Validate refactoring efforts

Save JSON reports in version control or CI artifacts for historical comparison.

### 7. Use Compact Format for Large Solutions

For solutions with 100+ projects, use compact format to:
- Reduce report size by ~99%
- Speed up report generation
- Integrate with custom visualization tools

```bash
structuralens analyze LargeSolution.sln --format compact --out report.slr
```

## Troubleshooting

### Analysis Fails with "File not found"

**Cause:** Solution or project file doesn't exist at the specified path.

**Solution:** Verify the path is correct and use absolute paths if needed:
```bash
structuralens analyze C:\projects\MySolution.sln
```

### High Memory Usage

**Cause:** Large solution with InMemory strategy.

**Solution:** Use SQLite strategy or increase memory threshold:
```bash
structuralens analyze LargeSolution.sln --aggregation-strategy SQLite
```

### NuGet Restore Fails

**Cause:** Private NuGet feeds require authentication.

**Solution:** Restore packages before running StructuraLens:
```bash
dotnet restore
structuralens analyze MySolution.sln
```

### Missing Metrics for Some Projects

**Cause:** Projects failed to load (missing SDKs, unsupported project types).

**Solution:** Run with `--verbose` to see detailed error messages:
```bash
structuralens analyze MySolution.sln --verbose
```

### HTML Report Graphs Not Loading

**Cause:** Browser blocking CDN access to D3.js or JavaScript disabled.

**Solution:** 
- Ensure internet connectivity (D3.js loads from CDN)
- Enable JavaScript in browser
- Check browser console for errors

## Advanced Usage

### Analyzing Specific Project Types

StructuraLens supports:
- Class libraries (`.csproj`)
- Console applications
- Web applications (ASP.NET Core)
- Test projects
- .NET 6, 7, 8, 9, 10 projects

**Not supported:**
- .NET Framework projects (.NET 4.x)
- Non-C# projects (F#, VB.NET)

### Custom Batch Sizes for SQLite

For very large solutions, adjust SQLite batch size for optimal performance:

```bash
# Larger batches = fewer disk writes, more memory
structuralens analyze LargeSolution.sln --sqlite-batch-size 5000

# Smaller batches = more frequent writes, less memory
structuralens analyze LargeSolution.sln --sqlite-batch-size 500
```

Default (1000) is optimal for most scenarios.

### Combining with Other Tools

StructuraLens JSON output can be consumed by:
- SonarQube (custom import scripts)
- Grafana (visualize metrics over time)
- Code review tools
- Technical debt tracking systems

Example: Parse JSON with jq to extract high-complexity methods:
```bash
structuralens analyze MySolution.sln --out report.json
cat report.json | jq '.projects[].methods[] | select(.cyclomaticComplexity > 10)'
```

## Next Steps

- Read the [Architecture Guide](architecture.md) to understand internal design
- See the [Development Guide](development.md) for contributor information
- Review the [Design Document](design.md) for architectural decisions
- Explore the [Compact Format Specification](compact-format.md) for integration details
