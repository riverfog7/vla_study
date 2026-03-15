from __future__ import annotations

from typing import cast

from pydantic import Field, computed_field
from pydantic_settings import BaseSettings, SettingsConfigDict


def build_settings_config(*, env_prefix: str) -> SettingsConfigDict:
    return cast(
        SettingsConfigDict,
        {
            "env_prefix": env_prefix,
            "env_file": ".env",
            "env_file_encoding": "utf-8",
            "extra": "ignore",
            "validate_default": True,
        },
    )


class OctoRuntimeConfig(BaseSettings):
    model_config = build_settings_config(env_prefix="OCTO_RUNTIME_")

    app_name: str = "octo-runtime"
    app_version: str = "0.1.0"
    host: str = "0.0.0.0"
    port: int = Field(default=8001, ge=1, le=65535)
    model_path: str = "hf://rail-berkeley/octo-small-1.5"
    default_dataset_statistics_key: str | None = "bridge_dataset"
    image_size: int = Field(default=256, ge=1)
    load_on_startup: bool = True
    max_request_bytes: int = Field(default=8_000_000, ge=1024)
    request_timeout_seconds: float = Field(default=120.0, gt=0.0)
    log_level: str = "info"

    @computed_field
    @property
    def model_id(self) -> str:
        return self.model_path
