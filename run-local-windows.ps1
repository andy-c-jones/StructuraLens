param(
  [string]$SolutionPath = "StructuraLens.slnx",
  [string]$Output = "structuralens-report.html"
)

# Build Docker image
docker build -t structura-lens:local -f Dockerfile .

# Determine NuGet cache path for Windows
$nugetCache = "$env:USERPROFILE\.nuget\packages"
$nugetMount = ""
if (Test-Path $nugetCache) {
  # Docker on Windows expects absolute paths; use the Windows path
  $nugetMount = "-v `"$nugetCache`":/root/.nuget/packages:ro"
}

# Run the analysis container
$cmd = "docker run --rm -v `"$pwd`":/workspace -w /workspace $nugetMount structura-lens:local /app/StructuraLens.Cli analyze `"$SolutionPath`" -f html -o `"$Output`""
Write-Host "Running: $cmd"
Invoke-Expression $cmd

Write-Host "Report written to: $Output"