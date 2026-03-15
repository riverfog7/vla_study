from __future__ import annotations

import base64
import json
import math
from io import BytesIO
from collections import deque
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from PIL import Image
from pydantic import BaseModel, ConfigDict, Field, ValidationError
from pydantic_settings import BaseSettings

from vla_control.backends.base import PolicyBackend, PolicyInput
from vla_control.config import build_settings_config
from vla_control.models import StandardizedDeltaAction, Vector3


class OctoBackendError(RuntimeError):
    """Base error for Octo remote backend failures."""


class OctoRemoteUnavailableError(OctoBackendError):
    """Raised when the Octo runtime cannot be reached or is not ready."""


class OctoProtocolError(OctoBackendError):
    """Raised when the Octo runtime returns malformed responses."""


class OctoServiceErrorResponse(BaseModel):
    model_config = ConfigDict(extra="ignore")

    error: str
    details: str | None = None


class OctoHealthResponse(BaseModel):
    model_config = ConfigDict(extra="ignore")

    ok: bool
    app: str
    version: str
    model_id: str


class OctoReadyResponse(BaseModel):
    model_config = ConfigDict(extra="ignore")

    ready: bool
    model_loaded: bool
    model_id: str
    dataset_statistics_keys: list[str] = Field(default_factory=list)
    default_dataset_statistics_key: str | None = None
    image_size: int = Field(ge=1)
    error: str | None = None


class OctoServiceTiming(BaseModel):
    model_config = ConfigDict(extra="ignore")

    decode_image_ms: float
    resize_image_ms: float
    task_ms: float
    predict_ms: float
    total_ms: float


class OctoServicePredictRequest(BaseModel):
    instruction: str
    images_base64: list[str]
    timestep_pad_mask: list[bool]
    image_mime_type: str = "image/jpeg"
    dataset_statistics_key: str | None = None
    request_id: str | None = None
    step_index: int | None = Field(default=None, ge=0)


class OctoServicePredictResponse(BaseModel):
    model_config = ConfigDict(extra="ignore")

    actions: list[list[float]]
    action_dim: int
    action_horizon: int = Field(ge=1)
    dataset_statistics_key_used: str | None = None
    model_id: str
    request_id: str | None = None
    timing_ms: OctoServiceTiming


class OctoRestBackendConfig(BaseSettings):
    model_config = build_settings_config(env_prefix="VLA_OCTO_")

    base_url: str = "http://127.0.0.1:8001"
    timeout_seconds: float = Field(default=120.0, gt=0.0)
    dataset_statistics_key: str | None = None
    history_horizon: int = Field(default=2, ge=1)
    use_temporal_ensembling: bool = True
    temporal_ensemble_exp_weight: float = Field(default=1.0, ge=0.0)
    image_quality: int = Field(default=85, ge=1, le=100)
    image_format: str = "JPEG"


class OctoRestBackend(PolicyBackend):
    def __init__(self, config: OctoRestBackendConfig | None = None) -> None:
        self.config = config or OctoRestBackendConfig()
        self._ready_response: OctoReadyResponse | None = None
        self._image_history: deque[Image.Image] = deque(
            maxlen=self.config.history_horizon
        )
        self._action_chunk_history: deque[list[list[float]]] = deque()
        self._last_instruction: str | None = None

    @property
    def name(self) -> str:
        return "octo-rest"

    def load(self) -> None:
        ready = self.ready()
        if not ready.ready:
            details = ready.error or "Octo runtime reported not ready."
            raise OctoRemoteUnavailableError(details)
        self._ready_response = ready
        self._reset_rollout_buffers()

    def health(self) -> OctoHealthResponse:
        return self._request_model("GET", "/health", OctoHealthResponse)

    def ready(self) -> OctoReadyResponse:
        return self._request_model("GET", "/ready", OctoReadyResponse)

    def predict(self, observation: PolicyInput) -> StandardizedDeltaAction:
        if self._ready_response is None:
            self.load()

        if self._should_reset_rollout_buffers(observation):
            self._reset_rollout_buffers()

        history_images, timestep_pad_mask = self._append_image_and_build_history(
            observation.image
        )
        self._last_instruction = observation.instruction

        request_model = OctoServicePredictRequest(
            instruction=observation.instruction,
            images_base64=[
                self._encode_image_base64(image) for image in history_images
            ],
            timestep_pad_mask=timestep_pad_mask,
            image_mime_type="image/jpeg",
            dataset_statistics_key=self.config.dataset_statistics_key,
            request_id=f"step-{observation.step_index}",
            step_index=observation.step_index,
        )
        response = self._request_model(
            "POST", "/predict", OctoServicePredictResponse, body=request_model
        )
        self._append_action_chunk(response.actions, response.action_horizon)
        return self._link_action(response)

    def _link_action(
        self, response: OctoServicePredictResponse
    ) -> StandardizedDeltaAction:
        if not response.actions:
            raise OctoProtocolError(
                "Expected at least one action from Octo, but received an empty action chunk."
            )

        action = self._select_action(response.actions)
        if len(action) < 4:
            raise OctoProtocolError(
                f"Expected at least 4 action values from the selected Octo action, but received {len(action)}: {action}"
            )

        raw_rotation_delta = None
        if len(action) >= 6:
            raw_rotation_delta = Vector3(x=action[3], y=action[4], z=action[5])

        gripper = action[6] if len(action) >= 7 else action[-1]
        gripper = max(0.0, min(1.0, float(gripper)))

        return StandardizedDeltaAction(
            delta_position=Vector3(x=action[0], y=action[1], z=action[2]),
            gripper=gripper,
            blocking=False,
            raw_rotation_delta=raw_rotation_delta,
            rotation_mode="ignored",
            source=self.name,
            raw_action=action,
            unnorm_key_used=response.dataset_statistics_key_used,
            model_id=response.model_id,
            inference_ms=response.timing_ms.total_ms,
            request_id=response.request_id,
        )

    def _should_reset_rollout_buffers(self, observation: PolicyInput) -> bool:
        return (
            observation.step_index == 0
            or self._last_instruction is None
            or observation.instruction != self._last_instruction
        )

    def _reset_rollout_buffers(self) -> None:
        self._image_history.clear()
        self._action_chunk_history.clear()
        self._last_instruction = None

    def _append_image_and_build_history(
        self, image: Image.Image
    ) -> tuple[list[Image.Image], list[bool]]:
        self._image_history.append(image.copy())

        history_images = list(self._image_history)
        if not history_images:
            raise OctoProtocolError("Octo image history is unexpectedly empty.")

        pad_count = self.config.history_horizon - len(history_images)
        if pad_count > 0:
            history_images = [
                history_images[0].copy() for _ in range(pad_count)
            ] + history_images

        timestep_pad_mask = [False] * max(pad_count, 0) + [True] * min(
            len(self._image_history), self.config.history_horizon
        )
        return history_images, timestep_pad_mask

    def _append_action_chunk(
        self, action_chunk: list[list[float]], action_horizon: int
    ) -> None:
        self._action_chunk_history.append(action_chunk)
        while len(self._action_chunk_history) > action_horizon:
            self._action_chunk_history.popleft()

    def _select_action(self, latest_chunk: list[list[float]]) -> list[float]:
        if not self.config.use_temporal_ensembling:
            return latest_chunk[0]

        num_chunks = len(self._action_chunk_history)
        current_step_predictions = [
            action_chunk[predicted_index]
            for predicted_index, action_chunk in zip(
                range(num_chunks), reversed(self._action_chunk_history)
            )
            if predicted_index < len(action_chunk)
        ]

        if not current_step_predictions:
            raise OctoProtocolError(
                "Temporal ensembling could not find any predicted actions for the current timestep."
            )

        if len(current_step_predictions) == 1:
            return current_step_predictions[0]

        weights = [
            math.exp(-self.config.temporal_ensemble_exp_weight * index)
            for index in range(len(current_step_predictions))
        ]
        weight_total = sum(weights)
        normalized_weights = [weight / weight_total for weight in weights]

        action_length = len(current_step_predictions[0])
        ensembled_action = [0.0] * action_length
        for weight, action in zip(normalized_weights, current_step_predictions):
            if len(action) != action_length:
                raise OctoProtocolError(
                    "Temporal ensembling received action chunks with inconsistent action dimensions."
                )
            for index, value in enumerate(action):
                ensembled_action[index] += weight * float(value)

        return ensembled_action

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
            raise OctoProtocolError(
                f"Octo runtime returned invalid JSON for {path}: {decoded}"
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
            raise OctoRemoteUnavailableError(
                f"Could not reach Octo runtime at {self.config.base_url}: {exc.reason}"
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
    def _build_api_error(exc: HTTPError) -> OctoBackendError:
        payload = exc.read()
        decoded = payload.decode("utf-8", errors="replace") if payload else None

        if payload:
            try:
                parsed = OctoServiceErrorResponse.model_validate_json(payload)
                return OctoBackendError(
                    f"Octo runtime returned HTTP {exc.code}: {parsed.error} ({parsed.details or 'no details'})"
                )
            except ValidationError:
                pass

        return OctoBackendError(
            f"Octo runtime returned HTTP {exc.code}: {decoded or 'no response body'}"
        )


__all__ = [
    "OctoBackendError",
    "OctoHealthResponse",
    "OctoProtocolError",
    "OctoReadyResponse",
    "OctoRemoteUnavailableError",
    "OctoRestBackend",
    "OctoRestBackendConfig",
]
