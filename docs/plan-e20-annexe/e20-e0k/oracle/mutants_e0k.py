"""mutants_e0k.py - E20.e0k1 T0: the one-word mutants of the oracle (teeth of the e0k facts).  Real patches of the emulated ALUN_CD.EXE (France), applied to RAM
right after the world is built (before the spawn), never simulated.  Every mutant runs the same eleven scenarios as its control; a mutant is KILLED when at least one scenario
key differs from the control's, and the scenarios that differ are listed.  A SURVIVED mutant must be proven equivalent in writing (none is expected).

  Scenario keys (control values are the oracle values of the plan, section 1.18):
   S1 wall      arrow (3) Z+16 px into the wall column 15 (mask 0x40, height 0)            (first ForceAdjusted tick, X at that tick, last X)
   S2 step32    arrow (3) Z+16 px against a 32 px step (height byte 2)                        (first ForceAdjusted tick, last X)
   S3 ball      fire ball (11) born at Z+32 px, flat field, 14 ticks                          (PosZ per tick, IsOnGround per tick)
   S4 ballstep  fire ball (11) born at Z+32 px, 16 px step at column 6                        (PosZ per tick, ForceAdjusted tick)
   S5 slide     arrow (3), wall column 15 on rows >= 7, y = 106                               (first ForceAdjusted tick, last X, last Y, last status)
   S6 edge      arrow (3) from x = 1200 to the map edge                                       (first ForceAdjusted tick, X at that tick)
   S7 spawn     arrow (3) spawn heights 0 and -1 raw                                          (initial PosZ)
   S8 marker    Beannoid hop whose animation impulse is 0x8000, with and without gravity     (PosZ and ForceZ per tick)
   S9 ceiling   Beannoid hop started at Z = 1905 px (ceiling 1920 px)                         (PosZ, ForceZ, landing flag per tick)
   S10 magnet   Beannoid walking down the ramp at speeds 320 and 640                         (ticks with IsOnGround = 0)
   S11 hop      Beannoid hop on flat ground, 40 ticks                                          (PosZ, ForceZ, landing flag per tick)

  M01 0x80037440 slt v0,v0,t0 -> slt v0,t0,v0          height test of the non-hero blocking function reversed
  M02 0x80037430 bnez v0 -> nop                         the cell mask no longer blocks
  M03 0x800373F4 addiu a3,zero,0x40 -> 0x80             the non-ClassB mask word 0x40 -> 0x80
  M04 0x80036AE0 andi v0,v0,0x100 -> 0x200              gravity test inside the 0x8000-marker branch
  M05 0x80036860 ori s5,zero,0x8000 -> 0x8001           the marker constant
  M06 0x80036B10 andi v0,v0,0x100 -> 0x200              gravity test of the force law
  M07 0x80036C24 slt v0,v1,t1 -> slt v0,t1,v1           landing comparison reversed
  M08 0x80036C30 addiu t1,v1,1 -> addiu t1,v1,0         landing height T + 1 -> T
  M09 0x80036DC4 lui v0,0x780 -> lui v0,0x700           the ceiling constant
  M10 0x80039EA4 addiu v0,v0,1 -> addiu v0,v0,0         the +1 of the creation
  M11 0x800379C4 andi v0,v0,0x2000 -> 0x1000            the NoObstacleSlide test
  M12 0x8003786C lui v1,3 -> lui v1,2                   magnet window 3 px -> 2 px
  M13 0x80036788 slt v0,a1,a3 -> slt v0,a3,a1           the X clipping bound comparison reversed
  M14 0x80037850 bnez v0 -> nop                         magnet also with ForceZ != 0
  M15 0x80036B48 slt v0,a1,a0 -> slt v0,a0,a1           the +-(V << 8) clamp comparison reversed
  M16 0x80036B30 subu v1,v1,v0 -> addu v1,v1,v0         the gravity term added instead of subtracted
  M17 0x800376F4 andi v1,v1,0x100 -> 0x200              the gravity test after a landing (ForceZ cleared)
  M18 0x80037674 andi v0,v0,0x100 -> 0x200              the gravity test after a blocked rise

usage (from the directory of this file):  python -B -I mutants_e0k.py        writes out/mutations.txt and out/mutations.json
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
from k0_arrows import wall_col, step_cols, hunter_arrow
from k0_arrows2 import arrow_run
import k0_walker as WK

OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)

MUTANTS = [
    ('M01', 0x80037440, 0x0102102A, 'slt v0,v0,t0 -> slt v0,t0,v0 (height test reversed)'),
    ('M02', 0x80037430, 0x00000000, 'bnez v0 -> nop (the cell mask no longer blocks)'),
    ('M03', 0x800373F4, 0x24070080, 'addiu a3,zero,0x40 -> addiu a3,zero,0x80 (mask word)'),
    ('M04', 0x80036AE0, 0x30420200, 'andi v0,v0,0x100 -> 0x200 (gravity test in the 0x8000 branch)'),
    ('M05', 0x80036860, 0x34158001, 'ori s5,zero,0x8000 -> 0x8001 (marker constant)'),
    ('M06', 0x80036B10, 0x30420200, 'andi v0,v0,0x100 -> 0x200 (gravity test of the force law)'),
    ('M07', 0x80036C24, 0x0123102A, 'slt v0,v1,t1 -> slt v0,t1,v1 (landing comparison reversed)'),
    ('M08', 0x80036C30, 0x24690000, 'addiu t1,v1,1 -> addiu t1,v1,0 (landing height T+1 -> T)'),
    ('M09', 0x80036DC4, 0x3C020700, 'lui v0,0x780 -> lui v0,0x700 (ceiling)'),
    ('M10', 0x80039EA4, 0x24420000, 'addiu v0,v0,1 -> addiu v0,v0,0 (creation +1)'),
    ('M11', 0x800379C4, 0x30421000, 'andi v0,v0,0x2000 -> 0x1000 (NoObstacleSlide test)'),
    ('M12', 0x8003786C, 0x3C030002, 'lui v1,3 -> lui v1,2 (magnet window 3 px -> 2 px)'),
    ('M13', 0x80036788, 0x00E5102A, 'slt v0,a1,a3 -> slt v0,a3,a1 (clip bound comparison reversed)'),
    ('M14', 0x80037850, 0x00000000, 'bnez v0 -> nop (magnet also with ForceZ != 0)'),
    ('M15', 0x80036B48, 0x0085102A, 'slt v0,a1,a0 -> slt v0,a0,a1 (clamp comparison reversed)'),
    ('M16', 0x80036B30, 0x00621821, 'subu v1,v1,v0 -> addu v1,v1,v0 (gravity term sign)'),
    ('M17', 0x800376F4, 0x30630200, 'andi v1,v1,0x100 -> 0x200 (gravity test after a landing: ForceZ cleared)'),
    ('M18', 0x80037674, 0x30420200, 'andi v0,v0,0x100 -> 0x200 (gravity test after a blocked rise)'),
]

_PATCHES = []
_orig_make_world = K.make_world


def _patched_make_world(*a, **kw):
    m = _orig_make_world(*a, **kw)
    for addr, word in _PATCHES:
        m.poke(addr, word)
    return m


K.make_world = _patched_make_world


def first(rows, s, pred):
    for r in rows:
        if pred(r['slots'][s]):
            return r['tick']
    return None


def s1():
    m, s, init, rows = hunter_arrow(wall_col(15), typ=3, n=60, z_px=16)
    t = first(rows, s, lambda r: r['adj_13C'] != 0)
    return (t, rows[t - 1]['slots'][s]['px'] if t else None, rows[-1]['slots'][s]['px'])


def s2():
    m, s, init, rows = hunter_arrow(step_cols(15, 30, 2), typ=3, n=60, z_px=16)
    t = first(rows, s, lambda r: r['adj_13C'] != 0)
    return (t, rows[-1]['slots'][s]['px'])


def s3():
    m, s, init, rows = hunter_arrow(None, typ=11, n=14, z_px=32)
    return ([r['slots'][s]['pz'] for r in rows], [r['slots'][s]['ground_144'] for r in rows])


def s4():
    m, s, init, rows = hunter_arrow(step_cols(6, 12, 1), typ=11, n=14, z_px=32)
    return ([r['slots'][s]['pz'] for r in rows], first(rows, s, lambda r: r['adj_13C'] != 0))


def s5():
    def partial_wall(col, row):
        return dict(wk=0x40) if col == 15 and row >= 7 else None
    m, s, init, rows = arrow_run(cells=partial_wall, typ=3, n=70, y=106, hero_px=(100, 106))
    t = first(rows, s, lambda r: r['adj_13C'] != 0)
    f = rows[-1]['slots'][s]
    return (t, f['px'], f['py'], f['status'])


def s6():
    m, s, init, rows = arrow_run(cells=None, typ=3, n=20, x=1200, hero_px=(1200, 100))
    t = first(rows, s, lambda r: r['adj_13C'] != 0)
    return (t, rows[t - 1]['slots'][s]['px'] if t else None)


def s7():
    out = []
    for zx in (0, -1):
        m, s, init, rows = arrow_run(cells=None, typ=3, n=2, z_px=0, z_extra=zx)
        out.append(init['pz'])
    return tuple(out)


_LAST_INFO = []
_orig_srec = K.spawn_record_entities


def _srec(*a, **kw):
    r = _orig_srec(*a, **kw)
    _LAST_INFO[:] = [r[1]]
    return r


K.spawn_record_entities = _srec
BEAN_ANIM_HOP = 8


def _beannoid(n, impulse=None, no_gravity=False, z_raw=None, anim=BEAN_ANIM_HOP, cells=None):
    """Beannoid (map 14, sprite 79) hopping (animation 8, impulse 1280 unless `impulse` is written into the real animation record: signed half at +10 of the 14-byte set);
    optional harness writes at tick 1: gravity bit cleared, PosZ."""
    def pre_extra(m_, t, s_):
        if t == 1:
            if impulse is not None:
                m_.poke16(_LAST_INFO[0]['animsets'] + 14 * BEAN_ANIM_HOP + 10, impulse & 0xFFFF)
            if no_gravity:
                K.poke_entity(m_, s_, flags6C=m_.peek(K.ent(s_) + 0x6C) & ~0x100)
            if z_raw is not None:
                K.place(m_, s_, z_raw=z_raw)
    m, s, init, rows = WK.walker(cells=cells, anim=anim, n=n, pos=(100, 100), pre_extra=pre_extra)
    return s, rows


def s8():
    """the 0x8000 marker: a hop whose animation impulse is 0x8000, with and without the gravity bit (written into the real animation record before the first tick)"""
    out = []
    for ng in (False, True):
        s, rows = _beannoid(8, impulse=0x8000, no_gravity=ng, z_raw=200 << 16)   # 200 px up: the gravity variant falls 128 px per tick, the other keeps its Z
        out.append(([r['slots'][s]['pz'] for r in rows], [r['slots'][s]['fz_B8'] for r in rows]))
    return tuple(out)


def s9():
    """the ceiling: the hop starts at Z = 1905 px (the ceiling is 1920 px)"""
    s, rows = _beannoid(12, z_raw=1905 << 16)
    return ([r['slots'][s]['pz'] for r in rows], [r['slots'][s]['fz_B8'] for r in rows], [r['slots'][s]['collz_140'] for r in rows])


def s11():
    """a hop on flat ground: PosZ, ForceZ and the landing flag per tick (landing clears ForceZ only with the gravity bit)"""
    s, rows = _beannoid(40)
    return ([r['slots'][s]['pz'] for r in rows], [r['slots'][s]['fz_B8'] for r in rows], [r['slots'][s]['collz_140'] for r in rows])


def s10():
    def ramp_down2(col, row):
        if col < 3:
            return dict(h=4)
        if col <= 6:
            return dict(h=4 - (col - 3), slope=2)
        return None
    out = []
    for sp in (320, 640):
        m, s, init, rows = WK.walker(cells=ramp_down2, anim=7, n=45, pos=(40, 100), speed_override={7: sp})
        out.append([r['tick'] for r in rows if r['slots'][s]['ground_144'] == 0])
    return tuple(out)


SCENARIOS = [('S1 wall', s1), ('S2 step32', s2), ('S3 ball', s3), ('S4 ballstep', s4), ('S5 slide', s5), ('S6 edge', s6), ('S7 spawn', s7), ('S8 marker', s8), ('S9 ceiling', s9), ('S10 magnet', s10), ('S11 hop', s11)]


def run_all(patches):
    del _PATCHES[:]
    _PATCHES.extend(patches)
    res = {}
    for name, fn in SCENARIOS:
        try:
            res[name] = fn()
        except Exception as e:                      # a mutant may break a run (the scenario then counts as differing)
            res[name] = 'EXCEPTION %s: %s' % (type(e).__name__, e)
    return res


def main():
    control = run_all([])
    rows = []
    for name, addr, word, desc in MUTANTS:
        res = run_all([(addr, word)])
        diff = [k for k in control if res[k] != control[k]]
        rows.append(dict(name=name, address='0x%08X' % addr, word='0x%08X' % word, patch=desc, verdict='KILLED' if diff else 'SURVIVED', killed_by=diff,
                         control={k: control[k] for k in diff}, mutant={k: res[k] for k in diff}))
        print(name, rows[-1]['verdict'], diff)
        sys.stdout.flush()
    out = dict(control=control, rows=rows)
    json.dump(out, open(os.path.join(OUT, 'mutations.json'), 'w', newline='\n'), indent=1, sort_keys=True)
    lines = ['# E20.e0k T0 mutants: %d one-word patches of the emulated binary, applied to RAM before the spawn; eleven scenarios per run; KILLED = at least one scenario key differs from the control' % len(rows)]
    lines.append('control keys:')
    for k in control:
        lines.append('   %-12s %s' % (k, control[k]))
    for r in rows:
        lines.append('%-3s %-9s %s %s  %s' % (r['name'], r['verdict'], r['address'], r['word'], r['patch']))
        lines.append('      killed by: %s' % (', '.join(r['killed_by']) or '-'))
        for k in r['killed_by']:
            lines.append('      %-12s control %s' % (k, r['control'][k]))
            lines.append('      %-12s mutant  %s' % ('', r['mutant'][k]))
    lines.append('killed %d of %d' % (sum(1 for r in rows if r['verdict'] == 'KILLED'), len(rows)))
    open(os.path.join(OUT, 'mutations.txt'), 'w', newline='\n').write('\n'.join(lines) + '\n')
    return 0 if all(r['verdict'] == 'KILLED' for r in rows) else 2


if __name__ == '__main__':
    sys.exit(main())
