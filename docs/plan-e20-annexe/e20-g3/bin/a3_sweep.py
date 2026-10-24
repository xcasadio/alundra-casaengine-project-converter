"""a3_sweep.py - disc-e20g3 A3: sweep of the hero's jump onto the crate column of map 392 with the REAL MovePlayer in the oracle machine (lib392.py).
One run = one world (real cells of map 392, the 7 crates rec4..rec8, rec10, rec11 from the real spawner, hero on the ground) + one pad script:
   hold DIR for tj-1 ticks, DIR+CROSS at tick tj, DIR until tick N.   DIR = LEFT (hero comes from the east, the story direction) or RIGHT (from the west).
Outputs per run: the first tick the hero's support (+0x12C) is a crate (and which one, its record id), the hero's final X and Z, whether it ended beyond the column, contact (+0x130) at the end.
Usage: python -B -I a3_sweep.py east|west  YMIN YMAX YSTEP  TJMIN TJMAX  XS  [XPHASE_STEP N_PHASES]   -> out/a3_sweep_<dir>_<tag>.json and a text summary on stdout
"""
import sys
import os
import json
import itertools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import multiprocessing as mp


def run_one(args):
    import lib392 as L
    side, y, xs, tj, n = args
    DIR = L.LEFT if side == 'east' else L.RIGHT
    m, S = L.world392(hero_px=(int(xs), 296), direction=(8 if side == 'east' else 24))
    h = L.K.ent(0)
    m.poke(h + 0x114, int(round(xs * 65536)) & 0xFFFFFFFF)
    m.poke(h + 0x118, int(round(y * 65536)) & 0xFFFFFFFF)
    pads, nn = L.pad_seq([(tj - 1, DIR), (1, DIR | L.CROSS), (n - tj, DIR)])
    crates = list(S.values())
    rows = L.gl.run(m, nn, pads, crates=crates)
    rec_of = {L.K.ent(s): r for r, s in S.items()}
    first_sup = None
    for r in rows:
        sp = r['hero']['support']
        if sp in rec_of and r['hero']['ground']:
            first_sup = (r['tick'], rec_of[sp])
            break
    last = rows[-1]['hero']
    blocked = [(r['tick'], rec_of.get(r['hero']['xcoll'])) for r in rows if r['hero']['xcoll'] in rec_of][:1]
    zmax = max(r['hero']['pz'] for r in rows) / 65536.0
    return dict(side=side, y=y, xs=xs, tj=tj, first_support=first_sup, final_x=last['px'] / 65536.0 if 'px' in last else last['px'], final_z=last['pz'] / 65536.0,
                blocked_first=blocked, zmax=zmax)


def main():
    side = sys.argv[1]
    ymin, ymax, ystep = float(sys.argv[2]), float(sys.argv[3]), float(sys.argv[4])
    tjmin, tjmax = int(sys.argv[5]), int(sys.argv[6])
    xs0 = float(sys.argv[7])
    pstep = float(sys.argv[8]) if len(sys.argv) > 8 else 0.0
    nph = int(sys.argv[9]) if len(sys.argv) > 9 else 1
    tag = os.environ.get('TAG', 'run')
    ys = []
    y = ymin
    while y <= ymax + 1e-9:
        ys.append(round(y, 4))
        y += ystep
    jobs = [(side, y, xs0 + k * pstep, tj, 75) for y in ys for tj in range(tjmin, tjmax + 1) for k in range(nph)]
    print('jobs', len(jobs), flush=True)
    with mp.Pool(22) as pool:
        res = pool.map(run_one, jobs, chunksize=8)
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'a3_sweep_%s_%s.json' % (side, tag))
    json.dump(res, open(out, 'w'))
    # summary: for each y, the set of outcomes
    from collections import defaultdict
    byy = defaultdict(list)
    for r in res:
        byy[r['y']].append(r)
    print('y   : landed-on-rec5 count / landed-on-other-crate / blocked-only / total   (first tick of support in brackets for rec5)')
    for y in ys:
        rr = byy[y]
        on5 = [r for r in rr if r['first_support'] and r['first_support'][1] == 5]
        oth = [r for r in rr if r['first_support'] and r['first_support'][1] != 5]
        print('%7.3f: rec5 %3d  other %3d  total %3d  other recs %s' % (y, len(on5), len(oth), len(rr), sorted(set(r['first_support'][1] for r in oth))))


if __name__ == '__main__':
    mp.freeze_support()
    main()
