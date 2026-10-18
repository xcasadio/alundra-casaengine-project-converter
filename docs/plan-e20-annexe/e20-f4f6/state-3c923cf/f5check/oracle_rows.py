import json, sys
# usage: oracle_rows.py FILE [type]
path = sys.argv[1]
want = int(sys.argv[2]) if len(sys.argv) > 2 else 3
rows = json.load(open(path))
S = None
out = []
for r in rows:
    for k, s in r["slots"].items():
        if s.get("type68") == want:
            if S is None:
                S = r["tick"]
            out.append((r["tick"], k, s, r))
print("S =", S)
for t, k, s, r in out:
    print(f"S+{t-S:>2} slot {k} st {s['status']} px {s['px']/65536:.3f} pz {s['pz']/65536:.3f} fx {s['fx']} fz {s['fz']} adj {s['adj_13C']} dir {s['dir_t']} anim {s['anim_t']} fl {hex(s['flags6C'])} box {1 if s['box1d4'] else 0} rAC {s['reset_AC']} hitc {s['hitc']} c1d0 {s['c1d0']} atk {s['atk_attr']}/{s['atk_pow']} draws {r['draws']} j {r['journal']} eff {len(r['effects'])}")
last = rows[-1]["tick"]
print("last row tick", last, "S+", last - S if S else None)
