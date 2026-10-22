"""
Generates the Hub's UI sprites (white shapes tinted in Unity, plus the coloured background) into
Assets/_Collection/Art/Hub/UI. Everything is drawn 4x and downsampled for clean anti-aliased edges.
Run: python Tools/hub_art.py
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "_Collection", "Art", "Hub", "UI")
FONT = os.path.join(ROOT, "Assets", "Most In One", "Common", "Third-Party", "Fonts", "Nunito.ttf")
SS = 4  # supersampling

WHITE = (255, 255, 255, 255)


def canvas(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (255, 255, 255, 0))


def down(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"))


def rounded_rect(w, h, r, name):
    img = canvas(w, h)
    ImageDraw.Draw(img).rounded_rectangle([0, 0, w * SS - 1, h * SS - 1], radius=r * SS, fill=WHITE)
    save(down(img, w, h), name)


def circle(size, name):
    img = canvas(size, size)
    ImageDraw.Draw(img).ellipse([0, 0, size * SS - 1, size * SS - 1], fill=WHITE)
    save(down(img, size, size), name)


def ring(size, width, name):
    img = canvas(size, size)
    d = ImageDraw.Draw(img)
    d.ellipse([0, 0, size * SS - 1, size * SS - 1], fill=WHITE)
    w = width * SS
    d.ellipse([w, w, size * SS - 1 - w, size * SS - 1 - w], fill=(255, 255, 255, 0))
    save(down(img, size, size), name)


def soft_shadow(size, r, blur, name):
    """A blurred rounded rect (white, alpha only) for drop shadows; 9-sliced in Unity."""
    pad = blur * 2
    full = size + pad * 2
    img = Image.new("L", (full, full), 0)
    ImageDraw.Draw(img).rounded_rectangle([pad, pad, pad + size, pad + size], radius=r, fill=255)
    img = img.filter(ImageFilter.GaussianBlur(blur))
    out = Image.new("RGBA", (full, full), (255, 255, 255, 0))
    out.putalpha(img)
    save(out, name)


def vertical_fade(w, h, top_alpha, bottom_alpha, name):
    img = Image.new("RGBA", (w, h))
    for y in range(h):
        a = int(top_alpha + (bottom_alpha - top_alpha) * y / (h - 1))
        for x in range(w):
            img.putpixel((x, y), (255, 255, 255, a))
    save(img, name)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def background(name, w=540, h=960):
    """Violet to sky-blue gradient with a soft light spot behind the logo and darker corners."""
    top, mid, bottom = (108, 59, 240), (74, 91, 245), (42, 168, 255)
    img = Image.new("RGB", (w, h))
    px = img.load()
    for y in range(h):
        t = y / (h - 1)
        base = lerp(top, mid, t / 0.5) if t < 0.5 else lerp(mid, bottom, (t - 0.5) / 0.5)
        for x in range(w):
            # light spot near the top, vignette at the edges
            dx, dy = (x - w / 2) / w, (y - h * 0.22) / h
            glow = max(0.0, 1 - math.sqrt(dx * dx * 2.2 + dy * dy * 5)) * 0.28
            vx, vy = (x - w / 2) / (w / 2), (y - h / 2) / (h / 2)
            vig = min(1.0, (vx * vx + vy * vy) * 0.5) * 0.22
            c = tuple(min(255, int(base[i] + (255 - base[i]) * glow - base[i] * vig)) for i in range(3))
            px[x, y] = c
    save(img.convert("RGBA"), name)


def pattern(name, size=512, seed=7):
    """Seamless tile of faint playful shapes (white, low alpha)."""
    rng = random.Random(seed)
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    S = size * SS
    for _ in range(26):
        kind = rng.choice(["circle", "ring", "square", "triangle", "plus"])
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        r = rng.uniform(14, 34) * SS
        a = rng.randint(14, 26)
        col = (255, 255, 255, a)
        rot = rng.uniform(0, math.pi)
        for ox in (-S, 0, S):  # draw wrapped copies so the tile is seamless
            for oy in (-S, 0, S):
                cx, cy = x + ox, y + oy
                if kind == "circle":
                    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col)
                elif kind == "ring":
                    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=col, width=int(r * 0.35))
                elif kind == "square":
                    pts = [(cx + r * math.cos(rot + k * math.pi / 2), cy + r * math.sin(rot + k * math.pi / 2)) for k in range(4)]
                    d.polygon(pts, fill=col)
                elif kind == "triangle":
                    pts = [(cx + r * math.cos(rot + k * 2 * math.pi / 3), cy + r * math.sin(rot + k * 2 * math.pi / 3)) for k in range(3)]
                    d.polygon(pts, fill=col)
                else:
                    t = r * 0.32
                    d.rectangle([cx - r, cy - t, cx + r, cy + t], fill=col)
                    d.rectangle([cx - t, cy - r, cx + t, cy + r], fill=col)
    save(down(big, size, size), name)


def rays(name, size=512, count=14):
    img = canvas(size, size)
    d = ImageDraw.Draw(img)
    c = size * SS / 2
    R = c * 1.0
    for k in range(count):
        a0 = 2 * math.pi * k / count
        a1 = a0 + math.pi / count * 0.8
        d.polygon([(c, c), (c + R * math.cos(a0), c + R * math.sin(a0)), (c + R * math.cos(a1), c + R * math.sin(a1))], fill=WHITE)
    img = down(img, size, size)
    # fade towards the edge
    mask = Image.new("L", (size, size), 0)
    md = mask.load()
    for y in range(size):
        for x in range(size):
            r = math.hypot(x - size / 2, y - size / 2) / (size / 2)
            md[x, y] = int(255 * max(0.0, 1 - r) ** 1.4)
    alpha = Image.composite(img.split()[3], Image.new("L", (size, size), 0), mask)
    out = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    out.putalpha(Image.eval(alpha, lambda v: int(v * 0.55)))
    save(out, name)


def icon(name, draw_fn, size=128):
    img = canvas(size, size)
    draw_fn(ImageDraw.Draw(img), size * SS)
    save(down(img, size, size), name)


def draw_home(d, S):
    m = S * 0.12
    d.polygon([(S / 2, m), (S - m, S * 0.48), (m, S * 0.48)], fill=WHITE)
    d.rounded_rectangle([S * 0.22, S * 0.42, S * 0.78, S - m], radius=S * 0.06, fill=WHITE)
    d.rounded_rectangle([S * 0.42, S * 0.62, S * 0.58, S - m], radius=S * 0.04, fill=(255, 255, 255, 0))


def draw_play(d, S):
    pts = [(S * 0.28, S * 0.16), (S * 0.86, S * 0.5), (S * 0.28, S * 0.84)]
    d.polygon(pts, fill=WHITE)
    for p in pts:  # rounded corners
        d.ellipse([p[0] - S * 0.05, p[1] - S * 0.05, p[0] + S * 0.05, p[1] + S * 0.05], fill=WHITE)


def draw_plus(d, S):
    t = S * 0.11
    d.rounded_rectangle([S * 0.14, S / 2 - t, S * 0.86, S / 2 + t], radius=t, fill=WHITE)
    d.rounded_rectangle([S / 2 - t, S * 0.14, S / 2 + t, S * 0.86], radius=t, fill=WHITE)


def draw_close(d, S):
    w = S * 0.13
    for a in (math.pi / 4, -math.pi / 4):
        cx, cy, L = S / 2, S / 2, S * 0.36
        dx, dy = math.cos(a) * L, math.sin(a) * L
        d.line([(cx - dx, cy - dy), (cx + dx, cy + dy)], fill=WHITE, width=int(w))
        for px, py in ((cx - dx, cy - dy), (cx + dx, cy + dy)):
            d.ellipse([px - w / 2, py - w / 2, px + w / 2, py + w / 2], fill=WHITE)


def draw_check(d, S):
    w = S * 0.13
    pts = [(S * 0.18, S * 0.52), (S * 0.42, S * 0.76), (S * 0.84, S * 0.28)]
    d.line(pts, fill=WHITE, width=int(w), joint="curve")
    for px, py in (pts[0], pts[2]):
        d.ellipse([px - w / 2, py - w / 2, px + w / 2, py + w / 2], fill=WHITE)


def draw_tv(d, S):
    """'Watch ad' icon: a screen with a play triangle."""
    d.rounded_rectangle([S * 0.08, S * 0.2, S * 0.92, S * 0.78], radius=S * 0.1, fill=WHITE)
    d.polygon([(S * 0.42, S * 0.34), (S * 0.66, S * 0.49), (S * 0.42, S * 0.64)], fill=(255, 255, 255, 0))
    d.rounded_rectangle([S * 0.3, S * 0.84, S * 0.7, S * 0.92], radius=S * 0.04, fill=WHITE)


def draw_noads(d, S):
    """'AD' in a circle with a strike."""
    w = S * 0.09
    d.ellipse([w, w, S - w, S - w], outline=WHITE, width=int(w))
    font = ImageFont.truetype(FONT, int(S * 0.36))
    try:
        font.set_variation_by_axes([900])  # heaviest weight when the font is variable
    except Exception:
        pass
    tw = d.textlength("AD", font=font)
    d.text(((S - tw) / 2, S * 0.27), "AD", font=font, fill=WHITE)
    a = math.pi / 4
    L = S / 2 - w
    d.line([(S / 2 - math.cos(a) * L, S / 2 - math.sin(a) * L), (S / 2 + math.cos(a) * L, S / 2 + math.sin(a) * L)], fill=WHITE, width=int(w))


def draw_gift(d, S):
    d.rounded_rectangle([S * 0.14, S * 0.42, S * 0.86, S * 0.9], radius=S * 0.06, fill=WHITE)
    d.rounded_rectangle([S * 0.08, S * 0.3, S * 0.92, S * 0.46], radius=S * 0.05, fill=WHITE)
    d.rectangle([S * 0.45, S * 0.3, S * 0.55, S * 0.9], fill=(255, 255, 255, 0))
    d.ellipse([S * 0.22, S * 0.1, S * 0.5, S * 0.34], outline=WHITE, width=int(S * 0.07))
    d.ellipse([S * 0.5, S * 0.1, S * 0.78, S * 0.34], outline=WHITE, width=int(S * 0.07))


def draw_back(d, S):
    w = S * 0.13
    pts = [(S * 0.62, S * 0.18), (S * 0.3, S * 0.5), (S * 0.62, S * 0.82)]
    d.line(pts, fill=WHITE, width=int(w), joint="curve")
    for px, py in (pts[0], pts[2]):
        d.ellipse([px - w / 2, py - w / 2, px + w / 2, py + w / 2], fill=WHITE)


def main():
    rounded_rect(192, 192, 48, "rr48")      # cards, panels (9-slice border 56)
    rounded_rect(96, 96, 24, "rr24")        # small chips (border 28)
    rounded_rect(256, 128, 64, "pill")      # pills / buttons (border 63)
    circle(256, "circle")
    ring(256, 22, "ring")
    soft_shadow(160, 44, 26, "shadow")      # border 100
    vertical_fade(8, 256, 110, 0, "gloss")
    vertical_fade(8, 256, 0, 200, "fade_down")
    background("bg_gradient")
    pattern("bg_pattern")
    rays("rays")
    for n, f in [("ic_home", draw_home), ("ic_play", draw_play), ("ic_plus", draw_plus), ("ic_close", draw_close),
                 ("ic_check", draw_check), ("ic_tv", draw_tv), ("ic_noads", draw_noads), ("ic_gift", draw_gift),
                 ("ic_back", draw_back)]:
        icon(n, f)
    print("hub art ->", OUT)


if __name__ == "__main__":
    main()
