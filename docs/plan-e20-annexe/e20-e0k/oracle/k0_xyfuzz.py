"""k0_xyfuzz.py - the real ComputeXYPosition 0x80037730 (called on its own, non-hero, NoObstacleSlide set, no entity around) against an independent 1-D python model of its refinement loop.
Model (read at 0x800377C0-0x80037DFC): attempt X += f; blocked -> restore, f = (f == -1 ? 0 : f >> 1), retry while f != 0; accepted -> if it was the first attempt stop, else f halved and retry while f != 0
(after the first accepted refinement the loop goes on from the NEW position); ForceAdjusted (+0x13C) = 1 only when the loop ends on a blocked attempt AND no sub-step was ever accepted in this call (0x800379A4 bnez sp+0x2C -> 0x80037D58 skips the store), NoObstacleSlide: no slide.
Blocked(x): the body corner pixels (x + off) >> 16 and (x + off + size) >> 16 fall in the wall column (cell walkability bit 0x40; flat terrain 0 below the body).
Run: python -B -I k0_xyfuzz.py [N]"""
import sys, os, random
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
import world as W
from k0_arrows import OUT

OFFX, SIZEX = -10, 21           # arrow: body offset -10 px, width 21 px (stored (21 << 16) - 1)
M = K.M32


def col_of(px):
    # pixel -> column table 0x80127168 (zsim.py / world.py: min(x // 24, 51)); the binary clamps the pixel to 0..0x33 columns via the table read (blez -> 0)
    if px <= 0:
        return 0
    return min(px // 24, 51)


def model(x, f, wall):
    def blocked(xx):
        x1 = (xx + (OFFX << 16)) >> 16
        x2 = (xx + (OFFX << 16) + (SIZEX << 16) - 1) >> 16
        return col_of(x1) == wall or col_of(x2) == wall
    it = 0
    moved_ok = False
    adj = 0
    if f == 0:
        return x, 0
    while True:
        x0 = x
        x = x0 + f
        if not blocked(x):
            if it == 0:
                return x, 0
            moved_ok = True
            f = 0 if f == -1 else f >> 1
            if f == 0:
                return x, 0
            it += 1
            continue
        x = x0
        f = 0 if f == -1 else f >> 1
        if f == 0:
            return x, (0 if moved_ok else 1)              # END_BLOCK (0x8003799C): moved_ok != 0 -> 0x80037D58 (the store of 0x13C is skipped); else NoObstacleSlide -> 0x80037D54 stores 1
        it += 1


def run(n=3000, seed=1):
    rnd = random.Random(seed)
    bad = []
    stats = dict(total=0, flag=0)
    m = K.make_world(hero_px=(30, 30))
    s = K.spawn_global(m, 3, 100, 100, K.hero_z(m) + (16 << 16), 24)
    e = K.ent(s)
    prev = None
    def cell(col, row):
        return W.TILES + 0x604 + row * W.ROW + col * 8
    for i in range(n):
        wall = rnd.randint(8, 40)
        east = rnd.random() < 0.5
        if east:
            xp = wall * 24 - 11 - rnd.randint(0, 80)
        else:
            xp = (wall + 1) * 24 + 10 + rnd.randint(0, 80)
        x0 = (xp << 16) + rnd.randint(0, 65535)
        if rnd.random() < 0.5:                      # next to the limit: gap 0..40 raw units
            gap = rnd.choice([0, 0, 1, 2, 3, 7, 40, 255])
            x0 = ((wall * 24 - 11) << 16) - gap if east else (((wall + 1) * 24 + 10) << 16) + gap
        f = rnd.choice([1, 2, 3, 5, 100, 4096, 65535, 98304, 196608, 393216, 600000, 777777, 1000000]) * (1 if east else -1)
        if rnd.random() < 0.3:
            f += rnd.randint(-50, 50)
        if f == 0:
            f = 7
        if prev is not None:
            for row in (5, 6):
                m.poke16(cell(prev, row), 0)
        for row in (5, 6):
            m.poke16(cell(wall, row), 0x40)
        prev = wall
        m.poke(e + 0x114, x0 & M); m.poke(e + 0x118, 100 << 16)
        m.poke(e + 0x1D8, (x0 + (OFFX << 16)) & M); m.poke(e + 0x1DC, (100 << 16) + m.peeks(e + 0x1E8))
        m.poke(e + 0xE4, f & M); m.poke(e + 0xE8, 0); m.poke(e + 0x13C, 0); m.poke(e + 0x130, 0)
        m.poke(e + 0x8C, 24 if east else 8)
        m.call(0x80037730, e, limit=2_000_000)
        got = (m.peeks(e + 0x114), m.peeks(e + 0x13C), m.peek(e + 0x130))
        want_x, want_adj = model(x0 if x0 < (1 << 31) else x0 - (1 << 32), f, wall)
        stats['total'] += 1
        stats['flag'] += want_adj
        if got[0] != want_x or got[1] != want_adj or got[2] != 0:
            bad.append((i, wall, east, x0, f, got, (want_x, want_adj)))
    return stats, bad


if __name__ == '__main__':
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
    stats, bad = run(n)
    lines = ['k0_xyfuzz: %d cases, ForceAdjusted expected in %d ; mismatches: %d' % (stats['total'], stats['flag'], len(bad))]
    for b in bad[:10]:
        lines.append('  MISMATCH %s' % (b,))
    txt = '\n'.join(lines)
    open(os.path.join(OUT, 'xyfuzz-summary.txt'), 'w').write(txt + '\n')
    print(txt)
