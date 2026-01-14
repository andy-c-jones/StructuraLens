# Design Document: StructuraLens

## Executive Summary

StructuraLens is a .NET 10 CLI tool for analyzing C# codebases using Roslyn. It produces per-method/type/project complexity metrics (Cyclomatic Complexity, Halstead Volume, Lines of Executable Code, Depth of Inheritance, Maintainability Index), inter-project coupling analysis, and Roslyn diagnostics. The tool outputs machine-readable JSON (primary), human-friendly HTML reports, compact format optimized for size, and console summaries. Designed for scalability with memory-efficient aggregation strategies for large codebases.

## Goals and Scope

### In Scope

- **Metrics**: 
  - Cyclomatic Complexity (CC = decision_points + 1)
  - Halstead Volume (V = N * log2(n))
  - Lines of Executable Code (LOC for statements)
  - Depth of Inheritance (DIT per class)
  - Maintainability Index (composite metric using Halstead, CC, LOC)
  - Coupling metrics (efferent, afferent, instability)

- **Coupling Analysis**: 
  - Inter-project dependencies
  - Namespace-level coupling
  - Type-level dependencies
  - Calculated at project, namespace, and type granularity

- **Roslyn Diagnostics**: 
  - Collect compiler/IDE/CA diagnostics per project
  - Include severity levels and source locations

- **Output Formats**:
  - JSON (complete, structured, for tooling)
  - HTML (interactive with D3.js graphs)
  - Compact (.slr format, 99% smaller)
  - Summary (console-friendly)

- **Scalability**:
  - Memory-efficient aggregation strategies (InMemory, SQLite, Adaptive)
  - Handle solutions with 100+ projects

### Out of Scope

- Architecture linting and dependency rules enforcement (not implemented)
- Configuration files and hierarchical config inheritance (removed from design)
- Plugin system and extensibility via IMetricProvider (future consideration)
- .NET Framework support (.NET 6+ only)
- Non-C# languages (F#, VB.NET)

## High-Level Architecture

### CLI Workflow

1. **Parse CLI arguments** using System.CommandLine 2.0
2. **Configure logging** (Console logger, adjustable with `--verbose`)
3. **Build analysis options** from CLI parameters
4. **Create dependency injection container** with all services
5. **Execute analysis**:
   - Load solution/project using MSBuildWorkspace
   - Restore NuGet packages if needed
   - Create dependency collector (InMemory, SQLite, or Adaptive)
   - Analyze coupling and calculate metrics
   - Collect Roslyn diagnostics
6. **Generate report** in requested format
7. **Output result** to file or stdout
8. **Return exit code** (0 = success, 1 = error)

### Project Structure

```
StructuraLens/
├── src/
│   ├── StructuraLens.Core/           # Core analysis engine (library)
│   │   ├── Abstractions/             # Interfaces for DI
│   │   ├── Analysis/                 # Analyzers and calculators
│   │   ├── Export/                   # Report exporters/generators
│   │   ├── Infrastructure/           # MSBuild, NuGet, file system
│   │   └── Models/                   # Domain models
│   └── StructuraLens.Cli/            # CLI application (executable)
│       ├── Program.cs                # Entry point, DI setup
│       └── Logging/                  # Source-generated logging
└── tests/
    └── StructuraLens.Tests/          # TUnit tests
        ├── Analysis/                 # Analysis tests
        ├── Export/                   # Export tests
        ├── Infrastructure/           # Infrastructure tests
        └── Models/                   # Model tests
```

## Components and Responsibilities

### CLI Module (`StructuraLens.Cli`)

**Responsibilities:**
- Argument parsing with System.CommandLine
- Logging configuration (console output, log levels)
- Dependency injection container setup
- Command handler orchestration
- Exit code management

**Key Files:**
- `Program.cs` - Entry point, DI container, command definitions

### Core Analysis Engine (`StructuraLens.Core`)

#### Abstractions Layer

Interfaces for dependency injection and testability:
- `ISolutionAnalyzer` - Main analysis orchestrator
- `IMetricsCalculator` - Unified metrics calculation facade
- `ICouplingAnalyzer` - Coupling analysis
- `IMSBuildWorkspaceFactory` - Roslyn workspace creation
- `INuGetRestorer` - NuGet package restoration
- `IReportExporter` - Report export to compact format
- `IReportGenerator` - HTML report generation
- `IFileSystemService` - File system operations
- `IMSBuildRegistrationService` - MSBuild locator management
- `IDependencyCollector` - Dependency aggregation interface

#### Analysis Components

**SolutionAnalyzer** (`ISolutionAnalyzer`)
- Orchestrates entire analysis workflow
- Loads solution/project using MSBuildWorkspace
- Creates appropriate dependency collector based on strategy
- Coordinates coupling analysis, metrics calculation, and diagnostics collection
- Produces final `AnalysisReport`

**CouplingAnalyzer** (`ICouplingAnalyzer`)
- Analyzes semantic models to extract dependencies
- Tracks references between projects, namespaces, and types
- Aggregates dependency edges using IDependencyCollector
- Calculates efferent coupling (Ce), afferent coupling (Ca), and instability (I)
- Builds dependency graphs for visualization

**MetricsCalculator** (`IMetricsCalculator`)
- Facade over individual metric calculators
- Delegates to static calculator classes:
  - `CyclomaticComplexityCalculator` - Uses Roslyn ControlFlowGraph
  - `HalsteadCalculator` - Counts operators/operands via syntax walking
  - `DepthOfInheritanceCalculator` - Traverses base type chains
  - `MaintainabilityIndexCalculator` - Composite metric calculation
  - `LinesOfCodeCalculator` - Counts executable statements

**DiagnosticsCollector**
- Captures Roslyn `Diagnostic` instances from compilations
- Includes compiler errors/warnings and code analysis (CA) rules
- Preserves severity, location, and message information

#### Dependency Collectors

**InMemoryDependencyCollector**
- Uses `ConcurrentDictionary<DependencyEdge, int>` for fast aggregation
- Deduplicates edges and maintains reference counts
- Best for small-medium solutions (up to ~50 projects)
- Memory usage: 50-70% reduction from naive approach

**SQLiteDependencyCollector**
- Disk-backed storage using Microsoft.Data.Sqlite
- Creates temporary database in system temp directory
- Batched inserts for performance (default: 1000 batch size)
- Minimal memory footprint (95% reduction vs. InMemory)
- Best for large solutions (100+ projects)

**AdaptiveDependencyCollector**
- Starts with InMemoryDependencyCollector
- Monitors memory usage via `GC.GetTotalMemory()`
- Automatically migrates to SQLiteDependencyCollector when threshold exceeded
- Seamless transition preserves all collected data
- Default threshold: 1024 MB (configurable via `--memory-threshold`)

#### Export and Report Generation

**CompactReportExporter** (`IReportExporter`)
- Transforms `AnalysisReport` to compact format
- Uses short property names (n, tc, mc, cc, loc, dit, mi, ce, ca, i)
- Produces graph data for D3.js visualization
- Outputs nodes as `[id, name, size]` and edges as `[sourceId, targetId, weight]`

**HtmlReportGenerator** (`IReportGenerator`)
- Generates single-file HTML report with embedded CSS/JS
- Includes D3.js force-directed dependency graphs (loaded from CDN)
- Features:
  - Summary tab with overview cards
  - Projects tab with sortable/filterable metrics table
  - Coupling tab with interactive graphs (project + namespace levels)
  - Diagnostics tab with severity filters
- Uses `IReportExporter` to get compact format for graph data

#### Infrastructure Services

**MSBuildWorkspaceFactory** (`IMSBuildWorkspaceFactory`)
- Creates Roslyn `MSBuildWorkspace` instances
- Ensures MSBuild is registered via `MSBuildLocator`
- Configures workspace properties and diagnostics handling

**MSBuildRegistrationService** (`IMSBuildRegistrationService`)
- Manages MSBuildLocator lifecycle (one-time registration)
- Prevents duplicate registration errors
- Thread-safe singleton pattern

**NuGetRestorer** (`INuGetRestorer`)
- Restores NuGet packages via `dotnet restore` CLI
- Supports private feeds (uses existing NuGet config)
- Provides detailed error logging for restore failures

**FileSystemService** (`IFileSystemService`)
- Abstracts file system operations for testability
- Wraps `System.IO.File` and `System.IO.Directory`
- Used for file existence checks and path validation

## Dependency Injection Architecture

### DI Container

StructuraLens uses **Microsoft.Extensions.DependencyInjection** (built-in .NET DI framework).

### Service Registration

All services registered as **Singletons** in `Program.cs`:

```csharp
services.AddSingleton<ISolutionAnalyzer, SolutionAnalyzer>();
services.AddSingleton<ICouplingAnalyzer, CouplingAnalyzer>();
services.AddSingleton<IMetricsCalculator, MetricsCalculator>();
services.AddSingleton<IMSBuildWorkspaceFactory, MSBuildWorkspaceFactory>();
services.AddSingleton<IMSBuildRegistrationService, MSBuildRegistrationService>();
services.AddSingleton<INuGetRestorer, NuGetRestorer>();
services.AddSingleton<IReportExporter, CompactReportExporter>();
services.AddSingleton<IReportGenerator, HtmlReportGenerator>();
services.AddSingleton<IFileSystemService, FileSystemService>();
```

**Logging:**
- Console logging configured at `Information` level (or `Debug` with `--verbose`)
- `ILogger<T>` injected into all components

### Key Patterns

- **Constructor Injection**: All dependencies injected via constructors
- **Interface Segregation**: Clean separation between abstractions and implementations
- **Factory Pattern**: `IDependencyCollector` implementations created by factory method (not in DI container) to enable runtime strategy selection
- **Manual Resolution**: Command handler creates `SolutionAnalyzer` with manual resolution for `AnalysisOptions`

## Testing Architecture

### Test Framework

**TUnit** (v1.9.45) with Microsoft Testing Platform

### Test Organization

```
tests/StructuraLens.Tests/
├── Analysis/              # SolutionAnalyzer, CouplingAnalyzer, metric calculators
├── Export/                # Report exporters and generators
├── Infrastructure/        # MSBuild, NuGet, FileSystem services
└── Models/                # Domain model tests
```

### Test Patterns

- **Unit Tests**: Test individual components in isolation with mocking (FakeItEasy)
- **Integration Tests**: Test against actual StructuraLens solution (self-testing)
- **Thread-Safety Tests**: Verify concurrent access patterns for dependency collectors

### Test Conventions

- Test classes: `{ClassName}Tests`
- Test methods: `{MethodName}_{Scenario}_{ExpectedResult}`
- Setup/teardown: `[Before(Test)]` and `[After(Test)]` attributes
- Assertions: Fluent `await Assert.That()` pattern

### Mocking Strategy

- Uses **FakeItEasy** for interface mocking
- Minimal mocking - prefers real objects when practical
- Integration tests use zero mocks (full stack)

## CLI UX

### Commands

**analyze** (only command)
- Analyzes solution or project
- Supports all output formats
- Configurable aggregation strategy

### Options

| Option | Default | Description |
|--------|---------|-------------|
| `--out` / `-o` | stdout | Output file path |
| `--format` / `-f` | `json` | Output format (json, compact, html, summary) |
| `--verbose` / `-v` | false | Enable debug logging |
| `--aggregation-strategy` | `Adaptive` | InMemory, SQLite, or Adaptive |
| `--memory-threshold` | 1024 | MB threshold for adaptive strategy |
| `--sqlite-batch-size` | 1000 | Batch size for SQLite writes |

### Exit Codes

- `0` - Success
- `1` - Error (file not found, analysis failure, unhandled exception)

### Output

- **Structured machine-readable output** to stdout or file
- **Logging to stderr** (or console in summary mode)
- **Progress indicators** in verbose mode

## Performance and Scaling

### Parallelization

- Per-project analysis runs in parallel (MSBuildWorkspace limitations)
- Per-file syntax tree processing can be parallelized
- Semantic model reuse minimizes redundant compilations

### Memory Management

- **Deduplication**: Dependency collectors deduplicate edges to reduce memory
- **Adaptive Strategy**: Automatically switches to disk-based storage when needed
- **Batched Writes**: SQLite collector uses batched inserts (1000 records default)
- **Garbage Collection**: Monitors `GC.GetTotalMemory()` for threshold detection

### Caching

- **SemanticModel Caching**: Roslyn workspace caches semantic models per document
- **Compilation Caching**: MSBuildWorkspace reuses compilations across analysis passes

### Large Codebase Support

- **100+ project solutions** tested successfully
- **SQLite strategy** enables analysis of virtually unlimited codebases
- **Incremental analysis** not yet implemented (future enhancement)

## Formulas and Algorithms

### Cyclomatic Complexity

```
CC = decision_points + 1
```

Decision points: `if`, `else if`, `switch` cases, loops, logical operators (`&&`, `||`), ternary operators, `catch` blocks.

**Implementation**: Uses Roslyn's `ControlFlowGraph` API to count branches.

### Halstead Volume

```
V = N * log2(n)
```

Where:
- N = Total operands + operators
- n = Distinct operands + distinct operators

**Implementation**: Syntax walker counts operator/operand tokens.

### Maintainability Index

```
MI = max(0, 100 * (171 - 5.2*ln(V) - 0.23*CC - 16.2*ln(LOC)) / 171)
```

**Range**: 0 (worst) to 100 (best)

### Coupling Metrics

```
Efferent Coupling (Ce) = Count of outgoing dependencies
Afferent Coupling (Ca) = Count of incoming dependencies
Instability (I) = Ce / (Ca + Ce)
```

**Range**: 0.0 (stable) to 1.0 (unstable)

## Deployment Model

### Distribution

- **Pre-built executables** for Windows, Linux, macOS
- **GitHub Releases** with semantic versioning
- **Self-contained** or **framework-dependent** builds

### Build Process

```bash
dotnet build -c Release
dotnet publish -c Release --self-contained -r linux-x64
```

### CI/CD (GitHub Actions)

- **Build and test** on every commit
- **Semantic versioning** via semantic-release
- **Automated releases** with binary artifacts
- **Self-analysis** (tool runs against its own codebase in CI)

## Limitations and Constraints

### Known Limitations

1. **Full assembly dependency analysis** requires built outputs (DLLs/PDBs)
   - Source-only analysis is best-effort
   - Users should run `dotnet build` before analysis for complete results

2. **MSBuildWorkspace limitations**
   - Requires MSBuild SDK installed
   - May fail on unsupported project types

3. **Memory usage**
   - InMemory strategy can use significant RAM for large solutions
   - Adaptive strategy mitigates but adds complexity

4. **No incremental analysis**
   - Full re-analysis required on every run
   - Future enhancement opportunity

### Supported Platforms

- **.NET 6+** (primary target: .NET 10)
- **C# projects only**
- **Windows, Linux, macOS**

### Unsupported

- .NET Framework (.NET 4.x and earlier)
- F#, VB.NET projects
- Projects requiring custom MSBuild targets not available at runtime

## Future Enhancements (Roadmap)

### Not Yet Implemented

1. **Architecture linting** - NsDepCop-like dependency rules (removed from scope)
2. **Configuration files** - `structuralens.json` with hierarchical inheritance (removed from scope)
3. **Plugin system** - IMetricProvider interface for custom metrics (future consideration)
4. **Incremental analysis** - Only re-analyze changed projects (performance optimization)
5. **Windows native support** - Optimized for Windows (currently cross-platform)
6. **Custom analyzer loading** - `--analyzers` flag to load external Roslyn analyzers (planned)

### Potential Future Features

- Trend analysis (compare metrics over time)
- GitHub/Azure DevOps integration
- VS Code extension
- Real-time analysis during development
- Custom metric thresholds and quality gates

## References

### Key Technologies

- **.NET 10** - Runtime platform
- **Roslyn (Microsoft.CodeAnalysis)** - C# code analysis
- **MSBuildWorkspace** - Solution/project loading
- **System.CommandLine 2.0** - CLI framework
- **TUnit** - Testing framework
- **Microsoft.Data.Sqlite** - Disk-backed storage
- **D3.js** - Dependency graph visualization (HTML reports)

### Academic References

- **Cyclomatic Complexity**: McCabe, T.J. (1976)
- **Halstead Complexity**: Halstead, M.H. (1977)
- **Maintainability Index**: Coleman et al. (1994), adapted by Visual Studio
- **Coupling Metrics**: Martin, R.C. (1994) - Afferent/Efferent coupling, Instability

## Appendix: Key Design Decisions

### Why No Architecture Linting?

Architecture linting (dependency rules enforcement) was planned but ultimately removed from scope due to:
- Complexity of rule engine implementation
- Overlap with existing tools (ArchUnitNET, NsDepCop)
- Focus on metrics and coupling analysis as primary value proposition

Future versions may reintroduce this feature if user demand exists.

### Why Remove Configuration Files?

Configuration file support (`structuralens.json`) was designed but not implemented because:
- CLI flags provide sufficient configuration for initial release
- Reduces complexity and maintenance burden
- Configuration inheritance adds significant code complexity
- Users can script CLI invocations for consistency

May be added in future if team usage patterns require it.

### Why Three Aggregation Strategies?

The three-tier approach (InMemory, SQLite, Adaptive) balances:
- **Performance** - InMemory is fastest for small solutions
- **Scalability** - SQLite handles unlimited data
- **Usability** - Adaptive removes need for users to choose

Adaptive strategy chosen as default to provide "just works" experience.

### Why Executable Instead of Container?

Early versions targeted containerized deployment (Docker). This was changed to executable distribution because:
- Simpler distribution and installation
- Faster startup (no container overhead)
- Easier local development and debugging
- Users can still containerize if needed

Container support may return as an optional distribution method.
