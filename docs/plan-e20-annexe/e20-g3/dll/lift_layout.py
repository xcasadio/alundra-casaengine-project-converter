#!/usr/bin/env python3
"""Prints, for the given maps, every liftable record (kind != 0) with its centre position, Z (Height*8 px) and its collision box in logical px.
Box = [Pos+Offset, Pos+Offset+Size) on each axis (semi-open, EntityCollision.FindEntityCollisionCandidate: Width = (size<<16)-1, overlap needs delta < Width+1).
Hero box: offsets (-10,-7,0), size 21x15x32 (Alundra.Tests/AlundraDialogueOpcodesProductionTests.cs:350-351).
Usage: lift_layout.py 390 392 ...   (reads all_records.json produced by census_liftables.py --json)
"""
import json, sys

rows = json.load(open('all_records.json', encoding='utf-8'))
maps = [int(a) for a in sys.argv[1:]]
for m in maps:
    rs = [r for r in rows if r['map'] == m and r['kind'] != 0]
    print(f'== map {m}: {len(rs)} liftable records')
    for r in sorted(rs, key=lambda r: r['idx']):
        x0 = r['x'] + r['offx']; x1 = x0 + r['sx']
        y0 = r['y'] + r['offy']; y1 = y0 + r['sy']
        z0 = r['z'] + r['offz']; z1 = z0 + r['sz']
        print(f"  rec{r['idx']:>3} {r['name'][:14]:<14} kind{r['kind']} en={r['enabled']} pos=({r['x']},{r['y']},{r['z']}) "
              f"box x[{x0},{x1}) y[{y0},{y1}) z[{z0},{z1}) progs(A..F)={r['progs']}")
