"""Annotated junction figures (grid + robe bbox + arm end face + contact point).

usage: python fig_junction.py   -> calib/junction_t060.png, calib/junction_t003.png
Numbers are hard-coded from junction_comps.py output (absolute image px).
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SC = 8


def font(sz):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/consola.ttf", sz)
    except Exception:
        return ImageFont.load_default()


def draw(name, x0, y0, w, h, out, robe, head, arm, tip, contact, notes):
    im = Image.open(os.path.join(ROOT, "frames", name + ".png")).convert("RGB")
    crop = im.crop((x0, y0, x0 + w, y0 + h)).resize((w * SC, h * SC), Image.NEAREST)
    d = ImageDraw.Draw(crop)
    for i in range(w + 1):
        c = (0, 220, 220) if i % 10 == 0 else ((80, 80, 80) if i % 5 == 0 else (45, 45, 45))
        d.line([(i * SC, 0), (i * SC, h * SC)], fill=c, width=1)
    for j in range(h + 1):
        c = (0, 220, 220) if j % 10 == 0 else ((80, 80, 80) if j % 5 == 0 else (45, 45, 45))
        d.line([(0, j * SC), (w * SC, j * SC)], fill=c, width=1)
    for i in range(0, w, 10):
        d.text((i * SC + 2, 2), str(x0 + i), fill=(255, 255, 0), font=font(11))
    for j in range(0, h, 10):
        d.text((2, j * SC + 2), str(y0 + j), fill=(255, 255, 0), font=font(11))

    def box(b, col, label, lw=2):
        bx0, by0, bx1, by1 = b
        d.rectangle([(bx0 - x0) * SC, (by0 - y0) * SC, (bx1 - x0 + 1) * SC, (by1 - y0 + 1) * SC],
                    outline=col, width=lw)
        if label:
            d.text(((bx0 - x0) * SC + 3, (by1 - y0 + 1) * SC + 1), label, fill=col, font=font(12))

    box(robe, (255, 220, 0), "robe", 2)
    box(head, (255, 60, 220), "head", 2)
    box(arm, (0, 255, 255), None, 1)
    # arm end face
    tx, ty = tip
    d.line([(tx - x0 - 9) * SC, (ty - y0) * SC, (tx - x0 + 9) * SC, (ty - y0) * SC],
           fill=(0, 255, 255), width=2)
    # contact point
    cx, cy = contact
    r = 5
    d.line([(cx - x0 - r) * SC, (cy - y0) * SC, (cx - x0 + r) * SC, (cy - y0) * SC],
           fill=(255, 0, 0), width=2)
    d.line([(cx - x0) * SC, (cy - y0 - r) * SC, (cx - x0) * SC, (cy - y0 + r) * SC],
           fill=(255, 0, 0), width=2)
    ytxt = h * SC - 18 * len(notes) - 6
    for k, t in enumerate(notes):
        d.text((6, ytxt + 18 * k), t, fill=(255, 255, 255), font=font(13))
    crop.save(os.path.join(ROOT, "calib", out))
    print("wrote", out, crop.size)


draw("t060", 1440, 780, 140, 120, "junction_t060.png",
     robe=(1492, 828, 1550, 891), head=(1476, 816, 1505, 843),
     arm=(1506, 760, 1524, 831), tip=(1515, 829), contact=(1515, 830),
     notes=["t060  robe 59x64 c=(1522.3,858.4) | head c=(1488.0,828.4)",
            "arm last span x1506-1524 (19 px wide), visible tip y=831",
            "contact (1515,830): sits on robe top edge, overlap ~3 px"])

draw("t003", 950, 545, 170, 110, "junction_t003.png",
     robe=(993, 584, 1058, 635), head=(966, 594, 995, 614),
     arm=(993, 540, 1119, 603), tip=(997, 580), contact=(995, 580),
     notes=["t003  robe 66x52 c=(1027.2,608.6) | head c=(979.0,604.6)",
            "grey blob fill=0.18 -> thin-line structure: arm-end ID UNRELIABLE",
            "auxiliary only: a thick grey something presses down from above"])
