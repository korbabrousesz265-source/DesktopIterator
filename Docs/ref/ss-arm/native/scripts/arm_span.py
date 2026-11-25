"""Arm span width profile on one frame, via geodesic distance from the root.

The arm band is the warm-grey blob that leaves the carriage plate.  A BFS from
a seed just under the plate gives, for every pixel, its distance along the arm;
the pixel count at each distance level is the band's width there (level sets of a
geodesic distance are locally perpendicular to the medial axis).

usage: python arm_span.py t060 1688 240
       -> stdout profile + calib/arm_span_<frame>.json + qc/armspan_<frame>.png
"""
import json, os, sys
from collections import deque
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def grey_mask(a, vmin=52, vmax=205, drg=(2, 24), dgb=(-8, 16)):
    R = a[:, :, 0].astype(np.int16)
    G = a[:, :, 1].astype(np.int16)
    B = a[:, :, 2].astype(np.int16)
    v = (R + G + B) / 3.0
    return ((v > vmin) & (v < vmax) & (R - G >= drg[0]) & (R - G <= drg[1]) &
            (G - B >= dgb[0]) & (G - B <= dgb[1]) & (R > 60))


def main():
    name, sx, sy = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
    im = Image.open(os.path.join(ROOT, "frames", name + ".png")).convert("RGB")
    a = np.asarray(im)
    m = grey_mask(a)
    h, w = m.shape
    dist = np.full((h, w), -1, np.int32)
    if not m[sy, sx]:
        # snap the seed to the nearest mask pixel
        ys, xs = np.nonzero(m[max(0, sy - 12):sy + 12, max(0, sx - 12):sx + 12])
        if len(ys) == 0:
            raise SystemExit("seed not on mask")
        sy, sx = sy - 12 + ys[0], sx - 12 + xs[0]
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

    levels = {}
    for d in range(0, 260):
        ys, xs = np.nonzero((dist >= d) & (dist < d + 4))
        if len(ys) == 0:
            break
        levels[d] = dict(n=int(len(ys)), cx=float(xs.mean()), cy=float(ys.mean()),
                         ymin=int(ys.min()), ymax=int(ys.max()),
                         xmin=int(xs.min()), xmax=int(xs.max()))

    print(f'{"d":>4} {"n":>4} {"width/3":>7}  centre      bbox')
    for d in sorted(levels)[:120]:
        L = levels[d]
        print(f'{d:4d} {L["n"]:4d} {L["n"]/3.0:7.2f}  ({L["cx"]:6.1f},{L["cy"]:6.1f})  '
              f'x{L["xmin"]}-{L["xmax"]} y{L["ymin"]}-{L["ymax"]}')

    levels = {int(k): {kk: (int(vv) if isinstance(vv, (np.integer,)) else float(vv))
                       for kk, vv in v.items()} for k, v in levels.items()}
    json.dump(dict(frame=name, seed=[int(sx), int(sy)], levels=levels),
              open(os.path.join(ROOT, "calib", "arm_span_%s.json" % name), "w",
                   encoding="utf-8"), ensure_ascii=False, indent=1)

    # overlay: colour by distance
    ov = a.copy()
    hue = ((dist.astype(np.float32) / 8.0) % 1.0)
    ys, xs = np.nonzero(dist >= 0)
    ov[ys, xs] = (np.stack([hue[ys, xs] * 255, (1 - hue[ys, xs]) * 200, 255 * (hue[ys, xs] > .5)],
                           1)).astype(np.uint8)
    out = Image.fromarray(ov)
    d2 = ImageDraw.Draw(out)
    d2.ellipse([sx - 3, sy - 3, sx + 3, sy + 3], outline=(255, 255, 0))
    outp = os.path.join(ROOT, "qc", "armspan_%s.png" % name)
    out.crop((1200, 130, 1900, 900)).resize((1400, 1540), Image.LANCZOS).save(outp)
    print("wrote", outp)


main()
