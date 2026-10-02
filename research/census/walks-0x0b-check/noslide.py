"""noslide.py - for the difference-class rows whose mechanism is the tile rule (corner slide), is NoObstacleSlide (0x46) or a
class op (0x28-0x2B) or a gravity op (0x16/0x17) executed on the path from the program entry to the site?
Path analysis on the checker's own control-flow graph (pop.succ): every instruction of the root's reach that can reach the
site; for 0x45/0x46 we compute, per instruction, the set of possible NoObstacleSlide states (forward data flow)."""
import csv
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
C0 = os.path.join(os.path.dirname(HERE), "census0b")
sys.path.insert(0, HERE)
from pop import maps, succ, SZ_BIN  # noqa: E402
import pickle  # noqa: E402

M = maps()
tab = {(int(r["map"]), int(r["pc"])): r for r in csv.DictReader(open(C0 + "/table.tsv", encoding="utf-8"), delimiter="\t")}
rows = {(r["map"], r["pc"]): r for r in pickle.load(open(C0 + "/rows_DLL.pkl", "rb"))}


def entries_of(mid, C, T, root_labels):
    out = []
    for lab in root_labels:
        # 'rec6 C[5]' or 'mapevent0 B[1]'
        slot = lab.split()[-1]
        k, i = slot[0], int(slot[2:-1])
        out.append((lab, T[k][i]))
    return out


def flow(C, start, site):
    """forward data flow of the NoObstacleSlide bit set by 0x46 / cleared by 0x45 in THIS program (None = inherited)."""
    rets = set()
    states = {start: {None}}
    work = [start]
    guard = 0
    while work and guard < 200000:
        guard += 1
        pc = work.pop()
        st = states[pc]
        op = C[pc]
        if op == 0x46:
            out = {1}
        elif op == 0x45:
            out = {0}
        else:
            out = set(st)
        for n in succ(C, pc, start, rets, SZ_BIN):
            if n < 0 or n >= len(C):
                continue
            old = states.get(n)
            if old is None or not out <= old:
                states[n] = (old or set()) | out
                work.append(n)
    return states.get(site)


def main():
    keys = [k for k, r in tab.items() if r["cls"] in ("blocked in DLL only", "blocked in DLL only (some starts)",
                                                       "blocked in original only", "blocked in original only (some starts)")
            and r["mech"] == "rule"]
    for mid, pc in sorted(keys):
        folder, evf = M[mid]
        ev = json.load(open(evf, encoding="utf-8"))
        C = ev["Codes"]
        T = {k: ev["EventCodes%sTable" % k] for k in "ABCDEF"}
        r = rows[(mid, pc)]
        res = []
        for lab, st in entries_of(mid, C, T, r["roots"]):
            res.append((lab, sorted(flow(C, st, pc) or [], key=str)))
        print("%4d @%-5d %-38s %-28s %s" % (mid, pc, tab[(mid, pc)]["cls"], tab[(mid, pc)]["actor"][:28], res))


if __name__ == "__main__":
    main()
