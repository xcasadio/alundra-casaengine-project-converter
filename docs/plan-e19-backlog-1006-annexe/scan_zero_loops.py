import json, os, sys
root = r"D:\development\repo\alundra-casaengine-project-converter\alundra-project"
hits = []
total = 0
loop_total = 0
for dp, dn, fn in os.walk(root):
    for f in fn:
        if not f.endswith(".anim2d"):
            continue
        total += 1
        p = os.path.join(dp, f)
        with open(p, "r", encoding="utf-8-sig") as fh:
            d = json.load(fh)
        if d.get("animation_type") != "Loop":
            continue
        loop_total += 1
        # duration = max keyframe time over all tracks
        maxt = 0.0
        for t in d.get("tracks", []):
            for k, v in t.items():
                if k.endswith("_keyframes"):
                    for kf in v:
                        maxt = max(maxt, kf["time_seconds"])
        if maxt == 0.0:
            hits.append(os.path.relpath(p, root))
print("anim2d files", total, "loop files", loop_total)
print("loop files whose last key time is 0:", len(hits))
for h in sorted(hits):
    print(h)
