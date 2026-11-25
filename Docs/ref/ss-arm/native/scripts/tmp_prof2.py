import numpy as np, os
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
def prof(name, x0,y0,x1,y1):
    a = np.asarray(Image.open(os.path.join(ROOT,"frames",name+".png")).convert("RGB")).astype(np.float32)
    m = a[:,:,2]-0.8*a[:,:,1]
    print("== %s row y=%d, x %d..%d"%(name,y0,x0,x1))
    vals = " ".join("%3d"%m[y0,x] for x in range(x0,x1+1))
    print("  "+vals)
    # find 90-10% edges
    print("   x index:", " ".join("%3d"%x for x in range(x0,x1+1)))
for name,args in [("t098",(1700,412,1750,412)),]: pass
prof("t098",1700,412,1750,412)
prof("t018",1730,873,1780,873)
prof("t000",960,464,1010,464)
