import numpy as np
from PIL import Image
import os

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm"
fr = os.path.join(base, "frames")


def arm_mask(a):
    R, G, B = a[:, :, 0].astype(int), a[:, :, 1].astype(int), a[:, :, 2].astype(int)
    return (R > 115) & (R - B >= 12) & (R - B <= 62) & (np.abs(G - B) <= 22) & (G > 95)


def hwidth(mask, y, x0, x1):
    row = mask[y, x0:x1]
    best = 0; cur = 0
    for v in row:
        cur = cur + 1 if v else 0
        best = max(best, cur)
    return best


def vwidth(mask, x, y0, y1):
    col = mask[y0:y1, x]
    best = 0; cur = 0
    for v in col:
        cur = cur + 1 if v else 0
        best = max(best, cur)
    return best


rows = []
for f, y, x0, x1 in [("t06.00.png", 120, 350, 430),   # vertical upper segment
                     ("t13.00.png", 150, 390, 470),
                     ("t15.00.png", 200, 330, 410),
                     ("t15.00.png", 320, 400, 470)]:
    a = np.asarray(Image.open(os.path.join(fr, f)).convert("RGB"))
    m = arm_mask(a)
    rows.append((f, "row y=%d" % y, hwidth(m, y, x0, x1)))

for f, x, y0, y1 in [("t00.00.png", 150, 130, 200),  # horizontal upper limb
                     ("t00.00.png", 200, 130, 220),
                     ("t16.50.png", 200, 90, 160),
                     ("t06.00.png", 320, 300, 360)]:
    a = np.asarray(Image.open(os.path.join(fr, f)).convert("RGB"))
    m = arm_mask(a)
    rows.append((f, "col x=%d" % x, vwidth(m, x, y0, y1)))

for r in rows:
    print(r)
