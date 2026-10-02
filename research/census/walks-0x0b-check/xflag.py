"""xflag.py - cross-program flag writes on a walker: for every reachable 0x62/0x63 (and 0x28-0x2B, 0x16/0x17, 0x45/0x46) in the
map, which record or the hero does it touch (logic entity of its program = owner, or the hero for B programs / after 0x42;
0x62/0x63 search v1 < 0x80 = a record id, 0x81 = the hero, 0x82 = everyone)? Report, for the difference-class and
'blocked in both' rows, the mask / gravity / slide bits some OTHER program (or an earlier one) can change on the walker, then
re-run the census states with every such combination (mysim) and report the sites whose verdict can change."""
import collections
import csv
import itertools
import json
import os
import pickle
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
C0 = os.path.join(os.path.dirname(HERE), "census0b")
sys.path.insert(0, HERE)
from pop import maps, descend, SZ_BIN  # noqa: E402
import mysim as S  # noqa: E402
from compare import mapdata, pid_of, speed  # noqa: E402

M = maps()
SLOTS = (("A", "EventCodesA_LoadIndex"), ("C", "EventCodesC_TickIndex"), ("D", "EventCodesD_TouchIndex"),
         ("E", "EventCodesE_DeactivateIndex"), ("F", "EventCodesF_InteractIndex"))
BITS = {0x0001: "A", 0x0008: "B", 0x0100: "G", 0x2000: "N"}


def writes(mid):
    folder, evf = M[mid]
    ev = json.load(open(evf, encoding="utf-8"))
    C = ev["Codes"]
    T = {k: ev["EventCodes%sTable" % k] for k in "ABCDEF"}
    F, recs = mapdata(mid)
    roots = []
    for r in recs.values():
        for slot, key in SLOTS:
            idx = int(r.get(key, 0)) & 0x7F
            if idx and idx < len(T[slot]) and T[slot][idx]:
                roots.append((("R", int(r["Index"])), T[slot][idx]))
    import glob
    tm = json.load(open(glob.glob(folder + "/tilemap/*.tileMap")[0], encoding="utf-8"))
    for o in [l for l in tm.get("object_layers", []) if l["name"] == "MapEvents"][0]["objects"] if any(l["name"] == "MapEvents" for l in tm.get("object_layers", [])) else []:
        e = o["custom_properties"]
        idx = int(e["EventCodesBIndex"]) & 0x7F
        if idx and idx < len(T["B"]) and T["B"][idx]:
            roots.append(("H", T["B"][idx]))
    out = collections.defaultdict(set)   # target -> {(bit, value, dll_ported, pc)}
    for owner, st in roots:
        for pc in descend(C, st, SZ_BIN):
            op = C[pc]
            v = list(C[pc:pc + 6]) + [0] * 6
            tgts = []
            if op in (0x62, 0x63):
                if v[1] == 0x80:
                    tgts = [owner, "?logic"]
                elif v[1] == 0x81:
                    tgts = ["H"]
                elif v[1] == 0x82:
                    tgts = ["*"]
                elif v[1] < 0x80:
                    tgts = [("R", v[1])]
                f = v[2] | (v[3] << 8)
                for bit, name in BITS.items():
                    if f & bit:
                        for t in tgts:
                            out[t].add((name, 1 if op == 0x62 else 0, name != "N", pc))
            elif op in (0x28, 0x29, 0x2A, 0x2B):
                name = "B" if op in (0x28, 0x29) else "A"
                val = 1 if op in (0x28, 0x2A) else 0
                for t in (owner, "?logic"):
                    out[t].add((name, val, False, pc))
            elif op in (0x16, 0x17):
                for t in (owner, "?logic"):
                    out[t].add(("G", 1 if op == 0x16 else 0, True, pc))
            elif op in (0x45, 0x46):
                for t in (owner, "?logic"):
                    out[t].add(("N", 1 if op == 0x46 else 0, False, pc))
    return out


def main():
    tab = {(int(r["map"]), int(r["pc"])): r for r in csv.DictReader(open(C0 + "/table.tsv", encoding="utf-8"), delimiter="\t")}
    runs = {t: {(r["map"], r["pc"]): r for r in pickle.load(open(C0 + "/rows_%s.pkl" % t, "rb"))} for t in ("DLL", "ORIG")}
    W = {}
    hits = []
    for (mid, pc), row in sorted(tab.items()):
        if row["cls"] not in ("blocked in both", "blocked in DLL only", "blocked in DLL only (some starts)", "blocked in original only",
                              "blocked in original only (some starts)", "same outcome, start-dependent"):
            continue
        if mid not in W:
            W[mid] = writes(mid)
        res = {}
        for t, rule in (("DLL", "DLL"), ("ORIG", "ORIG")):
            rr = runs[t][(mid, pc)]
            base = collections.Counter()
            best = collections.Counter()
            for cls, det in rr.get("all") or []:
                if det.get("pin") is None or det.get("dir") is None or det.get("key") is None:
                    continue
                key = det["key"]
                ws = W[mid].get(key if key == "H" else tuple(key), set()) | W[mid].get("*", set())
                if rule == "DLL":
                    ws = {w for w in ws if w[2]}
                F, recs = mapdata(mid)
                pid = pid_of(mid, key)
                h = S.SPR.get(pid)
                if F is None or h is None or not speed(pid, det.get("anim")):
                    continue
                X0, Y0 = int(round(det["pin"][0] * S.U)), int(round(det["pin"][1] * S.U))
                names = sorted({w[0] for w in ws})
                b0 = S.Body(h)
                k0 = (S.walk_dll if rule == "DLL" else S.walk_orig)(F, b0, X0, Y0, det["dir"], det["rad"])[0]
                base["pass" if k0 in ("pass", "left") else "blocked"] += 1
                passed = False
                choices = [sorted({(w[0], w[1]) for w in ws if w[0] == n}) + [(n, None)] for n in names]
                for combo in itertools.product(*choices) if choices else [()]:
                    fl = S.flags_of(h)
                    for n, val in combo:
                        if val is None:
                            continue
                        bit = {"A": 1, "B": 8, "G": 0x100, "N": 0x2000}[n]
                        fl = (fl | bit) if val else (fl & ~bit)
                    b = S.Body(h, flags=fl)
                    k_ = (S.walk_dll if rule == "DLL" else S.walk_orig)(F, b, X0, Y0, det["dir"], det["rad"])[0]
                    if k_ in ("pass", "left"):
                        passed = True
                        break
                best["pass" if passed else "blocked"] += 1
            res[t] = (dict(base), dict(best))
        flip = any(v[0].get("blocked", 0) > v[1].get("blocked", 0) for v in res.values())
        if flip:
            hits.append(((mid, pc), row["cls"], row["actor"][:28], res))
    print("rows whose blocked states can pass with a flag write made by some program of the map on the walker:", len(hits))
    for k, cls, actor, res in hits:
        print("  %4d @%-5d %-40s %-28s DLL %s -> %s | ORIG %s -> %s" % (k[0], k[1], cls, actor, res["DLL"][0], res["DLL"][1],
                                                                        res["ORIG"][0], res["ORIG"][1]))


if __name__ == "__main__":
    main()
