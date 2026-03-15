from __future__ import annotations

from pydantic import BaseModel, ConfigDict, Field


class RuntimeBaseModel(BaseModel):
    model_config = ConfigDict(extra="forbid", validate_assignment=True)


class ErrorResponse(RuntimeBaseModel):
    error: str
    details: str | None = None


class HealthResponse(RuntimeBaseModel):
    ok: bool
    app: str
    version: str
    model_id: str


class ReadyResponse(RuntimeBaseModel):
    ready: bool
    model_loaded: bool
    model_id: str
    dataset_statistics_keys: list[str] = Field(default_factory=list)
    default_dataset_statistics_key: str | None = None
    image_size: int = Field(ge=1)
    error: str | None = None


class PredictRequest(RuntimeBaseModel):
    instruction: str = Field(min_length=1)
    images_base64: list[str] = Field(min_length=1)
    timestep_pad_mask: list[bool] = Field(min_length=1)
    image_mime_type: str = Field(default="image/jpeg", min_length=1)
    dataset_statistics_key: str | None = None
    request_id: str | None = None
    step_index: int | None = Field(default=None, ge=0)


class TimingBreakdown(RuntimeBaseModel):
    decode_image_ms: float
    resize_image_ms: float
    task_ms: float
    predict_ms: float
    total_ms: float


class PredictResponse(RuntimeBaseModel):
    actions: list[list[float]]
    action_dim: int = Field(ge=0)
    action_horizon: int = Field(ge=1)
    dataset_statistics_key_used: str | None = None
    model_id: str
    request_id: str | None = None
    timing_ms: TimingBreakdown
