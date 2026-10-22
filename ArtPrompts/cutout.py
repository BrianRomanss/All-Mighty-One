"""Turns kept Gemini images (ArtPrompts/kept/<group>/) into the hub's art (Assets/_Collection/Art/...).

  python ArtPrompts/cutout.py                 every job whose kept image exists
  python ArtPrompts/cutout.py icon_ logo      only jobs whose name starts with one of these

Gemini puts a small sparkle in the bottom-right corner: full-bleed art (icons, background, store art) is cropped
past it, the logo has it blanked before its white background is keyed out. Then rebuild the hub
(All Mighty One > Hub > Build Hub And Home Buttons) so it picks the new art up.
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
KEPT = os.path.join(HERE, "kept")
HUB = os.path.join(PROJECT, "Assets", "_Collection", "Art", "Hub")
STORE = os.path.join(PROJECT, "Assets", "_Collection", "Art", "Store")
GAMES = ["squad", "crowd", "brain", "pistol", "dodge", "roads", "bomb", "ninja", "blocks", "adventure", "cubey", "splat"]


def find(name):
    for root, _, files in os.walk(KEPT):
        if name in files: return os.path.join(root, name)
    return None


def inset_square(img, inset=0.07, size=512):
    """Centre square, trimmed by `inset` on every side (drops the sparkle and any edge frame), resized."""
    w, h = img.size
    s = min(w, h)
    x0, y0 = (w - s) // 2, (h - s) // 2
    m = int(s * inset)
    return img.crop((x0 + m, y0 + m, x0 + s - m, y0 + s - m)).resize((size, size), Image.LANCZOS)


def cover(img, w, h, bottom_trim=0.0, right_trim=0.0):
    """Fills w x h (crop the overflow, centred), after trimming the sparkle edge."""
    W, H = img.size
    img = img.crop((0, 0, int(W * (1 - right_trim)), int(H * (1 - bottom_trim))))
    W, H = img.size
    scale = max(w / W, h / H)
    img = img.resize((int(W * scale + 0.5), int(H * scale + 0.5)), Image.LANCZOS)
    x0, y0 = (img.width - w) // 2, (img.height - h) // 2
    return img.crop((x0, y0, x0 + w, y0 + h))


def key_out(img, sparkle=0.1, t0=0.06, t1=0.18):
    """Removes a flat light background reachable from the border (keeps light colours inside the shape), trims."""
    a = np.asarray(img.convert("RGB")).astype(np.float32) / 255.0
    h, w, _ = a.shape
    border = np.concatenate([a[:8].reshape(-1, 3), a[-8:].reshape(-1, 3), a[:, :8].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    s = int(min(w, h) * sparkle)
    a[h - s:, w - s:] = bg  # the sparkle
    d = np.abs(a - bg).max(axis=2)
    bgish = Image.fromarray(((d < t0 * 1.5) * 255).astype(np.uint8)).copy()
    for xy in [(x, 0) for x in range(0, w, 16)] + [(x, h - 1) for x in range(0, w, 16)] + \
              [(0, y) for y in range(0, h, 16)] + [(w - 1, y) for y in range(0, h, 16)]:
        if bgish.getpixel(xy) == 255: ImageDraw.floodfill(bgish, xy, 128)
    outside = np.asarray(Image.fromarray(((np.asarray(bgish) == 128) * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5))) > 0
    alpha = np.clip((d - t0) / (t1 - t0), 0, 1)
    alpha = np.where(outside, alpha, 1.0)
    safe = np.maximum(alpha, 1e-3)[..., None]
    rgb = np.clip((a - bg * (1 - alpha[..., None])) / safe, 0, 1)
    out = Image.fromarray((np.dstack([rgb, alpha]) * 255).astype(np.uint8), "RGBA")
    return out.crop(out.getbbox())


def fit_width(img, max_w):
    if img.width <= max_w: return img
    return img.resize((max_w, int(img.height * max_w / img.width)), Image.LANCZOS)


JOBS = {f"icon_{g}": (f"icon_{g}.png", os.path.join(HUB, "Icons", f"{g}.png"), lambda im: inset_square(im)) for g in GAMES}
JOBS.update({
    "logo": ("logo.png", os.path.join(HUB, "Logo.png"), lambda im: fit_width(key_out(im), 1400)),
    "bg_hub": ("bg_hub.png", os.path.join(HUB, "Background.png"), lambda im: cover(im, 1080, 2340, bottom_trim=0.07)),
    "app_icon": ("app_icon.png", os.path.join(STORE, "app_icon.png"), lambda im: inset_square(im, 0.06, 1024)),
    "feature_graphic": ("feature_graphic.png", os.path.join(STORE, "feature_graphic.png"), lambda im: cover(im, 1024, 500, bottom_trim=0.08)),
})


def main():
    prefixes = sys.argv[1:]
    done = 0
    for name, (src_name, dest, fn) in JOBS.items():
        if prefixes and not any(name.startswith(p) for p in prefixes): continue
        src = find(src_name)
        if not src: continue
        out = fn(Image.open(src).convert("RGBA" if name == "logo" else "RGB"))
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        out.save(dest)
        print(f"{src_name} -> {os.path.relpath(dest, PROJECT)}  {out.width}x{out.height}")
        done += 1
    print(f"{done} image(s) cut out" + ("" if done else " (nothing kept yet: python ArtPrompts/gemini_gen.py keep <file> <n>)"))


if __name__ == "__main__":
    main()
