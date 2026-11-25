import os
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods"
FR = os.path.join(base, "frames")


def puppet_mask(a):
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    o = (R > 195) & (R - G > 130) & (R - B > 170)
    o[:380, :] = False; o[780:, :] = False; o[:, :820] = False; o[:, 1120:] = False
    return ndi.binary_opening(o, np.ones((2, 2)))


for fn in ("t1159.00.png", "t0000.00.png", "t0456.00.png", "t1824.00.png"):
    a = np.asarray(Image.open(os.path.join(FR, fn)).convert("RGB")).astype(np.int16)
    o = puppet_mask(a)
    lab, n = ndi.label(o)
    if n:
        sizes = ndi.sum(o, lab, range(1, n + 1))
        k = int(np.argmax(sizes)) + 1
        ys, xs = np.nonzero(lab == k)
        print(fn, "n=%d total=%d biggest=%d bbox x %d..%d y %d..%d  (%.0f,%.0f)" % (
            n, o.sum(), sizes[k - 1], xs.min(), xs.max(), ys.min(), ys.max(), xs.mean(), ys.mean()))
    else:
        print(fn, "none")
