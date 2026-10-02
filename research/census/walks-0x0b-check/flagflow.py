"""flagflow.py - which walk sites run after a flag op that changes the walker's collision behaviour, on the logic entity, in the
same program: 0x62/0x63 (search 0x80 = the logic entity; [DLL] ported with ResyncControllerFromFlags, so they change the DLL
mask too), 0x28-0x2B (class bits; [DLL] skipped), 0x16/0x17 (gravity), 0x45/0x46 (NoObstacleSlide). The census (c0b.py) models
only 0x28-0x2B (runs ORIG/FIXC, CLASSOPS=1). Then re-run the census states of those sites with every reachable flag
combination (mysim) and report the sites whose verdict depends on it.

Forward data flow per root: state = (ClassA, ClassB, Gravity, NoSlide, logic_switched) with values None (header) / 0 / 1."""
import collections
import csv
import json
import os
import pickle
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
C0 = os.path.join(os.path.dirname(HERE), "census0b")
sys.path.insert(0, HERE)
from pop import maps, succ, SZ_BIN  # noqa: E402
import mysim as S  # noqa: E402
from compare import mapdata, pid_of, speed  # noqa: E402

M = maps()
BITS = ((0x0001, 0), (0x0008, 1), (0x0100, 2), (0x2000, 3))


def apply(st, op, v):
    st = list(st)
    if op in (0x42, 0x43):
        st[4] = 1
        return tuple(st)
    if st[4]:
        return tuple(st)            # logic entity switched: the census attributes later walks to another actor
    if op in (0x62, 0x63) and v[1] == 0x80:
        f = v[2] | (v[3] << 8)
        for bit, i in BITS:
            if f & bit:
                st[i] = 1 if op == 0x62 else 0
                if i < 3:
                    st[5 + i] = 1 if op == 0x62 else 0
    elif op in (0x16, 0x17):
        st[2] = st[7] = (1 if op == 0x16 else 0)
    elif op == 0x28:
        st[1] = 1
    elif op == 0x29:
        st[1] = 0
    elif op == 0x2A:
        st[0] = 1
    elif op == 0x2B:
        st[0] = 0
    elif op == 0x46:
        st[3] = 1
    elif op == 0x45:
        st[3] = 0
    return tuple(st)


def flow(C, start):
    rets = set()
    states = {start: {(None, None, None, None, 0, None, None, None)}}
    work = [start]
    n = 0
    while work and n < 500000:
        n += 1
        pc = work.pop()
        op = C[pc]
        v = list(C[pc:pc + 10]) + [0] * 10
        outs = {apply(s, op, v) for s in states[pc]}
        for q in succ(C, pc, start, rets, SZ_BIN):
            if q < 0 or q >= len(C):
                continue
            old = states.setdefault(q, set())
            if not outs <= old:
                old |= outs
                work.append(q)
    return states


def main():
    tab = {(int(r["map"]), int(r["pc"])): r for r in csv.DictReader(open(C0 + "/table.tsv", encoding="utf-8"), delimiter="\t")}
    runs = {t: {(r["map"], r["pc"]): r for r in pickle.load(open(C0 + "/rows_%s.pkl" % t, "rb"))} for t in ("DLL", "ORIG0", "ORIG")}
    flows = {}
    touched = []
    for (mid, pc), row in sorted(tab.items()):
        if row["reach"] != "reachable":
            continue
        folder, evf = M[mid]
        if mid not in flows:
            ev = json.load(open(evf, encoding="utf-8"))
            C = ev["Codes"]
            T = {k: ev["EventCodes%sTable" % k] for k in "ABCDEF"}
            flows[mid] = (C, T, {})
        C, T, cache = flows[mid]
        r = runs["DLL"][(mid, pc)]
        sts = set()
        for lab in r["roots"]:
            slot = lab.split()[-1]
            st = T[slot[0]][int(slot[2:-1])]
            if st not in cache:
                cache[st] = flow(C, st)
            sts |= cache[st].get(pc, set())
        rel = {s for s in sts if not s[4] and any(x is not None for x in s[:4] + s[5:])}
        if rel:
            touched.append(((mid, pc), sorted(rel, key=str)))
    print("reachable sites with a flag op on the logic entity before them in the same program:", len(touched))
    kinds = collections.Counter()
    for k, rel in touched:
        for s in rel:
            for i, name in enumerate(("ClassA", "ClassB", "Gravity", "NoSlide")):
                if s[i] is not None:
                    kinds[name] += 1
    print("  by bit (state count):", dict(kinds))
    # re-run the census states with each flag combination
    changed = []
    for (mid, pc), rel in touched:
        row = tab[(mid, pc)]
        for t, rule in (("DLL", "DLL"), ("ORIG0", "ORIG")):
            rr = runs[t][(mid, pc)]
            base = collections.Counter()
            alt = collections.Counter()
            for cls, det in rr.get("all") or []:
                if det.get("pin") is None or det.get("dir") is None or det.get("key") is None:
                    continue
                F, recs = mapdata(mid)
                pid = pid_of(mid, det["key"])
                h = S.SPR.get(pid)
                if F is None or h is None or not speed(pid, det.get("anim")):
                    continue
                X0, Y0 = int(round(det["pin"][0] * S.U)), int(round(det["pin"][1] * S.U))
                for s in rel:
                    fl = S.flags_of(h)
                    if rule == "DLL":
                        for (bit, i) in BITS[:3]:
                            if s[5 + i] is not None:
                                fl = (fl | bit) if s[5 + i] else (fl & ~bit)
                    else:
                        for (bit, i) in BITS:
                            if s[i] is not None:
                                fl = (fl | bit) if s[i] else (fl & ~bit)
                    if rule == "DLL" and s[1] is None and s[0] is None and s[2] is None:
                        pass
                    if rule == "DLL":
                        # the DLL ports 0x62/0x63 but skips 0x28-0x2B: only 0x62/0x63 / 0x16/0x17 change its flags; approximate
                        # by applying every bit (the 0x28-0x2B part is reported apart below)
                        k_, *_ = S.walk_dll(F, S.Body(h, flags=fl), X0, Y0, det["dir"], det["rad"])
                    else:
                        k_, *_ = S.walk_orig(F, S.Body(h, flags=fl), X0, Y0, det["dir"], det["rad"])
                    base[cls] += 1
                    alt[{"pass": "pass", "blocked": "blocked", "left": "pass", "zero": "zero"}[k_]] += 1
            if base and alt and (alt.get("blocked", 0) != base.get("blocked", 0)):
                changed.append(((mid, pc), t, row["cls"], row["actor"][:30], rel, dict(base), dict(alt)))
    print("sites whose census verdict changes once the flag ops are applied:", len({c[0] for c in changed}))
    for c in changed:
        print("  %4d @%-5d %-6s %-40s %-30s %s census %s -> with flags %s" % (c[0][0], c[0][1], c[1], c[2], c[3], c[4], c[5], c[6]))


if __name__ == "__main__":
    main()
