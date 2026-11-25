import re, io
p = "arm_span.py"
s = open(p, encoding="utf-8").read()
s = s.replace("""    json.dump(dict(frame=name, seed=[sx, sy], levels=levels),""",
"""    levels = {int(k): {kk: (int(vv) if isinstance(vv, (np.integer,)) else float(vv))
                       for kk, vv in v.items()} for k, v in levels.items()}
    json.dump(dict(frame=name, seed=[int(sx), int(sy)], levels=levels),""")
open(p, "w", encoding="utf-8").write(s)
print("patched")
