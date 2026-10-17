"""census_z.py - read-only census of the entities that MOVE IN Z by script (candidate carriers) and of the waits / 0x3E sites, over the exported corpus.

Method (own code; the control-flow successor function is the one of research/census/waits-0x24/census_exec.py, re-typed here):
  roots   : B entry of every map event (owner = hero) ; for every record the A, C, D, E, F slot entries (table[idx & 0x7F]) -> 'primary' when the index has bit 0x80,
            'fallback' (DLL-only) otherwise.
  walk    : DFS over states (pc, logic) with logic in {hero, ('R', n), None}: 0x42 -> hero, 0x43 [n] -> ('R', n) (n < 0x80), 0x43 [0x80] unchanged, 0x43 [0x81] -> hero,
            a record root starts with logic = its own record.  All branches open.  Sites of interest are recorded with the logic entity that is current there.
  sites   : 0x1B (Fly, ForceZ = sext16 << 8), 0x20 0x21 0x22 0x23 (Z waits), 0x3E (is player riding entity), 0x64 / 0x65 (set / move position), 0x16 / 0x17 (flag 0x100 set / clear),
            0x63 (clear flags), 0x2D (activate entity).
Output  : census_z.json (per map, per site) ; summary on stdout.  Writes only into its own folder.
"""
import sys, os, json, glob, re, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "h2tools"))
import m
from m import Map, SZ, NM, s16, ROOT

sys.stdout.reconfigure(encoding="utf-8")
SPR = json.load(open(ROOT + "/alundra-project/Data/sprite-records.json", encoding="utf-8-sig"))
HERO = "H"
SLOTS = (("A", "EventCodesA_LoadIndex"), ("C", "EventCodesC_TickIndex"), ("D", "EventCodesD_TouchIndex"),
         ("E", "EventCodesE_DeactivateIndex"), ("F", "EventCodesF_InteractIndex"))
INTEREST = {0x1B, 0x20, 0x21, 0x22, 0x23, 0x3E, 0x64, 0x65, 0x16, 0x17, 0x63, 0x2D, 0x25, 0x26}


def all_maps():
    out = []
    for f in glob.glob(m.MAPS + "/*/*/events/*.events.json"):
        base = os.path.basename(os.path.dirname(os.path.dirname(f)))
        mm = re.search(r"-(\d+)$", base)
        if mm:
            out.append(int(mm.group(1)))
    return sorted(set(out))


def succ(C, pc, start, calls):
    op = C[pc]
    sz = SZ.get(op) or 1
    v = C[pc:pc + 10] + [0] * 10
    if op == 0xFF:
        return []
    if op == 0x02:
        return [pc + s16(v[1], v[2])]
    if op in (0x03, 0x04):
        return [pc + s16(v[1], v[2]), pc + 3]
    if op in (0x30, 0x31, 0x7B, 0x7C):
        return [pc + s16(v[3], v[4]), pc + 5]
    if op == 0x78:
        calls.add(pc + 3)
        return [pc + s16(v[1], v[2])]
    if op in (0x79, 0x7A):
        calls.add(pc + 3)
        return [pc + s16(v[1], v[2]), pc + 3]
    if op == 0x74:
        return [pc + s16(v[1], v[2]), pc + 3]
    if op == 0x49:
        return [start]
    if op in (0x4A, 0x4B):
        return [start, pc + 1]
    if op in (0x57, 0x58):
        return [pc + s16(v[1], v[2]), pc + s16(v[3], v[4]), pc + s16(v[5], v[6]), pc + s16(v[7], v[8])]
    if op == 0x7D:
        return list(calls)
    return [pc + sz]


def walk(C, start, logic0):
    """returns {pc: set(logic)} for the instructions reached, with the logic entity current when each is reached"""
    calls = set()
    seen = {}
    for _ in range(8):                   # fixpoint on the call return set
        before = len(calls)
        seen = collections.defaultdict(set)
        work = [(start, logic0)]
        while work:
            pc, lg = work.pop()
            if pc < 0 or pc >= len(C) or lg in seen[pc]:
                continue
            seen[pc].add(lg)
            op = C[pc]
            nlg = lg
            if op == 0x42:
                nlg = HERO
            elif op == 0x43:
                a = C[pc + 1] if pc + 1 < len(C) else 0x80
                if a == 0x80:
                    nlg = lg
                elif a == 0x81:
                    nlg = HERO
                elif a < 0x80:
                    nlg = ("R", a)
                else:
                    nlg = None
            for s in succ(C, pc, start, calls):
                work.append((s, nlg))
        if len(calls) == before:
            break
    return seen


def prefab_info(r):
    pid = r.get("PrefabAssetId")
    h = SPR.get(pid) if pid else None
    if not h:
        return None
    fl = h["MoreFlags"] | (h["CanPickup"] << 8) | (h["FlagsPortraitShadowType"] << 16)
    return dict(pid=pid, flags=fl, size=(h["SizeX"], h["SizeY"], h["SizeZ"]), off=(h["OffsetX"], h["OffsetY"], h["OffsetZ"]))


def main():
    out = {}
    tot = collections.Counter()
    for mid in all_maps():
        mp = Map(mid)
        C = mp.C
        boundary = sum(2 * len(t) for t in mp.T.values())
        # sites by linear decode (population)
        pop = collections.defaultdict(list)
        pc = boundary
        while pc < len(C):
            op = C[pc]
            if op in INTEREST:
                pop[op].append(pc)
            pc += SZ.get(op) or 1
        if not any(op in pop for op in (0x1B, 0x20, 0x21, 0x22, 0x23, 0x3E, 0x64, 0x65)):
            continue
        recs = {int(r["Index"]): r for r in mp.records}
        roots = []
        for e in mp.mapevents:
            idx = int(e["EventCodesBIndex"])
            t = mp.T["B"]
            if (idx & 0x7F) and (idx & 0x7F) < len(t) and t[idx & 0x7F]:
                roots.append(("map B[%d]" % (idx & 0x7F), t[idx & 0x7F], HERO, "primary" if idx & 0x80 else "fallback", None))
        for r in mp.records:
            for slot, key in SLOTS:
                idx = int(r.get(key, 0))
                t = mp.T[slot]
                if (idx & 0x7F) and (idx & 0x7F) < len(t) and t[idx & 0x7F]:
                    roots.append(("rec%s %s[%d]" % (r["Index"], slot, idx & 0x7F), t[idx & 0x7F], ("R", int(r["Index"])), "primary" if idx & 0x80 else "fallback", int(r["Index"])))
        sites = []   # (op, pc, root label, kind, logic, operands)
        for label, start, lg0, kind, own in roots:
            seen = walk(C, start, lg0)
            for pcx, lgs in seen.items():
                op = C[pcx]
                if op in INTEREST:
                    for lg in lgs:
                        sites.append(dict(op=op, pc=pcx, root=label, kind=kind, logic=lg if lg in (HERO, None) else list(lg),
                                          arg=C[pcx + 1:pcx + (SZ.get(op) or 1)]))
        # dedupe
        uniq = {}
        for s in sites:
            k = (s["op"], s["pc"], json.dumps(s["logic"]), s["kind"])
            uniq.setdefault(k, s)
        sites = list(uniq.values())
        entry = dict(name=mp.d["name"], zone=mp.d["zone"], pop={hex(k): len(v) for k, v in pop.items()}, sites=sites)
        # records referenced as logic of a 0x1B
        tgt = collections.defaultdict(list)
        for s in sites:
            if s["op"] == 0x1B and isinstance(s["logic"], list):
                a = s["arg"]
                f = a[0] | (a[1] << 8)
                f = f - 65536 if f >= 32768 else f
                tgt[s["logic"][1]].append(dict(pc=s["pc"], force=f, root=s["root"], kind=s["kind"]))
        entry["fly_targets"] = {}
        for n, lst in tgt.items():
            r = recs.get(n)
            pi = prefab_info(r) if r else None
            entry["fly_targets"][str(n)] = dict(name=(r or {}).get("EntityName"), prefab=pi, sites=lst)
        out[str(mid)] = entry
    json.dump(out, open(os.path.join(HERE, "census_z.json"), "w", encoding="utf-8"), indent=1)
    print("maps with any site:", len(out))


if __name__ == "__main__":
    main()
