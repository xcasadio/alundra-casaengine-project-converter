import json, sys
import c9_replay as C
for f in sys.argv[1:]:
    d = json.load(open(f)); n = 0; bad = 0
    for v in d["vectors"]:
        n += 1
        try:
            w, exp, c, si = C.replay(v, "extra")
        except Exception as ex:
            bad += 1; print("EXC", v.get("id"), ex); continue
        ok = (w == exp)
        if ok and "ret" in v and c.r[2] != v["ret"]:
            ok = False
        if not ok: bad += 1; print("MISMATCH", v.get("id"))
    print(f, "vectors", n, "failures", bad)
