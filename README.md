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

Private NuGet Feeds:
The container includes the Azure Artifacts Credential Provider and supports multiple authentication methods:

**Option 1: Static credentials in nuget.config (simplest)**
The scripts automatically mount your local NuGet configuration:
- Linux/macOS: Mounts `~/.nuget/NuGet/` or `~/.nuget/NuGet.Config`
- Windows: Mounts `%APPDATA%\NuGet\`

Configure credentials on your host:
```bash
dotnet nuget add source <url> -n <name> -u <user> -p <password> --store-password-in-clear-text
```

**Option 2: Azure Artifacts / credential provider with PAT (recommended for CI)**
Set environment variables before running the scripts:
```bash
# Linux/macOS
export NUGET_FEED_URL="https://pkgs.dev.azure.com/ORG/PROJECT/_packaging/FEED/nuget/v3/index.json"
export NUGET_PAT="your-personal-access-token"
./run-local.sh MySolution.sln

# Windows PowerShell
$env:NUGET_FEED_URL = "https://pkgs.dev.azure.com/ORG/PROJECT/_packaging/FEED/nuget/v3/index.json"
$env:NUGET_PAT = "your-personal-access-token"
.\run-local-windows.ps1 -SolutionPath MySolution.sln
```

**Option 3: Manual docker run with credentials**
```bash
docker run --rm \
  -v "$PWD:/workspace:Z" \
  -v "$HOME/.nuget/packages:/root/.nuget/packages:ro,Z" \
  -e VSS_NUGET_EXTERNAL_FEED_ENDPOINTS='{"endpointCredentials": [{"endpoint":"<feed-url>", "username":"docker", "password":"<pat>"}]}' \
  -w /workspace \
  structura-lens:local analyze MySolution.sln --format html --out report.html
```

Conventional Commits & Contribution Guidelines
- Work on feature branches named `feature/<short-description>` or `fix/<short-description>`.
- Open a pull request against `main` for all changes and use Conventional Commits for PR titles. PRs will be squashed on merge so the PR title becomes the commit message used by semantic-release.

Conventional Commit examples:
- feat: add new analysis rule for controller coupling
- fix: handle null reference in parser when project file missing
- chore: update dependencies
- docs: clarify README examples

Use the Copilot instructions in .github/COPILOT_INSTRUCTIONS.md for quick reference.