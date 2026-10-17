#!/usr/bin/env python3
"""critic-complet c11: filters c10_walkers_near.out: keeps the walkers that are Collidable themselves (a non-collidable mover is never a subject:
EntitySupport.IsEligibleSubject, EntitySupport.cs:73-76; binary 0x80036F74, ../bin/report.md A1.2) and whose Z range [z+offz, z+offz+sz) overlaps
the Z range of a liftable within sumR + 24 px in XY (record Z = 8 * Height, approximate: no ground clamp). Read-only, stdout utf-8."""
import json, os, re, sys
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
R = json.load(open(os.path.join(HERE, '..', 'dll', 'all_records.json'), encoding='utf-8'))
by = {(r['map'], r['idx']): r for r in R}
CHAIN = {389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185}
def gapxy(a, b):
    ax0, ay0 = a['x'] + a['offx'], a['y'] + a['offy']; ax1, ay1 = ax0 + a['sx'], ay0 + a['sy']
    bx0, by0 = b['x'] + b['offx'], b['y'] + b['offy']; bx1, by1 = bx0 + b['sx'], by0 + b['sy']
    return max(bx0 - ax1, ax0 - bx1, by0 - ay1, ay0 - by1, 0)
def zr(r):
    return r['z'] + r['offz'], r['z'] + r['offz'] + r['sz']
kept = []
for line in open(os.path.join(HERE, 'c10_walkers_near.out'), encoding='utf-8'):
    m = re.match(r'\s+map (\d+) rec(\d+) .* sumR=(\d+) gap=(\d+)', line)
    if not m:
        continue
    mp, idx, sumr = int(m.group(1)), int(m.group(2)), int(m.group(3))
    w = by[(mp, idx)]
    if not w['collidable']:
        continue
    wz0, wz1 = zr(w)
    best = None
    for r in R:
        if r['map'] != mp or not r['kind'] or not r['collidable']:
            continue
        z0, z1 = zr(r)
        if z0 >= wz1 or wz0 >= z1:
            continue
        g = gapxy(w, r)
        if g < sumr + 24 and (best is None or g < best[0]):
            best = (g, r['idx'], r['name'], (r['x'], r['y'], r['z']))
    if best:
        kept.append((mp, 'CHAIN' if mp in CHAIN else '', idx, w['name'][:26], (w['x'], w['y'], w['z']), sumr, best))
print('collidable walkers with a Z-overlapping liftable within reach: %d' % len(kept))
for k in kept:
    print('  map %d %s rec%d %-26s at %s sumR=%d -> gap %d to rec%d %s at %s' % (k[0], k[1], k[2], k[3], k[4], k[5], k[6][0], k[6][1], k[6][2], k[6][3]))
