"""Global scan of 0x41 (SpriteProgramIndexes[slot] = code) and 0x40 [slot, 0] in ALL programs of ALL maps (A..F tables, B map events), not only the
programs of records. Prints (slot, code) -> maps. Read-only. python -B scan41.py"""
import collections
import pickle
import sys

import common
import census as C

sys.stdout.reconfigure(encoding="utf-8")
data = common.load_cache()
maps = data["maps"]
sizes = common.opcode_sizes()
L = "ABCDEF"
swaps = collections.defaultdict(lambda: collections.defaultdict(set))   # (slot,code) -> map -> {(table,prog)}
for mid, m in maps.items():
    ends = C.program_extents(m["tables"], len(m["codes"]))
    refF = {(e["EventCodesF_InteractIndex"] or 0) & 0x7F for e in m["entities"] if e["IsEnabled"]}
    for letter in L:
        for number, start in enumerate(m["tables"][letter]):
            if letter == "F" and number not in refF:
                continue
            if not (0 < start < len(m["codes"])):
                continue
            if start not in ends:
                nxt = [x for x in ends if x > start]
                ends[start] = min(nxt) if nxt else len(m["codes"])
            ops, stopped = C.decode(m["codes"], start, ends[start], sizes)
            for pc, op, raw in ops:
                if op == 0x41 and len(raw) == 3 and raw[1] < 6:
                    swaps[(raw[1], raw[2])][mid].add((letter, number))
res = {}
for (s, c), mm in sorted(swaps.items()):
    res[(s, c)] = {m: sorted(v) for m, v in mm.items()}
    print(f"{L[s]}{c}: maps {sorted(mm)[:12]}{'...' if len(mm) > 12 else ''} ({sum(len(v) for v in mm.values())} program sites)")
with open("cache/swaps_global.pkl", "wb") as fh:
    pickle.dump(res, fh)
