# StructuraLens

A high-performance .NET 10 CLI tool for analyzing C# codebases. StructuraLens provides comprehensive code complexity metrics, coupling analysis, and compiler diagnostics with multiple output formats optimized for different use cases.

## Features

- **Code Complexity Metrics**: Cyclomatic Complexity, Halstead Volume, Lines of Executable Code, Depth of Inheritance, and Maintainability Index calculated at method, type, and project levels
- **Coupling Analysis**: Tracks dependencies between projects, namespaces, and types with efferent coupling (Ce), afferent coupling (Ca), and instability (I) metrics
- **Multiple Output Formats**: JSON (machine-readable), HTML (interactive reports), Compact (99% smaller), and Summary (console-friendly)
- **Memory-Efficient**: Adaptive aggregation strategies automatically handle large codebases with hundreds of projects
- **Compiler Diagnostics**: Collects and reports all Roslyn compiler diagnostics with severity levels
- **Self-Contained Analysis**: Uses Roslyn and MSBuild to analyze solutions without requiring compilation

## Quick Start

### Prerequisites

- .NET 10 SDK

### Installation

Download the latest release from the [Releases](https://github.com/your-org/structuralens/releases) page or build from source:

```bash
git clone https://github.com/your-org/structuralens.git
cd structuralens
dotnet build -c Release
```

### Basic Usage

Analyze a solution and generate an HTML report:

```bash
structuralens analyze MySolution.sln --format html --out report.html
```

### Diffing Reports (CI/PR)

Compare a base report to a head report and generate a diff:

```bash
structuralens diff --base base.json --head head.json --format json --out diff.json
```

Generate an HTML report with visual diff overlays:

```bash
structuralens diff --base base.json --head head.json --format html --out diff.html
```

Generate a Markdown summary for PR comments:

```bash
structuralens diff --base base.json --head head.json --format markdown --out diff.md
```

Analyze with JSON output (default):

```bash
structuralens analyze MySolution.sln --out report.json
```

Quick console summary:

```bash
structuralens analyze MySolution.sln --format summary
```

Generate compact format for large solutions:

```bash
structuralens analyze MySolution.sln --format compact --out report.slr
```

## CLI Reference

### analyze Command

Analyzes a C# solution or project and generates metrics reports.

**Syntax:**
```bash
structuralens analyze <path> [options]
```

**Arguments:**
- `<path>` - Path to solution (`.sln`, `.slnx`) or project (`.csproj`) file

**Options:**

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--out` | `-o` | Output file path for the report | stdout |
| `--format` | `-f` | Output format: `json`, `compact`, `html`, `summary` | `json` |
| `--verbose` | `-v` | Enable verbose logging (Debug level) | `false` |
| `--aggregation-strategy` | | Memory strategy: `InMemory`, `SQLite`, `Adaptive` | `Adaptive` |
| `--memory-threshold` | | Memory threshold in MB for adaptive strategy | `1024` |
| `--sqlite-batch-size` | | Batch size for SQLite operations | `1000` |

### Output Formats

- **json** - Complete structured data, suitable for tooling integration
- **html** - Interactive single-file HTML report with dependency graphs and filterable tables
- **compact** - Optimized for size (~99% smaller), includes graph data for visualization
- **summary** - Human-readable console output with key metrics and top complexity items

### diff Command

Compares two JSON analysis reports and produces a diff summary.

**Syntax:**
```bash
structuralens diff --base <base.json> --head <head.json> [options]
```

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--format` | Output format: `json`, `html`, `summary`, `markdown` | `json` |
| `--out` | Output file path | stdout |
| `--max-projects` | Max projects in markdown table | `10` |

See [Usage Guide](docs/usage.md) for detailed documentation.

## Metrics Explained

### Code Complexity

| Metric | Description | Interpretation |
|--------|-------------|----------------|
| **Cyclomatic Complexity (CC)** | Number of independent paths | CC > 10 suggests refactoring |
| **Lines of Executable Code (LOC)** | Count of executable statements | Higher LOC = more maintenance effort |
| **Halstead Volume (V)** | Program size (operators + operands) | Higher volume = more complex |
| **Depth of Inheritance (DIT)** | Inheritance hierarchy depth | Deep hierarchies can be fragile |
| **Maintainability Index (MI)** | Overall maintainability (0-100) | MI < 40 is difficult to maintain |

### Coupling Metrics

| Metric | Description | Interpretation |
|--------|-------------|----------------|
| **Efferent Coupling (Ce)** | Outgoing dependencies | High Ce = depends on many others |
| **Afferent Coupling (Ca)** | Incoming dependencies | High Ca = many others depend on this |
| **Instability (I)** | Ce / (Ca + Ce) | 0.0 = stable, 1.0 = unstable |

## Memory-Efficient Analysis

StructuraLens uses adaptive aggregation strategies to handle large codebases efficiently:

- **InMemory** - Fast, best for small-medium solutions (up to ~50 projects)
- **SQLite** - Disk-backed, best for large solutions (100+ projects), minimal memory usage
- **Adaptive** (default) - Starts with InMemory, automatically migrates to SQLite when memory threshold exceeded

For large solutions, you can force SQLite mode:

```bash
structuralens analyze LargeSolution.sln --aggregation-strategy SQLite --verbose
```

## Building from Source

### Prerequisites

- .NET 10 SDK
- Git

### Build Steps

```bash
git clone https://github.com/your-org/structuralens.git
cd structuralens
dotnet restore
dotnet build -c Release
```

### Run Tests

```bash
dotnet test
```

### Run Locally

```bash
dotnet run --project src/StructuraLens.Cli -- analyze <path>
```

## Project Structure

```
StructuraLens/
├── src/
│   ├── StructuraLens.Core/      # Core analysis engine
│   └── StructuraLens.Cli/       # CLI application
├── tests/
│   └── StructuraLens.Tests/     # TUnit tests
└── docs/                         # Documentation
```

## Documentation

- [Usage Guide](docs/usage.md) - Complete CLI reference and examples
- [Design Document](docs/design.md) - Architecture and design decisions
- [Compact Format Specification](docs/compact-format.md) - Details on the compact output format
- [Architecture Guide](docs/architecture.md) - Internal architecture for contributors
- [Development Guide](docs/development.md) - Developer onboarding and workflows

## Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch: `feature/<short-description>` or `fix/<short-description>`
3. Make your changes with tests
4. Use Conventional Commits for commit messages
5. Open a pull request against `main`

**Conventional Commit Examples:**
- `feat: add new analysis rule for controller coupling`
- `fix: handle null reference in parser when project file missing`
- `chore: update dependencies`
- `docs: clarify README examples`

See [Copilot Instructions](.github/copilot-instructions.md) for detailed contribution guidelines.

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Error (analysis failure, file not found, or unhandled exception) |

## License

Proprietary. All rights reserved.

## Support

For issues, questions, or feature requests, please open an issue on GitHub.
