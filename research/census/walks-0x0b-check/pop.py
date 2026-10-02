"""pop.py - checker's own population of 0x0B / 0x1E sites (read-only).

Independent of c0b.py: own map enumeration, own linear decode, own recursive-descent decode with the FULL control-flow family
(including 0x7E/0x7F/0x80/0x81 returns to the remembered point, and 0x7B/0x7C remembering pc+5), own root set.
Sizes: the DLL table (EventOpcodeSizeTable.cs), except 0x5F = 8 (binary, plan 0.2.5); size 0 -> no successor in descent.
Outputs pop.pkl and prints the comparison with census0b/table.tsv.
"""
import collections
import csv
import glob
import json
import os
import pickle
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")
ROOT = "D:/development/repo/alundra-casaengine-project-converter"
HERE = os.path.dirname(os.path.abspath(__file__))
CENSUS = os.path.join(os.path.dirname(HERE), "census0b")

_tbl = open(ROOT + "/Alundra/Scripts/EventOpcodeSizeTable.cs", encoding="utf-8-sig").read()
SZ = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),', _tbl):
    SZ[int(m.group(1), 16)] = int(m.group(2))
SZ_BIN = dict(SZ)
SZ_BIN[0x5F] = 8


def s16(lo, hi):
    v = lo | (hi << 8)
    return v - 65536 if v >= 32768 else v


def maps():
    out = {}
    for f in glob.glob(ROOT + "/alundra-project/Maps/*/*/events/*.events.json"):
        f = f.replace("\\", "/")
        folder = os.path.dirname(os.path.dirname(f))
        mm = re.search(r"-(\d+)$", os.path.basename(folder))
        if mm:
            out[int(mm.group(1))] = (folder, f)
    return out


def linear(C, start, sizes):
    pcs = []
    pc = start
    while pc < len(C):
        pcs.append(pc)
        pc += sizes.get(C[pc]) or 1
    return pcs


def succ(C, pc, start, rets, sizes):
    op = C[pc]
    v = list(C[pc:pc + 10]) + [0] * 10
    sz = sizes.get(op)
    if op == 0xFF or op in (0x9D, 0xAE):
        return []
    if op == 0x00:
        return [pc + 1]
    if sz == 0:
        return []
    if op == 0x02:
        return [pc + s16(v[1], v[2])]
    if op in (0x03, 0x04, 0x74):
        return [pc + s16(v[1], v[2]), pc + 3]
    if op in (0x30, 0x31):
        return [pc + s16(v[3], v[4]), pc + 5]
    if op == 0x78:
        rets.add(pc + 3)
        return [pc + s16(v[1], v[2])]
    if op in (0x79, 0x7A):
        rets.add(pc + 3)
        return [pc + s16(v[1], v[2]), pc + 3]
    if op in (0x7B, 0x7C):
        rets.add(pc + 5)
        return [pc + s16(v[3], v[4]), pc + 5]
    if op == 0x7D:
        return list(rets)
    if op in (0x7E, 0x7F):
        return list(rets) + [pc + 1]
    if op in (0x80, 0x81):
        return list(rets) + [pc + 3]
    if op == 0x49:
        return [start]
    if op in (0x4A, 0x4B):
        return [start, pc + 1]
    if op in (0x57, 0x58):
        return [pc + s16(v[1], v[2]), pc + s16(v[3], v[4]), pc + s16(v[5], v[6]), pc + s16(v[7], v[8])]
    return [pc + sz]


def descend(C, start, sizes):
    rets = set()
    while True:
        n0 = len(rets)
        seen = set()
        work = [start]
        while work:
            pc = work.pop()
            if pc in seen or pc < 0 or pc >= len(C):
                continue
            seen.add(pc)
            work.extend(succ(C, pc, start, rets, sizes))
        if len(rets) == n0:
            return seen


SLOTS = (("A", "EventCodesA_LoadIndex"), ("C", "EventCodesC_TickIndex"), ("D", "EventCodesD_TouchIndex"),
         ("E", "EventCodesE_DeactivateIndex"), ("F", "EventCodesF_InteractIndex"))


def main():
    M = maps()
    res = {}
    agg = collections.Counter()
    for mid in sorted(M):
        folder, evf = M[mid]
        ev = json.load(open(evf, encoding="utf-8"))
        C = ev["Codes"]
        T = {k: ev["EventCodes%sTable" % k] for k in "ABCDEF"}
        boundary = sum(2 * len(t) for t in T.values())
        nz = [o for t in T.values() for o in t if o]
        minentry = min(nz) if nz else None
        lin = linear(C, boundary, SZ)
        lin_bin = linear(C, boundary, SZ_BIN)
        L = {pc for pc in lin if C[pc] in (0x0B, 0x1E)}
        Lb = {pc for pc in lin_bin if C[pc] in (0x0B, 0x1E)}
        # recursive descent from every table entry (all tables)
        allents = set(nz)
        R_all = set()
        for e in allents:
            R_all |= descend(C, e, SZ_BIN)
        # roots actually used by records / map events
        tm = json.load(open(glob.glob(folder + "/tilemap/*.tileMap")[0], encoding="utf-8"))
        layers = {l["name"]: l["objects"] for l in tm.get("object_layers", [])}
        recs = [o["custom_properties"] for o in layers.get("Entities", [])]
        mevs = [o["custom_properties"] for o in layers.get("MapEvents", [])]
        roots = {}
        for e in mevs:
            idx = int(e["EventCodesBIndex"]) & 0x7F
            if idx and idx < len(T["B"]) and T["B"][idx]:
                roots.setdefault(T["B"][idx], []).append("mapevent%s B[%d]" % (e["Index"], idx))
        for r in recs:
            for slot, key in SLOTS:
                raw = int(r.get(key, 0))
                idx = raw & 0x7F
                if idx and idx < len(T[slot]) and T[slot][idx]:
                    roots.setdefault(T[slot][idx], []).append("rec%s %s[%d]%s" % (r["Index"], slot, idx, "" if raw & 0x80 else " (no 0x80)"))
        R_root = set()
        R_root80 = set()
        for st, labs in roots.items():
            d = descend(C, st, SZ_BIN)
            R_root |= d
            if any("no 0x80" not in l for l in labs):
                R_root80 |= d
        sites_desc = {pc for pc in R_all if C[pc] in (0x0B, 0x1E)}
        res[mid] = dict(L=L, Lb=Lb, R_all=sites_desc, R_root={pc for pc in R_root if C[pc] in (0x0B, 0x1E)},
                        R_root80={pc for pc in R_root80 if C[pc] in (0x0B, 0x1E)}, boundary=boundary, minentry=minentry,
                        ops={pc: C[pc] for pc in L | sites_desc})
        agg["L"] += len(L)
        agg["Lb"] += len(Lb)
        agg["desc_all"] += len(sites_desc)
        agg["L-desc"] += len(L - sites_desc)
        agg["desc-L"] += len(sites_desc - L)
        agg["root"] += len(res[mid]["R_root"])
        agg["root80"] += len(res[mid]["R_root80"])
        if minentry is not None and minentry < boundary:
            agg["entry<boundary"] += 1
        if L != Lb:
            agg["maps L!=Lb"] += 1
    pickle.dump(res, open(HERE + "/pop.pkl", "wb"))
    print("maps", len(M))
    for k, v in sorted(agg.items()):
        print("%-16s %d" % (k, v))
    n0b = sum(1 for m in res.values() for pc in m["L"] if m["ops"][pc] == 0x0B)
    n1e = sum(1 for m in res.values() for pc in m["L"] if m["ops"][pc] == 0x1E)
    print("linear: 0x0B", n0b, "maps", sum(1 for m in res.values() if any(m["ops"][p] == 0x0B for p in m["L"])),
          " 0x1E", n1e, "maps", sum(1 for m in res.values() if any(m["ops"][p] == 0x1E for p in m["L"])))
    rr0b = sum(1 for m in res.values() for pc in m["R_root"] if m["ops"][pc] == 0x0B)
    rr1e = sum(1 for m in res.values() for pc in m["R_root"] if m["ops"][pc] == 0x1E)
    print("root-reachable: 0x0B", rr0b, " 0x1E", rr1e, " maps", sum(1 for m in res.values() if m["R_root"]))
    # differences
    for mid, m in sorted(res.items()):
        for pc in sorted(m["L"] - m["R_all"]):
            print("  linear-only (no table entry reaches it): map %d @%d op %02X" % (mid, pc, m["ops"][pc]))
        for pc in sorted(m["R_all"] - m["L"]):
            print("  descent-only (missed by the linear decode): map %d @%d op %02X" % (mid, pc, m["ops"][pc]))
    # compare with the census table
    tab = list(csv.DictReader(open(CENSUS + "/table.tsv", encoding="utf-8"), delimiter="\t"))
    cen = {(int(r["map"]), int(r["pc"])): r for r in tab}
    mine = {(mid, pc) for mid, m in res.items() for pc in m["L"]}
    print("census rows", len(cen), " mine", len(mine), " census-mine", sorted(set(cen) - mine)[:20], " mine-census", sorted(mine - set(cen))[:20])
    reach_c = collections.Counter(r["reach"] for r in tab)
    print("census reach column", dict(reach_c))
    myreach = {(mid, pc) for mid, m in res.items() for pc in m["R_root"]}
    a = {k for k, r in cen.items() if r["reach"] == "reachable"}
    print("census reachable", len(a), " my root-reachable", len(myreach), " census-only", sorted(a - myreach)[:40], " mine-only", sorted(myreach - a)[:40])


if __name__ == "__main__":
    main()
