import json, sys

f = sys.argv[1]
d = json.load(open(f, encoding="utf-8"))
ck = d.get("collision_keyframes", [])
print("collision_keyframes:", len(ck))
prev = None
for kf in ck:
    s = json.dumps({k: v for k, v in kf.items() if k != "time_seconds"}, sort_keys=True)
    t = kf.get("time_seconds")
    if s != prev:
        print("  t=%.3f (tick %d)" % (t, round(t * 50)), s[:400])
        prev = s
print("keys of first:", list(ck[0].keys()) if ck else None)
