"""Kullanım: python docs/frame.py <ham-klasör> docs/screens
Ham ekran görüntülerini telefon çerçevesine koyar ve bir vitrin (hero) görseli üretir."""
import sys, os
from PIL import Image, ImageDraw, ImageFilter

raw, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)

from PIL import ImageFont
FONT = ImageFont.truetype(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "Kuafor.App", "Resources", "Fonts", "Inter-SemiBold.otf"), 38)

def statusbar(img):
    """Ekran görüntüsünün üst boşluğuna sade bir Android durum çubuğu çizer (11:20, sinyal, wifi, pil)."""
    img = img.convert("RGB")
    w, _ = img.size
    bg = img.getpixel((w // 2, 20))
    dark = sum(bg) / 3 < 128
    fg = (235, 236, 240) if dark else (25, 25, 32)
    d = ImageDraw.Draw(img)
    cy = 48
    d.text((70, cy), "11:20", font=FONT, fill=fg, anchor="lm")
    x = w - 70
    # pil
    d.rounded_rectangle([x - 52, cy - 14, x - 6, cy + 14], 6, outline=fg, width=3)
    d.rectangle([x - 6, cy - 6, x - 1, cy + 6], fill=fg)
    d.rounded_rectangle([x - 47, cy - 9, x - 11, cy + 9], 3, fill=fg)
    x -= 80
    # sinyal
    for i in range(4):
        hh = 8 + i * 7
        d.rectangle([x - 40 + i * 11, cy + 14 - hh, x - 33 + i * 11, cy + 14], fill=fg)
    x -= 70
    # wifi (yay dilimleri)
    for i, r in enumerate([30, 21, 12]):
        d.pieslice([x - r, cy + 14 - r * 1.6, x + r, cy + 14 + r * 0.4], 225, 315, fill=fg if i == 2 else None, outline=fg, width=4)
    d.pieslice([x - 6, cy + 4, x + 6, cy + 16], 225, 315, fill=fg)
    return img

def phone(img, scale=1.0):
    img = statusbar(img)
    w, h = img.size
    bez = int(w * 0.035)
    r_out = int(w * 0.13); r_in = r_out - bez
    W, H = w + bez * 2, h + bez * 2
    pad = int(w * 0.08)
    canvas = Image.new("RGBA", (W + pad * 2, H + pad * 2), (0, 0, 0, 0))
    # gölge
    sh = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(sh)
    d.rounded_rectangle([pad, pad + int(pad * 0.35), pad + W, pad + H + int(pad * 0.35)], r_out, fill=(30, 10, 60, 110))
    sh = sh.filter(ImageFilter.GaussianBlur(pad * 0.45))
    canvas.alpha_composite(sh)
    d = ImageDraw.Draw(canvas)
    d.rounded_rectangle([pad, pad, pad + W, pad + H], r_out, fill=(18, 18, 22, 255))
    d.rounded_rectangle([pad + 3, pad + 3, pad + W - 3, pad + H - 3], r_out - 3, outline=(70, 70, 80, 255), width=3)
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, w, h], r_in, fill=255)
    canvas.paste(img.convert("RGBA"), (pad + bez, pad + bez), mask)
    # ön kamera
    cx = pad + W // 2; cy = pad + bez + int(w * 0.035)
    rr = int(w * 0.018)
    d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=(10, 10, 12, 255))
    if scale != 1.0:
        canvas = canvas.resize((int(canvas.width * scale), int(canvas.height * scale)), Image.LANCZOS)
    return canvas

names = sorted(f for f in os.listdir(raw) if f.endswith(".png"))
for n in names:
    im = Image.open(os.path.join(raw, n))
    fr = phone(im, 0.5)
    fr.save(os.path.join(out, n), optimize=True)
    print(n, fr.size)

# vitrin: 4 telefon yan yana, gradyan arka plan
pick = [n for n in ["03-randevu.png", "01-bugun.png", "08-kasa.png", "12-koyu-bugun.png"] if n in names]
if pick:
    frames = [phone(Image.open(os.path.join(raw, n)), 0.42) for n in pick]
    fw, fh = frames[0].size
    gap = -int(fw * 0.08)
    W = fw * len(frames) + gap * (len(frames) - 1) + 80
    H = int(fh * 1.08) + 40
    bg = Image.new("RGBA", (W, H))
    top, bot = (124, 58, 237), (219, 39, 119)
    d = ImageDraw.Draw(bg)
    for y in range(H):
        t = y / H
        d.line([(0, y), (W, y)], fill=tuple(int(top[i] * (1 - t) + bot[i] * t) for i in range(3)) + (255,))
    x = 40
    for i, f in enumerate(frames):
        y = 20 + (0 if i in (1, 2) else int(fh * 0.06))
        bg.alpha_composite(f, (x, y))
        x += fw + gap
    bg.convert("RGB").save(os.path.join(out, "hero.png"), optimize=True)
    print("hero", bg.size)
