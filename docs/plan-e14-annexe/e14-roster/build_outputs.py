"""Builds roster.tsv, handlers.tsv and report_data.json from cache/census.pkl, the EXE tables, the decompilation index and the DLL sources.
Read-only on the repository. Run: python -B build_outputs.py   (after extract_maps.py, exe_handlers.py, census.py)"""
import collections
import json
import os
import pickle
import re
import sys

import common
import decomp_index
import handlers_base as hb

sys.stdout.reconfigure(encoding="utf-8")
HERE = common.HERE
LETTERS = "ABCDEF"
SLOTS = (0, 2, 3, 4, 5)
HDR_FIELD = {0: "ProgramLoad", 2: "ProgramTick", 3: "ProgramTouch", 4: "ProgramDeactivate", 5: "ProgramInteract"}

# ---------------------------------------------------------------------------------------------------------------------------
# Story order given by the plans (docs/plan-e19-opcodes.md): the only ordered list of maps the plans contain.
# Each phase: (label, maps in the order listed, source). The order inside a phase is the order of the list that gives it.
STORY = [
    ("1-intro", [389, 390, 476, 478, 392, 391, 416, 163], "docs/plan-e19-opcodes.md:382"),
    ("2-jour1", [162, 165, 164], "docs/plan-e19-opcodes.md:2564-2565"),
    ("3-jour2", [172, 169, 170, 171, 173, 174, 175, 14, 15, 10], "docs/plan-e19-opcodes.md:4118 (14, 15, 169 a 175), :2564 (10/14/15), :4153 (E1 list order)"),
    ("4-Tarn", [115, 116, 117], "docs/plan-e19-opcodes.md:4118 (Tarn's Manor 115 a 117), :2564"),
    ("5-jour3", [179, 44, 176, 135, 178], "docs/plan-e19-opcodes.md:2564-2566"),
    ("6-jour4", [183, 185, 362], "docs/plan-e19-opcodes.md:2566"),
    ("7-chaine-jour-non-precise", [177, 180, 181, 182, 184], "docs/plan-e19-opcodes.md:4153 (E1 list, day not stated)"),
]
STORY_RANK = {}
for pi, (label, lst, src) in enumerate(STORY):
    for li, m in enumerate(lst):
        STORY_RANK.setdefault(m, (pi, li, label))


def bfs_depth(start, edges_a, edges_b):
    """Directed BFS depth from start over the union of edge dicts; edges to or from map 0 (Test Map) and 11 (Debug zone) are ignored."""
    depth = {start: 0}
    queue = collections.deque([start])
    while queue:
        u = queue.popleft()
        if u in (0, 11):
            continue
        for v in set(edges_a.get(u, ())) | set(edges_b.get(u, ())):
            if v in (0, 11):
                continue
            if v not in depth:
                depth[v] = depth[u] + 1
                queue.append(v)
    return depth


_INS = None


def ins_cache():
    global _INS
    if _INS is None:
        with open(os.path.join(HERE, "cache", "exe_ins.pkl"), "rb") as fh:
            _INS = pickle.load(fh)
    return _INS


def small_effect(addr, end):
    """Straight-line constants a short handler stores at entity+0x88 (TargetAnimationId) and the byte store at +0x277 (Bytes[3]).
    A constant register is tracked from 'addiu rX, $zero, imm' until the next write of rX; branches are ignored (the values are the ones
    the handler can store, not a trace)."""
    ins = ins_cache()
    consts = {}
    anim = []
    flags = []
    a = addr
    while a < end:
        x = ins.get(a)
        a += 4
        if not x:
            continue
        mn, op = x
        parts = [q.strip() for q in op.split(",")]
        if mn in ("addiu", "ori") and len(parts) == 3 and parts[1] == "$zero":
            try:
                consts[parts[0]] = int(parts[2], 0)
            except ValueError:
                consts.pop(parts[0], None)
        elif mn in ("sw", "sh", "sb") and len(parts) == 2:
            m = re.match(r"(0x[0-9a-fA-F]+|-?\d+)\((\$\w+)\)", parts[1])
            if m and parts[0] in consts:
                off = int(m.group(1), 0)
                if off == 0x88:
                    anim.append(consts[parts[0]])
                elif off == 0x277:
                    flags.append(consts[parts[0]])
        elif mn not in ("nop",) and parts and parts[0].startswith("$") and mn not in ("beq", "bne", "beqz", "bnez", "sw", "sh", "sb", "jr", "j"):
            consts.pop(parts[0], None)
    out = []
    if anim:
        out.append("anim:" + ",".join(str(v) for v in sorted(set(anim))))
    if flags:
        out.append("Bytes[3]=" + ",".join(str(v) for v in sorted(set(flags))))
    return " ".join(out)


def tsv(path, header, rows):
    with open(path, "w", encoding="utf-8", newline="") as fh:
        fh.write("\t".join(header) + "\n")
        for r in rows:
            fh.write("\t".join("" if v is None else str(v).replace("\t", " ").replace("\n", " ") for v in r) + "\n")


def main():
    census = pickle.load(open(os.path.join(HERE, "cache", "census.pkl"), "rb"))
    T, K, H = census["types"], census["keys"], census["headers"]
    data = common.load_cache()
    maps = data["maps"]
    names = common.load_entity_names()
    maps_cat = common.load_maps_catalog()
    bal = common.load_balance()
    impl = common.parse_dll_implementation()
    exe, addr_idx, methods, regs = hb.load()
    E = exe["entries"]
    sites = decomp_index.build_spawn_sites()
    guards_raw = decomp_index.build_guards()
    guards = collections.defaultdict(list)
    for (path_, fn_), v_ in guards_raw.items():
        for nm_ in v_["names"]:
            if nm_ not in guards[fn_]:
                guards[fn_].append(nm_)

    import hashlib
    import exelib
    ins_all = ins_cache()
    noop_keys = set()
    body_groups = collections.defaultdict(list)
    for (s_, c_), e_ in E.items():
        words = (e_["end"] - e_["addr"]) // 4
        if words == 2 and ins_all.get(e_["addr"]) == ("jr", "$ra") and ins_all.get(e_["addr"] + 4) == ("nop", ""):
            noop_keys.add((s_, c_))
        raw = exelib.DATA[exelib.off(e_["addr"]):exelib.off(e_["addr"]) + (e_["end"] - e_["addr"])]
        body_groups[hashlib.md5(raw).hexdigest()].append((s_, c_, e_["addr"]))

    def identical_body(s_, c_):
        e_ = E.get((s_, c_))
        if not e_:
            return ""
        raw = exelib.DATA[exelib.off(e_["addr"]):exelib.off(e_["addr"]) + (e_["end"] - e_["addr"])]
        grp = body_groups[hashlib.md5(raw).hexdigest()]
        return ",".join(sorted({f"{LETTERS[a]}{b}" for a, b, ad in grp if ad != e_["addr"]}))

    def impl_status2(s_, c_, t_):
        st_ = common.impl_status(impl, s_, c_, t_)
        if st_ == "none" and (s_, c_) in noop_keys:
            return "noop-in-binary"
        return st_

    depth = bfs_depth(389, census["portal_edges"], census["map_edges"])
    depth_portal_only = bfs_depth(389, census["portal_edges"], {})

    DEV_RE = re.compile(r"unused|debug|test map|early version|beta|warp-zone|a big mess", re.I)

    def is_dev_map(mid):
        z_, n_ = maps_cat.get(mid, ("", ""))
        return bool(DEV_RE.search(z_ + " " + n_))

    def map_label(mid):
        z, n = maps_cat.get(mid, ("?", "?"))
        return f"{mid}:{n}"

    # ------------------------------------------------------------------ spawn sites by type and by enclosing function
    boss_methods = {name for (path, name) in methods if "/Boss/" in path}
    reg_by_func = {}
    for (s, c_), (fn, line, cm) in regs.items():
        reg_by_func.setdefault(fn.split(".")[-1], []).append((s, c_))
    spawn_types_by_func = collections.defaultdict(set)   # func -> set(type)
    spawners_by_type = collections.defaultdict(list)      # type -> [(func, loc)]
    dynamic_sites = []
    for st in sites:
        mp = decomp_index.eval_const(st["mapArg"])
        tp = decomp_index.eval_const(st["typeArg"])
        types_here = []
        if tp is not None and mp is not None:
            types_here = [tp | (0x100 if mp else 0)]
        else:
            m2 = re.fullmatch(r"\(?(?:uint\))?\(?\s*(0x[0-9a-fA-F]+)\s*\|\s*\(\w+\s*&\s*1\)\)?", st["typeArg"].strip())
            if m2 and mp is not None:
                base = int(m2.group(1), 16)
                types_here = [(base | (0x100 if mp else 0)), ((base | 1) | (0x100 if mp else 0))]
        if types_here:
            for t in types_here:
                spawn_types_by_func[st["func"]].add(t)
                spawners_by_type[t].append((st["func"], f"{st['file']}:{st['line']}"))
        else:
            dynamic_sites.append(st)

    def func_keys(func):
        return reg_by_func.get(func, [])

    # ------------------------------------------------------------------ handler rows
    def callees(slot, code):
        e = E.get((slot, code))
        if not e:
            return {}
        out = {}
        for a, n in e["jals"].items():
            out[hb.func_name_at(a, addr_idx) or f"0x{a:08x}"] = n
        return out

    COMPUTE_HP = 0x8003A374
    SPAWN_WARP = 0x80039F58
    SPAWN_CHILD = 0x8003A048
    DESTROY = 0x8003A59C

    # types by header code, for every header (even types without any record)
    types_by_header = collections.defaultdict(list)
    for t, h in H.items():
        for s in SLOTS:
            types_by_header[(s, h[HDR_FIELD[s]])].append(t)

    # classification first (the handler table needs the roles of the types that use a handler)
    flagnames = {0x01: "ClassA", 0x02: "HitsClassB", 0x04: "HitsClassA", 0x08: "ClassB", 0x10: "DeactOnImpact", 0x20: "DeactOnHit", 0x40: "DeactOnAnimEnd", 0x80: "b7"}

    role = {}
    role_evidence = {}
    boss_evidence = {}
    sub = {}

    def d_handler_damages(h):
        hd = h["ProgramTouch"]
        e = E.get((3, hd)) if hd else None
        return bool(e and COMPUTE_HP in e["jals"])

    boss_file_keys = {}
    for (s, c_), (fn, line, cm) in regs.items():
        if fn.split(".")[-1] in boss_methods:
            boss_file_keys[(s, c_)] = fn

    BOSS_NAME_WORDS = re.compile(r"Boss", re.I)
    PART_WORDS = re.compile(r"\((corps principal|tronc|tronc 2|queue|joint|œil|grand|petit|corps|avec corps|pensée résiduelle)\)|^◆Corps$")
    PROJ_WORDS = re.compile(r"Projectile|Flèche|Lance Niv|Boomerang|Souffle|Spores|Débris|Boule de feu|Boule de glace|Magie|Laser à|Effet|Collider|Bombardement|Tentacule|Grand rocher|Cercle magique|Bras, projectiles|Flammes de dragon", re.I)

    for t in sorted(H):
        h = H[t]
        nm = names[t][0] if t < len(names) else "?"
        jp = names[t][1] if t < len(names) else "?"
        fl = h["MoreFlags"]
        hurts = bool(fl & 0x02)
        hurtable = bool(fl & 0x01)
        deact = bool(fl & 0x70)
        dmg_recv = d_handler_damages(h)
        arena_maps = [m for m in (T.get(t, {}).get("maps") or {}) if re.search(r"\(Boss|Arena", maps_cat.get(m, ("", ""))[1] + " " + maps_cat.get(m, ("", ""))[0])]
        keys_here = [(s, h[HDR_FIELD[s]]) for s in SLOTS if h[HDR_FIELD[s]] or s in (0, 4)]
        boss_fact = [f"{LETTERS[s]}{c_}={boss_file_keys[(s, c_)]}" for (s, c_) in keys_here if (s, c_) in boss_file_keys]
        ev = []
        r = None
        if t == 0:
            r = "hero"
            ev.append("type 0, row 0 of EntityNames.csv, MoreFlags 0x%02x" % fl)
        elif re.match(r"^I\d\d_", nm):
            r = "item (pickup)"
            ev.append("name I##_ (EntityNames.csv); header A255/C255 (C255 = pickable object handler per plan-e20-heros.md:547)")
        elif (nm in ("(vide)", "(empty)")) and not T.get(t):
            r = "unused slot"
            ev.append("EntityNames placeholder (vide), no record")
        elif hurtable and dmg_recv:
            r = "enemy"
            ev.append(f"FACT: D{h['ProgramTouch']} handler calls ComputeNewHp 0x8003A374 (EXE); MoreFlags 0x{fl:02x} " + ("hittable (ClassA) and hurts the hero (HitsClassB)" if hurts else "hittable (ClassA), does not hurt by contact"))
            all_boss_maps = bool(T.get(t)) and all(re.search(r"\(Boss|Arena", " ".join(maps_cat.get(m, ("", "")))) for m in T[t]["maps"])
            bev = []
            if boss_fact:
                bev.append("FACT(placement): handler in decompilation Boss/ file: " + ",".join(boss_fact))
            if all_boss_maps:
                bev.append("INFERENCE: every record lies in a map named (Boss)/Arena: " + ",".join(map_label(m) for m in sorted(T[t]["maps"])[:4]))
            if BOSS_NAME_WORDS.search(nm):
                bev.append("INFERENCE: name contains Boss")
            some_boss_maps = [m for m in (T.get(t, {}).get("maps") or {}) if re.search(r"\(Boss|Arena", " ".join(maps_cat.get(m, ("", ""))))]
            if some_boss_maps and not all_boss_maps and T[t]["recs"] <= 5:
                bev.append("INFERENCE: at most 5 records and one lies in a map named (Boss)/Arena: " + ",".join(map_label(m) for m in sorted(some_boss_maps)[:3]))
            cfn = regs.get((2, h["ProgramTick"]), ("",))[0] if h["ProgramTick"] else ""
            if re.search(r"Boss|Twin|Watcher|Melzas|Special", cfn):
                bev.append(f"INFERENCE: decompiler names its C handler {cfn}")
            if h["ProgramTick"] >= 43 and h["ProgramTick"] != 61 and not bev and T.get(t) is not None:
                pass
            if PART_WORDS.search(nm):
                sub[t] = "part (name: " + PART_WORDS.search(nm).group(0) + ")"
            if bev:
                r = "boss"
                boss_evidence[t] = "; ".join(bev)
        elif (1 <= t <= 24 or 27 <= t <= 29) and nm not in ("(vide)",):
            r = "projectile/effect (hero weapon)" if t >= 3 else "object (hero item: beans/bomb)"
            ev.append("global page 1-24 and 27-29 = hero items, weapons and magic by name (EntityNames.csv); 25 (block) and 26 (mine cart) are props")
        elif hurts and not hurtable and (PROJ_WORDS.search(nm) or nm.startswith("◆") or (deact and t in spawners_by_type)):
            r = "projectile/effect (hostile)"
            ev.append(f"MoreFlags 0x{fl:02x}: HitsClassB without ClassA (hurts the hero, cannot be hit)")
            if deact:
                ev.append("flags DeactivateOn{Impact,Hit,AnimEnd} set")
            if t in spawners_by_type:
                ev.append("spawned by decompilation handlers: " + ",".join(sorted({f for f, _ in spawners_by_type[t]})[:4]))
        elif hurts and not hurtable:
            r = "hazard/object (hurts the hero)"
            ev.append(f"MoreFlags 0x{fl:02x}: HitsClassB, not hittable; no projectile signal")
        elif hurts and hurtable:
            r = "hazard/object (hittable and hurts the hero, no native hit handler)" if not nm.startswith("◆") else "enemy-attached object (diamond name; hittable and hurts, no native hit handler)"
            ev.append(f"MoreFlags 0x{fl:02x} = ClassA+HitsClassB but D{h['ProgramTouch']} does not call ComputeNewHp")
        elif h["FlagsPortraitShadowType"] & 0x80:
            r = "NPC"
            ev.append("FACT: header FlagsPortraitShadowType bit 0x80 = dialogue portrait present (DialoguePortrait in sprite-records.json)")
        elif re.search(r"\(.+\)", nm) and fl == 0x80 and not hurts and 256 <= t <= 290 and not re.search(r"générique|Pierre|Cruche|Tonneau|Caisse|Herbe|Statue|Interrupteur|Plateforme|Porte|Grille|Trappe|Bloc|Rocher|Boule|Mur|Pilier", nm):
            r = "NPC"
            ev.append("INFERENCE: name 'Person (role)' in the NPC block of EntityNames.csv, flags 0x80, no combat bits")
        elif re.search(r"Toutou|Poulet|Corbeau|Mouette|Marin", nm):
            r = "NPC"
            ev.append("INFERENCE: name = animal or crew (ambient), flags no combat bits")
        elif nm.startswith("◆"):
            r = "enemy-attached object (diamond name, no combat bits)"
            ev.append(f"INFERENCE: diamond marker in EntityNames.csv, MoreFlags 0x{fl:02x}")
            if hurtable:
                ev.append("hittable (ClassA) without hurting; D handler calls ComputeNewHp" if dmg_recv else "hittable (ClassA)")
        else:
            r = "object/prop"
            ev.append(f"INFERENCE: name-based, MoreFlags 0x{fl:02x}, no combat signal")
        if r in ("enemy", "boss") and not T.get(t):
            r = r + " (spawned only, no map record)"
        if r in ("enemy", "boss") and sub.get(t):
            ev.append("subkind: " + sub[t])
        role[t] = r
        role_evidence[t] = "; ".join(ev)
        # object sub-kinds by name (INFERENCE)
        if r == "object/prop":
            for pat, lab in ((r"Interrupteur", "switch"), (r"Plateforme|asiba", "platform"), (r"Porte|Grille|Trappe", "door/grate/hatch"),
                              (r"Bloc|Pilier|Statue|Pierre|Rocher|Boule|Mur", "block/statue/stone/wall"), (r"Cruche|Tonneau|Caisse|Herbe|Tronc|Coffre", "container/breakable"),
                              (r"Torche|Flamme|Ronces|Pilier de feu", "fire"), (r"Déclencheur", "invisible trigger")):
                if re.search(pat, nm):
                    sub[t] = lab
                    break

    # ------------------------------------------------------------------ implementation status per type
    def native_requirements(t):
        """Keys the type's records reach natively: loaded native slots (effective ones) and 0x40 hand-overs to native."""
        x = T.get(t)
        req = {}
        h = H[t]
        if not x:
            # a type with no map record is spawned at run time (SpawnWarpEntity): its program indexes start at 0, so every header code runs natively
            for s_ in SLOTS:
                c_ = h[HDR_FIELD[s_]]
                if c_ == 0 and s_ in (2, 3, 5):
                    continue
                req[(s_, c_)] = 0
            return req
        if x:
            for (s, c_), n in x["native_keys"].items():
                if c_ == 0 and s in (2, 3, 5):
                    continue  # null entry: the dispatch does not call anything
                req[(s, c_)] = req.get((s, c_), 0) + n
            for (frm, slot, val), n in x["handover"].items():
                if slot in SLOTS and (val & 0x7F) == 0:
                    c_ = h[HDR_FIELD[slot]]
                    if not (c_ == 0 and slot in (2, 3, 5)):
                        req.setdefault((slot, c_), 0)
            for (frm, slot, val), n in x["spritecode"].items():
                # 0x41 [slot, code]: SpriteProgramIndexes[slot] = code, so the native (slot, code) can run for this entity afterwards
                if slot in SLOTS and val > 0:
                    req.setdefault((slot, val), 0)
        return req

    def status_of(t):
        req = native_requirements(t)
        if not req:
            return "n/a (no native key reached)", [], []
        if all(k == (0, 0) for k in req):
            return "n/a (A0 only)", [(0, 0)] if common.impl_status(impl, 0, 0, t) == "none" else [], []
        done, missing = [], []
        for k in sorted(req):
            st = impl_status2(k[0], k[1], t)
            (done if st != "none" else missing).append(k)
        if not missing:
            return "implemented", missing, done
        if missing == [(0, 0)]:
            return "implemented except A0", missing, done
        if not done or done == [(4, 0)] or all(k[0] == 4 and k[1] in impl["inline_e"] for k in done):
            return "none (E0/E1 death inline only)" if done else "none", missing, done
        return "partial", missing, done

    # ------------------------------------------------------------------ roster rows
    def fmt_key(k):
        return f"{LETTERS[k[0]]}{k[1]}"

    roster_rows = []
    roster_json = []
    for t in sorted(H):
        h = H[t]
        x = T.get(t)
        nm_fr, nm_jp = (names[t][0], names[t][1]) if t < len(names) else ("?", "?")
        page = "M" if t >= 256 else "G"
        chain = common.balance_chain(bal, t)
        chain_s = "|".join(f"L{r['level']}:hp{r['hp']}/p{r['maxPower']}" for r in chain) if chain else ""
        hp_used = ""
        if x and chain:
            used = set()
            for mid in x["maps"]:
                thr = maps[mid]["info"]["D"] or 0
                r = common.select_level(chain, thr)
                if r:
                    used.add((r["level"], r["hp"], r["maxPower"]))
            hp_used = "|".join(f"L{a}:hp{b}/p{c_}" for a, b, c_ in sorted(used))
        status, missing, done = status_of(t)
        req = native_requirements(t)
        recs = x["recs"] if x else 0
        mps = sorted(x["maps"]) if x else []
        first_maps = " ; ".join(map_label(m) for m in mps[:5])
        n_dev = sum(1 for m in mps if is_dev_map(m))
        story_best = None
        for m in mps:
            r = STORY_RANK.get(m)
            if r and (story_best is None or r < story_best[1]):
                story_best = (m, r)
        first_story = f"{map_label(story_best[0])} [{story_best[1][2]}]" if story_best else ""
        dmin = min([depth[m] for m in mps if m in depth], default=None)
        slot_ns = ""
        if x:
            slot_ns = " ".join(f"{LETTERS[s]}{x['slot_native'].get(s, 0)}/{x['slot_scripted'].get(s, 0)}" for s in SLOTS)
        nat_keys = ""
        if x:
            nat_keys = " ".join(f"{fmt_key(k)}x{n}" for k, n in sorted(x["native_keys"].items()) if not (k[1] == 0 and k[0] in (2, 3, 5)))
        scripted = ""
        if x:
            parts = []
            for s in SLOTS:
                pr = x["scripted_progs"].get(s)
                if pr:
                    parts.append(f"{LETTERS[s]}:" + ",".join(f"{p}x{n}" for p, n in sorted(pr.items())[:8]) + ("..." if len(pr) > 8 else ""))
            scripted = " ".join(parts)
        handover = ""
        if x and x["handover"]:
            handover = " ".join(f"{LETTERS[a]}:0x40[{LETTERS[b]},{v}]x{n}" for (a, b, v), n in sorted(x["handover"].items()))
        sprite_code = ""
        if x and x["spritecode"]:
            sprite_code = " ".join(f"{LETTERS[a]}:0x41[{LETTERS[b]},{v}]x{n}" for (a, b, v), n in sorted(x["spritecode"].items()))
        impl_keys = " ".join(f"{fmt_key(k)}:{impl_status2(k[0], k[1], t)}" for k in sorted(req))
        spawns = set()
        for s in SLOTS:
            code = h[HDR_FIELD[s]]
            fn = regs.get((s, code), (None,))[0]
            if fn:
                spawns |= spawn_types_by_func.get(fn.split(".")[-1], set())
        spawns_s = ",".join(f"{u}:{names[u][0] if u < len(names) else '?'}" for u in sorted(spawns))
        spawned_by = ""
        if t in spawners_by_type:
            spawned_by = "; ".join(sorted({f"{f}@{loc.split('/')[-1]}" for f, loc in spawners_by_type[t]}))
        flags_list = "+".join(n for b, n in flagnames.items() if h["MoreFlags"] & b)
        row = [t, page, t & 0xFF if page == "M" else t, nm_fr, nm_jp, role[t], role_evidence[t], boss_evidence.get(t, ""), sub.get(t, ""),
               h["ProgramLoad"], h["ProgramTick"], h["ProgramTouch"], h["ProgramDeactivate"], h["ProgramInteract"],
               f"0x{h['MoreFlags']:02x}", flags_list, "Y" if h["FlagsPortraitShadowType"] & 0x80 else "", h["BreakEffect"], h["Contents"],
               chain_s, hp_used, recs, x["load_spawn"] if x else 0, x["script_spawn"] if x else 0, len(mps), n_dev, first_maps, first_story,
               dmin if dmin is not None else "", slot_ns, nat_keys, scripted, handover, sprite_code, impl_keys,
               " ".join(fmt_key(k) for k in missing), status, spawns_s, spawned_by]
        roster_rows.append(row)
        roster_json.append({"type": t, "name": nm_fr, "role": role[t], "boss": t in boss_evidence, "status": status, "recs": recs, "maps": len(mps),
                            "mapIds": mps, "devMaps": n_dev, "missing": [fmt_key(k) for k in missing], "done": [fmt_key(k) for k in done],
                            "first_story": story_best[0] if story_best else None, "story_phase": story_best[1][2] if story_best else None,
                            "story_rank": list(story_best[1][:2]) if story_best else None, "depth": dmin, "hp_used": hp_used, "chain": chain_s,
                            "hdr": [h["ProgramLoad"], h["ProgramTick"], h["ProgramTouch"], h["ProgramDeactivate"], h["ProgramInteract"]],
                            "req": {fmt_key(k): n for k, n in req.items()}, "spawns": sorted(spawns), "spawned_by": spawned_by,
                            "scripted_c": dict(x["scripted_progs"].get(2, {})) if x else {}, "handover": handover, "slot_ns": slot_ns,
                            "native_c_recs": x["slot_native"].get(2, 0) if x else 0, "scripted_c_recs": x["slot_scripted"].get(2, 0) if x else 0})
    roster_header = ["type", "page", "table_index", "name_fr", "name_jp", "class", "class_evidence", "boss_evidence", "subkind",
                     "hdr_A_load", "hdr_C_tick", "hdr_D_touch", "hdr_E_deactivate", "hdr_F_interact", "MoreFlags", "flag_bits", "portrait", "BreakEffect", "Contents",
                     "balance_chain(level:hp/maxpower)", "balance_used_on_maps", "records_enabled", "records_spawn_at_load(0x40)", "records_script_spawn_only",
                     "n_maps", "n_maps_named_test/debug/unused(INFERENCE)", "first_5_maps_by_id(id:name)", "first_story_map[phase]", "min_portal_depth_from_389", "slots_native/scripted(A C D E F)",
                     "native_keys_by_records(key x records)", "scripted_programs(slot:prog x records)", "handovers_0x40", "sprite_code_swaps_0x41",
                     "impl_status_of_reached_keys", "keys_not_implemented", "status", "spawns_types(decompilation)", "spawned_by(decompilation)"]
    tsv(os.path.join(HERE, "roster.tsv"), roster_header, roster_rows)

    # ------------------------------------------------------------------ handlers.tsv
    role_of = role
    handler_rows = []
    handler_json = []
    all_keys = sorted(set(E) | set(K))
    for (s, c_) in all_keys:
        e = E.get((s, c_))
        reg = regs.get((s, c_))
        k = K.get((s, c_), {"recs": 0, "types": collections.Counter(), "maps": set(), "via_handover": collections.Counter()})
        hdr_types = types_by_header.get((s, c_), [])
        types_c = k["types"]
        type_list = ", ".join(f"{t}:{names[t][0]}x{n}" for t, n in sorted(types_c.items(), key=lambda kv: -kv[1])[:12])
        hdr_list = ", ".join(f"{t}:{names[t][0]}" for t in sorted(hdr_types)[:14]) + ("..." if len(hdr_types) > 14 else "")
        roles = collections.Counter(role_of[t] for t in set(hdr_types) | set(types_c))
        impl_types = sorted({t for t in set(hdr_types) | set(types_c) if impl_status2(s, c_, t) != "none"})
        unported_types = sorted({t for t in types_c if impl_status2(s, c_, t) == "none"})
        impl_s = ",".join(f"{impl_status2(s, c_, t)}:{t}" for t in impl_types) or "none"
        mps = sorted(k["maps"])
        first_story_best = None
        for m in mps:
            r = STORY_RANK.get(m)
            if r and (first_story_best is None or r < first_story_best[1]):
                first_story_best = (m, r)
        cal = callees(s, c_) if e else {}
        has_hp = bool(e and COMPUTE_HP in e["jals"])
        n_spawn = (e["jals"].get(SPAWN_WARP, 0) + e["jals"].get(SPAWN_CHILD, 0)) if e else 0
        fn_short = reg[0].split(".")[-1] if reg else ""
        spawns_t = sorted(spawn_types_by_func.get(fn_short, set())) if fn_short else []
        dloc = ""
        if reg:
            for (path, nm_), line in methods.items():
                if nm_ == fn_short and ("FunctionType" in path or "Boss" in path or "SpriteEventHandlers" in path):
                    dloc = f"{path}:{line}"
                    break
        addr_match = ""
        if e and reg:
            hits = [n for n, _ in addr_idx.get(e["addr"], [])]
            addr_match = "Y" if fn_short in hits else ("no address comment" if not hits else "N")
        gnames = guards.get(fn_short, []) if fn_short else []
        seen_names = {names[t_][0] for t_ in (set(hdr_types) | set(types_c) | set(k.get("via_swap", {}))) if t_ < len(names)}
        g_extra = [n_ for n_ in gnames if n_ not in seen_names and n_ != "Melzas2_FinalBoss"]
        g_missing = sorted(n_ for n_ in seen_names if gnames and n_ not in gnames)
        bossy = (s, c_) in boss_file_keys
        effect = small_effect(e["addr"], e["end"]) if e and (e["end"] - e["addr"]) // 4 <= 80 else ""
        shape = ""
        if e and s == 3 and (e["end"] - e["addr"]) // 4 == 25 and set(e["jals"].values()) == {1} and len(e["jals"]) == 2 and COMPUTE_HP in e["jals"]:
            shape = "HitReaction(" + effect + ")"
        alias = sorted(f"{LETTERS[s2]}{c2}" for (s2, c2), e2 in E.items() if e and e2["addr"] == e["addr"] and (s2, c2) != (s, c_))
        handler_rows.append([
            f"{LETTERS[s]}{c_}", LETTERS[s], c_, f"0x{e['addr']:08x}" if e else "null (no call)", (e["end"] - e["addr"]) // 4 if e else "", ",".join(alias), identical_body(s, c_),
            reg[0] if reg else "", dloc, addr_match, f"SpriteEventHandlers.cs:{reg[1]}" if reg else "", "Y" if bossy else "",
            e["rng"] if e else "", "; ".join(f"{n}x{v}" for n, v in sorted(cal.items())) if cal else "",
            "Y" if has_hp else "", n_spawn, shape or effect, ",".join(f"{u}:{names[u][0] if u < len(names) else '?'}" for u in spawns_t),
            k["recs"], len(types_c), len(mps), ",".join(map(str, mps[:6])), f"{map_label(first_story_best[0])} [{first_story_best[1][2]}]" if first_story_best else "",
            sum(k["via_handover"].values()), ",".join(f"{t}:{names[t][0]}x{n}" for t, n in sorted(k.get("via_swap", {}).items())), type_list, len(hdr_types), hdr_list, "; ".join(f"{r}:{n}" for r, n in roles.most_common()),
            impl_s, ",".join(map(str, unported_types)), " | ".join(gnames), " | ".join(g_extra), " | ".join(g_missing)])
        handler_json.append({"key": f"{LETTERS[s]}{c_}", "slot": s, "code": c_, "addr": e["addr"] if e else None, "words": (e["end"] - e["addr"]) // 4 if e else 0,
                             "func": reg[0] if reg else "", "boss": bossy, "rng": e["rng"] if e else 0, "calls": cal, "hp": has_hp, "spawnsN": n_spawn,
                             "recs": k["recs"], "types": dict(types_c), "maps": mps, "hdrTypes": hdr_types, "implTypes": impl_types, "unported": unported_types,
                             "roles": dict(roles), "firstStory": first_story_best[0] if first_story_best else None,
                             "storyRank": list(first_story_best[1][:2]) if first_story_best else None})
    handler_header = ["key", "slot", "code", "exe_table_entry", "exe_span_words(upper bound)", "same_function_as", "identical_body_as", "decompilation_function", "decompilation_location", "decomp_address_comment_matches_exe",
                      "registration_line", "in_Boss_file", "rng_sites(lui 0x7d2b)", "exe_calls(name x count)", "calls_ComputeNewHp", "calls_SpawnWarp/SpawnChild", "short_handler_effect",
                      "spawns_types(decompilation literals)", "records_native_enabled", "n_types_with_records", "n_maps", "first_6_maps_by_id", "first_story_map[phase]",
                      "records_reaching_by_0x40", "types_swapping_to_it_by_0x41(type x sites)", "types_with_records(type:name x records)", "n_types_with_header_code", "types_with_header_code", "roles_of_types",
                      "dll_implemented(status:type)", "unported_types_with_records", "decomp_guard_names(entity.Name checks)", "guard_names_not_in_census", "census_types_not_in_guard"]
    tsv(os.path.join(HERE, "handlers.tsv"), handler_header, handler_rows)

    with open(os.path.join(HERE, "cache", "report_data.pkl"), "wb") as fh:
        pickle.dump({"roster": roster_json, "handlers": handler_json, "dynamic_sites": dynamic_sites, "STORY": STORY, "depth": depth,
                     "depth_portal_only": depth_portal_only, "impl": {k: (sorted(v) if isinstance(v, set) else v) for k, v in impl["registry"].items()},
                     "boss_file_keys": boss_file_keys, "spawn_types_by_func": {k: sorted(v) for k, v in spawn_types_by_func.items()},
                     "spawners_by_type": dict(spawners_by_type)}, fh)
    print("roster rows", len(roster_rows), "handler rows", len(handler_rows), "dynamic spawn sites", len(dynamic_sites))
    print("BFS reach from 389: portals+0x53", len(depth), "portals only", len(depth_portal_only), "of", len(maps))
    cnt = collections.Counter(role.values())
    print(cnt)


if __name__ == "__main__":
    main()
