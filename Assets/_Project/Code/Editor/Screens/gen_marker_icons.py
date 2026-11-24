"""Icone olografiche dei marker del casco (128x128 RGBA, bianco su trasparente).

Stesso linguaggio visivo del mirino (T_Crosshair_Ring) e delle icone torcia (T_TorchMode_*):
esagono regolare a punte destra/sinistra con tratto spesso, leggibile sotto AnalogLook (pixel 3).
Uso: python gen_marker_icons.py  (scrive in Assets/_Project/Assets/Models/Helmet/Markers/)
"""
import math
import os
from PIL import Image, ImageDraw, ImageFont

SIZE, S = 128, 4
FONT = os.path.join(os.path.dirname(__file__), "..", "..", "..", "Assets", "Fonts", "Jersey15-Regular.ttf")
W = SIZE * S
STROKE = 9 * S  # ~7% del lato: nitido anche a 32-44 px con filtro pixel 3
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "..", "Assets", "Models", "Helmet", "Markers")


def new_canvas():
    img = Image.new("L", (W, W), 0)
    return img, ImageDraw.Draw(img)


def hex_points(cx, cy, r):
    # Punte a sinistra e destra (0, 60, 120, ...) come T_Crosshair_Ring
    return [(cx + math.cos(math.pi / 3 * i) * r, cy + math.sin(math.pi / 3 * i) * r) for i in range(6)]


def draw_polyline(d, pts, width=STROKE, closed=False):
    pts = [(x * S, y * S) for x, y in pts]
    if closed:
        pts = pts + [pts[0]]
    d.line(pts, fill=255, width=width, joint="curve")
    r = width / 2
    for x, y in pts:
        d.ellipse((x - r, y - r, x + r, y + r), fill=255)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    alpha = img.resize((SIZE, SIZE), Image.LANCZOS)
    out = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 0))
    out.putalpha(alpha)
    path = os.path.join(OUT, name + ".png")
    out.save(path)
    return path


def make_objective():
    """Esagono vuoto con diamante pieno al centro (chiusa / punto d'interesse generico)."""
    img, d = new_canvas()
    cx = cy = SIZE / 2
    draw_polyline(d, hex_points(cx, cy, 50), STROKE, closed=True)
    r = 16
    diamond = [((cx + dx) * S, (cy + dy) * S) for dx, dy in ((0, -r), (r, 0), (0, r), (-r, 0))]
    d.polygon(diamond, fill=255)
    return save(img, "T_Marker_Objective")


def make_o2():
    """Esagono vuoto con la scritta O2 in Jersey15 (font dei nomi sullo schermo comms): la goccia del primo giro si leggeva come acqua."""
    img, d = new_canvas()
    cx = cy = SIZE / 2
    draw_polyline(d, hex_points(cx, cy, 50), STROKE, closed=True)
    big = ImageFont.truetype(FONT, 58 * S)
    small = ImageFont.truetype(FONT, 34 * S)
    wo = d.textlength("O", font=big)
    w2 = d.textlength("2", font=small)
    x0 = cx * S - (wo + w2) / 2
    # Ancora "ms"/"ls": O centrata in verticale, 2 a pedice con la base un po' sotto quella della O
    d.text((x0, cy * S), "O", font=big, fill=255, anchor="lm")
    d.text((x0 + wo, (cy + 15) * S), "2", font=small, fill=255, anchor="ls")
    return save(img, "T_Marker_O2")


def make_power():
    """Esagono vuoto con fulmine netto al centro (generatore / energia)."""
    img, d = new_canvas()
    cx = cy = SIZE / 2
    draw_polyline(d, hex_points(cx, cy, 50), STROKE, closed=True)
    bolt = [
        (cx + 6, cy - 26),
        (cx - 16, cy + 4),
        (cx + 1, cy + 4),
        (cx - 6, cy + 26),
        (cx + 16, cy - 4),
        (cx - 1, cy - 4),
    ]
    d.polygon([(x * S, y * S) for x, y in bolt], fill=255)
    return save(img, "T_Marker_Power")


def make_plug():
    """Esagono vuoto con spina vista di lato, rebbi in alto (presa dove inserire la cella)."""
    img, d = new_canvas()
    cx = cy = SIZE / 2
    draw_polyline(d, hex_points(cx, cy, 50), STROKE, closed=True)
    # Corpo della spina: rettangolo pieno a spigoli smussati, cavo che scende
    d.rounded_rectangle(((cx - 15) * S, (cy - 8) * S, (cx + 15) * S, (cy + 12) * S), radius=4 * S, fill=255)
    for px in (cx - 8, cx + 8):
        d.rectangle(((px - 3) * S, (cy - 24) * S, (px + 3) * S, (cy - 8) * S), fill=255)
    d.rectangle(((cx - 3) * S, (cy + 12) * S, (cx + 3) * S, (cy + 26) * S), fill=255)
    return save(img, "T_Marker_Plug")


if __name__ == "__main__":
    paths = [make_objective(), make_o2(), make_power(), make_plug()]
    for p in paths:
        assert os.path.getsize(p) > 200, f"File troppo piccolo: {p}"
        print("OK:", p)
