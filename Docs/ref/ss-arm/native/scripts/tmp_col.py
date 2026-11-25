import numpy as np, os
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
def col(name, x, y0, y1):
    a = np.asarray(Image.open(os.path.join(ROOT,"frames",name+".png")).convert("RGB")).astype(np.float32)
    m = a[:,:,2]-0.8*a[:,:,1]
    print("== %s col x=%d, y %d..%d"%(name,x,y0,y1))
    print("  vals:", " ".join("%3d"%m[y,x] for y in range(y0,y1+1)))
    print("  ys:  ", " ".join("%3d"%y for y in range(y0,y1+1)))
col("t098",1724,395,435)
col("t112",1000,790,820)
col("t098",1718,395,435)
