"""
Frames the raw store screenshots for Google Play: caption on a gradient, the shot below in a rounded phone-like frame.

  1. Run the [Explicit] test HubSnapshots.CaptureStoreShots  -> Logs/StoreShots/<n>_<id>.png (1080x1920)
  2. python ArtPrompts/store_frames.py                        -> ArtPrompts/kept/Store screenshots/<n>_<id>.png

Output is 1080x1920 (9:16), inside Google Play's phone screenshot limits.
"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Logs" / "StoreShots"
OUT = Path(__file__).resolve().parent / "kept" / "Store screenshots"
FONT = ROOT / "Assets" / "Most In One" / "Common" / "Third-Party" / "Fonts" / "Nunito.ttf"

W, H = 1080, 1920
TOP, BOTTOM = (124, 77, 255), (56, 34, 150)   # hub purple
CAPTIONS = {
    "hub": ("12 GAMES", "IN ONE APP"),
    "squad": ("BUILD YOUR", "SQUAD"),
    "blocks": ("CLEAR THE", "BLOCKS"),
    "cubey": ("SLIDE & SOLVE", "PUZZLES"),
    "crowd": ("GROW THE", "BIGGEST CROWD"),
    "dodge": ("DODGE THE", "BULLS"),
    "roads": ("SWITCH LANES", "FAST"),
    "bomb": ("SURVIVE THE", "BLASTS"),
}


def font(size):
    return ImageFont.truetype(str(FONT), size)


def gradient():
    img = Image.new("RGB", (W, H))
    d = ImageDraw.Draw(img)
    for y in range(H):
        t = y / (H - 1)
        d.line([(0, y), (W, y)], fill=tuple(round(a + (b - a) * t) for a, b in zip(TOP, BOTTOM)))
    return img


def centered(d, y, text, f, fill, shadow=True):
    w = d.textlength(text, font=f)
    if shadow:
        d.text(((W - w) / 2 + 4, y + 6), text, font=f, fill=(30, 16, 90))
    d.text(((W - w) / 2, y), text, font=f, fill=fill)


def frame(shot_path):
    key = shot_path.stem.split("_", 1)[-1]
    line1, line2 = CAPTIONS.get(key, ("ALL MIGHTY", "ONE"))
    canvas = gradient()
    d = ImageDraw.Draw(canvas)
    centered(d, 70, line1, font(78), (255, 255, 255))
    centered(d, 160, line2, font(104), (255, 214, 64))

    # shot scaled into the lower area, rounded corners, soft shadow and a white rim
    shot = Image.open(shot_path).convert("RGB")
    sh = H - 330 - 50
    sw = round(shot.width * sh / shot.height)
    shot = shot.resize((sw, sh), Image.LANCZOS)
    x, y, r, rim = (W - sw) // 2, 330, 48, 10
    mask = Image.new("L", (sw, sh), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, sw - 1, sh - 1), r, fill=255)

    shadow = Image.new("L", (W, H), 0)
    ImageDraw.Draw(shadow).rounded_rectangle((x - rim, y - rim + 18, x + sw + rim, y + sh + rim + 18), r + rim, fill=140)
    canvas.paste((20, 10, 60), (0, 0), shadow.filter(ImageFilter.GaussianBlur(24)))
    ImageDraw.Draw(canvas).rounded_rectangle((x - rim, y - rim, x + sw + rim, y + sh + rim), r + rim, fill=(255, 255, 255))
    canvas.paste(shot, (x, y), mask)
    return canvas


def main():
    shots = sorted(SRC.glob("*.png"))
    if not shots:
        sys.exit(f"No shots in {SRC}: run the HubSnapshots.CaptureStoreShots test first")
    OUT.mkdir(parents=True, exist_ok=True)
    for p in shots:
        frame(p).save(OUT / p.name)
        print(OUT / p.name)


if __name__ == "__main__":
    main()
