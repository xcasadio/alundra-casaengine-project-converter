#!/usr/bin/env python3
"""Same static check as portal_arrivals.py for the scripted warps 0x53 of every event program of the export.
0x53 operands (EventProgramRunner.cs:1878-1883): v1 | v2 << 8 = destination map index, v3/v4/v5 = destination tile x/y/z, v6 = effect, v7 = sound.
Arrival = tile centre (X = tx*24+12, Y = ty*16+8), as the portals (portal_arrivals.py). Reports XY overlaps with a liftable of the destination map,
and the chain destinations with their nearest-liftable gap. Programs are swept linearly (both branches), so a site may be dormant (INFERENCE).
"""
import glob, json, os, re, sys
sys.path.insert(0, '.')
import decode_events as de

rows = json.load(open('all_records.json', encoding='utf-8'))
lifts = {}
for r in rows:
    if r['kind'] != 0:
        lifts.setdefault(r['map'], []).append(r)

sites = []
for f in glob.glob(de.ROOT + '/alundra-project/Maps/*/*/events/*.events.json'):
    f = f.replace(chr(92), '/')
    m = re.search(r'-(\d+)\.events\.json$', f)
    if not m:
        continue
    src = int(m.group(1))
    doc = json.load(open(f, encoding='utf-8'))
    for slot, i, s, e in de.all_programs(doc):
        for pc, op, v, bad in de.sweep(doc, s, e):
            if bad:
                break
            if op == 0x53:
                sites.append((src, slot, i, pc, v[1] | v[2] << 8, v[3], v[4], v[5]))

print('0x53 sites (linear sweep, programs counted once per slot table entry):', len(sites))
seen = set()
uniq = []
for s in sites:
    key = (s[0], s[3])
    if key not in seen:
        seen.add(key)
        uniq.append(s)
print('distinct (map, pc):', len(uniq))
into = [s for s in uniq if s[4] in lifts]
print('into a map with liftables:', len(into))
over = []
for (src, slot, i, pc, dst, tx, ty, tz) in into:
    ax, ay = tx * 24 + 12, ty * 16 + 8
    for r in lifts[dst]:
        x0 = r['x'] + r['offx']; x1 = x0 + r['sx']; y0 = r['y'] + r['offy']; y1 = y0 + r['sy']
        if ax - 10 < x1 and x0 < ax + 11 and ay - 7 < y1 and y0 < ay + 8:
            over.append((src, pc, dst, (ax, ay), tz, r['idx'], (r['z'] + r['offz'], r['z'] + r['offz'] + r['sz'])))
print('XY overlaps of a scripted arrival with a liftable footprint:', len(over))
for o in over:
    print('  map %d 0x53@%d -> map %d tile-centre (%d,%d) z-level %d overlaps rec%d z[%d,%d)' % (o[0], o[1], o[2], o[3][0], o[3][1], o[4], o[5], o[6][0], o[6][1]))
