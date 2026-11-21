"""Lobby (Home) art: slanted plates, the striped PLAY button, the Rush Pass banner, the hexagon
level badge and the medal / star / user icons. Outputs to Assets/Art/UI/Skins and Icons.

    python ArtSource/UI/gen_lobby.py
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_icons as gi  # noqa: E402
import gen_ui_skins as g  # noqa: E402

SS = g.SS
NAVY = (16, 24, 78, 255)


def slanted(name, size, slant, top, bottom, border=None, stripes=None, gloss=70, outline=5):
    """Parallelogram plate (leans right). Slice left/right = slant + 12 px when used as 9-slice."""
    w, h = size
    W, H, S = w * SS, h * SS, slant * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    def para(inset):
        i = inset * SS
        m = Image.new("L", (W, H), 0)
        ImageDraw.Draw(m).polygon([(S + i, i), (W - i, i), (W - S - i, H - i), (i, H - i)], fill=255)
        return m.filter(ImageFilter.GaussianBlur(SS * 0.5))

    o = Image.new("RGBA", (W, H), NAVY)
    o.putalpha(para(0))
    img.alpha_composite(o)
    inner = outline
    if border:
        b = Image.new("RGBA", (W, H), border)
        b.putalpha(para(outline))
        img.alpha_composite(b)
        inner += 3
    body = para(inner)
    img.alpha_composite(g.fill_mask(body, top, bottom))
    if stripes:
        st = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        d = ImageDraw.Draw(st)
        step = stripes[1] * SS
        for x in range(-H, W + H, step):
            d.polygon([(x, 0), (x + step // 2, 0), (x + step // 2 - H, H), (x - H, H)], fill=stripes[0])
        st.putalpha(Image.fromarray(np.minimum(np.asarray(st.split()[3]), np.asarray(body))))
        img.alpha_composite(st)
    if gloss:
        gm = Image.new("L", (W, H), 0)
        ImageDraw.Draw(gm).rectangle([0, 0, W, int(H * 0.42)], fill=gloss)
        gm = Image.fromarray(np.minimum(np.asarray(gm), np.asarray(para(inner + 3))))
        gw = Image.new("RGBA", (W, H), (255, 255, 255, 0))
        gw.putalpha(gm.filter(ImageFilter.GaussianBlur(SS)))
        img.alpha_composite(gw)
    out = g.bleed(img.resize((w, h), Image.LANCZOS))
    out.save(f"{g.SKINS}/{name}.png")


def hexagon(name="hex_level", size=128):
    big = size * SS
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))

    def hexm(r):
        m = Image.new("L", (big, big), 0)
        c = big / 2
        pts = [(c + math.cos(math.radians(30 + 60 * i)) * r, c + math.sin(math.radians(30 + 60 * i)) * r) for i in range(6)]
        ImageDraw.Draw(m).polygon(pts, fill=255)
        return m

    r = big * 0.47
    o = Image.new("RGBA", (big, big), NAVY)
    o.putalpha(hexm(r))
    img.alpha_composite(o)
    img.alpha_composite(g.fill_mask(hexm(r - 6 * SS), (255, 236, 120), (240, 160, 20)))
    img.alpha_composite(g.fill_mask(hexm(r - 16 * SS), (70, 150, 255), (25, 60, 190)))
    gm = Image.fromarray(np.minimum(np.asarray(hexm(r - 18 * SS)), np.asarray(Image.new("L", (big, big), 0).point(lambda _: 0))))
    out = g.bleed(img.resize((size, size), Image.LANCZOS))
    out.save(f"{g.SKINS}/{name}.png")


def medal():
    lay = gi.layer()
    gi.add(lay, gi.poly([(84, 20), (124, 20), (150, 104), (110, 104)]), (255, 90, 110), (210, 30, 60))
    gi.add(lay, gi.poly([(132, 20), (172, 20), (146, 104), (106, 104)]), (90, 170, 255), (30, 90, 220))
    disc = gi.ell((58, 92, 198, 232))
    gi.add(lay, disc, (255, 240, 120), (235, 150, 20))
    gi.add(lay, gi.ell((80, 114, 176, 210)), (245, 180, 30), (255, 225, 90))
    star = [(128 + math.cos(-math.pi / 2 + i * math.pi / 5) * (34 if i % 2 == 0 else 15),
             162 + math.sin(-math.pi / 2 + i * math.pi / 5) * (34 if i % 2 == 0 else 15)) for i in range(10)]
    gi.add(lay, gi.poly(star), (255, 255, 230), (255, 230, 140))
    gi.gloss(lay, disc, 0.4, 70)
    gi.save(lay, "medal")


def pass_star():
    lay = gi.layer()
    star = [(128 + math.cos(-math.pi / 2 + i * math.pi / 5) * (112 if i % 2 == 0 else 52),
             134 + math.sin(-math.pi / 2 + i * math.pi / 5) * (112 if i % 2 == 0 else 52)) for i in range(10)]
    m = gi.poly(star)
    gi.add(lay, m, (255, 245, 140), (245, 150, 20), (255, 205, 60))
    gi.shade(lay, m, (0, 10), (160, 70, 0, 90))
    gi.gloss(lay, m, 0.42, 110)
    gi.save(lay, "pass_star")


if __name__ == "__main__":
    slanted("plate_navy", (320, 64), 18, (40, 56, 150), (18, 26, 90), border=(110, 150, 255), gloss=45)
    slanted("plate_name", (360, 64), 18, (255, 255, 255), (220, 228, 248), gloss=0, outline=5)
    slanted("play_slanted", (520, 150), 40, (255, 236, 110), (255, 160, 20), border=(255, 250, 210),
            stripes=((255, 255, 255, 60), 64), gloss=80, outline=6)
    slanted("pass_banner", (440, 110), 30, (255, 120, 220), (120, 60, 240), border=(255, 220, 250),
            stripes=((255, 255, 255, 30), 48), gloss=60, outline=6)
    hexagon()
    medal()
    pass_star()
    print("lobby art ->", g.SKINS, g.ICONS)
