import json, os, collections
root = r"D:\development\repo\alundra-casaengine-project-converter\alundra-project"
types = collections.Counter(); zero = collections.defaultdict(list); total = 0
for dp, dn, fn in os.walk(root):
    for f in fn:
        if not f.endswith(".anim2d"): continue
        total += 1
        d = json.load(open(os.path.join(dp, f), encoding="utf-8-sig"))
        types[d.get("animation_type")] += 1
        maxt = 0.0
        for t in d.get("tracks", []):
            for k, v in t.items():
                if k.endswith("_keyframes"):
                    for kf in v: maxt = max(maxt, kf["time_seconds"])
        if maxt == 0.0:
            zero[d.get("animation_type")].append(os.path.relpath(os.path.join(dp, f), root))
print(total, dict(types))
for k, v in zero.items(): print(k, len(v)); [print("  ", x) for x in sorted(v)[:12]]
