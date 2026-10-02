"""cellw.py - per map, the cells a REACHABLE 0x54 (set bits), 0x55 (clear bits) or 0x85 (copy a rectangle) can rewrite at
run time (operands as AlundraEventProgramRunner.cs:1213-1252). The census reads the exported cells only, so a walk blocked
on such a cell is 'uncertain (cell write)'. Output cellw.pkl {mid: {(x, y): [(pc, op)]}}."""
import collections
import os
import pickle
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from census_exec_orig import Map, SZ, reach, SLOTS  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8")


def roots(mp):
    out = []
    for e in mp.mapevents:
        idx = int(e["EventCodesBIndex"])
        t = mp.T["B"]
        if (idx & 0x7F) and (idx & 0x7F) < len(t) and t[idx & 0x7F]:
            out.append(t[idx & 0x7F])
    for r in mp.records:
        for slot, key in SLOTS:
            idx = int(r.get(key, 0))
            t = mp.T[slot]
            if (idx & 0x80) and (idx & 0x7F) and (idx & 0x7F) < len(t) and t[idx & 0x7F]:
                out.append(t[idx & 0x7F])
    return out


def build(mids):
    out = {}
    for mid in mids:
        mp = Map(mid)
        u = set()
        for s in roots(mp):
            u |= reach(mp.C, s)
        cw = collections.defaultdict(list)
        for pc in sorted(u):
            op = mp.C[pc]
            v = mp.C[pc:pc + 8] + [0] * 8
            if op in (0x54, 0x55):
                cw[(min(v[1], 0x33), min(v[2], 0x3B))].append((pc, op))
            elif op == 0x85:
                for dx in range(v[3]):
                    for dy in range(v[4]):
                        cw[(v[5] + dx, v[6] + dy)].append((pc, op))
        out[mid] = dict(cw)
    return out


if __name__ == "__main__":
    G = pickle.load(open(HERE + "/gstatic.pkl", "rb"))
    o = build(sorted(G["names"]))
    pickle.dump(o, open(HERE + "/cellw.pkl", "wb"))
    print("maps with cell writes", sum(1 for v in o.values() if v), "cells", sum(len(v) for v in o.values()))
    print(165, sorted(o[165])[:20])
