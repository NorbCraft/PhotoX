#!/usr/bin/env python3
"""Builds the PhotoX icon exports from the SVG masters (prompt_04 §2).

photox-icon.svg is the master; photox-icon-small.svg is its 16-24 px variant (thicker lines, no letters).
Renders PNGs with headless Chromium and writes the multi-resolution Windows icon with Pillow:

    python3 assets/logo/build_icons.py
"""
import os
import shutil
import subprocess
import sys
import tempfile

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SIZES = [16, 24, 32, 48, 64, 128, 256, 512]
ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]
SMALL = {16, 24}


def browser():
    for name in ("chromium", "chromium-browser", "google-chrome"):
        if shutil.which(name):
            return name
    sys.exit("Chromium/Chrome is needed to render the SVG files.")


def render(exe, svg, size, out):
    # A window smaller than 256 px can hang headless Chromium: render in a larger window and crop.
    win = max(size, 256)
    with tempfile.TemporaryDirectory() as tmp:
        shutil.copy(svg, os.path.join(tmp, "icon.svg"))
        html = os.path.join(tmp, "icon.html")
        with open(html, "w", encoding="utf-8") as f:
            f.write(f'<html><body style="margin:0;background:transparent"><img src="icon.svg" style="width:{size}px;height:{size}px;display:block"></body></html>')
        shot = os.path.join(tmp, "shot.png")
        subprocess.run([exe, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--default-background-color=00000000",
                        f"--window-size={win},{win}", "--force-device-scale-factor=1", f"--screenshot={shot}", "file://" + html],
                       check=True, timeout=60, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        Image.open(shot).convert("RGBA").crop((0, 0, size, size)).save(out)


def main():
    exe = browser()
    images = {}
    for size in SIZES:
        svg = os.path.join(HERE, "photox-icon-small.svg" if size in SMALL else "photox-icon.svg")
        out = os.path.join(HERE, f"photox-icon-{size}.png")
        render(exe, svg, size, out)
        images[size] = Image.open(out)
        print("wrote", os.path.relpath(out))
    ico = os.path.join(HERE, "photox.ico")
    largest = images[max(ICO_SIZES)]
    largest.save(ico, format="ICO", sizes=[(s, s) for s in ICO_SIZES], append_images=[images[s] for s in ICO_SIZES if s != max(ICO_SIZES)])
    print("wrote", os.path.relpath(ico), [s for s in ICO_SIZES])


if __name__ == "__main__":
    main()
