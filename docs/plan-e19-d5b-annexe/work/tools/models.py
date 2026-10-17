"""models.py - Python transcriptions of the native handlers and helpers of the slice E14.b1, written from the disassembly listings (lst/) of
ALUN_CD.EXE (France). Each model mutates an Ent (the entity image) and logs sound/effect/hp calls in j; `gdt(dx, dy)` is GetDirectionToTarget
(0x8002E814, run as real code by the harness, already ported in the DLL).  The vectors in vec.py compare every model with the real code."""
from h import M32, s32

# ---- offsets (the entity image) ------------------------------------------------------------------------------------------------
STATUS, HP, FLAGS, TYPE = 0x10, 0x14, 0x6C, 0x68
TANIM, TDIR, CANIM, CDIR, ADIR, ANIMEND = 0x88, 0x8C, 0x90, 0x94, 0x98, 0xAC
TFX, TFY, FX, FY, FSX, FSY = 0xBC, 0xC0, 0xC4, 0xC8, 0xD4, 0xD8
POSX, POSY, POSZ, TILEX, TILEY, FLOOR = 0x114, 0x118, 0x11C, 0x120, 0x124, 0x134
FORCEADJ, ONGROUND = 0x13C, 0x144
MH = (0x158, 0x15C, 0x160, 0x164)
CURATTACK, TOUCH = 0x1C8, 0x224
B0, B1, B2, B3 = 0x274, 0x275, 0x276, 0x277
AI0, AI1, W28C = 0x280, 0x282, 0x28C

CARDINAL = (0x00, 0x10, 0x08, 0x18)           # 0x80028B54
MASK_TURN = (TFX, TFY, FX, FY, FSX, FSY)


def zero_forces(e):
    for o in MASK_TURN:
        e.set32(o, 0)


def sgn_abs(v):
    return v if v >= 0 else s32(-v)


# ---- helpers ------------------------------------------------------------------------------------------------------------------------
def relpos(e, hero, from_posz=False):
    """0x8007FE20 (dz = floor - hero floor) / 0x8007FE8C (dz = PosZ - hero floor): [|dx|, |dy|, |dz|, dx, dy, dz]"""
    dx = s32(e.s32(TILEX) - hero.s32(TILEX))
    dy = s32(e.s32(TILEY) - hero.s32(TILEY))
    dz = s32((e.s32(POSZ) if from_posz else e.s32(FLOOR)) - hero.s32(FLOOR))
    return [sgn_abs(dx), sgn_abs(dy), sgn_abs(dz), dx, dy, dz]


def try_attack_front(e, rel, near, far, zmax):
    """0x800805C8: the window by the direction word +0x98 (all comparisons signed)"""
    if s32(zmax) < rel[2]:
        return False
    d = e.s32(ADIR)
    if d == 0:
        return near >= rel[0] and rel[4] <= 0 and far >= rel[1]
    if d == 1:
        return near >= rel[0] and rel[4] >= 0 and far >= rel[1]
    if d == 2:
        return near >= rel[1] and rel[3] >= 0 and far >= rel[0]
    if d == 3:
        return near >= rel[1] and rel[3] <= 0 and far >= rel[0]
    return False


def can_move_forward(e, dist):
    """0x80080260: 1 when a corner pair is lower than the floor by more than dist (the 'ledge ahead' predicate of HandleAnimationDirection, without its effects)"""
    thr = s32(dist + 1)
    f = e.s32(FLOOR)
    t0, t1, t2, t3 = (s32(f - e.s32(o)) for o in MH)
    b0, b1, b2, b3 = thr < t0, thr < t1, thr < t2, thr < t3
    d = e.s32(TDIR)
    u = lambda x: x & M32
    return int((b0 and b1 and u(d - 9) < 15)
               or (b1 and b3 and u(d - 17) < 15)
               or (b3 and b2 and not (u(d - 8) < 17))
               or (b2 and b0 and d < 16))


def start_flying(e, hero, anim, rng_range, chance, rng, gdt):
    """0x8007FEF8 (the decompilation calls it StartFlying, the DLL StartWalk): one draw below 0x65 against chance & 0xFF, else a draw below 0x20 for the direction; the animation; and, when the low 16 bits of the range are not 0, AIValues[1] = below(16) + range"""
    r = rng.below(0x65)
    if r < (chance & 0xFF):
        e.set32(TDIR, gdt(s32(hero.s32(POSX) - e.s32(POSX)), s32(hero.s32(POSY) - e.s32(POSY))))
    else:
        e.set32(TDIR, rng.below(0x20))
    e.set32(TANIM, anim & 0xFF)
    if (rng_range & 0xFFFF) != 0:
        e.set16(AI1, rng.below(16) + rng_range)


def dir_to_hero(e, hero, gdt):
    return gdt(s32(hero.s32(POSX) - e.s32(POSX)), s32(hero.s32(POSY) - e.s32(POSY)))


def dir_away(e, hero, gdt):
    return gdt(s32(e.s32(POSX) - hero.s32(POSX)), s32(e.s32(POSY) - hero.s32(POSY)))


# ---- A0, E1 ---------------------------------------------------------------------------------------------------------------------------
def a0(e):
    """0x8006174C: AIValues[0] = high half-word (signed) of PosZ"""
    e.set16(AI0, e.s16(POSZ + 2))


# ---- C15: Orc (axe), type 338 ------------------------------------------------------------------------------------------------------
def c15(e, hero, rng, j, gdt, mapc=None):
    rel = relpos(e, hero)
    a = e.u32(TANIM)
    if a in (0, 1):
        if e.u8(B0) != 0:
            e.set32(TANIM, 3)
            e.set32(TDIR, dir_to_hero(e, hero, gdt))
            return
        if e.u8(B1) == 0:
            d = CARDINAL[e.s32(ADIR)] if 0 <= e.s32(ADIR) < 4 else 0
            e.set8(B1, 4)
            zero_forces(e)
            e.set32(TDIR, (d - 8) & 0x1F)
        ai1 = e.u16(AI1)
        e.set32(TANIM, 1)
        if ai1 == 0:
            e.set16(AI1, 0x1E)
            if try_attack_front(e, rel, 2, 2, 0):
                if rng.below(5) == 0:
                    j.append(('sound', 0x94))
                e.set8(B0, 3)
        else:
            e.set16(AI1, ai1 - 1)
            if e.s32(FORCEADJ) != 0:
                d = CARDINAL[e.s32(ADIR)] if 0 <= e.s32(ADIR) < 4 else 0
                zero_forces(e)
                e.set8(B1, e.u8(B1) - 1)
                e.set32(TDIR, (d - 0x10) & 0x1F)
    elif a == 3:
        if e.s32(ANIMEND) == 0:
            return
        e.set8(B0, e.u8(B0) - 1)
        if e.u8(B0) == 0:
            r = rng.below(4)
            e.set32(TANIM, 0)
            e.set16(AI1, 0)
            e.set8(B1, 0)
            e.set32(TDIR, r << 3)
        else:
            e.set32(CANIM, 0xFFFFFFFF)
    elif a in (5, 6):
        if e.s32(ANIMEND) == 0:
            return
        if e.u8(B3) != 0:
            e.set32(TANIM, 2)
            e.set32(FLAGS, e.u32(FLAGS) | 0x40)
        else:
            if rel[0] < 2 and rel[1] < 2:
                e.set8(B0, 3)
            e.set32(TANIM, 0)


# ---- C16: Orc (mace), type 341 -----------------------------------------------------------------------------------------------------
def c16(e, hero, rng, j, gdt, mapc=0):
    rel = relpos(e, hero)
    a = e.u32(TANIM)
    if a == 0:
        ai1 = e.u16(AI1)
        if ai1 != 0:
            e.set16(AI1, ai1 - 1)
        else:
            start_flying(e, hero, 1, 0x78, 0x46, rng, gdt)
            e.set8(B1, 0)
    elif a == 1:
        v = (e.u16(AI1) - 1) & 0xFFFF
        e.set16(AI1, v)
        if v == 0:
            if e.u8(B1) != 0:
                e.set8(B1, e.u8(B1) - 1)
                r = rng.below(0x20)
                e.set16(AI1, 0x78)
                e.set32(TDIR, r)
            else:
                e.set32(TANIM, 0)
                e.set16(AI1, 0x14)
                e.set8(B0, 0)
            return
        if try_attack_front(e, rel, 3, 3, 0):
            if rng.below(5) == 0:
                j.append(('sound', 0x94))
            c16_attack(e, hero, gdt)
            return
        if e.s32(FORCEADJ) == 0:
            return
        if e.u8(B1) != 0:
            e.set8(B1, e.u8(B1) - 1)
            r = rng.below(0x20)
            e.set16(AI1, 0x78)
            e.set32(TDIR, r)
        else:
            e.set8(B0, e.u8(B0) + 1)
            if e.u8(B0) < 4:
                e.set32(TANIM, 0)
                return
            e.set8(B0, 0)
            r = rng.below(0x20)
            e.set16(AI1, 0x78)
            e.set8(B1, 3)
            e.set32(TDIR, r)
    elif a == 3:
        e.set32(W28C, e.u32(W28C) + 1)
        if (e.u32(W28C) & 7) == 0:
            j.append(('effect', 0, mapc, 0, e.u32(POSX), e.u32(POSY), e.u32(FLOOR)))
        if e.s32(ANIMEND) != 0:
            e.set32(TANIM, 0)
            e.set16(AI1, 0x1E)
    elif a == 5:
        if e.s32(ANIMEND) == 0:
            return
        if e.u8(B3) != 0:
            e.set32(TANIM, 2)
            e.set32(FLAGS, e.u32(FLAGS) | 0x40)
            return
        c16_attack(e, hero, gdt)
    elif a == 6:
        if e.s32(ANIMEND) != 0:
            e.set32(TANIM, 0)
            e.set16(AI1, 0x14)
            e.set32(TDIR, (e.u32(TDIR) + 0x10) & 0x1F)


def c16_attack(e, hero, gdt):
    """0x8006A258: animation 7 facing the hero, the dust counter cleared"""
    e.set32(TANIM, 7)
    e.set32(TDIR, dir_to_hero(e, hero, gdt))
    e.set32(W28C, 0)


# ---- C17: Orc (iron armour), type 344 ----------------------------------------------------------------------------------------------
def c17(e, hero, rng, j, gdt, mapc=None):
    rel = relpos(e, hero)
    a = e.u32(TANIM)
    if a == 0:
        if e.u16(AI1) == 0:
            r = rng.below(0x20)
            e.set16(AI1, 0x3C)
            e.set32(TDIR, r)
        e.set16(AI1, e.u16(AI1) - 1)
        if e.u16(AI1) != 0:
            return
        e.set32(TANIM, 1)
        e.set16(AI1, rng.below(0x20) + 0x78)
    elif a == 1:
        e.set16(AI1, e.u16(AI1) - 1)
        if e.u16(AI1) == 0:
            e.set32(TANIM, 0)
            return
        ax, dx, dy, az = rel[0], rel[3], rel[4], rel[2]
        if dy <= 0 and ax < 4 and az <= 0x100000:
            if ax == 0 and dy >= -4:
                e.set32(TANIM, 4)
                e.set32(TDIR, 0)
                return
            if dy >= -1:
                e.set32(TANIM, 2)
                e.set32(TDIR, 0x18 if dx < -1 else 0)
                return
            if dy >= -4:
                e.set32(TANIM, 3)
                e.set32(TDIR, 0x18 if dx < -1 else 0)
                return
        if e.s32(FORCEADJ) != 0:
            e.set32(TDIR, (e.u32(TDIR) - 0x10) & 0x1F)
            e.set16(AI1, 0x78 if e.s32(ADIR) != 0 else 0xF0)
            e.set32(TANIM, 1)
            return
        if can_move_forward(e, 0):
            e.set32(TANIM, 0)
            e.set16(AI1, 0)
    elif a in (2, 3, 4):
        if e.s32(ANIMEND) != 0:
            e.set32(TANIM, 0)
            e.set16(AI1, 0)
    elif a == 6:
        if e.s32(ANIMEND) == 0:
            return
        if e.u8(B3) != 0:
            e.set32(TANIM, 7)
            e.set32(FLAGS, e.u32(FLAGS) | 0x40)
        else:
            e.set32(TANIM, 1)
            e.set32(TDIR, dir_to_hero(e, hero, gdt))
            e.set16(AI1, 0x78)


# ---- C18: Wasp, type 455 ---------------------------------------------------------------------------------------------------------------
def c18(e, hero, rng, j, gdt, mapc=None):
    rel = relpos(e, hero, from_posz=True)
    a = e.u32(TANIM)
    if a == 0:
        if e.u8(B0) != 0:
            e.set32(TDIR, dir_to_hero(e, hero, gdt))
            e.set16(AI1, 0x3C)
            e.set8(B0, e.u8(B0) - 1)
            dz = rel[5]
            e.set32(TANIM, (3 if dz > 0x180000 else 1) if dz > 0xFFFFF else 2)
            return
        ai1 = e.u16(AI1)
        if ai1 != 0:
            v = (ai1 - 1) & 0xFFFF
            e.set16(AI1, v)
            if v != 0:
                return
            e.set16(AI1, 0x78)
            dz = rel[5]
            if dz > 0xFFFFF:
                e.set32(TANIM, 3 if (dz > 0x180000 and e.s32(ONGROUND) == 0) else 1)
            else:
                e.set32(TANIM, 2)
            return
        if rel[0] < 3 and rel[1] < 3 and rel[2] <= 0x200000:
            e.set32(TDIR, dir_to_hero(e, hero, gdt))
            e.set16(AI1, 0x3C)
            return
        r1 = rng.below(0x20)
        r2 = rng.below(0x29)
        e.set32(TDIR, r1)
        t0 = s32(e.s32(POSZ) - (e.u16(AI0) << 16))
        e.set16(AI1, r2 + 0x50)
        if t0 > 0x4FFFFF:
            e.set32(TANIM, 3)
        elif t0 > -0x500000:
            e.set32(TANIM, rng.below(3) + 1)
        else:
            e.set32(TANIM, 2)
    elif a in (1, 2, 3):
        v = (e.u16(AI1) - 1) & 0xFFFF
        e.set16(AI1, v)
        if a == 3:
            if v == 0 or e.s32(ONGROUND) != 0:
                e.set32(TANIM, 0)
        elif v == 0:
            e.set32(TANIM, 0)
        if e.u8(B0) != 0:
            return
        if try_attack_front(e, rel, 1, 4, 0x200000):
            c18_attack(e, hero, gdt)
        elif a == 1:
            if e.s32(FORCEADJ) != 0:
                e.set32(TANIM, 0)
                e.set32(TDIR, (e.u32(TDIR) + 4) & 0x1F)
    elif a == 5:
        if e.s32(ANIMEND) == 0:
            return
        if e.u8(B3) != 0:
            e.set32(TANIM, 6)
            e.set32(FLAGS, e.u32(FLAGS) | 0x40)
        else:
            e.set32(TANIM, 0)
            e.set8(B0, 0)
            e.set16(AI1, 0)


def c18_attack(e, hero, gdt):
    """0x8006A8E8"""
    e.set32(TANIM, 0)
    e.set32(TDIR, dir_to_hero(e, hero, gdt))
    e.set16(AI1, 0)
    e.set8(B0, 10)


# ---- C19: Fish, type 392 ---------------------------------------------------------------------------------------------------------------
def c19(e, hero, rng, j, gdt, mapc=None):
    rel = relpos(e, hero)
    a = e.u32(TANIM)
    if a == 1:
        if rel[0] < 3 and rel[1] < 3 and rel[2] <= 0x100000:
            e.set8(B0, 1)
            r = rng.below(16)
            e.set32(TANIM, 0)
            e.set16(AI1, r + 0x3C)
            return
        if e.s32(FORCEADJ) == 0 and not can_move_forward(e, 0):
            return
        r = rng.below(0x20)
        e.set32(TANIM, 0)
        e.set16(AI1, 0x3C)
        e.set32(TDIR, r)
    elif a == 0:
        if e.u16(AI1) != 0:
            e.set16(AI1, e.u16(AI1) - 1)
            return
        if e.u8(B0) == 0:
            e.set32(TANIM, 1)
            return
        e.set32(TDIR, dir_to_hero(e, hero, gdt))
        e.set32(TANIM, 3)
        e.set8(B0, 0)
    elif a == 5:
        if e.s32(ONGROUND) == 0:
            return
        e.set32(TANIM, 0)
        e.set16(AI1, 0x14)
        j.append(('sound', 0x96))
    elif a == 7:
        if e.s32(ANIMEND) == 0:
            return
        if e.u8(B3) != 0:
            e.set32(TANIM, 8)
            e.set32(FLAGS, e.u32(FLAGS) | 0x40)
        else:
            e.set32(TANIM, 0)
            e.set16(AI1, 0x14)
            e.set8(B0, 0)


# ---- D handlers (ComputeNewHp is a stub in the harness: cnh(e) says what it returned) ------------------------------------------------
def hit_common(e, hero, anim, cnh, gdt):
    """D6 (6) and D11 (4); D9 and D1 have the same shape"""
    if cnh(e):
        e.set8(B3, 1)
    e.set32(TANIM, anim)
    e.set32(TDIR, dir_away(e, hero, gdt))


def d5(e, hero, rng, j, gdt, cnh, nib):
    """0x8007DBE0: Orc (iron armour) 344, Mimic 404, Mummies 425/454, Wood man 461, Furnace 467. The special case is the type 0x1A9 = 425."""
    e.set32(TDIR, dir_away(e, hero, gdt))
    if ((nib - 1) & M32) < 3 and e.u32(TYPE) == 0x1A9 and e.u32(TANIM) < 2:
        if rng.below(100) >= 0x3C:
            e.set32(TANIM, 0xB)
            return
    if cnh(e):
        e.set8(B3, 1)
    e.set32(TANIM, 5)


def d12(e, hero, rng, j, gdt, cnh, nib):
    """0x8007DF4C: Orc (axe) 338 and Orc (mace) 341: 30% parry (animation 6, no damage) for the sword nibbles 1 to 3"""
    e.set32(TDIR, dir_away(e, hero, gdt))
    if ((nib - 1) & M32) < 3:
        r = rng.below(100)
        if r >= 0x46:
            t = e.u32(TYPE)
            a = e.u32(TANIM)
            if t == 0x152:
                if a < 2:
                    e.set32(TANIM, 6)
                    return
            elif t == 0x155:
                if a < 2 or a == 7:
                    e.set32(TANIM, 6)
                    return
    if cnh(e):
        e.set8(B3, 1)
    e.set32(TANIM, 4)
