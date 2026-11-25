import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods"
SRC = base + r"\frames\t1159.00.png"
a = np.asarray(Image.open(SRC).convert("RGB")).astype(np.int16)
R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
mx = a.max(axis=2); mn = a.min(axis=2)
sat = (mx - mn) / np.maximum(mx, 1)

# --- core mask (strict): rod body only
core = (R > 60) & (R < 175) & (sat < 0.34) & (G > 38) & (G >= B * 0.80)
# --- broad mask: includes the soft edge / shadow fringe
broad = (R > 45) & (R < 185) & (sat < 0.46) & (G > 26) & (G >= B * 0.70)

for m in (core, broad):
    m[:330, :] = False; m[985:, :] = False; m[:, :700] = False; m[:, 1120:] = False

lab, n = ndi.label(core)
sz = ndi.sum(core, lab, range(1, n + 1))
keep = [i + 1 for i in range(n) if sz[i] > 400]
arm = np.isin(lab, keep)
arm = ndi.binary_closing(arm, np.ones((3, 3)))
print("core arm px:", int(arm.sum()), "components kept:", len(keep))

D = ndi.distance_transform_edt(arm)
Db = ndi.distance_transform_edt(ndi.binary_closing(broad, np.ones((3, 3))))

print("\n== DT local maxima (r>=4) ==")
mxf = ndi.maximum_filter(D, size=9)
peaks = (D == mxf) & (D >= 4)
ys, xs = np.nonzero(peaks)
pts = sorted(zip(D[ys, xs], ys, xs), reverse=True)
shown = []
for d, y, x in pts:
    if all((x - sx) ** 2 + (y - sy) ** 2 > 18 ** 2 for _, sy, sx in shown):
        shown.append((d, y, x))
for d, y, x in shown[:12]:
    print("  (x=%4d,y=%4d) r=%.2f  core直径=%.1f px   broad直径=%.1f px" % (
        x, y, d, 2 * d, 2 * Db[int(y), int(x)]))

# --- perpendicular width of the first segment near the base
# segment runs from base head (958, 912) to elbow (795, 716)
p0 = np.array([958.0, 912.0]); p1 = np.array([795.0, 716.0])
u = (p1 - p0) / np.linalg.norm(p1 - p0)
nrm = np.array([-u[1], u[0]])
print("\n== 第1节（底座→肘）垂直截面宽度 ==")
for s in (10, 20, 30, 40, 60, 80, 100):
    c = p0 + u * s
    ws_c = ws_b = 0
    for o in np.arange(-40, 40, 0.5):
        x = int(round(c[0] + nrm[0] * o)); y = int(round(c[1] + nrm[1] * o))
        if 0 <= y < arm.shape[0] and 0 <= x < arm.shape[1]:
            ws_c += arm[y, x]; ws_b += ndi.binary_closing(broad, np.ones((3, 3)))[y, x]
    print("  距底座 %3d px 处  (x=%4.0f,y=%4.0f)  core=%.1f px  broad=%.1f px" % (
        s, c[0], c[1], ws_c * 0.5, ws_b * 0.5))

# --- annotate
ov = Image.open(SRC).convert("RGB")
d = ImageDraw.Draw(ov)
ovn = np.asarray(ov).copy()
ovn[arm] = (0, 255, 0)
ov2 = Image.fromarray(ovn)
d2 = ImageDraw.Draw(ov2)


def font(sz):
    for p in (r"C:\Windows\Fonts\arialbd.ttf",):
        if np.os.path.exists(p):
            return ImageFont.truetype(p, sz)
    return ImageFont.load_default(size=sz)


import os
f = font(14)
for d_, y, x in shown[:6]:
    d2.ellipse([x - d_, y - d_, x + d_, y + d_], outline=(0, 255, 255), width=1)
    d2.text((x + d_ + 2, y - 7), "d=%.1f" % (2 * d_), fill=(0, 255, 255), font=f)
ov2.crop((740, 470, 1120, 1010)).resize((760, 1080), Image.LANCZOS).save(base + r"\qc\measure_annotated.png")
print("\nsaved qc/measure_annotated.png")
