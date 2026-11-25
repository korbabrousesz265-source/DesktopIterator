import numpy as np, os
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
a = np.asarray(Image.open(os.path.join(ROOT,"frames","t112.png")).convert("RGB")).astype(np.float32)
g = a.mean(axis=2)
# horizontal scan in a quiet band of the room floor
for y in (700, 900, 1100):
    row = g[y, 800:1860]
    # grid lines are local dark minima; smooth then find minima
    sm = np.convolve(row, np.ones(3)/3, mode="same")
    mins = []
    for i in range(3, len(sm)-3):
        if sm[i] == sm[i-3:i+4].min() and sm[i] < np.median(sm) - 6:
            if not mins or i - mins[-1] > 8:
                mins.append(i)
            elif sm[i] < sm[mins[-1]]:
                mins[-1] = i
    xs = [800+m for m in mins]
    if len(xs) > 2:
        d = np.diff(xs)
        print("y=%d lines at %s"%(y, xs))
        print("   gaps:", d.tolist(), "median", float(np.median(d)))
print()
for x in (1100, 1400):
    col = g[220:1200, x]
    sm = np.convolve(col, np.ones(3)/3, mode="same")
    mins=[]
    for i in range(3, len(sm)-3):
        if sm[i] == sm[i-3:i+4].min() and sm[i] < np.median(sm) - 6:
            if not mins or i - mins[-1] > 8: mins.append(i)
            elif sm[i] < sm[mins[-1]]: mins[-1]=i
    ys=[220+m for m in mins]
    if len(ys)>2:
        print("x=%d lines at %s"%(x, ys))
        print("   gaps:", np.diff(ys).tolist(), "median", float(np.median(np.diff(ys))))
