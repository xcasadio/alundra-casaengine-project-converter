"""sc_grass_placed_pf.py - E20.h3a T0 (c) (w4): the grass cut by a bomb BORN POSED, at the bench of the test.  A copy of the scenario `grass` of docs/plan-e20-annexe/e20-h3/bin/run/sc_misc.py:
the same 25 bush cells (columns 8 to 12, rows 10 to 14, walkability bit 0x02), but the bomb is born by the real SpawnWarpEntity (no hands, no C62 of the hero, as in the DLL test) at the
centre of the grid, and put to status 3 by the harness after tick K (the key's tick 0); the hero stays at (100, 100), 152 px or more from the bomb.
The generator (0x80098708) is set to the seed of the script (0x12345678) at the end of tick K, so the tick K + 1 starts from it, and the PER-FRAME STEP of the binary (s <- s * 0x7D2B89DD +
0xE06A02E7, the end of the frame function 0x8002BD0C-0x8002BD34, section 1.20 of docs/plan-e20-heros.md, block, point 3) is added AFTER the record of rng1 of each tick and BEFORE the next tick
(never in an after-tick hook that runs before rng1 is read): rng1(t) = the state after the draws of the tick t and before its step, rng0(t + 1) = step(rng1(t)).
Columns of a line: tick relative to K, rng0, rng1, the number of draws (steps from rng0 to rng1), the bomb (status, target animation, frame box), the cells cut in the tick, the effects
(id, x, y, z in pixels) and the sounds.   usage (from a copy of bin/run with runpy.py): python -B runpy.py sc_grass_placed_pf.py [BX BY K N [nograss]]"""
import sys
import h3_game as G
from h3_game import *

BX = int(sys.argv[1]) if len(sys.argv) > 1 else 252
BY = int(sys.argv[2]) if len(sys.argv) > 2 else 200
K = int(sys.argv[3]) if len(sys.argv) > 3 else 5
N = int(sys.argv[4]) if len(sys.argv) > 4 else 60
NOGRASS = len(sys.argv) > 5 and sys.argv[5] == 'nograss'      # the generator probe (T0 (f)): the same scene without a single bush cell
SEED = 0x12345678
RNG = 0x80098708
M32 = 0xFFFFFFFF


def step(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32


def draws(a, b):
    s = a
    for n in range(0, 40):
        if s == b:
            return n
        s = step(s)
    return -1


def cells(col, row):
    if not NOGRASS and 8 <= col <= 12 and 10 <= row <= 14:
        return (0, 0, 2, 0)                                   # a bush: walkability bit 0x02 (CheckAndTriggerTileEffect)
    return (0, 0, 0, 0)


m, STATS = G.make(None, hero_px=(100, 100), cells=cells, indices=(1, 2))
H = G.HERO
b = G.spawn_global(m, 2, BX, BY)                              # harness write: the bomb is born posed (real SpawnWarpEntity)
assert b
m.poke(b + 0x274, 240)                                        # the fuse kept high: the native C62 does not end it before the poke of the status 3


def cellword(c, r):
    return m.peek(W.TILES + 0x604 + r * W.ROW + c * 8) & 0xFFFF


GRID = [(c, r) for c in range(8, 13) for r in range(10, 15)]
state = {c: cellword(*c) for c in GRID}
print('bomb born at (%d, %d), z 0, slot %#x ; hero at (%.2f, %.2f) ; bush cells: %s ; K = %d (status 3 poked at the end of tick K) ; seed %#x at the end of tick K' % (
    BX, BY, b, m.peeks(H + 0x114) / 65536, m.peeks(H + 0x118) / 65536, 'none (nograss)' if NOGRASS else 'columns 8..12, rows 10..14 (25)', K, SEED))
pad = W.Pad()
cut_all = []
t_free = None
prev_rng1 = None
for t in range(1, K + N + 1):
    m.tick_no = t
    j0 = len(m.journal)
    rng0 = m.peek(RNG)
    W.tick(m, pad, 0, check_pad=False)
    rng1 = m.peek(RNG)
    js = [j for j in m.journal[j0:] if not j[1].startswith('pass_')]
    if t == K:
        m.poke(b + 0x10, 3)                                   # the harness: status 3 (Deactivated) at the end of tick K
        m.poke(RNG, SEED)                                     # the harness: the generator at the seed of the script, the tick K + 1 starts from it
        continue
    if t < K:
        continue
    now = {c: cellword(*c) for c in GRID}
    cut = sorted(c for c in GRID if now[c] != state[c])
    state = now
    cut_all += [(t - K, c) for c in cut]
    fx = [(x[2][1], round(x[2][3] / 65536 if x[2][3] < 2 ** 31 else (x[2][3] - 2 ** 32) / 65536, 3), round(x[2][4] / 65536 if x[2][4] < 2 ** 31 else (x[2][4] - 2 ** 32) / 65536, 3), round(x[2][5] / 65536 if x[2][5] < 2 ** 31 else (x[2][5] - 2 ** 32) / 65536, 3)) for x in js if x[1] == 'effect_3BDD8']
    snd = [x[2] for x in js if x[1] == 'sound' and x[2] != 0]
    bs = (m.peeks(b + 0x10), m.peeks(b + 0x88), 1 if m.peek(b + 0x1D4) else 0)
    line = 'k%+4d rng0=%08X rng1=%08X draws=%d bomb(st,tanim,box)=%s hero(hp)=%d' % (t - K, rng0, rng1, draws(rng0, rng1), bs, m.peeks(H + 0x14))
    if cut: line += ' cut=%s' % cut
    if fx: line += ' fx=%s' % fx
    if snd: line += ' snd=%s' % snd
    print(line)
    m.poke(RNG, step(rng1))                                   # the per-frame step, after rng1 and before the next rng0
    if bs[0] == 0:
        t_free = t - K
        break
print('cells cut (k, (col,row)):', cut_all)
print('cells cut, sorted (col,row):', sorted(c for _, c in cut_all), 'count', len(cut_all))
print('bomb slot free at k%+d' % t_free if t_free is not None else 'bomb slot never free')
