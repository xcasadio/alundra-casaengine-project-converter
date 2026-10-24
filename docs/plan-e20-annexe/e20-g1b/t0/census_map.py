#!/usr/bin/env python3
"""E20.g1b T0 (a): the layout of one candidate map: arrivals (portals of any map that lead to it), wired liftables, platforms, tops of 16 px,
scan of the lock ops (0x10) in every program. Read-only. Usage: python -B census_map.py MAP [MAP ...]"""
import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import census_recipe as cr, decode_events as de
files = cr.tilemaps()
def portals_to(target):
    out = []
    for m, f in files.items():
        d = json.load(open(f, encoding='utf-8'))
        for l in d['object_layers']:
            if l['name'] == 'Portals':
                for o in l['objects']:
                    p = o['custom_properties']
                    if int(p['DestMapId']) == target:
                        out.append((m, int(p['Index']), int(p['DestTileX']) * 12 + 12, int(p['DestTileY']) * 8 + 8, p['Flags']))
    return out
for m in [int(a) for a in sys.argv[1:]]:
    print('######## map %d' % m)
    for src, i, x, y, fl in portals_to(m):
        print('  arrival from map %d portal %d -> (%d, %d) flags %s' % (src, i, x, y, fl))
    rows = [cr.row(p, h) for p, h in cr.placements(files[m])]
    for r in rows:
        if not r['en']: continue
        tag = None
        if r['kind'] and r['hdr'][1] == 23: tag = 'LIFT-C23 kind%d' % r['kind']
        elif r['kind']: tag = 'lift-other kind%d' % r['kind']
        elif cr.PLAT.search(r['name'] or ''): tag = 'PLATFORM'
        elif r['coll'] and r['sz'] == 16: tag = 'top16'
        if tag:
            print('  %-16s ent %2d %-30s pos (%d,%d,%d) box off (%d,%d,%d) size (%d,%d,%d) coll %s prog %s hdr %s' % (tag, r['idx'], (r['name'] or '')[:30], r['x'], r['y'], r['z'], r['ox'], r['oy'], r['oz'], r['sx'], r['sy'], r['sz'], r['coll'], r['prog'], r['hdr']))
    doc = de.load_map(m)
    locks = []
    for slot, i, st, en in de.all_programs(doc):
        for pc, op, v, bad in de.sweep(doc, st, en):
            if op in (0x10, 0x11) and v is not None: locks.append('%s%d@%d:%02X' % (slot, i, pc, op))
    print('  lock/gain ops in the programs:', locks)
