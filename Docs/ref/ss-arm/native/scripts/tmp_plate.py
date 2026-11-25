import numpy as np, os
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
for name, pts in [("t060", [(1740,180),(1720,200),(1750,215),(1690,245),(1700,260)]),
                  ("t123",[(1262,180),(1240,200),(1290,215)]),
                  ("t098",[(1270,210),(1250,230)])]:
    a = np.asarray(Image.open(os.path.join(ROOT,"frames",name+".png")).convert("RGB")).astype(int)
    print(name, [(x,y,tuple(a[y,x])) for x,y in pts])
