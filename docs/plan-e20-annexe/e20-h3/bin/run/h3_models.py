"""h3_models.py - Python transcriptions of the BEANS (item 0x1F) and BOMB (item 0x23) code of ALUN_CD.EXE (France), written from the disassembly
(lst/ in this folder: seed_start_80034224.txt, bomb_start_8003453C.txt, drivers, cancels, c62.txt, d30.txt, e12.txt, c73.txt, e21.txt), compared with the REAL code on random
vectors by h3_vec.py.  Same conventions as h4_models.py (memory image, stubs reached through Env).

Functions (every address is a virtual address of the binary):
  start_beans   0x80034224   start_bomb   0x8003453C
  drv_beans     0x80035204   drv_bomb     0x800352C4   (small item drivers, called by the wrapper 0x8003634C)
  cancel_beans  0x80034ACC   cancel_bomb  0x80034B54   (called by the dispatcher 0x800350C0 through the table 0x800233EC)
  c62 0x8007A978 (bomb tick), d30 0x8007E79C (bomb touch), e12 0x8007F27C (bomb deactivate = explosion)
  c73 0x8007BB30 (beans tick), e21 0x8007F7CC (beans deactivate)
"""
from h4_models import *  # noqa: F401,F403
import h4_models as M

HERO = M.HERO
STRIDE = M.STRIDE
RNG = M.RNG


class Env3(M.Env):
    """Env of h4 plus the two stubbed calls of the bomb/beans code: DestroyEntity(e, fx) 0x8003A59C and CheckAndTriggerTileEffect 0x8003A7B0."""

    def destroy(self, e, fx):
        self.j.append(('destroy', e & M32, fx & M32))

    def cate(self, e):
        self.j.append(('cate', e & M32))


def hero_slope(m):
    return m.s32(HERO + 0x18C)


# ---- 0x80034224 : start of the beans --------------------------------------------------------------------------------------------------------------------------
def _link(env, h, e):
    """0x80034298-0x800342F8 (beans) / 0x800345F8-0x80034658 (bomb): hero.+0x2C = e, e.+0x28 = hero, hero anim 5 (on the ground) or 0xB (in the air), offsets e.pos - hero.pos"""
    m = env.m
    m.w32(h + 0x2C, e)
    m.w32(e + 0x28, h)
    m.w32(h + 0x88, 5 if m.u32(h + 0x144) != 0 else 0xB)
    m.w32(h + 0x30, (m.u32(e + 0x114) - m.u32(h + 0x114)) & M32)
    m.w32(h + 0x34, (m.u32(e + 0x118) - m.u32(h + 0x118)) & M32)
    m.w32(h + 0x38, (m.u32(e + 0x11C) - m.u32(h + 0x11C)) & M32)


def start_beans(env):
    m = env.m
    h = HERO
    if m.u32(h + 0x2C) != 0 or m.s32(h + 0x18C) == 4 or m.u32(INUSE) != 0:   # 0x80034238-0x80034260
        env.sound(3)
        return 1
    e = env.spawn(h, 0, 1, m.u32(h + 0x114), m.u32(h + 0x118), m.u32(h + 0x11C), 0)     # 0x80034264-0x80034288
    if e == 0:                                                                  # 0x80034290 : no sound
        return 1
    _link(env, h, e)
    return 0


# ---- 0x8003453C : start of the bomb ---------------------------------------------------------------------------------------------------------------------------
def start_bomb(env):
    m = env.m
    h = HERO
    if m.u32(h + 0x2C) != 0 or m.s32(h + 0x18C) == 4 or m.u32(INUSE) != 0:   # 0x80034550-0x80034578
        env.sound(3)
        return 1
    n = 0
    for i in range(0x40):                                                       # 0x80034580-0x800345A8 : 64 slots from the hero, +0x68 == 2, whatever the status
        if m.s32(ent(i) + 0x68) == 2:
            n += 1
    if n >= 3:                                                                  # 0x800345AC : slti 3
        env.sound(3)
        return 1
    e = env.spawn(h, 0, 2, m.u32(h + 0x114), m.u32(h + 0x118), m.u32(h + 0x11C), 0)
    if e == 0:
        return 1
    m.w32(e + 0x274, 0xF0)                                                      # 0x800345F0-0x800345F4
    _link(env, h, e)
    return 0


# ---- the small drivers 0x80035204 (beans) / 0x800352C4 (bomb) --------------------------------------------------------------------------------------------------
def _small_driver(env, typ):
    m = env.m
    c = m.u32(HERO + 0x2C)
    if c == 0:
        return 1
    if m.s32(c + 0x68) != typ:
        return 1
    if m.s32(HERO + 0x18C) != 4:
        return 0
    M.cancel(env)                                                               # 0x800350C0
    return 1


def drv_beans(env):
    return _small_driver(env, 1)


def drv_bomb(env):
    return _small_driver(env, 2)


# ---- the cancels 0x80034ACC (beans) and 0x80034B54 (bomb) ------------------------------------------------------------------------------------------------------
def _cancel_obj(env, typ, fxid):
    m = env.m
    c = m.u32(HERO + 0x2C)
    if c == 0:
        return
    if m.s32(c + 0x68) != typ:
        return
    if m.s32(c + 0x24) != 0:
        return
    m.w32(HERO + 0x2C, 0)                                                       # the carried entity's own +0x28 is NOT cleared here (the 1-argument destroy does not clear +0x28 either)
    env.fx(0, fxid, 0, m.u32(c + 0x114), m.u32(c + 0x118), m.u32(c + 0x11C))
    destroy_nofx(m, c)                                                          # 0x8003A774


def cancel_beans(env):
    _cancel_obj(env, 1, 8)


def cancel_bomb(env):
    _cancel_obj(env, 2, 12)


M.CANCELS[0x1F] = cancel_beans
M.CANCELS[0x23] = cancel_bomb


# ---- C62 0x8007A978 : the bomb ----------------------------------------------------------------------------------------------------------------------------------
def _corner_pit(m, s):
    """loop of 0x8007AB94 / 0x8007AC10 : the four corner-cell pointers at +0x148 + 4*i ; the u16 under each ; (u16 & 0x1001) == 0x1001"""
    hits = []
    for i in range(4):
        p = m.u32(s + 0x148 + 4 * i)
        hits.append((m.u16(p) & 0x1001) == 0x1001)
    return hits


def c62(env, s):
    m = env.m
    if m.u32(s + 0x184) & 4:                                                    # 0x8007A988-0x8007A994
        destroy_nofx(m, s)                                                      # 0x8003A774
        return
    v = m.s32(s + 0x274)
    m.w32(s + 0x274, v - 1)                                                     # always written (delay slot of 0x8007A9BC)
    if v == 0:                                                                  # fuse over
        carrier = m.u32(s + 0x28)
        m.w32(s + 0x10, 3)                                                      # Status 3 : only the E program runs from now on
        if carrier != 0:
            m.w32(carrier + 0x2C, 0)
            m.w32(s + 0x28, 0)
        return _free(env, s)
    if v < 0x3C and (v & 7) == 0:                                               # a spark every 8 ticks during the last 60 (v = 56, 48 ... 8)
        fxe = env.fx(0, 9, 0, m.u32(s + 0x114), m.u32(s + 0x118), (m.u32(s + 0x11C) + 0x80000) & M32)
        if fxe != 0:
            st1 = rng_step(m)
            st2 = (st1 * 0x7D2B89DD + 0xE06A02E7) & M32
            m.w32(RNG, st2)
            r1 = (st1 * 0x30001) >> 32
            r2 = (st2 * 0x20001) >> 32
            m.w32(fxe + 0x54, (r1 + 0xFFFE8000) & M32)
            m.w32(fxe + 0x58, (r2 + 0xFFFF0000) & M32)
    carrier = m.u32(s + 0x28)
    if carrier == 0:
        return _free(env, s)
    if m.s32(s + 0x24) == 0:                                                    # carried, not released : idle animation
        m.w32(s + 0x88, 0)
        return
    m.w32(s + 0x88, 2)                                                          # released (thrown, dropped, hurt, ...) : the flight animation
    m.w32(s + 0x24, 0)
    carrier = m.u32(s + 0x28)
    if carrier != 0:
        m.w32(carrier + 0x2C, 0)
    m.w32(s + 0x28, 0)


def _free(env, s):
    """L_8007AAFC : the bomb is not carried"""
    m = env.m
    if m.s32(s + 0x88) == 0:
        return
    if m.s32(s + 0x13C) != 0:                                                   # ForceAdjusted (blocked)
        if m.s32(s + 0x144) == 0:                                               # in the air, blocked
            if m.s32(s + 0xB8) <= 0:
                return
            if m.s32(s + 0x278) != 0:
                return
            m.w32(s + 0x278, 1)
            m.w32(s + 0x8C, (m.s32(s + 0x8C) + 0x10) & 0x1F)                    # half turn : the bomb bounces back off a wall
            env.fx(0, 9, 0, m.u32(s + 0x114), m.u32(s + 0x118), (m.u32(s + 0x11C) + 0x80000) & M32)
            return
        for i, hit in enumerate(_corner_pit(m, s)):                             # on the ground, blocked : a corner over a pit -> Status 3
            if hit:
                m.w32(s + 0x10, 3)
        m.w32(s + 0x88, 0)
        return
    if m.s32(s + 0x144) == 0 and m.s32(s + 0x220) == 0:                         # still flying, nothing hit
        return
    env.sound(0x18)                                                             # landed (or hit something)
    m.w32(s + 0x278, 0)
    if any(_corner_pit(m, s)):                                                  # a corner over a pit : stays in flight animation
        return
    m.w32(s + 0x88, 0)


# ---- D30 0x8007E79C : the bomb touched --------------------------------------------------------------------------------------------------------------------------
def d30(env, s):
    m = env.m
    t = m.u32(s + 0x224)
    atk = m.u32(t + 0x1C8)
    n = m.u8(atk) & 0xF
    if n in (4, 6, 10):
        m.w32(s + 0x10, 3)


# ---- E12 0x8007F27C : the bomb explodes ---------------------------------------------------------------------------------------------------------------------------
def e12(env, s):
    m = env.m
    if m.s32(s + 0x88) == 1 and m.s32(s + 0xAC) == 1:
        env.destroy(s, -1)
        return
    if m.u32(s + 0x184) & 4:
        env.destroy(s, -1)
        return
    env.cate(s)                                                                 # 0x8003A7B0 : the grass cut, on the box stored at the end of the previous image
    m.w32(s + 0x88, 1)
    m.w32(s + 0x6C, (m.u32(s + 0x6C) | 0x40) & 0xFFFFFFCF)


# ---- C73 0x8007BB30 : the beans ---------------------------------------------------------------------------------------------------------------------------------
def c73(env, s):
    m = env.m
    carrier = m.u32(s + 0x28)
    if carrier != 0:
        if m.s32(s + 0x24) == 0:
            m.w32(s + 0x88, 0)
            return
        m.w32(s + 0x88, 2)
        carrier = m.u32(s + 0x28)
        m.w32(s + 0x24, 0)
        if carrier != 0:
            m.w32(carrier + 0x2C, 0)
        m.w32(s + 0x6C, m.u32(s + 0x6C) | 0x10)
        m.w32(s + 0x28, 0)
        return
    if m.s32(s + 0x144) != 0:
        m.w32(s + 0x10, 3)


# ---- E21 0x8007F7CC : the beans land ----------------------------------------------------------------------------------------------------------------------------
def e21(env, s):
    m = env.m
    sup = m.u32(s + 0x12C)
    if sup != 0 and m.s32(sup + 0x68) == 0x199 and m.s32(sup + 0x88) == 0:     # standing on a 'Pot magique' (map sprite 153) at rest : wake it up, the bean vanishes
        m.w32(sup + 0x88, 1)
        destroy_nofx(m, s)
    if m.s32(s + 0x88) == 1 and m.s32(s + 0xAC) == 1:
        env.destroy(s, -1)
        return
    m.w32(s + 0x88, 1)
    m.w32(s + 0x6C, (m.u32(s + 0x6C) | 0x40) & 0xFFFFFFCF)


HANDLERS = {
    'C62': (0x8007A978, c62), 'D30': (0x8007E79C, d30), 'E12': (0x8007F27C, e12), 'C73': (0x8007BB30, c73), 'E21': (0x8007F7CC, e21),
}
STARTS = {'start_beans': (0x80034224, start_beans), 'start_bomb': (0x8003453C, start_bomb)}
DRIVERS = {'drv_beans': (0x80035204, drv_beans), 'drv_bomb': (0x800352C4, drv_bomb)}
CANCEL_FN = {'cancel_beans': (0x80034ACC, cancel_beans), 'cancel_bomb': (0x80034B54, cancel_bomb)}
