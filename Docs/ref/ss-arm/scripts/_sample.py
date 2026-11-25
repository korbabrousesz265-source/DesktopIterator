import numpy as np
from PIL import Image
p = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\frames\t06.00.png"
a = np.asarray(Image.open(p).convert("RGB"))
pts = [(390,120),(385,200),(380,270),(300,300),(350,338),(200,330),(490,340),(520,300),(250,340)]
for (x,y) in pts:
    print((x,y), a[y-1:y+2, x-1:x+2].reshape(-1,3).mean(axis=0).round(1))
