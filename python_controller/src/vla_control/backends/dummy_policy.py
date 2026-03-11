from __future__ import annotations

from math import sqrt

from pydantic import Field
from pydantic_settings import BaseSettings

from vla_control.backends.base import PolicyBackend, PolicyInput
from vla_control.config import build_settings_config
from vla_control.models import DeltaPoseAction, Quaternion, StateResponse, Vector3


class DummyPolicyConfig(BaseSettings):
    model_config = build_settings_config(env_prefix="VLA_DUMMY_")

    goal_position: Vector3 = Field(default_factory=lambda: Vector3(x=0.2, y=0.95, z=0.15))
    goal_rotation: Quaternion = Field(default_factory=Quaternion.identity)
    goal_gripper: float = Field(default=0.5, ge=0.0, le=1.0)
    max_translation_delta_meters: float = Field(default=0.05, gt=0.0)
    position_tolerance_meters: float = Field(default=0.02, ge=0.0)
    gripper_tolerance: float = Field(default=0.05, ge=0.0)


class DummyRawAction(DeltaPoseAction):
    goal_position: Vector3
    goal_gripper: float = Field(ge=0.0, le=1.0)
    remaining_distance_meters: float = Field(ge=0.0)


class DummyPolicy(PolicyBackend):
    def __init__(self, config: DummyPolicyConfig | None = None) -> None:
        self.config = config or DummyPolicyConfig()
        self._loaded = False
        self._last_gripper_goal = self.config.goal_gripper

    @property
    def name(self) -> str:
        return "dummy"

    def load(self) -> None:
        self._loaded = True

    def predict(self, observation: PolicyInput) -> DummyRawAction:
        if not self._loaded:
            self.load()

        current_position = observation.state.current_pose.position
        goal_position = self.config.goal_position
        delta_x = goal_position.x - current_position.x
        delta_y = goal_position.y - current_position.y
        delta_z = goal_position.z - current_position.z
        distance = sqrt(delta_x * delta_x + delta_y * delta_y + delta_z * delta_z)

        scale = 1.0
        if distance > self.config.max_translation_delta_meters and distance > 0.0:
            scale = self.config.max_translation_delta_meters / distance

        target_gripper = self._resolve_target_gripper(observation.instruction)
        self._last_gripper_goal = target_gripper

        return DummyRawAction(
            delta_position=Vector3(x=delta_x * scale, y=delta_y * scale, z=delta_z * scale),
            target_rotation=self.config.goal_rotation,
            gripper=target_gripper,
            blocking=False,
            goal_position=goal_position,
            goal_gripper=target_gripper,
            remaining_distance_meters=distance,
        )

    def should_stop(self, state: StateResponse, step_index: int) -> bool:
        position_error = _distance(state.current_pose.position, self.config.goal_position)
        gripper_error = abs(state.gripper - self._last_gripper_goal)
        return position_error <= self.config.position_tolerance_meters and gripper_error <= self.config.gripper_tolerance

    def _resolve_target_gripper(self, instruction: str) -> float:
        lowered = instruction.lower()
        if "open" in lowered:
            return 0.0
        if "close" in lowered:
            return 1.0
        return self.config.goal_gripper


def _distance(a: Vector3, b: Vector3) -> float:
    dx = a.x - b.x
    dy = a.y - b.y
    dz = a.z - b.z
    return sqrt(dx * dx + dy * dy + dz * dz)
