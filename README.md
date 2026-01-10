# StructuraLens C# runner

Proprietary .NET 10 CLI for static C# code analysis, coupling metrics, and architecture linting.

It supports json and html report output.

Prerequisites
- .NET 10 SDK

Build: `dotnet build`
Run tests: `dotnet test`

Running locally with Docker

- Linux / macOS (use run-local.sh):

  ./run-local.sh [path-to-solution-or-project] [output-file.html]

  Example: ./run-local.sh StructuraLens.slnx structuralens-report.html

- Windows (PowerShell - see run-local-windows.ps1):

  .\run-local-windows.ps1 -SolutionPath <path> -Output <output-file.html>

  Example: .\run-local-windows.ps1 -SolutionPath StructuraLens.slnx -Output structuralens-report.html

Notes:
- Both scripts mount your local NuGet cache (if present) to speed up analysis and avoid restoring packages repeatedly.
- The scripts build the Docker image and run the analyzer inside the container, producing an HTML report in the repository root.

Conventional Commits & Contribution Guidelines
- Work on feature branches named `feature/<short-description>` or `fix/<short-description>`.
- Open a pull request against `main` for all changes and use Conventional Commits for PR titles. PRs will be squashed on merge so the PR title becomes the commit message used by semantic-release.

Conventional Commit examples:
- feat: add new analysis rule for controller coupling
- fix: handle null reference in parser when project file missing
- chore: update dependencies
- docs: clarify README examples

Use the Copilot instructions in .github/COPILOT_INSTRUCTIONS.md for quick reference.