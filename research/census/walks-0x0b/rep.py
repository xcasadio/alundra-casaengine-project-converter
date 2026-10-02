"""rep.py - compare the c0b.py runs site by site; write table.tsv, table.pkl and totals.txt.

Runs: DLL (port today); ORIG0 (binary rule, no entity); ORIG (binary rule + CERTAIN obstacles: S1 same-program static blocks,
S2 never-touched load-static records); FIX (DLL rule + the same certain obstacles = the recommended fix where presence is
certain); ORIGMAX / FIXMAX (the same with the over-approximate cloud of every place a Collidable record can stand: an
indicator of cross-program, timing-dependent entity contacts).

Per run the verdict of a site is the set of outcomes of the states that reach it:
  P every state passes; B every state blocked; M some pass, some blocked; Z zero speed; I indeterminate (alone or mixed);
  U:<cause> no state reaches it (walk = cut by a blocked walk upstream, flag = flag never set, entry, indeterminate).
Class (DLL against ORIG):
  passes in both / blocked in DLL only (mechanism 'entity' if ORIG0 is not P, else 'rule') / blocked in both /
  blocked in original only / downstream (DLL U:walk, ORIG reaches it) / not reached / uncertain.
Overrides: CERTIFIED (sites decided by the emulations of the collision surface, map 10 and its copy 331).
Flags: xp = ORIGMAX differs from ORIG (a cross-program obstacle can change the outcome: timing-dependent);
       cw = a DLL-blocked state stops on a cell that a reachable 0x54/0x55/0x85 rewrites; loop = every DLL-blocked state starts
       from a pin produced after the site (loop re-entry of the all-branches-open walk); detour = DLL detour assessment.
"""
import collections
import os
import pickle
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.stdout.reconfigure(encoding="utf-8")
RUNS = ("DLL", "FIX", "FIXC", "ORIG0", "ORIGnc", "ORIG", "FIXMAX", "ORIGMAX")
STORY = set(range(162, 190)) | {44, 10, 14, 15, 114, 115, 116, 117, 135, 136, 362}
# sites decided by an emulation of the binary loop (collision.md F10-F14, collision-check.md: map 10; 331 = same code,
# records and cells in the scene areas, see census0b.md F3)
CERTIFIED = {
    (10, 5172): ("blocked in DLL only", "entity", "Nestus 0x24 @5163 stops on Meade (cross-program) [emu]"),
    (10, 1215): ("blocked in DLL only", "entity", "hero 0x24 @1212 stops on rec41 spawned by Septimus C[23] [emu]"),
    (10, 3673): ("blocked in DLL only", "entity", "Septimus 0x24 @3670 stops on rec41 (same program) [emu]"),
    (10, 5071): ("blocked in DLL only", "entity", "Bergus 0x24 @5061 stops on Rumi (cross-program) [emu]"),
    (331, 5172): ("blocked in DLL only", "entity", "copy of map 10 @5172 [emu on 10 + data identity]"),
    (331, 1215): ("blocked in DLL only", "entity", "copy of map 10 @1215 [emu on 10 + data identity]"),
    (331, 3673): ("blocked in DLL only", "entity", "copy of map 10 @3673 [emu on 10 + data identity]"),
    (331, 5071): ("blocked in DLL only", "entity", "copy of map 10 @5071 [emu on 10 + data identity]"),
    (185, 506): ("blocked in DLL only", "entity", "Septimus 0x24 @458 stops on rec7 (same program) at 330.0, @463 at 182.0, @506 ends at 342.5 [emu scene185.py]"),
    (10, 2456): ("blocked in DLL only", "rule", "scripted jump (anim 2 + IsZForceApplied, 0x25): the hero crosses the 16 px ledge in the binary [emu jump/jsim.py]"),
    (10, 6394): ("blocked in DLL only", "rule", "scripted jump of Giles (anim 3 + IsZForceApplied, 0x25) [emu jump/jsim.py]"),
    (331, 6394): ("blocked in DLL only", "rule", "copy of map 10 @6394 (scripted jump) [emu on 10 + data identity]"),
}


def jump_near(mid, pc, back=8):
    """a 0x25 (wait landing) or an animation with IsZForceApplied set by 0x1A/0x59/0x5B in the 'back' instructions before pc
    (linear decode): the original may jump over a ledge that the census cannot cross."""
    import sys as _s
    _s.path.insert(0, HERE)
    from census_exec_orig import Map, SZ
    mp = _MAPS.setdefault(mid, Map(mid))
    if not hasattr(mp, "_lin"):
        lin = []
        q = sum(2 * len(t) for t in mp.T.values())
        while q < len(mp.C):
            lin.append(q)
            q += SZ.get(mp.C[q]) or 1
        mp._lin = lin
        mp._pos = {x: i for i, x in enumerate(lin)}
    i = mp._pos.get(pc)
    if i is None:
        return False
    for q in mp._lin[max(0, i - back):i]:
        if mp.C[q] == 0x25:
            return True
    return False


_MAPS = {}


class BPin(tuple):
    pass


def verdict(r):
    if r is None:
        return "-"
    if r["reach"] == "dormant":
        return "dormant"
    if not r.get("reached"):
        return "U:" + r["unreached"][0]
    cs = set(r["classes_seen"])
    if cs == {"pass"}:
        return "P"
    if cs == {"blocked"}:
        return "B"
    if cs == {"pass", "blocked"}:
        return "M"
    if cs == {"zero"}:
        return "Z"
    if "indet" in cs:
        return "I" + ("+" + "".join(sorted(c[0].upper() for c in cs - {"indet"})) if len(cs) > 1 else "")
    return "+".join(sorted(cs))


def states(r):
    out = {}
    for c, d in (r or {}).get("all", []):
        k = (repr(d.get("key")), d.get("anim"), d.get("dir"), d.get("pin"), d.get("rad"), d.get("root"))
        out.setdefault(k, set()).add(c)
    return out


def per_state(rd, ro, r0=None, rn=None):
    """same start state in both runs: 'same' when no start blocks in one and not the other; else 'dll>' / 'orig>' / 'both',
    with the mechanism read per state from ORIG0 (binary rule without entities): 'rule' when ORIG0 already differs from the
    DLL for those states, else 'entity'."""
    a, b = states(rd), states(ro)
    z = states(r0)
    common = set(a) & set(b)
    dll_only = [s for s in common if "blocked" in a[s] and "blocked" not in b[s]]
    orig_only = [s for s in common if "blocked" in b[s] and "blocked" not in a[s]]
    diff = dll_only + orig_only
    rule = any(s in z and z[s] != a[s] for s in diff)
    zn = states(rn)
    cls_op = (not rule) and rn is not None and all(s in zn and zn[s] == a[s] for s in diff)
    mech = "rule" if rule else ("class-op" if cls_op else "entity")
    if not dll_only and not orig_only:
        return "same", ""
    if dll_only and not orig_only:
        return "dll>", mech
    if orig_only and not dll_only:
        return "orig>", mech
    return "both", mech


def klass(v, rd=None, ro=None, r0=None, rn=None):
    d, o0, o = v["DLL"], v["ORIG0"], v["ORIG"]
    if d == "B" and o == "M":
        # every DLL start blocks; the binary rule reaches the site with starts of its own (upstream walks stopped elsewhere,
        # mostly by an entity) and some of them pass: the defect pattern, partially
        return "blocked in DLL only (some starts)", ("rule" if o0 != "B" else ("class-op" if v.get("ORIGnc") == "B" else "entity"))
    if d == "M" and o == "B":
        return "blocked in original only (some starts)", ("entity" if o0 == "M" else "rule")
    if d in ("M", "B", "I+BP") and o in ("M", "B", "I+BP") and not (d == "B" and o == "B"):
        ps, mech = per_state(rd, ro, r0, rn)
        if ps == "same":
            return "same outcome, start-dependent", "%s/%s" % (d, o)
        if ps == "dll>":
            return "blocked in DLL only (some starts)", mech
        if ps == "orig>":
            return "blocked in original only (some starts)", mech
        return "uncertain", "per-state both ways %s/%s" % (d, o)
    if d == "U:flag" and not o.startswith("U"):
        return "downstream", "flag set only under the original rule; ORIG %s" % o
    if d == "dormant":
        return "dormant", ""
    if d == "P" and o == "P":
        return "passes in both", ""
    if d in ("B", "M") and o == "P":
        if o0 == "P":
            return "blocked in DLL only", "rule"
        if v.get("ORIGnc", "P") != "P":
            return "blocked in DLL only", ("class-op" if v.get("ORIGnc") == d else "class-op+entity")
        return "blocked in DLL only", "entity"
    if d == "B" and o == "B":
        return "blocked in both", ""
    if d == "P" and o in ("B", "M"):
        if o0 != "P":
            return "blocked in original only", "rule"
        return "blocked in original only", ("class-op" if v.get("ORIGnc") == "P" else "entity")
    if d.startswith("U") and o.startswith("U"):
        return "not reached", "%s/%s" % (d, o)
    if d == "U:walk" and not o.startswith("U"):
        return "downstream", "ORIG %s" % o
    return "uncertain", "%s/%s" % (d, o)


def load():
    rows = {}
    for run in RUNS:
        p = HERE + "/rows_%s.pkl" % run
        if not os.path.exists(p):
            continue
        for r in pickle.load(open(p, "rb")):
            rows.setdefault((r["map"], r["pc"]), {})[run] = r
    return rows


def first(r, cls):
    for c, d in (r or {}).get("all", []):
        if c == cls:
            return d
    return None


def chain_pcs(prov, mid, key, pin, depth=10):
    out = []
    seen = set()
    while pin is not None and depth > 0:
        k = (mid, key, tuple(pin))
        if k not in prov or k in seen:
            break
        seen.add(k)
        pc, op, frm, kind, reason, ob = prov[k]
        out.append((pc, op, reason, ob))
        pin = frm
        depth -= 1
    return out


def main():
    rows = load()
    cellw = pickle.load(open(HERE + "/cellw.pkl", "rb"))
    detour = pickle.load(open(HERE + "/detour.pkl", "rb")) if os.path.exists(HERE + "/detour.pkl") else {}
    prov = pickle.load(open(HERE + "/prov_DLL.pkl", "rb"))
    provo = pickle.load(open(HERE + "/prov_ORIG.pkl", "rb")) if os.path.exists(HERE + "/prov_ORIG.pkl") else {}
    out = []
    tot = collections.Counter()
    tot_story = collections.Counter()
    tot_lock = collections.Counter()
    for key in sorted(rows):
        rr = rows[key]
        base = rr.get("DLL") or next(iter(rr.values()))
        v = {run: verdict(rr.get(run)) for run in RUNS}
        k, mech = klass(v, rr.get("DLL"), rr.get("ORIG"), rr.get("ORIG0"), rr.get("ORIGnc"))
        note = ""
        if key in CERTIFIED:
            k, mech, note = CERTIFIED[key]
        lock = any((rr.get(run) or {}).get("lock") for run in ("DLL", "ORIG"))
        flags = []
        if "ORIGMAX" in v and v["ORIGMAX"] != v["ORIG"]:
            flags.append("xp:%s" % v["ORIGMAX"])
        # DLL-blocked states: cell writes, loop re-entry
        bl = [d for c, d in (rr.get("DLL") or {}).get("all", []) if c == "blocked"]
        if bl:
            cws = [d for d in bl if d.get("cell") and tuple(d["cell"]) in cellw.get(key[0], {})]
            if cws:
                flags.append("cw:%d/%d" % (len(cws), len(bl)))
            loops = [d for d in bl if any(p is not None and p > key[1] for p, _, _, _ in chain_pcs(prov, key[0], d["key"], d["pin"]))]
            if loops and len(loops) == len(bl):
                flags.append("loop")
            elif loops:
                flags.append("loop:%d/%d" % (len(loops), len(bl)))
        if (v["DLL"] in ("B", "M") or v["ORIG"] in ("B", "M")) and jump_near(key[0], key[1]):
            flags.append("jump")
        if key in detour:
            kinds = sorted({x[0] for x in detour[key]})
            flags.append("detour:" + ("clean" if "path clean" in kinds else ("none" if kinds == ["no grid path"] else "blocked-path")))
        # ORIG upstream obstacle stops on the start pin
        ostops = set()
        for c, d in (rr.get("ORIG") or {}).get("all", []):
            if d.get("pin") is not None and d.get("key") is not None:
                for pc, op, reason, ob in chain_pcs(provo, key[0], d["key"], d["pin"]):
                    if ob:
                        ostops.add((pc, ob[0], ob[3]))
            if d.get("obst"):
                ostops.add((key[1], d["obst"][0], d["obst"][3]))
        rel = ""
        if key in CERTIFIED:
            if mech == "entity":
                rel = "released by FIX [emu]" + (" (reached once @5172 passes)" if key[1] == 5071 else "")
            else:
                rel = "NOT released by FIX (scripted jump: 0x25 / IsZForceApplied, surface jump)"
        elif v["DLL"] in ("B", "M"):
            if v["FIX"] == "P":
                rel = "released by FIX"
            elif v.get("FIXC") == "P":
                rel = "released by FIX + 0x28-0x2B"
            elif v["FIXMAX"] == "P" if "FIXMAX" in v else False:
                rel = "released by FIX only with cross-program obstacles"
        if v["DLL"] == "P" and v["FIX"] in ("B", "M"):
            rel = "NEW block under FIX (%s)" % v["FIX"]
        bd = first(rr.get("DLL"), "blocked")
        ob = first(rr.get("ORIG"), "blocked")
        who = (first(rr.get("DLL"), "pass") or bd or first(rr.get("ORIG"), "pass") or ob or {}).get("actor", "")
        o = dict(map=key[0], pc=key[1], op="0x%02X" % base["op"], v=base.get("v"), reach=base["reach"], story=key[0] in STORY,
                 lock=lock, actor=who, cls=k, mech=mech, note=note, rel=rel, flags=" ".join(flags),
                 ostops=sorted(ostops, key=repr), **{"v_" + r: v.get(r, "-") for r in RUNS},
                 dll_block=(bd["reason"], bd["cell"], bd["n"], bd["rad"], bd["pin"], bd["dir"]) if bd else None,
                 orig_block=(ob["reason"], ob["cell"], ob["obst"], ob["n"], ob["rad"], ob["pin"], ob["dir"]) if ob else None,
                 n_states=len((rr.get("DLL") or {}).get("all", [])), n_blocked=len(bl),
                 dll_unreached=(rr.get("DLL") or {}).get("unreached"), orig_unreached=(rr.get("ORIG") or {}).get("unreached"))
        out.append(o)
        if base["reach"] == "dormant":
            tot["dormant"] += 1
            continue
        tot[k] += 1
        if key[0] in STORY:
            tot_story[k] += 1
        if lock:
            tot_lock[k] += 1
    pickle.dump(out, open(HERE + "/table.pkl", "wb"))
    with open(HERE + "/table.tsv", "w", encoding="utf-8") as f:
        cols = ["map", "pc", "op", "v", "reach", "story", "lock", "actor", "cls", "mech", "rel", "flags", "note", "v_DLL", "v_ORIG0", "v_ORIGnc",
                "v_ORIG", "v_FIX", "v_FIXC", "v_ORIGMAX", "v_FIXMAX", "n_states", "n_blocked", "dll_block", "orig_block", "ostops", "dll_unreached", "orig_unreached"]
        f.write("\t".join(cols) + "\n")
        for o in out:
            f.write("\t".join(str(o[c]) for c in cols) + "\n")
    with open(HERE + "/totals.txt", "w", encoding="utf-8") as f:
        f.write("population %d sites (0x0B %d, 0x1E %d)\n" % (len(out), sum(1 for o in out if o["op"] == "0x0B"),
                                                            sum(1 for o in out if o["op"] == "0x1E")))
        for name, t in (("all", tot), ("story maps", tot_story), ("held control", tot_lock)):
            f.write("== %s\n" % name)
            for k, n in sorted(t.items(), key=lambda kv: -kv[1]):
                f.write("  %5d  %s\n" % (n, k))
    print(open(HERE + "/totals.txt", encoding="utf-8").read())
    return out


if __name__ == "__main__":
    main()
