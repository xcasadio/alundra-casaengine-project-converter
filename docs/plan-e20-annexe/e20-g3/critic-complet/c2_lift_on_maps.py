#!/usr/bin/env python3
"""critic-complet c2: the liftable records (kind != 0) of the maps given on the command line, with their XY box and Z (census of
../dll/all_records.json: X = 12*XPos+12, Y = 8*YPos+8, Z = 8*Height px; box [pos+off, pos+off+size)). Also lists every record of
the map within R px (XY box gap) of a given point when '--near x,y,R' is passed. Read-only, stdout."""
import json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
recs = json.load(open(os.path.join(HERE, '..', 'dll', 'all_records.json'), encoding='utf-8'))
args = sys.argv[1:]
near = None
if '--near' in args:
    i = args.index('--near')
    near = tuple(float(v) for v in args[i + 1].split(','))
    args = args[:i] + args[i + 2:]
maps = [int(a) for a in args]


def box(r):
    x0, y0 = r['x'] + r['offx'], r['y'] + r['offy']
    return x0, y0, x0 + r['sx'], y0 + r['sy']


for mp in maps:
    rows = [r for r in recs if r['map'] == mp]
    print('== map %d : %d records, %d liftable' % (mp, len(rows), sum(1 for r in rows if r['kind'])))
    for r in rows:
        x0, y0, x1, y1 = box(r)
        tag = 'LIFT k%d' % r['kind'] if r['kind'] else '      '
        show = r['kind'] != 0
        gap = None
        if near:
            px, py, R = near
            dx = max(x0 - px, 0, px - x1)
            dy = max(y0 - py, 0, py - y1)
            gap = max(dx, dy)
            show = show or gap <= R
        if show:
            print('  rec%-3d %s coll=%d flags=0x%06X %-28s pos=(%d,%d,z%d) box X[%d,%d) Y[%d,%d) Z[%d,%d)%s' % (
                r['idx'], tag, r['collidable'], r['flags'], r['name'][:28], r['x'], r['y'], r['z'], x0, x1, y0, y1,
                r['z'] + r['offz'], r['z'] + r['offz'] + r['sz'], '' if gap is None else '  gap=%.0f' % gap))
