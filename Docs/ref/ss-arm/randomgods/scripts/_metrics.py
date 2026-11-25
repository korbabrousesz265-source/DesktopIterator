import os, json
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


rows = []
for fn in files:
    a = np.asarray(Image.open(os.path.join(FR, fn)).convert("RGB")).astype(np.int16)
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    mx = a.max(axis=2); mn = a.min(axis=2)
    sat = (mx - mn) / np.maximum(mx, 1)
    o = (R > 150) & (G > 40) & (G < 175) & (B < 95) & (R - B > 90) & (R - G > 40)
    o[:100, :] = False; o[1010:, :] = False; o[:, :400] = False; o[:, 1520:] = False
    ys, xs = np.nonzero(o)
    if len(xs):
        pcx, pcy = xs.mean(), ys.mean()
        ow, oh = xs.max() - xs.min(), ys.max() - ys.min()
    else:
        pcx = pcy = ow = oh = -1
    m = arm_mask(a)
    ay, ax = np.nonzero(m)
    base_x = float(np.median(ax[(ay > 900) & (ay < 975)])) if ((ay > 900) & (ay < 975)).sum() > 100 else -1
    rows.append(dict(t=fn, puppet=[round(float(pcx)), round(float(pcy))], robe=[int(ow), int(oh)],
                     arm_px=int(m.sum()), base_x=round(base_x),
                     arm_bbox=[int(ax.min()), int(ay.min()), int(ax.max()), int(ay.max())] if len(ax) else None))

with open(os.path.join(base, "qc", "frame_metrics.csv"), "w", encoding="utf-8") as fh:
    fh.write("time,file,puppet_x,puppet_y,robe_w,robe_h,arm_px,base_x\n")
    for r in rows:
        fh.write("%s,%s,%d,%d,%d,%d,%d,%d\n" % (r["t"][1:8], r["t"], r["puppet"][0], r["puppet"][1],
                                                r["robe"][0], r["robe"][1], r["arm_px"], r["base_x"]))

px = [r["puppet"][0] for r in rows]
py = [r["puppet"][1] for r in rows]
bx = [r["base_x"] for r in rows]
print("puppet x: %d..%d (range %d)" % (min(px), max(px), max(px) - min(px)))
print("puppet y: %d..%d (range %d)" % (min(py), max(py), max(py) - min(py)))
print("base x:   %d..%d (range %d)" % (min(bx), max(bx), max(bx) - min(bx)))
print("robe:     %dx%d .. %dx%d" % (min(r["robe"][0] for r in rows), min(r["robe"][1] for r in rows),
                                    max(r["robe"][0] for r in rows), max(r["robe"][1] for r in rows)))
print("arm px:   %d..%d" % (min(r["arm_px"] for r in rows), max(r["arm_px"] for r in rows)))

# ---- cord (black-white dashed cable) dash geometry on the measurement frame
a = np.asarray(Image.open(os.path.join(FR, "t1159.00.png")).convert("RGB")).astype(np.int16)
R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
V = a.max(axis=2); mn2 = a.min(axis=2)
sat2 = (V - mn2) / np.maximum(V, 1)
dash = (V > 62) & (V < 165) & (sat2 < 0.30)
dash[:380, :] = False; dash[985:, :] = False; dash[:, :720] = False; dash[:, 1140:] = False
lab, n = ndi.label(dash)
sizes = ndi.sum(dash, lab, range(1, n + 1))
dashes = []
for k in range(n):
    if not (25 <= sizes[k] <= 700):
        continue
    ysk, xsk = np.nonzero(lab == k + 1)
    w = xsk.max() - xsk.min() + 1
    h = ysk.max() - ysk.min() + 1
    if min(w, h) > 22:
        continue
    dashes.append((min(w, h), max(w, h), int(xsk.mean()), int(ysk.mean())))
if dashes:
    mn_ax = np.median([d[0] for d in dashes])
    mx_ax = np.median([d[1] for d in dashes])
    print("\n缆线虚线段: %d 段  中位短轴=%.1f px  中位长轴=%.1f px" % (len(dashes), mn_ax, mx_ax))
    print("  示例:", dashes[:6])
