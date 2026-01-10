# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy everything and publish
COPY . .
RUN dotnet publish src/StructuraLens.Cli/StructuraLens.Cli.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o /app/publish

# Runtime stage - use SDK image since MSBuildWorkspace needs dotnet
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["/app/StructuraLens.Cli"]
