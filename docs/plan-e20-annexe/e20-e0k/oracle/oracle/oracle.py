"""oracle.py - D1 oracle of MovePlayer (E20.0): scenarios S-A and S-B of docs/plan-e19-d2c-valeurs.md B.1, run on the emulated binary.

This file COMPUTES the traces from the emulated machine only.  It contains no expected value of the annex; the comparison is a separate step
(compare.py), which parses the annex text.

usage: python -B oracle.py [outdir]
writes outdir/S-A.trace.txt, S-A.trace.json, S-B.trace.txt, S-B.trace.json (+ warm-up traces) and outdir/run.log
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from machine import Machine, s32
import world as W

START_X, START_Y = 100, 100          # px, far from every clamp of the 52 x 60 cell field (1248 x 960 px)
NTICKS = 24

FIELDS = [
    ('tick', None), ('hold', None),
    ('anim_cur', W.F['anim_cur']), ('anim_target', W.F['anim_target']), ('dir', W.F['dir_target']),
    ('force_x', W.F['forcex']), ('force_y', W.F['forcey']), ('force_z', W.F['forcez']),
    ('pos_x', W.F['posx']), ('pos_y', W.F['posy']), ('pos_z', W.F['posz']),
    ('on_ground', W.F['ground']), ('coll_z', W.F['coll']), ('b0', W.F['animcounter']), ('cnt1c', W.F['cnt1c']),
]
MP_POINTS = [      # (address, must be reached, meaning) - decoded in out/dis_moveplayer.txt
    (0x80031BA4, True, 'entry reads the hero +0x20'),
    (0x80031BC4, True, '+0x20 == 0: the tick goes on (the exit at 0x80031BB4 is not taken)'),
    (0x80031BB4, False, 'early exit: +0x20 != 0'),
    (0x80031BDC, False, 'locked branch: [0x800DC4B8] & 0x34'),
    (0x80031C30, True, 'free branch: flag 0x100 set, CreatePlayerAnimationEffects(0)'),
    (0x80031C44, True, 'damage entry 0x80031A68 called'),
    (0x80031C58, False, 'damage taken (result != -1): state 0x31/0x3A'),
    (0x80031CA0, True, 'no damage: continue'),
    (0x800327A4, False, 'branch of death: +0x14 == 0'),
    (0x80032768, False, 'branch +0x188 & 0x80'),
    (0x80031D68, True, 'prologue calls 0x8002F49C, 0x8002F768, 0x8002F884(0)'),
    (0x80031D8C, True, 'state switch (table 0x80022D3C)'),
    (0x80032914, True, 'epilogue: 0x800307E8, SetPlayerHpMax, SetPlayerHp'),
]
GLOBALS = [('sprint_cnt_80126FF4', 0x80126FF4), ('charge_cnt_80126FFC', 0x80126FFC)]


def snap(m, tick, hold):
    P = W.ent(0)
    d = {'tick': tick, 'hold': hold}
    for name, off in FIELDS[2:]:
        d[name] = m.peeks(P + off)
    for name, a in GLOBALS:
        d[name] = m.peeks(a)
    return d


def setup(seed=0x12345678, mutate=None, direction=24, gravity=128, visc=4096):
    """a fresh machine: world, BSS words, balance blob, new-game stats, the hero, the random seed.  mutate(m) runs last (mutation tests only)."""
    m = Machine()
    W.build_world(m, gravity=gravity, visc=visc)
    W.init_bss(m)
    W.load_balance(m, 0)
    kit = W.init_stats(m)
    info = W.place_hero(m, START_X, START_Y, direction=direction)
    W.init_pad2(m)
    m.poke(0x80098708, seed)                         # the binary's pseudo-random state (lui 0x800a; lw -0x78f8 at 0x80032988/0x8003298C), noted; consumed by the landing dust at tick 22 (E20.0 check of D1)
    setup_unread = sorted((pc, a, n, c) for (pc, a, n), c in m.uninit_reads.items())   # real init code (new-game stats, balance, InitializeEntity): must be empty
    if mutate:
        mutate(m)
    m.uninit_reads.clear()
    m.calls.clear()
    m.journal.clear()
    return m, dict(kit=kit, bank=info['stats'], seed=seed, setup_unread=setup_unread)


def step(m, pad, hold, rows, tick, skip=()):
    m.tick_no = tick
    W.tick(m, pad, hold, skip=skip)
    rows.append(snap(m, tick, hold))


def fmt(rows):
    cols = ['tick', 'hold', 'anim_target', 'anim_cur', 'dir', 'force_x', 'force_y', 'force_z', 'pos_x', 'pos_y', 'pos_z', 'on_ground', 'coll_z', 'b0', 'cnt1c',
            'sprint_cnt_80126FF4', 'charge_cnt_80126FFC']
    out = ['\t'.join(cols)]
    for r in rows:
        out.append('\t'.join(('0x%04X' % r[c]) if c == 'hold' else str(r[c]) for c in cols))
    return '\n'.join(out) + '\n'


def scenario_A(skip=(), mutate=None, cross_held=False, idle_warm=0, **kw):
    """S-A: standing jump.  The hero is at rest in Idle, grounded, forces zero (the state InitializeEntity leaves).  Tick 1 = Cross edge, no stick."""
    m, meta = setup(mutate=mutate, **kw)
    pad = W.Pad()
    warm = []
    for t in range(-idle_warm, 0):
        step(m, pad, 0, warm, t, skip)
    pre = snap(m, 0, 0)
    rows = []
    for t in range(1, NTICKS + 1):
        hold = W.CROSS if (t == 1 or cross_held) else 0
        step(m, pad, hold, rows, t, skip)
    return m, meta, pre, rows, warm


def scenario_B(skip=(), mutate=None, cross_held=False, max_warm=60, **kw):
    """S-B: jump walking east.  Warm-up: Right held until two consecutive ticks in Moving (current animation 1) with ForceX = 159744
    (159744 = 208 * 768, the steady walk of the binary); the start position is the one at the end of the warm-up.  Then tick 1 = Cross edge,
    Right still held."""
    m, meta = setup(mutate=mutate, **kw)
    pad = W.Pad()
    warm = []
    streak = 0
    for t in range(-max_warm, 0):
        step(m, pad, W.RIGHT, warm, t, skip)
        r = warm[-1]
        if r['anim_cur'] == 1 and r['force_x'] == 159744:
            streak += 1
        else:
            streak = 0
        if streak == 2:
            break
    else:
        raise RuntimeError('warm-up did not reach two Moving ticks at ForceX 159744')
    for i, r in enumerate(warm):                      # label the warm-up ticks -n .. -1 (the last one is -1)
        r['tick'] = i - len(warm)
    pre = snap(m, 0, W.RIGHT)
    rows = []
    for t in range(1, NTICKS + 1):
        hold = W.RIGHT | (W.CROSS if (t == 1 or cross_held) else 0)
        step(m, pad, hold, rows, t, skip)
    return m, meta, pre, rows, warm


def write_scenario(name, res, outdir):
    m, meta, pre, rows, warm = res
    with open(os.path.join(outdir, name + '.trace.txt'), 'w', encoding='utf-8') as f:
        f.write('# %s: per-tick trace computed by the emulated binary (ALUN_CD.EXE France), binary convention (PosZ raw: rest = terrain + 1)\n' % name)
        f.write('# values after the tick; pos_x/pos_y absolute 16.16; the start state (before tick 1) is the line "tick 0"\n')
        if warm:
            f.write('# warm-up ticks (negative tick numbers) come first\n')
            f.write(fmt(warm))
        f.write(fmt([pre] + rows))
    json.dump(dict(meta=meta, start=pre, rows=rows, warm=warm), open(os.path.join(outdir, name + '.trace.json'), 'w'), indent=1)
    with open(os.path.join(outdir, name + '.machine.txt'), 'w', encoding='utf-8') as f:
        f.write('# functions entered by jal/jalr during the scenario (address: count)\n')
        for a, c in sorted(m.calls.items()):
            f.write('%08x: %d\n' % (a, c))
        f.write('# reads of bytes nobody had written during the SETUP (real init code), must be empty: %s\n' % (meta['setup_unread'] or 'empty'))
        f.write('# reads of bytes nobody had written during the ticks (pc, address, size: count), must be empty\n')
        for (pc, a, n), c in sorted(m.uninit_reads.items()):
            f.write('pc %08x addr %08x size %d: %d\n' % (pc, a, n, c))
        f.write('# MovePlayer 0x80031B50: points that must be reached (R) or must not be reached (N) for the hero to run the normal path\n')
        for a, want, what in MP_POINTS:
            f.write('%08x %s %-8s %s\n' % (a, 'R' if want else 'N', 'REACHED' if a in m.cov else 'not hit', what))
        f.write('# journal of stubbed calls (tick, kind, args)\n')
        for j in m.journal:
            f.write(repr(j) + '\n')


def main(outdir):
    os.makedirs(outdir, exist_ok=True)
    log = []
    for name, fn in (('S-A', scenario_A), ('S-B', scenario_B)):
        res = fn()
        write_scenario(name, res, outdir)
        m, meta, pre, rows, warm = res
        log.append('%s: start %s' % (name, json.dumps(pre)))
        log.append('%s: warm-up ticks %d, comparison ticks %d, uninitialised-read sites %d' % (name, len(warm), len(rows), len(m.uninit_reads)))
    open(os.path.join(outdir, 'run.log'), 'w').write('\n'.join(log) + '\n')
    print('\n'.join(log))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out'))
