from __future__ import annotations

from pathlib import Path

from pydantic import BaseModel, ConfigDict

from vla_control.action_adapter import ActionAdapter
from vla_control.backends.base import PolicyInput
from vla_control.backends.openvla_rest_backend import (
    OpenVLAHealthResponse,
    OpenVLAReadyResponse,
    OpenVLARestBackend,
    OpenVLARestBackendConfig,
)
from vla_control.evaluation_runner import (
    EvaluationRunner,
    RolloutConfig,
    RolloutSummary,
)
from vla_control.models import (
    HealthResponse,
    MoveToPoseResponse,
    PoseCommand,
    StandardizedDeltaAction,
    StateResponse,
    StepResponse,
)
from vla_control.unity_client import UnityClient

DEFAULT_OPENVLA_INSTRUCTION = "move to the center"
DEFAULT_OPENVLA_CAMERA_NAME = "main"
DEFAULT_OPENVLA_IMAGE_WIDTH = 256
DEFAULT_OPENVLA_IMAGE_HEIGHT = 256
DEFAULT_OPENVLA_IMAGE_QUALITY = 80
DEFAULT_OPENVLA_TIMEOUT_SECONDS = 300.0
DEFAULT_OPENVLA_UNNORM_KEY = "bridge_orig"
DEFAULT_OPENVLA_ROLLOUT_STEPS = 3
DEFAULT_OPENVLA_ARTIFACT_ROOT = Path("artifacts/openvla_first_rollout")


class OpenVLASingleStepCheckResult(BaseModel):
    model_config = ConfigDict(arbitrary_types_allowed=True)

    instruction: str
    camera_name: str
    unity_health: HealthResponse
    openvla_health: OpenVLAHealthResponse
    openvla_ready: OpenVLAReadyResponse
    state_before: StateResponse
    raw_action: StandardizedDeltaAction
    pose_command: PoseCommand
    move_response: MoveToPoseResponse
    step_response: StepResponse
    state_after: StateResponse


def create_openvla_backend(
    *,
    base_url: str | None = None,
    unnorm_key: str = DEFAULT_OPENVLA_UNNORM_KEY,
    timeout_seconds: float = DEFAULT_OPENVLA_TIMEOUT_SECONDS,
) -> OpenVLARestBackend:
    config = OpenVLARestBackendConfig()
    overrides = {"unnorm_key": unnorm_key, "timeout_seconds": timeout_seconds}
    if base_url is not None:
        overrides["base_url"] = base_url
    return OpenVLARestBackend(config.model_copy(update=overrides))


def run_openvla_single_step_check(
    client: UnityClient,
    backend: OpenVLARestBackend,
    *,
    instruction: str = DEFAULT_OPENVLA_INSTRUCTION,
    camera_name: str = DEFAULT_OPENVLA_CAMERA_NAME,
    image_width: int = DEFAULT_OPENVLA_IMAGE_WIDTH,
    image_height: int = DEFAULT_OPENVLA_IMAGE_HEIGHT,
    image_quality: int = DEFAULT_OPENVLA_IMAGE_QUALITY,
    action_adapter: ActionAdapter | None = None,
) -> OpenVLASingleStepCheckResult:
    adapter = action_adapter or ActionAdapter()
    unity_health = client.health()
    openvla_health = backend.health()
    openvla_ready = backend.ready()
    backend.load()

    image = client.get_camera(
        camera_name,
        width=image_width,
        height=image_height,
        quality=image_quality,
    )
    state_before = client.get_state()
    raw_action = backend.predict(
        PolicyInput(
            image=image,
            instruction=instruction,
            state=state_before,
            step_index=0,
        )
    )
    pose_command = adapter.raw_to_pose_command(raw_action, state_before)
    move_response = client.move_to_pose(pose_command)
    step_response = client.step_control_interval()
    state_after = client.get_state()

    return OpenVLASingleStepCheckResult(
        instruction=instruction,
        camera_name=camera_name,
        unity_health=unity_health,
        openvla_health=openvla_health,
        openvla_ready=openvla_ready,
        state_before=state_before,
        raw_action=raw_action,
        pose_command=pose_command,
        move_response=move_response,
        step_response=step_response,
        state_after=state_after,
    )


def run_openvla_rollout(
    client: UnityClient,
    backend: OpenVLARestBackend,
    *,
    rollout_config: RolloutConfig | None = None,
    action_adapter: ActionAdapter | None = None,
) -> RolloutSummary:
    runner = EvaluationRunner(client, backend, action_adapter or ActionAdapter())
    config = rollout_config or RolloutConfig(
        instruction=DEFAULT_OPENVLA_INSTRUCTION,
        max_steps=DEFAULT_OPENVLA_ROLLOUT_STEPS,
        camera_name=DEFAULT_OPENVLA_CAMERA_NAME,
        image_width=DEFAULT_OPENVLA_IMAGE_WIDTH,
        image_height=DEFAULT_OPENVLA_IMAGE_HEIGHT,
        image_quality=DEFAULT_OPENVLA_IMAGE_QUALITY,
        artifact_root=DEFAULT_OPENVLA_ARTIFACT_ROOT,
        reset_before_rollout=True,
        reset_after_rollout=True,
    )
    return runner.run_rollout(config)
