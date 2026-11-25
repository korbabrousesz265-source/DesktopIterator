import os
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods"
FR = os.path.join(base, "frames")
a = np.asarray(Image.open(os.path.join(FR, "t1159.00.png")).convert("RGB")).astype(np.int16)
for (x, y) in [(1040, 820), (1030, 800), (1050, 840), (1000, 900), (980, 870), (960, 830),
               (1010, 760), (900, 800), (880, 760), (990, 940), (930, 700)]:
    print((x, y), a[y - 1:y + 2, x - 1:x + 2].reshape(-1, 3).mean(axis=0).round(1))
