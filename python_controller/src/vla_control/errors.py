from __future__ import annotations


class UnityClientError(RuntimeError):
    """Base error for Unity client failures."""


class UnityTransportError(UnityClientError):
    """Raised when the Unity server cannot be reached."""


class UnityProtocolError(UnityClientError):
    """Raised when the Unity server returns malformed content."""


class UnityApiError(UnityClientError):
    """Raised when the Unity server returns a non-success response."""

    def __init__(self, status_code: int, error: str | None = None, details: str | None = None, body: str | None = None):
        self.status_code = status_code
        self.error = error
        self.details = details
        self.body = body

        if error and details:
            message = f"Unity API error {status_code}: {error} ({details})"
        elif error:
            message = f"Unity API error {status_code}: {error}"
        elif details:
            message = f"Unity API error {status_code}: {details}"
        elif body:
            message = f"Unity API error {status_code}: {body}"
        else:
            message = f"Unity API error {status_code}"

        super().__init__(message)
