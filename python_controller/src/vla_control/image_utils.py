from __future__ import annotations

from io import BytesIO
from pathlib import Path

from PIL import Image, UnidentifiedImageError

from vla_control.errors import UnityProtocolError


def decode_jpeg_bytes(data: bytes) -> Image.Image:
    try:
        with Image.open(BytesIO(data)) as image:
            image.load()
            return image.convert("RGB") if image.mode != "RGB" else image.copy()
    except UnidentifiedImageError as exc:
        raise UnityProtocolError("Unity returned bytes that are not a valid JPEG image.") from exc


def save_image(image: Image.Image, path: Path) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, format="JPEG")
    return path
