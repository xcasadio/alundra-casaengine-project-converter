#!/usr/bin/env python3
"""Over-approximate reachability of liftable footprints by scripted walkers, per map. Read-only.

For a map: S = start points = positions of every record (liftable ones excluded) + every placement (0x64 with x/y given, 0x8A) of the event
programs; R(program) = sum of the walk radii (0x0B r, 0x1E/0x1F d) of the program, swept linearly (loops ignored: a looping patrol could go further,
reported as 'looping' when the program contains a backward goto). A walker starting at p can cross a liftable L only if
chebyshev_gap(p, L) < R + 24 (24 px = half tile slack for diagonal walks, a walk of d px advances up to d px on EACH axis for a diagonal direction).
Output per map: the smallest gap between any start point and any liftable, the largest R, and the verdict 'cannot' (gap >= Rmax+24) or 'maybe'.
This is a static bound, not a simulation (INFERENCE).

Usage: reach_check.py MAP [MAP ...]
"""
import json, sys
sys.path.insert(0, '.')
import decode_events as de

rows = json.load(open('all_records.json', encoding='utf-8'))


def gap(p, box):
    """Chebyshev gap between the point p=(x,y) (taken as the hero/NPC centre) and the box (x0,x1,y0,y1), counting the walker half-extent 10 px."""
    x, y = p
    dx = max(box[0] - 10 - x, 0, x - (box[1] + 10))
    dy = max(box[2] - 7 - y, 0, y - (box[3] + 7))
    return max(dx, dy)


def main():
    for m in [int(a) for a in sys.argv[1:]]:
        recs = [r for r in rows if r['map'] == m]
        lifts = [r for r in recs if r['kind'] != 0]
        boxes = [(r['x'] + r['offx'], r['x'] + r['offx'] + r['sx'], r['y'] + r['offy'], r['y'] + r['offy'] + r['sy']) for r in lifts]
        starts = [(r['x'], r['y'], f"rec{r['idx']}") for r in recs if r['kind'] == 0]
        doc = de.load_map(m)
        rmax = 0
        loops = False
        placements = []
        if doc:
            for slot, i, s, e in de.all_programs(doc):
                total = 0
                for pc, op, v, bad in de.sweep(doc, s, e):
                    if bad:
                        break
                    if op == 0x0B:
                        total += v[2] | v[3] << 8
                    elif op in (0x1E, 0x1F):
                        total += v[1] | v[2] << 8
                    elif op == 0x64 and v[1] < 0x80:
                        placements.append((v[2] | v[3] << 8, v[4] | v[5] << 8, f'0x64@{pc} sel{v[1]}'))
                    elif op == 0x64:
                        placements.append((v[2] | v[3] << 8, v[4] | v[5] << 8, f'0x64@{pc} sel{v[1]}'))
                    elif op == 0x8A:
                        placements.append((v[2] | v[3] << 8, v[4] | v[5] << 8, f'0x8A@{pc}'))
                    elif op in (0x02, 0x03, 0x04):
                        tgt = v[1] | v[2] << 8
                        if tgt <= pc:
                            loops = True
                rmax = max(rmax, total)
        pts = starts + placements
        best = None
        for (x, y, why) in pts:
            for b, r in zip(boxes, lifts):
                g = gap((x, y), b)
                if best is None or g < best[0]:
                    best = (g, why, (x, y), r['idx'])
        verdict = 'no liftable' if not lifts else ('cannot' if best and best[0] >= rmax + 24 else 'maybe')
        print(f'map {m}: liftables={len(lifts)} starts={len(pts)} Rmax(program sum)={rmax}px loopingProgram={loops} '
              f'minGap={best[0] if best else None} at {best[1:] if best else None} -> {verdict}')


if __name__ == '__main__':
    main()
