from __future__ import annotations

from pathlib import Path
from time import perf_counter
from typing import Any, Literal

from pydantic import BaseModel, Field
from pydantic_settings import BaseSettings

from vla_control.action_adapter import ActionAdapter
from vla_control.backends.base import PolicyBackend, PolicyInput
from vla_control.config import build_settings_config
from vla_control.logging_utils import RolloutArtifactWriter
from vla_control.models import MoveToPoseResponse, PoseCommand, StateResponse, StepResponse
from vla_control.unity_client import UnityClient


class RolloutConfig(BaseSettings):
    model_config = build_settings_config(env_prefix="VLA_ROLLOUT_")

    instruction: str = Field(default="move the proxy toward the dummy goal", min_length=1)
    max_steps: int = Field(default=10, ge=1)
    camera_name: str = Field(default="main", min_length=1)
    image_width: int = Field(default=512, ge=1)
    image_height: int = Field(default=512, ge=1)
    image_quality: int = Field(default=90, ge=1, le=100)
    artifact_root: Path | None = None
    run_name: str | None = None
    save_images: bool = True
    reset_before_rollout: bool = True
    reset_after_rollout: bool = False
    stop_on_backend_terminal: bool = True
    raise_on_error: bool = True


class StepTiming(BaseModel):
    capture_image_seconds: float
    fetch_state_seconds: float
    inference_seconds: float
    adapt_action_seconds: float
    move_command_seconds: float
    step_simulation_seconds: float
    fetch_post_state_seconds: float
    total_step_seconds: float


class RolloutStepRecord(BaseModel):
    step_index: int
    instruction: str
    backend_name: str
    state_before: StateResponse
    raw_action: dict[str, Any]
    pose_command: PoseCommand
    move_response: MoveToPoseResponse
    step_response: StepResponse
    state_after: StateResponse
    timing: StepTiming
    image_path: Path | None = None


class RolloutSummary(BaseModel):
    backend_name: str
    instruction: str
    status: Literal["success", "max_steps", "error"]
    initial_state: StateResponse
    final_state: StateResponse
    total_steps: int
    total_wall_time_seconds: float
    records: list[RolloutStepRecord]
    artifact_dir: Path | None = None
    error_message: str | None = None


class EvaluationRunner:
    def __init__(self, client: UnityClient, policy: PolicyBackend, action_adapter: ActionAdapter) -> None:
        self.client = client
        self.policy = policy
        self.action_adapter = action_adapter

    def run_rollout(self, config: RolloutConfig | None = None, *, instruction: str | None = None) -> RolloutSummary:
        rollout_config = config or RolloutConfig()
        if instruction is not None:
            rollout_config = rollout_config.model_copy(update={"instruction": instruction})

        artifact_writer = self._maybe_create_artifact_writer(rollout_config, self.policy.name)
        initial_state = self._prepare_initial_state(rollout_config)
        records: list[RolloutStepRecord] = []
        status: Literal["success", "max_steps", "error"] = "max_steps"
        error_message: str | None = None
        overall_start = perf_counter()

        try:
            self.policy.load()

            for step_index in range(rollout_config.max_steps):
                step_start = perf_counter()

                capture_start = perf_counter()
                image = self.client.get_camera(
                    rollout_config.camera_name,
                    width=rollout_config.image_width,
                    height=rollout_config.image_height,
                    quality=rollout_config.image_quality,
                )
                capture_elapsed = perf_counter() - capture_start

                state_start = perf_counter()
                state_before = self.client.get_state()
                state_elapsed = perf_counter() - state_start

                inference_start = perf_counter()
                raw_action = self.policy.predict(
                    PolicyInput(
                        image=image,
                        instruction=rollout_config.instruction,
                        state=state_before,
                        step_index=step_index,
                    )
                )
                inference_elapsed = perf_counter() - inference_start

                adapt_start = perf_counter()
                pose_command = self.action_adapter.raw_to_pose_command(raw_action, state_before)
                adapt_elapsed = perf_counter() - adapt_start

                move_start = perf_counter()
                move_response = self.client.move_to_pose(pose_command)
                move_elapsed = perf_counter() - move_start

                step_call_start = perf_counter()
                step_response = self.client.step_control_interval()
                step_elapsed = perf_counter() - step_call_start

                post_state_start = perf_counter()
                state_after = self.client.get_state()
                post_state_elapsed = perf_counter() - post_state_start

                record = RolloutStepRecord(
                    step_index=step_index,
                    instruction=rollout_config.instruction,
                    backend_name=self.policy.name,
                    state_before=state_before,
                    raw_action=raw_action.model_dump(mode="json") if isinstance(raw_action, BaseModel) else dict(raw_action),
                    pose_command=pose_command,
                    move_response=move_response,
                    step_response=step_response,
                    state_after=state_after,
                    timing=StepTiming(
                        capture_image_seconds=capture_elapsed,
                        fetch_state_seconds=state_elapsed,
                        inference_seconds=inference_elapsed,
                        adapt_action_seconds=adapt_elapsed,
                        move_command_seconds=move_elapsed,
                        step_simulation_seconds=step_elapsed,
                        fetch_post_state_seconds=post_state_elapsed,
                        total_step_seconds=perf_counter() - step_start,
                    ),
                )

                if artifact_writer is not None:
                    record.image_path = artifact_writer.write_step(step_index, record, image=image)

                records.append(record)

                if rollout_config.stop_on_backend_terminal and self.policy.should_stop(state_after, step_index + 1):
                    status = "success"
                    break
        except Exception as exc:
            status = "error"
            error_message = str(exc)
            if rollout_config.raise_on_error:
                summary = self._build_summary(
                    initial_state=initial_state,
                    records=records,
                    backend_name=self.policy.name,
                    instruction=rollout_config.instruction,
                    status=status,
                    total_wall_time_seconds=perf_counter() - overall_start,
                    artifact_writer=artifact_writer,
                    error_message=error_message,
                )
                if artifact_writer is not None:
                    artifact_writer.write_summary(summary)
                raise
        finally:
            if rollout_config.reset_after_rollout:
                self.client.reset()

        summary = self._build_summary(
            initial_state=initial_state,
            records=records,
            backend_name=self.policy.name,
            instruction=rollout_config.instruction,
            status=status,
            total_wall_time_seconds=perf_counter() - overall_start,
            artifact_writer=artifact_writer,
            error_message=error_message,
        )
        if artifact_writer is not None:
            artifact_writer.write_summary(summary)
        return summary

    def _prepare_initial_state(self, config: RolloutConfig) -> StateResponse:
        if config.reset_before_rollout:
            self.client.reset()
        return self.client.get_state()

    @staticmethod
    def _maybe_create_artifact_writer(config: RolloutConfig, backend_name: str) -> RolloutArtifactWriter | None:
        if config.artifact_root is None:
            return None
        return RolloutArtifactWriter.create(config.artifact_root, backend_name=backend_name, run_name=config.run_name, save_images=config.save_images)

    def _build_summary(
        self,
        *,
        initial_state: StateResponse,
        records: list[RolloutStepRecord],
        backend_name: str,
        instruction: str,
        status: Literal["success", "max_steps", "error"],
        total_wall_time_seconds: float,
        artifact_writer: RolloutArtifactWriter | None,
        error_message: str | None,
    ) -> RolloutSummary:
        final_state = records[-1].state_after if records else initial_state
        return RolloutSummary(
            backend_name=backend_name,
            instruction=instruction,
            status=status,
            initial_state=initial_state,
            final_state=final_state,
            total_steps=len(records),
            total_wall_time_seconds=total_wall_time_seconds,
            records=records,
            artifact_dir=artifact_writer.run_dir if artifact_writer is not None else None,
            error_message=error_message,
        )
