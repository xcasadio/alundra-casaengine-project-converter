"""run_f5.py - E20.f5 T0: the NEW scenarios of the normal arrows (types 3 and 4, handler E17 at 0x8007F420) on the real machine, plus the `_pf` replay of the 17 frozen keys.

Rules of the plan (section 1.20 "Revision au HEAD" points 3 and 10, section 1.21.1 T0):
  * the 17 frozen keys `arrow*` of ../../bin/out are NOT rewritten here (frozen evidence): this script replays them with the step of the generator that ends every logical tick of the binary
    (`s <- s * 0x7D2B89DD + 0xE06A02E7`, 0x8002BD0C-0x8002BD34) applied AFTER the record of rng1 of the tick and BEFORE the read of rng0 of the next tick (never by the after_tick hook), suffix `_pf`;
    the new scenarios are written in both forms (plain and `_pf`);
  * a harness injection is documented in the title of the key (the oracle never decides the expected value: it runs the real code).
Usage (from a COPY of the annex, it writes ./out): python -B run_f5.py [name ...]
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.join(HERE, '..', '..', 'bin')
sys.path.insert(0, BIN)
import fw
import world as W
import c1world as C
import run_arrows as RA
import run_arrows_extra as RX
from run_arrows import SQ, DIR

OUT = os.path.join(HERE, 'out')
os.makedirs(os.path.join(OUT, 'json'), exist_ok=True)
os.makedirs(os.path.join(OUT, 'traces'), exist_ok=True)
RA.OUT = OUT                       # run_arrows.save writes under run_arrows.OUT: keep the frozen bin/out untouched
ENT0, STRIDE = W.ENT0, W.STRIDE
P = fw.P
RNG = fw.RNG
CROSS = W.CROSS

# the real sprite 136 of map 14 (body 14 x 16 x 17 in the header, Ubuff[11..13]): a placed sprite of the map the oracle already uses, same layout, same spawner
C.KINDS['map14-sprite136'] = dict(map='map_14', index=136, label='real sprite 136 of map 14 (body 14x16x17)')

PF = {'on': False}


def run_pf(spec):
    """fw.run with the step by image added at the end of each tick (after rng1 is recorded): the loop of fw.run, copied from f4/oracle/run_charged_pf.py (not changed)."""
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


def run(spec):
    return run_pf(spec) if PF['on'] else fw.run(spec)


def first_tick(res, slot):
    for r in res['rows']:
        if slot in r['slots']:
            return r['tick']
    return None


def play(name, spec, slots, title='', tall=None, first=1, last=None, birth_slot=None):
    """run one scenario and write its key. tall = SizeZ patched into the Beannoid record of map 14 (documented harness change, restored after)."""
    if tall is not None:
        C.map14().record['Header']['Ubuff'][13] = tall
    try:
        res = run(spec)
    finally:
        if tall is not None:
            C.map14().record['Header']['Ubuff'][13] = 8
    S = first_tick(res, birth_slot if birth_slot is not None else slots[0])
    key = name + ('_pf' if PF['on'] else '')
    RA.save(key, res, list(slots), rel=S, title='%s (spawn tick S=%s)  %s' % (key, S, title), first=first, last=last)
    print(key, 'S=', S)
    return res, S


def inj(target_slot, arrow_slot, off, fn, state):
    """after the tick in which the arrow's hit counter (+0x220) is raised, apply fn to the word at the target's offset off (stands for a D handler / the opcode 0x86 / a script that runs before E17 and
    changes the target in the tick that follows the hit; in the binary the dispatch runs the slots in ascending order)"""
    def f(m, t):
        a = ENT0 + STRIDE * arrow_slot
        if m.peek(a + 0x220) and not state.get('done'):
            addr = ENT0 + STRIDE * target_slot + off
            m.poke(addr, fn(m.peek(addr)) & 0xFFFFFFFF)
            state['done'] = t
    return f


def target_spec(item, n, after_tick=None, init=None, kind=None, targets=((24, 12, 0),), direction=24, holds=None, hero_px=(100, 100), cells=None):
    spec = dict(item=item, n=n, holds=holds or {1: SQ}, after=0, hero_px=hero_px, direction=direction, cells=cells, targets=list(targets), init=init or [], after_tick=after_tick or [])
    if kind:
        spec['target_kind'] = kind
    return spec


SCENARIOS = {}


def sc(f):
    SCENARIOS[f.__name__] = f
    return f


# ---- the filters of E17 applied AFTER the touch (the mutants E17-8 and E17-9 are killed only here) ------------------------------------------------------------------------------------------
@sc
def arrow3_target_filter_b4():
    st = {}
    return play('arrow3_target_filter_b4', target_spec(5, 75, after_tick=[inj(1, 2, 0xB4, lambda v: v | 0x40, st)]), (2, 1), tall=32,
                title='INJECTION: target +0xB4 |= 0x40 right after the hit tick (skip rule of E17): the arrow flies on at status 2 (hit tick %s)' % st.get('done'))


@sc
def arrow3_target_filter_class():
    st = {}
    return play('arrow3_target_filter_class', target_spec(5, 75, after_tick=[inj(1, 2, 0x6C, lambda v: v & ~1, st)]), (2, 1), tall=32,
                title='INJECTION: target +0x6C &= ~1 (the class bit the arrow mask 1 tests) right after the hit tick: E17 finds no valid target, the arrow flies on at status 2')


# ---- the target destroyed in the same dispatch (status 4, lower slot): still found, there is no status filter ------------------------------------------------------------------------------
def kill_on_D(m):
    e = ENT0 + STRIDE * 1
    code = m.peek(e + 0x70 + 4 * 3)
    p = m.peek(C.HANDLER_TABLES + 4 * 3)
    addr = m.peek(p + 4 * code)

    def hook(m2):
        a0 = m2.r[4]
        slot = (a0 - ENT0) // STRIDE
        m2.journal.append((m2.tick_no, 'handler', [slot, 3, addr]))
        m2.poke(a0 + 0x10, 4)           # DestroyEntity-like: status 4, nothing else touched (flags and body kept as the real one does)
        m2.r[2] = 0
    m.hooks[addr] = hook


@sc
def arrow3_target_dead_same_dispatch():
    return play('arrow3_target_dead_same_dispatch', target_spec(5, 60, init=[kill_on_D]), (2, 1), tall=32,
                title='INJECTION: the target (slot 1) is set to status 4 in its own D handler at the tick of the touch (stands for DestroyEntity in the same dispatch); the arrow (slot 2) still finds it and dies')


# ---- the target in a higher slot than the arrow, and in the last slot of the scan ------------------------------------------------------------------------------------------------------
def free_slot1(m):
    base = ENT0 + STRIDE * 1
    for o in range(0, STRIDE, 4):
        m.poke(base + o, 0)
    m.poke(base, 1)       # index word of the template block


@sc
def arrow3_target_higher_slot():
    return play('arrow3_target_higher_slot', target_spec(5, 75, init=[free_slot1], targets=[(24, 6, 0), (24, 12, 0)]), (1, 2), tall=32,
                title='INJECTION: slot 1 (the first of two targets) freed before the shot, so the arrow is born in slot 1 and the target of y=104 stays in slot 2, the LAST slot of the scan')


# ---- the vertical reach: the arrow flies 16 px above the ground; a body of 16 is missed, a body of 17 is hit ------------------------------------------------------------------------------
@sc
def arrow3_zreach_16():
    return play('arrow3_zreach_16', target_spec(5, 75), (2, 1), tall=16, title='Beannoid SizeZ patched to 16: the arrow (box z 16 to 27) misses it')


@sc
def arrow3_zreach_17():
    return play('arrow3_zreach_17', target_spec(5, 75), (2, 1), tall=17, title='Beannoid SizeZ patched to 17: the arrow touches it (the inclusive overlap)')


def set_target_animation(animation):
    def init(m):
        m.poke(ENT0 + STRIDE * 1 + 0x88, animation)       # +0x88: the animation asked (the stubbed program A does not choose one)
    return init


@sc
def arrow3_target_sprite136():
    # the sprite 136 starts in an animation without collision data (its program A, stubbed here, would choose the animation): animation 3 is the first of its sets with a hurt box (9 x 9 x 5)
    return play('arrow3_target_sprite136', target_spec(5, 75, kind='map14-sprite136', init=[set_target_animation(3)]), (2, 1),
                title='REAL sprite 136 of map 14 (header body 14x16x17, no patch) as the target at x=300,y=104, INJECTION: its animation set to 3 (the first with a hurt box 9x9x5): the arrow touches it')


# ---- the shot in the air ----------------------------------------------------------------------------------------------------------------------------------------------------------------------
@sc
def arrow3_air_east():
    return play('arrow3_air_east', dict(item=5, n=80, holds={1: CROSS, 8: SQ}, after=0, hero_px=(100, 100), direction=24, cells=fw.wall_col(15), targets=[]), (1,), birth_slot=1,
                title='Hunter bow shot IN THE AIR: Cross at t1 (jump), Square at t8, east, wall column 15 (the air sub-record of w8: delay 0)')


@sc
def arrow4_air_east():
    return play('arrow4_air_east', dict(item=6, n=80, holds={1: CROSS, 8: SQ}, after=0, hero_px=(100, 100), direction=24, cells=fw.wall_col(15), targets=[]), (1,), birth_slot=1,
                title='William bow normal shot IN THE AIR: Cross at t1 (jump), Square at t8, east, wall column 15 (the air sub-record of w9: delay 8)')


# ---- the south edge of the map (the obligation e0k3 left) --------------------------------------------------------------------------------------------------------------------------------
@sc
def arrow3_map_edge_south():
    return play('arrow3_map_edge_south', dict(item=5, n=60, holds={1: SQ}, after=0, hero_px=(100, 900), direction=DIR['south'], cells=None, targets=[]), (1,), birth_slot=1,
                title='Hunter bow south from y=900 on the open field to the south edge of the map (the north, west and east edges are the frozen keys)')


# ---- the frozen keys, replayed with the step by image (suffix _pf) ------------------------------------------------------------------------------------------------------------------------
def replay_frozen():
    """the 17 frozen keys again, with the step by image: same scenario definitions as ../../bin/run_arrows.py, run_arrows_extra.py and run_pointblank.py (not copied: their functions are called with fw.run replaced)."""
    saved = fw.run
    fw.run = run_pf
    try:
        for n in RA.SCENARIOS:
            RA.SCENARIOS[n]()
        RX.run_invuln()
        RX.run_1ad()
        RX.run_two_targets()
        RA.scen('arrow3_point_blank', 5, 50, hero_px=(108, 100), cells=fw.wall_col(5), title='Hunter arrow shot with the hero 12 px from a wall (column 5, x>=120)')
    finally:
        fw.run = saved


def main():
    names = sys.argv[1:]
    for n in names or list(SCENARIOS):
        PF['on'] = False
        SCENARIOS[n]()
        PF['on'] = True
        SCENARIOS[n]()
    PF['on'] = False
    if not names:
        # the frozen scenarios write their `_pf` keys: their save() adds no suffix, so write them under a temporary folder and rename
        import shutil
        tmp = os.path.join(OUT, '_frozen_pf')
        shutil.rmtree(tmp, ignore_errors=True)
        os.makedirs(os.path.join(tmp, 'json'))
        os.makedirs(os.path.join(tmp, 'traces'))
        RA.OUT = tmp
        RX.OUT = tmp
        replay_frozen()
        RA.OUT = OUT
        for sub, ext in (('json', '.json'), ('traces', '.txt')):
            for f in sorted(os.listdir(os.path.join(tmp, sub))):
                dst = os.path.join(OUT, sub, f[:-len(ext)] + '_pf' + ext)
                shutil.move(os.path.join(tmp, sub, f), dst)
        shutil.rmtree(tmp)


if __name__ == '__main__':
    main()
