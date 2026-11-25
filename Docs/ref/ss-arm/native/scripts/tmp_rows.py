import json, os
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
d = json.load(open(os.path.join(ROOT,"calib","arm_width_t060.json"), encoding="utf-8"))
rows = {r["d"]: r for r in d["rows"]}
for k in (6,8,10,12,14,16,18,20,22,24,26,28,30,32,36,40,60,100,140,180,220,260,280,288,296,304,312):
    if k in rows:
        r = rows[k]
        print("d=%3d  x=%.1f y=%.1f  width=%5.2f  (L %.2f R %.2f)"%(k, r["x"], r["y"], r["width"], r["left"], r["right"]))
print("max d", d["rows"][-1]["d"])
# arm span1 end = the bend: find where dy/dchanges
