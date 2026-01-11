#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
IMAGE_NAME="structura-lens:local"
SOLUTION="${1:-StructuraLens.slnx}"
FORMAT="${2:-html}"
OUTPUT="${3:-structuralens-report.html}"

echo "Building Docker image..."
podman build -t "$IMAGE_NAME" -f "$SCRIPT_DIR/Dockerfile" "$SCRIPT_DIR"

# Mount NuGet cache if available to speed up restore
NUGET_MOUNT=""
if [ -d "$HOME/.nuget/packages" ]; then
  NUGET_MOUNT="-v $HOME/.nuget/packages:/root/.nuget/packages:ro,Z"
  echo "Mounting NuGet cache: $HOME/.nuget/packages"
fi

# Mount NuGet config for private feed authentication
NUGET_CONFIG_MOUNT=""
if [ -d "$HOME/.nuget/NuGet" ]; then
  NUGET_CONFIG_MOUNT="-v $HOME/.nuget/NuGet:/root/.nuget/NuGet:ro,Z"
  echo "Mounting NuGet config: $HOME/.nuget/NuGet"
elif [ -f "$HOME/.nuget/NuGet.Config" ]; then
  NUGET_CONFIG_MOUNT="-v $HOME/.nuget/NuGet.Config:/root/.nuget/NuGet.Config:ro,Z"
  echo "Mounting NuGet config: $HOME/.nuget/NuGet.Config"
fi

# Mount credential provider plugins from host if available
CREDPROVIDER_MOUNT=""
if [ -d "$HOME/.nuget/plugins" ]; then
  CREDPROVIDER_MOUNT="-v $HOME/.nuget/plugins:/root/.nuget/plugins:ro,Z"
  echo "Mounting NuGet credential providers: $HOME/.nuget/plugins"
fi

# Pass credentials for Azure Artifacts Credential Provider if set
CREDENTIAL_ENV=""
if [ -n "$NUGET_PAT" ]; then
  echo "Using NUGET_PAT for private feed authentication"
  CREDENTIAL_ENV="-e VSS_NUGET_EXTERNAL_FEED_ENDPOINTS"
  # Build the endpoint JSON if feed URL is provided
  if [ -n "$NUGET_FEED_URL" ]; then
    export VSS_NUGET_EXTERNAL_FEED_ENDPOINTS="{\"endpointCredentials\": [{\"endpoint\":\"$NUGET_FEED_URL\", \"username\":\"docker\", \"password\":\"$NUGET_PAT\"}]}"
  fi
fi

echo "Running analysis..."
podman run --rm \
  -v "$SCRIPT_DIR:/workspace:Z" \
  $NUGET_MOUNT \
  $NUGET_CONFIG_MOUNT \
  $CREDPROVIDER_MOUNT \
  $CREDENTIAL_ENV \
  -w /workspace \
  "$IMAGE_NAME" \
  analyze "$SOLUTION" --format "$FORMAT" --out "$OUTPUT"

echo "Done. Report written to: $OUTPUT"
