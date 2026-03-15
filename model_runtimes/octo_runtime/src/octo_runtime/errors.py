from __future__ import annotations


class OctoRuntimeError(RuntimeError):
    """Base error for runtime-server failures."""


class RuntimeNotReadyError(OctoRuntimeError):
    """Raised when inference is requested before the model is ready."""


class RuntimeRequestError(OctoRuntimeError):
    """Raised when a request payload is invalid."""


class ModelLoadError(OctoRuntimeError):
    """Raised when the Octo model cannot be loaded."""


class InvalidDatasetStatisticsKeyError(RuntimeRequestError):
    """Raised when the request or config refers to an unknown dataset statistics key."""
