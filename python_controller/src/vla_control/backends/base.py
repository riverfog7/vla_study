from __future__ import annotations

from abc import ABC, abstractmethod

from PIL import Image
from pydantic import BaseModel, ConfigDict, Field

from vla_control.models import StateResponse


class PolicyInput(BaseModel):
    model_config = ConfigDict(arbitrary_types_allowed=True, extra="forbid", validate_assignment=True)

    image: Image.Image
    instruction: str = Field(min_length=1)
    state: StateResponse
    step_index: int = Field(default=0, ge=0)


class PolicyBackend(ABC):
    @property
    @abstractmethod
    def name(self) -> str:
        raise NotImplementedError

    def load(self) -> None:
        return None

    @abstractmethod
    def predict(self, observation: PolicyInput) -> BaseModel:
        raise NotImplementedError

    def should_stop(self, state: StateResponse, step_index: int) -> bool:
        return False

    def close(self) -> None:
        return None
