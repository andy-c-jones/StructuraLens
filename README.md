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
