"""Static flags of the owners (plates) and candidates (riders) of the 0x60/0x61 raw-id sites of sm1.
Flags = MoreFlags | CanPickup << 8 | FlagsPortraitShadowType << 16 (WorldProxy.cs:1992, EntitySpawnFactory.cs:740), from
alundra-project/Data/sprite-records.json keyed by the record's PrefabAssetId. Owner = record whose program index of the slot
has bit 0x80 and (index & 0x7F) == program. Candidate = record whose Index == raw id (EntityRefId)."""
import csv, json, glob, re, collections
REPO = "D:/development/repo/alundra-casaengine-project-converter"
SR = json.load(open(REPO + "/alundra-project/Data/sprite-records.json", encoding="utf-8"))
rows = list(csv.DictReader(open(REPO + "/docs/plan-e19-sm-annexe/skipped-sites.tsv", encoding="utf-8"), delimiter="\t"))
SLOTKEY = {"A": "EventCodesA_LoadIndex", "B": "EventCodesB_MapIndex", "C": "EventCodesC_TickIndex", "D": "EventCodesD_TouchIndex",
           "E": "EventCodesE_DeactivateIndex", "F": "EventCodesF_InteractIndex"}

def records(m):
    p = [x for x in glob.glob(REPO + "/alundra-project/Maps/*/*/tilemap/*.tileMap") if re.search(r"-%d\.tileMap$" % m, x)][0]
    d = json.load(open(p, encoding="utf-8-sig"))
    out = []
    for L in d["object_layers"]:
        if L.get("name") != "Entities":
            continue
        for o in L["objects"]:
            cp = o.get("custom_properties") or {}
            out.append(cp)
    return out

def flags(cp):
    r = SR.get(cp.get("PrefabAssetId"))
    if r is None:
        return None
    return r["MoreFlags"] | (r["CanPickup"] << 8) | (r["FlagsPortraitShadowType"] << 16)

summary = collections.Counter()
seen = set()
for r in rows:
    op = r["opcode"].upper()
    if op not in ("60", "61"):
        continue
    t = int(r["operands"], 16)
    m, slot, prog = int(r["map"]), r["slot"], int(r["program"])
    if t & 0x80:
        summary[("group", op, r["operands"])] += 1
        continue
    recs = records(m)
    owners = [cp for cp in recs if int(cp.get(SLOTKEY[slot], "0")) & 0x80 and (int(cp.get(SLOTKEY[slot], "0")) & 0x7F) == prog]
    cands = [cp for cp in recs if int(cp.get("Index", "-9")) == t]
    of = [flags(c) for c in owners]
    cf = [flags(c) for c in cands]
    own_ok = bool(owners) and all(f is not None and f & 0x80 for f in of)
    if op == "60":
        cand_ok = bool(cands) and all(f is not None and (f & 0x4100) == 0x100 and f & 0x80 for f in cf)
    else:
        cand_ok = bool(cands) and all(f is not None and f & 0x80 for f in cf)
        own_ok = bool(owners) and all(f is not None and (f & 0x4100) == 0x100 and f & 0x80 for f in of) if slot != "B" else None
    key = (m, slot, prog, t)
    summary[(op, "owner_ok" if own_ok else ("owner_n/a" if own_ok is None else "owner_FAIL"), "cand_ok" if cand_ok else "cand_FAIL")] += 1
    if key not in seen:
        seen.add(key)
        if not (own_ok in (True, None) and cand_ok):
            print("CHECK", op, key, "owners", [(c.get("Index"), c.get("EntityName"), hex(f) if f is not None else None) for c, f in zip(owners, of)],
                  "cands", [(c.get("Index"), c.get("EntityName"), hex(f) if f is not None else None) for c, f in zip(cands, cf)])
for k, v in sorted(summary.items(), key=str):
    print(v, k)
