"""Crop a region of a frame, upscale nearest-neighbour and draw a 1-px grid.

Every source pixel becomes a scale x scale block; the grid lets a human (or the
reviewer) count pixels exactly instead of trusting a threshold.

usage: python grid_crop.py t111 1432 880 90 90 12 calib/head_grid_t111.png
"""
import os, sys
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

def main():
    name, x0, y0, w, h, sc, out = sys.argv[1:8]
    x0, y0, w, h, sc = int(x0), int(y0), int(w), int(h), int(sc)
    im = Image.open(os.path.join(ROOT, "frames", name + ".png")).convert("RGB")
    crop = im.crop((x0, y0, x0 + w, y0 + h)).resize((w * sc, h * sc), Image.NEAREST)
    d = ImageDraw.Draw(crop)
    for i in range(w + 1):
        c = (0, 255, 255) if i % 10 == 0 else ((90, 90, 90) if i % 5 == 0 else (50, 50, 50))
        d.line([(i * sc, 0), (i * sc, h * sc)], fill=c, width=1)
    for j in range(h + 1):
        c = (0, 255, 255) if j % 10 == 0 else ((90, 90, 90) if j % 5 == 0 else (50, 50, 50))
        d.line([(0, j * sc), (w * sc, j * sc)], fill=c, width=1)
    # absolute coordinate ticks every 10 px
    for i in range(0, w + 1, 10):
        d.text((i * sc + 2, 2), str(x0 + i), fill=(255, 255, 0))
    for j in range(0, h + 1, 10):
        d.text((2, j * sc + 2), str(y0 + j), fill=(255, 255, 0))
    outp = os.path.join(ROOT, out)
    os.makedirs(os.path.dirname(outp), exist_ok=True)
    crop.save(outp)
    print("wrote", outp, crop.size)

main()
