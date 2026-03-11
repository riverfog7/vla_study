# pyright: reportMissingImports=false
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
    processor_loaded: bool
    model_id: str
    device: str
    dtype: str
    available_unnorm_keys: list[str] = Field(default_factory=list)
    default_unnorm_key: str | None = None
    error: str | None = None


class PredictRequest(RuntimeBaseModel):
    instruction: str = Field(min_length=1)
    image_base64: str = Field(min_length=1)
    image_mime_type: str = Field(default="image/jpeg", min_length=1)
    unnorm_key: str | None = None
    request_id: str | None = None
    step_index: int | None = Field(default=None, ge=0)


class TimingBreakdown(RuntimeBaseModel):
    decode_image_ms: float
    processor_ms: float
    predict_ms: float
    total_ms: float


class PredictResponse(RuntimeBaseModel):
    action: list[float]
    action_dim: int = Field(ge=0)
    unnorm_key_used: str | None = None
    prompt: str
    model_id: str
    request_id: str | None = None
    timing_ms: TimingBreakdown
