#!/usr/bin/env python
"""Corpus census of the event-program opcodes (label script2).

Port of Alundra.Tests/AlundraStoryChainOpcodeAudit.cs (WalkMap/WalkProgram/ReadRoots) applied to the 483 exported maps.
Read-only on the repository. Output: census.json (+ census-sites.tsv) in the script directory.

Usage: python -B census.py
"""
import json, os, sys, re, collections

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
PROJECT = REPO + '/alundra-project'
OUT = r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-state/sm0/verify/census'
SLOTS = 'ABCDEF'

# ---- size table (parsed from EventOpcodeSizeTable.cs, not retyped) -------------------------------------------------
def load_sizes():
    src = open(REPO + '/Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()
    sizes, names = {}, {}
    for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)\s*\}', src):
        sizes[int(m.group(1), 16)] = int(m.group(2))
        names[int(m.group(1), 16)] = m.group(3)
    return sizes, names

SIZES, NAMES = load_sizes()


def s16(codes, lo, hi):
    n = len(codes)
    b0 = codes[lo] if 0 <= lo < n else 0
    b1 = codes[hi] if 0 <= hi < n else 0
    v = b0 | (b1 << 8)
    return v - 0x10000 if v >= 0x8000 else v


def walk_program(codes, entry, extra_roots):
    """Port of WalkProgram. Returns {pc: opcode}."""
    n = len(codes)
    seen = {}
    marks = set()
    return_users = set()
    work = [entry]
    while True:
        while work:
            pc = work.pop()
            while 0 <= pc < n and pc not in seen:
                op = codes[pc]
                seen[pc] = op
                size = SIZES.get(op)
                if size is None or size == 0 or op == 0xFF:
                    break
                jumped = False
                stop = False
                if op == 0x02:
                    pc += s16(codes, pc + 1, pc + 2); jumped = True
                elif op in (0x03, 0x04):
                    work.append(pc + s16(codes, pc + 1, pc + 2))
                elif op in (0x30, 0x31):
                    work.append(pc + s16(codes, pc + 3, pc + 4))
                elif op == 0x74:
                    work.append(pc + s16(codes, pc + 1, pc + 2))
                elif op in (0x57, 0x58):
                    for d in range(4):
                        work.append(pc + s16(codes, pc + 1 + 2 * d, pc + 2 + 2 * d))
                    stop = True
                elif op == 0x78:
                    marks.add(pc + 3)
                    pc += s16(codes, pc + 1, pc + 2); jumped = True
                elif op == 0x79:
                    marks.add(pc + 3)
                    work.append(pc + s16(codes, pc + 1, pc + 2))
                elif op in (0x7B, 0x7C):
                    marks.add(pc + 5)
                    work.append(pc + s16(codes, pc + 3, pc + 4))
                elif op == 0x7D:
                    return_users.add(pc); stop = True
                elif op in (0x7E, 0x7F, 0x80, 0x81):
                    return_users.add(pc)
                elif op == 0x49:
                    pc = entry; jumped = True
                elif op == 0x4B:
                    work.append(entry)
                elif op == 0x40:
                    b1 = codes[pc + 1] if pc + 1 < n else 0
                    b2 = codes[pc + 2] if pc + 2 < n else 0
                    if 2 <= b1 <= 5:
                        extra_roots.add((SLOTS[b1], b2 & 0x7F))
                if stop:
                    break
                if not jumped:
                    pc += size
        if return_users:
            fresh = [m for m in marks if m not in seen]
            if fresh:
                work.extend(fresh)
                continue
        break
    return seen


def table_for(doc, slot):
    return doc['EventCodes%sTable' % slot]


def walk_map(doc, roots):
    codes = doc['Codes']
    reached = []  # (slot, index, pc, op)
    done = set()
    todo = list(roots)
    qi = 0
    while qi < len(todo):
        slot, index = todo[qi]; qi += 1
        if (slot, index) in done:
            continue
        done.add((slot, index))
        extra = set()
        seen = walk_program(codes, table_for(doc, slot)[index], extra)
        for pc in sorted(seen):
            reached.append((slot, index, pc, seen[pc]))
        for (os_, oi) in extra:
            t = table_for(doc, os_)
            if (os_, oi) not in done and oi < len(t) and t[oi] != 0:
                todo.append((os_, oi))
    return reached


def prop_int(v):
    return int(v) if not isinstance(v, int) else v


def read_roots(tilemap, doc, stats):
    roots = []

    def add(slot, raw, source):
        raw = prop_int(raw)
        idx = raw & 0x7F
        t = table_for(doc, slot)
        key = (slot, source)
        if raw != 0:
            stats['raw_nonzero'][key] += 1
            if raw & 0x80:
                stats['bit80_set'][key] += 1
            else:
                stats['bit80_clear_nonzero'][key] += 1
        if idx == 0:
            if raw != 0:
                stats['native_only_raw_nonzero'][key] += 1  # bit 0x80 alone: index 0 -> sprite AI
            return
        if idx < len(t) and t[idx] != 0:
            roots.append((slot, idx))
        else:
            stats['root_not_in_table'][key] += 1

    for layer in tilemap['object_layers']:
        if layer['name'] == 'MapEvents':
            for o in layer['objects']:
                add('B', o['custom_properties']['EventCodesBIndex'], 'mapevent')
    for layer in tilemap['object_layers']:
        if layer['name'] == 'Entities':
            for o in layer['objects']:
                p = o['custom_properties']
                add('A', p['EventCodesA_LoadIndex'], 'entity')
                add('B', p.get('EventCodesB_MapIndex', 0), 'entity')
                add('C', p['EventCodesC_TickIndex'], 'entity')
                add('D', p['EventCodesD_TouchIndex'], 'entity')
                add('E', p['EventCodesE_DeactivateIndex'], 'entity')
                add('F', p['EventCodesF_InteractIndex'], 'entity')
    return roots


def main():
    index = json.load(open(PROJECT + '/Maps/world-index.json', encoding='utf-8'))
    result = {'maps': {}, 'stats': {}}
    stats = {k: collections.Counter() for k in ('raw_nonzero', 'bit80_set', 'bit80_clear_nonzero', 'native_only_raw_nonzero', 'root_not_in_table')}
    sites_rows = []
    op0x40 = []  # (map, slot, prog, pc, v1, v2)
    operand_rows = []  # (map, slot, prog, pc, op, bytes) for ops whose operands matter
    maps_loaded = 0
    for mid_s, rel in sorted(index.items(), key=lambda kv: int(kv[0])):
        mid = int(mid_s)
        rel = rel.replace('\\', '/')
        folder = os.path.dirname(PROJECT + '/' + rel)
        base = os.path.splitext(os.path.basename(rel))[0]
        ev = folder + '/events/' + base + '.events.json'
        tm_dir = folder + '/tilemap'
        if not os.path.exists(ev):
            result['maps'][mid] = {'error': 'no events file'}
            continue
        doc = json.load(open(ev, encoding='utf-8'))
        tms = [f for f in os.listdir(tm_dir) if f.endswith('.tileMap')] if os.path.isdir(tm_dir) else []
        if not tms:
            result['maps'][mid] = {'error': 'no tileMap'}
            continue
        tilemap = json.load(open(tm_dir + '/' + tms[0], encoding='utf-8'))
        roots = read_roots(tilemap, doc, stats)
        reached = walk_map(doc, roots)
        maps_loaded += 1
        codes = doc['Codes']
        per_op = collections.Counter()
        pcs = {}
        for slot, idx, pc, op in reached:
            per_op[op] += 1
            pcs.setdefault(pc, set()).add(op)
            sites_rows.append((mid, slot, idx, pc, op))
            if op == 0x40:
                v = codes[pc + 1:pc + 3]
                op0x40.append((mid, slot, idx, pc, v[0] if len(v) > 0 else -1, v[1] if len(v) > 1 else -1))
            if op in (0x82, 0xA4, 0x2F, 0x53, 0x95, 0x87, 0x1E, 0x57, 0x58, 0xAF, 0xB0, 0x44, 0x22, 0x23):
                size = SIZES.get(op, 1)
                operand_rows.append((mid, slot, idx, pc, op, list(codes[pc + 1:pc + size])))
        result['maps'][mid] = {
            'base': base,
            'codes_len': len(codes),
            'roots': len(roots),
            'reached_site_programs': len(reached),
            'unique_pcs': len(pcs),
            'per_op': {('%02X' % k): v for k, v in sorted(per_op.items())},
            'unique_pc_ops': {('%02X' % k): v for k, v in sorted(collections.Counter(next(iter(s)) for s in pcs.values()).items())},
        }
    result['maps_loaded'] = maps_loaded
    result['stats'] = {k: {'%s/%s' % key: v for key, v in c.items()} for k, c in stats.items()}
    json.dump(result, open(OUT + '/census.json', 'w', encoding='utf-8'), indent=1)
    with open(OUT + '/census-sites.tsv', 'w', encoding='utf-8', newline='\n') as f:
        f.write('map\tslot\tprogram\tpc\topcode\n')
        for r in sites_rows:
            f.write('%d\t%s\t%d\t%d\t%02X\n' % r)
    with open(OUT + '/census-op0x40.tsv', 'w', encoding='utf-8', newline='\n') as f:
        f.write('map\tslot\tprogram\tpc\tv1_slot\tv2_index\n')
        for r in op0x40:
            f.write('%d\t%s\t%d\t%d\t%d\t%d\n' % r)
    with open(OUT + '/census-operands.tsv', 'w', encoding='utf-8', newline='\n') as f:
        f.write('map\tslot\tprogram\tpc\topcode\toperands\n')
        for r in operand_rows:
            f.write('%d\t%s\t%d\t%d\t%02X\t%s\n' % (r[0], r[1], r[2], r[3], r[4], ' '.join('%02X' % b for b in r[5])))
    print('maps loaded', maps_loaded, 'site-programs', len(sites_rows))


main()
