#!/usr/bin/env python3
"""E20.g1b T0 (a): for every platform-like placement of a map that also has a wired liftable (header ProgramTick 23, kind != 0), the sequence of
Walk segments (direction, pixels) of its tick program C, starting from the cardinal direction of its record (SpriteDirection & 3 -> 0, 0x10, 0x08, 0x18;
0 down, 8 left, 16 up, 24 right) after the direction ops of its load program A (0x08 add, 0x09 set, 0x0A reverse = +16). Linear sweep; a gate (any
wait or test on a flag, an entity, the pad, the hero) is reported. Read-only. Usage: python -B census_dirs.py [MAP ...]"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import census_recipe as cr, decode_events as de
CARD = [0, 0x10, 0x08, 0x18]
HOR = {8, 24}
NAME = {0: 'down', 8: 'left', 16: 'up', 24: 'right'}
def dname(d): return NAME.get(d % 32, 'dir%d' % (d % 32))
files = cr.tilemaps()
only = [int(a) for a in sys.argv[1:]]
for m in (only or sorted(files)):
    props = {}
    rows = []
    for p, h in cr.placements(files[m]):
        r = cr.row(p, h); props[r['idx']] = p; rows.append(r)
    lifts = [r for r in rows if r['kind'] and r['hdr'][1] == 23 and r['en']]
    if not lifts: continue
    doc = de.load_map(m)
    if doc is None: continue
    for r in rows:
        if not cr.PLAT.search(r['name'] or '') or not r['en']: continue
        d = CARD[int(props[r['idx']]['SpriteDirection']) & 3]
        a = cr.prog_of(doc, 'A', r['prog'][0]) if (r['prog'][0] & 0x7f) else None
        if a:
            for pc, op, v in a[2]:
                if op == 0x09: d = v[1]
                elif op == 0x08: d = (d + v[1]) % 32
                elif op == 0x0A: d = (d + 16) % 32
        c = cr.prog_of(doc, 'C', r['prog'][2]) if (r['prog'][2] & 0x7f) else None
        if not c: continue
        segs = []; gates = set(); walks = 0
        for pc, op, v in c[2]:
            if op == 0x09: d = v[1]
            elif op == 0x08: d = (d + v[1]) % 32
            elif op == 0x0A: d = (d + 16) % 32
            elif op in cr.WALK: segs.append((dname(d), v[1] | v[2] << 8)); walks += 1
            elif op in cr.GATE: gates.add(cr.GATE[op])
            elif op in (0x36, 0x3E, 0x37, 0x1C, 0x2E, 0x19, 0x62, 0x63): gates.add('op%02X' % op)
        if not segs: continue
        pure = all(s[0] in ('left', 'right') for s in segs)
        print('map %3d ent %2d %-34s pos %-16s liftables %2d segs %s%s gates %s' % (m, r['idx'], (r['name'] or '')[:34], (r['x'], r['y'], r['z']), len(lifts), segs, ' PURE-HORIZONTAL' if pure else '', sorted(gates)))
