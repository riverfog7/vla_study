# Octo Runtime

Minimal remote inference runtime for Octo.

Recommended remote workflow on the GPU host:

```bash
./configure.sh
./start.sh
```

Notes:

- This runtime is intended to run on a separate remote Linux GPU machine.
- The local Unity/controller machine should talk to it over HTTP.
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
