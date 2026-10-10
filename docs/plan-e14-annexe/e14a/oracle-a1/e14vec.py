"""e14vec.py - E14.a1 oracle: single-call vectors.  The handler is called DIRECTLY (emu.py Emu.call, the harness's registers: a0 = the entity) on a state the harness writes, the RNG word is an
INPUT (random, or crafted so that the successive draws give chosen results), and the entity and hero images, the RNG word and the journalled calls are read back: "one call at a time, RNG word as
input and output" (the comparison rule of docs/plan-e14-ia-native.md section 0.3).

The vectors hold INPUTS only (no expected value).  Input fields written (everything else keeps the value InitializeEntity / the spawner left):
    entity: TargetAnimationId +0x88, TargetDirection +0x8C, AnimationDirection +0x98, +0xAC (animation-end flag), forces (the six words +0xBC..+0xD8 get non-zero junk so that a "zero the forces" is visible),
            ForceAdjusted +0x13C, IsOnGround +0x144, Flags +0x6C, HP +0x14, Bytes[0..3] +0x274.., AIValues[0] +0x280, AIValues[1] +0x282, FloorHeight +0x134, MapHeights +0x158.., tile +0x120 +0x124,
            position +0x114 +0x118 +0x11C
    hero:   tile, position, FloorHeight, frame box pointer +0x1D4, current attack pointer +0x1C8 (-> a 2-byte (attribute, power) pair)
    D handlers: TouchingEntity +0x224 := the hero, the hero's attack pair, the latch 0x80146DF8
    RNG:    the word 0x80098708
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'tools'))
import e14world as E
import e14run as R
import c1world as C
import hworld as H
import world as W
from machine import M32, s32

ENT0, STRIDE = E.ENT0, E.STRIDE
EADDR = E.ENT0 + E.STRIDE        # slot 1 = the enemy
HADDR = E.ENT0                   # slot 0 = the hero
ATTACKS = 0x801EA800             # (attribute, power) pairs written by the vectors (free RAM, as hworld.SCRATCH2)
BOXP = 0x801EA900                # a non-null "frame box" pointer target
ENTX, ENTY = 252, 96             # px of the enemy (record (20, 11, 0))

HANDLER_ADDR = {v: k for k, v in E.LIVE.items()}


class Gen:
    """deterministic pseudo-random source (the binary's own LCG, fixed seed), for the input choices"""
    def __init__(self, seed):
        self.s = seed & M32

    def u32(self):
        self.s = E.lcg(self.s)
        return self.s

    def below(self, n):
        return (self.u32() * n) >> 32

    def pick(self, seq):
        return seq[self.below(len(seq))]


_chain = {}


def crafted(draws):
    key = tuple(draws)
    if key not in _chain:
        _chain[key] = E.seed_chain(list(draws))
    return _chain[key]


def edge_start(n, r, hi):
    """the RNG word whose NEXT draw gives result r of rng(n) at the lowest (hi = 0) or highest (hi = 1) word of that result"""
    lo = -(-r * (1 << 32) // n)
    top = -(-(r + 1) * (1 << 32) // n) - 1
    return E.lcg_inv(top if hi else lo)


def base(kind, inv='kit'):
    spec = dict(name='vec', kind=kind, recs=[dict(x=20, y=11, h=0)], hero_px=(452, 96), n=1, map=None, inv=inv)
    m, meta = E.setup(spec)
    m.poke(EADDR + 0x10, 2)                       # status 2: as if program A had run
    sb = C.Sandbox(m)
    return m, sb


def write_junk_forces(m, e, g):
    for o in range(0xB8, 0xE0, 4):                  # ForceZ .. the step words: junk everywhere, so the image comparison shows exactly which words a handler writes
        m.poke(e + o, (g.u32() % 20001) - 10000)


def apply_vec(m, v, g):
    """write the inputs of vector v (a dict) into the machine"""
    e, h = EADDR, HADDR
    m.poke(e + 0x88, v.get('anim', 0))
    m.poke(e + 0x8C, v.get('dir', 0))
    m.poke(e + 0x98, v.get('animdir', 0))
    m.poke(e + 0xAC, v.get('endflag', 0))
    write_junk_forces(m, e, g)
    m.poke(e + 0x13C, v.get('fadj', 0))
    m.poke(e + 0x144, v.get('onground', 1))
    if 'flags' in v:
        m.poke(e + 0x6C, v['flags'])
    m.poke(e + 0x14, v.get('hp', 9))
    m.poke8(e + 0x274, v.get('b0', 0))
    m.poke8(e + 0x275, v.get('b1', 0))
    m.poke8(e + 0x277, v.get('b3', 0))
    m.poke16(e + 0x280, v.get('ai0', 0))
    m.poke16(e + 0x282, v.get('ai1', 0))
    # geometry: the enemy at (ENTX, ENTY) px, tile (10, 6); the hero (a, b) tiles away: signed difference ENTITY - HERO = (a, b), with sub-tile px offsets (ox, oy)
    a, b = v.get('tile', (9, 9))
    ox, oy = v.get('off', (0, 0))
    zd = v.get('zd', 0)                           # entity floor - hero floor
    ex, ey, ez = ENTX, ENTY, 0
    m.poke(e + 0x114, ex << 16)
    m.poke(e + 0x118, ey << 16)
    m.poke(e + 0x11C, v.get('posz', 1))
    m.poke(e + 0x120, 10)
    m.poke(e + 0x124, 6)
    fl = 1
    m.poke(e + 0x134, fl)
    mh = v.get('mh')                               # corner heights: list of 4 D values (floor - corner) or None = all equal to the floor
    for k, off in enumerate((0x158, 0x15C, 0x160, 0x164)):
        m.poke(e + off, (fl - mh[k]) if mh else fl)
    hx = ex - (a * 24) + ox
    hy = ey - (b * 16) + oy
    m.poke(h + 0x114, hx << 16)
    m.poke(h + 0x118, hy << 16)
    m.poke(h + 0x120, 10 - a)
    m.poke(h + 0x124, 6 - b)
    m.poke(h + 0x134, fl - zd)
    atk = v.get('hero_atk')                        # None = no current attack, else (attribute, power)
    if atk is None:
        m.poke(h + 0x1C8, 0)
    else:
        m.poke_bytes(ATTACKS, bytes(atk))
        m.poke(h + 0x1C8, ATTACKS)
    m.poke(h + 0x1D4, BOXP if v.get('hero_box') else 0)
    if 'touching' in v:
        m.poke(e + 0x224, v['touching'])
    if 'latch' in v:
        m.poke(C.LATCH, v['latch'])
    if 'contents' in v:
        m.poke(e + 0x3C, v['contents'])
    if 'active_effect' in v:
        m.poke(e + 0x1B8, v['active_effect'])
    if 'platform' in v:
        m.poke(e + 0x28, v['platform'])
    m.poke(E.RNG, v['rng'])


def run_vectors(m, sb, vecs, seed=1, engine='emu'):
    g = Gen(seed)
    out = []
    for v in vecs:
        sb.restore()
        m.tick_no = 0
        apply_vec(m, v, g)
        if v.get('setup'):
            v['setup'](m)
        entry = HANDLER_ADDR[v['handler']]
        rec = m.begin_call(entry, EADDR)
        m.stack.append(rec)                         # the read / write sets are attributed to the open call
        if engine == 'emu':
            m.call(entry, EADDR)
        else:
            import interp2
            interp2.call2(m, entry, EADDR)
        m.stack.pop()
        m.end_call(rec)
        rec['vec'] = v
        out.append(rec)
    sb.restore()
    return out


# ---- the vector lists --------------------------------------------------------------------------------------------------------------------------------
ZD4 = (0, 0x100000, 0x100001, -0x100001)


def rnd_word(g):
    return g.u32()


def c1_vectors(g):
    V = []
    H_ = 'C1'
    tiles = [(a, b) for a in (0, 1, 2, 3, 4, 5, -1, -3) for b in (0, 1, 2, 3, 4, 5, -2, -4)]
    for ai1 in (0, 1, 2):
        for b1 in (0, 1):
            for t in tiles:
                for zd in (0, 0x200000, 0x200001, -0x200001):
                    if (g.below(3)) == 0 or t in ((2, 2), (3, 3), (2, 0), (0, 2), (3, 0), (0, 3), (4, 4)):
                        V.append(dict(handler=H_, anim=0, ai1=ai1, b1=b1, tile=t, zd=zd, rng=rnd_word(g), off=(g.below(24) - 12, g.below(16) - 8), dir=g.pick((0, 8, 16, 24, 4, 20))))
    for ai1 in (0, 1, 2):
        for fadj in (0, 1):
            for d in range(32):
                V.append(dict(handler=H_, anim=1, ai1=ai1, fadj=fadj, dir=d, rng=rnd_word(g), tile=(9, 9)))
    for t in tiles:
        V.append(dict(handler=H_, anim=2, tile=t, rng=rnd_word(g), zd=g.pick((0, 0x7000000))))
    for endflag in (0, 1):
        for b1 in (0, 1):
            for _ in range(60):
                V.append(dict(handler=H_, anim=3, endflag=endflag, b1=b1, rng=rnd_word(g), tile=g.pick([(0, 0), (1, 2), (5, 3), (-4, 4)]), off=(g.below(60) - 30, g.below(40) - 20)))
    for r in (18, 19, 20, 21, 22):                                            # StartWalk's first draw rng(0x65) next to the chase threshold 0x14, at the lowest and the highest word of each result
        for hi in (0, 1):
            V.append(dict(handler=H_, anim=3, endflag=1, rng=edge_start(0x65, r, hi), tile=(0, 0), off=(g.below(60) - 30, g.below(40) - 20)))
    for endflag in (0, 1):
        V.append(dict(handler=H_, anim=4, endflag=endflag, rng=rnd_word(g)))
    for endflag in (0, 1):
        for b3 in (0, 1):
            V.append(dict(handler=H_, anim=7, endflag=endflag, b3=b3, rng=rnd_word(g), flags=0x103))
    for anim in (8, 9, 10, 11, 12, 13, 14):
        for og in (0, 1):
            V.append(dict(handler=H_, anim=anim, onground=og, rng=rnd_word(g)))
    for ai1 in (0, 1, 2):
        V.append(dict(handler=H_, anim=19, ai1=ai1, rng=rnd_word(g)))
    for anim in (5, 6, 15, 16, 17, 18, 20, 21, 0x7F, 0xFF, -1):
        V.append(dict(handler=H_, anim=anim, rng=rnd_word(g), ai1=3, endflag=1, onground=1))
    return V


def c10_vectors(g):
    V = []
    H_ = 'C10'
    grid = [(a, b) for a in range(-8, 9) for b in range(-8, 9)]
    # state 0 calm: the cones (TryAttackPlayerFront with the entity's own AnimationDirection, an invalid direction too)
    for animdir in (0, 1, 2, 3, 4, -1):
        for t in grid:
            V.append(dict(handler=H_, anim=0, animdir=animdir, tile=t, zd=g.pick(ZD4), rng=rnd_word(g), ai1=g.pick((0, 0, 3)), b1=g.pick((0, 0, 2)), off=(g.below(24) - 12, g.below(16) - 8)))
    # state 0 calm outside every cone: the patrol sequence
    for b1 in range(0, 9):
        for ai1 in (0, 1):
            for animdir in (0, 3):
                if b1 == 0:
                    chains = [[(4, 0)], [(4, 2)], [(4, 3)]] + [[(4, 1), (4, k)] for k in range(4)]
                elif b1 in (1, 2, 3):
                    chains = [[(4, k)] for k in range(4)]
                else:
                    chains = [[(4, 0)]]
                for ch in chains:
                    V.append(dict(handler=H_, anim=0, animdir=animdir, b1=b1, ai1=ai1, dir=g.pick((0, 3, 8, 12, 16, 20, 24, 28, 31)), tile=(9, 9), rng=crafted(ch), forcejunk=1))
    # state 0 alerted: grid of cones with box / attack presence
    for animdir in (0, 1, 2, 3):
        for t in grid:
            hb = g.pick((0, 1, 1))
            atk = g.pick((None, (1, 1), (0, 1), (0x81, 1)))
            V.append(dict(handler=H_, anim=0, animdir=animdir, b0=1, tile=t, zd=g.pick(ZD4), rng=rnd_word(g), ai1=g.pick((0, 0, 0, 2)), hero_box=hb, hero_atk=atk,
                          off=(g.below(24) - 12, g.below(16) - 8), dir=g.pick((0, 8, 16, 24, 4))))
    # state 0 alerted, hero in front (south of an entity facing down): every arm of close / mid / far / lunge with crafted draws
    south = lambda k: (0, -k)
    for k, arms in ((1, [[(8, r)] for r in (0, 1, 2, 3, 6, 7)] + [[(8, r), (4, q)] for r in (4, 5) for q in range(4)]),
                    (2, [[(8, r)] for r in (0, 1, 2, 3, 6, 7)] + [[(8, r), (4, q)] for r in (4, 5) for q in (0, 1)]),
                    (3, [[(5, r)] for r in range(5)]), (4, [[(5, r)] for r in range(5)]),
                    (5, [[(8, r)] for r in range(8)]), (6, [[(8, r)] for r in range(8)])):
        for ch in arms:
            V.append(dict(handler=H_, anim=0, animdir=0, b0=1, tile=south(k), rng=crafted(ch), ai1=0, hero_box=0, hero_atk=None, dir=g.pick((0, 8, 16, 24, 12))))
    for k in (1, 2, 3, 4):
        for q in range(4):
            V.append(dict(handler=H_, anim=0, animdir=0, b0=1, tile=south(k), rng=crafted([(4, q)]), ai1=0, hero_box=1, hero_atk=(1, 1), dir=g.pick((0, 8, 16, 24))))
    # state 5, 9, 10
    for endflag in (0, 1):
        for b3 in (0, 1):
            V.append(dict(handler=H_, anim=5, endflag=endflag, b3=b3, rng=rnd_word(g), flags=0x30103, dir=g.pick((0, 4, 8, 17))))
    for anim in (9, 10):
        for og in (0, 1):
            V.append(dict(handler=H_, anim=anim, onground=og, rng=rnd_word(g)))
    # state 12: alert path, expiry, ForceAdjusted, no turn
    for b0 in (0, 1):
        for cone in (0, 1):
            for ai1 in (0, 1, 2, 9):
                for fadj in (0, 1):
                    t = (0, -2) if cone else (9, 9)
                    V.append(dict(handler=H_, anim=12, animdir=0, b0=b0, ai1=ai1, fadj=fadj, tile=t, rng=rnd_word(g), dir=g.pick((0, 8, 16, 24, 5))))
    # state 12: HandleAnimationDirection, every on/off pattern of the four corners x every direction (D = 0x400002 on, 0x400001 off), then the sign (D = -0x500000 'on', 0 'off')
    for pat in range(16):
        for d in range(32):
            mh = [0x400002 if (pat >> k) & 1 else 0x400001 for k in range(4)]
            V.append(dict(handler=H_, anim=12, animdir=0, b0=1, ai1=9, fadj=0, tile=(9, 9), mh=mh, dir=d, rng=rnd_word(g)))
    for pat in range(16):
        for d in (0, 4, 8, 12, 16, 20, 24, 28, 9, 23):
            mh = [-0x500000 if (pat >> k) & 1 else 0 for k in range(4)]
            V.append(dict(handler=H_, anim=12, animdir=0, b0=1, ai1=9, fadj=0, tile=(9, 9), mh=mh, dir=d, rng=rnd_word(g)))
    for pat in range(16):                                                     # mixed signs: a corner far ABOVE the floor (D < 0) beside a corner far below it (D > 0)
        for d in (0, 4, 9, 12, 16, 17, 20, 23, 24, 28, 31):
            mh = [0x500000 if (pat >> k) & 1 else -0x500000 for k in range(4)]
            V.append(dict(handler=H_, anim=12, animdir=0, b0=1, ai1=9, fadj=0, tile=(9, 9), mh=mh, dir=d, rng=rnd_word(g)))
    for pat in range(16):
        for d in (2, 10, 18, 26):
            mh = [0x7FFFFFF if (pat >> k) & 1 else 0 for k in range(4)]
            V.append(dict(handler=H_, anim=12, animdir=0, b0=1, ai1=9, fadj=0, tile=(9, 9), mh=mh, dir=d, rng=rnd_word(g)))
    # the other states: tail only
    for anim in (1, 2, 3, 4, 6, 7, 8, 11, 13, 14, 20, 255, -1):
        for _ in range(3):
            V.append(dict(handler=H_, anim=anim, animdir=g.below(4), b0=g.below(2), endflag=g.below(2), tile=g.pick(grid), rng=rnd_word(g), dir=g.below(32), ai1=g.below(4)))
    return V


def d_vectors(g, handler):
    V = []
    combos = [(a, p) for a, p in ((0x01, 1), (0x81, 1), (0x04, 2), (0x84, 2), (0x03, 2), (0x00, 5), (0x0F, 3), (0x81, 16), (0x02, 4), (0x82, 6), (0x05, 2), (0x07, 1))]
    compass = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1)]
    for attr, power in combos:
        for latch in (-1, 0, 1, 3, 16):
            for hp in (1, 2, 3, 9):
                for k in range(3):
                    cx, cy = compass[(g.below(8))]
                    mag = g.pick((8, 20, 40, 90))
                    V.append(dict(handler=handler, hp=hp, anim=g.pick((0, 3, 12)), dir=g.below(32), hero_atk=(attr, power), touching=HADDR, latch=latch, rng=rnd_word(g),
                                  tile=(0, 0), off=(cx * mag, cy * mag)))
    return V


def e1_vectors(g, kind):
    V = []
    for contents in (0, 69, 70, 71, 84, 85, 0x26, 0x51, 0x52):
        for ae in (0, 1):
            for plat in (0, 1):
                for fx in (None, 0, 8, 12):
                    def setup(m, ae=ae, plat=plat, fx=fx):
                        if ae:
                            m.zero(E.EFFECT_SLOT_AE, 0x100)
                            m.poke(EADDR + 0x1B8, E.EFFECT_SLOT_AE)
                            m.poke(E.EFFECT_SLOT_AE + 0x68, 77)
                        if plat:
                            m.poke(EADDR + 0x28, ENT0 + 3 * STRIDE)
                            m.poke(ENT0 + 3 * STRIDE + 0x2C, 99)
                        if fx is not None:
                            m.poke8(C.BEAN_REC + 0x10 + 0xE, fx)           # the sprite header byte +0x1E (BreakEffect), header16 starts at record + 0x10
                    V.append(dict(handler='E1', contents=contents, rng=rnd_word(g), setup=setup, ae=ae, plat=plat, fx=fx, hp=0, flags=0x103))
    return V


def a_vectors(g):
    V = []
    for z in (0, 1, 0x10000, 0x7F0000, 0x12345678, -0x20000):
        V.append(dict(handler='A0', posz=z & M32, rng=rnd_word(g)))
    for _ in range(3):
        V.append(dict(handler='A1', rng=rnd_word(g), anim=g.below(5)))
    return V


E.EFFECT_SLOT_AE = 0x801EAA00
