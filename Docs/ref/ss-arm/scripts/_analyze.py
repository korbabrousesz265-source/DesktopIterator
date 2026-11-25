import os, json
import numpy as np
from PIL import Image

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm"
fr = os.path.join(base, "frames")
files = sorted(f for f in os.listdir(fr) if f.endswith(".png"))

rows = []
for f in files:
    a = np.asarray(Image.open(os.path.join(fr, f)).convert("RGB")).astype(np.int16)
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    H, W = R.shape
    region = np.zeros_like(R, dtype=bool)
    region[42:614, 32:688] = True
    orange = (R > 165) & (G > 60) & (G < 170) & (B < 95) & (R - B > 100) & region
    arm = (R > 115) & (R - B >= 12) & (R - B <= 62) & (np.abs(G - B) <= 22) & (G > 95) & region
    ys, xs = np.nonzero(orange)
    px, py = (int(round(xs.mean())), int(round(ys.mean()))) if len(xs) else (-1, -1)
    ow, oh = (int(xs.max() - xs.min()), int(ys.max() - ys.min())) if len(xs) else (0, 0)
    ay, ax = np.nonzero(arm)
    out = dict(t=f, puppet=[px, py], ow=ow, oh=oh, arm_px=int(len(ax)))
    if len(ax):
        out["arm_bbox"] = [int(ax.min()), int(ay.min()), int(ax.max()), int(ay.max())]
        # anchor: arm pixels within 26 px of the image frame
        near_top = ay < 78
        near_left = ax < 88
        near_right = ax > 632
        near_bot = ay > 578
        anch = []
        if near_top.sum() > 30:
            anch.append("top@x=%d" % int(np.median(ax[near_top])))
        if near_bot.sum() > 30:
            anch.append("bottom@x=%d" % int(np.median(ax[near_bot])))
        if near_left.sum() > 30:
            anch.append("left@y=%d" % int(np.median(ay[near_left])))
        if near_right.sum() > 30:
            anch.append("right@y=%d" % int(np.median(ay[near_right])))
        out["anchor"] = "; ".join(anch)
    # thickness via row/col runs near the middle of each limb
    out["arm_px"] = int(len(ax))
    rows.append(out)

for r in rows:
    print("%s puppet=(%4d,%4d) orange=%3dx%3d arm=%5d bbox=%s anchor=%s" % (
        r["t"], r["puppet"][0], r["puppet"][1], r["ow"], r["oh"], r["arm_px"],
        r.get("arm_bbox"), r.get("anchor", "-")))

with open(os.path.join(base, "_analysis.json"), "w", encoding="utf-8") as fh:
    json.dump(rows, fh, indent=1)


def save(o):
    if isinstance(o, (np.integer,)):
        return int(o)
    raise TypeError


json.dump(rows, open(os.path.join(base, "_analysis.json"), "w", encoding="utf-8"), indent=1, default=save)
print("ok")
