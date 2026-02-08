# StructuraLens Compact Report Format (.slr)

The StructuraLens Report (`.slr`) format is a compact JSON-based format optimized for machine parsing, storage efficiency, and visualization. It is designed to be consumed by dashboards, CI/CD pipelines, and trend-tracking systems.

## File Extension

**`.slr`** - StructuraLens Report

Example: `analysis-2026-01-10.slr`

## Format Overview

The compact format achieves ~99% size reduction compared to the full JSON format by:
- Using short property names (1-3 characters)
- Omitting redundant computed fields
- Using arrays instead of objects for graph data
- Using Unix timestamps instead of ISO strings
- Excluding detailed method-level data by default

## Schema

### Root Object

```json
{
  "v": 1,
  "p": "/path/to/Solution.sln",
  "t": 1768063200000,
  "prj": [...],
  "g": {...}
}
```

| Field | Type | Description |
|-------|------|-------------|
| `v` | `int` | Format version (currently `1`) |
| `p` | `string` | Absolute path to analyzed solution/project |
| `t` | `long` | Analysis timestamp (Unix milliseconds UTC) |
| `prj` | `array` | Project metrics array |
| `g` | `object` | Graph data for visualization |

### Project Metrics (`prj[]`)

Each project is represented as an object with aggregated metrics:

```json
{
  "n": "MyApp.Core",
  "tc": 25,
  "mc": 150,
  "cc": 450,
  "loc": 1200,
  "dit": 3,
  "mi": 72.5,
  "ce": 5,
  "ca": 12,
  "i": 0.29,
  "err": 0,
  "warn": 2
}
```

| Field | Type | Description |
|-------|------|-------------|
| `n` | `string` | Project name |
| `tc` | `int` | Type count (classes, structs, records, etc.) |
| `mc` | `int` | Method count |
| `cc` | `int` | Total cyclomatic complexity |
| `loc` | `int` | Total lines of executable code |
| `dit` | `int` | Maximum depth of inheritance |
| `mi` | `double` | Average maintainability index (0-100) |
| `id` | `int` | Internal dependencies (depends on other projects in solution) |
| `idx` | `int` | Internal dependents (other projects that depend on this) |
| `dr` | `double` | Dependency ratio: id/(id+idx), range 0-1 where 0=provider, 1=consumer |
| `ed` | `int` | Total external dependencies (BCL + packages) |
| `edb` | `int` | External BCL dependencies (System.*, Microsoft.*) |
| `edp` | `int` | External package dependencies (third-party) |
| `err` | `int` | Compiler error count (omitted if 0) |
| `warn` | `int` | Compiler warning count (omitted if 0) |

#### Optional: Type Details (`types`)

When exported with `includeTypeDetails: true`:

```json
{
  "n": "MyApp.Core",
  "tc": 25,
  ...
  "types": [
    {
      "n": "UserService",
      "dit": 2,
      "cc": 45,
      "loc": 120,
      "mi": 68.5
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `n` | `string` | Type name (without namespace) |
| `dit` | `int` | Depth of inheritance |
| `cc` | `int` | Total cyclomatic complexity |
| `loc` | `int` | Lines of executable code |
| `mi` | `double` | Average maintainability index |
| `m` | `array?` | Method details (if `includeMethodDetails`) |

#### Optional: Method Details (`m`)

When exported with `includeMethodDetails: true`:

```json
{
  "n": "GetUser(int)",
  "cc": 5,
  "loc": 15,
  "hv": 125.5,
  "mi": 65.0,
  "sl": 10,
  "el": 25
}
```

| Field | Type | Description |
|-------|------|-------------|
| `n` | `string` | Method name with parameters (without namespace/type) |
| `cc` | `int` | Cyclomatic complexity |
| `loc` | `int` | Lines of executable code |
| `hv` | `double` | Halstead volume |
| `mi` | `double` | Maintainability index |
| `sl` | `int` | Start line number |
| `el` | `int` | End line number |

### Graph Data (`g`)

The graph section contains pre-processed data for rendering dependency visualizations (e.g., d3.js force-directed graphs). Only **internal dependencies** are included (no System.*, Microsoft.*, etc.).

```json
{
  "g": {
    "p": {
      "n": [[0, "MyApp.Core", 1200], [1, "MyApp.Api", 800]],
      "e": [[1, 0, 1]]
    },
    "ns": {
      "n": [[0, "MyApp.Core.Services", 500], [1, "MyApp.Core.Models", 300]],
      "e": [[0, 1, 5]]
    }
  }
}
```

#### Graph Layers

| Field | Description |
|-------|-------------|
| `g.p` | Project-level dependency graph |
| `g.ns` | Namespace-level dependency graph |

#### Nodes (`n`)

**Project nodes** (in `g.p.n`): Array of tuples `[id, name, loc]`

| Index | Type | Description |
|-------|------|-------------|
| 0 | `int` | Unique node ID (0-indexed) |
| 1 | `string` | Project name |
| 2 | `int` | Lines of code (for bubble sizing) |

**Namespace nodes** (in `g.ns.n`): Array of tuples `[id, name, loc, cc, mi, tc, mc, id, idx, dr, ed]`

| Index | Type | Description |
|-------|------|-------------|
| 0 | `int` | Unique node ID (0-indexed) |
| 1 | `string` | Namespace name |
| 2 | `int` | Lines of code |
| 3 | `int` | Cyclomatic complexity |
| 4 | `double` | Maintainability index |
| 5 | `int` | Type count |
| 6 | `int` | Method count |
| 7 | `int` | Internal dependencies |
| 8 | `int` | Internal dependents |
| 9 | `double` | Dependency ratio (0-1) |
| 10 | `int` | External dependencies |

#### Edges (`e`)

Array of edge tuples: `[sourceId, targetId, weight]`

| Index | Type | Description |
|-------|------|-------------|
| 0 | `int` | Source node ID |
| 1 | `int` | Target node ID |
| 2 | `int` | Edge weight (reference count) |

### Linting Results

**Note:** Architecture linting is not currently implemented in StructuraLens. This section is reserved for future enhancement.

## Example: Complete Report

```json
{
  "v": 1,
  "p": "/home/dev/MyApp/MyApp.sln",
  "t": 1768063200000,
  "prj": [
    {"n":"MyApp.Core","tc":25,"mc":150,"cc":450,"loc":1200,"dit":3,"mi":72.5,"id":5,"idx":12,"dr":0.29,"ed":20,"edb":15,"edp":5},
    {"n":"MyApp.Api","tc":10,"mc":60,"cc":180,"loc":500,"dit":1,"mi":68.0,"id":8,"idx":0,"dr":1.0,"ed":30,"edb":25,"edp":5}
  ],
  "g": {
    "p": {
      "n": [[0,"MyApp.Core",1200],[1,"MyApp.Api",500]],
      "e": [[1,0,1]]
    },
    "ns": {
      "n": [[0,"MyApp.Core.Services",400],[1,"MyApp.Core.Models",300],[2,"MyApp.Api.Controllers",500]],
      "e": [[0,1,12],[2,0,8]]
    }
  }
}
```

## Parsing Examples

### TypeScript/JavaScript

```typescript
interface SlrReport {
  v: number;
  p: string;
  t: number;
  prj: Array<{
    n: string;
    tc: number;
    mc: number;
    cc: number;
    loc: number;
    dit: number;
    mi: number;
    ce: number;
    ca: number;
    i: number;
  }>;
  g: {
    p: { n: [number, string, number][]; e: [number, number, number][] };
    ns: { n: [number, string, number][]; e: [number, number, number][] };
  };
}

// Parse report
const report: SlrReport = JSON.parse(fileContent);

// Convert timestamp
const analyzedAt = new Date(report.t);

// Build d3.js graph data
const nodes = report.g.p.n.map(([id, name, size]) => ({ id, name, size }));
const links = report.g.p.e.map(([source, target, weight]) => ({ source, target, weight }));
```

### C#

```csharp
using System.Text.Json;

var report = JsonSerializer.Deserialize<CompactReport>(json);
var analyzedAt = DateTimeOffset.FromUnixTimeMilliseconds(report.Timestamp);

// Access project metrics
foreach (var project in report.Projects)
{
    Console.WriteLine($"{project.Name}: CC={project.CyclomaticComplexity}, MI={project.AvgMaintainabilityIndex}");
}
```

### Python

```python
import json
from datetime import datetime

with open("report.slr") as f:
    report = json.load(f)

analyzed_at = datetime.fromtimestamp(report["t"] / 1000)

# Build NetworkX graph
import networkx as nx

G = nx.DiGraph()
for id, name, size in report["g"]["p"]["n"]:
    G.add_node(id, name=name, size=size)
for source, target, weight in report["g"]["p"]["e"]:
    G.add_edge(source, target, weight=weight)
```

## d3.js Visualization Example

```javascript
// Load the report
const report = await fetch('report.slr').then(r => r.json());

// Prepare data for force-directed graph
const nodes = report.g.p.n.map(([id, name, size]) => ({
  id,
  name,
  radius: Math.sqrt(size) / 2  // Size circles by LOC
}));

const links = report.g.p.e.map(([source, target, weight]) => ({
  source,
  target,
  value: weight
}));

// Create force simulation
const simulation = d3.forceSimulation(nodes)
  .force("link", d3.forceLink(links).id(d => d.id))
  .force("charge", d3.forceManyBody().strength(-200))
  .force("center", d3.forceCenter(width / 2, height / 2));

// Render circles and links...
```

## Version History

| Version | Changes |
|---------|---------|
| 1 | Initial format |

## MIME Type

Recommended: `application/vnd.structuralens+json`

## Generating Reports

```bash
# Generate compact report
structuralens analyze MySolution.sln --format compact --out report.slr

# Generate with type details
structuralens analyze MySolution.sln --format compact --out report.slr
# (Use API for type/method detail options)
```

## Best Practices

1. **Store reports with timestamps** in filenames for trend analysis:
   ```
   reports/2026-01-10T12-00-00.slr
   reports/2026-01-11T12-00-00.slr
   ```

2. **Compress for long-term storage**: The format compresses well with gzip (~80% additional reduction).

3. **Use project-level metrics for dashboards**: The `prj[]` array contains all metrics needed for summary tables.

4. **Use graph data for visualizations**: The `g.p` and `g.ns` sections are pre-indexed for direct use with graph libraries.
