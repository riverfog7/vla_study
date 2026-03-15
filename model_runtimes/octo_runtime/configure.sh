#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
export PATH="$HOME/.local/bin:$PATH"
if ! command -v uv >/dev/null 2>&1; then
  if command -v curl >/dev/null 2>&1; then curl -LsSf https://astral.sh/uv/install.sh | sh; else wget -qO- https://astral.sh/uv/install.sh | sh; fi
  export PATH="$HOME/.local/bin:$PATH"
fi
repo_root="$(git rev-parse --show-toplevel)"
git -C "$repo_root" submodule update --init --recursive -- "model_runtimes/octo_runtime/vendor/octo"
rm -rf .venv
uv sync --frozen
