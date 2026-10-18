"""Read-only: which skipped sites of the corpus are reached ONLY through roots whose program index has the bit 0x80 clear
(global map_alundra table in the binary, the map's own table in the DLL fallback and in the census)."""
import collections
import json
import os
import re

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
P = REPO + '/alundra-project'
SLOTS = 'ABCDEF'

idx = json.load(open(P + '/Maps/world-index.json', encoding='utf-8'))
sizes_src = open(REPO + '/Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()
SIZES = {int(m.group(1), 16): int(m.group(2)) for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),', sizes_src)}


def s16(codes, lo, hi):
    n = len(codes)
    b0 = codes[lo] if 0 <= lo < n else 0
    b1 = codes[hi] if 0 <= hi < n else 0
    v = b0 | (b1 << 8)
    return v - 0x10000 if v >= 0x8000 else v


def walk_program(codes, entry, extra):
    # same rules as the census
    count = len(codes)
    seen = {}
    marks = set()
    users = set()
    work = [entry]
    while True:
        while work:
            pc = work.pop()
            while 0 <= pc < count and pc not in seen:
                op = codes[pc]
                seen[pc] = op
                size = SIZES.get(op, 0)
                if size == 0 or op == 0xFF:
                    break
                jumped = False
                stop = False
                if op == 0x02:
                    pc += s16(codes, pc + 1, pc + 2)
                    jumped = True
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
                    pc += s16(codes, pc + 1, pc + 2)
                    jumped = True
                elif op == 0x79:
                    marks.add(pc + 3)
                    work.append(pc + s16(codes, pc + 1, pc + 2))
                elif op in (0x7B, 0x7C):
                    marks.add(pc + 5)
                    work.append(pc + s16(codes, pc + 3, pc + 4))
                elif op == 0x7D:
                    users.add(pc)
                    stop = True
                elif op in (0x7E, 0x7F, 0x80, 0x81):
                    users.add(pc)
                elif op == 0x49:
                    pc = entry
                    jumped = True
                elif op == 0x4B:
                    work.append(entry)
                elif op == 0x40:
                    b1 = codes[pc + 1] if pc + 1 < count else 0
                    b2 = codes[pc + 2] if pc + 2 < count else 0
                    if 2 <= b1 <= 5:
                        extra.add((SLOTS[b1], b2 & 0x7F))
                if stop:
                    break
                if not jumped:
                    pc += size
        if users:
            fresh = [m for m in marks if m not in seen]
            if fresh:
                work.extend(fresh)
                continue
        break
    return seen


skipped = {0x15, 0x3C, 0x3D, 0x56, 0x5F, 0x60, 0x61, 0x66, 0x6A, 0x6B, 0x6C, 0x6D, 0x72, 0x7A, 0x88, 0x96, 0x97, 0x9E, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB9, 0xBB, 0xC2}

only_clear_sites = collections.Counter()
total_clear_roots = collections.Counter()
for mid_s, rel in sorted(idx.items(), key=lambda kv: int(kv[0])):
    rel = rel.replace(chr(92), '/')
    folder = os.path.dirname(P + '/' + rel)
    base = os.path.splitext(os.path.basename(rel))[0]
    ev = json.load(open(folder + '/events/' + base + '.events.json', encoding='utf-8'))
    tmn = [f for f in os.listdir(folder + '/tilemap') if f.endswith('.tileMap')][0]
    d = json.load(open(folder + '/tilemap/' + tmn, encoding='utf-8'))
    codes = ev['Codes']
    set_roots = []
    clear_roots = []

    def add(slot, raw):
        raw = int(raw)
        i = raw & 0x7F
        t = ev['EventCodes%sTable' % slot]
        if i == 0 or not (i < len(t) and t[i] != 0):
            return
        (set_roots if raw & 0x80 else clear_roots).append((slot, i))

    for layer in d['object_layers']:
        if layer['name'] == 'MapEvents':
            for o in layer['objects']:
                add('B', o['custom_properties']['EventCodesBIndex'])
        if layer['name'] == 'Entities':
            for o in layer['objects']:
                p = o['custom_properties']
                add('A', p['EventCodesA_LoadIndex'])
                add('C', p['EventCodesC_TickIndex'])
                add('D', p['EventCodesD_TouchIndex'])
                add('E', p['EventCodesE_DeactivateIndex'])
                add('F', p['EventCodesF_InteractIndex'])

    def reach(roots):
        out = set()
        done = set()
        todo = list(roots)
        while todo:
            s, i = todo.pop()
            if (s, i) in done:
                continue
            done.add((s, i))
            extra = set()
            seen = walk_program(codes, ev['EventCodes%sTable' % s][i], extra)
            for pc, op in seen.items():
                out.add((s, i, pc, op))
            for (os_, oi) in extra:
                t = ev['EventCodes%sTable' % os_]
                if (os_, oi) not in done and oi < len(t) and t[oi] != 0:
                    todo.append((os_, oi))
        return out

    if clear_roots:
        total_clear_roots[int(mid_s)] += len(clear_roots)
        all_sites = reach(set_roots + clear_roots)
        with_set_only = reach(set_roots)
        diff = all_sites - with_set_only
        n_skip = sum(1 for (s, i, pc, op) in diff if op in skipped)
        print(sorted((s,i,pc,'%02X'%op) for (s,i,pc,op) in diff if op in skipped))
        if n_skip:
            only_clear_sites[int(mid_s)] = n_skip
        print('map', mid_s, 'clear-bit roots', clear_roots, 'sites reached only by them', len(diff), 'skipped among them', n_skip)

print('maps with clear-bit roots', dict(total_clear_roots))
print('skipped sites reached ONLY via clear-bit roots', dict(only_clear_sites), sum(only_clear_sites.values()))
