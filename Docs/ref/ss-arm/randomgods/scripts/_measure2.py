import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods"
SRC = base + r"\frames\t1159.00.png"


def font(sz):
    for p in (r"C:\Windows\Fonts\arialbd.ttf", r"C:\Windows\Fonts\consolab.ttf"):
        if os.path.exists(p):
            return ImageFont.truetype(p, sz)
    return ImageFont.load_default(size=sz)


def masks(a):
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    mx = a.max(axis=2); mn = a.min(axis=2)
    sat = (mx - mn) / np.maximum(mx, 1)
    core = (R > 60) & (R < 175) & (sat < 0.34) & (G > 38) & (G >= B * 0.80)
    core[:330, :] = False; core[985:, :] = False; core[:, :700] = False; core[:, 1120:] = False
    return core


a = np.asarray(Image.open(SRC).convert("RGB")).astype(np.int16)
core = masks(a)
lab, n = ndi.label(core)
sz = ndi.sum(core, lab, range(1, n + 1))
arm = ndi.binary_closing(np.isin(lab, [i + 1 for i in range(n) if sz[i] > 400]), np.ones((3, 3)))
D = ndi.distance_transform_edt(arm)

mxf = ndi.maximum_filter(D, size=9)
peaks = (D == mxf) & (D >= 4)
ys, xs = np.nonzero(peaks)
pts = sorted(zip(D[ys, xs], ys, xs), reverse=True)
shown = []
for d, y, x in pts:
    if all((x - sx) ** 2 + (y - sy) ** 2 > 20 ** 2 for _, sy, sx in shown):
        shown.append((d, y, x))

ov = np.asarray(Image.open(SRC).convert("RGB")).copy()
ov[arm] = (0, 255, 0)
img = Image.fromarray(ov)
d2 = ImageDraw.Draw(img)
f = font(13)
for d_, y, x in shown[:8]:
    d2.ellipse([x - d_, y - d_, x + d_, y + d_], outline=(0, 255, 255), width=1)
    d2.text((x + int(d_) + 2, y - 7), "%.1fpx" % (2 * d_), fill=(0, 255, 255), font=f)
img.crop((740, 470, 1120, 1010)).resize((760, 1080), Image.LANCZOS).save(base + r"\qc\measure_annotated.png")
print("annotated saved")

# --- base plate position across all 120 survey frames
print("\n== 底座/臂下段在 120 帧采样中的横向位置（y 900..975 带内的臂质心 x） ==")
rows = []
for fn in sorted(os.listdir(base + r"\frames")):
    a2 = np.asarray(Image.open(os.path.join(base, "frames", fn)).convert("RGB")).astype(np.int16)
    m = masks(a2)
    band = m[900:975, :]
    ys2, xs2 = np.nonzero(band)
    if len(xs2) < 200:
        rows.append((fn, -1, -1))
        continue
    rows.append((fn, float(np.median(xs2)), float(xs2.max() - xs2.min())))
xs_all = [r[1] for r in rows if r[1] > 0]
print("frames with base detected: %d/120" % len(xs_all))
print("median x range: %.0f .. %.0f  (spread %.0f px)" % (min(xs_all), max(xs_all), max(xs_all) - min(xs_all)))
for fn, mx_, w in rows[::12]:
    print("  %s  base x=%.0f  width=%d" % (fn, mx_, w))
