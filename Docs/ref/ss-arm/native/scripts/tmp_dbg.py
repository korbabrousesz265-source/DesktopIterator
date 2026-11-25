import sys, os, numpy as np
sys.path.insert(0,".")
import head_measure as hm
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
for sec in (112, 0, 60):
    p = os.path.join(ROOT,"frames","t%03d.png"%sec)
    im = Image.open(p)
    m = hm.hue_mask(im)
    lab = hm.label(m)
    ids, counts = np.unique(lab[lab>0], return_counts=True)
    order = np.argsort(-counts)
    print("=== t%03d  hue px=%d"%(sec, m.sum()))
    for k in order[:6]:
        cid, cnt = ids[k], counts[k]
        if cnt < 60: continue
        b = hm.blob_shape(lab, cid)
        print("  area %5d bbox %dx%d at (%d,%d) aspect %.2f fill %.2f rowmax %d colmax %d"%(
            b["area"], b["w"], b["h"], b["x0"], b["y0"], b["w"]/b["h"], b["fill"], b["max_row_w"], b["max_col_h"]))
    a = np.asarray(im.convert("RGB")).astype(int)
    ys,xs = np.nonzero(m)
    if len(ys):
        print("  mean RGB of hue mask:", a[ys,xs].mean(axis=0).round(1))
