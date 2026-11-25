"""Arm / carriage grey mask + overlay, for eyeballing thresholds.

Mask: warm grey (arm body, carriage plate, primary cable), rejects the orange
room decor, the pink puppet and the near-black room interior.

usage: python arm_mask.py t060 t111 ...   -> qc/armmask_<frame>.png
       (mask painted green, components > 400 px outlined yellow with id)
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def grey_mask(a):
    R, G, B = a[:, :, 0].astype(np.int16), a[:, :, 1].astype(np.int16), a[:, :, 2].astype(np.int16)
    v = (R + G + B) / 3.0
    return (v > 58) & (v < 205) & (R - G >= 2) & (R - G <= 22) & (G - B >= -6) & (G - B <= 14) & (R > 60)


def label(mask):
    h, w = mask.shape
    lab = np.zeros((h, w), np.int32)
    parent = [0]

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    nxt = 1
    ys, xs = np.nonzero(mask)
    for y, x in zip(ys.tolist(), xs.tolist()):
        up = lab[y - 1, x] if y > 0 else 0
        lf = lab[y, x - 1] if x > 0 else 0
        if up and lf:
            a, b = find(up), find(lf)
            if a != b:
                parent[b] = a
            lab[y, x] = a
        elif up or lf:
            lab[y, x] = find(up or lf)
        else:
            parent.append(nxt)
            lab[y, x] = nxt
            nxt += 1
    flat = np.array([find(i) for i in range(nxt)], np.int32)
    return flat[lab]


def main():
    for name in sys.argv[1:]:
        p = os.path.join(ROOT, "frames", name + ".png")
        im = Image.open(p).convert("RGB")
        a = np.asarray(im)
        m = grey_mask(a)
        lab = label(m)
        ov = a.copy()
        ov[m] = (0, 255, 0)
        out = Image.fromarray(ov)
        d = ImageDraw.Draw(out)
        ids, counts = np.unique(lab[lab > 0], return_counts=True)
        order = np.argsort(-counts)[:12]
        for n, k in enumerate(order):
            cid, cnt = ids[k], counts[k]
            if cnt < 400:
                continue
            ys, xs = np.nonzero(lab == cid)
            x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
            d.rectangle([x0, y0, x1, y1], outline=(255, 255, 0))
            d.text((x0 + 2, y0 + 2), "#%d %dpx" % (n, cnt), fill=(255, 255, 0))
        outp = os.path.join(ROOT, "qc", "armmask_%s.png" % name)
        out.resize((1280, 720), Image.LANCZOS).save(outp)
        print("wrote", outp, "mask px", int(m.sum()))


main()
