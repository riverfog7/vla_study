from __future__ import annotations


class OpenVLARuntimeError(RuntimeError):
    """Base error for runtime-server failures."""


class RuntimeNotReadyError(OpenVLARuntimeError):
    """Raised when inference is requested before the model is ready."""


class RuntimeRequestError(OpenVLARuntimeError):
    """Raised when a request payload is invalid."""


class ModelLoadError(OpenVLARuntimeError):
    """Raised when the OpenVLA model cannot be loaded."""


class InvalidUnnormKeyError(RuntimeRequestError):
    """Raised when the request or config refers to an unknown normalization key."""
