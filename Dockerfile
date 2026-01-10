# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy everything and build
COPY . .
RUN dotnet build -c Release

# Run tests
RUN dotnet test -c Release --no-build --verbosity normal

# Publish self-contained for linux-x64
RUN dotnet publish src/StructuraLens.Cli/StructuraLens.Cli.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["/app/StructuraLens.Cli"]
