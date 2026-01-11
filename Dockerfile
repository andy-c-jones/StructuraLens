# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy everything and publish
COPY . .
RUN dotnet publish src/StructuraLens.Cli/StructuraLens.Cli.csproj \
    -c Release \
    -r linux-musl-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o /app/publish

# Manually copy BuildHost files from NuGet cache (required for MSBuildWorkspace)
RUN find /root/.nuget/packages/microsoft.codeanalysis.workspaces.msbuild -name "BuildHost-*" -type d | head -1 | xargs -I {} cp -r {} /app/publish/ || true

# Runtime stage - use Alpine SDK for smaller size
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS runtime
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["/app/StructuraLens.Cli"]
