import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods"


def font(sz):
    for p in (r"C:\Windows\Fonts\arialbd.ttf", r"C:\Windows\Fonts\consolab.ttf"):
        if os.path.exists(p):
            return ImageFont.truetype(p, sz)
    return ImageFont.load_default(size=sz)


def grid_crop(src, box, scale, out, grid=10, minor=1):
    im = Image.open(src).convert("RGB").crop(box)
    w, h = im.size
    im = im.resize((w * scale, h * scale), Image.NEAREST)
    d = ImageDraw.Draw(im)
    f = font(12)
    for gx in range(0, w + 1, minor):
        sx = gx * scale
        if gx % grid == 0:
            d.line([(sx, 0), (sx, h * scale)], fill=(0, 200, 255), width=1)
            d.text((sx + 2, 2), str(box[0] + gx), fill=(0, 220, 255), font=f)
        elif scale >= 6:
            d.line([(sx, 0), (sx, h * scale)], fill=(40, 70, 80), width=1)
    for gy in range(0, h + 1, minor):
        sy = gy * scale
        if gy % grid == 0:
            d.line([(0, sy), (w * scale, sy)], fill=(0, 200, 255), width=1)
            d.text((2, sy + 1), str(box[1] + gy), fill=(0, 220, 255), font=f)
        elif scale >= 6:
            d.line([(0, sy), (w * scale, sy)], fill=(40, 70, 80), width=1)
    im.save(out)
    print(out, im.size, "src box", box, "scale", scale)


if __name__ == "__main__":
    src = base + r"\frames\t1159.00.png"
    grid_crop(src, (900, 870, 1030, 990), 8, base + r"\qc\_z_base.png", 10, 1)
    grid_crop(src, (770, 640, 900, 760), 8, base + r"\qc\_z_elbow.png", 10, 1)
    grid_crop(src, (830, 540, 1010, 640), 8, base + r"\qc\_z_top.png", 10, 1)
