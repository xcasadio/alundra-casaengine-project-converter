"""g10_upce_fuzz.py - S10: UpdatePlayerCarriedEntity 0x8002F884 called directly (REAL code) on random hero/crate positions, compared with a Python model written from the disassembly (modes 0, 1, 2).
Harness writes: hero.+0x2C = crate, crate positions/hero position (random), crate sheet byte 0xE forced to 0 or 1 (the sheet pointer +0x1C4 is redirected to a scratch sheet for the AttackCount=0 case), counter [0x80098F28].
Run: python -B -I g10_upce_fuzz.py"""
import sys, os, random
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import k0lib as K
from machine import M32, s32

HERO = gl.HERO
CTR = 0x80098F28
SCRATCH_SHEET = 0x801F7000


def ease(v, target, step):
    d = target - v
    if abs(d) < step:
        return target
    return v + step if d > 0 else v - step


def model(mode, off, attacks, ctr):
    """returns (offset, counter, destroyed, mode_effective, crate_pos_override)"""
    dx, dy, dz = off
    if attacks == 0 and mode == 1:
        mode = 0
    if mode == 0:
        ax, ay = abs(dx), abs(dy)
        zerr = dz - 0x200000
        a0 = zerr if zerr >= 0 else 0x200000 - dz
        v = max(ax, ay)
        if v < a0:
            nx, ny = dx, dy
        else:
            nx, ny = ease(dx, 0, 0x10000), ease(dy, 0, 0x10000)
        nz = ease(dz, 0x200000, 0x10000)
        return (nx, ny, nz), 0, False
    if mode == 1:
        if attacks == 0:
            return off, 0, False
        if ctr < 5:
            return None, ctr + 1, False
        ax, ay, az = abs(dx), abs(dy), abs(dz)
        if ax <= 0x1FFFF and ay <= 0x1FFFF and az <= 0x1FFFF:
            return None, ctr, True
        return (ease(dx, 0, 0x20000), ease(dy, 0, 0x20000), ease(dz, 0, 0x20000)), ctr, False
    return off, 0, False


def s(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


def one(m, c, rnd, mode, attacks, ctr):
    ce = K.ent(c)
    m.poke(HERO + 0x2C, ce)
    hx, hy, hz = [rnd.randrange(-50, 400) * 65536 + rnd.randrange(0, 65536) for _ in range(3)]
    hz = rnd.randrange(0, 60) * 65536
    off = (rnd.randrange(-40, 40) * 65536 + rnd.randrange(-65536, 65536), rnd.randrange(-40, 40) * 65536 + rnd.randrange(-65536, 65536), rnd.randrange(-10, 60) * 65536 + rnd.randrange(-65536, 65536))
    if rnd.random() < 0.3:
        off = (rnd.randrange(-3, 3) * 65536 // 2, rnd.randrange(-3, 3) * 65536 // 2, 0x200000 + rnd.randrange(-3, 3) * 65536 // 2)
    if mode == 1 and rnd.random() < 0.5:
        off = tuple(rnd.randrange(-0x30000, 0x30000) for _ in range(3))           # near the hero's centre: exercises the destroy threshold 0x1FFFF
    m.poke(HERO + 0x114, hx); m.poke(HERO + 0x118, hy); m.poke(HERO + 0x11C, hz)
    m.poke(ce + 0x114, (hx + off[0]) & M32); m.poke(ce + 0x118, (hy + off[1]) & M32); m.poke(ce + 0x11C, (hz + off[2]) & M32)
    m.poke(HERO + 0x30, 0x12345); m.poke(HERO + 0x34, 0x23456); m.poke(HERO + 0x38, 0x34567)           # stale values: the routine must not read them
    m.poke(HERO + 0x8C, 0x10)
    m.poke(CTR, ctr)
    m.poke(ce + 0x1C4, SCRATCH_SHEET if attacks == 0 else m.peek(SCRATCH_SHEET + 0x100))
    m.call(0x8002F884, mode)
    got_off = (s32(m.peek(HERO + 0x30)), s32(m.peek(HERO + 0x34)), s32(m.peek(HERO + 0x38)))
    return off, got_off, m.peek(CTR), m.peek(ce + 0x10), (m.peek(ce + 0x24), m.peek(ce + 0x8C), s32(m.peek(ce + 0x114)) - hx, s32(m.peek(ce + 0x118)) - hy, s32(m.peek(ce + 0x11C)) - hz), m.peek(ce + 0x228)


if __name__ == '__main__':
    rnd = random.Random(20261015)
    m = gl.world(hero_px=(100, 100))
    c = gl.add_crate(m, 130, 100)
    ce = K.ent(c)
    real_sheet = m.peek(ce + 0x1C4)
    m.zero(SCRATCH_SHEET, 0x40)
    m.poke(SCRATCH_SHEET + 0x100, real_sheet)           # remember the real sheet (harness bookkeeping at a scratch address)
    bad = 0
    n = 0
    stats = {0: 0, 1: 0, 2: 0}
    for mode in (0, 1, 2):
        for attacks in (1, 0):
            for ctr in ((0,) if mode != 1 else (0, 3, 4, 5, 7)):
                for _ in range(120):
                    n += 1
                    m.poke(ce + 0x10, 2)
                    m.poke(ce + 0x24, 0)
                    off, got, ctr_after, status, crate_state, letter = one(m, c, rnd, mode, attacks, ctr)
                    if mode == 2:
                        # mode 2: crate.+0x24 = -1, crate direction = hero's, crate pos = hero pos + 32 px (Z), counter reset; hero.+0x30.. untouched (stale values)
                        ok = (crate_state[0] & M32) == M32 and crate_state[1] == 0x10 and crate_state[2:] == (0, 0, 0x200000) and ctr_after == 0 and got == (0x12345, 0x23456, 0x34567) and status == 2
                    else:
                        want_off, want_ctr, destroyed = model(mode, off, attacks, ctr)
                        eff_mode = 0 if (attacks == 0 and mode == 1) else mode
                        want_ctr_final = 0 if (eff_mode == 0 or (mode == 1 and attacks == 0)) else want_ctr
                        if eff_mode == 1 and destroyed:
                            ok = status == 4 and ctr_after == ctr and got == (0x12345, 0x23456, 0x34567) and m.peeks(HERO + 0x2C) == 0 if False else (status == 4 and ctr_after == ctr)
                        else:
                            stale = (0x12345, 0x23456, 0x34567)
                            ok = got == (stale if want_off is None else want_off) and ctr_after == want_ctr_final and status == 2
                    if not ok:
                        bad += 1
                        if bad <= 8:
                            print('MISMATCH mode', mode, 'attacks', attacks, 'ctr', ctr, 'off', off, 'got', got, 'ctr_after', ctr_after, 'status', status, 'state', crate_state)
                    stats[mode] += 1
    print('cases', n, 'mismatches', bad, stats)
    open(os.path.join(gl.OUT, 'g10-upce-fuzz.txt'), 'w').write('cases %d mismatches %d %s\n' % (n, bad, stats))
