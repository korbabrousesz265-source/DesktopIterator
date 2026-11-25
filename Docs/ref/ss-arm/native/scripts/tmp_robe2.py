import numpy as np, os
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
a = np.asarray(Image.open(os.path.join(ROOT,"frames","t060.png")).convert("RGB")).astype(int)
sub = a[820:900, 1480:1560]
# brightest orange pixels
R,G,B = sub[:,:,0],sub[:,:,1],sub[:,:,2]
m = (R>150)&(R-G>60)
print("robe pixels", m.sum(), "mean", sub[m].mean(axis=0).round(0) if m.sum() else None)
print("sample", [(x,y,tuple(a[y,x])) for x,y in [(1521,858),(1515,870),(1530,850),(1500,845)]])
