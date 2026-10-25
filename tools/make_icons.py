#!/usr/bin/env python3
"""Render Charmeleon's icon files from its drawing, CharmeleonGUI/Resources/Charmeleon.svg.

Writes, next to the drawing:

* Charmeleon.png: 512 x 512 pixels, for the About window and the readme;
* Charmeleon.ico: 16 to 256 pixels, for Charmeleon.exe, shortcuts and the installer;

and embeds the new .ico in MainGUI.resx, where the main window's title-bar icon lives.

Every image has a transparent background. From 32 pixels up an image shows the whole
figure; at 16 to 24 pixels, where the whole figure would be a green blob, it shows a
close-up of the head (HEAD_VIEW), so that the face stays recognizable.

The drawing was traced (with vtracer) from a hand-drawn JPEG; edit the SVG itself, then:

    pip install PySide6-Essentials Pillow
    python tools/make_icons.py
"""

from __future__ import annotations

import base64
import io
import os
import re
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
RESOURCES = ROOT / "CharmeleonGUI" / "Resources"
DRAWING = RESOURCES / "Charmeleon.svg"
PNG = RESOURCES / "Charmeleon.png"
ICO = RESOURCES / "Charmeleon.ico"
RESX = ROOT / "CharmeleonGUI" / "MainGUI.resx"

PNG_SIZE = 512
# The sizes Windows picks from for 100% to 200% display scaling.
ICO_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)
HEAD_SIZES = {16, 20, 24}
# The head, in the drawing's own coordinates: x, y, width, height (see its viewBox).
HEAD_VIEW = (20, 10, 900, 900)


def main() -> None:
    """Writes the .png and the .ico, and embeds the .ico in MainGUI.resx."""
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")  # no window needed, so no screen either
    from PySide6.QtGui import QGuiApplication  # imported after the line above, which it reads

    _app = QGuiApplication.instance() or QGuiApplication([])
    render(PNG_SIZE).save(PNG, optimize=True)
    images = {size: render(size, HEAD_VIEW if size in HEAD_SIZES else None) for size in ICO_SIZES}
    ico = ico_bytes(images)
    ICO.write_bytes(ico)
    embed_window_icon(RESX, ico)
    print(f"Wrote {PNG.name}, {ICO.name} ({len(ico) // 1024} KB) and the window icon in {RESX.name}.")


def render(size: int, view: tuple[float, float, float, float] | None = None) -> Image.Image:
    """Renders the drawing into a square image with a transparent background.

    Args:
        size: The image's width and height, in pixels.
        view: The part of the drawing to show (x, y, width, height); None for all of it.

    Returns:
        The image, as a Pillow RGBA image.
    """
    from PIL import Image
    from PySide6.QtCore import QRectF, Qt
    from PySide6.QtGui import QImage, QPainter
    from PySide6.QtSvg import QSvgRenderer

    renderer = QSvgRenderer(str(DRAWING))
    if not renderer.isValid():
        raise SystemExit(f"{DRAWING} is not a valid SVG drawing.")
    if view is not None:
        renderer.setViewBox(QRectF(*view))
    image = QImage(size, size, QImage.Format.Format_RGBA8888)
    image.fill(Qt.GlobalColor.transparent)
    painter = QPainter(image)
    painter.setRenderHint(QPainter.RenderHint.Antialiasing)
    painter.setRenderHint(QPainter.RenderHint.SmoothPixmapTransform)
    renderer.render(painter, QRectF(0, 0, size, size))
    painter.end()
    # RGBA8888 is byte-ordered R, G, B, A everywhere; rows may be padded.
    return Image.frombuffer("RGBA", (size, size), bytes(image.constBits()), "raw", "RGBA", image.bytesPerLine(), 1)


def ico_bytes(images: dict[int, Image.Image]) -> bytes:
    """A Windows .ico holding one image per size, each stored as PNG (as Windows Vista and later read).

    Args:
        images: The images, by size; each drawn at its own size, which is sharper than one image
            scaled down.

    Returns:
        The .ico file's content.
    """
    largest = max(images)
    buffer = io.BytesIO()
    # Pillow takes the largest image as the base and the others, matched by size, as
    # replacements for what it would otherwise scale down.
    images[largest].save(
        buffer,
        format="ICO",
        sizes=[(size, size) for size in sorted(images)],
        append_images=[images[size] for size in sorted(images) if size != largest],
    )
    return buffer.getvalue()


def embed_window_icon(resx: Path, ico: bytes) -> None:
    """Replaces the main window's icon in its .resx file, formatted as the Visual Studio designer does.

    Args:
        resx: MainGUI.resx.
        ico: The .ico file's content.
    """
    encoded = base64.b64encode(ico).decode("ascii")
    lines = "\n".join("        " + encoded[i : i + 80] for i in range(0, len(encoded), 80))
    text = resx.read_text(encoding="utf-8")
    pattern = re.compile(r'(<data name="\$this\.Icon" [^>]*>\s*<value>\n)(.*?)(\n</value>)', re.DOTALL)
    text, count = pattern.subn(lambda match: match[1] + lines + match[3], text)
    if count != 1:
        raise SystemExit(f"Found no window icon ($this.Icon) in {resx}.")
    resx.write_text(text, encoding="utf-8")


if __name__ == "__main__":
    main()
