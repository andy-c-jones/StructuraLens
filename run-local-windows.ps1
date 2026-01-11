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

# Mount NuGet config for private feed authentication
$nugetConfigMount = ""
$nugetConfigDir = "$env:APPDATA\NuGet"
if (Test-Path $nugetConfigDir) {
  $nugetConfigMount = "-v `"$nugetConfigDir`":/root/.nuget/NuGet:ro"
  Write-Host "Mounting NuGet config: $nugetConfigDir"
}

# Mount credential provider plugins from host if available
$credProviderMount = ""
$credProviderDir = "$env:USERPROFILE\.nuget\plugins"
if (Test-Path $credProviderDir) {
  $credProviderMount = "-v `"$credProviderDir`":/root/.nuget/plugins:ro"
  Write-Host "Mounting NuGet credential providers: $credProviderDir"
}

# Pass credentials for Azure Artifacts Credential Provider if set
$credentialEnv = ""
if ($env:NUGET_PAT) {
  Write-Host "Using NUGET_PAT for private feed authentication"
  if ($env:NUGET_FEED_URL) {
    $endpoints = "{`"endpointCredentials`": [{`"endpoint`":`"$env:NUGET_FEED_URL`", `"username`":`"docker`", `"password`":`"$env:NUGET_PAT`"}]}"
    $credentialEnv = "-e VSS_NUGET_EXTERNAL_FEED_ENDPOINTS=`"$endpoints`""
  }
}

# Run the analysis container
$cmd = "docker run --rm -v `"$pwd`":/workspace -w /workspace $nugetMount $nugetConfigMount $credProviderMount $credentialEnv structura-lens:local /app/StructuraLens.Cli analyze `"$SolutionPath`" -f html -o `"$Output`""
Write-Host "Running: $cmd"
Invoke-Expression $cmd

Write-Host "Report written to: $Output"