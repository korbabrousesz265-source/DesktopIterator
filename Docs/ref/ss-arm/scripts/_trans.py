import os, re
import numpy as np
from PIL import Image

m = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\frames_24fps"
files = sorted(os.listdir(m))
for f in files:
    a = np.asarray(Image.open(os.path.join(m, f)).convert("RGB")).astype(np.int16)
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    reg = np.zeros_like(R, dtype=bool)
    reg[42:614, 32:688] = True
    o = (R > 165) & (G > 60) & (G < 170) & (B < 95) & (R - B > 100) & reg
    arm = (R > 115) & (R - B >= 12) & (R - B <= 62) & (np.abs(G - B) <= 22) & (G > 95) & reg
    ys, xs = np.nonzero(o)
    ay, ax = np.nonzero(arm)
    top = int(np.median(ax[ay < 78])) if (ay < 78).sum() > 30 else -1
    print("%-18s puppet=(%4d,%4d) n=%4d anchor_top_x=%4d" % (
        f, xs.mean() if len(xs) else -1, ys.mean() if len(ys) else -1, len(xs), top))
