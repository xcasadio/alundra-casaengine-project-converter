"""h4_models.py - Python transcriptions of the tome drivers, their cancel handlers, the item-driver wrapper and the target query of ALUN_CD.EXE (France),
written from the listings in lst/ (drv_*.txt, cancel_*.txt, driver_8003634C.txt, f_8003AF70.txt, f_8003AE04_sort.txt).
The models run on a memory image (bytearray copy of the machine's RAM) so that a comparison with the real code can be byte for byte on the written regions.

Conventions: addresses are the binary's virtual addresses (0x80......); u32/s32 helpers; every function says which listing it comes from.
Only the stubbed functions (SpawnWarpEntity, CreateEffectEntity, PlaySoundEffect) are called through the `env` object; everything else is modelled here."""
import sys

M32 = 0xFFFFFFFF
BASE = 0x80000000


def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


def s16(x):
    x &= 0xFFFF
    return x - 0x10000 if x & 0x8000 else x


HERO = 0x80127D30
STRIDE = 0x294
INUSE, COUNTER, PHASE = 0x80127164, 0x8012715C, 0x80127160
G_COUNT = 0x80134360              # index of the last used entity slot
RNG = 0x80098708
SHAKE = 0x800E4338                # 9 words saved/restored by fire level 2
SHAKE_BAK = 0x80127138

TAB_X = 0x80023654                # 32 x s16, the "DirForceTableX"
TAB_Y = 0x80023694
EARTH1, EARTH2, WATER_UP, WATER_LO, WIND1 = 0x8002343C, 0x80023504, 0x80023544, 0x8002357C, 0x800235E0


class Mem:
    """a copy of the machine RAM; reads/writes as the MIPS code does"""

    def __init__(self, ram):
        self.b = bytearray(ram)

    @staticmethod
    def o(a):
        return a & 0x1FFFFFFF

    def u8(self, a):
        return self.b[self.o(a)]

    def u16(self, a):
        o = self.o(a)
        return self.b[o] | self.b[o + 1] << 8

    def s16(self, a):
        return s16(self.u16(a))

    def u32(self, a):
        o = self.o(a)
        return int.from_bytes(self.b[o:o + 4], 'little')

    def s32(self, a):
        return s32(self.u32(a))

    def w8(self, a, v):
        self.b[self.o(a)] = v & 0xFF

    def w16(self, a, v):
        o = self.o(a)
        self.b[o:o + 2] = (v & 0xFFFF).to_bytes(2, 'little')

    def w32(self, a, v):
        o = self.o(a)
        self.b[o:o + 4] = (v & M32).to_bytes(4, 'little')


def ent(i):
    return HERO + STRIDE * i


class Env:
    """the stubbed world of a vector: spawn / effect / sound with journals.  `spawn_results` is a list of pool pointers (or 0 for a refused spawn) consumed in order;
    `fx_results` the same for effects."""

    def __init__(self, mem, spawn_results, fx_results, gdt):
        self.m = mem
        self.spawn_results = list(spawn_results)
        self.fx_results = list(fx_results)
        self.j = []
        self.gdt = gdt

    def spawn(self, owner, flag, sprite, x, y, z, direction):
        self.j.append(('spawn', owner & M32, flag & M32, sprite & M32, x & M32, y & M32, z & M32, direction & M32))
        return self.spawn_results.pop(0) if self.spawn_results else 0

    def fx(self, a0, eid, var, x, y, z):
        self.j.append(('fx', a0 & M32, eid & M32, var & M32, x & M32, y & M32, z & M32))
        return self.fx_results.pop(0) if self.fx_results else 0

    def sound(self, sid):
        self.j.append(('sound', sid & M32))


# ---- the generator (inline LCG of the binary): state' = state * 0x7D2B89DD + 0xE06A02E7; a draw below n is hi(state' * n) -----------------------------------
def rng_step(m):
    s = (m.u32(RNG) * 0x7D2B89DD + 0xE06A02E7) & M32
    return s


def rng_draw(m, n):
    """one step, state stored, returns hi(new_state * n) (unsigned multu)"""
    s = rng_step(m)
    m.w32(RNG, s)
    return (s * n) >> 32


# ---- DestroyEntityNoFx 0x8003A774 -------------------------------------------------------------------------------------------------------------------------
def destroy_nofx(m, e):
    fx = m.u32(e + 0x1B8)
    m.w32(e + 0x10, 4)
    m.w32(e + 0x228, M32)
    if fx != 0:
        m.w32(fx + 0x68, 0)
        m.w32(e + 0x1B8, 0)
    plat = m.u32(e + 0x28)
    if plat != 0:
        m.w32(plat + 0x2C, 0)


# ---- the target query 0x8003AF70 and its sort 0x8003AE04 ------------------------------------------------------------------------------------------------------
def qsort_pair(ents, dists, lo, hi):
    """0x8003AE04 (Hoare quicksort on the unsigned distances, pivot = (d[lo] + d[hi]) / 2 with a 32-bit add, both arrays moved together)"""
    if lo >= hi and False:
        return
    i, j = lo, hi
    pivot = (((dists[lo] + dists[hi]) & M32) >> 1)
    while True:
        while dists[i] < pivot:
            i += 1
        while pivot < dists[j]:
            j -= 1
        if i < j:
            # swap (this version of the listing swaps then moves both pointers)
            ents[i], ents[j] = ents[j], ents[i]
            dists[i], dists[j] = dists[j], dists[i]
            i += 1
            j -= 1
            continue
        break
    # listing 0x8003AF1C: a3 = i - 1 ; if lo < a3 recurse(lo, a3) ; a2 = j + 1 ; if a2 < hi recurse(a2, hi)
    a3 = i - 1
    if lo < a3:
        qsort_pair(ents, dists, lo, a3)
    a2 = j + 1
    if a2 < hi:
        qsort_pair(ents, dists, a2, hi)


def query(m, owner, mask, atk_type):
    """0x8003AF70(owner, mask, atk_type, out_ents, out_dists): returns the (entity pointer, squared distance) list in the order of the binary"""
    count = s32(m.u32(G_COUNT))
    ents, dists = [], []
    for i in range(1, count):                          # t3 = 1 .. count-1 (the last used slot is NOT scanned)
        e = ent(i)
        if e == owner:
            continue
        if ((m.s32(e + 0x10) - 2) & M32) >= 2:         # status 2 or 3
            continue
        if m.u32(e + 0x20) != 0:
            continue
        if (m.u32(e + 0x6C) & mask) == 0:
            continue
        if m.u32(e + 0x1D0) != 0:
            continue
        if m.u32(e + 0x1CC) != 0:
            continue
        if m.u32(e + 0xB4) & 0x40:
            continue
        sheet = m.u32(e + 0x1C4)
        if sheet == 0:
            continue
        if (m.u8(sheet + atk_type + 2) & 0xC0) == 0x80:
            continue
        dx = (m.s32(e + 0x114) - m.s32(owner + 0x114)) >> 16
        dy = (m.s32(e + 0x118) - m.s32(owner + 0x118)) >> 16
        ents.append(e)
        dists.append((dx * dx + dy * dy) & M32)
    n = len(ents)
    if n >= 3:                                         # 0x8003B0B4 slti 3: sorted only when 3 or more
        qsort_pair(ents, dists, 0, n - 1)
    return list(zip(ents, dists))


# ---- hero helpers ------------------------------------------------------------------------------------------------------------------------------------------------------
def hero_anim(m):
    return m.s32(HERO + 0x88)


def set_hero_anim(m, a):
    m.w32(HERO + 0x88, a)


# ---- the cancel handlers (0x800350C0 dispatch) and the slope helper 0x800351B8 ------------------------------------------------------------------------
def cancel(env):
    m = env.m
    item = m.s32(INUSE)
    if item == 0:
        return
    idx = (item - 0x1F) & M32
    if idx < 0x14:
        h = CANCELS.get(item)
        if h:
            h(env)
        elif item in (0x1F, 0x23):
            raise NotImplementedError('beans/bomb cancel not modelled here')
    m.w32(INUSE, 0)


def cancel_earth2(env):                                # 0x80034BDC
    m = env.m
    for i in range(1, s32(m.u32(G_COUNT)) + 1):
        e = ent(i)
        if m.s32(e + 0x68) == 0x11 and m.s32(e + 0x10) == 2:
            m.w32(e + 0x10, 3)
            m.w32(e + 0x20, 0)
            m.w32(e + 0x88, 1)


def cancel_water(env):                                 # 0x80034C54
    m = env.m
    for i in range(1, s32(m.u32(G_COUNT)) + 1):
        e = ent(i)
        if m.s32(e + 0x68) == 0x13 and m.s32(e + 0x10) == 2:
            env.fx(0, 0x1A, 0, m.u32(e + 0x114), m.u32(e + 0x118), m.u32(e + 0x11C))
            destroy_nofx(m, e)


def cancel_fire1(env):                                 # 0x80034D2C
    m = env.m
    for i in range(1, s32(m.u32(G_COUNT)) + 1):
        e = ent(i)
        if m.s32(e + 0x10) != 2:
            continue
        t = m.s32(e + 0x68)
        if t == 0x14:
            env.fx(0, 0x13, 2, m.u32(e + 0x114), m.u32(e + 0x118), m.u32(e + 0x11C))
            destroy_nofx(m, e)
            t = m.s32(e + 0x68)
        if t == 0x17:
            m.w32(e + 0x10, 3)
            m.w32(e + 0x20, 0)


def restore_shake(m):
    for k in range(9):
        m.w32(SHAKE + 4 * k, m.u32(SHAKE_BAK + 4 * k))


def cancel_fire2(env):                                 # 0x80034E08
    m = env.m
    restore_shake(m)
    for i in range(1, s32(m.u32(G_COUNT)) + 1):
        e = ent(i)
        if m.s32(e + 0x68) == 0x15 and m.s32(e + 0x10) == 2:
            m.w32(e + 0x10, 3)
            m.w32(e + 0x20, 0)


def cancel_wind2(env):                                 # 0x80034EC4
    m = env.m
    for i in range(1, s32(m.u32(G_COUNT)) + 1):
        e = ent(i)
        if m.s32(e + 0x68) == 0x18 and m.s32(e + 0x10) == 2:
            m.w32(e + 0x10, 3)
            m.w32(e + 0x20, 0)
            for _ in range(16):
                s1 = m.u32(RNG)
                st1 = (s1 * 0x7D2B89DD + 0xE06A02E7) & M32
                st2 = (st1 * 0x7D2B89DD + 0xE06A02E7) & M32
                idx = (st1 * 0x20) >> 32
                zr = (st2 * 0x600001) >> 32
                m.w32(RNG, st2)
                fx = env.fx(0, 0x12, 0, m.u32(e + 0x114), m.u32(e + 0x118), (m.u32(e + 0x11C) + zr) & M32)
                if fx:
                    st3 = (st2 * 0x7D2B89DD + 0xE06A02E7) & M32
                    m.w32(fx + 0x54, (m.s16(TAB_X + 2 * idx) << 9))
                    m.w32(fx + 0x58, (m.s16(TAB_Y + 2 * idx) << 9))
                    m.w32(RNG, st3)
                    m.w32(fx + 0x5C, (((st3 * 0x10001) >> 32) - 0x8000))


CANCELS = {0x2C: cancel_earth2, 0x2D: cancel_water, 0x2E: cancel_water, 0x2F: cancel_fire1, 0x30: cancel_fire2, 0x32: cancel_wind2}


def slope_helper(env):                                 # 0x800351B8
    m = env.m
    if m.s32(HERO + 0x18C) == 4:
        cancel(env)
        return 1
    if ((m.s32(HERO + 0x88) - 0x32) & M32) < 2:
        return 0
    cancel(env)
    return 1


# ---- the heals used by the water tomes ---------------------------------------------------------------------------------------------------------------------------
def heal_water(env):                                   # 0x80032F84(hero, 0x29) : the second argument is overwritten before use
    m = env.m
    hpmax = m.s32(HERO + 0x18)
    add = int(hpmax / 2)                               # srl 31 trick: truncation toward zero
    if add < 5:
        add = 5
    hp = m.s32(HERO + 0x14) + add
    if hpmax < hp:
        hp = hpmax
    m.w32(HERO + 0x14, hp)
    for _ in range(4):
        fx = env.fx(0, 0xE, 0, m.u32(HERO + 0x114), m.u32(HERO + 0x118), (m.u32(HERO + 0x11C) + 0x100000) & M32)
        if fx:
            s0 = m.u32(RNG)
            st1 = (s0 * 0x7D2B89DD + 0xE06A02E7) & M32
            st2 = (st1 * 0x7D2B89DD + 0xE06A02E7) & M32
            m.w32(fx + 0x5C, 0x20000)
            m.w32(fx + 0x54, ((st1 * 0x20001) >> 32) - 0x10000)
            m.w32(RNG, st1)
            m.w32(RNG, st2)
            m.w32(fx + 0x58, (((st2 * 0x30001) >> 32) + 0xFFFE8000) & M32)


def heal_full(env):                                    # 0x80033274(hero, 0x25)
    m = env.m
    m.w32(HERO + 0x14, m.u32(HERO + 0x18))
    for k in range(8):
        fx = env.fx(0, 0xE, 0, m.u32(HERO + 0x114), m.u32(HERO + 0x118), (m.u32(HERO + 0x11C) + 0x80000) & M32)
        if fx:
            xi, yi = 4 * k, 4 * k                       # s0/s1 walk the tables 8 bytes at a time (every 4th entry)
            m.w32(fx + 0x3C, (m.u32(fx + 0x3C) + (m.s16(TAB_X + 8 * k) << 11)) & M32)
            m.w32(fx + 0x40, (m.u32(fx + 0x40) + (m.s16(TAB_Y + 8 * k) << 11)) & M32)
            idx = (8 + 4 * k) & 0x1F
            m.w32(fx + 0x5C, 0x30000)
            m.w32(fx + 0x54, (m.s16(TAB_X + 2 * idx) * 448))
            m.w32(fx + 0x58, (m.s16(TAB_Y + 2 * idx) * 448))


# ---- table walkers ----------------------------------------------------------------------------------------------------------------------------------------------------
def walk_table(m, base, esz, counter):
    """the loop shape of the earth/water/wind drivers: returns (entries whose tick == counter, address of the first entry with tick > counter, entered)"""
    a = base
    first = m.u16(a)
    if counter < first:
        return [], a, False
    hits = []
    while True:
        t = m.u16(a)
        if t == counter:
            hits.append(a)
        a += esz
        if counter < m.u16(a):
            break
    return hits, a, True


def drv_earth1(env, item):                             # 0x80035320
    m = env.m
    P = m.s32(PHASE)
    if P == 1:
        if slope_helper(env):
            return 1
        if m.s32(COUNTER) < 0x64:
            return 0
        m.w32(HERO + 0x88, 0x34)
        m.w32(PHASE, P + 1)
        return 0
    if P == 0:
        if slope_helper(env):
            return 1
        c = m.s32(COUNTER)
        hits, a, entered = walk_table(m, EARTH1, 6, c)
        for h in hits:
            e = env.spawn(HERO, 0, 0x10, m.u32(HERO + 0x114), m.u32(HERO + 0x118), m.u32(HERO + 0x11C), (m.s32(HERO + 0x8C) + m.s16(h + 4)) & 0x1F)
            if e:
                m.w32(e + 0xB8, (m.s16(h + 2) << 8))
        if m.u16(a) == 0xFFFF:
            m.w32(PHASE, m.s32(PHASE) + 1)
        return 0
    if P == 2:
        return 0 if m.s32(HERO + 0x88) == 0x34 else 1
    return 0


def drv_earth2(env, item):                             # 0x800354D0
    m = env.m
    P = m.s32(PHASE)
    if P == 1:
        if slope_helper(env):
            return 1
        if m.s32(COUNTER) < 0x118:
            return 0
        m.w32(HERO + 0x88, 0x34)
        m.w32(PHASE, P + 1)
        return 0
    if P == 0:
        if slope_helper(env):
            return 1
        c = m.s32(COUNTER)
        hits, a, entered = walk_table(m, EARTH2, 4, c)
        for h in hits:
            st1 = rng_step(m)                          # first step: r1 = hi(st1 * 32) picks the offset vector
            r1 = (st1 * 0x20) >> 32
            st2 = (st1 * 0x7D2B89DD + 0xE06A02E7) & M32  # second step: r2 = hi(st2 * 32) is the direction
            r2 = (st2 * 0x20) >> 32
            m.w32(RNG, st2)
            k = m.s16(h + 2) + 0xA00
            yoff = s32(m.s16(TAB_Y + 2 * r1) * k)
            xoff = s32(m.s16(TAB_X + 2 * r1) * k)
            z = (m.u32(HERO + 0x11C) + 0x1000000) & M32
            e = env.spawn(HERO, 0, 0x11, (m.u32(HERO + 0x114) + xoff) & M32, (m.u32(HERO + 0x118) + yoff) & M32, z, r2)
            if e:
                if s32(z) < m.s32(e + 0x138):
                    destroy_nofx(m, e)
                else:
                    m.w32(e + 0xB8, 0xFFF80000)
        if m.u16(a) == 0xFFFF:
            m.w32(PHASE, m.s32(PHASE) + 1)
        return 0
    if P == 2:
        return 0 if m.s32(HERO + 0x88) == 0x34 else 1
    return 0


def drv_water(env, item):                              # 0x80035790
    m = env.m
    P = m.s32(PHASE)
    if P == 1:
        return water_phase1(env, item)
    if P == 0:
        if slope_helper(env):
            return 1
        tbl = WATER_UP if item == 0x2D else WATER_LO
        c = m.s32(COUNTER)
        hits, a, entered = walk_table(m, tbl, 14, c)
        for h in hits:
            e = env.spawn(HERO, 0, 0x13, m.u32(HERO + 0x114), m.u32(HERO + 0x118), (m.u32(HERO + 0x11C) + 0x80000) & M32, 0)
            if e:
                m.w32(e + 0x70, 0xFE)
                m.w32(e + 0x274, m.s16(h + 2))
                m.w32(e + 0x278, m.s16(h + 4))
                m.w32(e + 0x27C, m.s16(h + 6))
                e8 = m.s16(h + 8)
                m.w32(e + 0x280, e8)
                m.w32(e + 0x284, 1)
                m.w32(e + 0x288, 0)
                v1 = s32(m.s16(h + 0xA) + s32(e8 * c))
                t = v1 if v1 >= 0 else v1 + 0xFF
                m.w32(e + 0x28C, v1 - ((t >> 8) << 8))
                m.w32(e + 0x290, m.s16(h + 0xC) - c)
        if m.u16(a) == 0xFFFF:
            m.w32(0x80127118, 0)
            m.w32(PHASE, m.s32(PHASE) + 1)
        return 0
    if P == 2:
        if m.s32(HERO + 0x88) == 0x34:
            return 0
        count = s32(m.u32(G_COUNT))
        if count <= 0:
            return 1
        for i in range(1, count + 1):
            e = ent(i)
            if m.s32(e + 0x68) == 0x13 and m.s32(e + 0x290) > 0:
                return 0
        return 1
    return 0


def water_phase1(env, item):
    m = env.m
    if slope_helper(env):
        return 1
    t = m.s32(0x80127118) + 1
    m.w32(0x80127118, t)
    if t == 0x1E:
        if item == 0x2D:
            heal_water(env)
            env.sound(0x31)
        else:
            heal_full(env)
            env.sound(0x30)
    if m.s32(0x80127118) < 0x3C:
        return 0
    m.w32(HERO + 0x88, 0x34)
    m.w32(PHASE, m.s32(PHASE) + 1)
    return 0


def drv_fire1(env, item):                              # 0x80035A84
    m = env.m
    P = m.s32(PHASE)
    if P == 0:
        if slope_helper(env):
            return 1
        if m.s32(HERO + 0x88) != 0x33:
            return 0
        e = env.spawn(HERO, 0, 0x14, m.u32(HERO + 0x114), m.u32(HERO + 0x118), m.u32(HERO + 0x11C), 0)
        m.w32(0x8012711C, e)
        if e == 0:
            return 1
        m.w32(0x80127120, m.u32(e + 4))
        m.w32(PHASE, m.s32(PHASE) + 1)
        return fire1_phase1(env)
    if P == 1:
        return fire1_phase1(env)
    if P == 2:
        return 0 if m.s32(HERO + 0x88) == 0x34 else 1
    return 0


def fire1_phase1(env):
    m = env.m
    if slope_helper(env):
        return 1
    ent_ptr = m.u32(0x8012711C)
    if m.u32(ent_ptr + 4) == m.u32(0x80127120):
        if (m.s32(COUNTER) & 7) != 0:
            return 0
        st = rng_step(m)
        m.w32(RNG, st)
        r = (st * 0x20) >> 32
        e = env.spawn(HERO, 0, 0x17, m.u32(HERO + 0x114), m.u32(HERO + 0x118), (m.u32(HERO + 0x11C) + 0x100000) & M32, r)
        if e:
            m.w32(e + 0xB8, 0x80000)
            m.w32(e + 0x274, 0x10)
            m.w32(e + 0x278, 0)
        return 0
    m.w32(HERO + 0x88, 0x34)
    m.w32(PHASE, m.s32(PHASE) + 1)
    return 0


def copy9(m, src, dst):
    for k in range(9):
        m.w32(dst + 4 * k, m.u32(src + 4 * k))


def drv_fire2(env, item):                              # 0x80035C64
    m = env.m
    P = m.s32(PHASE)
    if P == 0:
        if slope_helper(env):
            return 1
        if m.s32(HERO + 0x88) != 0x33:
            return 0
        e = env.spawn(HERO, 0, 0x15, m.u32(HERO + 0x114), m.u32(HERO + 0x118), m.u32(HERO + 0x11C), 0)
        m.w32(0x80127124, e)
        if e == 0:
            return 1
        copy9(m, SHAKE, SHAKE_BAK)
        m.w32(PHASE, m.s32(PHASE) + 1)
        m.w32(0x80127128, m.u32(m.u32(0x80127124) + 4))
        return fire2_phase1(env)
    if P == 1:
        return fire2_phase1(env)
    if P == 2:
        return 0 if m.s32(HERO + 0x88) == 0x34 else 1
    return 0


def fire2_phase1(env):
    m = env.m
    if slope_helper(env):
        copy9(m, SHAKE_BAK, SHAKE)
        return 1
    if m.u32(m.u32(0x80127124) + 4) == m.u32(0x80127128):
        m.w32(SHAKE, 1)
        c = m.s32(COUNTER)
        m.w32(SHAKE + 8, c >> 4)
        m.w32(SHAKE + 0x10, c >> 4)
        m.w32(SHAKE + 4, c >> 6)
        m.w32(SHAKE + 0xC, c >> 6)
        return 0
    copy9(m, SHAKE_BAK, SHAKE)
    m.w32(HERO + 0x88, 0x34)
    m.w32(PHASE, m.s32(PHASE) + 1)
    return 0


def drv_wind1(env, item):                              # 0x80035EB0
    m = env.m
    P = m.s32(PHASE)
    if P == 0:
        m.w32(0x8012712C, 0)
        m.w32(PHASE, 1)
        return wind1_phase1(env)
    if P == 1:
        return wind1_phase1(env)
    if P == 2:
        return 0 if m.s32(HERO + 0x88) == 0x34 else 1
    return 0


def wind1_phase1(env):
    m = env.m
    if slope_helper(env):
        return 1
    if m.s32(HERO + 0x88) != 0x33:
        return 0
    c = m.s32(COUNTER)
    s3 = 0
    if not (c < m.u16(WIND1)):
        a = WIND1
        while True:
            if m.u16(a) == c:
                q = query(m, HERO, 1, 0x0B)
                sel = 0
                for (e, d) in q:
                    if s32((m.u32(HERO + 0x11C) + 0x500000) & M32) < m.s32(e + 0x11C):
                        continue
                    if e == m.u32(0x8012712C):
                        continue
                    if d >= 0x6401:                    # signed slti: distances are < 2^31 here
                        break
                    sel = e
                    break
                if sel:
                    sp = env.spawn(HERO, 0, 0x16, m.u32(sel + 0x114), m.u32(sel + 0x118), (m.u32(sel + 0x11C) + 0x800000) & M32, 0)
                    if sp:
                        m.w32(sp + 0xB8, 0xFFF80000)
                    m.w32(0x8012712C, sel)
                else:
                    st1 = rng_step(m)
                    st2 = (st1 * 0x7D2B89DD + 0xE06A02E7) & M32
                    rx = (st1 * 0xC00001) >> 32
                    ry = (st2 * 0x800001) >> 32
                    m.w32(RNG, st2)
                    x = (m.u32(HERO + 0x114) + rx + 0xFFA00000) & M32
                    y = (m.u32(HERO + 0x118) + ry + 0xFFC00000) & M32
                    z = (m.u32(HERO + 0x11C) + 0x800000) & M32
                    sp = env.spawn(HERO, 0, 0x16, x, y, z, 0)
                    if sp:
                        if m.s32(sp + 0x144) != 0:
                            destroy_nofx(m, sp)
                        else:
                            m.w32(sp + 0xB8, 0xFFF80000)
                    m.w32(0x8012712C, 0)
            a += 2
            s3 += 1
            if c < m.u16(a):
                break
    if m.u16(WIND1 + 2 * s3) != 0xFFFF:
        return 0
    m.w32(HERO + 0x88, 0x34)
    m.w32(PHASE, m.s32(PHASE) + 1)
    return 0


def drv_wind2(env, item):                              # 0x80036218
    m = env.m
    P = m.s32(PHASE)
    if P == 0:
        if slope_helper(env):
            return 1
        if m.s32(HERO + 0x88) != 0x33:
            return 0
        e = env.spawn(HERO, 0, 0x18, m.u32(HERO + 0x114), m.u32(HERO + 0x118), m.u32(HERO + 0x11C), 0)
        m.w32(0x80127130, e)
        if e == 0:
            return 1
        m.w32(0x80127134, m.u32(e + 4))
        m.w32(PHASE, m.s32(PHASE) + 1)
        return 0
    if P == 1:
        if slope_helper(env):
            return 1
        if m.u32(m.u32(0x80127130) + 4) == m.u32(0x80127134):
            return 0
        m.w32(HERO + 0x88, 0x34)
        m.w32(PHASE, m.s32(PHASE) + 1)
        return 0
    if P == 2:
        return 0 if m.s32(HERO + 0x88) == 0x34 else 1
    return 0


DRIVERS = {0x2B: drv_earth1, 0x2C: drv_earth2, 0x2D: drv_water, 0x2E: drv_water, 0x2F: drv_fire1, 0x30: drv_fire2, 0x31: drv_wind1, 0x32: drv_wind2}


def wrapper(env):                                      # 0x8003634C
    m = env.m
    item = m.s32(INUSE)
    if item == 0:
        return 1
    idx = (item - 0x1F) & M32
    if idx < 0x14:
        if item in DRIVERS:
            res = DRIVERS[item](env, item)
        elif item in (0x1F, 0x20, 0x23):
            raise NotImplementedError('beans / cape / bomb driver not modelled here')
        else:
            res = 1
    else:
        res = 1
    c = m.s32(COUNTER)
    if not (0x7FFFFFFE < c):
        m.w32(COUNTER, c + 1)
    if res != 0:
        m.w32(INUSE, 0)
    return res
