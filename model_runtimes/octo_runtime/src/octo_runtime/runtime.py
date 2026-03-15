from __future__ import annotations

import base64
import os
from io import BytesIO
from threading import Lock
from time import perf_counter
from typing import Any, cast

from octo_runtime.config import OctoRuntimeConfig
from octo_runtime.errors import (
    InvalidDatasetStatisticsKeyError,
    ModelLoadError,
    RuntimeNotReadyError,
    RuntimeRequestError,
)
from octo_runtime.models import (
    PredictRequest,
    PredictResponse,
    ReadyResponse,
    TimingBreakdown,
)


class OctoModelRuntime:
    def __init__(self, config: OctoRuntimeConfig) -> None:
        self.config = config
        self._model: Any | None = None
        self._lock = Lock()
        self._load_error: str | None = None
        self._dataset_statistics_keys: list[str] = []

    @property
    def load_error(self) -> str | None:
        return self._load_error

    @property
    def dataset_statistics_keys(self) -> list[str]:
        return list(self._dataset_statistics_keys)

    @property
    def is_ready(self) -> bool:
        return self._model is not None and self._load_error is None

    def load(self) -> None:
        with self._lock:
            if self.is_ready:
                return

            self._load_error = None

            try:
                os.environ.setdefault("TOKENIZERS_PARALLELISM", "false")

                from octo.model.octo_model import OctoModel  # type: ignore[import-not-found]

                self._model = OctoModel.load_pretrained(self.config.model_path)
                dataset_statistics = (
                    getattr(self._model, "dataset_statistics", {}) or {}
                )
                self._dataset_statistics_keys = sorted(
                    str(key) for key in dataset_statistics.keys()
                )
            except (
                Exception
            ) as exc:  # pragma: no cover - depends on runtime environment
                self._model = None
                self._dataset_statistics_keys = []
                self._load_error = str(exc)
                raise ModelLoadError(
                    f"Failed to load Octo model '{self.config.model_path}': {exc}"
                ) from exc

    def get_ready_response(self) -> ReadyResponse:
        return ReadyResponse(
            ready=self.is_ready,
            model_loaded=self._model is not None,
            model_id=self.config.model_id,
            dataset_statistics_keys=self.dataset_statistics_keys,
            default_dataset_statistics_key=self.config.default_dataset_statistics_key,
            image_size=self.config.image_size,
            error=self._load_error,
        )

    def predict(self, request: PredictRequest) -> PredictResponse:
        if not self.is_ready:
            raise RuntimeNotReadyError(
                "Octo runtime is not ready. Check GET /ready for details."
            )

        if len(request.images_base64) != len(request.timestep_pad_mask):
            raise RuntimeRequestError(
                "images_base64 and timestep_pad_mask must have the same length."
            )

        start = perf_counter()
        images = [
            self._decode_image(image_base64, request.image_mime_type)
            for image_base64 in request.images_base64
        ]
        decode_elapsed_ms = (perf_counter() - start) * 1000.0

        resize_start = perf_counter()
        resized_images = [
            image.resize((self.config.image_size, self.config.image_size))
            for image in images
        ]
        resize_elapsed_ms = (perf_counter() - resize_start) * 1000.0

        model = cast(Any, self._model)
        stats_key = self._resolve_dataset_statistics_key(request.dataset_statistics_key)

        task_start = perf_counter()
        import numpy as np  # type: ignore[import-not-found]

        observation = {
            "image_primary": np.stack(
                [np.asarray(image, dtype=np.uint8) for image in resized_images], axis=0
            )[None, ...],
            "timestep_pad_mask": np.asarray(request.timestep_pad_mask, dtype=bool)[
                None, ...
            ],
        }
        task = model.create_tasks(texts=[request.instruction.strip()])
        task_elapsed_ms = (perf_counter() - task_start) * 1000.0

        predict_start = perf_counter()
        import jax  # type: ignore[import-not-found]

        rng = jax.random.PRNGKey(request.step_index or 0)
        action_chunk = model.sample_actions(
            observation,
            task,
            unnormalization_statistics=model.dataset_statistics[stats_key]["action"],
            rng=rng,
        )
        predict_elapsed_ms = (perf_counter() - predict_start) * 1000.0

        action_array = np.asarray(action_chunk, dtype=float)
        if (
            action_array.ndim != 3
            or action_array.shape[0] < 1
            or action_array.shape[1] < 1
        ):
            raise RuntimeRequestError(
                f"Octo returned an unexpected action shape {action_array.shape}; expected [batch, horizon, action_dim]."
            )

        action_chunk_list = action_array[0].tolist()

        return PredictResponse(
            actions=action_chunk_list,
            action_dim=int(action_array.shape[2]),
            action_horizon=int(action_array.shape[1]),
            dataset_statistics_key_used=stats_key,
            model_id=self.config.model_id,
            request_id=request.request_id,
            timing_ms=TimingBreakdown(
                decode_image_ms=decode_elapsed_ms,
                resize_image_ms=resize_elapsed_ms,
                task_ms=task_elapsed_ms,
                predict_ms=predict_elapsed_ms,
                total_ms=(perf_counter() - start) * 1000.0,
            ),
        )

    def _resolve_dataset_statistics_key(self, requested_key: str | None) -> str:
        if not self.dataset_statistics_keys:
            if requested_key:
                return requested_key
            if self.config.default_dataset_statistics_key:
                return self.config.default_dataset_statistics_key
            raise InvalidDatasetStatisticsKeyError(
                "Octo model did not expose dataset statistics keys, and no dataset_statistics_key was provided."
            )

        if requested_key:
            if requested_key not in self.dataset_statistics_keys:
                raise InvalidDatasetStatisticsKeyError(
                    f"Requested dataset_statistics_key '{requested_key}' is not available. Choose from {self.dataset_statistics_keys}."
                )
            return requested_key

        if self.config.default_dataset_statistics_key:
            if (
                self.config.default_dataset_statistics_key
                not in self.dataset_statistics_keys
            ):
                raise InvalidDatasetStatisticsKeyError(
                    f"Configured default_dataset_statistics_key '{self.config.default_dataset_statistics_key}' is not available. Choose from {self.dataset_statistics_keys}."
                )
            return self.config.default_dataset_statistics_key

        if len(self.dataset_statistics_keys) == 1:
            return self.dataset_statistics_keys[0]

        raise InvalidDatasetStatisticsKeyError(
            f"This model exposes multiple dataset statistics keys {self.dataset_statistics_keys}. Provide dataset_statistics_key in the request or set OCTO_RUNTIME_DEFAULT_DATASET_STATISTICS_KEY."
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
                f"Decoded image payload is {len(payload)} bytes, which exceeds OCTO_RUNTIME_MAX_REQUEST_BYTES={self.config.max_request_bytes}."
            )

        try:
            with Image.open(BytesIO(payload)) as image:
                image.load()
                return image.convert("RGB")
        except UnidentifiedImageError as exc:
            raise RuntimeRequestError(
                "Decoded image payload is not a supported image file."
            ) from exc
