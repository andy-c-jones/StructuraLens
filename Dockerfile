# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for restore
COPY StructuraLens.slnx ./
COPY src/StructuraLens.Cli/StructuraLens.Cli.csproj src/StructuraLens.Cli/
COPY src/StructuraLens.Core/StructuraLens.Core.csproj src/StructuraLens.Core/
COPY tests/StructuraLens.Tests/StructuraLens.Tests.csproj tests/StructuraLens.Tests/

# Restore packages
RUN dotnet restore

# Copy all source code
COPY . .

# Build release
RUN dotnet build -c Release --no-restore

# Run tests
RUN dotnet test -c Release --no-build --verbosity normal

# Publish self-contained for linux-x64 (no AOT)
RUN dotnet publish src/StructuraLens.Cli/StructuraLens.Cli.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o /app/publish

# Runtime stage - use runtime-deps for self-contained apps
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Set entrypoint
ENTRYPOINT ["./StructuraLens.Cli"]
