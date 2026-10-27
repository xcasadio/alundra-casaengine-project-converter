"""READ-ONLY probe on a COPY of the h3 oracle (./oracle_h3bin). Question: can the hero's depth key (sort driver 0x800397AC, overlap loop 0x80039860-0x80039978, list 0x80127C28) be raised by a
collidable neighbour so that the NON-collidable carried bomb ends up BEHIND the hero? Scenario: the bomb carried (Circle at tick 4, as bomb_keys2.py), a collidable global sprite spawned by the REAL
SpawnWarpEntity next to the hero (harness write), keys read after the REAL sort driver 0x800399B8 run after each tick."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
O = os.path.join(HERE, 'oracle_h3bin')
sys.path.insert(0, O)
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
import h3_game as G
import world as W

def run_case(label, sprite, dx, dy, hero=(200, 200), nticks=70, verbose=True):
    m, STATS = G.make(0x23, hero_px=hero)
    p = G.spawn_global(m, sprite, hero[0] + dx, hero[1] + dy)
    if not p:
        print(label, "spawn failed"); return None
    def holds(t):
        h = 0
        if t <= 3: h |= G.RIGHT
        if t == 4: h |= G.CIRCLE
        return h
    r = G.Run(m, holds)
    rows = []
    def post(m, t):
        W.run_pass(m, 0x800399B8)
        bomb = None
        for i in range(1, 40):
            q = W.ent(i)
            if m.peeks(q + 0x10) in (2, 3) and m.peeks(q + 0x68) == 2 and m.peek(q + 0x28) != 0:
                bomb = q; break
        if bomb is None: return
        def f(e):
            return dict(fl=m.peek(e + 0x6C), idsv=m.peeks(e + 0x1A8), y=m.peeks(e + 0x118)/65536, z=m.peeks(e + 0x11C)/65536, key=m.peek(e + 0x1BC) & 0xFFFFFFFF, car=m.peek(e + 0x28))
        rows.append((t, f(G.HERO), f(bomb), f(p)))
    r.step(nticks, post=post)
    n_behind = sum(1 for t, h, b, c in rows if b['key'] < h['key'])
    n_front = sum(1 for t, h, b, c in rows if b['key'] > h['key'])
    print("== %s: sprite %d at hero%+d,%+d  neighbour flags %08X (collidable=%s) ; ticks %d ; bomb front %d behind %d" % (
        label, sprite, dx, dy, rows[0][3]['fl'] if rows else 0, bool(rows and rows[0][3]['fl'] & 0x80), len(rows), n_front, n_behind))
    if verbose and rows:
        for t, h, b, c in rows[::10] + rows[-1:]:
            print("   t=%3d hero key %08X (idsv %d y %.2f)  bomb key %08X (z %.1f)  neighbour key %08X (idsv %d y %.2f z %.1f)" % (
                t, h['key'], h['idsv'], h['y'], b['key'], b['z'], c['key'], c['idsv'], c['y'], c['z']))
    return rows

if __name__ == '__main__':
    for spr in (36, 25, 34, 35, 37):
        for (dx, dy) in ((0, 14), (0, 20), (0, -14)):
            try:
                run_case("case", spr, dx, dy, verbose=(spr == 36))
            except Exception as e:
                print("sprite", spr, dx, dy, "error", repr(e)[:200])
