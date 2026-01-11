#!/usr/bin/env pwsh
param()

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ImageName = "structura-lens:local"

Write-Host "Building Docker container image: $ImageName" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

# Check for Docker
if (Get-Command docker -ErrorAction SilentlyContinue) {
    $ContainerCmd = "docker"
} else {
    Write-Host "Error: Docker not found. Please install Docker Desktop." -ForegroundColor Red
    exit 1
}

Write-Host "Using container runtime: $ContainerCmd"
& $ContainerCmd build -t $ImageName -f "$ScriptDir\Dockerfile" $ScriptDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "✓ Build complete! Image tagged as: $ImageName" -ForegroundColor Green
Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "USAGE GUIDE: Running StructuraLens with Docker" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Initialize a new .slnx file from existing solutions:" -ForegroundColor Yellow
Write-Host "   docker run --rm -v `"`${PWD}:/workspace`" -w /workspace $ImageName init"
Write-Host ""
Write-Host "2. Analyze a solution and generate HTML report:" -ForegroundColor Yellow
Write-Host "   docker run --rm -v `"`${PWD}:/workspace`" -w /workspace $ImageName analyze YourSolution.slnx --format html --out report.html"
Write-Host ""
Write-Host "3. Analyze with JSON output:" -ForegroundColor Yellow
Write-Host "   docker run --rm -v `"`${PWD}:/workspace`" -w /workspace $ImageName analyze YourSolution.slnx --format json --out report.json"
Write-Host ""
Write-Host "4. With NuGet cache mounting (faster restores):" -ForegroundColor Yellow
Write-Host "   docker run --rm ``"
Write-Host "     -v `"`${PWD}:/workspace`" ``"
Write-Host "     -v `"`${env:USERPROFILE}\.nuget\packages:/root/.nuget/packages:ro`" ``"
Write-Host "     -w /workspace ``"
Write-Host "     $ImageName analyze YourSolution.slnx --format html --out report.html"
Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "NOTES:" -ForegroundColor Cyan
Write-Host "- Current directory is mounted to /workspace in the container"
Write-Host "- All paths should be relative to your current directory"
Write-Host "- Use PowerShell variable `${PWD} for current directory"
Write-Host "- Mounting NuGet cache speeds up package restores"
Write-Host "================================================" -ForegroundColor Cyan
