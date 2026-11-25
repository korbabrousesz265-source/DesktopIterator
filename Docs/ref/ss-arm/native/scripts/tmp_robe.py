import numpy as np, os, json
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
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
def comps(mask, minpx=200):
    lab = label(mask); out=[]
    ids, counts = np.unique(lab[lab>0], return_counts=True)
    for cid,cnt in zip(ids.tolist(), counts.tolist()):
        if cnt<minpx: continue
        ys,xs = np.nonzero(lab==cid)
        out.append(dict(area=int(cnt), x0=int(xs.min()), y0=int(ys.min()), x1=int(xs.max()), y1=int(ys.max()),
                        w=int(xs.max()-xs.min()+1), h=int(ys.max()-ys.min()+1),
                        x=float(xs.mean()), y=float(ys.mean())))
    out.sort(key=lambda d:-d["area"])
    return out
for name in ("t060","t112","t098","t020"):
    a = np.asarray(Image.open(os.path.join(ROOT,"frames",name+".png")).convert("RGB")).astype(np.int16)
    R,G,B = a[:,:,0],a[:,:,1],a[:,:,2]
    m = (R>140)&(G>50)&(G<150)&(B<80)&((R-G)>60)
    m[:215,:]=False; m[1205:,:]=False; m[:,:780]=False; m[:,1888:]=False
    cs = comps(m, 600)
    print(name, "orange comps:", [(c["area"],c["w"],c["h"],int(c["x"]),int(c["y"])) for c in cs[:4]])
