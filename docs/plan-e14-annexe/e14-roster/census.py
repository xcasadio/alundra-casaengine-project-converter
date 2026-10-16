"""Census of the enabled Entities records of every map (version 1). Read-only on the repository.

Rule re-established from the code and the data (see report.md section 1):
  * Dispatch (EXE 0x800388F8-0x80038944): for the entity's pending slot s, if (entity[+0x4C+4*s] & 0x7F) == 0 the native handler
    table[s][ entity[+0x70+4*s] ] runs (null = no call); otherwise the bytecode program (index & 0x7F) runs.
  * entity[+0x4C+4*s] is the record's index of slot s (Entities record EventCodes{A,C,D,E,F}*Index), entity[+0x70+4*s] is the sprite
    header byte 0x13..0x17 (ProgramLoad/Tick/Touch/Deactivate/Interact), copied at 0x80039DD4-0x80039E10.
  * sprite type = SpriteTableIndex, + 0x100 when SpriteDirection & 0x80 (GameEngine.SpawnEntity, DLL EntityRecordMapper.ResolveSpriteType).
  * a record is spawnable only when IsEnabled != 0 (GameEngine.GetEntityRecord); it is spawned at map load only when SpriteDirection & 0x40
    (and the hero is in its zone), otherwise only by a script (opcodes 0x2D / 0x8A / 0x8B).
Output: cache/census.pkl
Run: python -B census.py"""
import collections
import os
import pickle
import sys

import common

SLOTS = (0, 2, 3, 4, 5)  # A C D E F (B = map events, no sprite handler)
LETTERS = "ABCDEF"
ENT_FIELD = {0: "EventCodesA_LoadIndex", 2: "EventCodesC_TickIndex", 3: "EventCodesD_TouchIndex", 4: "EventCodesE_DeactivateIndex", 5: "EventCodesF_InteractIndex"}
HDR_FIELD = {0: "ProgramLoad", 2: "ProgramTick", 3: "ProgramTouch", 4: "ProgramDeactivate", 5: "ProgramInteract"}


def program_extents(tables, ncodes):
    starts = sorted({o for k, t in tables.items() if k != 'F' for o in t if 0 < o < ncodes})
    ends = {}
    for i, s in enumerate(starts):
        ends[s] = starts[i + 1] if i + 1 < len(starts) else ncodes
    return ends


def decode(codes, start, end, sizes):
    pc = start
    ops = []
    stopped = False
    while pc < end:
        op = codes[pc]
        sz = sizes.get(op, (0, ""))[0]
        if sz <= 0 or pc + sz > len(codes):
            stopped = True
            break
        ops.append((pc, op, codes[pc:pc + sz]))
        pc += sz
    return ops, stopped


def main():
    data = common.load_cache()
    maps = data["maps"]
    glob_headers = data["alundra"]
    gev = data["alundraEvents"]
    sizes = common.opcode_sizes()

    # type header table (verified identical across maps and with the project export by the check in report.md)
    headers = {}
    for t, h in glob_headers.items():
        headers[t] = h
    for mid, m in maps.items():
        for i, h in m["spriteRecords"].items():
            headers.setdefault(0x100 + i, h)

    g_ends = program_extents(gev["tables"], len(gev["codes"]))
    cache_prog = {}

    def program_ops(mid, letter, number, from_global):
        key = (None if from_global else mid, letter, number)
        if key in cache_prog:
            return cache_prog[key]
        if from_global:
            tables, codes, ends = gev["tables"], gev["codes"], g_ends
        else:
            m = maps[mid]
            tables, codes = m["tables"], m["codes"]
            ends = m.setdefault("_ends", program_extents(tables, len(codes)))
        tab = tables[letter]
        res = ([], False, False)
        if 0 <= number < len(tab) and 0 < tab[number] < len(codes):
            if tab[number] not in ends:  # F table entries (the table has no recorded size): extent up to the next known start
                nxt = [x for x in ends if x > tab[number]]
                ends[tab[number]] = min(nxt) if nxt else len(codes)
            ops, stopped = decode(codes, tab[number], ends[tab[number]], sizes)
            res = (ops, stopped, True)
        cache_prog[key] = res
        return res

    types = collections.defaultdict(lambda: {
        "recs": 0, "load_spawn": 0, "script_spawn": 0, "no_header": 0,
        "maps": collections.Counter(), "slot_native": collections.Counter(), "slot_scripted": collections.Counter(),
        "native_keys": collections.Counter(), "native_keys_maps": collections.defaultdict(set), "null_keys": collections.Counter(),
        "scripted_progs": collections.defaultdict(collections.Counter), "global_table_progs": collections.Counter(),
        "handover": collections.Counter(), "spritecode": collections.Counter(), "decode_stops": 0,
        "levels": collections.Counter(), "record_ids": collections.defaultdict(list), "swap_sites": collections.defaultdict(list),
    })
    keys = collections.defaultdict(lambda: {"recs": 0, "types": collections.Counter(), "maps": set(), "via_handover": collections.Counter()})
    map_edges = collections.defaultdict(set)  # 0x53 destinations
    portal_edges = collections.defaultdict(set)
    totals = {"records": 0, "enabled": 0, "no_header_records": 0}

    for mid in sorted(maps):
        m = maps[mid]
        for p in m["portals"]:
            portal_edges[mid].add(p["dest"])
        # all programs of all tables: 0x53 map changes
        refF = {(e["EventCodesF_InteractIndex"] or 0) & 0x7F for e in m["entities"] if e["IsEnabled"]}
        for letter in LETTERS:
            for number, start in enumerate(m["tables"][letter]):
                if letter == "F" and number not in refF:
                    continue
                if start > 0:
                    ops, stopped, ok = program_ops(mid, letter, number, False)
                    for pc, op, raw in ops:
                        if op == 0x53 and len(raw) >= 3:
                            map_edges[mid].add(raw[1] | (raw[2] << 8))
        for e in m["entities"]:
            totals["records"] += 1
            if not e["IsEnabled"]:
                continue
            totals["enabled"] += 1
            page_map = bool(e["SpriteDirection"] & 0x80)
            t = e["SpriteTableIndex"] | (0x100 if page_map else 0)
            T = types[t]
            T["recs"] += 1
            T["maps"][mid] += 1
            T["record_ids"][mid].append(e["idx"])
            if e["SpriteDirection"] & 0x40:
                T["load_spawn"] += 1
            else:
                T["script_spawn"] += 1
            h = headers.get(t)
            if h is None:
                T["no_header"] += 1
                totals["no_header_records"] += 1
                continue
            for s in SLOTS:
                idx = e[ENT_FIELD[s]] or 0
                code = h[HDR_FIELD[s]]
                if (idx & 0x7F) == 0:
                    T["slot_native"][s] += 1
                    # null entry (C0, D0, F0 and the unused ones) = no call at all in the dispatch (beqz at 0x80038934)
                    key = (s, code)
                    T["native_keys"][key] += 1
                    T["native_keys_maps"][key].add(mid)
                    keys[key]["recs"] += 1
                    keys[key]["types"][t] += 1
                    keys[key]["maps"].add(mid)
                else:
                    T["slot_scripted"][s] += 1
                    from_global = (idx & 0x80) == 0
                    if from_global:
                        T["global_table_progs"][(s, idx & 0x7F)] += 1
                    T["scripted_progs"][s][idx & 0x7F] += 1
                    ops, stopped, ok = program_ops(mid, LETTERS[s], idx & 0x7F, from_global)
                    if stopped:
                        T["decode_stops"] += 1
                    for pc, op, raw in ops:
                        if op == 0x40 and len(raw) == 3:
                            T["handover"][(s, raw[1], raw[2])] += 1
                        elif op == 0x41 and len(raw) == 3:
                            T["spritecode"][(s, raw[1], raw[2])] += 1
                            T["swap_sites"][(s, raw[1], raw[2])].append((mid, e["idx"]))

    # hand-over to a native slot (0x40 [slot, 0] from a scripted program): the key (slot, header code) of the type is reachable
    for t, T in types.items():
        h = headers.get(t)
        if not h:
            continue
        for (frm, slot, val), n in T["handover"].items():
            if slot in SLOTS and (val & 0x7F) == 0:
                keys[(slot, h[HDR_FIELD[slot]])]["via_handover"][t] += n

    for t, T in types.items():
        for (frm, slot, val), n in T["spritecode"].items():
            if slot in SLOTS and val > 0:
                keys[(slot, val)]["via_swap"] = keys[(slot, val)].get("via_swap", collections.Counter())
                keys[(slot, val)]["via_swap"][t] += n

    with open(os.path.join(common.HERE, "cache", "census.pkl"), "wb") as fh:
        pickle.dump({"types": {t: dict(v) for t, v in types.items()}, "keys": {k: dict(v) for k, v in keys.items()},
                     "headers": headers, "map_edges": dict(map_edges), "portal_edges": dict(portal_edges), "totals": totals}, fh)
    print("records", totals["records"], "enabled", totals["enabled"], "types seen", len(types), "native keys", len(keys),
          "records without header", totals["no_header_records"])


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
