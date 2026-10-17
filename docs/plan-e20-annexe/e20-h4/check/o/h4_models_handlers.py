"""h4_models_handlers.py - Python transcriptions of the native sprite handlers of the tome entities: E3 (0x8007EDA0), E13 (0x8007F30C), E28 (0x8007FB58), E1 (0x8007ED10),
C63 (0x8007AC60), C65 (0x8007AF20), C75 (0x8007BD8C), written from lst/*.txt.  Same memory-image conventions as h4_models.py."""
from h4_models import *  # noqa: F401,F403
import h4_models as M

SIN256 = 0x800204F0          # s16[256]: 256 * sin(angle) (index 0..255), "B" table
COS256 = 0x800206F0          # s16[256]: 256 * cos(angle), "A" table
PAD_HOLD = 0x80126F28
OUT_ENTS, OUT_DISTS = 0x800CC058, 0x800CC158


def destroy(env, e, fx):
    env.j.append(('destroy', e & M32, fx & M32))


def e1(env, s):                                        # 0x8007ED10 : DestroyEntity(self, -1)
    destroy(env, s, -1)


def e3(env, s):                                        # 0x8007EDA0
    m = env.m
    if m.s32(s + 0x88) == 1 and m.s32(s + 0xAC) == 1:
        destroy(env, s, -1)
        return
    flags = (m.u32(s + 0x6C) | 0x40) & 0xFFFFFFCF
    m.w32(s + 0x88, 1)
    m.w32(s + 0x6C, flags)


def e13(env, s):                                       # 0x8007F30C : like E3 + the camera kick word 0x800DD874 = 2 on the first call
    m = env.m
    if m.s32(s + 0x88) == 1:                           # unlike E3, a TargetAnim of 1 with the animation still running ends the call (0x8007F330 bne -> 0x8007F368)
        if m.s32(s + 0xAC) == 1:
            destroy(env, s, -1)
        return
    m.w32(0x800DD874, 2)
    flags = (m.u32(s + 0x6C) | 0x40) & 0xFFFFFFCF
    m.w32(s + 0x88, 1)
    m.w32(s + 0x6C, flags)


def e28(env, s):                                       # 0x8007FB58 : eight fire bursts around the entity, then DestroyEntity(self, -1)
    m = env.m
    for k in range(8):
        st = rng_step(m)
        m.w32(RNG, st)
        r = (st * 0x1801) >> 32
        off = r + 0x320
        yoff = s32(m.s16(TAB_Y + 8 * k) * off)
        xoff = s32(m.s16(TAB_X + 8 * k) * off)
        env.fx(0, 0x13, 2, (m.u32(s + 0x114) + xoff) & M32, (m.u32(s + 0x118) + yoff) & M32, m.u32(s + 0x11C))
    destroy(env, s, -1)


def turn_toward(m, s, d):
    cur = m.s32(s + 0x8C)
    if d < cur:
        if cur - d < 4:
            m.w32(s + 0x8C, d)
        else:
            m.w32(s + 0x8C, cur - 4)
    else:
        if d - cur < 4:
            m.w32(s + 0x8C, d)
        else:
            m.w32(s + 0x8C, cur + 4)


def c63(env, s):                                       # 0x8007AC60 : the fire projectile of the level 1 tome (sprite 23)
    m = env.m
    b0 = m.s32(s + 0x274)
    if b0 != 0:
        m.w32(s + 0x274, b0 - 1)
        return
    tgt = m.u32(s + 0x278)
    m.w32(s + 0xB8, 0xFFFD0000)
    if tgt == 0:
        atype = m.u8(m.u32(s + 0x1C8)) & 0xF
        q = query(m, s, 1, atype)
        if q:
            t = q[0][0]
            m.w32(s + 0x278, t)
            m.w32(s + 0x27C, m.u32(t + 4))
        else:
            r = rng_draw(m, 9)
            m.w32(s + 0x274, r)
            return
    else:
        t = tgt
        if not (m.s32(t + 0x10) == 2 and m.u32(t + 4) == m.u32(s + 0x27C)):
            m.w32(s + 0x278, 0)
            return
    d = env.gdt(s32(m.u32(t + 0x114) - m.u32(s + 0x114)), s32(m.u32(t + 0x118) - m.u32(s + 0x118)))
    if m.s32(s + 0x8C) == d:
        return
    turn_toward(m, s, d)
    m.w32(s + 0x274, 2)


def c65(env, s):                                       # 0x8007AF20 : the vortex (sprite 24): pulls the first target of the screen window
    m = env.m
    atype = m.u8(m.u32(s + 0x1C8)) & 0xF
    q = query(m, s, 1, atype)
    for (t, dist) in q:
        dx = s32(m.u32(t + 0x114) - m.u32(s + 0x114))
        dy = s32(m.u32(t + 0x118) - m.u32(s + 0x118))
        if (-dx if dx < 0 else dx) > 0x1400000:
            continue
        if (-dy if dy < 0 else dy) > 0xF00000:
            continue
        d = env.gdt(dx, dy)
        idx = (d + 0x14) & 0x1F
        m.w32(t + 0xCC, (m.s16(COS256 + 16 * idx) * 3) << 8)
        m.w32(t + 0xD0, (m.s16(SIN256 + 16 * idx)) << 9)
        return


def c75(env, s):                                       # 0x8007BD8C : a water orb (sprite 19)
    m = env.m
    a0 = s + 0x274
    if (m.u16(PAD_HOLD) & 0x20) == 0:
        m.w32(a0 + 0x1C, m.s32(a0 + 0x1C) - 1)
    life = m.s32(a0 + 0x1C)
    if life > 0:
        mode = m.s32(a0 + 0x10)
        r = m.s32(a0 + 0x14)
        p0 = m.s32(a0)
        a1 = m.s32(a0 + 4 * mode)
        v1 = (1 if r < p0 else 0) << 6
        if a1 < r:
            r = r - m.s32(a0 + 8)
            cond = a1 < r
        else:
            r = r + m.s32(a0 + 8) + v1
            cond = r < a1
        if not cond:
            r = a1
            m.w32(a0 + 0x10, mode ^ 1)
        m.w32(a0 + 0x14, r)
        ang = (m.s32(a0 + 0x18) + m.s32(a0 + 0xC)) & 0xFF
        m.w32(a0 + 0x18, ang)
        sx = m.s16(SIN256 + 2 * ang) * r
        cy = m.s16(COS256 + 2 * ang) * r
        px = s32(m.u32(HERO + 0x114) + s32(3 * s32(sx)) + m.u32(HERO + 0xC4))
        py = s32(m.u32(HERO + 0x118) + s32(2 * s32(cy)) + m.u32(HERO + 0xC8))
        m.w32(s + 0xCC, px - m.s32(s + 0x114))
        m.w32(s + 0xD0, py - m.s32(s + 0x118))
        m.w32(s + 0xB8, s32(m.u32(HERO + 0x11C) - m.u32(s + 0x11C)) + 0x80000)
        return
    if life == 0:
        atype = m.u8(m.u32(s + 0x1C8)) & 0xF
        q = query(m, s, 1, atype)
        if q and s32(q[0][1]) < 0x3840:
            t = q[0][0]
            d = env.gdt(s32(m.u32(t + 0x114) - m.u32(s + 0x114)), s32(m.u32(t + 0x118) - m.u32(s + 0x118)))
        else:
            d = rng_draw(m, 0x20)
        m.w32(s + 0x274, m.s16(TAB_X + 2 * d) << 8)
        m.w32(s + 0x278, m.s16(TAB_Y + 2 * d) << 8)
        m.w32(s + 0x6C, m.u32(s + 0x6C) | 0x2130)
        m.w32(s + 0xB8, 0xA0000)
    m.w32(s + 0xCC, m.u32(s + 0x274))
    m.w32(s + 0xD0, m.u32(s + 0x278))


HANDLERS = {'E1': (0x8007ED10, e1), 'E3': (0x8007EDA0, e3), 'E13': (0x8007F30C, e13), 'E28': (0x8007FB58, e28), 'C63': (0x8007AC60, c63), 'C65': (0x8007AF20, c65),
            'C75': (0x8007BD8C, c75)}
