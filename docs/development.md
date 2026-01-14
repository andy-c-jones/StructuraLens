# StructuraLens Development Guide

This guide helps contributors get started with StructuraLens development, from initial setup through testing, debugging, and submitting changes.

## Table of Contents

- [Getting Started](#getting-started)
- [Project Structure](#project-structure)
- [Development Workflow](#development-workflow)
- [Building the Project](#building-the-project)
- [Running Tests](#running-tests)
- [Running Locally](#running-locally)
- [Debugging](#debugging)
- [Code Style and Conventions](#code-style-and-conventions)
- [Testing Guidelines](#testing-guidelines)
- [Performance Considerations](#performance-considerations)
- [Contributing](#contributing)

## Getting Started

### Prerequisites

- **.NET 10 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Git** - [Download](https://git-scm.com/downloads)
- **IDE** (recommended):
  - Visual Studio 2024 or later
  - JetBrains Rider 2024 or later
  - VS Code with C# Dev Kit extension

### Initial Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/your-org/structuralens.git
   cd structuralens
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Build the project:**
   ```bash
   dotnet build
   ```

4. **Run tests to verify setup:**
   ```bash
   dotnet test
   ```

If all tests pass, you're ready to start developing!

## Project Structure

```
StructuraLens/
├── src/
│   ├── StructuraLens.Core/          # Core analysis library
│   │   ├── Abstractions/            # Service interfaces for DI
│   │   ├── Analysis/                # Analyzers, calculators, collectors
│   │   ├── Export/                  # Report exporters and generators
│   │   ├── Infrastructure/          # MSBuild, NuGet, file system
│   │   └── Models/                  # Domain models and DTOs
│   └── StructuraLens.Cli/           # CLI application
│       ├── Program.cs               # Entry point and DI setup
│       └── Logging/                 # Source-generated logging
├── tests/
│   └── StructuraLens.Tests/         # TUnit test project
│       ├── Analysis/                # Analysis component tests
│       ├── Export/                  # Export tests
│       ├── Infrastructure/          # Infrastructure tests
│       └── Models/                  # Model tests
├── docs/                            # Documentation
│   ├── usage.md                     # User-facing usage guide
│   ├── design.md                    # Architecture and design
│   ├── architecture.md              # Internal architecture details
│   ├── development.md               # This file
│   └── compact-format.md            # Compact format specification
├── .github/
│   ├── workflows/                   # GitHub Actions CI/CD
│   └── copilot-instructions.md      # Contribution guidelines
├── StructuraLens.slnx               # Solution file (.NET XML format)
└── README.md                        # Project overview
```

### Key Directories

- **src/StructuraLens.Core** - All core logic, zero dependency on CLI
- **src/StructuraLens.Cli** - Thin CLI wrapper, argument parsing, DI setup
- **tests/StructuraLens.Tests** - Comprehensive test coverage using TUnit

## Development Workflow

### Branch Strategy

1. **Create feature branch:**
   ```bash
   git checkout -b feature/short-description
   # or for bug fixes:
   git checkout -b fix/short-description
   ```

2. **Make changes and commit frequently:**
   ```bash
   git add .
   git commit -m "feat: add new metric calculator"
   ```

3. **Push to remote:**
   ```bash
   git push origin feature/short-description
   ```

4. **Open pull request** against `main` branch

### Commit Message Format

Use **Conventional Commits** for all commit messages:

```
<type>: <description>

[optional body]

[optional footer]
```

**Types:**
- `feat:` - New feature
- `fix:` - Bug fix
- `chore:` - Maintenance (dependencies, build, etc.)
- `docs:` - Documentation changes
- `test:` - Test additions or changes
- `refactor:` - Code refactoring without behavior change
- `perf:` - Performance improvements

**Examples:**
```bash
git commit -m "feat: add depth of inheritance calculator"
git commit -m "fix: handle null reference in coupling analyzer"
git commit -m "docs: update architecture guide with DI patterns"
git commit -m "test: add thread-safety tests for SQLite collector"
git commit -m "chore: update Microsoft.CodeAnalysis to 10.0.0"
```

**Why Conventional Commits?**
- Enables automated semantic versioning via semantic-release
- Clear changelog generation
- PR titles must follow this format (PRs are squashed on merge)

## Building the Project

### Standard Build

```bash
# Debug build (default)
dotnet build

# Release build
dotnet build -c Release
```

### Build Specific Project

```bash
# Build only Core library
dotnet build src/StructuraLens.Core

# Build only CLI
dotnet build src/StructuraLens.Cli
```

### Clean Build

```bash
dotnet clean
dotnet build
```

### Publish Self-Contained Executable

```bash
# Windows
dotnet publish src/StructuraLens.Cli -c Release -r win-x64 --self-contained

# Linux
dotnet publish src/StructuraLens.Cli -c Release -r linux-x64 --self-contained

# macOS
dotnet publish src/StructuraLens.Cli -c Release -r osx-x64 --self-contained
```

Output will be in `src/StructuraLens.Cli/bin/Release/net10.0/{runtime}/publish/`

## Running Tests

### Run All Tests

```bash
dotnet test
```

### Run Tests with Verbose Output

```bash
dotnet test --verbosity normal
```

### Run Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~CyclomaticComplexityCalculatorTests"
```

### Run Specific Test Method

```bash
dotnet test --filter "FullyQualifiedName~Calculate_SimpleIfStatement_ReturnsTwo"
```

### Run Tests by Category

```bash
# Integration tests only
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# Unit tests (exclude integration)
dotnet test --filter "FullyQualifiedName!~IntegrationTests"
```

### Generate Test Results

```bash
# TRX format (compatible with Azure DevOps, GitHub Actions)
dotnet test --logger:"trx;LogFileName=test-results.trx"
```

### Watch Mode (Auto-run on file changes)

```bash
dotnet watch test
```

## Running Locally

### Run from Source

```bash
# Analyze the StructuraLens solution itself (self-analysis)
dotnet run --project src/StructuraLens.Cli -- analyze StructuraLens.slnx --format summary

# Analyze another solution
dotnet run --project src/StructuraLens.Cli -- analyze /path/to/MySolution.sln --format html --out report.html

# Enable verbose logging
dotnet run --project src/StructuraLens.Cli -- analyze MySolution.sln --verbose --format summary
```

### Run Compiled Executable

```bash
# After building
./src/StructuraLens.Cli/bin/Debug/net10.0/StructuraLens.Cli analyze MySolution.sln

# Or publish and run
dotnet publish src/StructuraLens.Cli -c Release
./src/StructuraLens.Cli/bin/Release/net10.0/publish/StructuraLens.Cli analyze MySolution.sln
```

### Common Development Commands

```bash
# Quick self-analysis with summary
dotnet run --project src/StructuraLens.Cli -- analyze StructuraLens.slnx --format summary

# Generate HTML report for visual inspection
dotnet run --project src/StructuraLens.Cli -- analyze StructuraLens.slnx --format html --out test-report.html

# Test memory management with SQLite
dotnet run --project src/StructuraLens.Cli -- analyze StructuraLens.slnx --aggregation-strategy SQLite --verbose
```

## Debugging

### Visual Studio

1. Set `StructuraLens.Cli` as startup project
2. Right-click project → **Properties** → **Debug**
3. Set **Command line arguments**: `analyze StructuraLens.slnx --format summary`
4. Press F5 to start debugging

### Visual Studio Code

Create `.vscode/launch.json`:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Debug StructuraLens",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/src/StructuraLens.Cli/bin/Debug/net10.0/StructuraLens.Cli.dll",
      "args": ["analyze", "StructuraLens.slnx", "--format", "summary", "--verbose"],
      "cwd": "${workspaceFolder}",
      "stopAtEntry": false,
      "console": "internalConsole"
    }
  ]
}
```

Press F5 to start debugging.

### JetBrains Rider

1. Open Run/Debug Configurations
2. Add new **.NET Project** configuration
3. Set **Project**: StructuraLens.Cli
4. Set **Program arguments**: `analyze StructuraLens.slnx --format summary`
5. Click **Debug** button

### Debugging Tips

**Set breakpoints in key locations:**
- `SolutionAnalyzer.AnalyzeSolutionAsync()` - Main orchestration
- `CouplingAnalyzer.AnalyzeCoupling()` - Dependency extraction
- `MetricsCalculator.CalculateMethodMetrics()` - Metric computation
- Collector `AddDependency()` methods - Aggregation logic

**Use conditional breakpoints:**
```csharp
// Only break when CC > 10
if (cyclomaticComplexity > 10)
{
    // Set breakpoint here
}
```

**Watch expressions:**
- `_dependencies.Count` (in collectors)
- `GC.GetTotalMemory(false)` (memory usage)
- `compilation.GetDiagnostics()` (Roslyn diagnostics)

## Code Style and Conventions

### Language Features

- **Target:** C# 13 / .NET 10
- **Nullable reference types:** Enabled (all projects)
- **Implicit usings:** Enabled
- **File-scoped namespaces:** Preferred

### Naming Conventions

**Interfaces:**
```csharp
public interface ISolutionAnalyzer { }
public interface IMetricsCalculator { }
```

**Classes:**
```csharp
public sealed class SolutionAnalyzer : ISolutionAnalyzer { }
public class MetricsCalculator : IMetricsCalculator { }
```

**Private fields:**
```csharp
private readonly ILogger<SolutionAnalyzer> _logger;
private readonly AnalysisOptions _options;
```

**Methods:**
```csharp
public async Task<AnalysisReport> AnalyzeSolutionAsync(string solutionPath);
private IDependencyCollector CreateDependencyCollector();
```

### Code Organization

**Usings order:**
1. System namespaces
2. Microsoft namespaces
3. Third-party namespaces
4. Project namespaces

**Class member order:**
1. Private fields
2. Constructors
3. Public methods
4. Internal methods
5. Private methods

### Logging

Use source-generated logging for performance:

```csharp
// Define log methods in partial class
public sealed partial class SolutionAnalyzer
{
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Analyzing solution: {SolutionPath}")]
    private partial void LogAnalyzingSolution(string solutionPath);
}

// Use in code
LogAnalyzingSolution(solutionPath);
```

**Event ID ranges:**
- 4000-4099: CLI operations
- 4100-4199: Warnings
- 4200-4299: Errors

### Dependency Injection

**Constructor injection only:**
```csharp
public SolutionAnalyzer(
    ILogger<SolutionAnalyzer> logger,
    INuGetRestorer nugetRestorer,
    // ... other dependencies
    AnalysisOptions? options = null)
{
    _logger = logger;
    _nugetRestorer = nugetRestorer;
    _options = options ?? new AnalysisOptions();
}
```

**Service interfaces in `Abstractions/`:**
- One interface per file
- Interface name matches implementation (with `I` prefix)
- Document public methods with XML comments

### Error Handling

**Fail fast for programmer errors:**
```csharp
ArgumentNullException.ThrowIfNull(solutionPath);
```

**Log and handle expected errors:**
```csharp
try
{
    await _nugetRestorer.RestoreAsync(solutionPath);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "NuGet restore failed, continuing with best-effort analysis");
}
```

## Testing Guidelines

### Test Structure

**AAA Pattern (Arrange, Act, Assert):**
```csharp
[Test]
public async Task CalculateMaintainabilityIndex_SimpleMethod_ReturnsHighScore()
{
    // Arrange
    var code = """
        public class Test
        {
            public int Add(int a, int b) => a + b;
        }
        """;
    var method = GetMethod(code);

    // Act
    var mi = MaintainabilityIndexCalculator.Calculate(1, 1, 10);

    // Assert
    await Assert.That(mi).IsGreaterThan(80);
}
```

### Test Naming

Pattern: `{MethodName}_{Scenario}_{ExpectedResult}`

**Examples:**
```csharp
Calculate_EmptyMethod_ReturnsOne
AnalyzeSolutionAsync_NonExistentFile_ThrowsFileNotFoundException
AddDependency_DuplicateEdge_IncreasesReferenceCount
ParallelAdd_ThreadSafe
```

### Test Attributes

```csharp
using TUnit.Core;

public class MyServiceTests
{
    private MyService _service;

    [Before(Test)]
    public void Setup()
    {
        _service = new MyService();
    }

    [After(Test)]
    public void Cleanup()
    {
        _service?.Dispose();
    }

    [Test]
    public async Task TestMethod()
    {
        // Test implementation
    }
}
```

### Assertions

Use fluent `await Assert.That()` syntax:

```csharp
// Null checks
await Assert.That(result).IsNotNull();
await Assert.That(nullValue).IsNull();

// Equality
await Assert.That(count).IsEqualTo(5);
await Assert.That(name).IsEqualTo("Expected");

// Comparisons
await Assert.That(value).IsGreaterThan(0);
await Assert.That(value).IsLessThan(100);
await Assert.That(value).IsGreaterThanOrEqualTo(10);

// Collections
await Assert.That(list).HasCount().EqualTo(3);
await Assert.That(list).Contains("item");
await Assert.That(list).IsEmpty();
await Assert.That(list).IsNotEmpty();

// Exceptions
await Assert.ThrowsAsync<FileNotFoundException>(
    async () => await analyzer.AnalyzeSolutionAsync("/nonexistent"));
```

### Mocking with FakeItEasy

```csharp
using FakeItEasy;

[Test]
public void Create_CallsRegistrationService()
{
    // Arrange - Create fake
    var registrationService = A.Fake<IMSBuildRegistrationService>();
    var factory = new MSBuildWorkspaceFactory(registrationService);

    // Act
    using var workspace = factory.Create();

    // Assert - Verify call
    A.CallTo(() => registrationService.EnsureMSBuildRegistered())
        .MustHaveHappenedOnceExactly();
}

[Test]
public async Task Method_WithMockedDependency_ReturnsExpectedResult()
{
    // Arrange - Setup return value
    var fileSystem = A.Fake<IFileSystemService>();
    A.CallTo(() => fileSystem.FileExists(A<string>._))
        .Returns(true);

    // Act & Assert
    var result = await fileSystem.FileExists("/path");
    await Assert.That(result).IsTrue();
}
```

### Test Data with Raw Strings

Use C# 13 raw string literals for code samples:

```csharp
var code = """
    public class TestClass
    {
        public void Method()
        {
            if (true)
            {
                Console.WriteLine("Hello");
            }
        }
    }
    """;
```

### Integration Tests

Integration tests run against the actual StructuraLens solution (self-testing):

```csharp
[Test]
public async Task AnalyzeSolutionAsync_OwnSolution_ReturnsValidReport()
{
    // Arrange
    var solutionPath = Path.Combine(
        Path.GetDirectoryName(typeof(SolutionAnalyzerIntegrationTests).Assembly.Location)!,
        "../../../../../StructuraLens.slnx");
    
    var analyzer = CreateAnalyzer(); // Real services, no mocks

    // Act
    var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

    // Assert
    await Assert.That(report).IsNotNull();
    await Assert.That(report.TotalProjects).IsGreaterThan(0);
}
```

### Thread-Safety Tests

Verify concurrent access patterns:

```csharp
[Test]
public async Task ParallelAdd_ThreadSafe()
{
    using var collector = new InMemoryDependencyCollector();
    var tasks = Enumerable.Range(0, 100)
        .Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                collector.AddDependency(
                    new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
            }
        }))
        .ToList();

    await Task.WhenAll(tasks);
    var result = collector.GetAggregatedDependencies();

    await Assert.That(result).HasCount().EqualTo(1);
    await Assert.That(result[0].ReferenceCount).IsEqualTo(100_000);
}
```

## Performance Considerations

### Memory Management

**Use `using` for disposables:**
```csharp
using var workspace = _workspaceFactory.Create();
using var collector = CreateDependencyCollector();
```

**Dispose collectors explicitly:**
```csharp
try
{
    var result = collector.GetAggregatedDependencies();
    return CreateReport(result);
}
finally
{
    collector.Dispose();
}
```

### Async Best Practices

**Use `ConfigureAwait(false)` in library code:**
```csharp
await Task.Delay(1000).ConfigureAwait(false);
```

**Avoid sync-over-async:**
```csharp
// Bad
var result = task.Result;  // Can deadlock

// Good
var result = await task;
```

### Roslyn Performance

**Reuse semantic models:**
```csharp
var semanticModel = compilation.GetSemanticModel(syntaxTree);
// Use same semantic model for all nodes in tree
```

**Cache compilations:**
```csharp
// MSBuildWorkspace automatically caches compilations
var compilation = await project.GetCompilationAsync();
```

### Collection Performance

**Use appropriate collection types:**
```csharp
// For deduplication and lookups
var seen = new HashSet<string>();

// For thread-safe aggregation
var dict = new ConcurrentDictionary<string, int>();

// For ordered iteration
var list = new List<string>();
```

## Contributing

### Before Submitting PR

1. **Run tests:**
   ```bash
   dotnet test
   ```

2. **Build in Release mode:**
   ```bash
   dotnet build -c Release
   ```

3. **Self-analyze:**
   ```bash
   dotnet run --project src/StructuraLens.Cli -- analyze StructuraLens.slnx --format summary
   ```

4. **Check for warnings:**
   ```bash
   dotnet build /warnaserror
   ```

### Pull Request Checklist

- [ ] PR title follows Conventional Commits format
- [ ] All tests pass (`dotnet test`)
- [ ] New features have tests
- [ ] Documentation updated if needed
- [ ] Code follows style conventions
- [ ] No new compiler warnings
- [ ] Ran self-analysis successfully

### PR Title Format

PR titles must follow Conventional Commits (PRs are squashed on merge):

```
feat: add new aggregation strategy
fix: resolve null reference in coupling analyzer
docs: update architecture guide
test: add integration tests for exporters
chore: update dependencies
```

### Review Process

1. **Automated checks** run (build, test, analysis)
2. **Maintainer review** for code quality and design
3. **Address feedback** with new commits
4. **Squash and merge** when approved

### Getting Help

- **Issues:** Open GitHub issue for bugs or feature requests
- **Discussions:** Use GitHub Discussions for questions
- **Documentation:** Check docs/ directory for detailed guides

## Additional Resources

- [Architecture Guide](architecture.md) - Internal architecture details
- [Usage Guide](usage.md) - CLI reference and examples
- [Design Document](design.md) - High-level design and decisions
- [Compact Format](compact-format.md) - Output format specification
- [Copilot Instructions](.github/copilot-instructions.md) - Quick contributor reference

## Common Tasks

### Adding a New Metric Calculator

1. Create calculator class in `src/StructuraLens.Core/Analysis/Calculators/`
2. Implement static `Calculate` method
3. Add tests in `tests/StructuraLens.Tests/Analysis/Calculators/`
4. Integrate into `MetricsCalculator.cs`
5. Update models if needed (`MethodMetrics`, `TypeMetrics`)

### Adding a New Output Format

1. Create exporter class in `src/StructuraLens.Core/Export/`
2. Implement export logic
3. Add format option to CLI (`Program.cs`)
4. Add tests in `tests/StructuraLens.Tests/Export/`
5. Document in `docs/usage.md`

### Adding a New Aggregation Strategy

1. Implement `IDependencyCollector` in `src/StructuraLens.Core/Analysis/Collectors/`
2. Add strategy enum value to `DependencyAggregationStrategy`
3. Update factory method in `SolutionAnalyzer.CreateDependencyCollector()`
4. Add tests in `tests/StructuraLens.Tests/Analysis/Collectors/`
5. Document strategy in docs

### Updating Dependencies

```bash
# Check for outdated packages
dotnet list package --outdated

# Update specific package
dotnet add package Microsoft.CodeAnalysis.CSharp --version 10.0.0

# Update all packages (careful!)
dotnet outdated --upgrade
```

### Profiling Performance

**Use BenchmarkDotNet for micro-benchmarks:**

```csharp
[MemoryDiagnoser]
public class CollectorBenchmarks
{
    [Benchmark]
    public void InMemoryCollector()
    {
        using var collector = new InMemoryDependencyCollector();
        for (int i = 0; i < 10000; i++)
        {
            collector.AddDependency(new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
        }
        _ = collector.GetAggregatedDependencies();
    }
}
```

**Profile real analysis with dotnet-trace:**

```bash
dotnet tool install --global dotnet-trace
dotnet trace collect --process-id <pid> --providers Microsoft-DotNETCore-SampleProfiler
```

---

Happy coding! If you have questions or need help, don't hesitate to open an issue or discussion on GitHub.
