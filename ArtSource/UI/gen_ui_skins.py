"""Generates KnockRush UI art: 9-slice button/card/panel skins (Assets/Art/UI/Skins) and the
coin, gem, crown, stopwatch, gift, missions, plus, play-arrows and alert icons (Assets/Art/UI/Icons).

    python ArtSource/UI/gen_ui_skins.py     then     python ArtSource/UI/gen_icons.py

Requires Pillow and NumPy. Everything is drawn here (no third-party art).
"""
import math
import os
import shutil
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "KnockRush", "Assets", "Art", "UI")
ICONS = ROOT + "/Icons"
SKINS = ROOT + "/Skins"
os.makedirs(ICONS, exist_ok=True)
os.makedirs(SKINS, exist_ok=True)

NAVY = (16, 24, 78, 255)
SS = 4  # supersampling


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def vgrad(size, top, bottom, mid=None):
    w, h = size
    arr = np.zeros((h, w, 4), np.uint8)
    for y in range(h):
        t = y / max(1, h - 1)
        if mid is not None:
            c = lerp(top, mid, t * 2) if t < 0.5 else lerp(mid, bottom, (t - 0.5) * 2)
        else:
            c = lerp(top, bottom, t)
        arr[y, :, :len(c)] = c
        if len(c) == 3:
            arr[y, :, 3] = 255
    return Image.fromarray(arr, "RGBA")


def fill_mask(mask, top, bottom, mid=None):
    g = vgrad(mask.size, top, bottom, mid)
    out = Image.new("RGBA", mask.size, (0, 0, 0, 0))
    out.paste(g, (0, 0), mask)
    return out


def dilate(alpha, px):
    if px <= 0:
        return alpha
    return alpha.filter(ImageFilter.MaxFilter(px * 2 + 1)) if px < 6 else _dilate_big(alpha, px)


def _dilate_big(alpha, px):
    a = alpha
    while px > 0:
        step = min(px, 5)
        a = a.filter(ImageFilter.MaxFilter(step * 2 + 1))
        px -= step
    return a


def finish(layer, size=256, outline=10, shadow=8, name=None, folder=ICONS):
    """layer: RGBA at SS*size. Adds navy outline + drop shadow, downsamples, bleeds colour, saves."""
    big = size * SS
    alpha = layer.split()[3].point(lambda v: 255 if v > 8 else 0)
    ring = dilate(alpha, outline * SS // 2)
    out = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    if shadow:
        sh = Image.new("RGBA", (big, big), (10, 16, 60, 0))
        sh.putalpha(ring.point(lambda v: int(v * 0.55)))
        sh = sh.filter(ImageFilter.GaussianBlur(SS * 2))
        out.alpha_composite(sh, (0, shadow * SS // 2))
    navy = Image.new("RGBA", (big, big), NAVY)
    navy.putalpha(ring)
    out.alpha_composite(navy)
    out.alpha_composite(layer)
    img = out.resize((size, size), Image.LANCZOS)
    img = bleed(img)
    if name:
        img.save(f"{folder}/{name}.png")
    return img


def bleed(img):
    """Give transparent pixels their neighbours' colour (no dark fringes when filtered)."""
    arr = np.asarray(img).copy()
    a = arr[..., 3]
    rgb = Image.fromarray(arr[..., :3])
    blurred = np.asarray(rgb.filter(ImageFilter.GaussianBlur(3))).astype(float)
    weight = np.asarray(Image.fromarray(a).filter(ImageFilter.GaussianBlur(3))).astype(float)[..., None] / 255.0
    premul = np.asarray(Image.fromarray((arr[..., :3] * (a[..., None] / 255.0)).astype(np.uint8)).filter(ImageFilter.GaussianBlur(3))).astype(float)
    fill = np.where(weight > 0.01, premul / np.maximum(weight, 0.01), blurred)
    mask = a < 8
    arr[..., :3][mask] = np.clip(fill[mask], 0, 255).astype(np.uint8)
    return Image.fromarray(arr, "RGBA")


def poly_mask(big, pts, scale=1.0):
    m = Image.new("L", (big, big), 0)
    ImageDraw.Draw(m).polygon([(x * scale, y * scale) for x, y in pts], fill=255)
    return m


# ---------------------------------------------------------------------------------------------
# Custom icons (coordinates on a 256 grid, drawn at SS)
# ---------------------------------------------------------------------------------------------
def P(pts):
    return [(x * SS, y * SS) for x, y in pts]


def coin(name="coin"):
    big = 256 * SS
    layer = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    c = 128 * SS
    r = 104 * SS
    edge = fill_mask(_circle(big, c, c + 6 * SS, r), (230, 140, 10), (190, 95, 0))
    layer.alpha_composite(edge)
    face = fill_mask(_circle(big, c, c - 4 * SS, r - 4 * SS), (255, 236, 110), (250, 175, 25))
    layer.alpha_composite(face)
    inner = fill_mask(_circle(big, c, c - 4 * SS, r - 26 * SS), (245, 180, 30), (255, 220, 80))
    layer.alpha_composite(inner)
    # embossed crown
    crown = [(78, 150), (70, 92), (104, 118), (128, 78), (152, 118), (186, 92), (178, 150)]
    cm = poly_mask(big, P(crown))
    ImageDraw.Draw(cm).rectangle(P([(80, 152), (176, 170)]), fill=255)
    layer.alpha_composite(fill_mask(cm, (255, 250, 200), (255, 215, 90)))
    shade = cm.filter(ImageFilter.GaussianBlur(SS))
    dark = Image.new("RGBA", (big, big), (200, 110, 0, 0))
    dark.putalpha(Image.fromarray((np.clip(np.asarray(shade).astype(int) - np.asarray(cm).astype(int), 0, 255)).astype(np.uint8)))
    layer.alpha_composite(dark, (0, 4 * SS))
    _shine(layer, c - 50 * SS, c - 62 * SS, 22 * SS)
    return finish(layer, outline=8, name=name)


def _circle(big, cx, cy, r):
    m = Image.new("L", (big, big), 0)
    ImageDraw.Draw(m).ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
    return m


def _shine(layer, x, y, r):
    big = layer.size[0]
    s = Image.new("RGBA", (big, big), (255, 255, 255, 0))
    m = Image.new("L", (big, big), 0)
    ImageDraw.Draw(m).ellipse([x - r, y - r * 0.6, x + r, y + r * 0.6], fill=170)
    s.putalpha(m.filter(ImageFilter.GaussianBlur(SS * 2)))
    layer.alpha_composite(s)


def gem(name="gem"):
    big = 256 * SS
    layer = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    top, mid_y, bot = 52, 104, 214
    outline = [(64, mid_y), (96, top), (160, top), (192, mid_y), (128, bot)]
    layer.alpha_composite(fill_mask(poly_mask(big, P(outline)), (200, 120, 255), (120, 40, 220)))
    facets = [
        ([(96, top), (128, top), (112, mid_y)], (240, 200, 255), (210, 150, 255)),
        ([(128, top), (160, top), (144, mid_y)], (225, 170, 255), (190, 120, 250)),
        ([(64, mid_y), (96, top), (112, mid_y)], (190, 110, 250), (170, 90, 240)),
        ([(160, top), (192, mid_y), (144, mid_y)], (170, 90, 240), (150, 70, 230)),
        ([(112, mid_y), (128, top), (144, mid_y)], (250, 230, 255), (230, 190, 255)),
        ([(64, mid_y), (112, mid_y), (128, bot)], (170, 80, 245), (120, 40, 210)),
        ([(112, mid_y), (144, mid_y), (128, bot)], (215, 150, 255), (150, 70, 235)),
        ([(144, mid_y), (192, mid_y), (128, bot)], (140, 50, 225), (95, 25, 190)),
    ]
    for pts, a, b in facets:
        layer.alpha_composite(fill_mask(poly_mask(big, P(pts)), a, b))
    _shine(layer, 104 * SS, 74 * SS, 14 * SS)
    return finish(layer, outline=8, name=name)


def crown_shape(name="crown", size=256):
    big = 256 * SS
    layer = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    body = [(40, 180), (30, 84), (84, 128), (128, 56), (172, 128), (226, 84), (216, 180)]
    layer.alpha_composite(fill_mask(poly_mask(big, P(body)), (255, 240, 120), (245, 160, 20)))
    band = Image.new("L", (big, big), 0)
    ImageDraw.Draw(band).rounded_rectangle(P([(36, 168), (220, 206)]), radius=8 * SS, fill=255)
    layer.alpha_composite(fill_mask(band, (255, 210, 60), (220, 120, 10)))
    d = ImageDraw.Draw(layer)
    for x, y in [(30, 84), (128, 56), (226, 84)]:
        layer.alpha_composite(fill_mask(_circle(big, x * SS, y * SS, 15 * SS), (255, 250, 200), (250, 190, 40)))
    for x, col in [(82, (60, 160, 255)), (128, (255, 70, 90)), (174, (60, 160, 255))]:
        layer.alpha_composite(fill_mask(_circle(big, x * SS, 187 * SS, 11 * SS), lerp(col, (255, 255, 255), 0.4) + (255,), col + (255,)))
    _shine(layer, 92 * SS, 118 * SS, 20 * SS)
    return finish(layer, outline=10, name=name)


def stopwatch(name="stopwatch"):
    big = 256 * SS
    layer = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    cx, cy, r = 128, 144, 88
    btn = Image.new("L", (big, big), 0)
    ImageDraw.Draw(btn).rounded_rectangle(P([(108, 28), (148, 56)]), radius=6 * SS, fill=255)
    ImageDraw.Draw(btn).rectangle(P([(120, 50), (136, 64)]), fill=255)
    layer.alpha_composite(fill_mask(btn, (255, 230, 100), (235, 150, 20)))
    side = Image.new("L", (big, big), 0)
    ImageDraw.Draw(side).polygon(P([(190, 64), (212, 84), (196, 100), (176, 80)]), fill=255)
    layer.alpha_composite(fill_mask(side, (255, 230, 100), (235, 150, 20)))
    layer.alpha_composite(fill_mask(_circle(big, cx * SS, cy * SS, r * SS), (255, 225, 90), (240, 150, 15)))
    layer.alpha_composite(fill_mask(_circle(big, cx * SS, cy * SS, (r - 18) * SS), (90, 200, 255), (30, 110, 230)))
    layer.alpha_composite(fill_mask(_circle(big, cx * SS, cy * SS, (r - 28) * SS), (255, 255, 255), (215, 235, 255)))
    for i in range(12):
        a = i * math.pi / 6
        r0, r1 = (r - 36), (r - 46 if i % 3 == 0 else r - 40)
        d.line([((cx + math.sin(a) * r0) * SS, (cy - math.cos(a) * r0) * SS), ((cx + math.sin(a) * r1) * SS, (cy - math.cos(a) * r1) * SS)], fill=(40, 70, 150, 255), width=4 * SS)
    d.line([(cx * SS, cy * SS), ((cx + 30) * SS, (cy - 34) * SS)], fill=(240, 60, 70, 255), width=8 * SS)
    d.line([(cx * SS, cy * SS), (cx * SS, (cy - 44) * SS)], fill=(30, 50, 120, 255), width=7 * SS)
    layer.alpha_composite(fill_mask(_circle(big, cx * SS, cy * SS, 9 * SS), (60, 80, 150), (20, 30, 90)))
    _shine(layer, 92 * SS, 90 * SS, 18 * SS)
    return finish(layer, outline=10, name=name)


def gift(name="gift"):
    big = 256 * SS
    layer = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    box = Image.new("L", (big, big), 0)
    ImageDraw.Draw(box).rounded_rectangle(P([(52, 118), (204, 222)]), radius=8 * SS, fill=255)
    layer.alpha_composite(fill_mask(box, (255, 80, 90), (200, 20, 50)))
    lid = Image.new("L", (big, big), 0)
    ImageDraw.Draw(lid).rounded_rectangle(P([(40, 92), (216, 132)]), radius=8 * SS, fill=255)
    layer.alpha_composite(fill_mask(lid, (255, 110, 120), (225, 40, 65)))
    rib = Image.new("L", (big, big), 0)
    ImageDraw.Draw(rib).rectangle(P([(114, 92), (142, 222)]), fill=255)
    layer.alpha_composite(fill_mask(rib, (255, 240, 120), (245, 170, 20)))
    bow = Image.new("L", (big, big), 0)
    bd = ImageDraw.Draw(bow)
    bd.ellipse(P([(62, 40), (128, 98)]), fill=255)
    bd.ellipse(P([(128, 40), (194, 98)]), fill=255)
    inner = Image.new("L", (big, big), 0)
    ImageDraw.Draw(inner).ellipse(P([(84, 58), (120, 90)]), fill=255)
    ImageDraw.Draw(inner).ellipse(P([(136, 58), (172, 90)]), fill=255)
    bow = Image.fromarray(np.clip(np.asarray(bow).astype(int) - np.asarray(inner).astype(int), 0, 255).astype(np.uint8))
    layer.alpha_composite(fill_mask(bow, (255, 245, 140), (240, 165, 20)))
    knot = Image.new("L", (big, big), 0)
    ImageDraw.Draw(knot).rounded_rectangle(P([(112, 78), (144, 102)]), radius=6 * SS, fill=255)
    layer.alpha_composite(fill_mask(knot, (255, 230, 100), (230, 150, 10)))
    _shine(layer, 82 * SS, 140 * SS, 16 * SS)
    return finish(layer, outline=10, name=name)


def clipboard(name="missions"):
    big = 256 * SS
    layer = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    board = Image.new("L", (big, big), 0)
    ImageDraw.Draw(board).rounded_rectangle(P([(54, 46), (202, 230)]), radius=14 * SS, fill=255)
    layer.alpha_composite(fill_mask(board, (80, 170, 255), (30, 90, 220)))
    paper = Image.new("L", (big, big), 0)
    ImageDraw.Draw(paper).rounded_rectangle(P([(70, 66), (186, 216)]), radius=6 * SS, fill=255)
    layer.alpha_composite(fill_mask(paper, (255, 255, 255), (220, 232, 250)))
    clip = Image.new("L", (big, big), 0)
    ImageDraw.Draw(clip).rounded_rectangle(P([(98, 32), (158, 70)]), radius=10 * SS, fill=255)
    layer.alpha_composite(fill_mask(clip, (255, 225, 90), (235, 150, 20)))
    d = ImageDraw.Draw(layer)
    for i, y in enumerate((106, 144, 182)):
        d.line(P([(82, y), (94, y + 12), (114, y - 10)]), fill=(40, 190, 80, 255), width=9 * SS, joint="curve")
        d.rounded_rectangle(P([(126, y - 4), (172, y + 6)]), radius=4 * SS, fill=(150, 175, 215, 255))
    return finish(layer, outline=10, name=name)


def plus(name="plus"):
    big = 256 * SS
    m = Image.new("L", (big, big), 0)
    d = ImageDraw.Draw(m)
    d.rounded_rectangle(P([(104, 40), (152, 216)]), radius=14 * SS, fill=255)
    d.rounded_rectangle(P([(40, 104), (216, 152)]), radius=14 * SS, fill=255)
    return finish(fill_mask(m, (255, 255, 255), (225, 235, 255)), outline=12, shadow=6, name=name)


def chevrons(name="play_arrows"):
    big = 256 * SS
    m = Image.new("L", (big, big), 0)
    d = ImageDraw.Draw(m)
    for ox in (0, 78):
        d.polygon(P([(40 + ox, 56), (84 + ox, 56), (148 + ox, 128), (84 + ox, 200), (40 + ox, 200), (104 + ox, 128)]), fill=255)
    return finish(fill_mask(m, (255, 255, 255), (255, 236, 200)), outline=10, shadow=6, name=name)


def alert(name="alert"):
    big = 256 * SS
    layer = fill_mask(_circle(big, 128 * SS, 128 * SS, 100 * SS), (255, 90, 90), (215, 20, 40))
    d = ImageDraw.Draw(layer)
    d.rounded_rectangle(P([(114, 58), (142, 150)]), radius=12 * SS, fill=(255, 255, 255, 255))
    d.ellipse(P([(112, 164), (144, 196)]), fill=(255, 255, 255, 255))
    return finish(layer, outline=14, shadow=4, name=name)


# ---------------------------------------------------------------------------------------------
# 9-slice skins
# ---------------------------------------------------------------------------------------------
def skin(name, size, radius, top, bottom, border=None, lip=None, gloss=70, outline=4, inner=None, mid=None, glow=None, alpha=255):
    w, h = size
    W, H = w * SS, h * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    pad = 0
    if glow:
        g = Image.new("L", (W, H), 0)
        ImageDraw.Draw(g).rounded_rectangle([2 * SS, 2 * SS, W - 2 * SS, H - 2 * SS], radius=(radius + 4) * SS, fill=255)
        gl = Image.new("RGBA", (W, H), glow)
        gl.putalpha(g.filter(ImageFilter.GaussianBlur(4 * SS)))
        img.alpha_composite(gl)
        pad = 8
    def rr(inset, r):
        m = Image.new("L", (W, H), 0)
        i = (pad + inset) * SS
        ImageDraw.Draw(m).rounded_rectangle([i, i, W - i - 1, H - i - 1], radius=max(1, r) * SS, fill=255)
        return m
    if outline:
        o = Image.new("RGBA", (W, H), NAVY[:3] + (255,))
        o.putalpha(rr(0, radius))
        img.alpha_composite(o)
    if border:
        b = Image.new("RGBA", (W, H), border)
        b.putalpha(rr(outline, radius - outline))
        img.alpha_composite(b)
    bw = outline + (3 if border else 0)
    if lip:
        l = Image.new("RGBA", (W, H), lip)
        l.putalpha(rr(bw, radius - bw))
        img.alpha_composite(l)
    body = rr(bw, radius - bw)
    if lip:
        # body sits above the lip: shift up by the lip height
        lip_h = 8 * SS
        arr = np.asarray(body).copy()
        arr = np.concatenate([arr[lip_h:], np.zeros((lip_h, W), np.uint8)])
        arr2 = np.asarray(rr(bw, radius - bw))
        body = Image.fromarray(np.minimum(arr, arr2))
    img.alpha_composite(fill_mask(body, top, bottom, mid))
    if inner:
        ring = Image.fromarray(np.clip(np.asarray(rr(bw + 1, radius - bw - 1)).astype(int) - np.asarray(rr(bw + 4, radius - bw - 4)).astype(int), 0, 255).astype(np.uint8))
        ring = Image.fromarray(np.minimum(np.asarray(ring), np.asarray(body)))
        il = Image.new("RGBA", (W, H), inner)
        il.putalpha(ring)
        img.alpha_composite(il)
    if gloss:
        gm = Image.new("L", (W, H), 0)
        i = (pad + bw + 5) * SS
        ImageDraw.Draw(gm).rounded_rectangle([i, i, W - i, int(H * 0.46)], radius=max(1, radius - bw - 5) * SS, fill=gloss)
        gm = Image.fromarray(np.minimum(np.asarray(gm), np.asarray(body)))
        gw = Image.new("RGBA", (W, H), (255, 255, 255, 0))
        gw.putalpha(gm.filter(ImageFilter.GaussianBlur(SS)))
        img.alpha_composite(gw)
    out = img.resize((w, h), Image.LANCZOS)
    if alpha < 255:
        a = np.asarray(out).copy()
        a[..., 3] = (a[..., 3].astype(int) * alpha // 255).astype(np.uint8)
        out = Image.fromarray(a, "RGBA")
    bleed(out).save(f"{SKINS}/{name}.png")


def main():
    coin()
    gem()
    crown_shape()
    stopwatch()
    gift()
    clipboard()
    plus()
    chevrons()
    alert()

    # skins: (name, size, radius, top, bottom, ...)
    skin("btn_yellow", (160, 160), 34, (255, 232, 90), (255, 150, 20), border=(255, 245, 190), lip=(205, 95, 10), gloss=80, mid=(255, 196, 40))
    skin("btn_green", (96, 96), 20, (150, 240, 90), (40, 170, 50), border=(210, 255, 180), lip=(20, 120, 40), gloss=70)
    skin("btn_red", (96, 96), 20, (255, 120, 120), (215, 40, 60), border=(255, 200, 200), lip=(150, 20, 40), gloss=60)
    skin("btn_purple", (96, 96), 20, (190, 130, 255), (110, 50, 220), border=(230, 200, 255), lip=(70, 30, 160), gloss=60)
    skin("btn_blue", (96, 96), 20, (90, 160, 255), (40, 80, 220), border=(150, 205, 255), lip=(25, 50, 160), gloss=60)
    skin("card_blue", (160, 160), 30, (60, 140, 255), (30, 60, 200), border=(120, 200, 255), gloss=50, mid=(40, 100, 235))
    skin("card_blue_sel", (176, 176), 30, (80, 170, 255), (35, 80, 225), border=(170, 245, 255), gloss=60, mid=(50, 125, 245), glow=(120, 230, 255, 255))
    skin("card_purple", (160, 160), 30, (190, 110, 255), (100, 40, 215), border=(230, 190, 255), gloss=50, mid=(150, 70, 240))
    skin("card_purple_sel", (176, 176), 30, (205, 130, 255), (115, 50, 225), border=(250, 220, 255), gloss=60, mid=(165, 85, 245), glow=(235, 170, 255, 255))
    skin("card_season", (160, 160), 30, (255, 110, 220), (110, 50, 230), border=(255, 200, 245), gloss=50, mid=(190, 80, 240))
    skin("panel_navy", (128, 128), 26, (40, 60, 160), (20, 30, 100), border=(110, 150, 255), gloss=0, outline=3, alpha=225)
    skin("bar_track", (64, 32), 14, (10, 20, 70), (25, 40, 110), gloss=0, outline=2)
    skin("bar_fill", (64, 32), 14, (255, 240, 120), (250, 160, 20), gloss=90, outline=0)
    skin("frame_gold", (128, 128), 28, (255, 240, 140), (230, 140, 10), border=(255, 250, 210), gloss=60, mid=(255, 200, 50))
    skin("avatar_bg", (128, 128), 22, (160, 225, 255), (70, 150, 245), gloss=0, outline=0)
    skin("tag_white", (96, 64), 18, (255, 255, 255), (225, 232, 250), gloss=0, outline=3)

    print("generated", len(os.listdir(ICONS)), "icons,", len(os.listdir(SKINS)), "skins")


if __name__ == "__main__":
    main()
