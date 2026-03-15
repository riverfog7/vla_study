# vla-control

Importable Python client library for the Unity VLA simulator.

## Install dependencies

```bash
uv sync
```

## Python shell / notebook usage

```python
from vla_control import UnityClient, PoseCommand

unity = UnityClient()
unity.reset()

state = unity.get_state()
image = unity.get_camera("main")

command = PoseCommand.world(x=0.2, y=0.95, z=0.15, gripper=0.5)
unity.move_to_pose(command)
unity.step_control_interval()

state = unity.get_state()
image
```

`get_camera()` returns a `PIL.Image.Image`, so notebooks can display it directly.

## Managing named cameras

```python
from vla_control import UnityClient, UpsertCameraRequest, Vector3

client = UnityClient()

client.list_cameras()

client.upsert_camera(
    UpsertCameraRequest.mounted(
        name="wrist",
        mount_target="proxy_camera_mount",
        local_position=Vector3(x=0.0, y=0.08, z=-0.12),
        local_rotation_euler=Vector3(x=15.0, y=0.0, z=0.0),
    )
)

wrist_image = client.get_camera("wrist")
client.delete_camera("wrist")
```

## Notebook rollout usage

```python
from pathlib import Path

from vla_control import ActionAdapter, DummyPolicy, EvaluationRunner, RolloutConfig, UnityClient

client = UnityClient()
runner = EvaluationRunner(client, DummyPolicy(), ActionAdapter())

summary = runner.run_rollout(
    RolloutConfig(
        instruction="move the proxy toward the dummy goal",
        max_steps=6,
        artifact_root=Path("artifacts/notebook-rollout"),
    )
)

summary.status, summary.total_steps
```

## OpenVLA workflow

Single-step sanity check against a hosted OpenVLA runtime:

```python
from vla_control import UnityClient, create_openvla_backend, run_openvla_single_step_check

client = UnityClient()
backend = create_openvla_backend(
    base_url="http://your-openvla-host:8000",
    unnorm_key="bridge_orig",
    timeout_seconds=300.0,
)

result = run_openvla_single_step_check(client, backend)
result.pose_command
```

Short rollout with the current xyz-only OpenVLA adapter path:

```python
from vla_control import UnityClient, create_openvla_backend, run_openvla_rollout

client = UnityClient()
backend = create_openvla_backend(
    base_url="http://your-openvla-host:8000",
    unnorm_key="bridge_orig",
    timeout_seconds=300.0,
)

summary = run_openvla_rollout(client, backend)
summary.status, summary.total_steps
```

Current OpenVLA behavior:

- `xyz` translation is applied
- gripper is applied
- rotation deltas are logged but not yet sent to Unity

## Octo workflow

Single-step sanity check against a hosted Octo runtime:

```python
from vla_control import UnityClient, create_octo_backend, run_octo_single_step_check

client = UnityClient()
backend = create_octo_backend(
    base_url="http://your-octo-host:8001",
    dataset_statistics_key="bridge_dataset",
    timeout_seconds=300.0,
)

result = run_octo_single_step_check(client, backend)
result.pose_command
```

Short rollout with the current xyz-only Octo adapter path:

```python
from vla_control import UnityClient, create_octo_backend, run_octo_rollout

client = UnityClient()
backend = create_octo_backend(
    base_url="http://your-octo-host:8001",
    dataset_statistics_key="bridge_dataset",
    timeout_seconds=300.0,
)

summary = run_octo_rollout(client, backend)
summary.status, summary.total_steps
```

Current Octo behavior:

- one RGB camera input (`main`) is used
- language instructions are used
- the backend keeps a short image history (default horizon `2`) and sends `timestep_pad_mask` to the Octo runtime
- Octo returns the full predicted action chunk
- temporal ensembling is applied inside the Octo backend before selecting a single action to execute
- only one action is executed per control step
- `xyz` translation is applied
- gripper is applied
- rotation deltas are logged but not yet sent to Unity

## CLI smoke test

```bash
uv run python main.py smoke --save-dir artifacts/smoke
```

```bash
uv run python main.py dummy-rollout --instruction "move the proxy toward the dummy goal" --max-steps 6 --save-dir artifacts/dummy-rollout
```

```bash
uv run python main.py openvla-check --openvla-url http://your-openvla-host:8000 --unnorm-key bridge_orig
```

```bash
uv run python main.py openvla-rollout --openvla-url http://your-openvla-host:8000 --unnorm-key bridge_orig --max-steps 3 --save-dir artifacts/openvla_first_rollout
```

```bash
uv run python main.py octo-check --octo-url http://your-octo-host:8001 --dataset-statistics-key bridge_dataset
```

```bash
uv run python main.py octo-rollout --octo-url http://your-octo-host:8001 --dataset-statistics-key bridge_dataset --max-steps 3 --save-dir artifacts/octo_first_rollout
```

This runs:

- `health`
- `reset`
- `get_state`
- `get_camera("main")`
- `move_to_pose`
- `step_control_interval`
- `get_state` again

The dummy rollout command runs the synchronous loop:

- capture image
- fetch state
- predict with the dummy backend
- adapt the raw action into `PoseCommand`
- send `move_to_pose`
- `step_control_interval`
- repeat until success or `max_steps`

## Environment variables

`UnityConfig` uses `pydantic-settings` and reads optional `VLA_CONTROL_` environment variables:

- `VLA_CONTROL_HOST`
- `VLA_CONTROL_PORT`
- `VLA_CONTROL_TIMEOUT_SECONDS`
- `VLA_CONTROL_DEFAULT_CAMERA_NAME`
- `VLA_CONTROL_DEFAULT_IMAGE_WIDTH`
- `VLA_CONTROL_DEFAULT_IMAGE_HEIGHT`
- `VLA_CONTROL_DEFAULT_IMAGE_QUALITY`
