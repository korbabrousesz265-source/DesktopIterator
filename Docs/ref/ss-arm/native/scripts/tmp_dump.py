import json, os
ROOT = os.path.dirname(os.path.dirname(os.path.abspath("scripts/x")))
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
rows = json.load(open(os.path.join(ROOT,"calib","pink_scan.json"), encoding="utf-8"))
print("frame size:", rows[0]["size"])
print(f'{"t":>4} {"pink":>5} {"sharp":>8} | components (area,w,h,aspect,fill,cx,cy)')
for r in rows:
    cs = "; ".join(f'{c["area"]:4d} {c["w"]:3d}x{c["h"]:3d} a{c["aspect"]:.2f} f{c["fill"]:.2f} ({c["cx"]:.0f},{c["cy"]:.0f})' for c in r["comps"][:3])
    print(f'{r["sec"]:4d} {r["pink_px"]:5d} {r["sharp"]:8.1f} | {cs}')
