"""sc_misc.py - the remaining bomb / beans scenarios of the h3 reading, each printing a COMPACT list of changes.
usage: python -B sc_misc.py NAME     NAME in: fuse chain grass wall limit3 locked water beans beans_pot beans_hurt bomb_hurt_clone
Harness writes (documented per scenario): pad words, the cell field, extra entities placed with the REAL SpawnWarpEntity (second bomb, targets, a fake pot), the control word 0x800DC4B8 (locked)."""
import sys
import h3_game as G
from h3_game import *

HEROP = G.HERO


def sl(m, p):
    return W.ent(p)


def watch(m, r, n, slots_types=(1, 2), keys=('st', 'ta', 'c28', 'gr', 'adj', 't24'), hero_keys=('ta', 'c2c', 'hp', 'inuse'), start=1, quiet=False, extra=None):
    """step n ticks, print a line when a watched key of the hero or of an entity of the given sprite types changes"""
    last = {}
    out = []
    for _ in range(n):
        r.step(1)
        t = r.t
        parts = []
        hero = dict(ta=m.peeks(HEROP + 0x88), c2c=m.peek(HEROP + 0x2C), hp=m.peeks(HEROP + 0x14), inuse=m.peeks(0x80127164))
        hk = tuple(hero[k] for k in hero_keys)
        if last.get('h') != hk:
            parts.append('H ' + ' '.join('%s=%s' % (k, ('%#x' % hero[k]) if k in ('ta', 'c2c', 'inuse') else hero[k]) for k in hero_keys) + ' x=%.1f y=%.1f' % (m.peeks(HEROP + 0x114) / 65536, m.peeks(HEROP + 0x118) / 65536))
            last['h'] = hk
        for i in range(1, 40):
            p = W.ent(i)
            st = m.peeks(p + 0x10)
            ty = m.peeks(p + 0x68)
            if st and ty in slots_types:
                s = G.e_snap(m, p)
                sk = tuple(s[k] for k in keys)
                if last.get(i) != sk:
                    parts.append('E%d(ty%d) ' % (i, ty) + ' '.join('%s=%s' % (k, ('%#x' % s[k]) if k in ('c28',) else s[k]) for k in keys) + ' pos=(%.1f,%.1f,%.1f)' % (px(s['x']), px(s['y']), px(s['z'])))
                    last[i] = sk
            elif last.get(i) is not None and not st:
                parts.append('E%d gone' % i)
                last[i] = None
        if extra:
            e = extra(m, t)
            if e: parts.append(e)
        js = [j for j in r.js if j[0] == t and j[1] in ('sound',) and j[2] != 0]
        if js:
            parts.append('snd ' + ','.join(str(j[2]) for j in js))
        fxs = [j for j in r.js if j[0] == t and j[1] in ('effect_3BDD8', 'effect_3BE74')]
        for j in fxs:
            parts.append('fx %s' % (list(j[2][:6]) if isinstance(j[2], (list, tuple)) else j[2:]))
        if parts and not quiet:
            out.append('t%03d ' % t + ' | '.join(parts))
    return out


def basic_holds(circle_at=4, square_at=None, turn_right=True, extra=None):
    def holds(t):
        h = 0
        if turn_right and t <= 3: h |= G.RIGHT
        if t == circle_at: h |= G.CIRCLE
        if square_at and t == square_at: h |= G.SQUARE
        if extra: h |= extra(t)
        return h
    return holds


def sc_fuse():
    m, STATS = G.make(0x23, hero_px=(200, 200))
    r = Run(m, basic_holds())
    for l in watch(m, r, 330): print(l)


def sc_chain():
    m, STATS = G.make(0x23, hero_px=(200, 200))
    r = Run(m, basic_holds(square_at=50))
    r.step(90)                                              # bomb 1 landed
    p = G.spawn_global(m, 2, 252, 200)                      # harness write: a second bomb 11 px east of bomb 1 (241.3), fuse set to the full 240
    m.poke(p + 0x274, 240)
    for l in watch(m, r, 260, keys=('st', 'ta', 'c28', 'gr', 'hitc')): print(l)


def sc_grass():
    def cells(col, row):
        if 8 <= col <= 12 and 10 <= row <= 14:
            return (0, 0, 2, 0)                              # a bush: walkability bit 0x02 (CheckAndTriggerTileEffect)
        return (0, 0, 0, 0)
    m, STATS = G.make(0x23, hero_px=(200, 200), cells=cells)
    r = Run(m, basic_holds(square_at=50))
    def cellword(c, rr):
        return m.peek(W.TILES + 0x604 + rr * W.ROW + c * 8) & 0xFFFF
    before = {(c, rr): cellword(c, rr) for c in range(8, 13) for rr in range(10, 15)}
    for l in watch(m, r, 330, extra=None): print(l)
    cut = sorted(k for k, v in before.items() if cellword(*k) != v)
    print('cells cut (col,row):', cut)
    fx = [j for j in r.js if j[1] == 'effect_3BDD8']
    print('effects created:', [(j[0], j[2][1]) for j in fx if j[2][1] != 9][:20])
    print('sounds 0x1F:', [j[0] for j in r.js if j[1] == 'sound' and j[2] == 0x1F])


def sc_wall():
    def cells(col, row):
        if col == 9 and 9 <= row <= 15:                       # a wall column at x = 216..239 (hero at 200, bomb thrown east lands at 241 without it)
            return (0, 0, 1, 0x10)
        return (0, 0, 0, 0)
    m, STATS = G.make(0x23, hero_px=(200, 200), cells=cells)
    r = Run(m, basic_holds(square_at=50))
    for l in watch(m, r, 120, keys=('st', 'ta', 'c28', 'gr', 'adj', 'dr', 'b278')): print(l)


def sc_limit3():
    m, STATS = G.make(0x23, hero_px=(200, 200))
    state = {'n': 0}
    def holds(t):
        h = 0
        if t <= 3: h |= G.RIGHT
        hero_ta = m.peeks(HEROP + 0x88)
        inuse = m.peeks(0x80127164)
        if hero_ta == 0xC and m.peek(HEROP + 0x2C):
            h |= G.SQUARE if t % 2 == 0 else 0
        elif hero_ta in (0, 1) and inuse == 0 and t > 3:
            h |= G.CIRCLE
        return h
    r = Run(m, holds)
    starts = []
    def extra(m, t):
        s = m.peeks(0x80127164)
        if s != state.get('s'):
            state['s'] = s
            return 'inuse=%#x' % s
    n_bombs = lambda: sum(1 for i in range(0, 64) if m.peeks(W.ent(i) + 0x68) == 2)
    for l in watch(m, r, 420, keys=('st', 'ta', 'c28'), extra=extra): print(l)
    print('sound 3 at', [j[0] for j in r.js if j[1] == 'sound' and j[2] == 3])


def sc_locked():
    m, STATS = G.make(0x23, hero_px=(200, 200))
    r = Run(m, basic_holds())
    r.step(60)                                              # carrying (0xC)
    m.poke(0x800DC4B8, 0x20)                                # dialogue lock bit (0x34 mask)
    for l in watch(m, r, 40, keys=('st', 'ta', 'c28', 't24')): print(l)
    print('journal (fx, destroy):', [j for j in r.js if j[1] not in ('sound',)][:6])


def sc_water():
    def cells(col, row):
        if col >= 10:
            return (0, 0, 0, 0x08)                           # deep water: ground property 8 -> slope 4 (+0x18C = (cell & 0xE00) >> 9, swim notes of E20.i)
        return (0, 0, 0, 0)
    m, STATS = G.make(0x23, hero_px=(200, 200), cells=cells, extra_items=(0x1B,))   # boots (item 0x1B) so that the deep-water wall does not stop the hero
    r = Run(m, basic_holds(extra=lambda t: G.RIGHT if t > 60 else 0))
    for l in watch(m, r, 120, keys=('st', 'ta', 'c28', 't24')): print(l)
    print('fx:', [(j[0], j[2][1], j[2][3] >> 16) for j in r.js if j[1] == 'effect_3BDD8'][:5])




def sc_carry_actions():
    """what the hero does with the bomb in hand: Circle (second use), Cross (jump), Triangle, walking, then Square"""
    m, STATS = G.make(0x23, hero_px=(200, 200))
    def holds(t):
        h = 0
        if t <= 3: h |= G.RIGHT
        if t == 4: h |= G.CIRCLE
        if t == 50: h |= G.CIRCLE            # second use while carrying
        if t == 60: h |= G.TRIANGLE
        if t == 70: h |= G.CROSS             # jump
        if t == 120: h |= G.SQUARE           # throw (hero landed by then)
        return h
    r = Run(m, holds)
    for l in watch(m, r, 140, keys=('st', 'ta', 'c28', 't24'), extra=lambda m, t: ('hz=%.1f' % (m.peeks(HEROP + 0x11C) / 65536)) if t in (72, 80, 90) else None): print(l)
    print('sounds:', [(j[0], j[2]) for j in r.js if j[1] == 'sound'])


def sc_air_start():
    """the bomb started in mid-jump (Cross at t4, Circle at t15)"""
    m, STATS = G.make(0x23, hero_px=(200, 200))
    def holds(t):
        h = 0
        if t == 4: h |= G.CROSS
        if t == 15: h |= G.CIRCLE
        return h
    r = Run(m, holds)
    for l in watch(m, r, 90, keys=('st', 'ta', 'c28', 't24'), extra=lambda m, t: ('hz=%.1f' % (m.peeks(HEROP + 0x11C) / 65536)) if t in (14, 15, 16, 20, 30, 40, 50) else None): print(l)


def sc_death():
    """HP 0 while carrying the bomb (hero entity +0x14 = 0 at t60), and the exit-cell bit (+0x188 & 0x80 is set by the tile probe; here only the death is exercised)"""
    m, STATS = G.make(0x23, hero_px=(200, 200))
    r = Run(m, basic_holds())
    r.step(60)
    m.poke(HEROP + 0x14, 0)
    for l in watch(m, r, 60, keys=('st', 'ta', 'c28', 't24', 'gr')): print(l)
    print('journal non-sound:', [(j[0], j[1], j[2][:7] if isinstance(j[2], (list, tuple)) else j[2:]) for j in r.js if j[1] != 'sound'][:6])


def sc_wall2():
    """wall column further away: the bomb lands against its foot (cols 10 and 11 tested) - does it explode at once on the ground (branch 0x8007AB94)?"""
    for col0 in (10, 11):
        def cells(col, row, col0=col0):
            return (0, 0, 1, 0x10) if (col == col0 and 9 <= row <= 15) else (0, 0, 0, 0)
        m, STATS = G.make(0x23, hero_px=(200, 200), cells=cells)
        r = Run(m, basic_holds(square_at=50))
        print('--- wall column', col0, '(x from %d)' % (col0 * 24))
        for l in watch(m, r, 110, keys=('st', 'ta', 'gr', 'adj', 'dr', 'b278')):
            if l.split()[0] >= 't048': print(l[:210])


def sc_thrown_in_water():
    """bomb (and then beans) thrown into deep water cells (ground property 8 from col 10, x >= 240): DestroyOnSlidingSlope (flags bit 20) of the entity pass 0x800386D0"""
    for item in (0x23, 0x1F):
        def cells(col, row):
            return (0, 0, 0, 0x08) if col >= 8 else (0, 0, 0, 0)
        m, STATS = G.make(item, hero_px=(178, 200), cells=cells)
        r = Run(m, basic_holds(square_at=50))
        print('--- item %#x thrown into deep water' % item)
        for l in watch(m, r, 110, slots_types=(1, 2), keys=('st', 'ta', 'c28', 'gr', 'fl')):
            if l.split()[0] >= 't049': print(l[:230])


def sc_air_throw():
    """carry the bomb, jump (Cross at t70), throw in mid-air (Square at t80, hero z about 27 px): the state 0x0A and the flight"""
    m, STATS = G.make(0x23, hero_px=(200, 200))
    def holds(t):
        h = 0
        if t <= 3: h |= G.RIGHT
        if t == 4: h |= G.CIRCLE
        if t == 70: h |= G.CROSS
        if t == 80: h |= G.SQUARE
        return h
    r = Run(m, holds)
    for l in watch(m, r, 120, keys=('st', 'ta', 'c28', 't24', 'gr'), extra=lambda m, t: ('hz=%.1f' % (m.peeks(HEROP + 0x11C) / 65536)) if t in (79, 80, 81) else None):
        if l.split()[0] >= 't068': print(l[:230])


def sc_relift():
    """can the hero pick the landed bomb up again?  After the landing (bomb at rest near x=241) the hero walks east into it and presses Square (PlayerTryAction 0x8002EEAC, real)."""
    for item in (0x23, 0x1F):
        m, STATS = G.make(item, hero_px=(200, 200))
        def holds(t):
            h = 0
            if t <= 3: h |= G.RIGHT
            if t == 4: h |= G.CIRCLE
            if t == 50: h |= G.SQUARE
            if 80 <= t < 200: h |= G.RIGHT
            if t in (100, 110, 120, 130, 140, 150, 160): h |= G.SQUARE
            return h
        r = Run(m, holds)
        print('--- item %#x' % item)
        out = watch(m, r, 200, slots_types=(1, 2), keys=('st', 'ta', 'c28', 't24'), extra=lambda m, t: ('contact130=%#x' % m.peek(HEROP + 0x130)) if (t in (95, 100, 110) and m.peek(HEROP + 0x130)) else None)
        for l in out:
            if l.split()[0] >= 't073': print(l[:220])
        print('hero x at the end %.1f ; carried=%#x ; flags of the object %#x' % (m.peeks(HEROP + 0x114) / 65536, m.peek(HEROP + 0x2C), m.peek(W.ent(1) + 0x6C)))


SC = {n[3:]: f for n, f in list(globals().items()) if n.startswith('sc_')}
if __name__ == '__main__':
    SC[sys.argv[1]]()
