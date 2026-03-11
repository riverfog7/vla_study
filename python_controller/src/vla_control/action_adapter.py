from __future__ import annotations

from pydantic import BaseModel, Field
from pydantic_settings import BaseSettings

from vla_control.config import build_settings_config
from vla_control.models import AbsolutePoseAction, DeltaPoseAction, PoseCommand, Quaternion, StateResponse, Vector3


class WorkspaceBounds(BaseModel):
    min: Vector3 = Field(default_factory=lambda: Vector3(x=-0.75, y=0.55, z=-0.75))
    max: Vector3 = Field(default_factory=lambda: Vector3(x=0.75, y=1.3, z=0.75))


class ActionAdapterConfig(BaseSettings):
    model_config = build_settings_config(env_prefix="VLA_ADAPTER_")

    frame: str = Field(default="world", min_length=1)
    default_blocking: bool = False
    workspace_bounds: WorkspaceBounds = Field(default_factory=WorkspaceBounds)


class ActionAdapter:
    def __init__(self, config: ActionAdapterConfig | None = None) -> None:
        self.config = config or ActionAdapterConfig()

    def raw_to_pose_command(self, raw_action: BaseModel | dict, state: StateResponse) -> PoseCommand:
        resolved_action = self._coerce_action(raw_action)

        if isinstance(resolved_action, PoseCommand):
            return resolved_action

        if isinstance(resolved_action, AbsolutePoseAction):
            return PoseCommand(
                frame=resolved_action.frame,
                position=self._clamp_position(resolved_action.position),
                rotation=resolved_action.rotation,
                gripper=resolved_action.gripper,
                blocking=resolved_action.blocking,
            )

        if isinstance(resolved_action, DeltaPoseAction):
            current_position = state.current_pose.position
            target_position = Vector3(
                x=current_position.x + resolved_action.delta_position.x,
                y=current_position.y + resolved_action.delta_position.y,
                z=current_position.z + resolved_action.delta_position.z,
            )
            return PoseCommand(
                frame=self.config.frame,
                position=self._clamp_position(target_position),
                rotation=resolved_action.target_rotation or state.current_pose.rotation,
                gripper=state.gripper if resolved_action.gripper is None else resolved_action.gripper,
                blocking=resolved_action.blocking,
            )

        raise TypeError(f"Unsupported raw action type: {type(raw_action)!r}")

    def _coerce_action(self, raw_action: BaseModel | dict) -> PoseCommand | AbsolutePoseAction | DeltaPoseAction:
        if isinstance(raw_action, (PoseCommand, AbsolutePoseAction, DeltaPoseAction)):
            return raw_action

        if isinstance(raw_action, dict):
            if "delta_position" in raw_action:
                return DeltaPoseAction.model_validate(raw_action)
            if "position" in raw_action:
                if "frame" in raw_action or "blocking" in raw_action:
                    return PoseCommand.model_validate(raw_action)
                return AbsolutePoseAction.model_validate(raw_action)

        raise TypeError(f"Unsupported raw action payload: {type(raw_action)!r}")

    def _clamp_position(self, position: Vector3) -> Vector3:
        bounds = self.config.workspace_bounds
        return Vector3(
            x=max(bounds.min.x, min(bounds.max.x, position.x)),
            y=max(bounds.min.y, min(bounds.max.y, position.y)),
            z=max(bounds.min.z, min(bounds.max.z, position.z)),
        )
