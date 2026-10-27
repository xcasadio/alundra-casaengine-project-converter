"""vec_corrected.py - E20.h4b1 T0 (c): the vectors of the CORRECTED mode (inclusive bound + sort from 2 results) on the worlds of the 130 query vectors
(the 120 of bin/vec/vec_query_8003AF70.json and the 10 of critic/vec_extra/vec_extra_query.json, the sentinel vector among them).
Oracle = c3_query.query_model (the critic's model, 0 mismatch against the real code in binary mode over 4000 worlds), variant "corrected" (copy of c3_query.py).
Rule named "an absent byte is 0" (for THIS oracle only: the real code never reads slot == count): the memory is the sparse "reads" of the vector, every byte
not listed reads 0.  The result is the list of SLOTS (pointer - 0x80127D30) / 0x294, in order (D-H4B1-3).
Usage: python -B vec_corrected.py <annex-root-of-e20-h4b> <out.json>"""
import json, sys, os
sys.dont_write_bytecode = True
from c3_query import query_model, HERO, STR, OUTP, M

A = sys.argv[1]
OUT = sys.argv[2]
files = [("vec_query_8003AF70.json", os.path.join(A, "bin", "vec", "vec_query_8003AF70.json")),
         ("vec_extra_query.json", os.path.join(A, "critic", "vec_extra", "vec_extra_query.json"))]
VARIANTS = [None, "inclusive", "sort_from2", "corrected"]


def slots_of(ptrs):
    out = []
    for p in ptrs:
        assert (p - HERO) % STR == 0
        out.append((p - HERO) // STR)
    return out


res = []
bad_binary = 0
for fname, path in files:
    for idx, v in enumerate(json.load(open(path))["vectors"]):
        ram = {}
        for a, n, val in v["reads"]:
            for i in range(n):
                ram[a + i] = (val >> (8 * i)) & 0xFF
        g = lambda a, n: sum(ram.get(a + i, 0) << (8 * i) for i in range(n))
        caller, mask, atk = v["args"][0], v["args"][1], v["args"][2]
        count = g(0x80134360, 4)
        # the real result: final value of the pointer array written at OUTP
        wr = {(a, n): val for a, n, val in v["writes"]}
        real = slots_of([wr[(OUTP + 4 * k, 4)] for k in range(v["ret"])])
        out = {}
        for var in VARIANTS:
            n, P, K = query_model(g, caller, mask, atk, count, var)
            out[var or "binary"] = slots_of(P)
        if out["binary"] != real:
            bad_binary += 1
        rec = {"file": fname, "id": v.get("id", idx), "case": v.get("case"), "caller_slot": (caller - HERO) // STR, "mask": mask, "type": atk, "count": count,
               "binary": real, "inclusive_only": out["inclusive"], "sort2_only": out["sort_from2"], "corrected": out["corrected"]}
        res.append(rec)
json.dump({"note": "E20.h4b1 T0 (c): corrected-mode oracle results, lists of slots in order; 'binary' is the real code's result (the vector's writes), checked equal to the critic model; "
                   "rule: an absent byte is 0 (this oracle only)", "vectors": res}, open(OUT, "w"), indent=1)
bound = [r for r in res if r["inclusive_only"] != r["binary"]]
thr = [r for r in res if r["sort2_only"] != r["binary"]]
both = [r for r in res if r["corrected"] != r["binary"]]
print("vectors", len(res), "binary model != real code:", bad_binary)
print("bound distinguishes (inclusive_only != binary):", [(r["file"][:9], r["id"]) for r in bound])
print("threshold distinguishes (sort2_only != binary):", [(r["file"][:9], r["id"]) for r in thr])
print("corrected != binary:", len(both))
print("n==2 vectors:", [(r["file"][:9], r["id"]) for r in res if len(r["binary"]) == 2], "of which threshold-distinguishing:", len(thr))
print("corrected != inclusive_only+sort2 composition?", [(r["file"][:9], r["id"]) for r in res if r["corrected"] != r["inclusive_only"] and r["corrected"] != r["sort2_only"] and r["corrected"] == r["binary"]])
