# Octo Runtime

Minimal remote inference runtime for Octo.

This runtime vendors the upstream Octo source as a git submodule at `vendor/octo` and imports it via `PYTHONPATH`. This avoids the broken upstream `v1.5` package metadata, which does not install the `octo.model` subpackages correctly.

Recommended remote workflow on the GPU host:

```bash
./configure.sh
./start.sh
```

If the repo was not cloned with submodules, `configure.sh` will initialize `model_runtimes/octo_runtime/vendor/octo` automatically.

Notes:

- This runtime is intended to run on a separate remote Linux GPU machine.
- The local Unity/controller machine should talk to it over HTTP.
- JAX GPU support is explicit in `pyproject.toml` via `jax==0.4.20`, the exact CUDA 11 `jaxlib` wheel, and the matching CUDA 11 runtime packages.
- TensorFlow is intentionally CPU-only via `tensorflow-cpu==2.15.0`; Octo inference uses JAX/Flax, while TensorFlow is retained for checkpoint and utility code.
- The runtime accepts a short history of RGB frames plus a `timestep_pad_mask`.
- The controller currently uses language instructions, a history horizon of `2`, and temporal ensembling over Octo's returned action chunks.
- Rotation deltas are returned to the controller but remain ignored by the Unity adapter, matching the current OpenVLA behavior.

Environment variables:

- `OCTO_RUNTIME_HOST`
- `OCTO_RUNTIME_PORT`
- `OCTO_RUNTIME_MODEL_PATH`
- `OCTO_RUNTIME_DEFAULT_DATASET_STATISTICS_KEY`
- `OCTO_RUNTIME_IMAGE_SIZE`
- `OCTO_RUNTIME_LOAD_ON_STARTUP`
- `OCTO_RUNTIME_LOG_LEVEL`

Recommended defaults:

- `OCTO_RUNTIME_MODEL_PATH=hf://rail-berkeley/octo-small-1.5`
- `OCTO_RUNTIME_DEFAULT_DATASET_STATISTICS_KEY=bridge_dataset`

Endpoints:

- `GET /health`
- `GET /ready`
- `POST /predict`

`POST /predict` request fields:

- `instruction`
- `images_base64`
- `timestep_pad_mask`
- `image_mime_type`
- `dataset_statistics_key`
- `request_id`
- `step_index`

Quick import check on the server:

```bash
uv run --no-sync python -c "from octo.model.octo_model import OctoModel; print('ok')"
```
