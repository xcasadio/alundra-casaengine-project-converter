#!/usr/bin/env python
"""Try walker variants to see whether one reproduces the plan's old figure (111 136 reachable instructions, 2026-09-29). Label script2. Read-only."""
import json, os, re, itertools, collections

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
PROJECT = REPO + '/alundra-project'
src = open(REPO + '/Alundra/Scripts/EventOpcodeSizeTable.cs', encoding='utf-8').read()
SIZES = {int(m.group(1), 16): int(m.group(2)) for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),', src)}
SL = 'ABCDEF'

def s16(c, lo, hi):
    n = len(c)
    v = (c[lo] if 0 <= lo < n else 0) | ((c[hi] if 0 <= hi < n else 0) << 8)
    return v - 0x10000 if v >= 0x8000 else v

def walk(codes, entry, extra, v):
    n = len(codes); seen = {}; marks = set(); ret = set(); work = [entry]
    while True:
        while work:
            pc = work.pop()
            while 0 <= pc < n and pc not in seen:
                op = codes[pc]; seen[pc] = op
                size = SIZES.get(op)
                if size is None or size == 0 or op == 0xFF:
                    break
                jumped = False; stop = False
                if op == 0x02: pc += s16(codes, pc + 1, pc + 2); jumped = True
                elif op in (0x03, 0x04): work.append(pc + s16(codes, pc + 1, pc + 2))
                elif op in (0x30, 0x31): work.append(pc + s16(codes, pc + 3, pc + 4))
                elif op == 0x74 and v['f74']: work.append(pc + s16(codes, pc + 1, pc + 2))
                elif op in (0x57, 0x58) and v['dir']:
                    for d in range(4): work.append(pc + s16(codes, pc + 1 + 2 * d, pc + 2 + 2 * d))
                    stop = True
                elif op == 0x78 and v['marks']: marks.add(pc + 3); pc += s16(codes, pc + 1, pc + 2); jumped = True
                elif op == 0x79 and v['marks']: marks.add(pc + 3); work.append(pc + s16(codes, pc + 1, pc + 2))
                elif op in (0x7B, 0x7C) and v['marks']: marks.add(pc + 5); work.append(pc + s16(codes, pc + 3, pc + 4))
                elif op == 0x7D and v['marks']: ret.add(pc); stop = True
                elif op in (0x7E, 0x7F, 0x80, 0x81) and v['marks']: ret.add(pc)
                elif op == 0x49: pc = entry; jumped = True
                elif op == 0x4B: work.append(entry)
                elif op == 0x40 and v['x40']:
                    b1 = codes[pc + 1] if pc + 1 < n else 0; b2 = codes[pc + 2] if pc + 2 < n else 0
                    if 2 <= b1 <= 5: extra.add((SL[b1], b2 & 0x7F))
                if stop: break
                if not jumped: pc += size
        if ret:
            fresh = [m for m in marks if m not in seen]
            if fresh: work.extend(fresh); continue
        break
    return seen

index = json.load(open(PROJECT + '/Maps/world-index.json', encoding='utf-8'))
data = []
for mid, rel in index.items():
    rel = rel.replace('\\', '/'); folder = os.path.dirname(PROJECT + '/' + rel); base = os.path.splitext(os.path.basename(rel))[0]
    doc = json.load(open(folder + '/events/' + base + '.events.json', encoding='utf-8'))
    tms = [f for f in os.listdir(folder + '/tilemap') if f.endswith('.tileMap')]
    tm = json.load(open(folder + '/tilemap/' + tms[0], encoding='utf-8'))
    roots = []
    def add(slot, raw, doc=doc, roots=roots):
        raw = int(raw); i = raw & 0x7F; t = doc['EventCodes%sTable' % slot]
        if i and i < len(t) and t[i]: roots.append((slot, i))
    for l in tm['object_layers']:
        if l['name'] == 'MapEvents':
            for o in l['objects']: add('B', o['custom_properties']['EventCodesBIndex'])
        if l['name'] == 'Entities':
            for o in l['objects']:
                p = o['custom_properties']
                add('A', p['EventCodesA_LoadIndex']); add('C', p['EventCodesC_TickIndex']); add('D', p['EventCodesD_TouchIndex'])
                add('E', p['EventCodesE_DeactivateIndex']); add('F', p['EventCodesF_InteractIndex'])
    data.append((int(mid), doc, roots))

for f74, dirv, marks, x40 in itertools.product([True, False], [True, False], [True, False], [True, False]):
    v = {'f74': f74, 'dir': dirv, 'marks': marks, 'x40': x40}
    sp = 0; up = 0; uprog = 0
    for mid, doc, roots in data:
        codes = doc['Codes']; done = set(); todo = list(roots); qi = 0; pcs = set()
        while qi < len(todo):
            s, i = todo[qi]; qi += 1
            if (s, i) in done: continue
            done.add((s, i)); extra = set()
            seen = walk(codes, doc['EventCodes%sTable' % s][i], extra, v)
            sp += len(seen)
            for pc in seen: pcs.add(pc)
            for (os_, oi) in extra:
                t = doc['EventCodes%sTable' % os_]
                if (os_, oi) not in done and oi < len(t) and t[oi] != 0: todo.append((os_, oi))
        up += len(pcs)
    print(v, 'site-programs', sp, 'unique (map,pc)', up)
