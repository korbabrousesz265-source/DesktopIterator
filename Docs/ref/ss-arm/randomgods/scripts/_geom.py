import numpy as np
from PIL import Image
from scipy import ndimage as ndi

p = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods\frames\t1159.00.png"
a = np.asarray(Image.open(p).convert("RGB")).astype(np.int16)
R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
# room frame: saturated red-orange border
border = (R > 120) & (R - G > 60) & (R - B > 70) & (G < 110)
col = border.sum(axis=0); row = border.sum(axis=1)
xs = np.nonzero(col > 60)[0]; ys = np.nonzero(row > 60)[0]
print("room border extent x %d..%d (w=%d)  y %d..%d (h=%d)" % (
    xs.min(), xs.max(), xs.max() - xs.min(), ys.min(), ys.max(), ys.max() - ys.min()))
# puppet head: pink/magenta
head = (R > 170) & (B > 90) & (R - G > 70) & (B - G > 10)
head[:460, :] = False; head[600:, :] = False; head[:, :880] = False; head[:, 1040:] = False
lab, n = ndi.label(ndi.binary_opening(head, np.ones((2, 2))))
if n:
    sizes = ndi.sum(head, lab, range(1, n + 1))
    k = int(np.argmax(sizes)) + 1
    yy, xx = np.nonzero(lab == k)
    print("head bbox x %d..%d y %d..%d  (%dx%d)" % (
        xx.min(), xx.max(), yy.min(), yy.max(), xx.max() - xx.min() + 1, yy.max() - yy.min() + 1))
# halo ring extent (red dashes around puppet)
halo = (R > 110) & (R - G > 55) & (R - B > 55) & (B < 90)
halo[:400, :] = False; halo[700:, :] = False; halo[:, :820] = False; halo[:, 1120:] = False
yy, xx = np.nonzero(halo)
print("halo-ish red structures bbox x %d..%d y %d..%d" % (xx.min(), xx.max(), yy.min(), yy.max()))
