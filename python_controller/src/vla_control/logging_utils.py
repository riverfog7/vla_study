from __future__ import annotations

from datetime import datetime
from pathlib import Path

from PIL import Image
from pydantic import BaseModel

from vla_control.image_utils import save_image


class RolloutArtifactWriter:
    def __init__(self, run_dir: Path, *, save_images: bool = True) -> None:
        self.run_dir = run_dir
        self.save_images = save_images
        self.steps_dir = self.run_dir / "steps"
        self.steps_dir.mkdir(parents=True, exist_ok=True)

    @classmethod
    def create(cls, artifact_root: Path, backend_name: str, run_name: str | None = None, *, save_images: bool = True) -> "RolloutArtifactWriter":
        timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        name_parts = [timestamp, backend_name]
        if run_name:
            sanitized = "".join(character if character.isalnum() or character in {"-", "_"} else "-" for character in run_name).strip("-")
            if sanitized:
                name_parts.append(sanitized)
        run_dir = artifact_root / "_".join(name_parts)
        run_dir.mkdir(parents=True, exist_ok=True)
        return cls(run_dir, save_images=save_images)

    def write_step(self, step_index: int, record: BaseModel, image: Image.Image | None = None) -> Path | None:
        image_path: Path | None = None
        if not self.save_images or image is None:
            image_path = None
        else:
            image_path = self.steps_dir / f"step_{step_index:03d}.jpg"
            save_image(image, image_path)

        if hasattr(record, "image_path"):
            setattr(record, "image_path", image_path)

        json_path = self.steps_dir / f"step_{step_index:03d}.json"
        json_path.write_text(record.model_dump_json(indent=2), encoding="utf-8")
        return image_path

    def write_summary(self, summary: BaseModel) -> Path:
        summary_path = self.run_dir / "rollout_summary.json"
        summary_path.write_text(summary.model_dump_json(indent=2), encoding="utf-8")
        return summary_path
