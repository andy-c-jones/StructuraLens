# StructuraLens Architecture

This document provides a comprehensive overview of the StructuraLens internal architecture, designed for contributors and maintainers. It covers the project structure, dependency injection patterns, core components, testing architecture, and extension points.

## Table of Contents

- [Project Structure](#project-structure)
- [Dependency Injection](#dependency-injection)
- [Core Components](#core-components)
- [Dependency Collectors](#dependency-collectors)
- [Testing Architecture](#testing-architecture)
- [Extension Points](#extension-points)

## Project Structure

StructuraLens is organized as a multi-project solution:

```
StructuraLens/
├── StructuraLens.slnx                      # Solution file (.NET XML format)
├── src/
│   ├── StructuraLens.Core/                 # Core analysis library
│   │   ├── Abstractions/                   # Service interfaces
│   │   │   ├── ICouplingAnalyzer.cs
│   │   │   ├── IDependencyCollector.cs
│   │   │   ├── IFileSystemService.cs
│   │   │   ├── IMetricsCalculator.cs
│   │   │   ├── IMSBuildRegistrationService.cs
│   │   │   ├── IMSBuildWorkspaceFactory.cs
│   │   │   ├── INuGetRestorer.cs
│   │   │   ├── IReportExporter.cs
│   │   │   ├── IReportGenerator.cs
│   │   │   └── ISolutionAnalyzer.cs
│   │   ├── Analysis/                       # Analysis components
│   │   │   ├── CouplingAnalyzer.cs
│   │   │   ├── MetricsCalculator.cs
│   │   │   ├── SolutionAnalyzer.cs
│   │   │   ├── Calculators/
│   │   │   │   ├── CyclomaticComplexityCalculator.cs
│   │   │   │   ├── DepthOfInheritanceCalculator.cs
│   │   │   │   ├── HalsteadCalculator.cs
│   │   │   │   ├── LinesOfCodeCalculator.cs
│   │   │   │   └── MaintainabilityIndexCalculator.cs
│   │   │   └── Collectors/
│   │   │       ├── AdaptiveDependencyCollector.cs
│   │   │       ├── InMemoryDependencyCollector.cs
│   │   │       └── SQLiteDependencyCollector.cs
│   │   ├── Export/                         # Report generation
│   │   │   ├── CompactReportExporter.cs
│   │   │   └── HtmlReportGenerator.cs
│   │   ├── Infrastructure/                 # External service integrations
│   │   │   ├── FileSystemService.cs
│   │   │   ├── MSBuildRegistrationService.cs
│   │   │   ├── MSBuildWorkspaceFactory.cs
│   │   │   └── NuGetRestorer.cs
│   │   └── Models/                         # Domain models
│   │       ├── AnalysisOptions.cs
│   │       ├── AnalysisReport.cs
│   │       ├── DependencyEdge.cs
│   │       ├── MethodMetrics.cs
│   │       ├── ProjectMetrics.cs
│   │       └── TypeMetrics.cs
│   └── StructuraLens.Cli/                  # CLI application
│       ├── Program.cs                      # Entry point & DI setup
│       └── Logging/
│           └── ProgramLog.cs               # Source-generated logging
└── tests/
    └── StructuraLens.Tests/                # TUnit test project
        ├── Analysis/                       # Analysis component tests
        ├── Export/                         # Export tests
        ├── Infrastructure/                 # Infrastructure tests
        └── Models/                         # Model tests
```

### Project Responsibilities

**StructuraLens.Core**
- Core analysis engine (library)
- No dependency on CLI or System.CommandLine
- Can be referenced by other tools/integrations
- Contains all business logic and Roslyn integration

**StructuraLens.Cli**
- Thin CLI wrapper around Core
- Argument parsing with System.CommandLine
- Dependency injection container setup
- Logging configuration
- Output formatting and file writing

**StructuraLens.Tests**
- Comprehensive test coverage
- Mix of unit, integration, and thread-safety tests
- Uses TUnit testing framework
- Tests Core library (Cli is thin and doesn't require extensive testing)

## Dependency Injection

### Container and Framework

StructuraLens uses **Microsoft.Extensions.DependencyInjection** (Microsoft's built-in DI container) configured in `Program.cs`.

### Service Registration

All services registered as **Singletons** (stateless services only):

```csharp
// src/StructuraLens.Cli/Program.cs (lines 13-46)
var services = new ServiceCollection();

// Logging
services.AddLogging(builder => builder
    .AddConsole()
    .SetMinimumLevel(LogLevel.Information));

// Core analysis services
services.AddSingleton<ISolutionAnalyzer, SolutionAnalyzer>();
services.AddSingleton<ICouplingAnalyzer, CouplingAnalyzer>();
services.AddSingleton<IMetricsCalculator, MetricsCalculator>();

// Infrastructure services
services.AddSingleton<IMSBuildWorkspaceFactory, MSBuildWorkspaceFactory>();
services.AddSingleton<IMSBuildRegistrationService, MSBuildRegistrationService>();
services.AddSingleton<INuGetRestorer, NuGetRestorer>();
services.AddSingleton<IFileSystemService, FileSystemService>();

// Export services
services.AddSingleton<IReportExporter, CompactReportExporter>();
services.AddSingleton<IReportGenerator, HtmlReportGenerator>();

var serviceProvider = services.BuildServiceProvider();
```

### Key DI Patterns

#### 1. Constructor Injection

All services receive dependencies through constructors exclusively:

```csharp
public sealed class SolutionAnalyzer : ISolutionAnalyzer
{
    private readonly ILogger<SolutionAnalyzer> _logger;
    private readonly INuGetRestorer _nugetRestorer;
    private readonly IMSBuildWorkspaceFactory _workspaceFactory;
    private readonly ICouplingAnalyzer _couplingAnalyzer;
    private readonly IMetricsCalculator _metricsCalculator;
    private readonly IFileSystemService _fileSystem;
    private readonly AnalysisOptions _options;

    public SolutionAnalyzer(
        ILogger<SolutionAnalyzer> logger,
        INuGetRestorer nugetRestorer,
        IMSBuildWorkspaceFactory workspaceFactory,
        ICouplingAnalyzer couplingAnalyzer,
        IMetricsCalculator metricsCalculator,
        IFileSystemService fileSystem,
        AnalysisOptions? options = null)
    {
        _logger = logger;
        _nugetRestorer = nugetRestorer;
        _workspaceFactory = workspaceFactory;
        _couplingAnalyzer = couplingAnalyzer;
        _metricsCalculator = metricsCalculator;
        _fileSystem = fileSystem;
        _options = options ?? new AnalysisOptions();
    }
}
```

#### 2. Interface Segregation

Clean separation between abstractions and implementations:
- Interfaces defined in `Core/Abstractions/`
- Implementations in respective functional directories
- Core library has zero dependency on DI container
- Enables easy mocking and testing

#### 3. Factory Pattern for Collectors

`IDependencyCollector` implementations are **NOT** registered in the DI container. Instead, `SolutionAnalyzer` uses a **Factory Method** to create collectors at runtime:

```csharp
private IDependencyCollector CreateDependencyCollector()
{
    return _options.AggregationStrategy switch
    {
        DependencyAggregationStrategy.InMemory => 
            new InMemoryDependencyCollector(),
        
        DependencyAggregationStrategy.SQLite => 
            new SQLiteDependencyCollector(
                _options.SQLiteBatchSize, 
                _logger),
        
        DependencyAggregationStrategy.Adaptive => 
            new AdaptiveDependencyCollector(
                _options.MemoryThresholdMB, 
                _options.SQLiteBatchSize, 
                _logger),
        
        _ => throw new ArgumentException($"Unknown strategy: {_options.AggregationStrategy}")
    };
}
```

**Why factory pattern?**
- Strategy selection happens at runtime based on `AnalysisOptions`
- Collectors have different constructor parameters
- Enables clean separation of concerns

#### 4. Options Pattern (Simplified)

`AnalysisOptions` passed directly to `SolutionAnalyzer` constructor rather than using `IOptions<T>`:

```csharp
var analyzer = new SolutionAnalyzer(
    serviceProvider.GetRequiredService<ILogger<SolutionAnalyzer>>(),
    serviceProvider.GetRequiredService<INuGetRestorer>(),
    serviceProvider.GetRequiredService<IMSBuildWorkspaceFactory>(),
    serviceProvider.GetRequiredService<ICouplingAnalyzer>(),
    serviceProvider.GetRequiredService<IMetricsCalculator>(),
    serviceProvider.GetRequiredService<IFileSystemService>(),
    analysisOptions  // Options passed directly
);
```

This lightweight approach is suitable for CLI applications where configuration is straightforward.

#### 5. Dynamic Service Provider Recreation

For the `--verbose` flag, the CLI creates a **new ServiceCollection** with adjusted log levels:

```csharp
if (verbose)
{
    // Create new service provider with Debug logging
    services = new ServiceCollection();
    services.AddLogging(builder => builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Debug));
    
    // Re-register all services...
    serviceProvider = services.BuildServiceProvider();
}
```

This ensures immutability of the base configuration.

### Dependency Chain

The application has a 3-level dependency chain:

**Level 1: Main Orchestrator**
- `SolutionAnalyzer` - Depends on 6 services

**Level 2: Infrastructure & Analysis**
- `CouplingAnalyzer` - Depends on logger
- `MetricsCalculator` - No dependencies (delegates to static calculators)
- `MSBuildWorkspaceFactory` - Depends on `IMSBuildRegistrationService`
- `NuGetRestorer` - Depends on logger
- `HtmlReportGenerator` - Depends on `IReportExporter`
- `CompactReportExporter` - No dependencies
- `FileSystemService` - No dependencies (wraps System.IO)

**Level 3: Platform Services**
- `MSBuildRegistrationService` - No dependencies

### Service Lifetimes

| Service | Lifetime | Reason |
|---------|----------|--------|
| All services | Singleton | Stateless, CLI runs once per invocation |
| `ILogger<T>` | Singleton | Logger factory creates loggers |

**No Scoped or Transient lifetimes** - CLI executes a single analysis per invocation, so singletons are sufficient and most performant.

## Core Components

### SolutionAnalyzer (Main Orchestrator)

**Location:** `src/StructuraLens.Core/Analysis/SolutionAnalyzer.cs`

**Responsibility:** Orchestrates entire analysis workflow from solution loading to report generation.

**Key Methods:**

```csharp
public async Task<AnalysisReport> AnalyzeSolutionAsync(string solutionPath);
```

**Workflow:**

1. **Validate Input**
   - Check if solution/project file exists
   - Throw `FileNotFoundException` if not found

2. **Restore NuGet Packages**
   - Call `INuGetRestorer.RestoreAsync()`
   - Continue on restore failure (best-effort)

3. **Load Solution**
   - Create MSBuildWorkspace via `IMSBuildWorkspaceFactory`
   - Open solution or project
   - Handle workspace diagnostics

4. **Create Dependency Collector**
   - Factory method selects strategy (InMemory, SQLite, Adaptive)
   - Collector used by `CouplingAnalyzer`

5. **Analyze Projects**
   - Iterate through all projects in solution
   - For each project:
     - Get compilation
     - Calculate metrics via `IMetricsCalculator`
     - Analyze coupling via `ICouplingAnalyzer`
     - Collect Roslyn diagnostics

6. **Aggregate Results**
   - Get aggregated dependencies from collector
   - Calculate coupling metrics (Ce, Ca, I)
   - Build dependency graphs

7. **Return Report**
   - Create `AnalysisReport` with all data
   - Dispose workspace and collector

**Error Handling:**
- File not found exceptions propagate to caller
- Workspace errors logged as warnings
- Project load failures logged but don't halt analysis

### CouplingAnalyzer

**Location:** `src/StructuraLens.Core/Analysis/CouplingAnalyzer.cs`

**Responsibility:** Extracts dependencies from semantic models and aggregates them using collectors.

**Key Methods:**

```csharp
public void AnalyzeCoupling(
    Solution solution, 
    IDependencyCollector collector);
```

**How It Works:**

1. **Iterate Through Projects**
   - For each project in solution

2. **Get Compilation**
   - Retrieve `Compilation` from project

3. **Walk Syntax Trees**
   - For each document in project:
     - Get syntax tree
     - Get semantic model
     - Walk syntax nodes

4. **Extract Dependencies**
   - Identify type references (method calls, field access, base types)
   - Resolve symbols via `SemanticModel.GetSymbolInfo()`
   - Determine dependency type (Project, Namespace, Type)

5. **Add to Collector**
   - Create `DependencyEdge` with source/target/type
   - Call `collector.AddDependency(edge)`
   - Collector handles deduplication and aggregation

6. **Build Graphs**
   - After all edges collected, call `collector.GetAggregatedDependencies()`
   - Build project, namespace, and type dependency graphs
   - Calculate coupling metrics (Ce, Ca, I)

**Dependency Types:**
- `ProjectReference` - Project-to-project dependencies
- `TypeReference` - Type-to-type references (method calls, field access)
- `NamespaceReference` - Namespace-level coupling

### MetricsCalculator

**Location:** `src/StructuraLens.Core/Analysis/MetricsCalculator.cs`

**Responsibility:** Facade over individual metric calculators. Delegates to static calculator classes.

**Key Methods:**

```csharp
public MethodMetrics CalculateMethodMetrics(
    IMethodSymbol method, 
    SemanticModel semanticModel);

public TypeMetrics CalculateTypeMetrics(
    INamedTypeSymbol type, 
    Compilation compilation);
```

**Delegates to:**

#### CyclomaticComplexityCalculator

**Location:** `src/StructuraLens.Core/Analysis/Calculators/CyclomaticComplexityCalculator.cs`

**How It Works:**
- Uses Roslyn's `ControlFlowGraph` API
- Counts basic blocks and decision points
- Formula: `CC = edges - nodes + 2 * exit_points`
- Simplified: `CC = decision_points + 1`

**Decision Points:**
- `if`, `else if` statements
- `switch` cases
- Loops (`for`, `foreach`, `while`, `do-while`)
- Logical operators (`&&`, `||`)
- Ternary operators (`? :`)
- `catch` blocks
- Null-coalescing operators (`??`)

#### HalsteadCalculator

**Location:** `src/StructuraLens.Core/Analysis/Calculators/HalsteadCalculator.cs`

**How It Works:**
- Walks syntax tree using `CSharpSyntaxWalker`
- Counts operators and operands
- Tracks distinct vs. total counts
- Formula: `V = N * log2(n)`
  - N = n1 + n2 (total operators + total operands)
  - n = η1 + η2 (distinct operators + distinct operands)

**Operators Include:**
- Arithmetic: `+`, `-`, `*`, `/`, `%`
- Logical: `&&`, `||`, `!`
- Comparison: `==`, `!=`, `<`, `>`, `<=`, `>=`
- Assignment: `=`, `+=`, `-=`, etc.
- Member access: `.`, `?. `, `[]`
- Method invocations

**Operands Include:**
- Identifiers (variable names, method names)
- Literals (numbers, strings, booleans)
- Keywords used as values (`this`, `base`, `null`)

#### DepthOfInheritanceCalculator

**Location:** `src/StructuraLens.Core/Analysis/Calculators/DepthOfInheritanceCalculator.cs`

**How It Works:**
- Traverses base type chain using `INamedTypeSymbol.BaseType`
- Counts levels until reaching `System.Object` or null
- Returns depth as integer

**Example:**
```csharp
class Animal { }                // DIT = 0
class Mammal : Animal { }       // DIT = 1
class Dog : Mammal { }          // DIT = 2
```

#### LinesOfCodeCalculator

**Location:** `src/StructuraLens.Core/Analysis/Calculators/LinesOfCodeCalculator.cs`

**How It Works:**
- Counts executable statements (not including braces, comments, whitespace)
- Uses syntax tree to find statement nodes
- Excludes:
  - Empty statements
  - Block statements (braces)
  - Using directives
  - Namespace declarations

**Counts:**
- Expression statements
- Return statements
- Variable declarations
- Control flow statements (if, for, while, etc.)
- Jump statements (break, continue, goto, throw)

#### MaintainabilityIndexCalculator

**Location:** `src/StructuraLens.Core/Analysis/Calculators/MaintainabilityIndexCalculator.cs`

**How It Works:**
- Composite metric combining CC, LOC, and Halstead Volume
- Formula: `MI = max(0, 100 * (171 - 5.2*ln(V) - 0.23*CC - 16.2*ln(LOC)) / 171)`
- Returns value between 0 (worst) and 100 (best)

**Interpretation:**
- 0-9: Unmaintainable
- 10-19: Difficult to maintain
- 20-39: Moderate
- 40-100: Good maintainability

### Export Services

#### CompactReportExporter

**Location:** `src/StructuraLens.Core/Export/CompactReportExporter.cs`

**Responsibility:** Transform `AnalysisReport` to compact JSON format with short property names.

**Transformations:**
- `name` → `n`
- `typeCount` → `tc`
- `methodCount` → `mc`
- `cyclomaticComplexity` → `cc`
- `linesOfExecutableCode` → `loc`
- `depthOfInheritance` → `dit`
- `maintainabilityIndex` → `mi`
- `efferentCoupling` → `ce`
- `afferentCoupling` → `ca`
- `instability` → `i`

**Graph Data:**
- Nodes: `[id, name, size]`
- Edges: `[sourceId, targetId, weight]`
- Enables D3.js force-directed graph visualization

#### HtmlReportGenerator

**Location:** `src/StructuraLens.Core/Export/HtmlReportGenerator.cs`

**Responsibility:** Generate interactive single-file HTML reports.

**Features:**
- Embeds all CSS and JavaScript inline
- Loads D3.js from CDN for graphs
- Tabs: Summary, Projects, Coupling, Diagnostics
- Interactive force-directed graphs (project + namespace dependencies)
- Sortable/filterable tables
- Responsive design

**Implementation:**
- Uses `IReportExporter` to get compact format
- Embeds compact JSON as JavaScript variable
- HTML template with embedded styles
- D3.js v7 for graph rendering

## Dependency Collectors

Collectors implement the `IDependencyCollector` interface to aggregate dependency edges during analysis.

### Interface

```csharp
public interface IDependencyCollector : IDisposable
{
    void AddDependency(DependencyEdge edge);
    List<AggregatedDependency> GetAggregatedDependencies();
}
```

### InMemoryDependencyCollector

**Location:** `src/StructuraLens.Core/Analysis/Collectors/InMemoryDependencyCollector.cs`

**Strategy:** Concurrent in-memory dictionary with deduplication.

**Implementation:**
```csharp
private readonly ConcurrentDictionary<DependencyEdge, int> _dependencies = new();

public void AddDependency(DependencyEdge edge)
{
    _dependencies.AddOrUpdate(edge, 1, (_, count) => count + 1);
}
```

**Characteristics:**
- **Thread-safe**: Uses `ConcurrentDictionary`
- **Deduplication**: Automatically merges duplicate edges
- **Reference counting**: Tracks how many times each edge appears
- **Memory usage**: 50-70% reduction from naive approach
- **Performance**: Fastest (no I/O)

**Best for:** Small-medium solutions (up to ~50 projects)

### SQLiteDependencyCollector

**Location:** `src/StructuraLens.Core/Analysis/Collectors/SQLiteDependencyCollector.cs`

**Strategy:** Disk-backed SQLite database with batched writes.

**Implementation:**
```csharp
private readonly SqliteConnection _connection;
private readonly List<DependencyEdge> _batch;
private readonly int _batchSize;

public void AddDependency(DependencyEdge edge)
{
    _batch.Add(edge);
    
    if (_batch.Count >= _batchSize)
    {
        FlushBatch();
    }
}

private void FlushBatch()
{
    using var transaction = _connection.BeginTransaction();
    foreach (var edge in _batch)
    {
        // INSERT OR UPDATE with reference count
    }
    transaction.Commit();
    _batch.Clear();
}
```

**Database Schema:**
```sql
CREATE TABLE Dependencies (
    Source TEXT NOT NULL,
    Target TEXT NOT NULL,
    Type TEXT NOT NULL,
    ReferenceCount INTEGER NOT NULL,
    PRIMARY KEY (Source, Target, Type)
);
```

**Characteristics:**
- **Disk-backed**: Minimal memory usage
- **Batched writes**: Configurable batch size (default: 1000)
- **Transactional**: Uses SQLite transactions for consistency
- **Temporary**: Database created in temp directory, deleted after analysis
- **Memory reduction**: 95% vs. InMemory
- **Performance**: 10-20% slower than InMemory

**Best for:** Large solutions (100+ projects)

### AdaptiveDependencyCollector

**Location:** `src/StructuraLens.Core/Analysis/Collectors/AdaptiveDependencyCollector.cs`

**Strategy:** Start with InMemory, migrate to SQLite when memory threshold exceeded.

**Implementation:**
```csharp
private IDependencyCollector _currentCollector;
private bool _migrated = false;

public void AddDependency(DependencyEdge edge)
{
    _currentCollector.AddDependency(edge);
    
    if (!_migrated && ShouldMigrate())
    {
        MigrateToSQLite();
    }
}

private bool ShouldMigrate()
{
    var memoryUsed = GC.GetTotalMemory(forceFullCollection: false);
    return memoryUsed > _thresholdBytes;
}

private void MigrateToSQLite()
{
    _logger.LogInformation("Memory threshold exceeded, migrating to SQLite");
    
    var sqliteCollector = new SQLiteDependencyCollector(_batchSize, _logger);
    
    // Copy all existing dependencies
    foreach (var dep in _currentCollector.GetAggregatedDependencies())
    {
        for (int i = 0; i < dep.ReferenceCount; i++)
        {
            sqliteCollector.AddDependency(new DependencyEdge(
                dep.Source, dep.Target, dep.DependencyType, 1));
        }
    }
    
    _currentCollector.Dispose();
    _currentCollector = sqliteCollector;
    _migrated = true;
}
```

**Characteristics:**
- **Transparent**: Analysis continues uninterrupted during migration
- **Memory monitoring**: Uses `GC.GetTotalMemory()` for threshold detection
- **Configurable threshold**: Default 1024 MB, adjustable via `--memory-threshold`
- **One-way migration**: Never migrates back to InMemory
- **Best of both**: Fast for small solutions, scalable for large ones

**Best for:** Unknown codebase sizes (default strategy)

## Testing Architecture

### Test Framework

**TUnit v1.9.45** with Microsoft Testing Platform

**Configuration** (StructuraLens.Tests.csproj):
```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    <OutputType>Exe</OutputType>
</PropertyGroup>
```

**Dependencies:**
- TUnit (v1.9.45) - Testing framework
- FakeItEasy (v9.0.0) - Mocking framework
- Microsoft.Testing.Extensions.TrxReport (v2.0.2) - TRX reports

### Test Organization

Tests organized by functional area, mirroring source structure:

```
tests/StructuraLens.Tests/
├── Analysis/
│   ├── SolutionAnalyzerTests.cs
│   ├── SolutionAnalyzerIntegrationTests.cs
│   ├── CouplingAnalyzerTests.cs
│   ├── MetricsCalculatorTests.cs
│   ├── Calculators/
│   │   ├── CyclomaticComplexityCalculatorTests.cs
│   │   ├── HalsteadCalculatorTests.cs
│   │   ├── DepthOfInheritanceCalculatorTests.cs
│   │   ├── LinesOfCodeCalculatorTests.cs
│   │   └── MaintainabilityIndexCalculatorTests.cs
│   └── Collectors/
│       ├── InMemoryDependencyCollectorTests.cs
│       ├── SQLiteDependencyCollectorTests.cs
│       └── AdaptiveDependencyCollectorTests.cs
├── Export/
│   ├── CompactReportExporterTests.cs
│   └── HtmlReportGeneratorTests.cs
├── Infrastructure/
│   ├── FileSystemServiceTests.cs
│   ├── MSBuildWorkspaceFactoryTests.cs
│   ├── MSBuildRegistrationServiceTests.cs
│   └── NuGetRestorerTests.cs
└── Models/
    └── AnalysisOptionsTests.cs
```

### Test Patterns

#### Unit Tests

Test individual components in isolation using mocks:

```csharp
[Test]
public void Create_EnsuresMSBuildRegistration_CallsRegistrationService()
{
    // Arrange
    var registrationService = A.Fake<IMSBuildRegistrationService>();
    var factory = new MSBuildWorkspaceFactory(registrationService);

    // Act
    using var workspace = factory.Create();

    // Assert
    A.CallTo(() => registrationService.EnsureMSBuildRegistered())
        .MustHaveHappenedOnceExactly();
}
```

#### Integration Tests

Test against actual StructuraLens solution (self-testing):

```csharp
[Test]
public async Task AnalyzeSolutionAsync_OwnSolution_ReturnsValidReport()
{
    // Arrange
    var solutionPath = GetSolutionPath(); // Points to StructuraLens.slnx
    var analyzer = CreateAnalyzer();      // Real services, no mocks

    // Act
    var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

    // Assert
    await Assert.That(report).IsNotNull();
    await Assert.That(report.TotalProjects).IsGreaterThan(0);
    await Assert.That(report.Projects).IsNotEmpty();
    
    var projectNames = report.Projects.Select(p => p.Name).ToList();
    await Assert.That(projectNames).Contains("StructuraLens.Core");
    await Assert.That(projectNames).Contains("StructuraLens.Cli");
}
```

#### Thread-Safety Tests

Verify concurrent access patterns:

```csharp
[Test]
public async Task ParallelAdd_ThreadSafe()
{
    // Arrange
    using var collector = new InMemoryDependencyCollector();
    var tasks = new List<Task>();
    
    // Act - 100 parallel tasks, each adding 1000 edges
    for (int i = 0; i < 100; i++)
    {
        tasks.Add(Task.Run(() =>
        {
            for (int j = 0; j < 1000; j++)
            {
                collector.AddDependency(
                    new DependencyEdge("A", "B", DependencyType.TypeReference, 1));
            }
        }));
    }
    
    await Task.WhenAll(tasks);
    var result = collector.GetAggregatedDependencies();
    
    // Assert - Should aggregate to single edge with count 100,000
    await Assert.That(result).HasCount().EqualTo(1);
    await Assert.That(result[0].ReferenceCount).IsEqualTo(100_000);
}
```

### Test Conventions

**Naming:**
- Test classes: `{ClassName}Tests`
- Test methods: `{MethodName}_{Scenario}_{ExpectedResult}`

**Example:**
```csharp
Calculate_EmptyMethod_ReturnsOne
AnalyzeSolutionAsync_NonExistentFile_ThrowsFileNotFoundException
```

**Attributes:**
- `[Test]` - Marks test methods
- `[Before(Test)]` - Setup runs before each test
- `[After(Test)]` - Cleanup runs after each test

**Assertions:**
Fluent `await Assert.That()` pattern:

```csharp
await Assert.That(result).IsNotNull();
await Assert.That(count).IsGreaterThan(0);
await Assert.That(list).Contains("item");
await Assert.That(value).IsEqualTo(expected);
```

**Exception Assertions:**
```csharp
// Async
await Assert.ThrowsAsync<FileNotFoundException>(
    async () => await analyzer.AnalyzeSolutionAsync("/nonexistent"));

// Sync
Assert.Throws<ArgumentNullException>(() => new Service(null!));
```

### Test Data Strategies

#### Raw String Literals

Used extensively for code samples:

```csharp
var code = """
    public class TestClass
    {
        public void Method(bool condition)
        {
            if (condition) { }
        }
    }
    """;
```

#### In-Memory Roslyn Compilations

Test without file I/O:

```csharp
var tree = CSharpSyntaxTree.ParseText(code);
var compilation = CSharpCompilation.Create("TestAssembly")
    .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
    .AddSyntaxTrees(tree);
var semanticModel = compilation.GetSemanticModel(tree);
```

#### Test Builders

Reduce duplication with helper methods:

```csharp
private static SolutionAnalyzer CreateAnalyzer()
{
    return new SolutionAnalyzer(
        new NullLogger<SolutionAnalyzer>(),
        new NuGetRestorer(new NullLogger<NuGetRestorer>()),
        new MSBuildWorkspaceFactory(new MSBuildRegistrationService()),
        new CouplingAnalyzer(new NullLogger<CouplingAnalyzer>()),
        new MetricsCalculator(),
        new FileSystemService());
}
```

### Mocking Strategy

**FakeItEasy** for interface mocking:

```csharp
// Create fake
var fileSystem = A.Fake<IFileSystemService>();

// Setup return value
A.CallTo(() => fileSystem.FileExists(A<string>._))
    .Returns(true);

// Verify call
A.CallTo(() => fileSystem.FileExists("/path/to/file"))
    .MustHaveHappenedOnceExactly();
```

**Minimal mocking philosophy:**
- Only mock interfaces
- Prefer real objects when practical
- Integration tests use zero mocks

### Test Coverage

**Comprehensive scenario coverage:**
1. Happy path - Normal operation
2. Edge cases - Empty collections, null values, boundaries
3. Error cases - Invalid input, exceptions, cancellation
4. Behavioral variations - Different dependency types, parallel ops
5. Integration scenarios - End-to-end with real dependencies

## Extension Points

While StructuraLens doesn't currently have a formal plugin system, several extension points exist for future enhancements:

### 1. Custom Metric Calculators

Add new metrics by implementing calculator classes following the existing pattern:

```csharp
public static class CustomMetricCalculator
{
    public static double Calculate(IMethodSymbol method, SemanticModel semanticModel)
    {
        // Custom metric logic
        return value;
    }
}
```

Then integrate into `MetricsCalculator`.

### 2. Additional Output Formats

Implement new report formats by:
1. Creating exporter class (e.g., `XmlReportExporter`)
2. Adding format option to CLI (`--format xml`)
3. Wiring up in command handler

### 3. Custom Dependency Collectors

Implement `IDependencyCollector` for alternative storage strategies:
- Redis-backed collector
- Cloud storage collector
- Custom aggregation algorithms

### 4. Roslyn Analyzer Integration (Planned)

Future enhancement to load custom Roslyn analyzers:

```bash
structuralens analyze MySolution.sln --analyzers ./my-analyzers
```

Would require:
- Analyzer assembly loading
- Integration with `CompilationWithAnalyzers`
- Diagnostic collection from external analyzers

### 5. Metrics Thresholds and Quality Gates (Planned)

Future configuration for quality gates:

```json
{
  "thresholds": {
    "cyclomaticComplexity": { "error": 20, "warning": 10 },
    "maintainabilityIndex": { "error": 20, "warning": 40 }
  }
}
```

Would enable failing CI builds based on metric thresholds.

## Architectural Principles

### SOLID Principles

**Single Responsibility**
- Each component has one well-defined purpose
- Calculators focus on single metrics
- Collectors focus on aggregation strategy

**Open/Closed**
- Extensible via new calculators, collectors, exporters
- Closed for modification (existing code stable)

**Liskov Substitution**
- All `IDependencyCollector` implementations interchangeable
- Collectors can be swapped without changing `SolutionAnalyzer`

**Interface Segregation**
- Focused interfaces (`ISolutionAnalyzer`, `IMetricsCalculator`, etc.)
- Clients depend only on methods they use

**Dependency Inversion**
- High-level components depend on abstractions
- `SolutionAnalyzer` depends on `INuGetRestorer`, not `NuGetRestorer`

### Clean Architecture

**Layering:**
- **Domain Models** - Core entities (`AnalysisReport`, `MethodMetrics`, etc.)
- **Business Logic** - Analysis, calculations, coupling
- **Infrastructure** - MSBuild, NuGet, file system
- **Presentation** - CLI, output formatting

**Dependencies flow inward:**
- Core has zero dependency on CLI
- Core has zero dependency on DI container
- Infrastructure implements core interfaces

### Performance Considerations

**Memory Efficiency:**
- Deduplication in collectors reduces memory by 50-95%
- Adaptive strategy prevents OOM on large solutions
- Batched writes minimize memory spikes

**CPU Efficiency:**
- Roslyn semantic models cached by workspace
- Concurrent dictionary enables parallel analysis
- Static calculators avoid object allocation

**I/O Efficiency:**
- SQLite batching reduces disk writes
- Transactions group related operations
- Temporary database deleted after analysis

## Debugging Tips

### Enable Verbose Logging

```bash
structuralens analyze MySolution.sln --verbose
```

Shows:
- MSBuild workspace diagnostics
- NuGet restore details
- Analysis progress per project
- Memory usage and collector migrations

### Attach Debugger

```bash
# In Visual Studio / Rider
dotnet run --project src/StructuraLens.Cli -- analyze MySolution.sln

# Set breakpoint in SolutionAnalyzer.AnalyzeSolutionAsync
```

### Test Specific Component

Run unit tests for isolated components:

```bash
dotnet test --filter "FullyQualifiedName~CyclomaticComplexityCalculatorTests"
```

### Self-Analysis

Run StructuraLens against itself:

```bash
dotnet run --project src/StructuraLens.Cli -- analyze StructuraLens.slnx --format summary
```

Validates tool works on real codebase.

## Further Reading

- [Usage Guide](usage.md) - CLI reference and examples
- [Design Document](design.md) - High-level architecture and decisions
- [Development Guide](development.md) - Contributor workflows
- [Compact Format Specification](compact-format.md) - Output format details
