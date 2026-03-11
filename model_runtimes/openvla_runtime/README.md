# OpenVLA Runtime

Minimal remote inference runtime for OpenVLA.

Standard workflow on the remote GPU host:

```bash
./configure.sh
./start.sh
```

`configure.sh` installs `uv` if needed and recreates `.venv` with `uv sync --frozen`.

`start.sh` is blocking and starts the runtime with `uv run openvla-runtime-server`.

By default, `start.sh` enables `flash_attention_2`. If you need to override anything, export environment variables before launching the script.

Environment variables:

- `OPENVLA_RUNTIME_HOST`
- `OPENVLA_RUNTIME_PORT`
- `OPENVLA_RUNTIME_MODEL_PATH`
- `OPENVLA_RUNTIME_DEVICE`
- `OPENVLA_RUNTIME_TORCH_DTYPE`
- `OPENVLA_RUNTIME_ATTN_IMPLEMENTATION`
- `OPENVLA_RUNTIME_DEFAULT_UNNORM_KEY`
- `OPENVLA_RUNTIME_LOAD_ON_STARTUP`

Endpoints:

- `GET /health`
- `GET /ready`
- `POST /predict`
