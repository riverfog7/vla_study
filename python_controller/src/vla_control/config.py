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
            "env_nested_delimiter": "__",
            "extra": "ignore",
            "validate_default": True,
        },
    )


class UnityConfig(BaseSettings):
    model_config = build_settings_config(env_prefix="VLA_CONTROL_")

    host: str = Field(default="127.0.0.1")
    port: int = Field(default=8080, ge=1, le=65535)
    timeout_seconds: float = Field(default=10.0, gt=0.0)
    default_camera_name: str = Field(default="main", min_length=1)
    default_image_width: int = Field(default=256, ge=1)
    default_image_height: int = Field(default=256, ge=1)
    default_image_quality: int = Field(default=80, ge=1, le=100)

    @computed_field
    @property
    def base_url(self) -> str:
        return f"http://{self.host}:{self.port}"
