from __future__ import annotations

import base64
from io import BytesIO
from threading import Lock
from time import perf_counter
from typing import Any, cast

from openvla_runtime.config import OpenVLARuntimeConfig
from openvla_runtime.errors import (
    InvalidUnnormKeyError,
    ModelLoadError,
    RuntimeNotReadyError,
    RuntimeRequestError,
)
from openvla_runtime.models import (
    PredictRequest,
    PredictResponse,
    ReadyResponse,
    TimingBreakdown,
)
from openvla_runtime.prompting import build_openvla_prompt


class OpenVLAModelRuntime:
    def __init__(self, config: OpenVLARuntimeConfig) -> None:
        self.config = config
        self._processor: Any | None = None
        self._model: Any | None = None
        self._lock = Lock()
        self._load_error: str | None = None
        self._available_unnorm_keys: list[str] = []

    @property
    def load_error(self) -> str | None:
        return self._load_error

    @property
    def available_unnorm_keys(self) -> list[str]:
        return list(self._available_unnorm_keys)

    @property
    def is_ready(self) -> bool:
        return (
            self._processor is not None
            and self._model is not None
            and self._load_error is None
        )

    def load(self) -> None:
        with self._lock:
            if self.is_ready:
                return

            self._load_error = None

            try:
                import torch  # type: ignore[import-not-found]
                from transformers import AutoModelForVision2Seq, AutoProcessor  # type: ignore[import-not-found]

                dtype = self._resolve_torch_dtype(torch)
                model_kwargs: dict[str, Any] = {
                    "torch_dtype": dtype,
                    "low_cpu_mem_usage": True,
                    "trust_remote_code": self.config.trust_remote_code,
                }
                if self.config.attn_implementation:
                    model_kwargs["attn_implementation"] = (
                        self.config.attn_implementation
                    )

                self._processor = AutoProcessor.from_pretrained(
                    self.config.model_path,
                    trust_remote_code=self.config.trust_remote_code,
                )
                self._model = AutoModelForVision2Seq.from_pretrained(
                    self.config.model_path, **model_kwargs
                ).to(self.config.device)
                norm_stats = getattr(self._model, "norm_stats", {}) or {}
                self._available_unnorm_keys = sorted(
                    str(key) for key in norm_stats.keys()
                )
            except (
                Exception
            ) as exc:  # pragma: no cover - depends on runtime environment
                self._processor = None
                self._model = None
                self._available_unnorm_keys = []
                self._load_error = str(exc)
                raise ModelLoadError(
                    f"Failed to load OpenVLA model '{self.config.model_path}': {exc}"
                ) from exc

    def get_ready_response(self) -> ReadyResponse:
        return ReadyResponse(
            ready=self.is_ready,
            model_loaded=self._model is not None,
            processor_loaded=self._processor is not None,
            model_id=self.config.model_id,
            device=self.config.device,
            dtype=self.config.torch_dtype,
            available_unnorm_keys=self.available_unnorm_keys,
            default_unnorm_key=self.config.default_unnorm_key,
            error=self._load_error,
        )

    def predict(self, request: PredictRequest) -> PredictResponse:
        if not self.is_ready:
            raise RuntimeNotReadyError(
                "OpenVLA runtime is not ready. Check GET /ready for details."
            )

        start = perf_counter()
        image = self._decode_image(request.image_base64, request.image_mime_type)
        decode_elapsed_ms = (perf_counter() - start) * 1000.0

        prompt = build_openvla_prompt(request.instruction)
        unnorm_key = self._resolve_unnorm_key(request.unnorm_key)
        processor = cast(Any, self._processor)
        model = cast(Any, self._model)

        processor_start = perf_counter()
        import torch  # type: ignore[import-not-found]

        inputs = processor(prompt, image).to(
            self.config.device, dtype=self._resolve_torch_dtype(torch)
        )
        processor_elapsed_ms = (perf_counter() - processor_start) * 1000.0

        predict_start = perf_counter()
        with torch.inference_mode():
            action = model.predict_action(
                **inputs, unnorm_key=unnorm_key, do_sample=False
            )
        predict_elapsed_ms = (perf_counter() - predict_start) * 1000.0

        import numpy as np  # type: ignore[import-not-found]

        action_list = np.asarray(action, dtype=float).tolist()

        return PredictResponse(
            action=action_list,
            action_dim=len(action_list),
            unnorm_key_used=unnorm_key,
            prompt=prompt,
            model_id=self.config.model_id,
            request_id=request.request_id,
            timing_ms=TimingBreakdown(
                decode_image_ms=decode_elapsed_ms,
                processor_ms=processor_elapsed_ms,
                predict_ms=predict_elapsed_ms,
                total_ms=(perf_counter() - start) * 1000.0,
            ),
        )

    def _resolve_unnorm_key(self, requested_key: str | None) -> str | None:
        if not self.available_unnorm_keys:
            return requested_key or self.config.default_unnorm_key

        if requested_key:
            if requested_key not in self.available_unnorm_keys:
                raise InvalidUnnormKeyError(
                    f"Requested unnorm_key '{requested_key}' is not available. Choose from {self.available_unnorm_keys}."
                )
            return requested_key

        if self.config.default_unnorm_key:
            if self.config.default_unnorm_key not in self.available_unnorm_keys:
                raise InvalidUnnormKeyError(
                    f"Configured default_unnorm_key '{self.config.default_unnorm_key}' is not available. Choose from {self.available_unnorm_keys}."
                )
            return self.config.default_unnorm_key

        if len(self.available_unnorm_keys) == 1:
            return self.available_unnorm_keys[0]

        raise InvalidUnnormKeyError(
            f"This model exposes multiple normalization keys {self.available_unnorm_keys}. Provide unnorm_key in the request or set OPENVLA_RUNTIME_DEFAULT_UNNORM_KEY."
        )

    def _decode_image(self, image_base64: str, image_mime_type: str) -> Any:
        from PIL import Image, UnidentifiedImageError  # type: ignore[import-not-found]

        allowed_mime_types = {"image/jpeg", "image/jpg", "image/png"}
        if image_mime_type.lower() not in allowed_mime_types:
            raise RuntimeRequestError(
                f"Unsupported image_mime_type '{image_mime_type}'. Use one of {sorted(allowed_mime_types)}."
            )

        try:
            payload = base64.b64decode(image_base64, validate=True)
        except Exception as exc:
            raise RuntimeRequestError("image_base64 is not valid base64 data.") from exc

        if len(payload) > self.config.max_request_bytes:
            raise RuntimeRequestError(
                f"Decoded image payload is {len(payload)} bytes, which exceeds OPENVLA_RUNTIME_MAX_REQUEST_BYTES={self.config.max_request_bytes}."
            )

        try:
            with Image.open(BytesIO(payload)) as image:
                image.load()
                return image.convert("RGB")
        except UnidentifiedImageError as exc:
            raise RuntimeRequestError(
                "Decoded image payload is not a supported image file."
            ) from exc

    def _resolve_torch_dtype(self, torch_module: Any) -> Any:
        if self.config.torch_dtype == "bfloat16":
            return torch_module.bfloat16
        if self.config.torch_dtype == "float16":
            return torch_module.float16
        return torch_module.float32
