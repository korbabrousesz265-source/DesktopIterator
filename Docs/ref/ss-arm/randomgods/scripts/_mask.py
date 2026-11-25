import numpy as np
from PIL import Image
from scipy import ndimage as ndi

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods"
p = base + r"\frames\t1159.00.png"
a = np.asarray(Image.open(p).convert("RGB")).astype(np.int16)
R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
mx = a.max(axis=2)
mn = a.min(axis=2)
sat = (mx - mn) / np.maximum(mx, 1)
# arm = desaturated mid-grey (its G ~ B, unlike the purple/red background)
m = (R > 48) & (R < 165) & (sat < 0.42) & (G > 28) & (G >= B * 0.72)
# restrict to room area
m[:120, :] = False
m[1010:, :] = False
m[:, :480] = False
m[:, 1460:] = False
lab, n = ndi.label(m)
sizes = ndi.sum(m, lab, range(1, n + 1))
order = np.argsort(sizes)[::-1]
print("components:", n, "top sizes:", sizes[order[:8]].astype(int))
for k in order[:6]:
    ys, xs = np.nonzero(lab == k + 1)
    print("  comp#%d size=%d bbox=(%d,%d)-(%d,%d)" % (
        k + 1, int(sizes[k]), xs.min(), ys.min(), xs.max(), ys.max()))
big = np.isin(lab, [int(k) + 1 for k in order[:3] if sizes[k] > 500])
ov = a.astype(np.uint8).copy()
ov[big] = [0, 255, 0]
Image.fromarray(ov).crop((740, 470, 1120, 1010)).resize((760, 1080), Image.NEAREST).save(base + r"\qc\_maskcheck.png")
print("mask px:", int(big.sum()))
