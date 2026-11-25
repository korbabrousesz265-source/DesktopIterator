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


def robe(a):
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    o = (R > 195) & (R - G > 130) & (R - B > 170)
    o[:380, :] = False; o[780:, :] = False; o[:, :820] = False; o[:, 1120:] = False
    o = ndi.binary_opening(o, np.ones((2, 2)))
    lab, n = ndi.label(o)
    if n == 0:
        return None
    sizes = ndi.sum(o, lab, range(1, n + 1))
    best = None
    for k in range(n):
        ys, xs = np.nonzero(lab == k + 1)
        w = xs.max() - xs.min() + 1
        h = ys.max() - ys.min() + 1
        if not (18 <= w <= 70 and 26 <= h <= 80 and sizes[k] > 450):
            continue
        d = ((xs.mean() - 958) ** 2 + (ys.mean() - 566) ** 2) ** 0.5
        if best is None or d < best[0]:
            best = (d, xs, ys, w, h)
    if best is None:
        return None
    d, xs, ys, w, h = best
    return dict(cx=xs.mean(), cy=ys.mean(), w=w, h=h,
                x0=xs.min(), x1=xs.max(), y0=ys.min(), y1=ys.max())


def orange_loose(a):
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    o = (R > 150) & (G > 35) & (B < 110) & (R - B > 90) & (R - G > 50)
    o[:380, :] = False; o[900:, :] = False; o[:, :820] = False; o[:, 1200:] = False
    return int(o.sum())


rows = []
for fn in files:
    a = np.asarray(Image.open(os.path.join(FR, fn)).convert("RGB")).astype(np.int16)
    r = robe(a)
    m = arm_mask(a)
    ay, ax = np.nonzero(m)
    band = (ay > 900) & (ay < 975)
    base_x = float(np.median(ax[band])) if band.sum() > 100 else -1
    rows.append(dict(t=fn[1:8], f=fn, note='',
                     cx=round(r["cx"]) if r else -1, cy=round(r["cy"]) if r else -1,
                     w=r["w"] if r else 0, h=r["h"] if r else 0,
                     arm=int(m.sum()), bx=round(base_x), oloose=orange_loose(a)))

med_o = float(np.median([r["oloose"] for r in rows]))
for r in rows:
    r["note"] = ("橙色发光/光团偏大" if r["oloose"] > med_o * 1.35
                  else ("橙色偏弱" if r["oloose"] < med_o * 0.7 else "静止构型"))
print("orange loose median", med_o)
with open(os.path.join(base, "qc", "frame_metrics.csv"), "w", encoding="utf-8") as fh:
    fh.write("time,file,puppet_cx,puppet_cy,robe_w,robe_h,arm_px,base_x,orange_loose,note\n")
    for r in rows:
        note = r["note"]
        fh.write("%s,%s,%d,%d,%d,%d,%d,%d,%d,%s\n" % (
            r["t"], r["f"], r["cx"], r["cy"], r["w"], r["h"], r["arm"], r["bx"], r["oloose"], note))

lines = []
lines.append("| 时码 | 文件 | 人偶质心 | 衣袍 bbox | 臂像素 | 臂底座 x | 备注 |")
lines.append("| --- | --- | --- | --- | --- | --- | --- |")
for r in rows:
    lines.append("| %s s | `frames/%s` | (%d, %d) | %d×%d | %d | %d | %s |" % (
        r["t"], r["f"], r["cx"], r["cy"], r["w"], r["h"], r["arm"], r["bx"], r["note"]))
open(os.path.join(base, "qc", "frames_table.md"), "w", encoding="utf-8").write("\n".join(lines))

print("rows:", len(rows))
print("puppet cx range", min(r["cx"] for r in rows), max(r["cx"] for r in rows))
print("puppet cy range", min(r["cy"] for r in rows), max(r["cy"] for r in rows))
print("arm px range", min(r["arm"] for r in rows), max(r["arm"] for r in rows))
print("\nsample table rows:")
print("\n".join(lines[:4]))
