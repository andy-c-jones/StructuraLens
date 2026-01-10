# StructuraLens Configuration Reference

StructuraLens uses JSON configuration files (`structuralens.json`) to customize analysis behavior. Configuration supports hierarchical inheritance, allowing solution-level defaults with project-level overrides.

## Configuration File Discovery

StructuraLens automatically discovers configuration files by:

1. Starting from the solution/project directory
2. Walking up the directory tree toward the root
3. Loading each `structuralens.json` found along the way
4. Merging configurations (parent-first, child overrides)

### Example Directory Structure

```
/repo
├── structuralens.json          # Solution-level config (loaded first)
├── MySolution.sln
├── src/
│   ├── MyProject.Api/
│   │   ├── structuralens.json  # Project-level override (loaded last)
│   │   └── MyProject.Api.csproj
│   └── MyProject.Core/
│       └── MyProject.Core.csproj
```

## Configuration Schema

### Complete Example

```json
{
  "$schema": "https://raw.githubusercontent.com/your-org/structuralens/main/docs/config.schema.json",
  "inheritanceDepth": 10,
  "coupling": {
    "mode": "filtered",
    "excludePatterns": [
      "System.*",
      "Microsoft.*",
      "Newtonsoft.Json*"
    ],
    "includePatterns": [],
    "patternType": "wildcard",
    "trackExternalDependencies": true,
    "groupByAssembly": false
  },
  "metrics": {
    "includeTests": true,
    "excludeGenerated": true
  },
  "output": {
    "includeSourceLocations": false,
    "maxDependenciesInSummary": 10
  },
  "rules": []
}
```

## Configuration Options

### Root Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `$schema` | string | - | JSON Schema reference for IDE support |
| `inheritanceDepth` | integer | 10 | Maximum depth for configuration inheritance (1-50) |
| `coupling` | object | - | Coupling analysis configuration |
| `metrics` | object | - | Metrics calculation configuration |
| `output` | object | - | Output configuration |
| `rules` | array | [] | Architecture linting rules (future) |

### Coupling Configuration

The `coupling` object controls how dependency analysis is performed.

```json
{
  "coupling": {
    "mode": "filtered",
    "excludePatterns": ["System.*", "Microsoft.*"],
    "includePatterns": [],
    "patternType": "wildcard",
    "trackExternalDependencies": true,
    "groupByAssembly": false
  }
}
```

#### coupling.mode

Controls which dependencies are tracked.

| Value | Description |
|-------|-------------|
| `internal` | Only track dependencies between your own code |
| `filtered` | Track external dependencies but apply exclude/include patterns (default) |
| `all` | Track all dependencies regardless of source |

#### coupling.excludePatterns

Array of patterns to exclude from coupling analysis. Only applies when `mode` is `filtered`.

```json
{
  "coupling": {
    "excludePatterns": [
      "System.*",
      "Microsoft.*",
      "*.Tests",
      "Moq*"
    ]
  }
}
```

Default patterns: `["System.*", "Microsoft.*"]`

#### coupling.includePatterns

Array of patterns to explicitly include. **Overrides excludePatterns** — if any include pattern matches, the dependency is included regardless of exclude patterns.

```json
{
  "coupling": {
    "excludePatterns": ["System.*"],
    "includePatterns": ["System.Text.Json"]
  }
}
```

This would exclude all `System.*` namespaces except `System.Text.Json`.

#### coupling.patternType

How patterns are interpreted.

| Value | Description | Example |
|-------|-------------|---------|
| `wildcard` | Wildcard matching with `*` and `?` (default) | `System.*` matches `System.Linq` |
| `regex` | Regular expression matching | `^System\..*` |
| `exact` | Exact string matching | `System.Linq` |

#### coupling.trackExternalDependencies

Whether to include external (non-source) dependencies. Default: `true`.

#### coupling.groupByAssembly

When `true`, groups external dependencies by assembly rather than namespace. Default: `false`.

### Metrics Configuration

```json
{
  "metrics": {
    "includeTests": true,
    "excludeGenerated": true
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `includeTests` | boolean | true | Include test projects in metrics |
| `excludeGenerated` | boolean | true | Exclude generated code from metrics |

### Output Configuration

```json
{
  "output": {
    "includeSourceLocations": false,
    "maxDependenciesInSummary": 10
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `includeSourceLocations` | boolean | false | Include source file locations in dependency output |
| `maxDependenciesInSummary` | integer | 10 | Maximum dependencies shown in summary output |

## Configuration Inheritance

When multiple configuration files are found, they are merged with the following rules:

### Scalar Values (strings, numbers, booleans)
- Child value overrides parent value
- If child doesn't specify a value, parent value is used

### Arrays (excludePatterns, includePatterns, rules)
- Arrays are **concatenated** (parent first, then child)
- Duplicate values are removed

### Example: Inheritance

**Solution-level config** (`/repo/structuralens.json`):
```json
{
  "coupling": {
    "mode": "filtered",
    "excludePatterns": ["System.*", "Microsoft.*"]
  }
}
```

**Project-level config** (`/repo/src/MyProject.Api/structuralens.json`):
```json
{
  "coupling": {
    "excludePatterns": ["Swashbuckle.*"]
  }
}
```

**Effective config for MyProject.Api**:
```json
{
  "coupling": {
    "mode": "filtered",
    "excludePatterns": ["System.*", "Microsoft.*", "Swashbuckle.*"]
  }
}
```

## Common Configuration Scenarios

### Minimal Configuration

For most projects, the defaults work well. Create an empty config to use defaults:

```json
{}
```

Or don't create a config file at all.

### Exclude Additional NuGet Packages

```json
{
  "coupling": {
    "excludePatterns": [
      "System.*",
      "Microsoft.*",
      "Newtonsoft.Json*",
      "Serilog*",
      "AutoMapper*",
      "FluentValidation*"
    ]
  }
}
```

### Internal-Only Analysis

Focus only on your own code:

```json
{
  "coupling": {
    "mode": "internal"
  }
}
```

### Include Specific Framework Namespaces

Track `System.Text.Json` while excluding other System namespaces:

```json
{
  "coupling": {
    "excludePatterns": ["System.*"],
    "includePatterns": ["System.Text.Json"]
  }
}
```

### Exclude Test Dependencies from Core Projects

In a test project, you might want to ignore test framework dependencies:

**`/repo/tests/MyProject.Tests/structuralens.json`**:
```json
{
  "coupling": {
    "excludePatterns": [
      "Xunit*",
      "Moq*",
      "FluentAssertions*",
      "NSubstitute*"
    ]
  }
}
```

### Using Regex Patterns

For complex matching requirements:

```json
{
  "coupling": {
    "patternType": "regex",
    "excludePatterns": [
      "^System\\..*",
      "^Microsoft\\..*",
      ".*\\.Tests$"
    ]
  }
}
```

### Complete Analysis (All Dependencies)

For auditing purposes:

```json
{
  "coupling": {
    "mode": "all"
  },
  "output": {
    "includeSourceLocations": true
  }
}
```

## CLI Override

CLI options override configuration file settings:

```bash
# Uses config file but overrides coupling mode
structuralens analyze MySolution.sln --config ./structuralens.json --coupling-mode internal
```

Priority (highest to lowest):
1. CLI arguments
2. Project-level config
3. Solution-level config
4. Default values

## Validating Configuration

Use the JSON Schema for IDE validation and autocompletion:

```json
{
  "$schema": "https://raw.githubusercontent.com/your-org/structuralens/main/docs/config.schema.json",
  "coupling": {
    "mode": "filtered"
  }
}
```

Most modern editors (VS Code, Rider, Visual Studio) will provide:
- Autocompletion for property names
- Validation of property values
- Documentation on hover
