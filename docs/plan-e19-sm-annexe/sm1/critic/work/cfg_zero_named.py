"""Critic: control-flow census of 0x60 0x61 0x3C 0x3D from the NON-ZERO entries of every program table (the annex only roots the
entries that a record or a map event names).  Shows what the closed list does NOT contain, and whether a record/map event names it."""
import re, json, glob, collections, csv, os, sys

root = "D:/development/repo/alundra-casaengine-project-converter"
s = open(root + '/Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8-sig').read()
SZ = {int(a, 16): int(b) for a, b, c in re.findall(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((-?\d+),\s*"([^"]*)"', s)}
TARGET = {0x60, 0x61, 0x3C, 0x3D}
SLOTS = 'ABCDEF'


def s16(c, i):
    v = (c[i] if i < len(c) else 0) | ((c[i + 1] if i + 1 < len(c) else 0) << 8)
    return v - 65536 if v >= 32768 else v


def walk(c, entry):
    n = len(c)
    seen = {}
    work = [entry]
    marks = set()
    ret = False
    while True:
        while work:
            pc = work.pop()
            while 0 <= pc < n and pc not in seen:
                op = c[pc]
                seen[pc] = op
                z = SZ.get(op)
                if z is None or z == 0 or op == 0xFF:
                    break
                nxt = pc + z
                stop = False
                if op == 0x02:
                    nxt = pc + s16(c, pc + 1)
                elif op in (3, 4):
                    work.append(pc + s16(c, pc + 1))
                elif op in (0x30, 0x31):
                    work.append(pc + s16(c, pc + 3))
                elif op == 0x74:
                    work.append(pc + s16(c, pc + 1))
                elif op in (0x57, 0x58):
                    for d in range(4):
                        work.append(pc + s16(c, pc + 1 + 2 * d))
                    stop = True
                elif op == 0x78:
                    marks.add(pc + 3)
                    nxt = pc + s16(c, pc + 1)
                elif op == 0x79:
                    marks.add(pc + 3)
                    work.append(pc + s16(c, pc + 1))
                elif op in (0x7B, 0x7C):
                    marks.add(pc + 5)
                    work.append(pc + s16(c, pc + 3))
                elif op == 0x7D:
                    ret = True
                    stop = True
                elif op in (0x7E, 0x7F, 0x80, 0x81):
                    ret = True
                elif op == 0x49:
                    nxt = entry
                elif op == 0x4B:
                    work.append(entry)
                if stop:
                    break
                pc = nxt
        if ret:
            fresh = [m for m in marks if m not in seen]
            if fresh:
                work.extend(fresh)
                continue
        break
    return seen


idx = json.load(open(root + '/alundra-project/Maps/world-index.json', encoding='utf-8'))
ann = set()
for r in csv.DictReader(open(root + '/docs/plan-e19-sm-annexe/skipped-sites.tsv', encoding='utf-8'), delimiter='\t'):
    if int(r['opcode'], 16) in TARGET:
        ann.add((int(r['map']), int(r['pc'])))

extra = collections.defaultdict(list)
ZERO_NAMED=[]
for mid_s, rel in idx.items():
    mid = int(mid_s)
    rel = rel.replace(chr(92), '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    folder = os.path.dirname(root + '/alundra-project/' + rel)
    d = json.load(open(folder + '/events/' + base + '.events.json', encoding='utf-8-sig'))
    c = d['Codes']
    # records / map events naming each (slot, index)
    named = set()
    tm = glob.glob(folder + '/tilemap/*.tmj')
    t = json.load(open(tm[0], encoding='utf-8-sig'))
    for l in t['layers']:
        if l['name'] == 'MapEvents':
            for o in l['objects']:
                pr = {q['name']: q['value'] for q in o['properties']}
                named.add(('B', pr['EventCodesBIndex'] & 0x7F))
        if l['name'] == 'Entities':
            for o in l['objects']:
                pr = {q['name']: q['value'] for q in o['properties']}
                for sl, key in zip(SLOTS, ('EventCodesA_LoadIndex', 'EventCodesB_MapIndex', 'EventCodesC_TickIndex', 'EventCodesD_TouchIndex', 'EventCodesE_DeactivateIndex', 'EventCodesF_InteractIndex')):
                    if pr.get(key, 0) & 0x7F:
                        named.add((sl, pr[key] & 0x7F))
    for sl in SLOTS:
        tab = d['EventCodes%sTable' % sl]
        for i, off in enumerate(tab):
            if off != 0 or i == 0 or (sl, i) not in named:
                continue
            ZERO_NAMED.append((mid, sl, i))
            seen = walk(c, off)
            for pc, op in seen.items():
                if op in TARGET and (mid, pc) not in ann:
                    extra[(mid, pc, op)].append((sl, i, (sl, i) in named))
print('sites reachable from NON-ZERO table entries but absent from the annex list (distinct map,pc):', len(extra))
bymap = collections.Counter(k[0] for k in extra)
print('maps:', dict(bymap))
anynamed = {k: v for k, v in extra.items() if any(x[2] for x in v)}
print('of which some record or map event names the program:', len(anynamed))
for k, v in sorted(anynamed.items())[:40]:
    print(k, v[:4])

print('named (slot,index) whose table offset is 0 (index != 0):', len(ZERO_NAMED), ZERO_NAMED[:20])
