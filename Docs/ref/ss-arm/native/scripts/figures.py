"""Evidence figures: annotated calibration + arm measurement overlays.

fig1  calib/fig_head_t112.png  : head disc, 50 %-crossing chords, neck excluded
fig2  calib/fig_arm_t060.png   : arm root, perpendicular cross-sections every
                                 4 px of geodesic distance, elbow disc circle
"""
import json, os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load(name):
    return Image.open(os.path.join(ROOT, "frames", name + ".png")).convert("RGB")


def font(sz):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/consola.ttf", sz)
    except Exception:
        return ImageFont.load_default()


# ---------- fig 1 : head -----------------------------------------------------
im = load("t112")
d = ImageDraw.Draw(im)
cx, cy = 1002.3, 804.1        # disc centre from the chord crossings
W, H = 23.7, 20.4
d.ellipse([cx - W / 2, cy - H / 2, cx + W / 2, cy + H / 2], outline=(0, 255, 255), width=1)
d.line([988.33, cy, 1001.7, cy], fill=(255, 255, 0), width=1)          # left half chord
d.line([1001.7, cy, 1011.69 + 0, cy], fill=(255, 120, 0), width=1)     # disc part of the row
d.line([cx, 793.85, cx, 814.3], fill=(0, 255, 120), width=1)           # full height chord
for x in (988.33, 1011.69):
    d.line([x, cy - 4, x, cy + 4], fill=(255, 255, 0), width=1)
for y in (793.85, 814.3):
    d.line([cx - 4, y, cx + 4, y], fill=(0, 255, 120), width=1)
f = font(13)
d.text((cx - 60, cy - 34), "W=23.7  H=20.4 px", fill=(255, 255, 0), font=f)
d.text((cx - 60, cy - 22), "scale=2.13  scale_y=2.25", fill=(255, 255, 0), font=f)
out = im.crop((940, 760, 1080, 850)).resize((1120, 720), Image.LANCZOS)
out.save(os.path.join(ROOT, "calib", "fig_head_t112.png"))
print("wrote fig_head_t112.png", out.size)

# ---------- fig 2 : arm ------------------------------------------------------
im = load("t060")
d = ImageDraw.Draw(im)
rows = json.load(open(os.path.join(ROOT, "calib", "arm_width_t060.json"), encoding="utf-8"))["rows"]
P = np.array([[r["x"], r["y"]] for r in rows])
f = font(12)
for i, r in enumerate(rows):
    if r["d"] % 8 or r["d"] < 8:
        continue
    lo, hi = max(0, i - 2), min(len(P) - 1, i + 2)
    t = P[hi] - P[lo]
    n = np.array([-t[1], t[0]])
    n /= max(1e-6, np.hypot(*n))
    x0, y0 = r["x"], r["y"]
    d.line([x0 - n[0] * r["left"], y0 - n[1] * r["left"],
            x0 + n[0] * r["right"], y0 + n[1] * r["right"]], fill=(0, 255, 255), width=1)
    d.text((x0 + 2, y0 - 12), "d%d w%.0f" % (r["d"], r["width"]), fill=(0, 255, 0), font=f)
# elbow disc
d.ellipse([1500 - 15, 508 - 12, 1500 + 15, 508 + 12], outline=(255, 0, 255), width=1)
d.text((1503, 480), "elbow ~30x24", fill=(255, 0, 255), font=f)
# carriage plate + root
d.rectangle([1690, 165, 1800, 232], outline=(255, 200, 0))
d.text((1610, 168), "carriage plate", fill=(255, 200, 0), font=f)
out = im.crop((1280, 150, 1820, 760)).resize((1350, 1525), Image.LANCZOS)
out.save(os.path.join(ROOT, "calib", "fig_arm_t060.png"))
print("wrote fig_arm_t060.png", out.size)
