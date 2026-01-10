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

echo "Running analysis..."
podman run --rm \
  -v "$SCRIPT_DIR:/workspace:Z" \
  $NUGET_MOUNT \
  -w /workspace \
  "$IMAGE_NAME" \
  analyze "$SOLUTION" --format "$FORMAT" --out "$OUTPUT"

echo "Done. Report written to: $OUTPUT"
