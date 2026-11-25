"""Overview sheets: 24 tiles per sheet at 4x downscale, labelled with timecode.

usage: python overview.py   -> qc/overview_1..6.png
"""
import json, os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMES = os.path.join(ROOT, "frames")
OUT = os.path.join(ROOT, "qc")
COLS, ROWS = 4, 6
TW, TH = 640, 360
PER = COLS * ROWS

files = sorted(f for f in os.listdir(FRAMES) if f.endswith(".png"))
os.makedirs(OUT, exist_ok=True)
idx = []
for si in range(0, len(files), PER):
    chunk = files[si:si + PER]
    sheet = Image.new("RGB", (COLS * TW, ROWS * (TH + 18)), (0, 0, 0))
    d = ImageDraw.Draw(sheet)
    for i, fn in enumerate(chunk):
        im = Image.open(os.path.join(FRAMES, fn)).convert("RGB").resize((TW, TH), Image.LANCZOS)
        x, y = (i % COLS) * TW, (i // COLS) * (TH + 18)
        sheet.paste(im, (x, y + 18))
        sec = int(fn[1:4])
        d.text((x + 4, y + 3), "t%03d   %02d:%02d" % (sec, sec // 60, sec % 60), fill=(0, 255, 120))
        idx.append([si // PER + 1, fn, sec])
    n = si // PER + 1
    outp = os.path.join(OUT, "overview_%d.png" % n)
    sheet.save(outp)
    print("wrote", outp, sheet.size, len(chunk))
json.dump(idx, open(os.path.join(ROOT, "calib", "overview_index.json"), "w", encoding="utf-8"))
