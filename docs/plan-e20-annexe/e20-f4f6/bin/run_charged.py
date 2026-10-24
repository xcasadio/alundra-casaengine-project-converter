"""run_charged.py - f4 scenarios: charged arrow (type 7) on the real code. Usage: python -B run_charged.py [name ...]"""
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fw
import world as W
import c1world as C
from run_arrows import scen, save, first_tick, OUT, SQ, DIR, row_wall

HOLD95 = {t: SQ for t in range(1, 96)}          # Square held ticks 1..95, released at tick 96 (R = 96)

SCENARIOS = {}


def sc(f):
    SCENARIOS[f.__name__] = f
    return f


def charged(name, n, slots, cells=None, targets=(), tall=None, direction=24, hero_px=(100, 100), title='', first=96, cols=None, init=None, after_tick=None, item=6, hold=HOLD95, seed=0x12345678):
    if tall is not None:
        C.map14().record['Header']['Ubuff'][13] = tall
    spec = dict(item=item, n=n, holds=dict(hold), after=0, hero_px=hero_px, direction=direction, cells=cells, targets=list(targets), seed=seed)
    if init:
        spec['init'] = init
    if after_tick:
        spec['after_tick'] = after_tick
    res = fw.run(spec)
    if tall is not None:
        C.map14().record['Header']['Ubuff'][13] = 8
    S = None
    cs = None
    # the charged shot is the LAST arrow to appear: first tick >= 96 where some slot holds a type-7 entity
    for r in res['rows']:
        if r['tick'] < 96:
            continue
        for s_, d in r['slots'].items():
            if d['type68'] == 7:
                S = r['tick']
                cs = s_
                break
        if S:
            break
    others = [x for x in slots if x != cs]
    slots = ([cs] if cs is not None else []) + others
    save(name, res, list(slots), rel=S, title='%s (charged spawn tick S=%s, release R=96)  %s' % (name, S, title), first=first, cols=cols)
    print(name, 'S=', S)
    return res, S


@sc
def charged_east_wall():
    return charged('charged7_east_wall', 190, (1,), cells=fw.wall_col(15), title='William bow charged (type 7), east, wall column 15; (the early normal arrow dies long before)')


@sc
def charged_free_dirs():
    out = []
    for d in ('west', 'north', 'south'):
        out.append(charged('charged7_free_%s' % d, 135, (1,), direction=DIR[d], title='charged arrow %s on the free field' % d, first=100))
    return out


@sc
def charged_map_edge():
    return charged('charged7_map_edge', 135, (1,), hero_px=(1150, 100), title='charged arrow east from x=1150 to the map edge', first=100)


@sc
def charged_vs_target():
    return charged('charged7_vs_target', 190, (2, 1), targets=[(24, 12, 0)], tall=None, cells=fw.wall_col(15),
                   title='charged arrow vs the REAL Beannoid (SizeZ 8) at x=300,y=104 then wall col 15; slot 1 = target (the normal arrow of the press dies on the field at the wall or the target, slot 2)')


@sc
def charged_grass():
    # a strip of cuttable cells (walkability bit 0x02) under the arrow: rows 5 and 6 (y 80..111), columns 5..12
    def cells(col, row):
        if 5 <= col <= 12 and row in (5, 6):
            return (0, 0, 0x02, 0)
        return (0, 0, 0, 0)
    return charged('charged7_grass', 135, (1,), cells=cells, title='charged arrow east over a strip of cuttable cells (walkability 0x02) cols 5..12 rows 5..6', first=100)


@sc
def charged_step_dirs():
    out = []
    # a 16 px step (height byte 1) in column 8: the charged arrow flies at Z+8 (box Z 0..15)
    def cells(col, row):
        return (1, 0, 0, 0) if col == 8 else (0, 0, 0, 0)
    out.append(charged('charged7_step16', 135, (1,), cells=cells, title='charged arrow east vs a 16 px step (height byte 1) in column 8', first=100))
    def cells2(col, row):
        return (2, 0, 0, 0) if col == 8 else (0, 0, 0, 0)
    out.append(charged('charged7_step32', 135, (1,), cells=cells2, title='charged arrow east vs a 32 px step (height byte 2) in column 8', first=100))
    return out


def main():
    names = sys.argv[1:] or list(SCENARIOS)
    for n in names:
        SCENARIOS[n]()


if __name__ == '__main__':
    main()
