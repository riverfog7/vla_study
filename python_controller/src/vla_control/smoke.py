from __future__ import annotations

from pathlib import Path

from pydantic import BaseModel, ConfigDict

from vla_control.image_utils import save_image
from vla_control.models import HealthResponse, MoveToPoseResponse, PoseCommand, ResetResponse, StateResponse, StepResponse
from vla_control.unity_client import UnityClient


class SmokeTestResult(BaseModel):
    model_config = ConfigDict(arbitrary_types_allowed=True)

    health: HealthResponse
    reset: ResetResponse
    initial_state: StateResponse
    command: PoseCommand
    move_response: MoveToPoseResponse
    step_response: StepResponse
    final_state: StateResponse
    saved_image_path: Path | None = None


def run_smoke_test(client: UnityClient, save_dir: Path | str | None = None) -> SmokeTestResult:
    health = client.health()
    reset = client.reset()
    initial_state = client.get_state()
    image = client.get_camera()

    saved_image_path = None
    if save_dir is not None:
        output_dir = Path(save_dir)
        saved_image_path = save_image(image, output_dir / "smoke_main.jpg")

    command = PoseCommand.world(x=0.2, y=0.95, z=0.15, gripper=0.5)
    move_response = client.move_to_pose(command)
    step_response = client.step_control_interval()
    final_state = client.get_state()

    return SmokeTestResult(
        health=health,
        reset=reset,
        initial_state=initial_state,
        command=command,
        move_response=move_response,
        step_response=step_response,
        final_state=final_state,
        saved_image_path=saved_image_path,
    )
