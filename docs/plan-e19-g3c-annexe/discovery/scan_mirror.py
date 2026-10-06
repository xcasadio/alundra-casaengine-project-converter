import json, glob, os, sys
from PIL import Image
root = "D:/development/repo/alundra-casaengine-project-converter/alundra-project"
files = glob.glob(root + "/Maps/*/*/effects/*.effects.json") + [root + "/Data/effects/effects-global.json"]
tot = 0; mx_n = 0; my_n = 0; mx0 = 0; my0 = 0; gut_bad_x = 0; gut_bad_y = 0
semi = {}
modes = {}
perfile_sheet = {}
for f in files:
    d = json.load(open(f, encoding="utf-8"))
    sheet = os.path.join(os.path.dirname(f), [n for n in os.listdir(os.path.dirname(f)) if n.endswith("_effectsheet.png")][0]) if any(n.endswith("_effectsheet.png") for n in os.listdir(os.path.dirname(f))) else None
    if sheet is None:
        if d.get("Effects"):
            perfile_sheet[f] = None
        continue
    im = Image.open(sheet).convert("RGBA"); W, H = im.size; px = im.load()
    for t in d["Effects"]:
        for s in t["ImageSets"]:
            for img in s["Images"]:
                tot += 1
                C = img["C"]
                mx = C[0] > C[2]; my = C[1] > C[5]
                key = (img["Semi"], img["Abr"]); modes[key] = modes.get(key, 0) + 1
                if mx:
                    mx_n += 1
                    if img["U"] == 0: mx0 += 1
                    else:
                        col = img["U"] - 1
                        if any(px[col, y][3] != 0 for y in range(img["V"], img["V"] + img["H"]) if 0 <= y < H): gut_bad_x += 1
                if my:
                    my_n += 1
                    if img["V"] == 0: my0 += 1
                    else:
                        row = img["V"] - 1
                        if any(px[x, row][3] != 0 for x in range(img["U"], img["U"] + img["W"]) if 0 <= x < W): gut_bad_y += 1
print("images", tot, "mirX", mx_n, "mirY", my_n, "mirX at U=0", mx0, "mirY at V=0", my0, "gutter col not transparent", gut_bad_x, "gutter row not transparent", gut_bad_y)
print("modes (semi, abr):", modes)
print("global-table companions w/o sheet but with effects:", [k for k,v in perfile_sheet.items()][:3])
