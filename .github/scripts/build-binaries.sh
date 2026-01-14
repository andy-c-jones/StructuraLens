#!/bin/bash
set -e

VERSION=$1

echo "Building binaries for version: $VERSION"

# Build for each platform
dotnet publish src/StructuraLens.Cli/StructuraLens.Cli.csproj \
  -c Release \
  -r linux-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/linux-arm64

dotnet publish src/StructuraLens.Cli/StructuraLens.Cli.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/linux-x64

dotnet publish src/StructuraLens.Cli/StructuraLens.Cli.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/osx-arm64

dotnet publish src/StructuraLens.Cli/StructuraLens.Cli.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/win-x64

# Create artifacts directory
mkdir -p artifacts

# Create archives with version in filename
echo "Creating release archives..."

cd publish/linux-arm64
tar -czf ../../artifacts/structuralens-linux-arm64-${VERSION}.tar.gz StructuraLens.Cli
cd ../linux-x64
tar -czf ../../artifacts/structuralens-linux-x64-${VERSION}.tar.gz StructuraLens.Cli
cd ../osx-arm64
tar -czf ../../artifacts/structuralens-macos-arm64-${VERSION}.tar.gz StructuraLens.Cli
cd ../win-x64
zip ../../artifacts/structuralens-windows-x64-${VERSION}.zip StructuraLens.Cli.exe
cd ../..

echo "Build complete! Artifacts:"
ls -lh artifacts/
