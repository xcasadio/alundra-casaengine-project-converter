import json, glob, os, sys

folder = sys.argv[1]
for f in sorted(glob.glob(os.path.join(folder, "*.anim2d"))):
    d = json.load(open(f, encoding="utf-8"))
    name = os.path.basename(f)
    tracks = d.get("tracks", [])
    last = 0.0
    nsprite = 0
    evt = []
    for t in tracks:
        for k in t.keys():
            if k.endswith("_keyframes"):
                for kf in t[k]:
                    last = max(last, kf.get("time_seconds", 0.0))
        if t.get("property") == "Sprite":
            nsprite = len(t.get("sprite_keyframes", []))
    for k in d.keys():
        if k not in ("tracks", "parts", "id", "name", "animation_type", "event_track_name"):
            evt.append(k)
    print(name, d.get("animation_type"), "last=%.3f" % last, "spriteKF=%d" % nsprite, "extraKeys=%s" % evt,
          "tracks=%s" % [t.get("name") + ":" + str(t.get("property")) for t in tracks])
