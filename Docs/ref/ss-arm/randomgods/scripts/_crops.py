import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods"
FR = os.path.join(base, "frames")


def masks(a):
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    mx = a.max(axis=2); mn = a.min(axis=2)
    sat = (mx - mn) / np.maximum(mx, 1)
    core = (R > 60) & (R < 175) & (sat < 0.34) & (G > 38) & (G >= B * 0.80)
    core[:330, :] = False; core[985:, :] = False; core[:, :700] = False; core[:, 1120:] = False
    return core


def font(sz):
    for p in (r"C:\Windows\Fonts\arialbd.ttf",):
        if os.path.exists(p):
            return ImageFont.truetype(p, sz)
    return ImageFont.load_default(size=sz)


T = "t1159.00.png"
CROPS = [
    ("base",     (880, 860, 1060, 1010), "底座与第1节根部（贴底边）"),
    ("joint1",   (900, 880, 1010, 970),  "底座枢接（第1关节）"),
    ("joint2",   (855, 625, 975, 715),   "第2关节（肩部枢轴）"),
    ("elbow",    (750, 675, 870, 800),   "肘部弯折"),
    ("wrist",    (805, 552, 925, 645),   "上端枢轴球 / 腕"),
    ("junction", (895, 540, 1025, 645),  "臂与袍子接合处"),
]
f = font(12)
out = []
for tag, box, desc in CROPS:
    im = Image.open(os.path.join(FR, T)).convert("RGB")
    c = im.crop(box)
    fn = os.path.join(base, "crop_%s_t1159.00.png" % tag)
    c.save(fn)
    w, h = c.size
    z = c.resize((w * 6, h * 6), Image.NEAREST)
    d = ImageDraw.Draw(z)
    for gx in range(0, w, 10):
        d.line([(gx * 6, 0), (gx * 6, h * 6)], fill=(0, 180, 230), width=1)
        d.text((gx * 6 + 2, 2), str(box[0] + gx), fill=(0, 230, 255), font=f)
    for gy in range(0, h, 10):
        d.line([(0, gy * 6), (w * 6, gy * 6)], fill=(0, 180, 230), width=1)
        d.text((2, gy * 6 + 1), str(box[1] + gy), fill=(0, 230, 255), font=f)
    z.save(os.path.join(base, "qc", "zoom_%s.png" % tag))
    out.append((tag, box, w, h, desc))
    print("crop_%s_t1159.00.png  box=%s  %dx%d  (qc/zoom_%s.png @6x)" % (tag, box, w, h, tag))

# arm pose stability across the loop
ref = masks(np.asarray(Image.open(os.path.join(FR, T)).convert("RGB")).astype(np.int16))
print("\n== 臂姿态相似度（与 t1159.00 的 IoU）==")
for fn in ("t0000.00.png", "t0228.00.png", "t0456.00.png", "t0684.00.png", "t0912.00.png",
           "t1368.00.png", "t1824.00.png", "t2261.00.png"):
    m = masks(np.asarray(Image.open(os.path.join(FR, fn)).convert("RGB")).astype(np.int16))
    inter = (ref & m).sum(); uni = (ref | m).sum()
    print("  %s  IoU=%.3f  arm_px=%d (ref %d)" % (fn, inter / uni, m.sum(), ref.sum()))
