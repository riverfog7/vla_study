#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
export PATH="$HOME/.local/bin:$PATH"
export OCTO_RUNTIME_HOST="${OCTO_RUNTIME_HOST:-0.0.0.0}"
export OCTO_RUNTIME_PORT="${OCTO_RUNTIME_PORT:-8001}"
export OCTO_RUNTIME_MODEL_PATH="${OCTO_RUNTIME_MODEL_PATH:-hf://rail-berkeley/octo-small-1.5}"
export OCTO_RUNTIME_DEFAULT_DATASET_STATISTICS_KEY="${OCTO_RUNTIME_DEFAULT_DATASET_STATISTICS_KEY:-bridge_dataset}"
export OCTO_RUNTIME_IMAGE_SIZE="${OCTO_RUNTIME_IMAGE_SIZE:-256}"
export OCTO_RUNTIME_LOAD_ON_STARTUP="${OCTO_RUNTIME_LOAD_ON_STARTUP:-true}"
exec uv run --no-sync octo-runtime-server
