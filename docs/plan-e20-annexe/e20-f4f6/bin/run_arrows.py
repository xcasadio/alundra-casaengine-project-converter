"""run_arrows.py - f5 scenarios: normal arrows (types 3 and 4) on the real code.  Usage: python -B run_arrows.py [name ...]
Outputs: out/traces/<name>.txt (compressed timeline), out/json/<name>.json (full rows).
"""
import sys
import os
import json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fw
import world as W
import c1world as C

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')
os.makedirs(os.path.join(OUT, 'traces'), exist_ok=True)
os.makedirs(os.path.join(OUT, 'json'), exist_ok=True)

SQ = W.SQUARE
# hero facing: 24 east, 8 west, 16 north (up), 0 south (down)  (direction table 0x80023644 = {0, 0x10, 8, 0x18} for AnimationDirection 0..3)
DIR = dict(east=24, west=8, north=16, south=0)


def row_wall(r, wk=0x40, hb=0):
    def cells(col, row):
        return (hb, 0, wk, 0) if row == r else (0, 0, 0, 0)
    return cells


def save(name, res, slots, rel=None, title='', cols=None, first=1, last=None):
    fw.dump2(res, os.path.join(OUT, 'traces', name + '.txt'), slots, rel=rel, title=title, cols=cols, first=first, last=last)
    rows = []
    for r in res['rows']:
        rr = dict(tick=r['tick'], hold=r['hold'], rng0=r['rng0'], rng1=r['rng1'], draws=r['draws'], effects=r['effects'], journal=r['journal'],
                  slots={str(s): r['slots'][s] for s in r['slots'] if s in slots})
        rows.append(rr)
    json.dump(rows, open(os.path.join(OUT, 'json', name + '.json'), 'w'))


def first_tick(res, slot):
    for r in res['rows']:
        if slot in r['slots']:
            return r['tick']
    return None


def scen(name, item, n, direction=24, cells=None, targets=(), holds=None, hero_px=(100, 100), tall=None, slots=(1,), first=1, last=None, cols=None, after=0, title='', seed=0x12345678, init=None):
    if tall is not None:
        C.map14().record['Header']['Ubuff'][13] = tall
    spec = dict(item=item, n=n, holds=holds or {1: SQ}, after=after, hero_px=hero_px, direction=direction, cells=cells, targets=list(targets), seed=seed)
    if init:
        spec['init'] = init
    res = fw.run(spec)
    if tall is not None:
        C.map14().record['Header']['Ubuff'][13] = 8       # restore the Beannoid's real SizeZ (map-14 record, header Ubuff[13])
    S = first_tick(res, slots[0])
    save(name, res, list(slots), rel=S, title='%s (spawn tick S=%s)  %s' % (name, S, title), cols=cols, first=first, last=last)
    print(name, 'S=', S)
    return res, S


SCENARIOS = {}


def sc(f):
    SCENARIOS[f.__name__] = f
    return f


@sc
def arrow3_east_wall():
    return scen('arrow3_east_wall', 5, 90, cells=fw.wall_col(15), title='Hunter bow, type 3, east, wall column 15 (x 360..383, walk flag 0x40)')


@sc
def arrow4_east_wall():
    return scen('arrow4_east_wall', 6, 90, cells=fw.wall_col(15), title='William bow normal shot, type 4, east, wall column 15')


@sc
def arrow3_free_dirs():
    out = []
    for d in ('east', 'west', 'north', 'south'):
        out.append(scen('arrow3_free_%s' % d, 5, 40, direction=DIR[d], title='Hunter bow, free field, %s' % d))
    return out


@sc
def arrow3_wall_dirs():
    out = []
    out.append(scen('arrow3_wall_west', 5, 90, direction=DIR['west'], cells=fw.wall_col(2), title='Hunter bow west, wall column 2 (x 48..71)'))
    out.append(scen('arrow3_wall_north', 5, 90, direction=DIR['north'], cells=row_wall(2), title='Hunter bow north, wall row 2 (y 32..47)'))
    out.append(scen('arrow3_wall_south', 5, 90, direction=DIR['south'], cells=row_wall(12), title='Hunter bow south, wall row 12 (y 192..207)'))
    return out


@sc
def arrow3_map_edge():
    # hero near the east edge: map is 52 columns * 24 px = 1248 px
    return scen('arrow3_map_edge', 5, 60, hero_px=(1150, 100), title='Hunter bow east from x=1150 on the open field to the map edge (1248 px)')


@sc
def arrow3_target():
    # Beannoid body made 32 px tall so that the arrow (Z+16) can touch it (documented harness change; the real SizeZ is 8)
    return scen('arrow3_target', 5, 70, targets=[(24, 12, 0)], tall=32, slots=(2, 1), title='Hunter arrow vs inert Beannoid (SizeZ patched to 32) at x=300,y=104; slot 1 = target')


@sc
def arrow3_target_low():
    return scen('arrow3_target_low', 5, 70, targets=[(24, 12, 0)], tall=None, slots=(2, 1), title='Hunter arrow vs the real Beannoid (SizeZ 8): the arrow is above the body and misses')


@sc
def arrow4_target():
    return scen('arrow4_target', 6, 70, targets=[(24, 12, 0)], tall=32, slots=(2, 1), title='William arrow vs patched Beannoid; slot 1 = target')


def main():
    names = sys.argv[1:] or list(SCENARIOS)
    for n in names:
        SCENARIOS[n]()


if __name__ == '__main__':
    main()
