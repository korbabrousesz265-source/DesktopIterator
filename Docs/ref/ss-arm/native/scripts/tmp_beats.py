import json, os, numpy as np
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
t3 = json.load(open(os.path.join(ROOT,"calib","track3.json"), encoding="utf-8"))
t4 = json.load(open(os.path.join(ROOT,"calib","track4.json"), encoding="utf-8"))
# where is the arm root each second: side + coordinate along that side
print(" t  side  along   gap |  head(x,y)      step | note")
prev_along=None
for a,b in zip(t3,t4):
    an = a["anchor"]; h = b["head"]
    if an:
        side = an["side"]
        along = an["x"] if side in ("T","B") else an["y"]
        gap = an["gap"]
    else:
        side, along, gap = "-", None, None
    hs = "(%.0f,%.0f)"%(h["x"],h["y"]) if h else "   -   "
    st = "%.0f"%b["step"] if (h and b["step"] is not None) else "  -"
    note=""
    if along is not None and prev_along is not None and side=="T" and abs(along-prev_along)>10:
        note="rail slide %+d"%int(along-prev_along)
    if along is not None: prev_along = along if side=="T" else None
    print("%3d  %s  %s  %s | %s %5s | %s"%(a["sec"], side, ("%6.0f"%along) if along is not None else "     -", ("%3d"%gap) if gap is not None else "  -", hs, st, note))
