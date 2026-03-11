from __future__ import annotations

import base64
import json
from io import BytesIO
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from PIL import Image
from pydantic import BaseModel, ConfigDict, Field, ValidationError
from pydantic_settings import BaseSettings

from vla_control.backends.base import PolicyBackend, PolicyInput
from vla_control.config import build_settings_config
from vla_control.models import StandardizedDeltaAction, Vector3


class OpenVLABackendError(RuntimeError):
    """Base error for OpenVLA remote backend failures."""


class OpenVLARemoteUnavailableError(OpenVLABackendError):
    """Raised when the OpenVLA runtime cannot be reached or is not ready."""


class OpenVLAProtocolError(OpenVLABackendError):
    """Raised when the OpenVLA runtime returns malformed responses."""


class OpenVLAServiceErrorResponse(BaseModel):
    model_config = ConfigDict(extra="ignore")

    error: str
    details: str | None = None


class OpenVLAHealthResponse(BaseModel):
    model_config = ConfigDict(extra="ignore")

    ok: bool
    app: str
    version: str
    model_id: str


class OpenVLAReadyResponse(BaseModel):
    model_config = ConfigDict(extra="ignore")

    ready: bool
    model_loaded: bool
    processor_loaded: bool
    model_id: str
    device: str
    dtype: str
    available_unnorm_keys: list[str] = Field(default_factory=list)
    default_unnorm_key: str | None = None
    error: str | None = None


class OpenVLAServiceTiming(BaseModel):
    model_config = ConfigDict(extra="ignore")

    decode_image_ms: float
    processor_ms: float
    predict_ms: float
    total_ms: float


class OpenVLAServicePredictRequest(BaseModel):
    instruction: str
    image_base64: str
    image_mime_type: str = "image/jpeg"
    unnorm_key: str | None = None
    request_id: str | None = None
    step_index: int | None = Field(default=None, ge=0)


class OpenVLAServicePredictResponse(BaseModel):
    model_config = ConfigDict(extra="ignore")

    action: list[float]
    action_dim: int
    unnorm_key_used: str | None = None
    prompt: str
    model_id: str
    request_id: str | None = None
    timing_ms: OpenVLAServiceTiming


class OpenVLARestBackendConfig(BaseSettings):
    model_config = build_settings_config(env_prefix="VLA_OPENVLA_")

    base_url: str = "http://127.0.0.1:8000"
    timeout_seconds: float = Field(default=120.0, gt=0.0)
    unnorm_key: str | None = None
    image_quality: int = Field(default=85, ge=1, le=100)
    image_format: str = "JPEG"


class OpenVLARestBackend(PolicyBackend):
    def __init__(self, config: OpenVLARestBackendConfig | None = None) -> None:
        self.config = config or OpenVLARestBackendConfig()
        self._ready_response: OpenVLAReadyResponse | None = None

    @property
    def name(self) -> str:
        return "openvla-rest"

    def load(self) -> None:
        ready = self.ready()
        if not ready.ready:
            details = ready.error or "OpenVLA runtime reported not ready."
            raise OpenVLARemoteUnavailableError(details)
        self._ready_response = ready

    def health(self) -> OpenVLAHealthResponse:
        return self._request_model("GET", "/health", OpenVLAHealthResponse)

    def ready(self) -> OpenVLAReadyResponse:
        return self._request_model("GET", "/ready", OpenVLAReadyResponse)

    def predict(self, observation: PolicyInput) -> StandardizedDeltaAction:
        if self._ready_response is None:
            self.load()

        request_model = OpenVLAServicePredictRequest(
            instruction=observation.instruction,
            image_base64=self._encode_image_base64(observation.image),
            image_mime_type="image/jpeg",
            unnorm_key=self.config.unnorm_key,
            request_id=f"step-{observation.step_index}",
            step_index=observation.step_index,
        )
        response = self._request_model(
            "POST", "/predict", OpenVLAServicePredictResponse, body=request_model
        )
        return self._link_action(response)

    def _link_action(
        self, response: OpenVLAServicePredictResponse
    ) -> StandardizedDeltaAction:
        action = response.action
        if len(action) < 4:
            raise OpenVLAProtocolError(
                f"Expected at least 4 action values from OpenVLA, but received {len(action)}: {action}"
            )

        raw_rotation_delta = None
        if len(action) >= 6:
            raw_rotation_delta = Vector3(x=action[3], y=action[4], z=action[5])

        gripper = action[6] if len(action) >= 7 else action[-1]

        return StandardizedDeltaAction(
            delta_position=Vector3(x=action[0], y=action[1], z=action[2]),
            gripper=gripper,
            blocking=False,
            raw_rotation_delta=raw_rotation_delta,
            rotation_mode="ignored",
            source=self.name,
            raw_action=action,
            prompt=response.prompt,
            unnorm_key_used=response.unnorm_key_used,
            model_id=response.model_id,
            inference_ms=response.timing_ms.total_ms,
            request_id=response.request_id,
        )

    def _request_model(
        self,
        method: str,
        path: str,
        model_type: type[BaseModel],
        *,
        body: BaseModel | None = None,
    ) -> Any:
        payload = self._request_bytes(method, path, body=body)
        try:
            return model_type.model_validate_json(payload)
        except ValidationError as exc:
            decoded = payload.decode("utf-8", errors="replace")
            raise OpenVLAProtocolError(
                f"OpenVLA runtime returned invalid JSON for {path}: {decoded}"
            ) from exc

    def _request_bytes(
        self, method: str, path: str, *, body: BaseModel | None = None
    ) -> bytes:
        headers: dict[str, str] = {}
        data: bytes | None = None

        if body is not None:
            headers["Content-Type"] = "application/json"
            data = json.dumps(body.model_dump(mode="json")).encode("utf-8")

        request = Request(
            self._build_url(path), data=data, headers=headers, method=method
        )

        try:
            with urlopen(request, timeout=self.config.timeout_seconds) as response:
                return response.read()
        except HTTPError as exc:
            raise self._build_api_error(exc) from exc
        except URLError as exc:
            raise OpenVLARemoteUnavailableError(
                f"Could not reach OpenVLA runtime at {self.config.base_url}: {exc.reason}"
            ) from exc

    def _build_url(self, path: str) -> str:
        normalized_path = path if path.startswith("/") else f"/{path}"
        return f"{self.config.base_url.rstrip('/')}{normalized_path}"

    def _encode_image_base64(self, image: Image.Image) -> str:
        buffer = BytesIO()
        image.save(
            buffer, format=self.config.image_format, quality=self.config.image_quality
        )
        return base64.b64encode(buffer.getvalue()).decode("ascii")

    @staticmethod
    def _build_api_error(exc: HTTPError) -> OpenVLABackendError:
        payload = exc.read()
        decoded = payload.decode("utf-8", errors="replace") if payload else None

        if payload:
            try:
                parsed = OpenVLAServiceErrorResponse.model_validate_json(payload)
                return OpenVLABackendError(
                    f"OpenVLA runtime returned HTTP {exc.code}: {parsed.error} ({parsed.details or 'no details'})"
                )
            except ValidationError:
                pass

        return OpenVLABackendError(
            f"OpenVLA runtime returned HTTP {exc.code}: {decoded or 'no response body'}"
        )


__all__ = [
    "OpenVLABackendError",
    "OpenVLAHealthResponse",
    "OpenVLAProtocolError",
    "OpenVLAReadyResponse",
    "OpenVLARemoteUnavailableError",
    "OpenVLARestBackend",
    "OpenVLARestBackendConfig",
]
