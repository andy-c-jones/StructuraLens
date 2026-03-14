# Copilot / Contribution Guidelines

StructuraLens is a .NET CLI tool for analyzing C# codebases. This document provides quick reference for contributors.

## Branch and PR Workflow

- **Work on feature branches** named `feature/<short-description>` or `fix/<short-description>`
- **Open a pull request** against `main` for all changes
- **Use Conventional Commits** for PR titles and commit messages
- **PRs are squashed on merge**, so the PR title becomes the commit message and release note entry

## Conventional Commits

Use the following format for commit messages and PR titles:

```
<type>: <description>

[optional body]
```

### Types

- `feat:` - New feature (triggers minor version bump)
- `fix:` - Bug fix (triggers patch version bump)
- `chore:` - Maintenance tasks (dependencies, build, CI)
- `docs:` - Documentation changes
- `test:` - Test additions or changes
- `refactor:` - Code refactoring without behavior change
- `perf:` - Performance improvements

### Examples

```
feat: add depth of inheritance calculator
fix: handle null reference in coupling analyzer
chore: update Microsoft.CodeAnalysis dependency
docs: clarify architecture guide with DI patterns
test: add thread-safety tests for SQLite collector
refactor: extract method in metrics calculator
perf: optimize dependency deduplication algorithm
```

## Project Structure Quick Reference

```
src/
├── StructuraLens.Core/         # Core library (all business logic)
│   ├── Abstractions/           # Service interfaces for DI
│   ├── Analysis/               # Analyzers, calculators, collectors
│   ├── Export/                 # Report exporters and generators
│   ├── Infrastructure/         # MSBuild, NuGet, file system
│   └── Models/                 # Domain models
└── StructuraLens.Cli/          # CLI wrapper (thin layer)
    ├── Program.cs              # Entry point, DI setup
    └── Logging/                # Source-generated logging

tests/
└── StructuraLens.Tests/        # TUnit tests
    ├── Analysis/               # Analysis component tests
    ├── Export/                 # Export tests
    ├── Infrastructure/         # Infrastructure tests
    └── Models/                 # Model tests
```

## Dependency Injection

**Container:** Microsoft.Extensions.DependencyInjection

**All services are singletons** (stateless, CLI runs once per invocation)

**Service registration** in `Program.cs`:
```csharp
services.AddSingleton<ISolutionAnalyzer, SolutionAnalyzer>();
services.AddSingleton<ICouplingAnalyzer, CouplingAnalyzer>();
// ... etc
```

**Constructor injection only:**
```csharp
public SolutionAnalyzer(
    ILogger<SolutionAnalyzer> logger,
    INuGetRestorer nugetRestorer,
    IMSBuildWorkspaceFactory workspaceFactory,
    // ... other dependencies
    AnalysisOptions? options = null)
{
    _logger = logger;
    // ... assign fields
}
```

**Interfaces defined in `Abstractions/`**, implementations in functional directories.

## Testing with TUnit

**Framework:** TUnit

**Naming conventions:**
- Test classes: `{ClassName}Tests`
- Test methods: `{MethodName}_{Scenario}_{ExpectedResult}`

**Example:**
```csharp
using TUnit.Core;

public class MetricsCalculatorTests
{
    [Before(Test)]
    public void Setup()
    {
        // Setup runs before each test
    }

    [After(Test)]
    public void Cleanup()
    {
        // Cleanup runs after each test
    }

    [Test]
    public async Task Calculate_EmptyMethod_ReturnsOne()
    {
        // Arrange
        var code = """
            public class Test
            {
                public void EmptyMethod() { }
            }
            """;

        // Act
        var cc = CalculateForMethod(code);

        // Assert
        await Assert.That(cc).IsEqualTo(1);
    }
}
```

**Assertions:**
```csharp
await Assert.That(result).IsNotNull();
await Assert.That(count).IsGreaterThan(0);
await Assert.That(list).Contains("item");
await Assert.That(value).IsEqualTo(expected);
```

**Mocking with FakeItEasy:**
```csharp
var service = A.Fake<IMyService>();
A.CallTo(() => service.Method()).Returns(42);
A.CallTo(() => service.Method()).MustHaveHappenedOnceExactly();
```

## Code Style

**Language features:**
- C# / .NET (follow repository target/framework settings)
- Nullable reference types enabled
- File-scoped namespaces preferred
- Implicit usings enabled

**Naming:**
- Interfaces: `ISolutionAnalyzer`
- Classes: `SolutionAnalyzer`
- Private fields: `_logger`, `_options`
- Methods: `AnalyzeSolutionAsync`, `CreateDependencyCollector`

**Logging:**
Use source-generated logging for performance:
```csharp
[LoggerMessage(
    EventId = 4001,
    Level = LogLevel.Information,
    Message = "Analyzing solution: {SolutionPath}")]
private partial void LogAnalyzingSolution(string solutionPath);
```

**Event ID ranges:**
- 4000-4099: CLI operations
- 4100-4199: Warnings
- 4200-4299: Errors

## Common Development Tasks

### Build and Run

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run CLI locally
dotnet run --project src/StructuraLens.Cli -- analyze MySolution.sln --format summary

# Self-analysis (dogfooding)
dotnet run --project src/StructuraLens.Cli -- analyze StructuraLens.slnx --format summary
```

### Formatting Checks (Husky.NET)

Git pre-commit hooks are managed with Husky.NET and run formatting verification automatically.

```bash
# Restore local tools (includes Husky.NET)
dotnet tool restore

# Install git hooks (once per clone)
dotnet husky install

# Run the same formatting check manually
dotnet format --verify-no-changes
```

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

### Adding a New Dependency Collector

1. Implement `IDependencyCollector` in `src/StructuraLens.Core/Analysis/Collectors/`
2. Add strategy enum value to `DependencyAggregationStrategy`
3. Update factory method in `SolutionAnalyzer.CreateDependencyCollector()`
4. Add tests in `tests/StructuraLens.Tests/Analysis/Collectors/`
5. Document strategy in docs

## Before Submitting PR

1. **Run tests:** `dotnet test`
2. **Build in Release:** `dotnet build -c Release`
3. **Verify formatting:** `dotnet format --verify-no-changes`
4. **Self-analyze:** `dotnet run --project src/StructuraLens.Cli -- analyze StructuraLens.slnx --format summary`
5. **Check for warnings:** `dotnet build /warnaserror`
6. **Update documentation** if needed

## PR Checklist

- [ ] PR title follows Conventional Commits format
- [ ] All tests pass
- [ ] New features have tests
- [ ] Documentation updated if needed
- [ ] Code follows style conventions
- [ ] No new compiler warnings
- [ ] Self-analysis runs successfully

## Semantic Versioning

StructuraLens uses semantic-release for automated versioning:

- `feat:` commits → minor version bump
- `fix:` commits → patch version bump
- `BREAKING CHANGE:` in commit body → major version bump
- Other types → no version bump (chore, docs, test, refactor)

**Ensure PR title is accurate** - it becomes the release note entry!

## License

StructuraLens is licensed under the **MIT License** (`MIT`).

## Architecture Overview

**Core Components:**
- `SolutionAnalyzer` - Main orchestrator
- `CouplingAnalyzer` - Dependency extraction
- `MetricsCalculator` - Metric computation facade
- `Calculators/` - Individual metric calculators (CC, Halstead, LOC, DIT, MI)
- `Collectors/` - Dependency aggregation strategies (InMemory, SQLite, Adaptive)

**Infrastructure:**
- `MSBuildWorkspaceFactory` - Creates Roslyn workspaces
- `NuGetRestorer` - Restores NuGet packages
- `FileSystemService` - File system abstraction

**Export:**
- `CompactReportExporter` - Compact format (.slr)
- `HtmlReportGenerator` - Interactive HTML reports

## Further Documentation

- [Usage Guide](../docs/usage.md) - CLI reference and examples
- [Architecture Guide](../docs/architecture.md) - Internal architecture details
- [Development Guide](../docs/development.md) - Comprehensive developer guide
- [Design Document](../docs/design.md) - High-level design and decisions
- [Compact Format](../docs/compact-format.md) - Output format specification

## Getting Help

- **Issues:** Open GitHub issue for bugs or feature requests
- **Discussions:** Use GitHub Discussions for questions
- **Documentation:** Check docs/ directory for detailed guides

## Key Principles

1. **Core library is independent** - No dependency on CLI or System.CommandLine
2. **Constructor injection only** - No service locator pattern
3. **Interface segregation** - Clean abstraction boundaries
4. **Testability first** - All components easily testable
5. **Performance matters** - Memory-efficient for large codebases
6. **Self-analysis** - Tool runs against its own codebase in CI
7. **Documentation is code** - Keep docs in sync with reality
