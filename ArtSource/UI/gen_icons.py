"""KnockRush menu icons (cart, outfit, trophy, friends, party, settings, mail, edit, clock, person):
colourful, shaded, original drawings.

Every icon: gradient-filled shapes, a soft top highlight, a navy outline and a drop shadow,
rendered at 4x and downsampled to 256 px.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
import gen_ui_skins as g  # noqa: E402  (shared helpers: fill_mask, finish, _circle, _shine, poly_mask, P)

SS = g.SS
OUT = sys.argv[1] if len(sys.argv) > 1 else g.ICONS
os.makedirs(OUT, exist_ok=True)
BIG = 256 * SS


def layer():
    return Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))


def P(pts):
    return [(x * SS, y * SS) for x, y in pts]


def rr(box, r):
    m = Image.new("L", (BIG, BIG), 0)
    ImageDraw.Draw(m).rounded_rectangle([v * SS for v in box], radius=r * SS, fill=255)
    return m


def ell(box):
    m = Image.new("L", (BIG, BIG), 0)
    ImageDraw.Draw(m).ellipse([v * SS for v in box], fill=255)
    return m


def poly(pts):
    m = Image.new("L", (BIG, BIG), 0)
    ImageDraw.Draw(m).polygon(P(pts), fill=255)
    return m


def add(lay, mask, top, bottom, mid=None):
    lay.alpha_composite(g.fill_mask(mask, top, bottom, mid))


def shade(lay, mask, offset=(0, 10), color=(0, 0, 0, 70)):
    """Darkens the lower part of a shape (simple form shading)."""
    inner = mask.filter(ImageFilter.GaussianBlur(SS * 3))
    shifted = Image.new("L", (BIG, BIG), 0)
    shifted.paste(inner, (offset[0] * SS, -offset[1] * SS))
    a = np.clip(np.asarray(mask).astype(int) - np.asarray(shifted).astype(int), 0, 255)
    s = Image.new("RGBA", (BIG, BIG), color[:3] + (0,))
    s.putalpha(Image.fromarray((a * color[3] // 255).astype(np.uint8)))
    lay.alpha_composite(s)


def gloss(lay, mask, height=0.45, strength=80):
    gm = Image.new("L", (BIG, BIG), 0)
    bbox = mask.getbbox()
    if not bbox:
        return
    x0, y0, x1, y1 = bbox
    cut = y0 + (y1 - y0) * height
    ImageDraw.Draw(gm).rectangle([x0, y0, x1, cut], fill=strength)
    gm = Image.fromarray(np.minimum(np.asarray(gm), np.asarray(mask.filter(ImageFilter.MinFilter(9)))))
    w = Image.new("RGBA", (BIG, BIG), (255, 255, 255, 0))
    w.putalpha(gm.filter(ImageFilter.GaussianBlur(SS * 2)))
    lay.alpha_composite(w)


def save(lay, name, outline=10):
    g.finish(lay, outline=outline, name=name, folder=OUT)


# ---------------------------------------------------------------------------------------------
def shop():
    lay = layer()
    d = ImageDraw.Draw(lay)
    # handle
    d.line(P([(28, 60), (58, 60), (76, 150)]), fill=(40, 60, 140, 255), width=14 * SS, joint="curve")
    basket = poly([(62, 78), (226, 78), (206, 162), (80, 162)])
    add(lay, basket, (255, 120, 90), (230, 50, 70))
    d = ImageDraw.Draw(lay)
    for x in (104, 144, 184):
        d.line(P([(x, 84), (x - 6, 156)]), fill=(255, 220, 210, 200), width=6 * SS)
    d.line(P([(70, 118), (218, 118)]), fill=(255, 220, 210, 200), width=6 * SS)
    gloss(lay, basket, 0.4, 90)
    rail = rr((76, 166, 214, 184), 9)
    add(lay, rail, (90, 130, 230), (40, 70, 170))
    for cx in (100, 192):
        add(lay, ell((cx - 20, 186, cx + 20, 226)), (70, 80, 120), (25, 30, 60))
        add(lay, ell((cx - 9, 197, cx + 9, 215)), (255, 230, 120), (240, 170, 30))
    save(lay, "cart")


def hoodie():
    lay = layer()
    body = poly([(70, 64), (104, 46), (152, 46), (186, 64), (230, 118), (204, 146), (186, 132), (186, 224), (70, 224), (70, 132), (52, 146), (26, 118)])
    add(lay, body, (255, 92, 92), (205, 30, 50), (240, 60, 70))
    hood = ell((96, 34, 160, 84))
    add(lay, hood, (225, 50, 60), (180, 20, 40))
    shade(lay, body)
    gloss(lay, body, 0.35, 70)
    crown = poly([(100, 172), (94, 128), (114, 146), (128, 118), (142, 146), (162, 128), (156, 172)])
    add(lay, crown, (255, 255, 255), (235, 235, 245))
    d = ImageDraw.Draw(lay)
    d.rectangle(P([(70, 210), (186, 224)]), fill=(190, 25, 45, 255))
    save(lay, "hanger")  # keeps the class name; the icon is now an outfit


def trophy():
    lay = layer()
    for side in (-1, 1):
        cx = 128 + side * 70
        ring = ell((cx - 34, 66, cx + 34, 136))
        hole = ell((cx - 18, 82, cx + 18, 120))
        handle = Image.fromarray(np.clip(np.asarray(ring).astype(int) - np.asarray(hole).astype(int), 0, 255).astype(np.uint8))
        add(lay, handle, (255, 225, 90), (225, 140, 15))
    cup = poly([(62, 44), (194, 44), (186, 104), (164, 146), (92, 146), (70, 104)])
    cup = Image.fromarray(np.maximum(np.asarray(cup), np.asarray(ell((70, 70, 186, 160)))))
    add(lay, cup, (255, 240, 120), (235, 150, 20), (255, 200, 50))
    shade(lay, cup, (0, 12), (160, 80, 0, 90))
    gloss(lay, cup, 0.38, 100)
    add(lay, rr((112, 154, 144, 192), 6), (250, 200, 60), (210, 120, 10))
    base = rr((74, 190, 182, 226), 10)
    add(lay, base, (90, 120, 220), (40, 60, 160))
    add(lay, rr((96, 198, 160, 214), 4), (255, 230, 120), (240, 170, 40))
    star = []
    for i in range(10):
        r = 22 if i % 2 == 0 else 10
        a = -math.pi / 2 + i * math.pi / 5
        star.append((128 + math.cos(a) * r, 92 + math.sin(a) * r))
    add(lay, poly(star), (255, 255, 230), (255, 235, 150))
    save(lay, "trophy")


def bean_head(lay, cx, cy, r, top, bottom):
    head = ell((cx - r, cy - r * 1.1, cx + r, cy + r * 1.1))
    add(lay, head, top, bottom)
    shade(lay, head, (0, 8), (0, 0, 0, 60))
    gloss(lay, head, 0.35, 70)
    for ex in (-0.34, 0.34):
        ecx, ecy = cx + ex * r, cy - 0.12 * r
        add(lay, ell((ecx - r * 0.24, ecy - r * 0.3, ecx + r * 0.24, ecy + r * 0.3)), (255, 255, 255), (235, 240, 250))
        add(lay, ell((ecx - r * 0.1, ecy - r * 0.08, ecx + r * 0.14, ecy + r * 0.22)), (40, 40, 60), (10, 10, 25))


def friends():
    lay = layer()
    add(lay, rr((128, 150, 236, 236), 40), (255, 120, 200), (220, 60, 160))
    bean_head(lay, 182, 106, 46, (255, 140, 210), (225, 70, 170))
    add(lay, rr((22, 140, 138, 236), 44), (90, 180, 255), (40, 100, 230))
    bean_head(lay, 80, 92, 52, (110, 195, 255), (40, 110, 235))
    save(lay, "friends")


def party():
    lay = layer()
    add(lay, rr((10, 156, 98, 234), 34), (120, 230, 120), (40, 170, 70))
    bean_head(lay, 54, 118, 38, (140, 240, 130), (50, 180, 80))
    add(lay, rr((158, 156, 246, 234), 34), (255, 210, 80), (240, 150, 20))
    bean_head(lay, 202, 118, 38, (255, 225, 100), (245, 160, 30))
    add(lay, rr((72, 140, 184, 238), 44), (190, 130, 255), (120, 60, 230))
    bean_head(lay, 128, 92, 50, (200, 150, 255), (130, 70, 235))
    save(lay, "party")


def gear(name="settings"):
    lay = layer()
    pts = []
    teeth = 8
    for i in range(teeth * 4):
        a = i * 2 * math.pi / (teeth * 4) + math.pi / (teeth * 4)
        r = 104 if (i % 4) in (0, 1) else 82
        pts.append((128 + math.cos(a) * r, 128 + math.sin(a) * r))
    body = poly(pts)
    add(lay, body, (235, 242, 255), (150, 170, 210))
    shade(lay, body, (0, 10), (40, 60, 120, 90))
    gloss(lay, body, 0.4, 90)
    ring = ell((76, 76, 180, 180))
    add(lay, ring, (120, 150, 210), (190, 210, 245))
    add(lay, ell((100, 100, 156, 156)), (60, 120, 240), (30, 70, 190))
    g._shine(lay, 110 * SS, 112 * SS, 10 * SS)
    save(lay, name)


def mail():
    lay = layer()
    env = rr((26, 62, 230, 202), 18)
    add(lay, env, (255, 255, 255), (215, 225, 245))
    d = ImageDraw.Draw(lay)
    d.line(P([(32, 196), (112, 132)]), fill=(170, 185, 220, 255), width=6 * SS)
    d.line(P([(224, 196), (144, 132)]), fill=(170, 185, 220, 255), width=6 * SS)
    flap = poly([(26, 70), (230, 70), (128, 146)])
    add(lay, flap, (235, 242, 255), (185, 200, 235))
    d = ImageDraw.Draw(lay)
    d.line(P([(30, 72), (128, 146), (226, 72)]), fill=(120, 140, 200, 255), width=6 * SS, joint="curve")
    heart = Image.new("L", (BIG, BIG), 0)
    hd = ImageDraw.Draw(heart)
    hd.ellipse(P([(106, 120), (130, 144)]), fill=255)
    hd.ellipse(P([(126, 120), (150, 144)]), fill=255)
    hd.polygon(P([(108, 138), (148, 138), (128, 162)]), fill=255)
    add(lay, heart, (255, 100, 110), (215, 30, 60))
    save(lay, "mail")


def pencil():
    lay = layer()
    shaft = poly([(170, 40), (214, 84), (100, 198), (56, 154)])
    add(lay, shaft, (255, 220, 70), (240, 150, 20))
    gloss(lay, shaft, 0.4, 70)
    add(lay, poly([(190, 20), (234, 64), (214, 84), (170, 40)]), (255, 150, 180), (235, 90, 140))
    add(lay, poly([(178, 32), (222, 76), (212, 86), (168, 42)]), (220, 225, 240), (160, 170, 200))
    add(lay, poly([(56, 154), (100, 198), (38, 216)]), (255, 230, 190), (230, 190, 140))
    add(lay, poly([(46, 186), (68, 208), (38, 216)]), (70, 70, 90), (30, 30, 45))
    save(lay, "edit", outline=12)


def bell_clock(name="clock"):
    lay = layer()
    face = ell((36, 36, 220, 220))
    add(lay, face, (255, 255, 255), (220, 230, 250))
    d = ImageDraw.Draw(lay)
    d.ellipse(P([(36, 36), (220, 220)]), outline=(60, 120, 230, 255), width=14 * SS)
    d.line(P([(128, 128), (128, 72)]), fill=(30, 40, 100, 255), width=14 * SS)
    d.line(P([(128, 128), (168, 150)]), fill=(30, 40, 100, 255), width=12 * SS)
    add(lay, ell((114, 114, 142, 142)), (255, 90, 100), (220, 40, 60))
    save(lay, name, outline=12)


def person():
    lay = layer()
    add(lay, rr((50, 140, 206, 236), 48), (255, 255, 255), (215, 225, 245))
    bean_head(lay, 128, 86, 50, (255, 255, 255), (215, 225, 245))
    save(lay, "person", outline=12)


if __name__ == "__main__":
    shop()
    hoodie()
    trophy()
    friends()
    party()
    gear()
    mail()
    pencil()
    bell_clock()
    person()
    print("icons ->", OUT)
