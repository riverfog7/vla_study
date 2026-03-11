# pyright: reportMissingImports=false
from __future__ import annotations

from typing import Literal, cast

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


class OpenVLARuntimeConfig(BaseSettings):
    model_config = build_settings_config(env_prefix="OPENVLA_RUNTIME_")

    app_name: str = "openvla-runtime"
    app_version: str = "0.1.0"
    host: str = "0.0.0.0"
    port: int = Field(default=8000, ge=1, le=65535)
    model_path: str = "openvla/openvla-7b"
    device: str = "cuda:0"
    torch_dtype: Literal["bfloat16", "float16", "float32"] = "bfloat16"
    attn_implementation: str | None = "flash_attention_2"
    trust_remote_code: bool = True
    default_unnorm_key: str | None = None
    load_on_startup: bool = True
    max_request_bytes: int = Field(default=8_000_000, ge=1024)
    request_timeout_seconds: float = Field(default=120.0, gt=0.0)
    log_level: str = "info"

    @computed_field
    @property
    def model_id(self) -> str:
        return self.model_path
