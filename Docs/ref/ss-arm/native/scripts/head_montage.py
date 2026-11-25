"""Montage of per-frame head crops.

Each tile: 76x76 px window centred on the detected head, 5x nearest-neighbour,
red rectangle = pinkness mask bbox, cyan text = timecode + measured w x h.

usage: python head_montage.py 0 35 calib/head_sheet_1.png
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RANK = json.load(open(os.path.join(ROOT, "calib", "head_rank.json"), encoding="utf-8"))

SC = 5
WIN = 76
COLS, ROWS = 7, 5


def main():
    lo, hi, out = int(sys.argv[1]), int(sys.argv[2]), sys.argv[3]
    cells = []
    for r in RANK[lo:hi]:
        im = Image.open(os.path.join(ROOT, "frames", r["file"])).convert("RGB")
        b = r["head"]
        if b:
            cx = (b["x0"] + b["x1"]) // 2
            cy = (b["y0"] + b["y1"]) // 2
        else:
            cx, cy = im.width // 2, im.height // 2
        x0, y0 = cx - WIN // 2, cy - WIN // 2
        crop = im.crop((x0, y0, x0 + WIN, y0 + WIN)).resize((WIN * SC, WIN * SC), Image.NEAREST)
        d = ImageDraw.Draw(crop)
        if b:
            d.rectangle([(b["x0"] - x0) * SC, (b["y0"] - y0) * SC,
                         (b["x1"] - x0 + 1) * SC - 1, (b["y1"] - y0 + 1) * SC - 1],
                        outline=(255, 0, 0), width=1)
            lab = "t%03d  %dx%d a%.2f" % (r["sec"], b["w"], b["h"], b["aspect"])
        else:
            lab = "t%03d  none" % r["sec"]
        d.rectangle([0, 0, 150, 14], fill=(0, 0, 0))
        d.text((2, 2), lab, fill=(0, 255, 255))
        cells.append(crop)

    w, h = WIN * SC, WIN * SC
    sheet = Image.new("RGB", (COLS * w, ROWS * h), (20, 20, 20))
    for i, c in enumerate(cells):
        sheet.paste(c, ((i % COLS) * w, (i // COLS) * h))
    outp = os.path.join(ROOT, out)
    os.makedirs(os.path.dirname(outp), exist_ok=True)
    sheet.save(outp)
    print("wrote", outp, sheet.size, "tiles", len(cells))


main()
