from __future__ import annotations

from pydantic import BaseModel, ConfigDict, Field


class UnityBaseModel(BaseModel):
    model_config = ConfigDict(extra="ignore", validate_assignment=True)


class UnityRequestModel(UnityBaseModel):
    model_config = ConfigDict(extra="forbid", validate_assignment=True)


class Vector3(UnityBaseModel):
    x: float
    y: float
    z: float

    @classmethod
    def zero(cls) -> "Vector3":
        return cls(x=0.0, y=0.0, z=0.0)


class Quaternion(UnityBaseModel):
    x: float = 0.0
    y: float = 0.0
    z: float = 0.0
    w: float = 1.0

    @classmethod
    def identity(cls) -> "Quaternion":
        return cls(x=0.0, y=0.0, z=0.0, w=1.0)


class Pose(UnityBaseModel):
    position: Vector3
    rotation: Quaternion


class AbsolutePoseAction(UnityRequestModel):
    frame: str = Field(default="world", min_length=1)
    position: Vector3
    rotation: Quaternion = Field(default_factory=Quaternion.identity)
    gripper: float = Field(default=0.0, ge=0.0, le=1.0)
    blocking: bool = False


class DeltaPoseAction(UnityRequestModel):
    delta_position: Vector3
    target_rotation: Quaternion | None = None
    gripper: float | None = Field(default=None, ge=0.0, le=1.0)
    blocking: bool = False


class PoseCommand(UnityRequestModel):
    frame: str = Field(default="world", min_length=1)
    position: Vector3
    rotation: Quaternion = Field(default_factory=Quaternion.identity)
    gripper: float = Field(default=0.0, ge=0.0, le=1.0)
    blocking: bool = False

    @classmethod
    def world(
        cls,
        *,
        x: float,
        y: float,
        z: float,
        qx: float = 0.0,
        qy: float = 0.0,
        qz: float = 0.0,
        qw: float = 1.0,
        gripper: float = 0.0,
        blocking: bool = False,
    ) -> "PoseCommand":
        return cls(
            frame="world",
            position=Vector3(x=x, y=y, z=z),
            rotation=Quaternion(x=qx, y=qy, z=qz, w=qw),
            gripper=gripper,
            blocking=blocking,
        )


class CameraRequest(UnityRequestModel):
    camera_name: str = Field(default="main", min_length=1)
    width: int = Field(default=256, ge=1)
    height: int = Field(default=256, ge=1)
    quality: int = Field(default=80, ge=1, le=100)

    def to_query_params(self) -> dict[str, str]:
        return {
            "width": str(self.width),
            "height": str(self.height),
            "quality": str(self.quality),
        }


class StepRequest(UnityRequestModel):
    steps: int = Field(default=1, ge=1)
    dt: float = Field(gt=0.0)


class HealthResponse(UnityBaseModel):
    ok: bool
    app: str
    version: str


class StateResponse(UnityBaseModel):
    sim_time: float
    step_count: int
    physics_dt: float
    policy_period_seconds: float
    steps_per_action: int
    robot_mode: str
    current_pose: Pose
    gripper: float
    target_pose: Pose
    target_gripper: float
    last_command_id: int
    motion_in_progress: bool
    last_command_was_clipped: bool


class StepResponse(UnityBaseModel):
    ok: bool
    sim_time: float
    step_count: int


class MoveToPoseResponse(UnityBaseModel):
    accepted: bool
    command_id: int


class ResetResponse(UnityBaseModel):
    ok: bool


class ErrorResponse(UnityBaseModel):
    error: str
    details: str | None = None
