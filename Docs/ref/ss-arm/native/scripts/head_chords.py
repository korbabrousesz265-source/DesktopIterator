"""Head diameter by 50 %-crossing chords, only on frames where the disc is clean.

For every 1 fps frame:
  1. pinkness m = B - 0.8*G ; blob = m > 45 ; take the component that looks like
     the head (area 250..700, bbox aspect 1.0..1.5)
  2. chord width  : along the row through the blob's vertical centre
     chord height: along the column through its horizontal centre
  3. accept only if both chord ends have real background just outside
     (outside sample m < 25 for >= 2 px) -> no robe / no arm / no neck in the way
  4. sub-pixel crossing by linear interpolation between the outside sample and
     the plateau sample

Output: calib/head_chords.json + ranked table.
"""
import json, os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMES = os.path.join(ROOT, "frames")
OUT = os.path.join(ROOT, "calib", "head_chords.json")
T = 50.0


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


def cross(vals, idx, direction):
    """walk from the plateau outwards until below T, return sub-pixel crossing."""
    plateau = vals[idx]
    i = idx
    n = len(vals)
    while 0 <= i < n and vals[i] > T:
        i += direction
    if not (0 <= i < n):
        return None
    outside = vals[i]
    inside = vals[i - direction]
    if outside > 25:
        return None
    # require at least one more dark sample further out
    j = i + direction
    if not (0 <= j < n) or vals[j] > 30:
        return None
    if inside == outside:
        return float(i)
    return i - direction * (T - outside) / (inside - outside)


rows = []
for i in range(130):
    fn = "t%03d.png" % i
    a = np.asarray(Image.open(os.path.join(FRAMES, fn)).convert("RGB")).astype(np.float32)
    m = a[:, :, 2] - 0.8 * a[:, :, 1]
    lab = label(m > 45)
    ids, counts = np.unique(lab[lab > 0], return_counts=True)
    best = None
    for cid, cnt in zip(ids.tolist(), counts.tolist()):
        if not (250 <= cnt <= 700):
            continue
        ys, xs = np.nonzero(lab == cid)
        w = int(xs.max() - xs.min() + 1)
        h = int(ys.max() - ys.min() + 1)
        asp = w / float(h)
        if not (1.0 <= asp <= 1.5):
            continue
        best = (cid, cnt, xs, ys, w, h)
        break
    if best is None:
        rows.append(dict(sec=i, file=fn, ok=False, why="no head-shaped blob"))
        continue
    cid, cnt, xs, ys, w, h = best
    xc = (int(xs.min()) + int(xs.max())) // 2
    yc = (int(ys.min()) + int(ys.max())) // 2
    row = m[yc, max(0, xc - 30):xc + 31]
    col = m[max(0, yc - 26):yc + 27, xc]
    lx = cross(row, 30, -1)
    rx = cross(row, 30, +1)
    ty = cross(col, 26, -1)
    by = cross(col, 26, +1)
    if None in (lx, rx, ty, by):
        rows.append(dict(sec=i, file=fn, ok=False, why="chord touches robe/arm/neck",
                         blob=[int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())],
                         chord=[lx, rx, ty, by]))
        continue
    W = rx - lx
    H = by - ty
    rows.append(dict(sec=i, file=fn, ok=True, w=round(W, 2), h=round(H, 2),
                     aspect=round(W / H, 3), xc=xc, yc=yc,
                     blob=[int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())],
                     area=int(cnt)))

def _c(o):
    if isinstance(o, (np.floating,)):
        return round(float(o), 3)
    if isinstance(o, (np.integer,)):
        return int(o)
    raise TypeError(str(type(o)))


json.dump(rows, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=_c)
ok = [r for r in rows if r["ok"]]
ok.sort(key=lambda r: -r["w"])
print("clean chords: %d / 130 frames" % len(ok))
print(f'{"t":>4} {"W":>6} {"H":>6} {"asp":>5} {"x":>5} {"y":>5}  blob')
for r in ok[:24]:
    print(f'{r["sec"]:4d} {r["w"]:6.2f} {r["h"]:6.2f} {r["aspect"]:5.2f} {r["xc"]:5d} {r["yc"]:5d}  {r["blob"]}')
if ok:
    W = np.array([r["w"] for r in ok])
    H = np.array([r["h"] for r in ok])
    A = np.array([r["aspect"] for r in ok])
    print("\nmedian W=%.2f  H=%.2f  aspect=%.3f" % (np.median(W), np.median(H), np.median(A)))
    print("scale = %.3f   scale_y = %.3f" % (np.median(W) / 11.1111, np.median(H) / 9.0909))
