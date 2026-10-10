"""e14model.py - E14.a1 oracle: an INDEPENDENT Python model of the six native handlers, written from the discovery annexes (docs/plan-e14-annexe/e14a/muruta.md section 3-4,
beannoide.md section 3-5, check.md) and the data tables of the disc file; never from the emulated run.  It is a cross-check of the oracle and of the annexes, not an input of the oracle.

A model call takes the entity image BEFORE the call (0x294 bytes), the hero image (0x294 bytes), the hero's current attack (attribute, power) or None, and the RNG word; it returns the predicted
entity image AFTER the call, the predicted RNG word, the list of sounds it asks for, and ComputeNewHp's inputs (D handlers: HP is checked by the caller against kmodel).  Writes are made on a
copy of the image, so ANY write the model does not know about shows up as a difference.

Shares with the oracle only: the disc tables (QUANT 0x80028B34, the cardinal table 0x80028B54, the atan table 0x80022488, the thresholds 0x80022688) read from the EXE file.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
import lib

M32 = 0xFFFFFFFF


def s32(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


def _tab(addr, n):
    o = addr - lib.BASE + lib.OFF
    return list(lib.DATA[o:o + n])


QUANT = _tab(0x80028B34, 32)          # counter-clockwise quarter-turn / snap table
CARDINAL = _tab(0x80028B54, 4)        # [0x00, 0x10, 0x08, 0x18]
ATAN = [int.from_bytes(bytes(_tab(0x80022488 + 2 * i, 2)), 'little', signed=True) for i in range(256)]
THR = [int.from_bytes(bytes(_tab(0x80022688 + 4 * i, 4)), "little") for i in range(29)]   # 15, 31, ..., 0xFFFFFFFF
CLOSE_T = [0, 1, 2, 2, 3, 3, 4, 4]    # jump-table groups of the close branch (0x8002722C): r 0 | 1 | 2,3 | 4,5 | 6,7 -> arms 0 1 2 2 3 3 4 4 (arm ids only used locally)


class Img:
    """the entity image with typed accessors that record every write"""
    def __init__(self, raw):
        self.b = bytearray(raw)
        self.writes = []

    def r32(self, o, signed=True):
        v = int.from_bytes(self.b[o:o + 4], 'little')
        return s32(v) if signed else v

    def r16(self, o):
        return int.from_bytes(self.b[o:o + 2], 'little')

    def r8(self, o):
        return self.b[o]

    def w32(self, o, v):
        self.b[o:o + 4] = (v & M32).to_bytes(4, 'little')
        self.writes.append((o, 4))

    def w16(self, o, v):
        self.b[o:o + 2] = (v & 0xFFFF).to_bytes(2, 'little')
        self.writes.append((o, 2))

    def w8(self, o, v):
        self.b[o] = v & 0xFF
        self.writes.append((o, 1))

    def raw(self):
        return bytes(self.b)


# ---- entity field offsets (the discovery annexes; the same offsets the oracle decodes) ---------------------------------------------------------------
ANIM, DIR, ANIMDIR, ENDFLAG, FLAGS = 0x88, 0x8C, 0x98, 0xAC, 0x6C
POSX, POSY = 0x114, 0x118
TILEX, TILEY, FLOOR = 0x120, 0x124, 0x134
FADJ, ONG = 0x13C, 0x144
MH = (0x158, 0x15C, 0x160, 0x164)
FORCES = (0xBC, 0xC0, 0xC4, 0xC8, 0xD4, 0xD8)       # TargetForceX/Y, ForceX/Y, ForceStepX/Y (the six words every "zero the forces" writes)
B0, B1, B3 = 0x274, 0x275, 0x277
AI0, AI1 = 0x280, 0x282
BOX, CURATK = 0x1D4, 0x1C8


class Rng:
    def __init__(self, s):
        self.s = s & M32
        self.n = 0

    def below(self, n):
        self.s = (self.s * 0x7D2B89DD + 0xE06A02E7) & M32
        self.n += 1
        return (self.s * n) >> 32


def get_dir(dx, dy):
    """0x8002E814 GetDirectionToTarget(dx, dy): quadrant, scaled index into the 16 x 16 atan table, quadrant arithmetic (read from the disassembly)"""
    dx, dy = s32(dx), s32(dy)
    q = (2 if dy <= 0 else 0) + (1 if dx < 0 else 0)
    ax, ay = abs(dx), abs(dy)
    a3 = max(ax, ay)
    sh = 0
    if THR[0] < a3:
        k = 1
        while True:
            sh = k
            if not (THR[k] < a3):
                break
            k += 1
    ax >>= sh
    ay >>= sh
    ang = ATAN[(ay << 4) + ax]
    if q == 0:
        v = ang + 0x18
    elif q == 1:
        v = 8 - ang
    elif q == 2:
        v = 0x18 - ang
    else:
        v = ang + 8
    return v & 0x1F


def rel_pos(e, h):
    """0x8007FE20: |dTileX|, |dTileY|, |dFloor| and the signed differences (entity - hero)"""
    dx = e.r32(TILEX) - h.r32(TILEX)
    dy = e.r32(TILEY) - h.r32(TILEY)
    dz = e.r32(FLOOR) - h.r32(FLOOR)
    return (abs(dx), abs(dy), abs(dz), dx, dy, dz)


def try_attack2(rel, d, near, far, zmax):
    """0x8008048C TryAttackPlayer2(self, rel, direction, near, far, zmax)"""
    ax, ay, az, dx, dy, dz = rel
    if zmax < az:
        return 0
    if d == 0:
        return int(ax <= near and dy <= 0 and ay <= far)
    if d == 1:
        return int(ax <= near and dy >= 0 and ay <= far)
    if d == 2:
        return int(ay <= near and dx >= 0 and ax <= far)
    if d == 3:
        return int(ay <= near and dx <= 0 and ax <= far)
    return 0


def try_front(e, rel, near, far, zmax):
    """0x800805C8: TryAttackPlayer2 with the entity's own AnimationDirection (+0x98)"""
    return try_attack2(rel, e.r32(ANIMDIR), near, far, zmax)


def zero_forces(e):
    for o in FORCES:
        e.w32(o, 0)


def had(e, anim, dist):
    """0x80080144 HandleAnimationDirection(self, animId, dist)"""
    thr = dist + 1
    D = [e.r32(FLOOR) - e.r32(o) for o in MH]
    b = [thr < d for d in D]                       # signed compare
    d = e.r32(DIR)
    turn = ((b[0] and b[1] and 9 <= d <= 23) or (b[1] and b[3] and 17 <= d <= 31) or (b[3] and b[2] and not (8 <= d <= 24)) or (b[2] and b[0] and d < 16))
    if not turn:
        return 0
    e.w32(ANIM, anim & 0xFF)
    zero_forces(e)
    e.w32(DIR, QUANT[d & 31])
    return 1


def start_walk(e, h, rng, anim, base, chance):
    """0x8007FEF8 StartWalk(self, anim, base, chance)"""
    r = rng.below(0x65)
    if r < chance:
        e.w32(DIR, get_dir(h.r32(POSX) - e.r32(POSX), h.r32(POSY) - e.r32(POSY)))
    else:
        e.w32(DIR, rng.below(0x20))
    e.w32(ANIM, anim)
    if base != 0:
        e.w16(AI1, base + rng.below(0x10))


def dir_to_hero(e, h):
    return get_dir(h.r32(POSX) - e.r32(POSX), h.r32(POSY) - e.r32(POSY))


# ---- the handlers ---------------------------------------------------------------------------------------------------------------------------------
def a1(e, h, hatk, rng):
    e.w32(ANIM, 2)
    return []


def a0(e, h, hatk, rng):
    e.w16(AI0, (e.r32(0x11C) >> 16) & 0xFFFF)          # AIValues[0] = PosZ >> 16 (0x8006174C)
    return []


def c1(e, h, hatk, rng):
    """Beannoide tick, beannoide.md section 3 (binary side: check.md rows 16, 17)"""
    anim = e.r32(ANIM) & M32
    if anim >= 20:
        return []
    rel = rel_pos(e, h)
    r0, r1, r2 = rel[0], rel[1], rel[2]
    ai1 = e.r16(AI1)
    if anim == 0:
        if ai1 != 0:
            ai1 = (ai1 - 1) & 0xFFFF
            e.w16(AI1, ai1)
            if ai1 != 0:
                return []
        if r0 < 3 and r1 < 3 and r2 <= 0x200000 and e.r8(B1) == 0:
            e.w32(DIR, dir_to_hero(e, h))
            e.w32(ANIM, 3)
            e.w8(B1, 1)
            return []
        e.w32(ANIM, 1)
        e.w16(AI1, 0x3C + rng.below(0x3D))
        if r0 < 4 and r1 < 4:
            e.w32(DIR, dir_to_hero(e, h))
        e.w8(B1, 0)
    elif anim == 1:
        ai1 = (ai1 - 1) & 0xFFFF
        e.w16(AI1, ai1)
        if ai1 != 0:
            return []
        if e.r32(FADJ) == 0:
            r = rng.below(0x2000)
            e.w32(ANIM, 0)
            e.w16(AI1, r & 0x7F)
            e.w32(DIR, r >> 8)
        else:
            r = rng.below(0x1F)
            e.w32(DIR, QUANT[e.r32(DIR) & 31])
            zero_forces(e)
            e.w16(AI1, 0x1E + r)
    elif anim == 2:
        if r0 < 4 and r1 < 4:
            e.w32(ANIM, 4)
    elif anim == 3:
        if e.r32(ENDFLAG) != 0:
            start_walk(e, h, rng, 1, 0x28, 0x14)
            e.w8(B1, 0)
    elif anim == 4:
        if e.r32(ENDFLAG) != 0:
            e.w32(ANIM, 0)
            e.w16(AI1, 0x1E)
    elif anim == 7:
        if e.r32(ENDFLAG) != 0:
            if e.r8(B3) != 0:
                e.w32(ANIM, 6)
                e.w32(FLAGS, e.r32(FLAGS) | 0x40)
            else:
                e.w32(ANIM, 0)
                e.w16(AI1, 0x28)
    elif 8 <= anim <= 14:
        if e.r32(ONG) != 0:
            e.w32(ANIM, 2)
    elif anim == 19:
        ai1 = (ai1 - 1) & 0xFFFF
        e.w16(AI1, ai1)
        if ai1 == 0:
            e.w32(ANIM, 0)
            e.w16(AI1, 0x1E)
    return []


def c10(e, h, hatk, rng):
    """Muruta tick, muruta.md section 3.  Returns the sounds asked for."""
    sounds = []
    rel = rel_pos(e, h)
    r0, r1 = rel[0], rel[1]
    st = e.r32(ANIM) & M32

    def tail():
        e.w32(DIR, (e.r32(DIR) + 2) & 0x1C)

    def cooldown_exit():            # 0x800688E0
        e.w16(AI1, 0x1E)
        e.w8(B1, 0)
        tail()

    if st == 0:
        if e.r8(B0) == 0:
            if try_front(e, rel, 2, 6, 0x100000):
                e.w8(B0, 1)
                e.w16(AI1, 0)
                tail()
                return sounds
            ai1 = e.r16(AI1)
            if ai1 != 0:
                e.w16(AI1, (ai1 - 1) & 0xFFFF)
                tail()
                return sounds
            if e.r8(B1) == 0:
                if rng.below(4) != 1:
                    tail()
                    return sounds
            b = e.r8(B1)
            e.w8(B1, b + 1)
            if b in (0, 1, 2):
                if rng.below(4) == 0:
                    sounds.append(0xCD)
                e.w32(ANIM, 8)
                e.w32(DIR, dir_to_hero(e, h))
            elif b == 3:
                d = CARDINAL[rng.below(4)]
                e.w16(AI1, 0x14)
                zero_forces(e)
                e.w32(DIR, d)
            elif b == 4:
                e.w16(AI1, 0x14)
                zero_forces(e)
                e.w32(DIR, QUANT[e.r32(DIR) & 31])
            elif b == 5:
                e.w32(ANIM, 0xC)
                e.w16(AI1, 0x78)
            else:
                e.w8(B1, 0)
                e.w16(AI1, 0)
            tail()
            return sounds
        # alerted
        if not try_front(e, rel, 2, 6, 0x100000) and not try_attack2(rel, e.r32(ANIMDIR) ^ 1, 2, 3, 0x100000):
            e.w8(B1, 0)
            e.w8(B0, 0)
            e.w16(AI1, 0x1E)
            tail()
            return sounds
        ai1 = e.r16(AI1)
        if ai1 != 0:
            e.w16(AI1, (ai1 - 1) & 0xFFFF)
            tail()
            return sounds
        e.w32(DIR, dir_to_hero(e, h))
        if h.r32(BOX, False) != 0 and h.r32(CURATK, False) != 0 and hatk is not None and hatk[0] != 0 and r0 < 5 and r1 < 5:
            if rng.below(4) == 0:
                sounds.append(0xCD)
            e.w32(ANIM, 3)
            e.w32(DIR, dir_to_hero(e, h))
            e.w16(AI1, 0x1E)
            e.w8(B1, 0)
            tail()
            return sounds
        away = (e.r32(DIR) - 0x10) & 0x1F
        if r0 < 3 and r1 < 3:
            r = rng.below(8)
            if r == 0:
                e.w32(ANIM, 0xC)
                e.w16(AI1, 0x14)
                e.w32(DIR, dir_to_hero(e, h))
            elif r == 1:
                e.w32(ANIM, 0xC)
                e.w16(AI1, 0x3C)
                e.w32(DIR, away)
            elif r in (2, 3):
                e.w32(ANIM, 0xC)
                e.w16(AI1, 100)
                e.w32(DIR, away)
            elif r in (4, 5):
                if rng.below(4) == 0:
                    sounds.append(0xCD)
                e.w32(ANIM, 7)
            else:
                e.w32(ANIM, 4)
                e.w32(DIR, away)
        elif r0 < 5 and r1 < 5:
            r = rng.below(5)
            if r in (0, 1):
                e.w32(ANIM, 8)
            else:
                e.w32(ANIM, 0xC)
                e.w16(AI1, 0x1E)
        elif r0 < 7 and r1 < 7:
            r = rng.below(8)
            if r in (0, 1, 2):
                e.w32(ANIM, 0xC)
                e.w16(AI1, 0x3C)
            elif r == 3:
                e.w32(ANIM, 0xC)
                e.w16(AI1, 0x1E)
            elif r in (4, 5):
                e.w32(ANIM, 4)
            else:
                e.w32(ANIM, 8)
        tail()
        return sounds
    if st == 5:
        if e.r32(ENDFLAG) != 0:
            if e.r8(B3) == 0:
                e.w8(B0, 1)
                e.w32(ANIM, 0)
                e.w16(AI1, 0x1E)
                e.w8(B1, 0)
            else:
                e.w32(ANIM, 6)
                e.w32(FLAGS, e.r32(FLAGS) | 0x40)
        tail()
        return sounds
    if st in (9, 10):
        if e.r32(ONG) != 0:
            e.w32(ANIM, 0)
            e.w16(AI1, 0x14)
        tail()
        return sounds
    if st == 12:
        if e.r8(B0) == 0 and try_front(e, rel, 2, 6, 0x100000):
            e.w8(B0, 1)
            e.w32(ANIM, 0)
            cooldown_exit()
            return sounds
        ai1 = (e.r16(AI1) - 1) & 0xFFFF
        e.w16(AI1, ai1)
        if ai1 == 0 or e.r32(FADJ) != 0:
            e.w32(ANIM, 0)
            zero_forces(e)
            cooldown_exit()
            return sounds
        if had(e, 0xC, 0x400000):
            e.w32(ANIM, 0)
            cooldown_exit()
            return sounds
        tail()
        return sounds
    tail()
    return sounds


def hit_reaction(anim):
    """D1 (anim 5) / D9 (anim 2): ComputeNewHp (checked elsewhere), Bytes[3] on a lethal result, anim, direction away from the hero.  `hp_after` is supplied by the caller"""
    def f(e, h, hatk, rng, hp_after):
        if hp_after == 0:
            e.w8(B3, 1)
        e.w32(ANIM, anim)
        e.w32(DIR, get_dir(e.r32(POSX) - h.r32(POSX), e.r32(POSY) - h.r32(POSY)))
        return []
    return f


d1 = hit_reaction(5)
d9 = hit_reaction(2)

HANDLERS = {'A1': a1, 'A0': a0, 'C1': c1, 'C10': c10}
DHANDLERS = {'D1': d1, 'D9': d9}


def predict(handler, ent_before, hero_raw, hero_atk, rng_in, hp_after=None):
    """-> (entity image after, rng after, sounds, number of draws)"""
    e = Img(ent_before)
    h = Img(hero_raw)
    rng = Rng(rng_in)
    if handler in HANDLERS:
        snd = HANDLERS[handler](e, h, hero_atk, rng)
    else:
        snd = DHANDLERS[handler](e, h, hero_atk, rng, hp_after)
    return e.raw(), rng.s, snd, rng.n
