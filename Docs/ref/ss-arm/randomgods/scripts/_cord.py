import os
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods"
FR = os.path.join(base, "frames")
SRC = os.path.join(FR, "t1159.00.png")
a = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)
V = a.max(axis=2)
mn = a.min(axis=2)
sat = (V - mn) / np.maximum(V, 1)
bg = ndi.uniform_filter(V, 15)
hp = V - bg
m = (hp > 7) & (V > 55) & (sat < 0.42)
m[:600, :] = False; m[975:, :] = False; m[:, :820] = False; m[:, 1140:] = False
m = ndi.binary_opening(m, np.ones((2, 2)))
lab, n = ndi.label(m)
sizes = ndi.sum(m, lab, range(1, n + 1))
comps = []
for k in range(n):
    if not (20 <= sizes[k] <= 900):
        continue
    ys, xs = np.nonzero(lab == k + 1)
    w = xs.max() - xs.min() + 1
    h = ys.max() - ys.min() + 1
    comps.append((min(w, h), max(w, h), int(xs.mean()), int(ys.mean()), int(sizes[k])))
print("dash-like components:", len(comps))
if comps:
    comps.sort()
    short = np.median([c[0] for c in comps])
    long_ = np.median([c[1] for c in comps])
    print("median minor axis = %.1f px, median major axis = %.1f px" % (short, long_))
    for c in comps[:10]:
        print("   w,h=(%d,%d) at (%d,%d) area=%d" % (c[0], c[1], c[2], c[3], c[4]))
ov = np.asarray(Image.open(SRC).convert("RGB")).copy()
sel = np.zeros_like(m)
for k in range(n):
    if 20 <= sizes[k] <= 900:
        sel |= (lab == k + 1)
ov[sel] = [0, 255, 255]
Image.fromarray(ov).crop((760, 560, 1200, 1010)).save(base + r"\qc\cord_mask_check.png")
print("saved qc/cord_mask_check.png")
