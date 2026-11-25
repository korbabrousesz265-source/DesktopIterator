import os
from PIL import Image, ImageDraw

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm"
fr = os.path.join(base, "frames")
crops = [
    ("t12.00.png", (275, 290, 415, 430), "t12.00 head+arm"),
    ("t12.50.png", (275, 290, 415, 430), "t12.50 head+arm"),
    ("t13.00.png", (265, 290, 405, 430), "t13.00 head+arm"),
    ("t10.00.png", (455, 300, 555, 400), "t10.00 head+arm"),
]
tiles = []
for f, box, tag in crops:
    im = Image.open(os.path.join(fr, f)).convert("RGB").crop(box)
    im = im.resize((im.width * 5, im.height * 5), Image.NEAREST)
    tiles.append((tag, im))
W = sum(t[1].width + 8 for t in tiles)
H = max(t[1].height for t in tiles) + 20
sheet = Image.new("RGB", (W, H), (0, 0, 0))
d = ImageDraw.Draw(sheet)
x = 0
for tag, im in tiles:
    d.text((x + 2, 3), tag, fill=(0, 255, 0))
    sheet.paste(im, (x, 18))
    x += im.width + 8
sheet.save(os.path.join(base, "qc", "zoom_junction.png"))
print(sheet.size)
