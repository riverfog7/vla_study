from __future__ import annotations

import json
from typing import Any, TypeVar
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urlencode
from urllib.request import Request, urlopen

from PIL import Image
from pydantic import BaseModel, ValidationError

from vla_control.config import UnityConfig
from vla_control.errors import UnityApiError, UnityProtocolError, UnityTransportError
from vla_control.image_utils import decode_jpeg_bytes
from vla_control.models import (
    CameraRequest,
    ErrorResponse,
    HealthResponse,
    MoveToPoseResponse,
    PoseCommand,
    ResetResponse,
    StateResponse,
    StepRequest,
    StepResponse,
)

ResponseModelT = TypeVar("ResponseModelT", bound=BaseModel)


class UnityClient:
    def __init__(
        self,
        config: UnityConfig | None = None,
        *,
        host: str | None = None,
        port: int | None = None,
        timeout_seconds: float | None = None,
    ) -> None:
        resolved_config = config or UnityConfig()
        if host is not None or port is not None or timeout_seconds is not None:
            resolved_config = resolved_config.model_copy(
                update={
                    key: value
                    for key, value in {
                        "host": host,
                        "port": port,
                        "timeout_seconds": timeout_seconds,
                    }.items()
                    if value is not None
                }
            )

        self.config = resolved_config

    def health(self) -> HealthResponse:
        return self._request_model("GET", "/v1/health", HealthResponse)

    def get_state(self) -> StateResponse:
        return self._request_model("GET", "/v1/state", StateResponse)

    def get_camera_bytes(
        self,
        camera_name: str | None = None,
        *,
        width: int | None = None,
        height: int | None = None,
        quality: int | None = None,
    ) -> bytes:
        request_model = CameraRequest(
            camera_name=camera_name or self.config.default_camera_name,
            width=width or self.config.default_image_width,
            height=height or self.config.default_image_height,
            quality=quality or self.config.default_image_quality,
        )
        camera_name_path = quote(request_model.camera_name, safe="")
        return self._request_bytes(
            "GET",
            f"/v1/camera/{camera_name_path}.jpg",
            query=request_model.to_query_params(),
        )

    def get_camera(
        self,
        camera_name: str | None = None,
        *,
        width: int | None = None,
        height: int | None = None,
        quality: int | None = None,
    ) -> Image.Image:
        return decode_jpeg_bytes(
            self.get_camera_bytes(
                camera_name,
                width=width,
                height=height,
                quality=quality,
            )
        )

    def move_to_pose(self, command: PoseCommand | dict[str, Any]) -> MoveToPoseResponse:
        request_model = command if isinstance(command, PoseCommand) else PoseCommand.model_validate(command)
        return self._request_model("POST", "/v1/robot/move_to_pose", MoveToPoseResponse, body=request_model)

    def step(self, steps: int, dt: float) -> StepResponse:
        return self._request_model("POST", "/v1/sim/step", StepResponse, body=StepRequest(steps=steps, dt=dt))

    def step_control_interval(self) -> StepResponse:
        state = self.get_state()
        return self.step(state.steps_per_action, state.physics_dt)

    def reset(self) -> ResetResponse:
        return self._request_model("POST", "/v1/reset", ResetResponse, empty_post_body=True)

    def _request_model(
        self,
        method: str,
        path: str,
        model_type: type[ResponseModelT],
        *,
        query: dict[str, str] | None = None,
        body: BaseModel | None = None,
        empty_post_body: bool = False,
    ) -> ResponseModelT:
        payload = self._request_bytes(method, path, query=query, body=body, empty_post_body=empty_post_body)
        try:
            return model_type.model_validate_json(payload)
        except ValidationError as exc:
            decoded = payload.decode("utf-8", errors="replace")
            raise UnityProtocolError(f"Unity returned invalid JSON for {path}: {decoded}") from exc

    def _request_bytes(
        self,
        method: str,
        path: str,
        *,
        query: dict[str, str] | None = None,
        body: BaseModel | None = None,
        empty_post_body: bool = False,
    ) -> bytes:
        headers: dict[str, str] = {}
        data: bytes | None = None

        if body is not None:
            headers["Content-Type"] = "application/json"
            data = json.dumps(body.model_dump(mode="json")).encode("utf-8")
        elif empty_post_body:
            data = b""

        request = Request(
            self._build_url(path, query=query),
            data=data,
            headers=headers,
            method=method,
        )

        try:
            with urlopen(request, timeout=self.config.timeout_seconds) as response:
                return response.read()
        except HTTPError as exc:
            self._raise_api_error(exc)
        except URLError as exc:
            raise UnityTransportError(f"Could not reach Unity at {self.config.base_url}: {exc.reason}") from exc

    def _build_url(self, path: str, *, query: dict[str, str] | None = None) -> str:
        normalized_path = path if path.startswith("/") else f"/{path}"
        url = f"{self.config.base_url}{normalized_path}"
        if query:
            return f"{url}?{urlencode(query)}"
        return url

    @staticmethod
    def _raise_api_error(exc: HTTPError) -> None:
        payload = exc.read()
        decoded = payload.decode("utf-8", errors="replace") if payload else None

        error = None
        details = None
        if payload:
            try:
                parsed = ErrorResponse.model_validate_json(payload)
                error = parsed.error
                details = parsed.details
            except ValidationError:
                details = decoded

        raise UnityApiError(status_code=exc.code, error=error, details=details, body=decoded) from exc
