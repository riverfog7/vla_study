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

## CLI smoke test

```bash
uv run python main.py smoke --save-dir artifacts/smoke
```

```bash
uv run python main.py dummy-rollout --instruction "move the proxy toward the dummy goal" --max-steps 6 --save-dir artifacts/dummy-rollout
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
