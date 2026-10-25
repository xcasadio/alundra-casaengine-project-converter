"""t6_sources.py - every way the data can put item #8 (Objet 008, pickup sprite type 38) in the hero's hands:
  (a) opcode 0x82 [8] (give item), (b) opcodes 0x83 [8] / 0x84 [8] (test / consume), (c) opcode 0x8A / 0x8B spawning an entity record whose SpriteTableIndex is 38,
  (d) entity records placed on the map with SpriteTableIndex 38 (pickup lying there), (e) chests (SpriteTableIndex 30) whose Contents is 8,
  (f) loot: records with Contents == 8 that are not chests (drop on death), (g) the test map (0).
Reads data-extracted/data/map_N.json only. Operand layout of 0x8A/0x8B read from Alundra/Events/EventProgramRunner.cs (v1 = record id)."""
import collections
import glob
import json
import re
import sys

P = r'D:/development/repo/alundra-casaengine-project-converter/'
sizes = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))

TARGET_SPRITE = int(sys.argv[1]) if len(sys.argv) > 1 else 38   # pickup sprite of item 8 (type = item + 0x1E)
TARGET_ITEM = TARGET_SPRITE - 0x1E

files = sorted(glob.glob(P + 'data-extracted/data/map_*.json'), key=lambda s: int(re.search(r'map_(\d+)\.json', s).group(1)) if re.search(r'map_(\d+)\.json', s) else -1)
give, test, use, spawnrec, placed, chests, loot = [], [], [], [], [], [], []
for fp in files:
    mm = re.search(r'map_(\d+)\.json$', fp)
    if not mm:
        continue
    mid = int(mm.group(1))
    d = json.load(open(fp, encoding='utf-8'))
    si = d.get('SpriteInfo')
    if not si:
        continue
    ents = ((si.get('Entities') or {}).get('Entities')) or []
    for idx, e in enumerate(ents):
        if not e:
            continue
        if e.get('SpriteTableIndex') == TARGET_SPRITE and not (e.get('SpriteDirection', 0) & 0x80):
            placed.append((mid, idx, e.get('IsEnabled'), e.get('XPos'), e.get('YPos'), e.get('Height')))
        if e.get('SpriteTableIndex') == 30 and not (e.get('SpriteDirection', 0) & 0x80) and e.get('Contents') == TARGET_ITEM:
            chests.append((mid, idx, e.get('IsEnabled'), e.get('XPos'), e.get('YPos')))
        if not (e.get('SpriteTableIndex') == 30 and not (e.get('SpriteDirection', 0) & 0x80)) and e.get('Contents') == TARGET_ITEM:
            loot.append((mid, idx, e.get('SpriteTableIndex'), e.get('IsEnabled')))
    ec = si.get('EventCodes')
    if not isinstance(ec, dict):
        continue
    codes = ec.get('Codes') or []
    starts = {}
    for k in ('EventCodesATable', 'EventCodesBTable', 'EventCodesCTable', 'EventCodesDTable', 'EventCodesETable', 'EventCodesFTable'):
        for i, o in enumerate(ec.get(k) or []):
            if 0 < o < len(codes):
                starts.setdefault(o, []).append('%s[%d]' % (k[10], i))
    order = sorted(starts)
    for n, s in enumerate(order):
        end = order[n + 1] if n + 1 < len(order) else len(codes)
        pc = s
        while pc < end and pc < len(codes):
            op = codes[pc]
            sz = sizes.get(op, (0, ''))[0]
            if sz <= 0:
                break
            args = codes[pc + 1: pc + sz]
            if op == 0x82 and args[0] == TARGET_ITEM:
                give.append((mid, starts[s], pc))
            elif op == 0x83 and args[0] == TARGET_ITEM:
                test.append((mid, starts[s], pc, args[1]))
            elif op == 0x84 and args[0] == TARGET_ITEM:
                use.append((mid, starts[s], pc))
            elif op in (0x8A, 0x8B):
                rid = args[0]
                if 0 <= rid < len(ents) and ents[rid] and ents[rid].get('SpriteTableIndex') == TARGET_SPRITE and not (ents[rid].get('SpriteDirection', 0) & 0x80):
                    spawnrec.append((mid, starts[s], pc, hex(op), rid, ents[rid].get('IsEnabled')))
            pc += sz

print('target: item %d = pickup sprite type %d' % (TARGET_ITEM, TARGET_SPRITE))
print('(a) 0x82 give  :', give)
print('(b) 0x83 test  :', test)
print('(b) 0x84 use   :', use)
print('(c) 0x8A/0x8B spawning a record of sprite %d:' % TARGET_SPRITE)
for r in spawnrec:
    print('   ', r)
print('(d) records placed with SpriteTableIndex %d (map, index, IsEnabled, x, y, height):' % TARGET_SPRITE)
for r in placed:
    print('   ', r)
print('(e) chests with Contents == %d:' % TARGET_ITEM, chests)
print('(f) non-chest records with Contents == %d (loot on death):' % TARGET_ITEM, loot)
