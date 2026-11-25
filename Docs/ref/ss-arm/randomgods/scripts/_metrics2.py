import os
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods"
FR = os.path.join(base, "frames")
files = sorted(f for f in os.listdir(FR) if f.endswith(".png"))


def arm_mask(a):
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    mx = a.max(axis=2); mn = a.min(axis=2)
    sat = (mx - mn) / np.maximum(mx, 1)
    m = (R > 60) & (R < 175) & (sat < 0.34) & (G > 38) & (G >= B * 0.80)
    m[:330, :] = False; m[985:, :] = False; m[:, :700] = False; m[:, 1120:] = False
    return m


def puppet(a):
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    o = (R > 165) & (G > 45) & (G < 180) & (B < 100) & (R - B > 100) & (R - G > 40)
    o[:380, :] = False; o[780:, :] = False; o[:, :850] = False; o[:, 1100:] = False
    o = ndi.binary_opening(o, np.ones((3, 3)))
    lab, n = ndi.label(o)
    if n == 0:
        return -1, -1, 0, 0
    sizes = ndi.sum(o, lab, range(1, n + 1))
    k = int(np.argmax(sizes)) + 1
    ys, xs = np.nonzero(lab == k)
    return xs.mean(), ys.mean(), xs.max() - xs.min() + 1, ys.max() - ys.min() + 1


rows = []
for fn in files:
    a = np.asarray(Image.open(os.path.join(FR, fn)).convert("RGB")).astype(np.int16)
    pcx, pcy, ow, oh = puppet(a)
    m = arm_mask(a)
    ay, ax = np.nonzero(m)
    band = (ay > 900) & (ay < 975)
    base_x = float(np.median(ax[band])) if band.sum() > 100 else -1
    rows.append((fn, pcx, pcy, ow, oh, int(m.sum()), base_x))

with open(os.path.join(base, "qc", "frame_metrics.csv"), "w", encoding="utf-8") as fh:
    fh.write("time,file,puppet_x,puppet_y,robe_w,robe_h,arm_px,base_x\n")
    for r in rows:
        fh.write("%s,%s,%.0f,%.0f,%d,%d,%d,%.0f\n" % (r[0][1:8], r[0], r[1], r[2], r[3], r[4], r[5], r[6]))

px = [r[1] for r in rows]; py = [r[2] for r in rows]
print("puppet x %.0f..%.0f (range %.0f)" % (min(px), max(px), max(px) - min(px)))
print("puppet y %.0f..%.0f (range %.0f)" % (min(py), max(py), max(py) - min(py)))
print("robe w %d..%d  h %d..%d" % (min(r[3] for r in rows), max(r[3] for r in rows),
                                   min(r[4] for r in rows), max(r[4] for r in rows)))
print("base x %.0f..%.0f" % (min(r[6] for r in rows), max(r[6] for r in rows)))
print("arm px %d..%d" % (min(r[5] for r in rows), max(r[5] for r in rows)))
