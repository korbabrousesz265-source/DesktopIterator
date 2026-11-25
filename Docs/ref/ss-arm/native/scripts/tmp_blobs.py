import numpy as np, os, collections
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
FR = os.path.join(ROOT,"frames")
def label(mask):
    h,w = mask.shape
    lab = np.zeros((h,w), np.int32); parent=[0]
    def find(x):
        while parent[x]!=x:
            parent[x]=parent[parent[x]]; x=parent[x]
        return x
    nxt=1
    ys,xs = np.nonzero(mask)
    for y,x in zip(ys.tolist(), xs.tolist()):
        up = lab[y-1,x] if y>0 else 0
        lf = lab[y,x-1] if x>0 else 0
        if up and lf:
            a,b = find(up), find(lf)
            if a!=b: parent[b]=a
            lab[y,x]=a
        elif up or lf: lab[y,x]=find(up or lf)
        else:
            parent.append(nxt); lab[y,x]=nxt; nxt+=1
    flat = np.array([find(i) for i in range(nxt)], np.int32)
    return flat[lab]

hist = collections.Counter(); nblob=[]
per = []
for i in range(130):
    a = np.asarray(Image.open(os.path.join(FR,"t%03d.png"%i)).convert("RGB")).astype(np.float32)
    m = (a[:,:,2] - 0.8*a[:,:,1]) > 50
    lab = label(m)
    ids, counts = np.unique(lab[lab>0], return_counts=True)
    blobs=[]
    for cid,cnt in zip(ids.tolist(), counts.tolist()):
        if cnt < 150: continue
        ys,xs = np.nonzero(lab==cid)
        w = xs.max()-xs.min()+1; h = ys.max()-ys.min()+1
        hist[(int(w),int(h))]+=1
        blobs.append((cnt,int(w),int(h),int(xs.mean()),int(ys.mean())))
    blobs.sort(reverse=True)
    per.append(blobs)
    nblob.append(len(blobs))
print("blobs per frame: min %d max %d mean %.1f"%(min(nblob),max(nblob),np.mean(nblob)))
print("top (w,h) histograms:")
for (w,h),c in hist.most_common(25):
    print("  %2dx%-2d : %3d"%(w,h,c))
