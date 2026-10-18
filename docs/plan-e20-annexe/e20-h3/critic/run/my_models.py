"""my_models.py - critic's OWN models, written from my own reading of the disassembly of ALUN_CD.EXE (France).  Nothing imported from the h3bin agent.
Each model: f(M, J, ctx, args) -> return value; M = Mem overlay, J = journal list, ctx = Rig (spawn_ret, eff_ret).
Field map (entity E): +0x10 status, +0x24 drop mode, +0x28 carrier, +0x2C (hero) carried link, +0x68 sprite kind, +0x6C flags, +0x88 target anim,
+0x8C target dir, +0xAC anim-ended, +0xB8 ForceZ, +0x12C support, +0x13C forceAdjusted, +0x144 onGround, +0x148.. 4 cell pointers, +0x184 vram flags,
+0x18C slope, +0x1C8 attack ptr, +0x220 hitCounter, +0x224 touching, +0x274 fuse / 0x278 / ..."""
from machine import M32, s32

H = 0x80127D30
RNG = 0x80098708
ITEM = 0x80127164
EFFSLOT = 0x801E8000


def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32


def sound(J, n):
    J.append(('sound', n))


def effect(M, J, ctx, a1, x, y, z):
    J.append(('effect', (0, a1, 0, x & M32, y & M32, z & M32)))
    if ctx.eff_ret:                      # the harness stub hands back a freshly zeroed slot (not a property of the game code)
        for i in range(0, 0x300, 4):
            M.ww(EFFSLOT + i, 0)
    return ctx.eff_ret


def start(M, J, ctx, kind):
    """kind 1 = beans (0x80034224), 2 = bomb (0x8003453C)"""
    if M.rw(H + 0x2C) != 0 or M.rws(H + 0x18C) == 4 or M.rw(ITEM) != 0:
        sound(J, 3)
        return 1
    if kind == 2:
        n = 0
        for i in range(64):
            if M.rw(H + 0x294 * i + 0x68) == 2:
                n += 1
        if n >= 3:                        # slti n,3 false
            sound(J, 3)
            return 1
    J.append(('spawn', (H, 0, kind, M.rw(H + 0x114), M.rw(H + 0x118), M.rw(H + 0x11C), 0)))
    o = ctx.spawn_ret
    if o == 0:
        return 1
    if kind == 2:
        M.ww(o + 0x274, 0xF0)
    M.ww(H + 0x2C, o)
    M.ww(o + 0x28, H)
    M.ww(H + 0x88, 5 if M.rw(H + 0x144) != 0 else 0xB)
    M.ww(H + 0x30, M.rw(o + 0x114) - M.rw(H + 0x114))
    M.ww(H + 0x34, M.rw(o + 0x118) - M.rw(H + 0x118))
    M.ww(H + 0x38, M.rw(o + 0x11C) - M.rw(H + 0x11C))
    return 0


def cancel(M, J, ctx, kind):
    c = M.rw(H + 0x2C)
    if c == 0:
        return
    if M.rw(c + 0x68) != kind:
        return
    if M.rw(c + 0x24) != 0:
        return
    M.ww(H + 0x2C, 0)
    fx = 8 if kind == 1 else 12
    effect(M, J, ctx, fx, M.rw(c + 0x114), M.rw(c + 0x118), M.rw(c + 0x11C))
    J.append(('destroy1', c))


def dispatch(M, J, ctx):
    it = M.rw(ITEM)
    if it == 0:
        return
    if (it - 0x1F) & M32 < 0x14:
        if it == 0x1F:
            cancel(M, J, ctx, 1)
        elif it == 0x23:
            cancel(M, J, ctx, 2)
        # others: default clear (items outside the tests)
    M.ww(ITEM, 0)


def driver(M, J, ctx, kind):
    c = M.rw(H + 0x2C)
    if c == 0:
        return 1
    if M.rw(c + 0x68) != kind:
        return 1
    if M.rws(H + 0x18C) != 4:
        return 0
    dispatch(M, J, ctx)
    return 1


def C62(M, J, ctx, E):
    if M.rw(E + 0x184) & 4:
        J.append(('destroy1', E))
        return
    v = M.rws(E + 0x274)
    M.ww(E + 0x274, v - 1)
    if v == 0:
        M.ww(E + 0x10, 3)
        c = M.rw(E + 0x28)
        if c != 0:
            M.ww(c + 0x2C, 0)
            M.ww(E + 0x28, 0)
    else:
        if v < 60 and (v & 7) == 0:
            eff = effect(M, J, ctx, 9, M.rw(E + 0x114), M.rw(E + 0x118), M.rw(E + 0x11C) + 0x80000)
            if eff != 0:
                s0 = M.rw(RNG)
                s1 = lcg(s0)
                s2 = lcg(s1)
                M.ww(RNG, s2)
                M.ww(eff + 0x54, ((s1 * 0x30001) >> 32) + 0xFFFE8000)
                M.ww(eff + 0x58, ((s2 * 0x20001) >> 32) + 0xFFFF0000)
        if M.rw(E + 0x28) != 0:
            if M.rw(E + 0x24) == 0:
                M.ww(E + 0x88, 0)
                return
            M.ww(E + 0x88, 2)
            c = M.rw(E + 0x28)
            M.ww(E + 0x24, 0)
            if c != 0:
                M.ww(c + 0x2C, 0)
            M.ww(E + 0x28, 0)
            return
    # free branch (reached after v == 0 with the carrier link cleared, or v != 0 with no carrier)
    if M.rw(E + 0x88) == 0:
        return
    if M.rw(E + 0x13C) != 0:
        if M.rw(E + 0x144) != 0:
            for i in range(4):
                p = M.rw(E + 0x148 + 4 * i)
                if (M.rh(p) & 0x1001) == 0x1001:
                    M.ww(E + 0x10, 3)
            M.ww(E + 0x88, 0)
            return
        if M.rws(E + 0xB8) <= 0:
            return
        if M.rw(E + 0x278) != 0:
            return
        M.ww(E + 0x278, 1)
        M.ww(E + 0x8C, (M.rw(E + 0x8C) + 0x10) & 0x1F)
        effect(M, J, ctx, 9, M.rw(E + 0x114), M.rw(E + 0x118), M.rw(E + 0x11C) + 0x80000)
        return
    if M.rw(E + 0x144) == 0 and M.rw(E + 0x220) == 0:
        return
    sound(J, 0x18)
    M.ww(E + 0x278, 0)
    found = False
    for i in range(4):
        p = M.rw(E + 0x148 + 4 * i)
        if (M.rh(p) & 0x1001) == 0x1001:
            found = True
            break
    if not found:
        M.ww(E + 0x88, 0)


def D30(M, J, ctx, E):
    t = M.rw(E + 0x224)
    atk = M.rw(t + 0x1C8)
    if (M.rb(atk) & 0xF) in (4, 6, 10):
        M.ww(E + 0x10, 3)


def E12(M, J, ctx, E):
    if M.rw(E + 0x88) == 1 and M.rw(E + 0xAC) == 1:
        J.append(('destroy2', E, M32))
        return
    if M.rw(E + 0x184) & 4:
        J.append(('destroy2', E, M32))
        return
    J.append(('cate', E))
    M.ww(E + 0x88, 1)
    M.ww(E + 0x6C, (M.rw(E + 0x6C) | 0x40) & ~0x30 & M32)


def C73(M, J, ctx, E):
    c = M.rw(E + 0x28)
    if c != 0:
        if M.rw(E + 0x24) == 0:
            M.ww(E + 0x88, 0)
        else:
            M.ww(E + 0x88, 2)
            M.ww(E + 0x24, 0)
            c2 = M.rw(E + 0x28)
            if c2 != 0:
                M.ww(c2 + 0x2C, 0)
            M.ww(E + 0x6C, M.rw(E + 0x6C) | 0x10)
            M.ww(E + 0x28, 0)
    else:
        if M.rw(E + 0x144) != 0:
            M.ww(E + 0x10, 3)


def E21(M, J, ctx, E):
    S = M.rw(E + 0x12C)
    if S != 0 and M.rw(S + 0x68) == 0x199 and M.rw(S + 0x88) == 0:
        M.ww(S + 0x88, 1)
        J.append(('destroy1', E))
    if M.rw(E + 0x88) == 1 and M.rw(E + 0xAC) == 1:
        J.append(('destroy2', E, M32))
    else:
        M.ww(E + 0x88, 1)
        M.ww(E + 0x6C, (M.rw(E + 0x6C) | 0x40) & ~0x30 & M32)
