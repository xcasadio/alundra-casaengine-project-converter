#!/usr/bin/env python3
"""Per map: the non-liftable records whose C (tick) program contains a walk (0x0B / 0x1E / 0x1F), with the walk radii, the record position and the gap
(Chebyshev, px, hero/NPC half extents included) to the nearest liftable footprint. Map programs (B) that walk the hero or an actor are listed too.
Static reading: the SIGN of a walk is not decoded (the direction comes from 0x09/0x08/0x27 before it), so 'reach' = the sum of the radii of the program.
Usage: walkers.py MAP [MAP ...]
"""
import json, sys
sys.path.insert(0, '.')
import decode_events as de

rows = json.load(open('all_records.json', encoding='utf-8'))


def gap(x, y, r):
    x0 = r['x'] + r['offx']; x1 = x0 + r['sx']; y0 = r['y'] + r['offy']; y1 = y0 + r['sy']
    gx = max(x0 - (x + 11), (x - 10) - x1, 0)
    gy = max(y0 - (y + 8), (y - 7) - y1, 0)
    return max(gx, gy)


def prog_walks(doc, slot, index):
    tab = doc['EventCodes%sTable' % slot]
    if index <= 0 or index >= len(tab):
        return None
    start = tab[index]
    starts = sorted({s for _, _, s in de.programs(doc)} | {len(doc['Codes'])})
    nxt = [s for s in starts if s > start]
    end = nxt[0] if nxt else len(doc['Codes'])
    walks = []
    for pc, op, v, bad in de.sweep(doc, start, end):
        if bad:
            break
        if op == 0x0B:
            walks.append(('0B', pc, v[2] | v[3] << 8))
        elif op in (0x1E, 0x1F):
            walks.append(('%02X' % op, pc, v[1] | v[2] << 8))
    return walks


def main():
    for m in [int(a) for a in sys.argv[1:]]:
        doc = de.load_map(m)
        recs = [r for r in rows if r['map'] == m]
        lifts = [r for r in recs if r['kind'] != 0]
        print(f'== map {m}: {len(lifts)} liftables')
        for r in recs:
            if r['kind'] != 0:
                continue
            c = int(r['progs'][2] or 0) & 0x7F
            f = int(r['progs'][5] or 0) & 0x7F
            c_hdr = r['prog_hdr'][1] & 0x7F
            walks = None
            if doc:
                walks = prog_walks(doc, 'C', c) if c else None
            if not walks:
                continue
            g = min(gap(r['x'], r['y'], l) for l in lifts) if lifts else None
            print(f"  rec{r['idx']:>3} {str(r['name'])[:16]:<16} at ({r['x']},{r['y']},{r['z']}) C[{c}] walks={walks} sumR={sum(w[2] for w in walks)} nearestLiftableGap={g}")
        if doc:
            for idx in range(1, len(doc['EventCodesBTable'])):
                w = prog_walks(doc, 'B', idx)
                if w:
                    print(f"  map program B[{idx}] walks={w}")


if __name__ == '__main__':
    main()
