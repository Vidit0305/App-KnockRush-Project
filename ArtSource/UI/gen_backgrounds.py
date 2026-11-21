"""Menu page backgrounds derived from the painted home arena (Assets/Art/UI/Backgrounds):
  PageBackground.png   - soft, darker blue blur for full-screen menu pages (lists stay readable)
  LockerBackground.png - purple dressing-room look with a spotlight, behind the 3D character

    python ArtSource/UI/gen_backgrounds.py
"""
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "KnockRush", "Assets", "Art", "UI", "Backgrounds")
home = Image.open(os.path.join(ROOT, "HomeBackground.png")).convert("RGB")
W, H = 1280, 720
base = home.resize((W, H), Image.LANCZOS)
yy, xx = np.mgrid[0:H, 0:W]
nx, ny = (xx - W / 2) / (W / 2), (yy - H / 2) / (H / 2)


def mix(img, color, amount):
    a = np.asarray(img).astype(float)
    return Image.fromarray(np.clip(a * (1 - amount) + np.array(color) * amount, 0, 255).astype(np.uint8))


# Page: heavy blur, deep-blue grade, vignette.
page = base.filter(ImageFilter.GaussianBlur(22))
page = mix(page, (22, 40, 120), 0.45)
vig = np.clip(1.0 - 0.55 * (nx ** 2 * 0.6 + ny ** 2), 0.35, 1.0)[..., None]
page = Image.fromarray(np.clip(np.asarray(page).astype(float) * vig, 0, 255).astype(np.uint8))
page.save(os.path.join(ROOT, "PageBackground.png"))

# Locker: lighter blur, purple grade, a warm spotlight where the character stands.
locker = base.filter(ImageFilter.GaussianBlur(10))
locker = mix(locker, (70, 30, 150), 0.5)
spot = np.exp(-(((nx + 0.0) / 0.42) ** 2 + ((ny - 0.25) / 0.75) ** 2))[..., None]
glow = np.array((255, 220, 255)) * 0.35
arr = np.asarray(locker).astype(float) * (0.7 + 0.45 * spot) + glow * spot
floor = np.exp(-((nx / 0.38) ** 2 + ((ny - 0.62) / 0.09) ** 2))[..., None]
arr = arr * (1 - 0.5 * floor) + np.array((255, 210, 90)) * 0.5 * floor
vig = np.clip(1.0 - 0.5 * (nx ** 2 * 0.7 + ny ** 2), 0.4, 1.0)[..., None]
Image.fromarray(np.clip(arr * vig, 0, 255).astype(np.uint8)).resize((1672, 941), Image.LANCZOS).save(os.path.join(ROOT, "LockerBackground.png"))
print("backgrounds ->", ROOT)
