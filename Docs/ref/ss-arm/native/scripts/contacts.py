"""Contact sheets: 4 sheets, <=200 frames, each tile labelled with its timecode.

usage: python contacts.py          -> contact/contact_1..4.png + calib/contact_index.json
"""
import json, os
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMES = os.path.join(ROOT, "frames")
OUTDIR = os.path.join(ROOT, "contact")
COLS, ROWS = 6, 6
CELL_W = 426
PER_SHEET = COLS * ROWS

files = sorted(f for f in os.listdir(FRAMES) if f.endswith(".png"))
os.makedirs(OUTDIR, exist_ok=True)
index = []
sheets = [files[i:i + PER_SHEET] for i in range(0, len(files), PER_SHEET)]

for si, chunk in enumerate(sheets, 1):
    first = Image.open(os.path.join(FRAMES, chunk[0]))
    w, h = first.size
    cw = CELL_W
    ch = int(round(h * cw / float(w)))
    sheet = Image.new("RGB", (COLS * cw, ROWS * (ch + 16)), (0, 0, 0))
    d = ImageDraw.Draw(sheet)
    for i, fn in enumerate(chunk):
        im = Image.open(os.path.join(FRAMES, fn)).convert("RGB").resize((cw, ch), Image.LANCZOS)
        x, y = (i % COLS) * cw, (i // COLS) * (ch + 16)
        sheet.paste(im, (x, y + 16))
        sec = int(fn[1:4])
        d.text((x + 4, y + 3), "t%03d  %02d:%02d  %s" % (sec, sec // 60, sec % 60, fn),
               fill=(0, 255, 120))
        index.append(dict(sheet=si, tile=i, file=fn, sec=sec))
    outp = os.path.join(OUTDIR, "contact_%d.png" % si)
    sheet.save(outp)
    print("wrote", outp, sheet.size, "tiles", len(chunk))

json.dump(index, open(os.path.join(ROOT, "calib", "contact_index.json"), "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)
