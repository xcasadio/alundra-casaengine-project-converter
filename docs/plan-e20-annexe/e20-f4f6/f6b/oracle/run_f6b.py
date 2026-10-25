"""run_f6b.py - E20.f6b T0: the NEW scenarios of the fire and ice balls (types 11, 12, 14, 15: C60 at 0x8007A8A0, E9 at 0x8007EF50) and of the three flames of the map 309, on the real machine of the annex, plus the `_pf` replay of the 32 frozen keys.

Rules of the plan (section 1.20 "Revision au HEAD" points 3 and 10, sections 1.22 / 1.22.2 T0):
  * the 32 frozen keys (`fire*`, `ice*`, `flames309*`) of ../../bin/out are NOT rewritten here (frozen evidence): this script replays them with the step of the generator that ends every logical tick of the binary
    (`s <- s * 0x7D2B89DD + 0xE06A02E7`, 0x8002BD0C-0x8002BD34) applied AFTER the record of rng1 of the tick and BEFORE the read of rng0 of the next tick (never by the after_tick hook), suffix `_pf` (the loop of
    ../../f5/oracle/run_f5.py `run_pf`, copied, not changed); the new keys are written in both forms (plain and `_pf`);
  * a harness injection is documented in the title of the key (the oracle never decides the expected value: it runs the real code).
New keys:
  fire11_branchA_water_on_puff_tick   : the fire ball explodes on flat ground (S = 12); +0x18C is set to 4 (deep water) at the end of the tick S+15, so the tick S+16 (the counter is a multiple of 8) runs E9 branch A with the slope 4:
                                         the plan says "the destruction, then the puff of the same tick" (the literal order of 0x8007EF84-0x8007F214);
  ice14_branchA_water_on_puff_tick    : the same with the ice rod (effect 18, no Z offset on the puff);
  fire11_branchA_slope1_on_puff_tick  : +0x18C = 1 at the same tick (the slope 1 destroys too in branch A);
  fire11_branchA_slope2_on_puff_tick  : +0x18C = 2 (the negative control: no destruction, the puff goes on);
  fire11_branchA_water_off_puff_tick  : +0x18C = 4 at the end of S+16, so the tick S+17 is not a multiple of 8: the destruction alone, no puff;
  fire11_branchA_water_on_end_tick    : +0x18C = 4 at the end of the tick S+47 where +0xAC is 1 (end of the animation): the destruction by the slope (effect 6) then the destruction by the end of the animation (effect -1), two calls in the same tick;
  fire11_flush, fire12_flush, ice14_flush, ice15_flush, fire11_near105, fire11_x107 : the balls shot with the hero flush against the wall column 5 (109), or at 105 and 107 (E20.e0k5: the first step is bounded by the halving search of the binary);
  flames309_real                      : the three records 6, 7, 8 of the map 309 placed by the real record loader 0x8003A1B8, the program A (`1A 02`, `63 80 F0 00`, `FF`) run by the REAL bytecode runner 0x8004205C (the map's Codes and
                                         EventCodes*Table installed in RAM where 0x80041EE4 reads them), instead of the harness stub of ../../bin/run_flames.py (the frozen `flames309` key, unchanged).
Usage (from a COPY of the annex, it writes ./out): python -B run_f6b.py [name ...]
"""
import os
import sys
import json
import shutil
import runpy

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.join(HERE, '..', '..', 'bin')
sys.path.insert(0, BIN)
import fw
import world as W
import c1world as C
import run_arrows as A
import run_balls as B
import run_flames as RF
from run_arrows import SQ

OUT = os.path.join(HERE, 'out')
os.makedirs(os.path.join(OUT, 'json'), exist_ok=True)
os.makedirs(os.path.join(OUT, 'traces'), exist_ok=True)
A.OUT = OUT          # run_arrows.save writes under run_arrows.OUT: redirect it to f6b/oracle/out (the frozen bin/out is never touched)
B.OUT = OUT
ENT0, STRIDE = W.ENT0, W.STRIDE
P = fw.P
RNG = fw.RNG

PF = {'on': False}


def run_pf(spec):
    """fw.run with the step by image added at the end of each tick (after rng1 is recorded): the loop of fw.run, as copied by f5/oracle/run_f5.py (not changed)."""
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
        m.poke(RNG, fw.lcg(rng1))          # the step of the end of the frame
    return dict(spec=spec, meta=meta, rows=rows, m=m)


_FW_RUN = fw.run


def use_pf(on):
    PF['on'] = on
    fw.run = run_pf if on else _FW_RUN


# ---- the injection of the slope --------------------------------------------------------------------------------------------------------------------------------------------------------
def inject_slope(slope, tick_abs, slot=1):
    """after the tick `tick_abs`, set +0x18C of the ball (slot 1) to `slope`: stands for the end pass of a tick spent over water / on a slope (the real pass would write it; the scenario does not need a map for it)"""
    def f(m, t):
        if t == tick_abs:
            m.poke(ENT0 + STRIDE * slot + 0x18C, slope)
    return f


S0 = 12                      # the tick of birth of the uncharged ball (Square at the tick 1: S = P + 11)


def play_ball(name, item, n, title, after_tick=None, **kw):
    spec_after = after_tick
    if spec_after is not None:
        # B.ball builds its own spec: pass the hook through its after_tick argument
        kw['after_tick'] = spec_after
    res, S = B.ball(name + ('_pf' if PF['on'] else ''), item, n, title=title, **kw)
    return res, S


SCENARIOS = {}


def sc(f):
    SCENARIOS[f.__name__] = f
    return f


@sc
def fire11_branchA_water_on_puff_tick():
    return play_ball('fire11_branchA_water_on_puff_tick', 16, 70, 'INJECTION: +0x18C = 4 at the end of S+15 (tick 27): E9 branch A at S+16 (a multiple of 8) sees the deep-water slope: destroy(6), then the puff of the same tick',
                     after_tick=[inject_slope(4, S0 + 15)])


@sc
def ice14_branchA_water_on_puff_tick():
    return play_ball('ice14_branchA_water_on_puff_tick', 14, 70, 'INJECTION: +0x18C = 4 at the end of S+15 (tick 27), ice rod: destroy(6), then the puff of the same tick (effect 18, Z + 0)',
                     after_tick=[inject_slope(4, S0 + 15)])


@sc
def fire11_branchA_slope1_on_puff_tick():
    return play_ball('fire11_branchA_slope1_on_puff_tick', 16, 70, 'INJECTION: +0x18C = 1 at the end of S+15: the slope 1 destroys in branch A too',
                     after_tick=[inject_slope(1, S0 + 15)])


@sc
def fire11_branchA_slope2_on_puff_tick():
    return play_ball('fire11_branchA_slope2_on_puff_tick', 16, 70, 'INJECTION: +0x18C = 2 at the end of S+15 (negative control): no destruction, the puff goes on',
                     after_tick=[inject_slope(2, S0 + 15)])


@sc
def fire11_branchA_water_off_puff_tick():
    return play_ball('fire11_branchA_water_off_puff_tick', 16, 70, 'INJECTION: +0x18C = 4 at the end of S+16 (tick 28): the tick S+17 is not a multiple of 8: destroy(6) alone, no puff',
                     after_tick=[inject_slope(4, S0 + 16)])


@sc
def fire11_branchA_water_on_end_tick():
    return play_ball('fire11_branchA_water_on_end_tick', 16, 70, 'INJECTION: +0x18C = 4 at the end of S+47 (tick 59), where +0xAC is 1: at S+48 destroy(6) by the slope, then destroy(-1) by the end of the animation, in the same tick',
                     after_tick=[inject_slope(4, S0 + 47)])


# ---- the ball shot with the hero flush against (or near) a wall (E20.e0k5: the binary bounds the first step of an entity born in the game to the wall face, the halving search of 0x80037730): the ball keys that e0k5 T0 left to f6b ----------------
# the scenarios of the reading of 2026-10-24 (../../../e20-e0k5-disc/pb_keys.py), now written here in both forms; the wall is the column 5 (x >= 120), the hero at 109 (flush), 105 (the 6 px step ends at 111, bounded to 109) or 107
@sc
def fire11_flush():
    return play_ball('fire11_flush', 16, 50, 'fire ball, hero flush against the wall (x=109, wall column 5)', hero_px=(109, 100), cells=fw.wall_col(5))


@sc
def fire12_flush():
    return play_ball('fire12_flush', 16, 130, 'charged fire ball, hero flush against the wall', charged=True, hero_px=(109, 100), cells=fw.wall_col(5))


@sc
def ice14_flush():
    return play_ball('ice14_flush', 14, 50, 'ice ball, hero flush against the wall', hero_px=(109, 100), cells=fw.wall_col(5))


@sc
def ice15_flush():
    return play_ball('ice15_flush', 14, 130, 'charged ice ball, hero flush against the wall', charged=True, hero_px=(109, 100), cells=fw.wall_col(5))


@sc
def fire11_near105():
    return play_ball('fire11_near105', 16, 50, 'fire ball, hero at 105 (the first step ends at 111: bounded to 109 by the halving)', hero_px=(105, 100), cells=fw.wall_col(5))


@sc
def fire11_x107():
    return play_ball('fire11_x107', 16, 50, 'fire ball, hero at 107', hero_px=(107, 100), cells=fw.wall_col(5))


# ---- the flames of the map 309 with the REAL bytecode runner ---------------------------------------------------------------------------------------------------------------------------------
CODES = 0x801D0000
TABLES = 0x801D1000


def real_runner(m):
    """after run_flames' own init: the map's bytecode and its six offset tables in RAM where the real code reads them (0x80041EE4: [0x80126E70 + 0x30] the bytecode base, [0x80126E70 + 0x34 + 4 L] the u16 table of the letter L),
    and the stub of 0x8004205C removed: the record word +0x4C = 0xFFFFFF87 (bit 0x80: this map's block; index 7) is run by the binary's own bytecode runner"""
    ec = RF.m309['SpriteInfo']['EventCodes']
    m.poke_bytes(CODES, bytes(ec['Codes']))
    m.poke(C.MAPBLK + 0x30, CODES)
    for k, letter in enumerate('ABCDEF'):
        addr = TABLES + 0x40 * k
        m.poke_bytes(addr, b''.join(int(v).to_bytes(2, 'little') for v in ec['EventCodes%sTable' % letter]))
        m.poke(C.MAPBLK + 0x34 + 4 * k, addr)
    m.hooks.pop(0x8004205C, None)


FLAME_COLS = ['status', 'anim_t', 'anim_c', 'dir_t', 'cnt1c', 'px', 'py', 'pz', 'fx', 'fy', 'fz', 'adj_13C', 'coll_140', 'ground_144', 'terr_138', 'slope_18C', 'reset_AC', 'hitc', 'touch_slot', 'flags6C', 'atk_attr', 'atk_pow',
              'box1d4', 'hbx', 'hbz', 'bw', 'bd', 'bh']


@sc
def flames309_real():
    spec, state = RF.make(emulate_a=False, n=80)
    spec['init'].append(real_runner)
    res = _run(spec)
    name = 'flames309_real' + ('_pf' if PF['on'] else '')
    A.save(name, res, state['slots'], rel=1, title='%s: map 309 records 6,7,8: global sprite 12 through 0x8003A1B8 on the real cells; the program A run by the REAL bytecode runner 0x8004205C' % name, cols=FLAME_COLS)
    print(name, 'slots', state['slots'])
    return res, state


def _run(spec):
    return run_pf(spec) if PF['on'] else _FW_RUN(spec)


# ---- the frozen keys, replayed with the step by image (suffix _pf) ------------------------------------------------------------------------------------------------------------------------
def replay_frozen():
    """the 32 frozen keys again with the step by image: the scenario definitions of ../../bin/run_balls.py, run_balls_extra.py, run_balls_mixed.py, run_pointblank.py (the fire part), run_flames.py and run_flames_noA.py are called, not copied,
    with fw.run replaced"""
    use_pf(True)
    try:
        for n in B.SCENARIOS:
            B.SCENARIOS[n]()
        runpy.run_path(os.path.join(BIN, 'run_balls_extra.py'), run_name='frozen_extra')
        runpy.run_path(os.path.join(BIN, 'run_balls_mixed.py'), run_name='frozen_mixed')
        B.ball('fire11_point_blank', 16, 50, hero_px=(108, 100), cells=fw.wall_col(5), title='fire ball shot with the hero 12 px from a wall (column 5, x>=120)')
        RF.main()
        runpy.run_path(os.path.join(BIN, 'run_flames_noA.py'), run_name='frozen_noA')
    finally:
        use_pf(False)


def main():
    names = sys.argv[1:]
    for n in names or list(SCENARIOS):
        use_pf(False)
        SCENARIOS[n]()
        use_pf(True)
        SCENARIOS[n]()
    use_pf(False)
    if not names:
        # the frozen scenarios write their keys without suffix: write them under a temporary folder and rename
        tmp = os.path.join(OUT, '_frozen_pf')
        shutil.rmtree(tmp, ignore_errors=True)
        os.makedirs(os.path.join(tmp, 'json'))
        os.makedirs(os.path.join(tmp, 'traces'))
        A.OUT = tmp
        B.OUT = tmp
        replay_frozen()
        A.OUT = OUT
        B.OUT = OUT
        for sub, ext in (('json', '.json'), ('traces', '.txt')):
            for f in sorted(os.listdir(os.path.join(tmp, sub))):
                dst = os.path.join(OUT, sub, f[:-len(ext)] + '_pf' + ext)
                shutil.move(os.path.join(tmp, sub, f), dst)
        shutil.rmtree(tmp)


if __name__ == '__main__':
    main()
