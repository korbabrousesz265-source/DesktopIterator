import numpy as np, os
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
a = np.asarray(Image.open(os.path.join(ROOT,"frames","t112.png")).convert("RGB")).astype(np.float32)
m = a[:,:,2] - 0.8*a[:,:,1]
print("horizontal profile at y=809, x=975..1025 (m values, 0 decimals):")
for y in (800, 806, 809, 811):
    print(" y=%d: "%y + " ".join("%3d"%m[y,x] for x in range(975,1026)))
print()
print("vertical profile at x=1000, y=788..824:")
for x in (995, 1000, 1005):
    print(" x=%d: "%x + " ".join("%3d"%m[y,x] for y in range(788,825)))
print()
print("mean RGB inside core (x 995..1005, y 802..810):", a[802:811,995:1006].reshape(-1,3).mean(axis=0).round(1))
