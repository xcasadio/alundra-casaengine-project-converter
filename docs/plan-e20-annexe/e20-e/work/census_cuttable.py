"""Census of cuttable cells (Walkability & 2) in the native map dumps data-extracted/data/map_N.json. Read-only.
Output: census_cuttable.json + census_cuttable.tsv in the scratch folder."""
import json, glob, os, re, sys, collections
D = r"D:\development\repo\alundra-casaengine-project-converter\data-extracted\data"
OUT = os.path.dirname(os.path.abspath(__file__))
rows = []
files = [f for f in glob.glob(os.path.join(D, "map_*.json")) if re.search(r"map_\d+\.json$", f)]
files.sort(key=lambda p: int(re.search(r"map_(\d+)\.json$", p).group(1)))
tot_cells = 0
hist_bal = collections.Counter()
heights = collections.Counter()
for f in files:
    mid = int(re.search(r"map_(\d+)\.json", f).group(1))
    with open(f, "r", encoding="utf-8") as fh:
        d = json.load(fh)
    m = d["Map"]
    w, h = m["Width"], m["Height"]
    tiles = m["MapTiles"]
    bal = d["Info"]["BalanceLevel"]
    n = 0; nstack = 0; nwo = 0; gp80 = 0; hs = collections.Counter(); both = 0
    for i, t in enumerate(tiles):
        if t["Walkability"] & 2:
            n += 1
            if t["WallTiles"] is not None: nstack += 1
            if t["WallTilesOffset"] != -1: nwo += 1
            hs[t["Height"]] += 1
            if t["Walkability"] & 0x40: both += 1
    rows.append(dict(map=mid, w=w, h=h, header_b9=bal, cells=len(tiles), cuttable=n, with_stack=nstack, with_offset=nwo, also_bit40=both, heights=dict(hs)))
    tot_cells += n
    if n: hist_bal[bal] += 1
    for k, v in hs.items(): heights[k] += v
json.dump(rows, open(os.path.join(OUT, "census_cuttable.json"), "w"), indent=1)
with open(os.path.join(OUT, "census_cuttable.tsv"), "w") as o:
    o.write("map\tw\th\theader_b9\tcuttable\twith_stack\twith_offset\talso_bit40\n")
    for r in rows:
        if r["cuttable"]:
            o.write(f'{r["map"]}\t{r["w"]}\t{r["h"]}\t{r["header_b9"]}\t{r["cuttable"]}\t{r["with_stack"]}\t{r["with_offset"]}\t{r["also_bit40"]}\n')
nm = sum(1 for r in rows if r["cuttable"])
print("maps read:", len(rows), "maps with cuttable:", nm, "cuttable cells:", tot_cells)
print("with wall stack:", sum(r["with_stack"] for r in rows), "with offset!=-1:", sum(r["with_offset"] for r in rows), "also bit 0x40:", sum(r["also_bit40"] for r in rows))
print("header b9 values over maps with cuttable cells:", dict(sorted(hist_bal.items())))
print("height (byte 3) distribution of cuttable cells:", dict(sorted(heights.items())))
