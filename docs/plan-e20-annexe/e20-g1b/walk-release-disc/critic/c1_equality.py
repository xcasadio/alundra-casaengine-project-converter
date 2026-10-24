"""c1_equality.py - CRITIC: UpdatePlayerCarriedEntity 0x8002F884 (REAL code) on hand-built EQUALITY and THRESHOLD cases, written independently of g10.
Questions: (1) mode 0 at max(|dx|,|dy|) == |dz - 0x200000| : does X/Y ease?  (R1 says yes, R2/h-g-deps say no.)
           (2) mode 1 destroy threshold: offsets 0x1FFFF vs 0x20000 on each axis, counter 4 vs 5.
           (3) mode 0 never reads hero+0x30..38 (stale values) and resets the counter ; mode 0 with counter 3.
Harness writes: hero.+0x2C = crate, the two positions, the counter [0x80098F28], stale hero offsets.  Nothing else.
Run: python -B -I c1_equality.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl
import k0lib as K
from machine import M32, s32

HERO = gl.HERO
CTR = 0x80098F28
P = 65536


def setup():
    m = gl.world(hero_px=(100, 100))
    c = gl.add_crate(m, 130, 100)
    return m, c, K.ent(c)


def call(m, ce, mode, off, ctr=0, stale=(0x12345, 0x23456, 0x34567)):
    hx, hy, hz = 100 * P, 100 * P, 0
    m.poke(HERO + 0x2C, ce)
    m.poke(HERO + 0x114, hx); m.poke(HERO + 0x118, hy); m.poke(HERO + 0x11C, hz)
    m.poke(ce + 0x114, (hx + off[0]) & M32); m.poke(ce + 0x118, (hy + off[1]) & M32); m.poke(ce + 0x11C, (hz + off[2]) & M32)
    m.poke(ce + 0x10, 2)
    m.poke(HERO + 0x30, stale[0]); m.poke(HERO + 0x34, stale[1]); m.poke(HERO + 0x38, stale[2])
    m.poke(CTR, ctr)
    m.call(0x8002F884, mode)
    return (s32(m.peek(HERO + 0x30)), s32(m.peek(HERO + 0x34)), s32(m.peek(HERO + 0x38))), m.peek(CTR), m.peek(ce + 0x10)


def main():
    out = []
    m, c, ce = setup()
    # (1) equality in mode 0
    cases = [
        ('dx=dz-err (21px, 0, 11px): zerr=21px', (21 * P, 0, 11 * P)),
        ('one raw unit less: max 21px-1 < zerr 21px', (21 * P - 1, 0, 11 * P)),
        ('one raw unit more: max 21px+1 > zerr 21px', (21 * P + 1, 0, 11 * P)),
        ('dy only (0, -21px, 11px) equality on Y', (0, -21 * P, 11 * P)),
        ('dz above target (0x200000+5px): zerr=5px, dx=5px', (5 * P, 0, 37 * P)),
        ('dz above target, dx=5px-1', (5 * P - 1, 0, 37 * P)),
        ('at target (0,0,32px): nothing to ease', (0, 0, 32 * P)),
    ]
    out.append('MODE 0 (stale hero offsets 0x12345,0x23456,0x34567 must be overwritten; counter reset)')
    for name, off in cases:
        got, ctr, st = call(m, ce, 0, off, ctr=3)
        out.append('  %-52s off=%s -> %s ctr=%d  X eased=%s' % (name, tuple(round(v / P, 5) for v in off), tuple(round(v / P, 5) for v in got), ctr, got[0] != off[0]))
    # (2) mode 1 thresholds, counter >= 5
    out.append('MODE 1, counter 5 (AttackCount of the crate sheet is 1 in this run: real sheet)')
    for name, off in [('(0x1FFFF,0,0)', (0x1FFFF, 0, 0)), ('(0x20000,0,0)', (0x20000, 0, 0)), ('(0,0x1FFFF,0x1FFFF)', (0, 0x1FFFF, 0x1FFFF)), ('(0,0,0x20000)', (0, 0, 0x20000)),
                      ('(-0x1FFFF,-0x1FFFF,-0x1FFFF)', (-0x1FFFF,) * 3), ('(-0x20000,0,0)', (-0x20000, 0, 0)), ('(0,0,32px) start of the easing', (0, 0, 32 * P))]:
        got, ctr, st = call(m, ce, 1, off, ctr=5)
        out.append('  %-36s -> hero off=%s ctr=%d status=%d (4 = destroyed)' % (name, tuple(hex(v) for v in got), ctr, st))
    out.append('MODE 1 counter ticks: counter 4 -> %s ; counter 0 -> %s' % (call(m, ce, 1, (0, 0, 32 * P), ctr=4)[1:], call(m, ce, 1, (0, 0, 32 * P), ctr=0)[1:]))
    # (3) trace of the whole mode 0 easing from the lift offsets, 40 calls, offsets as the routine itself recomputes them from the positions
    off = (21 * P, 0, 0)
    tr = []
    for i in range(36):
        tr.append(off)
        got, ctr, st = call(m, ce, 0, off)
        off = got
    out.append('MODE 0 easing trace from (21,0,0) px (call k reads the previous call output): first calls ' + ' '.join('(%g,%g,%g)' % tuple(v / P for v in o) for o in tr[:3]) + ' ... Z reaches 21 at call %d, XY first move at call %d, done at call %d' % (
        next(i for i, o in enumerate(tr) if o[2] == 21 * P), next(i for i, o in enumerate(tr) if o[0] != 21 * P), next(i for i, o in enumerate(tr) if o == (0, 0, 32 * P))))
    txt = '\n'.join(out)
    print(txt)
    open(os.path.join(gl.OUT, 'c1-equality.txt'), 'w').write(txt + '\n')


if __name__ == '__main__':
    main()
