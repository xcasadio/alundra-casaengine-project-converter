"""tags.py - per map, per record: the reachable ops that change its presence as an obstacle (read from walks_DLL.pkl states,
so the logic entity of a 0x80 selector is the one of the walk). Output tags.pkl:
  {mid: {("R", i) | "H": {"destroy": [(pc, label)], "decollide": [...], "collide": [...], "deactivate": [...],
                          "spawn": [...], "noslide_set": [...], "noslide_clear": [...], "native_c": idx or None}}}
Selectors >= 0x82 (searches by type/function) are recorded under the key "search" (they may hit any record)."""
import collections
import os
import pickle
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from census_exec_orig import Map, HERO, SZ  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8")


class BPin(tuple):
    """unpickling stub of c0b.BPin"""


def tgt(sel, logic):
    if sel == 0x80:
        return [logic]
    if sel == 0x81:
        return [HERO]
    if sel < 0x80:
        return [("R", sel)]
    return ["search"]


def build(tag="DLL"):
    walks = pickle.load(open(HERE + "/walks_%s.pkl" % tag, "rb"))
    out = {}
    for mid, wk in walks.items():
        mp = Map(mid)
        T = collections.defaultdict(lambda: collections.defaultdict(list))
        for r in mp.records:
            ci = int(r.get("EventCodesC_TickIndex", 0))
            if ci and not (ci & 0x80):
                T[("R", int(r["Index"]))]["native_c"].append(ci)
        for lab, states in wk.items():
            if lab == "__owners__":
                continue
            for pc, sts in states.items():
                op = mp.C[pc]
                v = mp.C[pc:pc + 10] + [0] * 10
                logics = {st[0] for st in sts}
                for logic in logics:
                    if op == 0x2E:
                        for t in tgt(v[1], logic):
                            T[t]["destroy"].append((pc, lab))
                    elif op == 0x63 and ((v[2] | (v[3] << 8)) & 0x80):
                        for t in tgt(v[1], logic):
                            T[t]["decollide"].append((pc, lab))
                    elif op == 0x62 and ((v[2] | (v[3] << 8)) & 0x80):
                        for t in tgt(v[1], logic):
                            T[t]["collide"].append((pc, lab))
                    elif op == 0x19:
                        T[logic]["deactivate"].append((pc, lab))
                    elif op in (0x2D, 0x8A):
                        T[("R", v[1])]["spawn"].append((pc, lab))
                    elif op == 0x8B:
                        T[("R", v[2])]["spawn"].append((pc, lab))
                    elif op == 0x46:
                        T[logic]["noslide_set"].append((pc, lab))
                    elif op == 0x45:
                        T[logic]["noslide_clear"].append((pc, lab))
        out[mid] = {k: {kk: sorted(set(vv)) for kk, vv in d.items()} for k, d in T.items()}
    return out


if __name__ == "__main__":
    t = build(sys.argv[1] if len(sys.argv) > 1 else "DLL")
    pickle.dump(t, open(HERE + "/tags.pkl", "wb"))
    print("maps", len(t))
    for mid in (10, 185):
        for k, d in sorted(t.get(mid, {}).items(), key=lambda kv: repr(kv[0]))[:80]:
            print(mid, k, {kk: vv[:3] for kk, vv in d.items()})
