# OpenVLA Runtime

Minimal remote inference runtime for OpenVLA.

Typical setup on the remote GPU host:

```bash
uv sync --extra cu118
uv run openvla-runtime-server
```

Environment variables:

- `OPENVLA_RUNTIME_HOST`
- `OPENVLA_RUNTIME_PORT`
- `OPENVLA_RUNTIME_MODEL_PATH`
- `OPENVLA_RUNTIME_DEVICE`
- `OPENVLA_RUNTIME_TORCH_DTYPE`
- `OPENVLA_RUNTIME_ATTN_IMPLEMENTATION`
- `OPENVLA_RUNTIME_DEFAULT_UNNORM_KEY`

Endpoints:

- `GET /health`
- `GET /ready`
- `POST /predict`
