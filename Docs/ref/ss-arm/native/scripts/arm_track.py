"""Per-frame tracking: puppet head centroid + arm component + carriage anchor.

* head  : pinkness blob that is closest to the previous frame's head
* arm   : warm-grey mask, largest component (arm + carriage + primary cable)
* anchor: the arm pixel nearest to the room's inner rectangle border
          -> where the carriage grips the rail

usage: python arm_track.py  -> calib/track.json + stdout table
"""
import json, os
from collections import deque
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMES = os.path.join(ROOT, "frames")
OUT = os.path.join(ROOT, "calib", "track.json")

# room inner rectangle (full-res px), verified visually on t000/t060/t112
ROOM = (768, 206, 1896, 1212)


def pinkness(a):
    return a[:, :, 2].astype(np.float32) - 0.8 * a[:, :, 1].astype(np.float32)


def grey_mask(a):
    R = a[:, :, 0].astype(np.int16)
    G = a[:, :, 1].astype(np.int16)
    B = a[:, :, 2].astype(np.int16)
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


def head_centroid(a, prev=None):
    m = pinkness(a)
    mask = m > 50
    if not mask.any():
        return None
    lab = label(mask)
    ids, counts = np.unique(lab[lab > 0], return_counts=True)
    best = None
    for cid, cnt in zip(ids.tolist(), counts.tolist()):
        if cnt < 120:
            continue
        ys, xs = np.nonzero(lab == cid)
        cx, cy = float(xs.mean()), float(ys.mean())
        d = 0.0 if prev is None else ((cx - prev[0]) ** 2 + (cy - prev[1]) ** 2) ** 0.5
        score = (d, -cnt)
        if best is None or score < best[0]:
            best = (score, cx, cy, int(cnt))
    if best is None:
        return None
    return best[1], best[2], best[3]


def arm_component(a):
    m = grey_mask(a)
    lab = label(m)
    ids, counts = np.unique(lab[lab > 0], return_counts=True)
    if len(ids) == 0:
        return None, None
    k = int(np.argmax(counts))
    cid = ids[k]
    if counts[k] < 2000:
        return None, None
    return (lab == cid), int(counts[k])


def anchor_point(comp):
    ys, xs = np.nonzero(comp)
    x0, y0, x1, y1 = ROOM
    dl = np.abs(xs - x0)
    dr = np.abs(xs - x1)
    dt = np.abs(ys - y0)
    db = np.abs(ys - y1)
    cands = [(dl.min(), "L"), (dr.min(), "R"), (dt.min(), "T"), (db.min(), "B")]
    d, side = min(cands)
    if side == "L":
        sel = dl <= d
    elif side == "R":
        sel = dr <= d
    elif side == "T":
        sel = dt <= d
    else:
        sel = db <= d
    return dict(side=side, dist=float(d),
                x=float(xs[sel].mean()), y=float(ys[sel].mean()),
                xmin=int(xs.min()), xmax=int(xs.max()),
                ymin=int(ys.min()), ymax=int(ys.max()))


rows = []
prev = None
for i in range(130):
    fn = "t%03d.png" % i
    a = np.asarray(Image.open(os.path.join(FRAMES, fn)).convert("RGB"))
    h = head_centroid(a, prev)
    if h:
        prev = (h[0], h[1])
    comp, area = arm_component(a)
    anc = anchor_point(comp) if comp is not None else None
    rows.append(dict(sec=i, file=fn,
                     head=None if not h else dict(x=round(h[0], 1), y=round(h[1], 1), px=h[2]),
                     arm_area=area, anchor=anc))
    if i % 10 == 0:
        print(i, "head", None if not h else (round(h[0]), round(h[1])), "arm", area,
              "anchor", None if not anc else (anc["side"], round(anc["x"]), round(anc["y"])), flush=True)

json.dump(rows, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

print()
print(f'{"t":>4} {"head_x":>7} {"head_y":>7} {"arm_px":>7} {"side":>4} {"anc_x":>7} {"anc_y":>7} {"d":>5}  arm bbox')
for r in rows:
    h, an = r["head"], r["anchor"]
    hs = f'{h["x"]:7.0f} {h["y"]:7.0f}' if h else "      -       -"
    if an:
        print(f'{r["sec"]:4d} {hs} {r["arm_area"]:7d} {an["side"]:>4} {an["x"]:7.0f} {an["y"]:7.0f} {an["dist"]:5.0f}  '
              f'({an["xmin"]},{an["ymin"]})-({an["xmax"]},{an["ymax"]})')
    else:
        print(f'{r["sec"]:4d} {hs} {str(r["arm_area"]):>7}    -       -       -     -  -')
