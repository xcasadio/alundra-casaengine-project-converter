"""critic: how often does the binary's halving refinement (0x80037730 loop, see k0_xyfuzz model) leave a 1-raw residual gap, i.e. flag the wall hit 2 ticks after arrival instead of 1?
Model of ONE tick against a blocking limit at distance g (raw) with a full step f0 (raw), no entity, cardinal east mover:
  first attempt full step; if it fits -> done (moved fully).
  else: loop: f = f>>1 ; try; if accepted: position += f, moved_ok, then f >>= 1 (the SAME f is not retried: after an accept, the code halves again); if rejected: halve again; until f == 0.
Careful: re-derived from the disassembly (0x80037938 reject: restore, f>>=1, continue while f!=0 ; 0x80037D68 accept after the first attempt: f>>=1, continue).
python -B -I residual_stats.py"""
import random

def tick(g, f0):
    """g = distance to limit (raw, >=0: the max total move that fits); returns (moved, flagged)"""
    pos = 0
    f = f0
    first = True
    moved_ok = False
    while True:
        if pos + f <= g:         # accepted
            pos += f
            if first:
                return pos, False  # the first (full) attempt accepted: stop
            moved_ok = True
        # rejected or accepted-after-first: halve
        first = False
        f >>= 1
        if f == 0:
            break
    return pos, (not moved_ok)

def sim(g0, f0, maxticks=200):
    """arrow reaches wall: g0 = initial distance; every tick the same f0. returns (tick of first flag, ticks until pos==g0)"""
    pos = 0
    arrived = None
    for t in range(1, maxticks):
        m, fl = tick(g0 - pos, f0)
        pos += m
        if pos == g0 and arrived is None:
            arrived = t
        if fl:
            return t, arrived
    return None, arrived

random.seed(20261016)
f0 = 6 * 65536
N = 20000
c = {}
for _ in range(N):
    g0 = random.randrange(0, 80 * 65536)
    t, a = sim(g0, f0)
    d = None if t is None or a is None else t - a
    c[d] = c.get(d, 0) + 1
print("6 px/tick, N", N, "flag delay after the tick the arrow reaches the limit exactly (None: the arrow never reached the limit exactly):", sorted(c.items(), key=lambda kv: str(kv[0])))
c = {}
for x0 in range(0, 12):
    t, a = sim(x0 * 65536, f0)
    d = None if t is None or a is None else t - a
    c[d] = c.get(d, 0) + 1
print("integer-pixel gaps 0..11 px:", sorted(c.items(), key=lambda kv: str(kv[0])))

def sim2(g0, f0, maxticks=200):
    pos = 0
    tcross = None
    for t in range(1, maxticks):
        if tcross is None and pos + f0 > g0:
            tcross = t
        m, fl = tick(g0 - pos, f0)
        pos += m
        if fl:
            return t - tcross
    return None

c = {}
for _ in range(N):
    g0 = random.randrange(0, 80 * 65536)
    d = sim2(g0, f0)
    c[d] = c.get(d, 0) + 1
print("flag tick minus the first tick whose FULL step no longer fits (random raw gaps):", sorted(c.items(), key=lambda kv: str(kv[0])))
c = {}
for m in range(0, 12):
    d = sim2(m * 65536, f0)
    c[d] = c.get(d, 0) + 1
print("same, integer px gaps 0..11 (the oracle C1 family):", sorted(c.items(), key=lambda kv: str(kv[0])))
# the same with the first, 3 px tick:  irrelevant
