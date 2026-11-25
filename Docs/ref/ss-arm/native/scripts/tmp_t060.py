import numpy as np, os
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
a = np.asarray(Image.open(os.path.join(ROOT,"frames","t060.png")).convert("RGB")).astype(np.float32)
m = a[:,:,2]-0.8*a[:,:,1]
for y in (816, 820, 824, 828):
    print("row y=%d x1458..1512:"%y)
    print("   ", " ".join("%3d"%m[y,x] for x in range(1458,1513)))
    print("   ", " ".join("%3d"%x for x in range(1458,1513)))
for x in (1480, 1486, 1492):
    print("col x=%d y800..842:"%x)
    print("   ", " ".join("%3d"%m[y,x] for y in range(800,843)))
    print("   ", " ".join("%3d"%y for y in range(800,843)))
