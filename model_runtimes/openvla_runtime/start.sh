#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
export PATH="$HOME/.local/bin:$PATH"
export OPENVLA_RUNTIME_HOST="${OPENVLA_RUNTIME_HOST:-0.0.0.0}"
export OPENVLA_RUNTIME_PORT="${OPENVLA_RUNTIME_PORT:-8000}"
export OPENVLA_RUNTIME_MODEL_PATH="${OPENVLA_RUNTIME_MODEL_PATH:-openvla/openvla-7b}"
export OPENVLA_RUNTIME_DEVICE="${OPENVLA_RUNTIME_DEVICE:-cuda:0}"
export OPENVLA_RUNTIME_TORCH_DTYPE="${OPENVLA_RUNTIME_TORCH_DTYPE:-bfloat16}"
export OPENVLA_RUNTIME_ATTN_IMPLEMENTATION="${OPENVLA_RUNTIME_ATTN_IMPLEMENTATION:-flash_attention_2}"
export OPENVLA_RUNTIME_DEFAULT_UNNORM_KEY="${OPENVLA_RUNTIME_DEFAULT_UNNORM_KEY:-bridge_orig}"
export OPENVLA_RUNTIME_LOAD_ON_STARTUP="${OPENVLA_RUNTIME_LOAD_ON_STARTUP:-true}"
CUDA_LIBS="$(find "$PWD/.venv" -type d \( -path '*/site-packages/nvidia/*/lib' -o -path '*/site-packages/nvidia/*/lib64' \) | paste -sd: -)"
export LD_LIBRARY_PATH="${CUDA_LIBS}${CUDA_LIBS:+:}${LD_LIBRARY_PATH:-}"
exec uv run --no-sync openvla-runtime-server
