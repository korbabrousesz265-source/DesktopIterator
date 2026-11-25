p="head_chords.py"; s=open(p,encoding="utf-8").read()
s=s.replace('''json.dump(rows, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)''',
'''def _c(o):
    if isinstance(o, (np.floating,)):
        return round(float(o), 3)
    if isinstance(o, (np.integer,)):
        return int(o)
    raise TypeError(str(type(o)))


json.dump(rows, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=_c)''')
open(p,"w",encoding="utf-8").write(s); print("patched")
