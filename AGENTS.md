# AGENTS.md

Purpose: onboarding notes for agentic coding tools working in this repo.
Keep this file concise, actionable, and aligned with existing docs.

## Repository Overview
- StructuraLens is a .NET 10 CLI tool for analyzing C# codebases.
- Core logic lives in `src/StructuraLens.Core`; CLI wrapper in `src/StructuraLens.Cli`.
- Tests live in `tests/StructuraLens.Tests` and use TUnit.
- HTML report UI lives in `web/` (Astro + TypeScript + D3).

## Required Rules (Copilot)
Source: `.github/copilot-instructions.md`
- Work on feature branches: `feature/<short-description>` or `fix/<short-description>`.
- Open PRs against `main`.
- Use Conventional Commits for commit messages and PR titles.
- PRs are squashed on merge; PR title becomes release note entry.

## Build / Lint / Test Commands
There is no separate linter configured; rely on `dotnet build` and tests.

### Restore
- `dotnet restore`

### Build
- Debug build: `dotnet build`
- Release build: `dotnet build -c Release`
- Build specific project: `dotnet build src/StructuraLens.Core`
- Build specific project: `dotnet build src/StructuraLens.Cli`

### Test (All)
- `dotnet test`

### Test (Single Test Class)
- `dotnet test --filter "FullyQualifiedName~CyclomaticComplexityCalculatorTests"`

### Test (Single Test Method)
- `dotnet test --filter "FullyQualifiedName~Calculate_EmptyMethod_ReturnsOne"`

### Test (Category Filters)
- Integration only: `dotnet test --filter "FullyQualifiedName~IntegrationTests"`
- Exclude integration: `dotnet test --filter "FullyQualifiedName!~IntegrationTests"`

### Test (Watch)
- `dotnet watch test`

### Run CLI Locally
- `dotnet run --project src/StructuraLens.Cli -- analyze StructuraLens.slnx --format summary`
- `dotnet run --project src/StructuraLens.Cli -- analyze <path.sln> --format html --out report.html`

### Web / HTML Report
- `dotnet build` auto-runs `npm run build` in `web/` via MSBuild target. Skip with `-p:SkipWebBuild=true`.
- Dev server: `cd web && npm install && npm run dev` (serves at localhost:4321 with test data).
- Template uses `{{PLACEHOLDER}}` tokens in prod; `HtmlReportGenerator.cs` replaces them at runtime.

## Code Style Guidelines
Follow existing code in `src/StructuraLens.Core` and `src/StructuraLens.Cli`.

### Language and Project Settings
- C# 13 / .NET 10.
- Nullable reference types enabled.
- Implicit usings enabled.
- File-scoped namespaces preferred.

### Imports (Usings)
- Order:
  1. `System.*`
  2. `Microsoft.*`
  3. Third-party
  4. Project namespaces
- Avoid unused usings; remove when not needed.

### Formatting
- Use standard C# formatting; prefer existing style in nearby files.
- Braces on new lines for types and members.
- Keep methods short and focused; extract helpers if complex.
- Prefer raw string literals for multiline C# code snippets in tests.

### Types and Design
- Favor explicit types when clarity matters; otherwise `var` is fine for obvious types.
- Prefer `record`/`record struct` for immutable data objects.
- Use `sealed` on classes that are not intended to be extended.
- Keep core library independent from CLI and System.CommandLine dependencies.

### Naming Conventions
- Interfaces: `IThing` (e.g., `ISolutionAnalyzer`).
- Classes: `Thing` (e.g., `SolutionAnalyzer`).
- Private fields: `_logger`, `_options`.
- Async methods end with `Async`.
- Tests: `{MethodName}_{Scenario}_{ExpectedResult}`.

### Dependency Injection
- Constructor injection only.
- Service interfaces live in `src/StructuraLens.Core/Abstractions`.
- Register services in `src/StructuraLens.Cli/Program.cs`.
- All services are singletons (CLI runs once per invocation).

### Logging
- Use source-generated logging (`[LoggerMessage]`).
- Event ID ranges:
  - 4000-4099: CLI operations
  - 4100-4199: Warnings
  - 4200-4299: Errors

### Error Handling
- Fail fast for programmer errors:
  - `ArgumentNullException.ThrowIfNull(...)`
- Log and recover from expected environmental failures where possible.
- Do not swallow exceptions without logging context.

### Performance and Resource Handling
- Dispose `IDisposable` resources via `using`.
- Avoid sync-over-async; use `await`.
- Prefer single-pass analyses when possible (see `UnifiedMetricsCalculator`).

### Tests
- Framework: TUnit.
- Use `await Assert.That(...)` fluent assertions.
- Keep tests self-contained; use raw strings for code fixtures.
- Use FakeItEasy for mocks when needed.

## Project Structure Quick Reference
```
src/StructuraLens.Core/   # Core logic (analysis, export, models, infra)
src/StructuraLens.Cli/    # CLI wrapper and DI
tests/StructuraLens.Tests/# TUnit tests
web/                     # Astro project → single-file HTML report template
docs/                    # Design, architecture, usage
```

## Conventional Commits (Required)
Format: `<type>: <description>`
Types: `feat`, `fix`, `chore`, `docs`, `test`, `refactor`, `perf`.
Example: `fix: handle null reference in coupling analyzer`.

## Notes for Agents
- Prefer minimal, targeted changes with tests.
- Match existing patterns in the area you edit.
- If unsure, check `docs/development.md` and `.github/copilot-instructions.md`.
