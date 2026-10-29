#!/usr/bin/env python3
"""PhotoX concept UI mockup generator.

Documentation tooling only: this is NOT application code and is not part of the
PhotoX build. It writes editable SVG sources to docs/mockups/svg/ and, when a
Chromium/Chrome browser is available, renders them to PNG in docs/mockups/.

    python3 docs/mockups/tools/build_mockups.py

All photographs in the mockups are synthetic vector illustrations.
"""
import math
import os
import random
import shutil
import subprocess
from xml.sax.saxutils import escape

HERE = os.path.dirname(os.path.abspath(__file__))
SVG_DIR = os.path.normpath(os.path.join(HERE, "..", "svg"))
PNG_DIR = os.path.normpath(os.path.join(HERE, ".."))

WIN_W, WIN_H = 1600, 1000  # application window, 16:10
LABEL_H = 44  # "concept" label strip above the window
FONT = "'Segoe UI Variable Text','Segoe UI','Inter','Ubuntu Sans','Noto Sans',sans-serif"

# Neutral (hue-free) greys around the photo, one accent colour, three status colours.
C = {
    "bg": "#1E1E1E",
    "bar": "#191919",
    "panel": "#242424",
    "panel2": "#2B2B2B",
    "raised": "#353535",
    "border": "#333333",
    "border2": "#474747",
    "canvas": "#151515",
    "track": "#4A4A4A",
    "text": "#EDEDED",
    "text2": "#A8A8A8",
    "text3": "#7A7A7A",
    "accent": "#3B82F6",
    "accent_tint": "#1D3557",
    "accent_tint_b": "#2F5C9E",
    "ok": "#34C759",
    "warn": "#F5A524",
    "danger": "#FF5A52",
    "titlebar": "#202020",
}

# --------------------------------------------------------------------------- text

try:
    from PIL import ImageFont

    _FONT_PATH = subprocess.run(
        ["fc-match", "-f", "%{file}", "Ubuntu Sans"], capture_output=True, text=True
    ).stdout.strip()
    _FONTS = {}

    def measure(s, size, weight=400):
        key = round(size * 4)
        if key not in _FONTS:
            _FONTS[key] = ImageFont.truetype(_FONT_PATH, size=key)
        w = _FONTS[key].getlength(s) / 4
        return w * (1.06 if weight >= 600 else 1.0)

except Exception:  # Pillow or fontconfig missing: rough estimate is good enough

    def measure(s, size, weight=400):
        return len(s) * size * (0.58 if weight >= 600 else 0.55)


def f(v):
    return f"{v:.1f}".rstrip("0").rstrip(".")


def r(x, y, w, h, fill, rx=0, stroke=None, sw=1, op=None, extra=""):
    s = f'<rect x="{f(x)}" y="{f(y)}" width="{f(w)}" height="{f(h)}" fill="{fill}"'
    if rx:
        s += f' rx="{f(rx)}"'
    if stroke:
        s += f' stroke="{stroke}" stroke-width="{f(sw)}"'
    if op is not None:
        s += f' opacity="{op}"'
    return s + (f" {extra}" if extra else "") + "/>"


def t(x, y, s, size=13, fill=None, weight=400, anchor="start", op=None, extra=""):
    fill = fill or C["text"]
    attrs = f'x="{f(x)}" y="{f(y)}" font-size="{f(size)}" font-weight="{weight}" fill="{fill}"'
    if anchor != "start":
        attrs += f' text-anchor="{anchor}"'
    if op is not None:
        attrs += f' opacity="{op}"'
    if extra:
        attrs += " " + extra
    lines = s.split("\n")
    if len(lines) == 1:
        return f"<text {attrs}>{escape(s)}</text>"
    spans = "".join(
        f'<tspan x="{f(x)}" dy="{0 if i == 0 else f(size * 1.38)}">{escape(line)}</tspan>'
        for i, line in enumerate(lines)
    )
    return f"<text {attrs}>{spans}</text>"


def ln(x1, y1, x2, y2, stroke, sw=1, dash=None, op=None):
    s = f'<line x1="{f(x1)}" y1="{f(y1)}" x2="{f(x2)}" y2="{f(y2)}" stroke="{stroke}" stroke-width="{f(sw)}"'
    if dash:
        s += f' stroke-dasharray="{dash}"'
    if op is not None:
        s += f' opacity="{op}"'
    return s + "/>"


def section(x, y, title, right=None, w=None):
    s = t(x, y, title, 11.5, C["text3"], 700, extra='letter-spacing="0.7"')
    if right and w:
        s += t(x + w, y, right, 12, C["accent"], 500, "end")
    return s


# --------------------------------------------------------------------------- icons

ICONS = {
    "folder": "M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z",
    "drive": "M4 7h16a1 1 0 0 1 1 1v8a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V8a1 1 0 0 1 1-1z M7 12h2 M15 12h2",
    "usb": "M9 3h6v6H9z M7 9h10v7a5 5 0 0 1-10 0z M11 5v1 M13 5v1",
    "check": "M5 12.5l4.5 4.5L19 7.5",
    "chev_r": "M9 6l6 6-6 6",
    "chev_d": "M6 9l6 6 6-6",
    "chev_l": "M15 6l-6 6 6 6",
    "help": "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z M9.5 9.2a2.6 2.6 0 1 1 3.6 2.4c-.7.3-1.1.9-1.1 1.6v.6 M12 17h.01",
    "globe": "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z M3 12h18 M12 3c2.5 2.6 3.7 5.6 3.7 9s-1.2 6.4-3.7 9c-2.5-2.6-3.7-5.6-3.7-9s1.2-6.4 3.7-9z",
    "rot_ccw": "M4 4v5h5 M4.8 9A8 8 0 1 1 5.5 16",
    "rot_cw": "M20 4v5h-5 M19.2 9A8 8 0 1 0 18.5 16",
    "undo": "M9 14L4 9l5-5 M4 9h10.5a5.5 5.5 0 0 1 0 11H11",
    "redo": "M15 14l5-5-5-5 M20 9H9.5a5.5 5.5 0 0 0 0 11H13",
    "wand": "M4 20L15 9 M13 7l4 4 M18 2.5v3 M16.5 4h3 M20.5 8.5v2 M19.5 9.5h2 M9 2.5v2 M8 3.5h2",
    "eye": "M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z",
    "face": "M8 3H5a2 2 0 0 0-2 2v3 M16 3h3a2 2 0 0 1 2 2v3 M8 21H5a2 2 0 0 1-2-2v-3 M16 21h3a2 2 0 0 0 2-2v-3 M9 10h.01 M15 10h.01 M9.5 15a3.5 3.5 0 0 0 5 0",
    "arrow_l": "M19 12H5 M11 6l-6 6 6 6",
    "arrow_r": "M5 12h14 M13 6l6 6-6 6",
    "save": "M5 3h11l4 4v12a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V5a2 2 0 0 1 1-2z M8 3v5h7V3 M8 21v-7h8v7",
    "crop": "M6 2v14a2 2 0 0 0 2 2h14 M2 6h14a2 2 0 0 1 2 2v14",
    "image": "M4 5h16a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z M3 16l5-5 4 4 3-3 6 6 M15.5 9.5h.01",
    "idcard": "M4 5h16a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z M9 12a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M5.5 16.5c.6-1.7 2-2.6 3.5-2.6s2.9.9 3.5 2.6 M15 9h3 M15 13h3",
    "eject": "M5 18h14 M12 6l7 8H5z",
    "clock": "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z M12 7v5l3 2",
    "reset": "M3 12a9 9 0 1 0 2.6-6.4 M3 3v5h5",
    "warn": "M12 3.5l9.5 16.5h-19z M12 10v4.5 M12 17.5h.01",
    "page": "M6 2.5h8.5l4.5 4.5v14.5H6z M14.5 2.5V7H19",
    "scissors": "M6 9a3 3 0 1 0 0-6 3 3 0 0 0 0 6z M6 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6z M20 4L8.1 15.9 M14.5 14.5L20 20 M8.1 8.1L12 12",
    "minus": "M5 12h14",
    "plus": "M12 5v14 M5 12h14",
    "grid": "M4 4h7v7H4z M13 4h7v7h-7z M4 13h7v7H4z M13 13h7v7h-7z",
    "lock": "M6 11h12v10H6z M8.5 11V8a3.5 3.5 0 0 1 7 0v3",
    "move": "M12 3v18 M3 12h18 M12 3l-3 3 M12 3l3 3 M12 21l-3-3 M12 21l3-3 M3 12l3-3 M3 12l3 3 M21 12l-3-3 M21 12l-3 3",
    "sort": "M7 4v16 M3 16l4 4 4-4 M14 6h7 M14 11h5 M14 16h3",
    "gear": "M12 15.2a3.2 3.2 0 1 0 0-6.4 3.2 3.2 0 0 0 0 6.4z M19.4 13.5l1.6 1.2-1.9 3.3-1.9-.7a7.6 7.6 0 0 1-2.3 1.3l-.3 2h-3.8l-.3-2a7.6 7.6 0 0 1-2.3-1.3l-1.9.7L3 14.7l1.6-1.2a7.7 7.7 0 0 1 0-3L3 9.3 4.9 6l1.9.7A7.6 7.6 0 0 1 9.1 5.4l.3-2h3.8l.3 2a7.6 7.6 0 0 1 2.3 1.3L17.7 6l1.9 3.3-1.6 1.2a7.7 7.7 0 0 1 0 3z",
}


def icon(name, x, y, size=18, color=None, sw=2):
    color = color or C["text"]
    k = size / 24
    return (
        f'<g transform="translate({f(x)},{f(y)}) scale({k:.4f})" fill="none" stroke="{color}" '
        f'stroke-width="{f(sw)}" stroke-linecap="round" stroke-linejoin="round"><path d="{ICONS[name]}"/></g>'
    )


def badge_check(cx, cy, rad=10):
    return (
        f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(rad)}" fill="{C["ok"]}" stroke="#151515" stroke-width="2"/>'
        + icon("check", cx - rad * 0.62, cy - rad * 0.62, rad * 1.24, "#FFFFFF", 3)
    )


def logo(x, y, s):
    return (
        f'<rect x="{f(x)}" y="{f(y)}" width="{f(s)}" height="{f(s)}" rx="{f(s * 0.26)}" fill="url(#g-logo)"/>'
        f'<circle cx="{f(x + s / 2)}" cy="{f(y + s / 2)}" r="{f(s * 0.24)}" fill="none" stroke="#FFFFFF" stroke-width="{f(s * 0.1)}"/>'
        f'<circle cx="{f(x + s / 2)}" cy="{f(y + s / 2)}" r="{f(s * 0.07)}" fill="#FFFFFF"/>'
    )


# --------------------------------------------------------------------------- widgets

BTN = {
    "primary": (C["accent"], None, "#FFFFFF", 650),
    "secondary": (C["raised"], C["border2"], C["text"], 500),
    "ghost": ("none", C["border2"], C["text"], 500),
    "tinted": (C["accent_tint"], C["accent_tint_b"], "#D6E6FF", 600),
}


KBD_ICONS = {"←": "arrow_l", "→": "arrow_r"}


def kbd(x, y, label, dark=False):
    w = 26 if label in KBD_ICONS else measure(label, 11, 600) + 12
    fill = "#FFFFFF" if dark else "#4A4A4A"
    fg = "#FFFFFF" if dark else C["text2"]
    op = 0.22 if dark else None
    s = r(x, y, w, 19, fill, rx=4, op=op)
    if label in KBD_ICONS:
        s += icon(KBD_ICONS[label], x + w / 2 - 7, y + 2.5, 14, fg, 2.4)
    else:
        s += t(x + w / 2, y + 13.5, label, 11, fg, 600, "middle")
    return s, w


def button(x, y, w, h, label, kind="secondary", ic=None, hint=None, size=13.5, sub=None):
    fill, stroke, fg, wt = BTN[kind]
    out = [r(x, y, w, h, fill, rx=8, stroke=stroke)]
    hint_w = 0
    if hint:
        _, hint_w = kbd(0, 0, hint)
        k, _ = kbd(x + w - 12 - hint_w, y + h / 2 - 9.5, hint, dark=(kind == "primary"))
        out.append(k)
        hint_w += 14
    avail = w - hint_w
    lw = measure(label, size, wt)
    cw = lw + (size + 8 if ic else 0)
    cx = x + avail / 2 - cw / 2 + (6 if hint else 0)
    ty = y + h / 2 + size * 0.36 - (7 if sub else 0)
    if ic:
        out.append(icon(ic, cx, ty - size * 0.95, size + 3, fg))
        cx += size + 8
    out.append(t(cx, ty, label, size, fg, wt))
    if sub:
        out.append(t(x + avail / 2 + (6 if hint else 0), ty + 17, sub, 11.5, fg, 400, "middle", op=0.8))
    return "".join(out)


def icon_btn(x, y, name, s=32, color=None):
    return r(x, y, s, s, C["raised"], rx=7, stroke=C["border2"]) + icon(name, x + s / 2 - 9, y + s / 2 - 9, 18, color or C["text"])


def chip(x, y, label, active=False, h=30, ic=None, count=None, size=13):
    w = measure(label, size, 600 if active else 500) + 24 + (22 if ic else 0)
    if count is not None:
        w += measure(str(count), 11.5, 600) + 16
    if active:
        out = [r(x, y, w, h, C["accent_tint"], rx=h / 2, stroke=C["accent"])]
        fg = "#D6E6FF"
    else:
        out = [r(x, y, w, h, C["panel2"], rx=h / 2, stroke=C["border2"])]
        fg = C["text"]
    cx = x + 12
    if ic:
        out.append(icon(ic, cx, y + h / 2 - 8, 16, fg))
        cx += 22
    out.append(t(cx, y + h / 2 + size * 0.36, label, size, fg, 600 if active else 500))
    if count is not None:
        cx += measure(label, size, 600 if active else 500) + 8
        cw = measure(str(count), 11.5, 600) + 10
        out.append(r(cx, y + h / 2 - 9, cw, 18, "#FFFFFF", rx=9, op=0.12))
        out.append(t(cx + cw / 2, y + h / 2 + 4, str(count), 11.5, fg, 600, "middle"))
    return "".join(out), w


def segmented(x, y, items, active, h=30, size=13, pad=16):
    widths = [measure(s, size, 600) + 2 * pad for s in items]
    out = [r(x, y, sum(widths) + 4, h + 4, C["panel2"], rx=8, stroke=C["border2"])]
    cx = x + 2
    for i, (s, w) in enumerate(zip(items, widths)):
        if i == active:
            out.append(r(cx, y + 2, w, h, C["raised"], rx=6, stroke="#5A5A5A"))
        out.append(t(cx + w / 2, y + 2 + h / 2 + size * 0.36, s, size, C["text"] if i == active else C["text2"], 600 if i == active else 500, "middle"))
        cx += w
    return "".join(out), sum(widths) + 4


def toggle(x, y, on=True):
    out = r(x, y, 38, 22, C["accent"] if on else "#4A4A4A", rx=11)
    cx = x + 27 if on else x + 11
    return out + f'<circle cx="{f(cx)}" cy="{f(y + 11)}" r="8" fill="#FFFFFF"/>'


def slider(x, y, w, frac, label, value, bipolar=False, grad=None, minus_plus=False):
    out = [t(x, y, label, 13.5, C["text"], 500), t(x + w, y, value, 13, C["text2"], 500, "end")]
    ty = y + 24
    tx, tw = x, w
    if minus_plus:
        out.append(icon_btn(x, ty - 15, "minus", 30))
        out.append(icon_btn(x + w - 30, ty - 15, "plus", 30))
        tx, tw = x + 42, w - 84
    if grad:
        out.append(r(tx, ty - 3, tw, 6, f"url(#{grad})", rx=3))
    else:
        out.append(r(tx, ty - 2, tw, 4, C["track"], rx=2))
    kx = tx + tw * frac
    if not grad:
        a, b = (sorted([tx + tw / 2, kx]) if bipolar else (tx, kx))
        out.append(r(a, ty - 2, b - a, 4, C["accent"], rx=2))
    if bipolar:
        out.append(ln(tx + tw / 2, ty - 6, tx + tw / 2, ty + 6, "#8A8A8A", 1.5))
    out.append(f'<circle cx="{f(kx)}" cy="{f(ty)}" r="8" fill="#F4F4F4" stroke="#151515" stroke-width="1.5"/>')
    return "".join(out)


def stepper(x, y, value, sub=None):
    out = [icon_btn(x, y, "minus", 32), r(x + 40, y, 56, 32, C["panel2"], rx=7, stroke=C["border2"])]
    out.append(t(x + 68, y + 21, value, 15, C["text"], 600, "middle"))
    out.append(icon_btn(x + 104, y, "plus", 32))
    if sub:
        out.append(t(x + 148, y + 21, sub, 12.5, C["text2"]))
    return "".join(out)


def dropdown(x, y, w, label, h=34):
    return (
        r(x, y, w, h, C["panel2"], rx=7, stroke=C["border2"])
        + t(x + 12, y + h / 2 + 5, label, 13.5, C["text"], 500)
        + icon("chev_d", x + w - 26, y + h / 2 - 8, 16, C["text2"])
    )


# --------------------------------------------------------------------------- scenes

SCENE_ASPECT = {}


def _sym(sid, w, h, body):
    SCENE_ASPECT[sid] = (w, h)
    return f'<symbol id="{sid}" viewBox="0 0 {w} {h}" preserveAspectRatio="xMidYMid slice">{body}</symbol>'


def _lg(gid, c1, c2, vertical=True):
    xy = 'x1="0" y1="0" x2="0" y2="1"' if vertical else 'x1="0" y1="0" x2="1" y2="0"'
    return f'<linearGradient id="{gid}" {xy}><stop offset="0" stop-color="{c1}"/><stop offset="1" stop-color="{c2}"/></linearGradient>'


def _pine(x, base, h, color):
    return f'<path d="M{f(x)} {f(base - h)}L{f(x + h * 0.32)} {f(base)}H{f(x - h * 0.32)}z" fill="{color}"/>'


def scene_defs():
    rnd = random.Random(7)
    d = [
        _lg("g-logo", "#3B82F6", "#8B5CF6"),
        _lg("g-temp", "#4C8DF6", "#F5B54A", vertical=False),
        _lg("g-levels", "#000000", "#FFFFFF", vertical=False),
        _lg("g-bright", "#3A3A3A", "#F2F2F2", vertical=False),
        _lg("sk-lake", "#5B98D4", "#D3E7F7"),
        _lg("wt-lake", "#6FA3CC", "#3D6F96"),
        _lg("sk-beach", "#EF8A63", "#FCE0B4"),
        _lg("wt-beach", "#4F8FB7", "#2D6A90"),
        _lg("sk-city", "#262A57", "#EE9467"),
        _lg("sk-flow", "#94C5EE", "#EAF5FC"),
        _lg("sk-snow", "#3C6CA6", "#BCD6EF"),
        _lg("sk-aut", "#F2DDB7", "#FAF0DE"),
        _lg("bg-por", "#DCE1E6", "#C3CBD2"),
        _lg("bg-por2", "#EFE3D3", "#DCCBB5"),
        _lg("sk-fam", "#A9DAF3", "#E6F5FC"),
        '<radialGradient id="sh-por" cx="0.85" cy="0.55" r="0.6"><stop offset="0" stop-color="#9AA3AC" stop-opacity="0.55"/><stop offset="1" stop-color="#9AA3AC" stop-opacity="0"/></radialGradient>',
    ]

    pines = "".join(_pine(x, 228, h, "#27482F") for x, h in [(18, 70), (40, 52), (60, 80), (338, 66), (362, 88), (386, 58)])
    d.append(_sym("sc-lake", 400, 300,
        '<rect width="400" height="300" fill="url(#sk-lake)"/>'
        '<circle cx="318" cy="64" r="20" fill="#FFF6D2" opacity="0.95"/>'
        '<path d="M0 190L60 118L112 160L172 86L238 166L300 108L400 178V214H0z" fill="#8CA5BE"/>'
        '<path d="M0 210L84 150L152 198L232 138L322 204L400 168V222H0z" fill="#5C7E9C"/>'
        '<rect y="214" width="400" height="86" fill="#5B8A4C"/>'
        '<path d="M0 232Q200 222 400 232V276Q200 286 0 276z" fill="url(#wt-lake)"/>'
        '<path d="M120 246h70M210 254h90M150 262h60" stroke="#FFFFFF" stroke-width="2" opacity="0.35"/>'
        + pines))

    people = "".join(
        f'<circle cx="{x}" cy="{y}" r="6" fill="#3A2E39"/><rect x="{x - 6}" y="{y + 7}" width="12" height="28" rx="5" fill="#3A2E39"/>'
        for x, y in [(150, 222), (172, 218)]
    )
    d.append(_sym("sc-beach", 400, 300,
        '<rect width="400" height="300" fill="url(#sk-beach)"/>'
        '<circle cx="250" cy="186" r="30" fill="#FFE6A8"/>'
        '<rect y="188" width="400" height="56" fill="url(#wt-beach)"/>'
        '<path d="M0 244Q200 232 400 244V300H0z" fill="#E8CD9B"/>'
        '<path d="M190 200h120M210 212h80" stroke="#FFE6A8" stroke-width="3" opacity="0.6"/>' + people))

    blds, wins = [], []
    x = 0
    while x < 400:
        bw = rnd.randint(26, 52)
        bh = rnd.randint(70, 180)
        blds.append(f'<rect x="{x}" y="{300 - bh}" width="{bw - 3}" height="{bh}" fill="#1E1F38"/>')
        for wy in range(300 - bh + 10, 292, 14):
            for wx in range(x + 6, x + bw - 9, 10):
                if rnd.random() < 0.3:
                    wins.append(f'<rect x="{wx}" y="{wy}" width="4" height="6" fill="#F6C76E"/>')
        x += bw
    d.append(_sym("sc-city", 400, 300, '<rect width="400" height="300" fill="url(#sk-city)"/><circle cx="90" cy="170" r="26" fill="#FBC58A" opacity="0.8"/>' + "".join(blds + wins)))

    flowers = "".join(
        f'<circle cx="{rnd.randint(0, 400)}" cy="{rnd.randint(180, 298)}" r="{rnd.choice([3, 4, 5, 6])}" fill="{rnd.choice(["#E85D75", "#F4B942", "#F7F7F7", "#B565D8", "#F08A4B"])}"/>'
        for _ in range(120)
    )
    d.append(_sym("sc-flow", 400, 300,
        '<rect width="400" height="300" fill="url(#sk-flow)"/>'
        '<path d="M0 170Q100 120 210 158T400 140V300H0z" fill="#86BC72"/>'
        '<path d="M0 196Q200 168 400 190V300H0z" fill="#4D8A3D"/>' + flowers))

    d.append(_sym("sc-snow", 400, 300,
        '<rect width="400" height="300" fill="url(#sk-snow)"/>'
        '<path d="M0 220L90 92L160 180L230 70L310 170L360 120L400 160V240H0z" fill="#F1F5F9"/>'
        '<path d="M90 92L120 200H60z M230 70L262 200H200z M360 120L380 200H340z" fill="#C7D3E0"/>'
        '<rect y="236" width="400" height="64" fill="#E5ECF2"/>'
        + "".join(_pine(x, 252, h, "#2C4A3A") for x, h in [(30, 46), (60, 62), (300, 56), (330, 40), (370, 64)])))

    trees = "".join(
        f'<rect x="{x - 4}" y="{y}" width="8" height="{300 - y}" fill="#5B3A29"/><circle cx="{x}" cy="{y}" r="{rr}" fill="{c}"/>'
        for x, y, rr, c in [(40, 170, 48, "#D9822B"), (250, 150, 56, "#B5502A"), (140, 120, 40, "#E3A23B"), (290, 250, 38, "#E3A23B"), (20, 280, 40, "#B5502A")]
    )
    d.append(_sym("sc-aut", 300, 400,
        '<rect width="300" height="400" fill="url(#sk-aut)"/>'
        '<rect y="280" width="300" height="120" fill="#8A6B3E"/>'
        '<path d="M120 280h60l70 120H50z" fill="#CDB79E"/>' + trees))

    def person(bg, hair, shirt, skin, uneven):
        return (
            f'<rect width="300" height="400" fill="url(#{bg})"/>'
            + ('<rect width="300" height="400" fill="url(#sh-por)"/>' if uneven else "")
            + f'<path d="M20 400C24 330 74 300 150 296C226 300 276 330 280 400z" fill="{shirt}"/>'
            f'<path d="M126 248h48v52c-8 12-40 12-48 0z" fill="{skin}" opacity="0.88"/>'
            f'<path d="M150 296l-24-18 24 34 24-34z" fill="#EDE7DD"/>'
            f'<ellipse cx="89" cy="182" rx="9" ry="16" fill="{skin}"/><ellipse cx="211" cy="182" rx="9" ry="16" fill="{skin}"/>'
            f'<ellipse cx="150" cy="176" rx="61" ry="79" fill="{skin}"/>'
            f'<path d="M88 176C82 112 112 88 150 88S220 112 212 176C206 150 196 132 176 124C150 136 118 132 100 132C94 146 90 160 88 176z" fill="{hair}"/>'
            f'<path d="M115 156q13-7 26-1 M159 155q13-6 26 1" stroke="{hair}" stroke-width="4.5" fill="none" stroke-linecap="round"/>'
            '<ellipse cx="128" cy="172" rx="10" ry="5.5" fill="#FBFBFB"/><ellipse cx="172" cy="172" rx="10" ry="5.5" fill="#FBFBFB"/>'
            '<circle cx="128" cy="172" r="4.6" fill="#4B3A2E"/><circle cx="172" cy="172" r="4.6" fill="#4B3A2E"/>'
            f'<path d="M150 178v20q-3 5-10 4" stroke="#B9846A" stroke-width="2.5" fill="none" stroke-linecap="round"/>'
            '<path d="M132 222q18 9 36 0" stroke="#B1605F" stroke-width="3.5" fill="none" stroke-linecap="round"/>'
        )

    d.append(_sym("sc-por", 300, 400, person("bg-por", "#3E2C22", "#2F3E52", "#E4B696", True)))
    d.append(_sym("sc-por2", 300, 400, person("bg-por2", "#A7743F", "#7A3B4E", "#EDC3A5", False)))

    fam = "".join(
        f'<circle cx="{x}" cy="{y}" r="{hr}" fill="{sk}"/><rect x="{x - hr * 1.1}" y="{y + hr + 2}" width="{hr * 2.2}" height="{hr * 4}" rx="{hr * 0.8}" fill="{c}"/>'
        for x, y, hr, c, sk in [(150, 150, 14, "#3D6FB6", "#E2B191"), (200, 140, 15, "#C64B4B", "#D9A27F"), (245, 176, 10, "#F0B43C", "#EDC3A5")]
    )
    d.append(_sym("sc-fam", 400, 300,
        '<rect width="400" height="300" fill="url(#sk-fam)"/>'
        '<path d="M0 180Q200 150 400 180V300H0z" fill="#7CBF6B"/>'
        '<circle cx="60" cy="150" r="40" fill="#4F9A4A"/><rect x="56" y="150" width="8" height="50" fill="#5B3A29"/>' + fam))
    return "".join(d)


def place(scene, x, y, w, h, clip=None):
    """Draw a scene fitted (contain) inside the box; returns (svg, (x, y, w, h))."""
    sw, sh = SCENE_ASPECT[scene]
    k = min(w / sw, h / sh)
    dw, dh = sw * k, sh * k
    dx, dy = x + (w - dw) / 2, y + (h - dh) / 2
    return f'<use href="#{scene}" x="{f(dx)}" y="{f(dy)}" width="{f(dw)}" height="{f(dh)}"/>', (dx, dy, dw, dh)


# --------------------------------------------------------------------------- chrome

def label_strip(num, title, lang):
    lang_name = {"en": "English UI", "ro": "Romanian UI"}[lang]
    return (
        r(0, 0, WIN_W, LABEL_H, "#F2F2EE")
        + r(16, 11, 90, 22, C["warn"], rx=4)
        + t(61, 27, "CONCEPT", 12, "#1A1A1A", 800, "middle", extra='letter-spacing="1"')
        + t(120, 28, f"Mockup {num} — {title}  ·  {lang_name}", 15, "#1A1A1A", 650)
        + t(WIN_W - 16, 28, "PhotoX planning (pre-0.1.0) · not an implemented interface · synthetic placeholder images", 12.5, "#5A5A54", 400, "end")
    )


def titlebar():
    x = WIN_W - 138
    return (
        r(0, 0, WIN_W, 32, C["titlebar"])
        + logo(12, 8, 16)
        + t(38, 21, "PhotoX", 12, "#D6D6D6")
        + ln(x + 18, 16.5, x + 28, 16.5, "#D6D6D6")
        + r(x + 64, 11.5, 10, 10, "none", rx=1.5, stroke="#D6D6D6")
        + f'<path d="M{x + 110} 11.5l10 10M{x + 120} 11.5l-10 10" stroke="#D6D6D6" stroke-width="1"/>'
    )


def appbar(L, lang, active, center=None, center_extra=None):
    y0, h = 32, 56
    out = [r(0, y0, WIN_W, h, C["bar"]), ln(0, y0 + h - 0.5, WIN_W, y0 + h - 0.5, C["border"])]
    out.append(logo(18, y0 + 15, 26))
    out.append(t(54, y0 + 35, "PhotoX", 19, C["text"], 700))
    # workspace switcher
    sx, sw = 172, 168
    out.append(r(sx, y0 + 9, sw * 2 + 8, 38, C["panel2"], rx=10, stroke=C["border"]))
    for i, (lab, ic) in enumerate([(L["tab_photos"], "image"), (L["tab_id"], "idcard")]):
        x = sx + 4 + i * sw
        on = i == active
        if on:
            out.append(r(x, y0 + 13, sw, 30, C["raised"], rx=7, stroke="#5A5A5A"))
        lw = measure(lab, 14, 600) + 26
        cx = x + sw / 2 - lw / 2
        col = C["text"] if on else C["text2"]
        out.append(icon(ic, cx, y0 + 19, 18, C["accent"] if on else col))
        out.append(t(cx + 26, y0 + 33, lab, 14, col, 600 if on else 500))
    if center:
        out.append(t(WIN_W / 2 + 40, y0 + 34, center, 14, C["text2"], 500, "middle"))
        if center_extra:
            cw = measure(center, 14, 500)
            px = WIN_W / 2 + 40 + cw / 2 + 14
            pw = measure(center_extra, 12.5, 600) + 30
            out.append(r(px, y0 + 16, pw, 24, "#3A2C10", rx=12, stroke="#7A5714"))
            out.append(f'<circle cx="{f(px + 12)}" cy="{f(y0 + 28)}" r="4" fill="{C["warn"]}"/>')
            out.append(t(px + 22, y0 + 32.5, center_extra, 12.5, "#F8D48A", 600))
    # language switcher — always on the main screen
    lx = WIN_W - 236
    out.append(icon("globe", lx, y0 + 19, 18, C["text2"]))
    out.append(r(lx + 26, y0 + 13, 92, 30, C["panel2"], rx=15, stroke=C["border2"]))
    for i, code in enumerate(["EN", "RO"]):
        bx = lx + 28 + i * 44
        on = code.lower() == lang
        if on:
            out.append(r(bx, y0 + 15, 44, 26, C["accent"], rx=13))
        out.append(t(bx + 22, y0 + 32.5, code, 12.5, "#FFFFFF" if on else C["text2"], 700, "middle"))
    out.append(icon("help", WIN_W - 112, y0 + 17, 22, C["text2"]))
    out.append(icon("gear", WIN_W - 66, y0 + 17, 22, C["text2"]))
    return "".join(out)


def document(num, title, lang, body, defs):
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{WIN_W}" height="{WIN_H + LABEL_H}" '
        f'viewBox="0 0 {WIN_W} {WIN_H + LABEL_H}">'
        f"<title>PhotoX concept mockup {num}: {escape(title)} ({lang})</title>"
        f"<style>text{{font-family:{FONT}}}</style>"
        f"<defs>{defs}</defs>"
        + label_strip(num, title, lang)
        + f'<g transform="translate(0,{LABEL_H})">'
        + body
        + "</g></svg>"
    )


# --------------------------------------------------------------------------- mockup 1

GALLERY = [
    ("IMG_0417.jpg", "sc-lake", "4000 × 3000"),
    ("IMG_0418.jpg", "sc-beach", "4000 × 3000"),
    ("IMG_0419.jpg", "sc-por2", "3000 × 4000"),
    ("IMG_0420.jpg", "sc-city", "4000 × 3000"),
    ("IMG_0421.jpg", "sc-flow", "4000 × 3000"),
    ("IMG_0422.jpg", "sc-fam", "4032 × 3024"),
    ("IMG_0423.jpg", "sc-aut", "3024 × 4032"),
    ("IMG_0424.jpg", "sc-lake", "4000 × 3000"),
    ("IMG_0425.jpg", "sc-snow", "4000 × 3000"),
    ("WhatsApp Image 2026-10-11.jpeg", "sc-fam", "1280 × 960"),
    ("IMG_0427.jpg", "sc-beach", "4000 × 3000"),
    ("IMG_0428.jpg", "sc-por", "3000 × 4000"),
    ("IMG_0429.jpg", "sc-flow", "4000 × 3000"),
    ("IMG_0430.jpg", "sc-city", "4000 × 3000"),
    ("IMG_0431.jpg", "sc-snow", "4000 × 3000"),
    ("IMG_0432.jpg", "sc-aut", "3024 × 4032"),
    ("IMG_0433.jpg", "sc-lake", "4000 × 3000"),
    ("IMG_0434.jpg", "sc-fam", "4032 × 3024"),
    ("IMG_0435.jpg", "sc-beach", "4000 × 3000"),
    ("IMG_0436.jpg", "sc-por2", "3000 × 4000"),
]
EDITED = 7  # first seven cells are already edited (photos 17–23 of the order)
SELECTED = 7  # IMG_0424 — first unedited photo
LOWRES = 9


def ellipsize(s, size, max_w):
    if measure(s, size) <= max_w:
        return s
    while s and measure(s + "…", size) > max_w:
        s = s[:-1]
    return s + "…"


def mockup_main(L, lang):
    out = [r(0, 0, WIN_W, WIN_H, C["bg"]), titlebar(), appbar(L, lang, 0)]

    # ---- sidebar
    out.append(r(0, 88, 300, 884, C["panel"]))
    out.append(ln(300.5, 88, 300.5, 972, C["border"]))
    y = 120
    out.append(section(20, y, L["locations"]))
    y += 14
    for ic, lab, on, extra in [("drive", L["disk_c"], False, None), ("drive", L["disk_d"], True, None), ("usb", L["usb"], False, "eject")]:
        if on:
            out.append(r(10, y, 280, 36, C["panel2"], rx=7))
        out.append(icon(ic, 22, y + 9, 18, C["text2"]))
        out.append(t(50, y + 23, lab, 14, C["text"], 500))
        if extra:
            out.append(icon(extra, 262, y + 10, 16, C["text3"]))
        y += 38
    y += 22
    out.append(section(20, y, L["recent"]))
    y += 14
    for name, done, total, on in [("Popescu Maria", 23, 85, True), ("Ionescu Andrei", 40, 40, False), ("Familia Dobre", 0, 112, False)]:
        if on:
            out.append(r(10, y, 280, 50, C["panel2"], rx=7))
        out.append(icon("clock", 22, y + 9, 18, C["text2"]))
        out.append(t(50, y + 22, name, 14, C["text"], 500))
        out.append(t(278, y + 22, f"{done}/{total}", 12.5, C["ok"] if done == total else C["text2"], 600, "end"))
        out.append(r(50, y + 33, 228, 4, C["track"], rx=2))
        out.append(r(50, y + 33, 228 * done / total, 4, C["ok"] if done == total else C["accent"], rx=2))
        y += 54
    y += 22
    out.append(section(20, y, L["folders"]))
    y += 12
    tree = [
        (0, "chev_d", "Comenzi", False),
        (1, "chev_r", "2026-09", False),
        (1, "chev_d", "2026-10", False),
        (2, None, "Dobre_familie", False),
        (2, None, "Ionescu Andrei", False),
        (2, None, "Popescu Maria", True),
        (2, None, "Stan_nunta", False),
        (1, "chev_r", "2026-11", False),
    ]
    for depth, chev, name, on in tree:
        x = 18 + depth * 20
        if on:
            out.append(r(10, y, 280, 32, C["accent_tint"], rx=7, stroke=C["accent_tint_b"]))
        if chev:
            out.append(icon(chev, x, y + 9, 14, C["text3"]))
        out.append(icon("folder", x + 18, y + 7, 18, C["accent"] if on else C["text2"]))
        out.append(t(x + 44, y + 21, name, 13.5, C["text"], 600 if on else 400))
        y += 34
    out.append(ln(20, 900, 280, 900, C["border"]))
    out.append(icon("lock", 20, 914, 16, C["text3"]))
    out.append(t(44, 926, L["originals_note"], 12, C["text3"]))

    # ---- grid header
    gx0, gx1 = 300, 1270
    out.append(icon("folder", 324, 106, 18, C["text2"]))
    crumbs = ["D:", "Comenzi", "2026-10", "Popescu Maria"]
    cx = 350
    for i, c in enumerate(crumbs):
        last = i == len(crumbs) - 1
        out.append(t(cx, 121, c, 14.5, C["text"] if last else C["text2"], 600 if last else 400))
        cx += measure(c, 14.5, 600 if last else 400) + 8
        if not last:
            out.append(icon("chev_r", cx - 2, 109, 14, C["text3"]))
            cx += 18
    x = 324
    for lab, n, on in [(L["f_all"], 85, False), (L["f_todo"], 62, True), (L["f_done"], 23, False)]:
        s, w = chip(x, 140, lab, on, count=n)
        out.append(s)
        x += w + 8
    out.append(icon("sort", 1000, 147, 16, C["text2"]))
    out.append(t(1022, 160, L["sort"], 13, C["text2"]))
    out.append(icon("grid", 1140, 147, 16, C["text2"]))
    out.append(r(1164, 153, 82, 4, C["track"], rx=2))
    out.append(r(1164, 153, 46, 4, C["accent"], rx=2))
    out.append('<circle cx="1210" cy="155" r="7" fill="#F4F4F4"/>')

    # ---- grid
    out.append('<clipPath id="clip-grid"><rect x="300" y="186" width="970" height="786"/></clipPath><g clip-path="url(#clip-grid)">')
    cw, ch, gap = 171, 184, 16
    for i, (name, scene, dims) in enumerate(GALLERY):
        col, row = i % 5, i // 5
        x = 324 + col * (cw + gap)
        y = 194 + row * (ch + 12)
        sel = i == SELECTED
        out.append(r(x, y, cw, ch, C["accent_tint"] if sel else C["panel"], rx=9, stroke=C["accent"] if sel else C["border"], sw=2 if sel else 1))
        img, (ix, iy, iw, ih) = place(scene, x + 8, y + 8, cw - 16, 124)
        out.append(img)
        if i < EDITED:
            out.append(badge_check(ix + iw - 12, iy + 12, 11))
        if i == LOWRES:
            out.append(f'<circle cx="{f(ix + iw - 13)}" cy="{f(iy + 13)}" r="11" fill="{C["warn"]}" stroke="#151515" stroke-width="2"/>')
            out.append(t(ix + iw - 13, iy + 18, "!", 14, "#1A1A1A", 800, "middle"))
        out.append(t(x + 10, y + 154, ellipsize(name, 13, cw - 20), 13, C["text"], 600 if sel else 500))
        info = L["lowres"] if i == LOWRES else (L["edited"] if i < EDITED else dims)
        col_info = C["warn"] if i == LOWRES else (C["ok"] if i < EDITED else C["text3"])
        out.append(t(x + 10, y + 173, info, 11.5, col_info, 500))
    out.append("</g>")
    out.append(r(1258, 196, 5, 760, "#2A2A2A", rx=2.5))
    out.append(r(1258, 330, 5, 170, "#5A5A5A", rx=2.5))

    # ---- details panel
    px = 1270
    out.append(r(px, 88, 330, 884, C["panel"]))
    out.append(ln(px + 0.5, 88, px + 0.5, 972, C["border"]))
    out.append(section(px + 20, 120, L["selected_photo"]))
    out.append(r(px + 20, 134, 290, 218, C["canvas"], rx=8))
    img, _ = place("sc-lake", px + 26, 140, 278, 206)
    out.append(img)
    out.append(t(px + 20, 382, "IMG_0424.jpg", 16, C["text"], 650))
    rows = [L["d_dims"], L["d_file"], L["d_taken"]]
    for k, s in enumerate(rows):
        out.append(t(px + 20, 408 + k * 22, s, 13, C["text2"]))
    out.append(f'<circle cx="{px + 25}" cy="{475}" r="5" fill="{C["text3"]}"/>')
    out.append(t(px + 38, 480, L["d_status"], 13, C["text"], 500))
    out.append(icon("check", px + 18, 492, 16, C["ok"], 2.5))
    out.append(t(px + 38, 505, L["d_print"], 13, C["text"], 500))

    out.append(ln(px + 20, 534, px + 310, 534, C["border"]))
    out.append(section(px + 20, 564, L["order_progress"]))
    out.append(t(px + 20, 600, "23", 30, C["text"], 700))
    out.append(t(px + 20 + measure("23", 30, 700) + 6, 600, L["of_85"], 15, C["text2"], 500))
    out.append(t(px + 310, 600, "27%", 15, C["text2"], 600, "end"))
    out.append(r(px + 20, 614, 290, 8, C["track"], rx=4))
    out.append(r(px + 20, 614, 290 * 23 / 85, 8, C["ok"], rx=4))
    out.append(t(px + 20, 644, L["todo_line"], 12.5, C["text2"]))

    out.append(button(px + 20, 760, 290, 64, L["start"], "primary", ic="crop", hint="Enter", size=16, sub=L["start_sub"]))
    out.append(button(px + 20, 834, 290, 42, L["edit_sel"], "secondary", ic="image"))
    out.append(button(px + 20, 886, 290, 42, L["to_id"], "ghost", ic="idcard"))

    # ---- status bar
    out.append(r(0, 972, WIN_W, 28, C["bar"]))
    out.append(ln(0, 972.5, WIN_W, 972.5, C["border"]))
    out.append(t(16, 990, L["status_counts"], 12, C["text2"]))
    out.append(t(WIN_W - 16, 990, L["status_right"], 12, C["text3"], 400, "end"))
    return "".join(out)


# --------------------------------------------------------------------------- mockup 2

def histogram(x, y, w, h):
    rnd = random.Random(3)
    n = 120

    def curve(params):
        vals = []
        for i in range(n):
            u = i / (n - 1)
            v = sum(a * math.exp(-0.5 * ((u - mu) / s) ** 2) for a, mu, s in params)
            vals.append(v * (1 + rnd.uniform(-0.05, 0.05)))
        return vals

    lum = curve([(0.35, 0.16, 0.06), (1.0, 0.42, 0.11), (0.75, 0.68, 0.09), (0.3, 0.88, 0.04)])
    red = curve([(0.3, 0.2, 0.07), (0.85, 0.46, 0.11), (0.6, 0.72, 0.08), (0.22, 0.9, 0.04)])
    grn = curve([(0.4, 0.18, 0.06), (0.95, 0.43, 0.1), (0.7, 0.67, 0.09), (0.25, 0.88, 0.04)])
    blu = curve([(0.45, 0.24, 0.08), (0.7, 0.5, 0.1), (0.8, 0.74, 0.07), (0.35, 0.93, 0.035)])
    top = max(max(c) for c in (lum, red, grn, blu)) * 1.08

    def pts(vals):
        return " ".join(f"{f(x + w * i / (n - 1))},{f(y + h - h * v / top)}" for i, v in enumerate(vals))

    out = [r(x, y, w, h, "#161616", rx=6, stroke=C["border"])]
    for q in (0.25, 0.5, 0.75):
        out.append(ln(x + w * q, y + 4, x + w * q, y + h - 4, "#2A2A2A"))
    out.append(f'<polygon points="{f(x)},{f(y + h)} {pts(lum)} {f(x + w)},{f(y + h)}" fill="#BDBDBD" opacity="0.32"/>')
    for vals, col in ((red, "#FF5A52"), (grn, "#34C759"), (blu, "#4C8DF6")):
        out.append(f'<polyline points="{pts(vals)}" fill="none" stroke="{col}" stroke-width="1.6" opacity="0.9"/>')
    return "".join(out)


def levels(x, y, w, black, gamma, white):
    out = [r(x, y, w, 8, "url(#g-levels)", rx=2)]
    for frac, fill in ((black, "#111111"), (gamma, "#8A8A8A"), (white, "#FFFFFF")):
        cx = x + w * frac
        out.append(f'<path d="M{f(cx)} {f(y + 11)}l7 12h-14z" fill="{fill}" stroke="#D0D0D0" stroke-width="1"/>')
    return "".join(out)


def mockup_editor(L, lang):
    out = [r(0, 0, WIN_W, WIN_H, C["bg"]), titlebar()]
    out.append(appbar(L, lang, 0, center="Popescu Maria  ›  IMG_0424.jpg", center_extra=L["unsaved"]))

    # ---- canvas toolbar
    out.append(r(0, 88, 1240, 50, C["panel"]))
    out.append(ln(0, 137.5, 1240, 137.5, C["border"]))
    out.append(icon("crop", 18, 104, 18, C["text2"]))
    x = 46
    for lab, on in [("10 × 15", True), ("15 × 20", False), (L["custom"], False)]:
        s, w = chip(x, 98, lab, on)
        out.append(s)
        x += w + 8
    x += 10
    s, w = segmented(x, 96, [L["portrait"], L["landscape"]], 1)
    out.append(s)
    x += w + 18
    for lab, ic in [(L["fit"], None), (L["reset_crop"], "reset")]:
        s, w = chip(x, 98, lab, False, ic=ic)
        out.append(s)
        x += w + 8
    s, w = chip(0, 98, L["before_after"], False, ic="eye")
    out.append(chip(1240 - 16 - w - 60, 98, L["before_after"], False, ic="eye")[0])
    out.append(kbd(1240 - 16 - 52, 103.5, L["space"])[0])

    # ---- canvas with photo and crop overlay
    out.append(r(0, 138, 1240, 734, C["canvas"]))
    img, (ix, iy, iw, ih) = place("sc-lake", 24, 160, 1192, 690)
    out.append(img)
    cx0, cw = ix + 46, iw - 70
    chh = cw * 2 / 3
    cy0 = iy + 40
    shade = "#000000"
    out.append(f'<path d="M{f(ix)} {f(iy)}h{f(iw)}v{f(ih)}h{f(-iw)}z M{f(cx0)} {f(cy0)}v{f(chh)}h{f(cw)}v{f(-chh)}z" fill="{shade}" fill-rule="evenodd" opacity="0.58"/>')
    out.append(r(cx0, cy0, cw, chh, "none", stroke="#FFFFFF", sw=1.5))
    for k in (1, 2):
        out.append(ln(cx0 + cw * k / 3, cy0, cx0 + cw * k / 3, cy0 + chh, "#FFFFFF", 1, op=0.45))
        out.append(ln(cx0, cy0 + chh * k / 3, cx0 + cw, cy0 + chh * k / 3, "#FFFFFF", 1, op=0.45))
    L_ = 26
    for (hx, hy, sx, sy) in [(cx0, cy0, 1, 1), (cx0 + cw, cy0, -1, 1), (cx0, cy0 + chh, 1, -1), (cx0 + cw, cy0 + chh, -1, -1)]:
        out.append(f'<path d="M{f(hx)} {f(hy + sy * L_)}V{f(hy)}H{f(hx + sx * L_)}" fill="none" stroke="#FFFFFF" stroke-width="5" stroke-linecap="square"/>')
    for (hx, hy, horiz) in [(cx0 + cw / 2, cy0, True), (cx0 + cw / 2, cy0 + chh, True), (cx0, cy0 + chh / 2, False), (cx0 + cw, cy0 + chh / 2, False)]:
        if horiz:
            out.append(ln(hx - 16, hy, hx + 16, hy, "#FFFFFF", 5))
        else:
            out.append(ln(hx, hy - 16, hx, hy + 16, "#FFFFFF", 5))
    px_w = round(4000 * cw / iw)
    px_h = round(px_w * 2 / 3)
    ppi = round(px_w / 6)
    label = f"10 × 15 cm  ·  {px_w} × {px_h} px  ·  {ppi} ppi"
    lw = measure(label, 13, 600) + 46
    out.append(r(cx0 + 12, cy0 + 12, lw, 30, "#000000", rx=15, op=0.62))
    out.append(icon("check", cx0 + 22, cy0 + 19, 16, C["ok"], 2.6))
    out.append(t(cx0 + 44, cy0 + 32, label, 13, "#FFFFFF", 600))
    out.append(t(620, 866, L["canvas_hint"], 12, C["text3"], 400, "middle"))

    # ---- filmstrip
    out.append(r(0, 872, 1240, 128, C["panel"]))
    out.append(ln(0, 872.5, 1240, 872.5, C["border"]))
    out.append(t(18, 900, "24", 22, C["text"], 700))
    out.append(t(18 + measure("24", 22, 700) + 5, 900, "/ 85", 15, C["text2"], 500))
    out.append(r(98, 891, 160, 6, C["track"], rx=3))
    out.append(r(98, 891, 160 * 23 / 85, 6, C["ok"], rx=3))
    out.append(t(268, 899, L["order_name"], 13, C["text2"]))
    out.append(icon("check", 1222 - measure(L["saved_toast"], 12.5) - 26, 885, 16, C["ok"], 2.5))
    out.append(t(1222, 898, L["saved_toast"], 12.5, C["text2"], 400, "end"))
    strip = ["sc-flow", "sc-fam", "sc-aut", "sc-lake", "sc-snow", "sc-fam", "sc-beach", "sc-por", "sc-flow", "sc-city", "sc-snow", "sc-aut"]
    tw_, th_ = 92, 70
    for k, scene in enumerate(strip):
        x = 18 + k * (tw_ + 9)
        y = 914
        cur = k == 3
        out.append(r(x, y, tw_, th_, C["canvas"], rx=5, stroke=C["accent"] if cur else C["border"], sw=3 if cur else 1))
        img, (ix2, iy2, iw2, ih2) = place(scene, x + 3, y + 3, tw_ - 6, th_ - 6)
        out.append(img)
        if k < 3:
            out.append(badge_check(x + tw_ - 10, y + 10, 8))
        if cur:
            out.append(f'<circle cx="{f(x + tw_ - 10)}" cy="{f(y + 10)}" r="6" fill="{C["warn"]}" stroke="#151515" stroke-width="2"/>')
        if k == 5:
            out.append(f'<circle cx="{f(x + tw_ - 10)}" cy="{f(y + 10)}" r="8" fill="{C["warn"]}" stroke="#151515" stroke-width="2"/>')
            out.append(t(x + tw_ - 10, y + 14.5, "!", 11, "#1A1A1A", 800, "middle"))

    # ---- right panel
    px, pw = 1240, 360
    ix0, iw0 = px + 18, pw - 36
    out.append(r(px, 88, pw, 912, C["panel"]))
    out.append(ln(px + 0.5, 88, px + 0.5, 1000, C["border"]))
    out.append(section(ix0, 116, L["hist"], L["reset"], iw0))
    out.append(histogram(ix0, 128, iw0, 104))
    out.append(levels(ix0, 242, iw0, 0.03, 0.52, 0.96))
    out.append(t(ix0, 286, f'{L["black"]} 8', 12, C["text2"]))
    out.append(t(ix0 + iw0 / 2, 286, f'{L["gamma"]} {L["g_val"]}', 12, C["text2"], 400, "middle"))
    out.append(t(ix0 + iw0, 286, f'{L["white"]} 246', 12, C["text2"], 400, "end"))
    out.append(button(ix0, 302, iw0, 40, L["auto"], "tinted", ic="wand"))

    out.append(section(ix0, 374, L["adjust"]))
    out.append(icon("undo", ix0 + iw0 - 50, 360, 18, C["text2"]))
    out.append(icon("redo", ix0 + iw0 - 18, 360, 18, C["text3"]))
    out.append(slider(ix0, 402, iw0, 0.58, L["exposure"], L["ev"], bipolar=True, minus_plus=True))
    out.append(slider(ix0, 460, iw0, 0.56, "Contrast", "+6", bipolar=True))
    out.append(slider(ix0, 512, iw0, 0.5, L["saturation"], "0", bipolar=True))
    out.append(slider(ix0, 564, iw0, 0.55, L["wb"], "+5", grad="g-temp", bipolar=True))

    out.append(section(ix0, 630, L["rotate"]))
    out.append(button(ix0, 642, iw0 / 2 - 5, 36, "90°", "secondary", ic="rot_ccw"))
    out.append(button(ix0 + iw0 / 2 + 5, 642, iw0 / 2 - 5, 36, "90°", "secondary", ic="rot_cw"))
    out.append(slider(ix0, 704, iw0, 0.47, L["straighten"], L["deg"], bipolar=True))
    out.append(icon("reset", ix0, 748, 16, C["accent"]))
    out.append(t(ix0 + 24, 761, L["reset_photo"], 13, C["accent"], 500))

    out.append(ln(px, 784.5, px + pw, 784.5, C["border"]))
    hb = (iw0 - 10) / 2
    out.append(button(ix0, 800, hb, 40, L["prev"], "secondary", ic="arrow_l", hint="←"))
    out.append(button(ix0 + hb + 10, 800, hb, 40, L["skip"], "secondary", ic="arrow_r", hint="→"))
    out.append(button(ix0, 850, iw0, 40, L["save"], "secondary", ic="save", hint="Ctrl+S"))
    out.append(button(ix0, 902, iw0, 76, L["save_next"], "primary", ic="arrow_r", hint="Enter", size=17, sub=L["save_next_sub"]))
    return "".join(out)


# --------------------------------------------------------------------------- mockups 3 & 4 (ID photos)

FRAME = (468.0, 176.0, 544.0, 700.0)  # x, y, w, h of the 35 × 45 mm frame on the canvas
POR_SCALE = 3.21
POR_X = FRAME[0] + FRAME[2] / 2 - 150 * POR_SCALE
POR_Y = FRAME[1] + 0.085 * FRAME[3] - 92 * POR_SCALE


def id_left_panel(L, lang, selected=1):
    out = [r(0, 88, 240, 912, C["panel"]), ln(240.5, 88, 240.5, 1000, C["border"])]
    out.append(section(18, 118, L["id_format"]))
    sizes = [("3 × 4 cm", 30, 40), (L["s35"], 35, 45), ("5 × 5 cm", 50, 50), ("9 × 12 cm", 90, 120)]
    y = 132
    for i, (lab, w, h) in enumerate(sizes):
        on = i == selected
        out.append(r(12, y, 216, 56, C["accent_tint"] if on else C["panel2"], rx=8, stroke=C["accent"] if on else C["border"], sw=1.5 if on else 1))
        k = 30 / max(w, h)
        out.append(r(24 + (30 - w * k) / 2, y + 13 + (30 - h * k) / 2, w * k, h * k, "none", rx=2, stroke="#D6E6FF" if on else C["text2"], sw=1.5))
        out.append(t(66, y + 25, lab, 14.5, C["text"], 650 if on else 500))
        px = f"{round(w / 25.4 * 300)} × {round(h / 25.4 * 300)} px @ 300 ppi"
        out.append(t(66, y + 43, px, 11.5, C["text2"]))
        y += 64
    out.append(r(12, y, 216, 44, "none", rx=8, stroke=C["border2"], extra='stroke-dasharray="4 4"'))
    out.append(icon("plus", 24, y + 13, 18, C["text2"]))
    out.append(t(52, y + 27, L["custom"], 14, C["text2"], 500))
    y += 64
    out.append(icon("warn", 16, y - 2, 16, C["warn"]))
    out.append(t(40, y + 10, L["disclaimer"], 12, C["text2"]))
    y += 104
    out.append(section(18, y, L["source"]))
    out.append(r(18, y + 14, 64, 84, C["canvas"], rx=4))
    out.append(place("sc-por", 20, y + 16, 60, 80)[0])
    out.append(t(94, y + 34, "portret_client.jpg", 13, C["text"], 600))
    out.append(t(94, y + 54, "3000 × 4000 px", 12, C["text2"]))
    out.append(icon("lock", 18, y + 110, 14, C["ok"]))
    out.append(t(38, y + 122, L["src_safe"], 12, C["ok"]))
    return "".join(out)


def id_stepper(L, step):
    out = [r(241, 88, 999, 50, C["panel"]), ln(241, 137.5, 1240, 137.5, C["border"])]
    labels = [L["step1"], L["step2"]]
    w1 = measure(labels[0], 14, 600) + 40
    w2 = measure(labels[1], 14, 600) + 40
    total = w1 + 80 + w2
    x = 740 - total / 2
    for i, lab in enumerate(labels):
        on = i == step
        done = i < step
        cx = x + 12
        fill = C["accent"] if on else (C["ok"] if done else C["raised"])
        out.append(f'<circle cx="{f(cx)}" cy="113" r="12" fill="{fill}"/>')
        if done:
            out.append(icon("check", cx - 8, 105, 16, "#FFFFFF", 3))
        else:
            out.append(t(cx, 118, str(i + 1), 13, "#FFFFFF", 700, "middle"))
        out.append(t(x + 32, 118, lab, 14, C["text"] if on else C["text2"], 600 if on else 500))
        x += (w1 if i == 0 else w2)
        if i == 0:
            out.append(ln(x, 113, x + 64, 113, C["border2"], 2))
            x += 80
    return "".join(out)


def portrait_frame_symbol():
    fx, fy, fw, fh = FRAME
    return (
        f'<symbol id="id-crop" viewBox="0 0 {f(fw)} {f(fh)}">'
        f'<use href="#sc-por" x="{f(POR_X - fx)}" y="{f(POR_Y - fy)}" width="{f(300 * POR_SCALE)}" height="{f(400 * POR_SCALE)}"/>'
        f'<rect width="{f(fw)}" height="{f(fh)}" fill="#FFFFFF" opacity="0.14"/>'
        "</symbol>"
    )


def mockup_id_editor(L, lang):
    out = [r(0, 0, WIN_W, WIN_H, C["bg"]), titlebar(), appbar(L, lang, 1, center="portret_client.jpg")]
    out.append(id_left_panel(L, lang))
    out.append(id_stepper(L, 0))

    fx, fy, fw, fh = FRAME
    out.append('<clipPath id="clip-idc"><rect x="241" y="138" width="999" height="862"/></clipPath><g clip-path="url(#clip-idc)">')
    out.append(r(241, 138, 999, 862, C["canvas"]))
    out.append(f'<use href="#sc-por" x="{f(POR_X)}" y="{f(POR_Y)}" width="{f(300 * POR_SCALE)}" height="{f(400 * POR_SCALE)}"/>')
    out.append(f'<path d="M241 138h999v862H241z M{f(fx)} {f(fy)}v{f(fh)}h{f(fw)}v{f(-fh)}z" fill="#000000" fill-rule="evenodd" opacity="0.62"/>')
    out.append(f'<use href="#id-crop" x="{f(fx)}" y="{f(fy)}" width="{f(fw)}" height="{f(fh)}"/>')
    out.append("</g>")
    out.append(r(fx, fy, fw, fh, "none", stroke="#FFFFFF", sw=2))

    crown = POR_Y + 90 * POR_SCALE
    chin = POR_Y + 255 * POR_SCALE
    eyes = POR_Y + 172 * POR_SCALE
    mid = fx + fw / 2
    green = "#5BE37D"
    # tolerance bands (head height 32–36 mm of 45 mm)
    mm = fh / 45
    out.append(r(fx, crown - 1.5 * mm, fw, 3 * mm, green, op=0.13))
    out.append(r(fx, chin - 1.5 * mm, fw, 3 * mm, green, op=0.13))
    for yy, key in [(crown, "crown"), (eyes, "eyes"), (chin, "chin")]:
        out.append(ln(fx, yy, fx + fw, yy, green, 1.6, dash="10 6" if key == "eyes" else None, op=0.95))
        tw = measure(L[key], 12.5, 600) + 18
        out.append(r(fx + fw + 10, yy - 12, tw, 24, "#0F2A17", rx=5, stroke=green))
        out.append(t(fx + fw + 10 + tw / 2, yy + 4.5, L[key], 12.5, "#BDF5CB", 600, "middle"))
    out.append(ln(mid, fy, mid, fy + fh, green, 1, dash="4 6", op=0.7))
    oval_ry = (chin - crown) / 2 + 6
    out.append(f'<ellipse cx="{f(mid)}" cy="{f((crown + chin) / 2 + 4)}" rx="{f(oval_ry * 0.74)}" ry="{f(oval_ry)}" fill="none" stroke="{green}" stroke-width="2.5" opacity="0.95"/>')
    # dimension annotations
    out.append(ln(fx, fy - 16, fx + fw, fy - 16, C["text2"], 1))
    out.append(ln(fx, fy - 22, fx, fy - 10, C["text2"], 1))
    out.append(ln(fx + fw, fy - 22, fx + fw, fy - 10, C["text2"], 1))
    out.append(r(mid - 32, fy - 27, 64, 22, C["canvas"]))
    out.append(t(mid, fy - 11, "35 mm", 12.5, C["text"], 600, "middle"))
    bx = fx - 22
    out.append(ln(bx, crown, bx, chin, green, 1.2))
    out.append(ln(bx - 6, crown, bx + 6, crown, green, 1.2))
    out.append(ln(bx - 6, chin, bx + 6, chin, green, 1.2))
    out.append(t(bx - 12, (crown + chin) / 2 - 6, L["head_h"], 12.5, "#BDF5CB", 600, "end"))
    out.append(t(bx - 12, (crown + chin) / 2 + 12, "≈ 34 mm", 12.5, C["text2"], 500, "end"))
    out.append(t(fx + fw + 26, fy + fh - 4, "45 mm", 12.5, C["text"], 600))
    out.append(r(740 - 210, 936, 420, 30, "#000000", rx=15, op=0.55))
    out.append(t(740, 956, L["id_hint"], 12.5, C["text2"], 400, "middle"))

    # ---- right panel
    px, pw = 1240, 360
    ix0, iw0 = px + 18, pw - 36
    out.append(r(px, 88, pw, 912, C["panel"]))
    out.append(ln(px + 0.5, 88, px + 0.5, 1000, C["border"]))
    out.append(button(ix0, 104, iw0, 48, L["auto_align"], "tinted", ic="face", hint="A", size=15))
    out.append(icon("check", ix0, 162, 16, C["ok"], 2.5))
    out.append(t(ix0 + 24, 175, L["face_found"], 12.5, C["text2"]))

    out.append(section(ix0, 214, L["framing"], L["guides_on"], iw0))
    out.append(slider(ix0, 242, iw0, 0.62, L["zoom"], "114%", minus_plus=True))
    out.append(slider(ix0, 300, iw0, 0.46, L["rotation"], L["rot_val"], bipolar=True))
    out.append(icon("move", ix0, 340, 18, C["text2"]))
    out.append(t(ix0 + 26, 354, L["move_hint"], 12.5, C["text2"]))

    out.append(section(ix0, 398, L["adjust"]))
    out.append(slider(ix0, 426, iw0, 0.55, L["exposure"], L["ev2"], bipolar=True, minus_plus=True))
    out.append(slider(ix0, 484, iw0, 0.53, "Contrast", "+4", bipolar=True))
    out.append(slider(ix0, 536, iw0, 0.48, L["wb"], "−3", grad="g-temp", bipolar=True))

    out.append(section(ix0, 602, L["background"]))
    out.append(slider(ix0, 630, iw0, 0.68, L["bg_bright"], "+18", grad="g-bright"))
    out.append(r(ix0, 676, iw0, 112, C["panel2"], rx=8, stroke=C["border"]))
    out.append(t(ix0 + 14, 702, L["auto_white"], 14, C["text"], 600))
    out.append(toggle(ix0 + iw0 - 52, 688, False))
    bw = measure("EXPERIMENTAL", 10.5, 700) + 14
    out.append(r(ix0 + 14, 714, bw, 18, "#4A3510", rx=4, stroke="#8A6514"))
    out.append(t(ix0 + 14 + bw / 2, 727, "EXPERIMENTAL", 10.5, "#F8D48A", 700, "middle", extra='letter-spacing="0.5"'))
    out.append(t(ix0 + 14, 754, L["auto_white_note"], 12, C["text2"]))

    out.append(ln(px, 896.5, px + pw, 896.5, C["border"]))
    out.append(button(ix0, 912, 110, 48, L["reset"], "secondary", ic="reset"))
    out.append(button(ix0 + 120, 912, iw0 - 120, 48, L["to_sheet"], "primary", ic="arrow_r", size=15))
    return "".join(out)


def mockup_id_sheet(L, lang):
    out = [r(0, 0, WIN_W, WIN_H, C["bg"]), titlebar(), appbar(L, lang, 1, center="portret_client.jpg")]
    out.append(id_left_panel(L, lang))
    out.append(id_stepper(L, 1))
    out.append(r(241, 138, 999, 862, C["canvas"]))

    # page preview: 101.6 × 152.4 mm at 4.8 px/mm
    k = 4.8
    pw_mm, ph_mm = 101.6, 152.4
    pgw, pgh = pw_mm * k, ph_mm * k
    pgx, pgy = 740 - pgw / 2, 186
    out.append(r(pgx + 6, pgy + 8, pgw, pgh, "#000000", op=0.45, rx=2))
    out.append(r(pgx, pgy, pgw, pgh, "#FFFFFF", rx=2))
    cols, rows, gap = 2, 3, 2.0
    pw, ph = 35.0, 45.0
    gw, gh = cols * pw + (cols - 1) * gap, rows * ph + (rows - 1) * gap
    ox, oy = (pw_mm - gw) / 2, (ph_mm - gh) / 2
    xs = [ox + c * (pw + gap) for c in range(cols)]
    ys = [oy + rr * (ph + gap) for rr in range(rows)]
    guide = "#9A9A9A"
    for xmm in sorted({v for x0 in xs for v in (x0, x0 + pw)}):
        out.append(ln(pgx + xmm * k, pgy, pgx + xmm * k, pgy + pgh, guide, 0.8, dash="3 3"))
    for ymm in sorted({v for y0 in ys for v in (y0, y0 + ph)}):
        out.append(ln(pgx, pgy + ymm * k, pgx + pgw, pgy + ymm * k, guide, 0.8, dash="3 3"))
    for y0 in ys:
        for x0 in xs:
            out.append(f'<use href="#id-crop" x="{f(pgx + x0 * k)}" y="{f(pgy + y0 * k)}" width="{f(pw * k)}" height="{f(ph * k)}"/>')
    # dimensions
    out.append(ln(pgx, pgy - 16, pgx + pgw, pgy - 16, C["text2"], 1))
    out.append(ln(pgx, pgy - 22, pgx, pgy - 10, C["text2"], 1))
    out.append(ln(pgx + pgw, pgy - 22, pgx + pgw, pgy - 10, C["text2"], 1))
    out.append(r(740 - 40, pgy - 27, 80, 22, C["canvas"]))
    out.append(t(740, pgy - 11, L["mm_w"], 12.5, C["text"], 600, "middle"))
    vx = pgx + pgw + 22
    out.append(ln(vx, pgy, vx, pgy + pgh, C["text2"], 1))
    out.append(ln(vx - 6, pgy, vx + 6, pgy, C["text2"], 1))
    out.append(ln(vx - 6, pgy + pgh, vx + 6, pgy + pgh, C["text2"], 1))
    out.append(t(vx + 12, pgy + pgh / 2 + 4, L["mm_h"], 12.5, C["text"], 600))
    # callout to one photo
    cx, cy = pgx + (xs[0] + pw) * k, pgy + ys[0] * k + 40
    out.append(ln(pgx - 10, cy, pgx + xs[0] * k, cy, C["accent"], 1))
    out.append(r(pgx - 236, cy - 32, 226, 64, C["panel2"], rx=8, stroke=C["accent_tint_b"]))
    out.append(t(pgx - 222, cy - 9, L["each_photo"], 12.5, C["text2"]))
    out.append(t(pgx - 222, cy + 13, L["each_dims"], 13.5, C["text"], 650))
    out.append(t(740, pgy + pgh + 40, L["page_line"], 13, C["text2"], 400, "middle"))

    # ---- right panel
    px, pw_ = 1240, 360
    ix0, iw0 = px + 18, pw_ - 36
    out.append(r(px, 88, pw_, 912, C["panel"]))
    out.append(ln(px + 0.5, 88, px + 0.5, 1000, C["border"]))
    out.append(section(ix0, 116, L["page"]))
    out.append(dropdown(ix0, 128, iw0, L["page_val"]))
    out.append(t(ix0, 182, L["page_note"], 12, C["text3"]))

    out.append(section(ix0, 224, L["layout"]))
    cards = [("6", "2 × 3", True, None), ("8", "4 × 2", False, L["tight"]), ("4", "2 × 2", False, None), ("2", "1 × 2", False, None)]
    cw, chh = (iw0 - 10) / 2, 96
    for i, (n, grid, on, warn) in enumerate(cards):
        x = ix0 + (i % 2) * (cw + 10)
        y = 238 + (i // 2) * (chh + 10)
        out.append(r(x, y, cw, chh, C["accent_tint"] if on else C["panel2"], rx=8, stroke=C["accent"] if on else C["border"], sw=1.5 if on else 1))
        # mini page
        mw, mh = (28, 42) if grid != "4 × 2" else (42, 28)
        mx, my = x + 12 + (42 - mw) / 2, y + (chh - mh) / 2
        out.append(r(mx, my, mw, mh, "#FFFFFF", rx=2, op=0.9))
        gc, gr = (int(v) for v in grid.split(" × "))
        cellw, cellh = (mw - 6) / gc, (mh - 6) / gr
        for a in range(gc):
            for b in range(gr):
                out.append(r(mx + 3 + a * cellw + 1, my + 3 + b * cellh + 1, cellw - 2, cellh - 2, "#8FA2B5"))
        out.append(t(x + 64, y + 40, n, 22, C["text"], 700))
        out.append(t(x + 64 + measure(n, 22, 700) + 6, y + 40, L["photos_word"], 13, C["text2"]))
        out.append(t(x + 64, y + 62, grid, 12.5, C["text2"]))
        if warn:
            out.append(icon("warn", x + 64, y + 70, 13, C["warn"]))
            out.append(t(x + 81, y + 81, warn, 11, C["warn"]))

    out.append(section(ix0, 474, L["copies"]))
    out.append(stepper(ix0, 486, "6", L["of_places"]))
    out.append(t(ix0, 556, L["spacing"], 13.5, C["text"], 500))
    out.append(dropdown(ix0 + iw0 - 110, 536, 110, "2 mm", 32))
    out.append(t(ix0, 598, L["margins"], 13.5, C["text"], 500))
    out.append(dropdown(ix0 + iw0 - 176, 578, 176, L["auto_center"], 32))
    out.append(icon("scissors", ix0, 624, 18, C["text2"]))
    out.append(t(ix0 + 28, 638, L["cut_guides"], 13.5, C["text"], 500))
    out.append(toggle(ix0 + iw0 - 40, 622, True))

    out.append(section(ix0, 688, L["output"]))
    out.append(r(ix0, 700, iw0, 92, C["panel2"], rx=8, stroke=C["border"]))
    out.append(t(ix0 + 14, 724, "JPEG  ·  300 ppi  ·  1200 × 1800 px", 13.5, C["text"], 600))
    out.append(t(ix0 + 14, 746, "Popescu_Maria_ID_35x45_6.jpg", 12.5, C["text2"]))
    out.append(icon("lock", ix0 + 14, 758, 14, C["ok"]))
    out.append(t(ix0 + 32, 770, L["out_note"], 12, C["ok"]))

    out.append(ln(px, 896.5, px + pw_, 896.5, C["border"]))
    out.append(button(ix0, 912, 104, 48, L["back"], "secondary", ic="arrow_l"))
    out.append(button(ix0 + 112, 912, iw0 - 112, 48, L["save_layout"], "primary", ic="save", hint="Ctrl+S", size=14.5))
    return "".join(out)


# --------------------------------------------------------------------------- strings

STR = {
    "en": {
        "tab_photos": "Photos", "tab_id": "ID Photos",
        "locations": "LOCATIONS", "disk_c": "Local Disk (C:)", "disk_d": "Orders (D:)", "usb": "USB drive KINGSTON (E:)",
        "recent": "RECENT ORDERS", "folders": "FOLDERS",
        "originals_note": "23 originals kept in “Original” (hidden)",
        "f_all": "All", "f_todo": "To do", "f_done": "Edited", "sort": "Sort: name",
        "edited": "Edited ✓", "lowres": "Low resolution for 10×15",
        "selected_photo": "SELECTED PHOTO",
        "d_dims": "4000 × 3000 px  ·  12 MP", "d_file": "3.4 MB  ·  JPEG  ·  sRGB", "d_taken": "Taken 12 Oct 2026, 14:35",
        "d_status": "Not edited yet", "d_print": "10×15 print: 667 ppi — excellent",
        "order_progress": "ORDER PROGRESS", "of_85": "of 85 edited", "todo_line": "62 photos left  ·  next: IMG_0424.jpg",
        "start": "Start editing", "start_sub": "from first unedited photo", "edit_sel": "Edit selected (1)", "to_id": "Use for ID photo",
        "status_counts": "85 photos  ·  23 edited  ·  62 to do  ·  1 low-resolution warning",
        "status_right": "Thumbnails cached  ·  PhotoX 0.1.0 (concept)",
        "custom": "Custom…", "portrait": "Portrait", "landscape": "Landscape", "fit": "Fit", "reset_crop": "Reset crop",
        "before_after": "Before / After", "space": "Space", "unsaved": "Unsaved changes",
        "canvas_hint": "Drag inside the frame to move  ·  drag handles to resize  ·  ratio locked to 10 × 15",
        "order_name": "Popescu Maria", "saved_toast": "IMG_0423.jpg saved  ·  original kept in Original/",
        "hist": "HISTOGRAM & LEVELS", "reset": "Reset", "black": "Black", "gamma": "Midtones", "g_val": "1.04", "white": "White",
        "auto": "Auto Correct", "adjust": "ADJUSTMENTS", "exposure": "Exposure", "ev": "+0.30 EV", "ev2": "+0.15 EV",
        "saturation": "Saturation", "wb": "White balance", "rotate": "ROTATE & STRAIGHTEN", "straighten": "Straighten", "deg": "−1.2°",
        "reset_photo": "Reset this photo", "prev": "Previous", "skip": "Skip", "save": "Save",
        "save_next": "Save & Next", "save_next_sub": "then opens IMG_0425.jpg",
        "id_format": "ID FORMAT", "s35": "3.5 × 4.5 cm",
        "disclaimer": "Geometric sizes only.\nAlways check the official\nrequirements of the document\n(passport, ID card, visa…).",
        "source": "SOURCE PHOTO", "src_safe": "Original is never modified",
        "step1": "Framing & adjustments", "step2": "Print sheet",
        "crown": "Crown", "eyes": "Eyes", "chin": "Chin", "head_h": "Head height",
        "id_hint": "Drag to move  ·  scroll to zoom  ·  Shift + scroll to rotate",
        "auto_align": "Auto Align", "face_found": "Face found — adjust manually if needed",
        "framing": "FRAMING", "guides_on": "Guides: on", "zoom": "Zoom", "rotation": "Rotation", "rot_val": "−0.8°",
        "move_hint": "Drag the photo or use the arrow keys",
        "background": "BACKGROUND", "bg_bright": "Background brightness", "auto_white": "Auto whiten background",
        "auto_white_note": "Check hair edges and light clothing\nbefore saving.",
        "to_sheet": "Continue to sheet",
        "page": "PAGE", "page_val": "10 × 15 cm  (4 × 6 in)", "page_note": "Exact media size is set per printer in Settings",
        "layout": "LAYOUT", "tight": "Tight margins", "photos_word": "photos",
        "copies": "COPIES", "of_places": "of 6 places", "spacing": "Spacing", "margins": "Margins", "auto_center": "Auto (centred)",
        "cut_guides": "Cut guides", "output": "OUTPUT", "out_note": "New file — source photo untouched",
        "back": "Back", "save_layout": "Save layout",
        "mm_w": "101.6 mm", "mm_h": "152.4 mm", "each_photo": "Each photo", "each_dims": "35.0 × 45.0 mm  ·  413 × 531 px",
        "page_line": "Page 10 × 15 cm (4 × 6 in)  ·  1200 × 1800 px at 300 ppi",
    },
    "ro": {
        "tab_photos": "Fotografii", "tab_id": "Foto acte",
        "locations": "LOCAȚII", "disk_c": "Disc local (C:)", "disk_d": "Comenzi (D:)", "usb": "Stick USB KINGSTON (E:)",
        "recent": "COMENZI RECENTE", "folders": "DOSARE",
        "originals_note": "23 originale păstrate în „Original” (ascuns)",
        "f_all": "Toate", "f_todo": "De editat", "f_done": "Editate", "sort": "Sortare: nume",
        "edited": "Editată ✓", "lowres": "Rezoluție mică pentru 10×15",
        "selected_photo": "FOTOGRAFIA SELECTATĂ",
        "d_dims": "4000 × 3000 px  ·  12 MP", "d_file": "3,4 MB  ·  JPEG  ·  sRGB", "d_taken": "Făcută la 12 oct. 2026, 14:35",
        "d_status": "Needitată", "d_print": "Print 10×15: 667 ppi — excelent",
        "order_progress": "PROGRES COMANDĂ", "of_85": "din 85 editate", "todo_line": "Au rămas 62  ·  urmează: IMG_0424.jpg",
        "start": "Începe editarea", "start_sub": "de la prima fotografie needitată", "edit_sel": "Editează selecția (1)", "to_id": "Folosește pentru foto acte",
        "status_counts": "85 fotografii  ·  23 editate  ·  62 de editat  ·  1 avertisment de rezoluție",
        "status_right": "Miniaturi în cache  ·  PhotoX 0.1.0 (concept)",
        "custom": "Personalizat…", "portrait": "Portret", "landscape": "Peisaj", "fit": "Potrivire", "reset_crop": "Resetare decupare",
        "before_after": "Înainte / După", "space": "Spațiu", "unsaved": "Modificări nesalvate",
        "canvas_hint": "Trageți în cadru pentru mutare  ·  trageți de margini pentru redimensionare  ·  raport fix 10 × 15",
        "order_name": "Popescu Maria", "saved_toast": "IMG_0423.jpg salvată  ·  originalul păstrat în Original/",
        "hist": "HISTOGRAMĂ ȘI NIVELURI", "reset": "Resetare", "black": "Negru", "gamma": "Tonuri medii", "g_val": "1,04", "white": "Alb",
        "auto": "Corecție automată", "adjust": "AJUSTĂRI", "exposure": "Expunere", "ev": "+0,30 EV", "ev2": "+0,15 EV",
        "saturation": "Saturație", "wb": "Balans de alb", "rotate": "ROTIRE ȘI ÎNDREPTARE", "straighten": "Îndreptare", "deg": "−1,2°",
        "reset_photo": "Resetează fotografia", "prev": "Anterioara", "skip": "Sari peste", "save": "Salvează",
        "save_next": "Salvează și următoarea", "save_next_sub": "apoi deschide IMG_0425.jpg",
        "id_format": "FORMAT ACT", "s35": "3,5 × 4,5 cm",
        "disclaimer": "Doar dimensiuni geometrice.\nVerificați mereu cerințele\noficiale ale actului\n(pașaport, buletin, viză…).",
        "source": "FOTOGRAFIE SURSĂ", "src_safe": "Originalul nu se modifică",
        "step1": "Încadrare și ajustări", "step2": "Coală de imprimare",
        "crown": "Creștet", "eyes": "Ochi", "chin": "Bărbie", "head_h": "Înălțime cap",
        "id_hint": "Trageți pentru mutare  ·  rotița pentru zoom  ·  Shift + rotița pentru rotire",
        "auto_align": "Aliniere automată", "face_found": "Față detectată — ajustați manual dacă e nevoie",
        "framing": "ÎNCADRARE", "guides_on": "Ghiduri: pornite", "zoom": "Zoom", "rotation": "Rotire", "rot_val": "−0,8°",
        "move_hint": "Trageți fotografia sau folosiți săgețile",
        "background": "FUNDAL", "bg_bright": "Luminozitate fundal", "auto_white": "Albire automată fundal",
        "auto_white_note": "Verificați marginile părului și hainele\ndeschise la culoare înainte de salvare.",
        "to_sheet": "Continuă la coală",
        "page": "COALĂ", "page_val": "10 × 15 cm  (4 × 6 in)", "page_note": "Dimensiunea exactă a hârtiei se setează în Setări",
        "layout": "ARANJARE", "tight": "Margini mici", "photos_word": "fotografii",
        "copies": "NUMĂR DE COPII", "of_places": "din 6 locuri", "spacing": "Spațiere", "margins": "Margini", "auto_center": "Automat (centrat)",
        "cut_guides": "Linii de tăiere", "output": "FIȘIER REZULTAT", "out_note": "Fișier nou — fotografia sursă rămâne neatinsă",
        "back": "Înapoi", "save_layout": "Salvează coala",
        "mm_w": "101,6 mm", "mm_h": "152,4 mm", "each_photo": "Fiecare fotografie", "each_dims": "35,0 × 45,0 mm  ·  413 × 531 px",
        "page_line": "Coală 10 × 15 cm (4 × 6 in)  ·  1200 × 1800 px la 300 ppi",
    },
}

MOCKUPS = [
    ("01", "main-explorer", "Main screen & file explorer", mockup_main),
    ("02", "photo-editor", "Ordinary photo editor", mockup_editor),
    ("03", "id-editor", "ID photo editor", mockup_id_editor),
    ("04", "id-sheet", "ID photo print-sheet preview", mockup_id_sheet),
]


def find_browser():
    for name in ("chromium", "chromium-browser", "google-chrome", "google-chrome-stable"):
        path = shutil.which(name)
        if path:
            return path
    return None


def render_png(browser, svg_path, png_path):
    subprocess.run(
        [browser, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
         f"--window-size={WIN_W},{WIN_H + LABEL_H}", f"--screenshot={png_path}", "file://" + svg_path],
        check=True, capture_output=True,
    )


def main():
    os.makedirs(SVG_DIR, exist_ok=True)
    defs = scene_defs() + portrait_frame_symbol()
    browser = find_browser()
    for num, slug, title, fn in MOCKUPS:
        for lang in ("en", "ro"):
            name = f"{num}-{slug}-{lang}"
            svg = document(num, title, lang, fn(STR[lang], lang), defs)
            svg_path = os.path.join(SVG_DIR, name + ".svg")
            with open(svg_path, "w", encoding="utf-8") as fh:
                fh.write(svg)
            if browser:
                render_png(browser, svg_path, os.path.join(PNG_DIR, name + ".png"))
            print("wrote", name)
    if not browser:
        print("Chromium/Chrome not found: SVGs written, PNGs not rendered.")


if __name__ == "__main__":
    main()
