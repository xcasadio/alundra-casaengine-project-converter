"""Scan every exported .anim2d (read-only) for zero-duration animations and same-instant keys.
Predicts which files a converter fix "no terminator hide key for a zero-duration Loop" would change."""
import json, os, collections
root = r"D:\development\repo\alundra-casaengine-project-converter\alundra-project"
types = collections.Counter()
zero = []            # (type, path) duration 0
dup = []             # files with two keys at the same time on one track
invisible_at0 = []   # Loop files whose every part samples invisible at t=0 (engine: last key <= t wins)
total = 0
for dp, dn, fn in os.walk(root):
    for f in fn:
        if not f.endswith(".anim2d"):
            continue
        total += 1
        p = os.path.join(dp, f)
        rel = os.path.relpath(p, root)
        d = json.load(open(p, encoding="utf-8-sig"))
        t = d.get("animation_type"); types[t] += 1
        maxt = 0.0
        has_dup = False
        vis_at0 = {}
        for tr in d.get("tracks", []):
            for k, v in tr.items():
                if k.endswith("_keyframes"):
                    times = [kf["time_seconds"] for kf in v]
                    if times:
                        maxt = max(maxt, max(times))
                    if len(times) != len(set(times)):
                        has_dup = True
                    if k == "visible_keyframes":
                        val = None
                        for kf in v:
                            if kf["time_seconds"] > 0.0:
                                break
                            val = kf["value"]
                        vis_at0[tr["target_part_id"]] = val
        for ev in d.get("collision_keyframes", []) or []:
            maxt = max(maxt, ev.get("time_seconds", 0.0))
        if maxt == 0.0:
            zero.append((t, rel))
        if has_dup:
            dup.append((t, rel, maxt))
        if vis_at0 and not any(vis_at0.values()):
            invisible_at0.append((t, rel, maxt))
print("anim2d files", total, dict(types))
print("duration 0:", len(zero))
for z in sorted(zero): print("  ", z)
print("files with two keys at one instant on a track:", len(dup))
for z in sorted(dup)[:20]: print("  ", z)
print("files whose every part is invisible at t=0:", len(invisible_at0))
for z in sorted(invisible_at0)[:20]: print("  ", z)
