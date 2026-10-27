"""READ-ONLY oracle run on a COPY of docs/plan-e20-annexe/e20-h3/bin/run (./oracle_h3bin, never edited): REAL code of ALUN_CD.EXE (France) in the MIPS interpreter.
Scenario: hero at (200, 200) facing east, Circle at tick 4 (the bomb is born in the hands), then the hero walks with the bomb (UP, DOWN, LEFT, RIGHT) and jumps with the Cross while walking in each direction.
After each tick the binary's sort driver 0x800399B8 is run (the tail of 0x8003B388 does it; the keys are read by nothing else but the draw and the effects) and the keys +0x1BC of the hero and of the bomb are printed
with their parts. Larger key = drawn later = in front (docs/plan-e19-g3c-annexe/discovery/notes.md 3.1)."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
O = os.path.join(HERE, 'oracle_h3bin')
sys.path.insert(0, O)
sys.path.insert(0, r'C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\t2-feedback\carried-draw\oracle_h3bin')
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
import h3_game as G  # noqa: E402
import world as W  # noqa: E402

HEROP = G.HERO


def slot(key):
    return 16 * min((key & 0xFFFFFFFF) >> 20, 59) + 6


def holds(t):
    h = 0
    if t <= 3:
        h |= G.RIGHT
    if t == 4:
        h |= G.CIRCLE
    # walk with the bomb: up, down, left, right, 25 ticks each, after the lift (the lift gesture lasts ~35 ticks)
    if 50 <= t < 75:
        h |= G.UP
    if 75 <= t < 100:
        h |= G.DOWN
    if 100 <= t < 125:
        h |= G.LEFT
    if 125 <= t < 150:
        h |= G.RIGHT
    # jumps (Cross is an edge) while walking in each direction, 30 ticks apart
    for start, d in ((160, G.UP), (200, G.DOWN), (240, G.LEFT), (280, G.RIGHT)):
        if start <= t < start + 16:
            h |= d
        if t == start + 2:
            h |= G.CROSS
    return h


m, STATS = G.make(0x23, hero_px=(200, 200))
r = G.Run(m, holds)
rows = []


def post(m, t):
    W.run_pass(m, 0x800399B8)
    bomb = None
    for i in range(1, 40):
        p = W.ent(i)
        if m.peeks(p + 0x10) in (2, 3) and m.peeks(p + 0x68) == 2:
            bomb = p
            break
    if bomb is None:
        return

    def f(e):
        return dict(st=m.peeks(e + 0x10), fl=m.peek(e + 0x6C), afl=m.peek(e + 0xB4) & 0xFF, car=m.peek(e + 0x28), idsv=m.peeks(e + 0x1A8), x=m.peeks(e + 0x114) / 65536,
                    y=m.peeks(e + 0x118) / 65536, z=m.peeks(e + 0x11C) / 65536, ta=m.peeks(e + 0x88), ca=m.peeks(e + 0x90), dr=m.peeks(e + 0x8C), key=m.peek(e + 0x1BC) & 0xFFFFFFFF)
    rows.append((t, f(HEROP), f(bomb)))


r.step(300, post=post)

print("tick | hero: anim dir idsv  y(px)   z(px)  key       slot | bomb: idsv y(px)   z(px)  key       slot | bomb-hero | bomb is")
prev = None
cnt = {'front': 0, 'behind': 0, 'equal': 0}
changes = []
for t, h, b in rows:
    d = b['key'] - h['key']
    v = 'IN FRONT' if d > 0 else ('BEHIND' if d < 0 else 'equal')
    cnt[{'IN FRONT': 'front', 'BEHIND': 'behind', 'equal': 'equal'}[v]] += 1
    state = (h['ca'], h['dr'], h['idsv'], v)
    if state != prev or t % 20 == 0:
        print("%4d | h: %3d %3d %3d  %7.2f %6.2f %08X %3d | b: %3d %7.2f %6.2f %08X %3d | %+8d | %s" % (
            t, h['ca'], h['dr'], h['idsv'], h['y'], h['z'], h['key'], slot(h['key']), b['idsv'], b['y'], b['z'], b['key'], slot(b['key']), d, v))
    prev = state
print()
print("ticks with the bomb in the lists:", len(rows), cnt)

# ticks where the bomb key is below the hero's, split by whether the bomb is still carried (its +0x28 is the hero)
carried_behind = [t for t, h, b in rows if b['car'] != 0 and b['key'] < h['key']]
carried_ticks = [t for t, h, b in rows if b['car'] != 0]
free_behind = [t for t, h, b in rows if b['car'] == 0 and b['key'] < h['key']]
print("carried ticks (bomb +0x28 != 0):", len(carried_ticks), " carried AND behind:", carried_behind)
print("not carried (thrown, flying, exploding) AND behind:", free_behind)
