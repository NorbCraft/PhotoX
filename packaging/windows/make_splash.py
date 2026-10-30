#!/usr/bin/env python3
"""Draws the installer splash (prompt_06 §8): the PhotoX icon, the name and the version.

Shown by the Velopack setup while it installs, so the installer is recognisable and shows its version:

    python3 packaging/windows/make_splash.py 0.9.0 releases/splash.png
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ICON = os.path.join(HERE, "..", "..", "assets", "logo", "photox-icon-256.png")
BLUE = (59, 130, 246)        # the primary blue of the app and the icon (#3B82F6)
INK = (27, 31, 36)
GREY = (91, 99, 109)
FONTS_BOLD = ["C:/Windows/Fonts/segoeuib.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "DejaVuSans-Bold.ttf"]
FONTS = ["C:/Windows/Fonts/segoeui.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "DejaVuSans.ttf"]


def font(candidates, size):
    for path in candidates:
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            continue
    return ImageFont.load_default()


def main(version, out):
    w, h = 520, 300
    img = Image.new("RGB", (w, h), (246, 249, 253))
    draw = ImageDraw.Draw(img)
    draw.rectangle([0, h - 6, w, h], fill=BLUE)
    icon = Image.open(ICON).convert("RGBA").resize((128, 128), Image.LANCZOS)
    img.paste(icon, (40, 70), icon)
    name = font(FONTS_BOLD, 52)
    draw.text((196, 78), "Photo", font=name, fill=INK)
    x = 196 + draw.textlength("Photo", font=name)
    draw.text((x, 78), "X", font=name, fill=BLUE)
    draw.text((198, 148), f"Version {version}", font=font(FONTS, 24), fill=GREY)
    draw.text((198, 186), "Installing…", font=font(FONTS, 18), fill=GREY)
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    img.save(out)
    print("wrote", out)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit("usage: make_splash.py VERSION OUT.png")
    main(sys.argv[1], sys.argv[2])
