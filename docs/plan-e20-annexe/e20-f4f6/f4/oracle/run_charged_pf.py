"""run_charged_pf.py - E20.f4 T0: the keys of the charged arrow (type 7) WITH the step of the generator that ends every logical tick of the binary (the end of the frame function, 0x8002BD0C-0x8002BD34).

The harness of ../../bin/fw.py does not model that step (a tick without a draw has rng0 == rng1 and draws == 0). Rule of the plan (section 1.20, "Revision au HEAD", point 3, revisions 3 and 4):
  s <- s * 0x7D2B89DD + 0xE06A02E7 is applied AFTER the record of rng1 of the tick and BEFORE the read of rng0 of the next tick, never by the after_tick hook (which fw.run runs before it reads rng1).
  So in a key of this folder: rng1(t) = the state after the draws of the tick t and before its step by image; rng0(t+1) = step(rng1(t)); draws(t) counts the draws of the tick only.
The loop below is the loop of fw.run (copied, not changed: ../../bin is frozen evidence) with that one statement added. Usage (from a COPY of the annex, it writes ./out): python -B run_charged_pf.py [name ...]
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.join(HERE, '..', '..', 'bin')
sys.path.insert(0, BIN)
import fw
import run_arrows
import run_charged
from run_arrows import SQ, DIR
import world as W

OUT = os.path.join(HERE, 'out')
os.makedirs(os.path.join(OUT, 'json'), exist_ok=True)
os.makedirs(os.path.join(OUT, 'traces'), exist_ok=True)
run_arrows.OUT = OUT                      # run_arrows.save writes under run_arrows.OUT: keep the frozen bin/out untouched

P = fw.P
RNG = fw.RNG


def step(s):
    return fw.lcg(s)


def run_pf(spec):
    """fw.run with the step by image added at the end of each tick (after rng1 is recorded)."""
    m, meta = P.setup(item=spec.get('item'), hero_px=spec.get('hero_px', (100, 100)), direction=spec.get('direction', 24), targets=spec.get('targets', ()),
                      cells=spec.get('cells'), seed=spec.get('seed', 0x12345678), types=spec.get('types', P.PROJECTILE_TYPES), keep=spec.get('keep', P.REAL),
                      target_kind=spec.get('target_kind'))
    fw.install_fx(m)
    for fn in spec.get('init', []):
        fn(m)
    pad = W.Pad()
    rows = []
    holds = spec.get('holds', {})
    after = spec.get('after', 0)
    for t in range(1, spec['n'] + 1):
        m.tick_no = t
        m.fxrecs = []
        j0 = len(m.journal)
        rng0 = m.peek(RNG)
        hold = holds.get(t, after)
        (spec.get('ticker') or W.tick)(m, pad, hold)
        for fn in spec.get('after_tick', []):
            fn(m, t)
        act = P.active_slots(m)
        rng1 = m.peek(RNG)
        jl = [P.fmt_j(j) for j in m.journal[j0:] if not j[1].startswith('pass_') and j[1] not in ('effect_3BDD8', 'effect_3BE74')]
        r = dict(tick=t, hold=hold, rng0=rng0, rng1=rng1, draws=fw.draws(rng0, rng1), rec_ptr=m.peek(0x80127110), charge=m.peeks(0x80126FFC),
                 slots={s: P.snap(m, s) for s in act}, journal=jl, effects=[fw.fx_text(m, rec) for rec in m.fxrecs])
        rows.append(r)
        m.poke(RNG, step(rng1))               # the step of the end of the frame: after rng1 is recorded, before rng0 of the next tick is read
    return dict(spec=spec, meta=meta, rows=rows, m=m)


def grass_cells(col, row):
    if 5 <= col <= 12 and row in (5, 6):
        return (0, 0, 0x02, 0)
    return (0, 0, 0, 0)


SCENARIOS = {
    'charged7_east_wall_pf': lambda: run_charged.charged('charged7_east_wall_pf', 190, (1,), cells=fw.wall_col(15), title='[pf] William bow charged (type 7), east, wall column 15'),
    'charged7_grass_pf': lambda: run_charged.charged('charged7_grass_pf', 135, (1,), cells=grass_cells, title='[pf] charged arrow east over a strip of cuttable cells (walkability 0x02) cols 5..12 rows 5..6', first=100),
    'charged7_map_edge_pf': lambda: run_charged.charged('charged7_map_edge_pf', 135, (1,), hero_px=(1150, 100), title='[pf] charged arrow east from x=1150 to the map edge', first=100),
    'charged7_vs_target_pf': lambda: run_charged.charged('charged7_vs_target_pf', 190, (2, 1), targets=[(24, 12, 0)], cells=fw.wall_col(15), title='[pf] charged arrow vs the REAL Beannoid at x=300,y=104 then wall col 15; slot 1 = target'),
    # E20.e0k5 T0: the hero flush against the wall (x=109) and at 105 (the 6 px step to 111 is bounded to 109), wall column 5 (keys of the reading of 2026-10-24, t2-proj/pb_keys.py)
    'charged7_flush_pf': lambda: run_charged.charged('charged7_flush_pf', 140, (1,), cells=fw.wall_col(5), hero_px=(109, 100), title='[pf] charged arrow, hero flush against the wall (x=109, wall column 5)', first=100),
    'charged7_near105_pf': lambda: run_charged.charged('charged7_near105_pf', 140, (1,), cells=fw.wall_col(5), hero_px=(105, 100), title='[pf] charged arrow, hero at 105 (6 px step to 111: bounded to 109)', first=100),
}


def main():
    fw.run = run_pf                            # run_charged.charged calls fw.run
    names = sys.argv[1:] or list(SCENARIOS)
    for n in names:
        SCENARIOS[n]()


if __name__ == '__main__':
    main()
