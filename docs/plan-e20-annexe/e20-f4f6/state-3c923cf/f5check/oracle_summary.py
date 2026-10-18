import json, sys, os
# usage: oracle_summary.py DIR name1 name2 ...  -> per scenario: S, first adj tick, status timeline, hits, draws, last tick
d = sys.argv[1]
for name in sys.argv[2:]:
    p = os.path.join(d, name + ".json")
    rows = json.load(open(p))
    S = None
    adj = None
    st3 = None
    st4 = None
    last = None
    hit = []
    statuses = []
    total_draws = 0
    for r in rows:
        total_draws += r["draws"]
        arrows = [(k, s) for k, s in r["slots"].items() if s.get("type68") in (3, 4)]
        for k, s in arrows:
            if S is None:
                S = r["tick"]
            rel = r["tick"] - S
            last = rel
            if adj is None and s["adj_13C"]:
                adj = rel
            if st3 is None and s["status"] == 3:
                st3 = rel
            if st4 is None and s["status"] == 4:
                st4 = rel
            if s["hitc"]:
                hit.append((rel, s["hitc"], r["draws"]))
            statuses.append((rel, s["status"]))
    # compress status runs
    runs = []
    for rel, st in statuses:
        if runs and runs[-1][2] == st and runs[-1][1] == rel - 1:
            runs[-1][1] = rel
        else:
            runs.append([rel, rel, st])
    print(f"{name}: S={S} adj@S+{adj} st3@S+{st3} st4@S+{st4} last@S+{last} hits={hit} draws_total={total_draws} runs={[(a,b,c) for a,b,c in runs]}")
