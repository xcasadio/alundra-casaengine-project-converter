"""critic: census of the map Info header words (Gravity, ZViscosity, SlideEffectId) over the 483 extracted maps. python -B -I census_header.py"""
import json, glob, os, collections
D = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
c = collections.Counter()
ex = collections.defaultdict(list)
n = 0
for f in sorted(glob.glob(D + "/map_*.json")):
    b = os.path.basename(f)
    if not b[4:-5].isdigit():
        continue
    d = json.load(open(f))
    i = d["Info"]
    k = (i["Gravity"], i["ZViscosity"], i["SlideEffectId"])
    c[k] += 1
    ex[k].append(int(b[4:-5]))
    n += 1
print("maps", n)
for k, v in c.most_common():
    print(k, v, ex[k][:12])
