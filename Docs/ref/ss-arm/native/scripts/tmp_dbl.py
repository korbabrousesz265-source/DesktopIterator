import numpy as np, os
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
a = np.asarray(Image.open(os.path.join(ROOT,"frames","t112.png")).convert("RGB")).astype(np.float32)
g = a.mean(axis=2)[300:1100, 900:1800]
for d in (1,2,3,4):
    dh = np.abs(g[:, d:] - g[:, :-d]).mean()
    dv = np.abs(g[d:, :] - g[:-d, :]).mean()
    print("shift %d : horizontal MAD %.2f  vertical MAD %.2f"%(d, dh, dv))
