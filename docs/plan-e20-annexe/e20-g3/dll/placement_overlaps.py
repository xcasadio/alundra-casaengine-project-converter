#!/usr/bin/env python3
"""Scripted placements (0x64 with a record or hero selector, 0x8A spawn at x/y) that land a box on a liftable footprint of the same map (X, Y and Z overlap).

0x64 [sel, x16, y16, z16] places the entity found by selector sel at (x, y, z) px (EventProgramRunner.cs SetEntitiesPosition, ~2511-2526); sel < 0x80 = the record sel of the
map, 0x81 = the hero. 0x8A [rec, x16, y16, ...] spawns the record rec at (x, y). The box is the record's own header box (hero: 21 x 15 x 32, offsets -10 -7 0).
Z = the operand (px). A placement Z of 0 or below the ground is raised by ClampToGround at run time (not modelled): a Z overlap is only 'a mesurer' if the operand Z is inside.
Programs are swept linearly (both branches), so a site can be dormant (INFERENCE).
"""
import glob, json, re, sys
sys.path.insert(0, '.')
import decode_events as de

rows = json.load(open('all_records.json', encoding='utf-8'))
byrec = {(r['map'], r['idx']): r for r in rows}
lifts = {}
for r in rows:
    if r['kind'] != 0:
        lifts.setdefault(r['map'], []).append(r)
CHAIN = {389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185}

hits = []
n = 0
for f in glob.glob(de.ROOT + '/alundra-project/Maps/*/*/events/*.events.json'):
    f = f.replace(chr(92), '/')
    m = re.search(r'-(\d+)\.events\.json$', f)
    if not m:
        continue
    mp = int(m.group(1))
    if mp not in lifts:
        continue
    doc = json.load(open(f, encoding='utf-8'))
    for slot, i, s, e in de.all_programs(doc):
        for pc, op, v, bad in de.sweep(doc, s, e):
            if bad:
                break
            if op == 0x64:
                sel, x, y, z = v[1], v[2] | v[3] << 8, v[4] | v[5] << 8, v[6] | v[7] << 8
            elif op == 0x8A:
                sel, x, y, z = v[1], v[2] | v[3] << 8, v[4] | v[5] << 8, v[6] | v[7] << 8
            else:
                continue
            n += 1
            if sel == 0x81:
                ox, oy, sx, sy, sz, oz = -10, -7, 21, 15, 32, 0
            elif (mp, sel) in byrec:
                r = byrec[(mp, sel)]
                ox, oy, sx, sy, sz, oz = r['offx'], r['offy'], r['sx'], r['sy'], r['sz'], r['offz']
            else:
                continue
            bx = (x + ox, x + ox + sx, y + oy, y + oy + sy, z + oz, z + oz + sz)
            for L in lifts[mp]:
                if byrec.get((mp, sel)) is L:
                    continue
                lb = (L['x'] + L['offx'], L['x'] + L['offx'] + L['sx'], L['y'] + L['offy'], L['y'] + L['offy'] + L['sy'], L['z'] + L['offz'], L['z'] + L['offz'] + L['sz'])
                if bx[0] < lb[1] and lb[0] < bx[1] and bx[2] < lb[3] and lb[2] < bx[3]:
                    zov = bx[4] < lb[5] and lb[4] < bx[5]
                    hits.append((mp, slot, i, pc, hex(op), sel, (x, y, z), L['idx'], zov))
print('placement sites (0x64/0x8A) examined on maps with liftables:', n)
print('XY overlaps with a liftable footprint:', len(hits))
for h in sorted(set(hits)):
    print('  map %d%s slot %s[%d] @%d %s sel=%d at (%d,%d,%d) overlaps rec%d in XY; Z overlap with operand Z: %s' % (
        h[0], ' [CHAIN]' if h[0] in CHAIN else '', h[1], h[2], h[3], h[4], h[5], h[6][0], h[6][1], h[6][2], h[7], h[8]))
