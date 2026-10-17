#!/usr/bin/env python3
"""Static check of D-E19-36 (a mover that already overlaps an entity stays blocked) between the map records themselves.

For every map, every pair (A, L) where L is a liftable collidable record and A is another COLLIDABLE record (kind 0 or not), whose boxes overlap in X, Y and Z at spawn
(semi-open [pos+offset, pos+offset+size), Z = Height*8 px from the record, before the ground clamp of the spawn: the Z is therefore approximate).
Two crates of the same stack are flush in Z (no overlap). Reported: pairs where A is not itself a liftable (an NPC, an enemy, a block...), and the count of liftable-liftable pairs.
Chain maps (AlundraStoryChainOpcodeAudit.ChainMaps) are flagged.
"""
import json

rows = json.load(open('all_records.json', encoding='utf-8'))
CHAIN = {389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185}


def box(r):
    x0 = r['x'] + r['offx']; y0 = r['y'] + r['offy']; z0 = r['z'] + r['offz']
    return (x0, x0 + r['sx'], y0, y0 + r['sy'], z0, z0 + r['sz'])


def overlap(a, b):
    return a[0] < b[1] and b[0] < a[1] and a[2] < b[3] and b[2] < a[3] and a[4] < b[5] and b[4] < a[5]


bymap = {}
for r in rows:
    if r['collidable']:
        bymap.setdefault(r['map'], []).append(r)

other = []
ll = 0
for m, rs in bymap.items():
    lifts = [r for r in rs if r['kind'] != 0]
    for L in lifts:
        bl = box(L)
        for A in rs:
            if A is L or A['sx'] <= 0 or A['sy'] <= 0 or A['sz'] <= 0:
                continue
            if overlap(bl, box(A)):
                if A['kind'] != 0:
                    ll += 1
                else:
                    other.append((m, A, L))
print('liftable-liftable overlapping pairs (ordered, both directions counted):', ll)
print('liftable overlapped by a collidable NON-liftable record at spawn (static Z = Height*8):', len(other))
for m, A, L in sorted(other, key=lambda t: (t[0] not in CHAIN, t[0], t[1]['idx'])):
    print('  map %d%s: rec%d %s (%d,%d,%d) overlaps liftable rec%d (%d,%d,%d) progs(C)=%s' % (
        m, ' [CHAIN]' if m in CHAIN else '', A['idx'], ascii(A['name'])[:26], A['x'], A['y'], A['z'], L['idx'], L['x'], L['y'], L['z'], A['progs'][2]))
