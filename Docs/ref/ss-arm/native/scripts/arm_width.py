"""Perpendicular width profile along the arm, on one frame.

Medial path = centroids of geodesic level sets seeded just under the carriage.
At each path point the width is measured by marching along the local normal
until the arm mask is left on both sides -> true perpendicular width, sub-pixel.

usage: python arm_width.py t060 1697 236
       -> calib/arm_width_<frame>.json, qc/armwidth_<frame>.png, stdout table
"""
import json, os, sys
from collections import deque
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
YCLIP = 234          # carriage plate bottom; keeps the plate out of the mask


def grey_mask(a, vmin=50, vmax=210, drg=(2, 25), dgb=(-9, 17)):
    R = a[:, :, 0].astype(np.int16)
    G = a[:, :, 1].astype(np.int16)
    B = a[:, :, 2].astype(np.int16)
    v = (R + G + B) / 3.0
    m = ((v > vmin) & (v < vmax) & (R - G >= drg[0]) & (R - G <= drg[1]) &
         (G - B >= dgb[0]) & (G - B <= dgb[1]) & (R > 60))
    m[:YCLIP, :] = False
    return m


def main():
    name, sx, sy = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
    im = Image.open(os.path.join(ROOT, "frames", name + ".png")).convert("RGB")
    a = np.asarray(im)
    m = grey_mask(a)
    h, w = m.shape
    dist = np.full((h, w), -1, np.int32)
    if not m[sy, sx]:
        ys, xs = np.nonzero(m[sy - 6:sy + 10, sx - 14:sx + 14])
        sy, sx = sy - 6 + ys[0], sx - 14 + xs[0]
        print("seed snapped to", sx, sy)
    q = deque([(sy, sx)])
    dist[sy, sx] = 0
    while q:
        y, x = q.popleft()
        d = dist[y, x]
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy == 0 and dx == 0:
                    continue
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < w and m[ny, nx] and dist[ny, nx] < 0:
                    dist[ny, nx] = d + 1
                    q.append((ny, nx))

    # medial path
    path = []
    for d in range(0, 400, 2):
        ys, xs = np.nonzero((dist >= d) & (dist < d + 2))
        if len(ys) < 2:
            break
        path.append((d, float(xs.mean()), float(ys.mean())))
    P = np.array([[p[1], p[2]] for p in path])
    D = np.array([p[0] for p in path])

    def mval(x, y):
        xi, yi = int(round(x)), int(round(y))
        if xi < 0 or yi < 0 or xi >= w or yi >= h:
            return False
        return bool(m[yi, xi])

    def width_at(i):
        lo = max(0, i - 2)
        hi = min(len(P) - 1, i + 2)
        t = P[hi] - P[lo]
        n = np.array([-t[1], t[0]])
        ln = np.hypot(*n)
        if ln < 1e-6:
            return None
        n /= ln
        x0, y0 = P[i]
        out = []
        for sgn in (1, -1):
            s = 0.0
            misses = 0
            while s < 40:
                s += 0.25
                if mval(x0 + sgn * n[0] * s, y0 + sgn * n[1] * s):
                    misses = 0
                else:
                    misses += 1
                    if misses >= 6:
                        break
            out.append(s - (misses - 1) * 0.25)
        return out[0] + out[1], out

    rows = []
    for i in range(len(P)):
        r = width_at(i)
        if r is None:
            continue
        wid, halves = r
        rows.append(dict(d=int(D[i]), x=round(float(P[i][0]), 1), y=round(float(P[i][1]), 1),
                         width=round(float(wid), 2),
                         left=round(float(halves[0]), 2), right=round(float(halves[1]), 2)))

    print(f'{"d":>4} {"x":>7} {"y":>7} {"width":>6} {"L":>6} {"R":>6}')
    for r in rows:
        if r["d"] % 4 == 0:
            print(f'{r["d"]:4d} {r["x"]:7.1f} {r["y"]:7.1f} {r["width"]:6.2f} {r["left"]:6.2f} {r["right"]:6.2f}')

    json.dump(dict(frame=name, seed=[int(sx), int(sy)], rows=rows),
              open(os.path.join(ROOT, "calib", "arm_width_%s.json" % name), "w",
                   encoding="utf-8"), ensure_ascii=False, indent=1)

    # evidence overlay
    out = im.copy()
    d2 = ImageDraw.Draw(out)
    for i, r in enumerate(rows):
        if r["d"] % 10:
            continue
        i0 = min(range(len(P)), key=lambda k: abs(P[k][0] - r["x"]) + abs(P[k][1] - r["y"]))
        lo, hi = max(0, i0 - 2), min(len(P) - 1, i0 + 2)
        t = P[hi] - P[lo]
        n = np.array([-t[1], t[0]])
        n /= max(1e-6, np.hypot(*n))
        x0, y0 = r["x"], r["y"]
        d2.line([x0 - n[0] * r["left"] * 1.0, y0 - n[1] * r["left"] * 1.0,
                 x0 + n[0] * r["right"] * 1.0, y0 + n[1] * r["right"] * 1.0],
                fill=(0, 255, 255), width=1)
        d2.text((x0 + 3, y0 - 10), "%d:%.0f" % (r["d"], r["width"]), fill=(0, 255, 0))
    outp = os.path.join(ROOT, "qc", "armwidth_%s.png" % name)
    out.crop((1200, 150, 1800, 780)).resize((1500, 1575), Image.LANCZOS).save(outp)
    print("wrote", outp)


main()
