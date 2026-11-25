import os
from PIL import Image

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm"
fr = os.path.join(base, "frames")

crops = [
    ("t06.00.png", (250, 260, 530, 380), "arm_mid"),
    ("t06.00.png", (430, 300, 560, 420), "robe_junction"),
    ("t15.00.png", (240, 430, 460, 600), "puppet_bottom"),
    ("t14.00.png", (320, 400, 470, 560), "junction_14"),
    ("t16.50.png", (20, 40, 340, 340), "corner_TL"),
    ("t01.50.png", (420, 30, 660, 240), "corner_TR"),
    ("t00.00.png", (20, 130, 300, 330), "left_wall_base"),
]
tiles = []
for f, box, tag in crops:
    im = Image.open(os.path.join(fr, f)).convert("RGB").crop(box)
    im = im.resize((im.width * 3, im.height * 3), Image.NEAREST)
    tiles.append((tag, im))

W = max(t[1].width for t in tiles)
H = sum(t[1].height + 18 for t in tiles)
sheet = Image.new("RGB", (W, H), (0, 0, 0))
from PIL import ImageDraw
d = ImageDraw.Draw(sheet)
y = 0
for tag, im in tiles:
    d.text((2, y + 3), tag, fill=(0, 255, 0))
    y += 18
    sheet.paste(im, (0, y))
    y += im.height
sheet.save(os.path.join(base, "_zoom.png"))
print(sheet.size)
