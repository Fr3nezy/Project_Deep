"""Fotogrammi placeholder dei cartelli CRT animati (bianco su trasparente, 256x128, 6 fotogrammi).

Stile delle icone T_Screen_*: contorno spesso con estremità arrotondate. Disegnati a 4x e ridotti per l'antialias.
Uso: python gen_sign_frames.py  (scrive in Assets/_Project/Assets/Models/Screens/Animated/)
Da sostituire con i disegni di Manu tenendo gli stessi nomi file.
"""
import math
import os

from PIL import Image, ImageDraw

W, H, S = 256, 128, 4
STROKE = 6 * S
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "..", "Assets", "Models", "Screens", "Animated")


def canvas():
    img = Image.new("L", (W * S, H * S), 0)
    return img, ImageDraw.Draw(img)


def line(d, pts, width=STROKE):
    pts = [(x * S, y * S) for x, y in pts]
    d.line(pts, fill=255, width=width, joint="curve")
    r = width / 2
    for x, y in (pts[0], pts[-1]):
        d.ellipse((x - r, y - r, x + r, y + r), fill=255)


def poly(d, pts, width=STROKE):
    line(d, pts + [pts[0]], width)


def fish(d, cx, cy, size, facing):
    """Pesce a contorno: corpo a goccia e coda a V. facing +1 guarda a destra, -1 a sinistra."""
    body = []
    for i in range(24):
        a = 2 * math.pi * i / 24
        body.append((cx + math.cos(a) * size * facing, cy + math.sin(a) * size * 0.55))
    poly(d, body, int(STROKE * 0.8))
    tx = cx - size * facing
    line(d, [(tx, cy), (tx - size * 0.7 * facing, cy - size * 0.5), (tx - size * 0.7 * facing, cy + size * 0.5), (tx, cy)], int(STROKE * 0.8))
    ex = cx + size * 0.45 * facing
    r = size * 0.12 * S
    d.ellipse((ex * S - r, (cy - size * 0.1) * S - r, ex * S + r, (cy - size * 0.1) * S + r), fill=255)


def cell(d, cx, cy, h):
    """Cella a profilo conico come T_Screen_Cell_Full, alta h."""
    top, bottom = cy - h / 2, cy + h / 2
    poly(d, [(cx - h * 0.18, top + h * 0.12), (cx + h * 0.18, top + h * 0.12), (cx + h * 0.32, bottom), (cx - h * 0.32, bottom)])
    line(d, [(cx - h * 0.1, top), (cx + h * 0.1, top)])
    line(d, [(cx - h * 0.42, bottom), (cx + h * 0.42, bottom)])


def rays(d, cx, cy, r0, r1, count=8, width=STROKE):
    for i in range(count):
        a = 2 * math.pi * i / count + math.pi / count
        line(d, [(cx + math.cos(a) * r0, cy + math.sin(a) * r0), (cx + math.cos(a) * r1, cy + math.sin(a) * r1)], width)


def hexagon(d, cx, cy, r):
    poly(d, [(cx + math.cos(math.pi / 3 * i) * r, cy + math.sin(math.pi / 3 * i) * r) for i in range(6)])


def torch(d, cx, cy, red):
    """Icona della modalità torcia: esagono con raggi; la rossa ha 3 bande dentro e raggi corti (T_TorchMode_Red)."""
    hexagon(d, cx, cy, 14)
    if red:
        for dy in (-6, 0, 6):
            line(d, [(cx - 8, cy + dy), (cx + 8, cy + dy)], int(STROKE * 0.6))
        rays(d, cx, cy, 20, 26)
    else:
        rays(d, cx, cy, 20, 34)


def save(img, name):
    img = img.resize((W, H), Image.LANCZOS)
    out = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    out.putalpha(img)
    os.makedirs(OUT, exist_ok=True)
    out.save(os.path.join(OUT, name + ".png"))


def light_attracts():
    """Cella accesa che attira: la cella si accende, i pesci arrivano dai lati e le si stringono attorno."""
    cx, cy = W / 2, H / 2 + 4
    # (distanza dei pesci dal centro, cella accesa, seconda coppia di pesci)
    steps = [(98, False, False), (90, True, False), (76, True, False), (62, True, True), (50, True, True), (50, True, True)]
    for i, (dist, lit, second) in enumerate(steps):
        img, d = canvas()
        cell(d, cx, cy, 56)
        if lit:
            rays(d, cx, cy - 4, 40, 50 if i % 2 else 46, 8, int(STROKE * 0.7))
        fish(d, cx - dist, cy - 10, 15, +1)
        fish(d, cx + dist, cy + 12, 15, -1)
        if second:
            fish(d, cx - dist - 14, cy + 34, 12, +1)
            fish(d, cx + dist + 12, cy - 34, 12, -1)
        save(img, "T_Sign_LightAttracts_%02d" % i)


def red_light():
    """Fauna e luce rossa: il pesce va verso il cono della torcia bianca; la torcia diventa rossa e il pesce passa oltre
    sopra la torcia, senza girarsi: non la vede (non scappa)."""
    tx, ty = 40, H / 2
    # (x del pesce, y del pesce, direzione, torcia rossa)
    steps = [(220, ty, -1, False), (175, ty, -1, False), (130, ty, -1, False), (115, ty - 10, -1, True), (75, ty - 30, -1, True), (20, ty - 38, -1, True)]
    for i, (fx, fy, facing, red) in enumerate(steps):
        img, d = canvas()
        torch(d, tx, ty, red)
        if not red:
            line(d, [(tx + 36, ty - 8), (W - 8, ty - 44)], int(STROKE * 0.6))
            line(d, [(tx + 36, ty + 8), (W - 8, ty + 44)], int(STROKE * 0.6))
        fish(d, fx, fy, 18, facing)
        save(img, "T_Sign_RedLight_%02d" % i)


# --- FAUNA PROTOCOL: cartello di sicurezza Thaladyne a vignette numerate con ✓/✕ (Flusso-Slice, 2026-11-24) ---

ASSETS = os.path.join(os.path.dirname(__file__), "..", "..", "..", "Assets")
ICONS = {
    "full": os.path.join(ASSETS, "Models", "Photocell", "Screen", "T_TorchMode_Full.png"),
    "red": os.path.join(ASSETS, "Models", "Photocell", "Screen", "T_TorchMode_Red.png"),
    "cell": os.path.join(ASSETS, "Models", "Screens", "T_Screen_Cell_Full.png"),
}
FONT = os.path.join(ASSETS, "Fonts", "VT323-Regular.ttf")
GROUND = 118
WORKER_X = 50


def icon(img, key, cx, cy, size):
    """Incolla un'icona vera del progetto (alfa), così torcia e cella sono identiche a quelle del casco e dei CRT."""
    a = Image.open(ICONS[key]).convert("RGBA").getchannel("A").resize((int(size * S), int(size * S)), Image.LANCZOS)
    img.paste(255, (int((cx - size / 2) * S), int((cy - size / 2) * S)), a)


def viperfish(d, cx, cy, length, facing, jaw=0.0):
    """Viperfish di profilo: corpo lungo, mascella inferiore sporgente con zanne fuori dalla bocca, esca sul dorso.
    facing +1 guarda a destra; jaw in radianti apre la mascella verso il basso."""
    hx, hy = 0.28, 0.10
    ca, sa = math.cos(jaw), math.sin(jaw)

    def j(x, y):
        dx, dy = x - hx, y - hy
        return hx + dx * ca - dy * sa, hy + dx * sa + dy * ca

    def p(pts):
        return [(cx + x * length * facing, cy + y * length * 1.2) for x, y in pts]

    w = int(STROKE * 0.55)
    body = [(0.5, -0.01), (0.44, -0.11), (0.25, -0.15), (0.0, -0.12), (-0.25, -0.07), (-0.42, -0.03),
            (-0.56, -0.15), (-0.5, 0.0), (-0.56, 0.15), (-0.42, 0.03), (-0.25, 0.07), (0.0, 0.11), (0.2, 0.13),
            (hx, hy), j(0.57, 0.08), j(0.38, 0.04), (0.29, 0.03)]
    poly(d, p(body), w)
    fang = int(STROKE * 0.4)
    line(d, p([j(0.47, 0.07), j(0.45, -0.12)]), fang)
    line(d, p([j(0.55, 0.08), j(0.55, -0.06)]), fang)
    line(d, p([(0.45, -0.03), (0.46, 0.07)]), fang)
    # esca su un raggio che parte da metà dorso, non dalla testa: sulla testa si legge come rana pescatrice (lettura agy)
    line(d, p([(-0.05, -0.12), (-0.02, -0.28), (0.15, -0.34), (0.3, -0.3)]), int(STROKE * 0.35))
    for x, y, r in ((0.31, -0.29, 0.035), (0.4, -0.06, 0.025)):
        (px, py), = p([(x, y)])
        rr = r * length * S
        d.ellipse((px * S - rr, py * S - rr, px * S + rr, py * S + rr), fill=255)


def worker(d, x, holding):
    """Operaio col casco tondo e visore, di fronte a destra; holding alza il braccio verso la mano a (x+16, 90)."""
    hy = GROUND - 2 - 52
    w = int(STROKE * 0.7)
    r = 10
    d.ellipse(((x - r) * S, (hy - r) * S, (x + r) * S, (hy + r) * S), outline=255, width=w)
    poly(d, [(x + 2, hy - 4), (x + 7, hy - 4), (x + 7, hy + 3), (x + 2, hy + 3)], int(STROKE * 0.4))
    poly(d, [(x - 8, hy + 12), (x + 8, hy + 12), (x + 8, hy + 36), (x - 8, hy + 36)], w)
    line(d, [(x - 4, hy + 36), (x - 5, GROUND - 2)], w)
    line(d, [(x + 4, hy + 36), (x + 5, GROUND - 2)], w)
    line(d, [(x + 7, hy + 15), (x + 16, hy + 26)] if holding else [(x + 9, hy + 15), (x + 11, hy + 32)], w)


def lit_cell(img, d, cx, cy, size, frame, ground=False):
    """Cella accesa che pulsa; a terra solo i raggi sopra il suolo."""
    icon(img, "cell", cx, cy, size)
    r0, r1 = size * 0.62, size * (0.85 if frame % 2 else 0.78)
    for i in range(8):
        a = 2 * math.pi * i / 8 + math.pi / 8
        if not (ground and math.sin(a) > 0.2):
            line(d, [(cx + math.cos(a) * r0, cy + math.sin(a) * r0), (cx + math.cos(a) * r1, cy + math.sin(a) * r1)], int(STROKE * 0.45))


def mark(d, ok):
    x0, y0, x1, y1 = W - 30, 6, W - 8, 28
    w = int(STROKE * 0.9)
    if ok:
        line(d, [(x0, y0 + 12), (x0 + 8, y1), (x1, y0)], w)
    else:
        line(d, [(x0, y0), (x1, y1)], w)
        line(d, [(x0, y1), (x1, y0)], w)


def dashed(d, a, b, n=7):
    for k in range(0, n * 2, 2):
        t0, t1 = k / (n * 2 - 1), (k + 1) / (n * 2 - 1)
        line(d, [(a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0), (a[0] + (b[0] - a[0]) * t1, a[1] + (b[1] - a[1]) * t1)], int(STROKE * 0.45))


def vignette(number, torch_mode, held, cell_ground, fish_steps, ok):
    """Un fotogramma per passo del pesce; l'ultimo ha il ✓/✕ ed è ripetuto per tenerlo a schermo.
    fish_steps: (x, y, facing, jaw). L'operaio è identico in ogni fotogramma della vignetta; cell_ground "drop" mostra
    la cella in mano solo nel primo fotogramma, così si vede che è la stessa posata a terra (lettura agy)."""
    from PIL import ImageFont
    font = ImageFont.truetype(FONT, 30 * S)
    frames = []
    for i, (fx, fy, facing, jaw) in enumerate(fish_steps + fish_steps[-1:]):
        img, d = canvas()
        d.text((6 * S, -2 * S), str(number), font=font, fill=255)
        line(d, [(4, GROUND), (W - 4, GROUND)], int(STROKE * 0.4))
        holding = held or (cell_ground == "drop" and i == 0)
        worker(d, WORKER_X, holding)
        if torch_mode:
            icon(img, torch_mode, 18, 64, 32)
        if torch_mode == "full":
            line(d, [(WORKER_X + 12, 62), (W - 40, 34)], int(STROKE * 0.45))
            line(d, [(WORKER_X + 12, 66), (W - 40, 96)], int(STROKE * 0.45))
        elif torch_mode == "red":
            # cono corto e tratteggiato: sul CRT ambra il rosso non si vede, senza cono l'icona si legge «luce spenta» (lettura agy)
            dashed(d, (WORKER_X + 12, 62), (WORKER_X + 92, 46))
            dashed(d, (WORKER_X + 12, 66), (WORKER_X + 92, 82))
        if holding:
            lit_cell(img, d, WORKER_X + 20, 86, 26, i)
        elif cell_ground:
            lit_cell(img, d, 126, GROUND - 14, 26, i, True)
        viperfish(d, fx, fy, 80, facing, jaw)
        if i >= len(fish_steps) - 1:
            mark(d, ok)
        frames.append(img)
    return frames


def fauna_protocol():
    """A luce rossa: torcia bianca ✕ (il viperfish punta il cono e apre la bocca), rossa ✓ (passa oltre senza girarsi).
    B cella: in mano ✕, anche con la rossa ✕, a terra e operaio fermo ✓ (il pesce resta sulla cella; se se ne va sembra
    respinto, lettura agy):
    da CreatureStealth, la cella in mano allerta sempre e dopo averla mollata il pesce attacca se il sub si muove."""
    attack = [(222, 74, -1, 0.0), (184, 72, -1, 0.0), (150, 72, -1, 0.2), (128, 74, -1, 0.7)]
    cycles = {
        "A": [
            vignette(1, "full", False, False, attack, False),
            vignette(2, "red", False, False, [(222, 40, -1, 0.0), (168, 40, -1, 0.0), (120, 40, -1, 0.0), (82, 40, -1, 0.0)], True),
        ],
        "B": [
            vignette(1, "full", True, False, attack, False),
            vignette(2, "red", True, False, attack, False),
            vignette(3, "red", False, "drop", [(244, 70, -1, 0.0), (222, 72, -1, 0.0), (186, 80, -1, 0.0), (176, 86, -1, 0.0), (178, 84, -1, 0.0)], True),
        ],
    }
    for key, vignettes in cycles.items():
        for i, img in enumerate(f for v in vignettes for f in v):
            save(img, "T_Fauna_%s_%02d" % (key, i))


if __name__ == "__main__":
    light_attracts()
    red_light()
    fauna_protocol()
    # self-check: fotogrammi 256x128 con qualcosa disegnato (12 cartelli, 10 + 16 FAUNA PROTOCOL)
    names = sorted(f for f in os.listdir(OUT) if f.startswith(("T_Sign_", "T_Fauna_")) and f.endswith(".png"))
    assert len(names) == 38, names
    for n in names:
        im = Image.open(os.path.join(OUT, n))
        assert im.size == (W, H) and im.getextrema()[3][1] == 255, n
    print("ok", len(names))
