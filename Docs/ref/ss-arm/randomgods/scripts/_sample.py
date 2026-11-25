import numpy as np
from PIL import Image
p = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods\frames\t1159.00.png"
a = np.asarray(Image.open(p).convert("RGB"))
pts = [(815, 720), (790, 700), (880, 760), (960, 940), (940, 950), (900, 620), (930, 600),
       (960, 580), (955, 500), (1000, 700), (860, 900), (700, 500)]
for (x, y) in pts:
    print((x, y), a[y - 2:y + 3, x - 2:x + 3].reshape(-1, 3).mean(axis=0).round(1))
print("global mean", a.reshape(-1, 3).mean(axis=0).round(1))
