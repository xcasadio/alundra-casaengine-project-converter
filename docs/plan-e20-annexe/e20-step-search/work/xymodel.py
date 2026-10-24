"""xymodel.py - hand model (own code, written from the disassembly of ALUN_CD.EXE (France), NOT from the C# decompilation) of
ComputeXYPosition 0x80037730 and the helpers it calls:
  0x80036F34 FindEntityCollisionCandidate   -> find_candidate
  0x800370C4 ComputeEntityGroundHeight      -> ground_height   (writes the 4 cell pointers +0x148.. and the 4 corner heights +0x158..)
  0x800373E4 wall test of a non-hero entity -> wall_other
  0x80037488 wall test of the hero          -> wall_hero       (reads ALL its data from the hero entity 0x80127D30, whatever a0 is)
The model works on plain dicts {byte offset: signed 32-bit word} per entity, so a full entity block can be diffed against the emulated memory.

Variable names follow the registers/stack slots of the binary so that each line can be checked against the listing:
  s1/s2  = FinalForceY/X working copies (halved)       s4 = result of the last FindEntityCollisionCandidate that was stored (may be STALE)
  s5     = attempt counter                             sp34 = the return value: s4 as it was at the TOP of the last attempt
  sp2c   = "a sub-step was accepted" flag              sp28 = "the corner slide was already applied" flag
  sp24   = (TargetDirection & 7) == 0
"""
M32 = 0xFFFFFFFF
HERO = 0x80127D30


def s32(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


def sra(v, n):
    return s32(v) >> n


def mod24(x):
    """x % 24 as the binary computes it (0x80037284..0x800372A4): q = (mulhi(x, 0x2AAAAAAB) >> 2) - (x >> 31); r = x - 24 q"""
    hi = (s32(x) * 0x2AAAAAAB) >> 32
    q = (hi >> 2) - (s32(x) >> 31)
    return s32(x) - q * 24


def mod16(y):
    """0x80037244..0x80037258: y - (((y + (15 if y < 0)) >> 4) << 4)"""
    y = s32(y)
    t = y if y >= 0 else y + 15
    return y - ((t >> 4) << 4)


class World:
    def __init__(self):
        self.ents = {}          # address -> {offset: signed word}
        self.collidables = []   # addresses, in list order (0x80127C28..)
        self.cell = {}          # (col,row) -> (attr16, slope_byte, height_byte)
        self.xtab = {}          # x -> column table value (s16), 0x80127168 + 2*x
        self.stairs = [1, 2, 2, 3, 4, 4, 5, 6, 6, 7, 8, 8, 9, 10, 10, 11, 12, 12, 13, 14, 14, 15, 16, 16]   # 0x800236D4
        self.gravflag = 0       # [0x80127000]
        self.locktimer = 0      # [0x80127164]
        self.dbg58 = 0          # [0x800DC058]
        self.dbg5c = 0          # [0x800DC05C]
        self.tile_base = 0x80310000


# ---------------------------------------------------------------------------------------------------------------------------------------
def find_candidate(w, a):
    """0x80036F34: first collidable (list order) whose box overlaps the box of entity a; 0 if none (or a is exempt)."""
    if a == HERO and s32(w.dbg58) < 0 and (w.dbg5c & 0x80000000):
        return 0
    E = w.ents[a]
    if not (E.get(0x6c, 0) & 0x80):
        return 0
    if E.get(0xb4, 0) & 0x80:
        return 0
    if E.get(0x28, 0) != 0:
        return 0
    t3, a1 = E.get(0x1d8, 0), E.get(0x1f0, 0)
    t2, v1 = E.get(0x1dc, 0), E.get(0x1f4, 0)
    t1, t7 = E.get(0x1e0, 0), E.get(0x1f8, 0)
    if len(w.collidables) <= 0:
        return 0
    t6, t5 = a1 + 1, v1 + 1
    for o in w.collidables:
        if o == a:
            continue
        O = w.ents[o]
        v = O.get(0x1d8, 0) - t3
        if v >= 0:
            if not (v < t6):
                continue
        elif not ((t3 - O.get(0x1d8, 0)) < O.get(0x1f0, 0) + 1):
            continue
        v = O.get(0x1dc, 0) - t2
        if v >= 0:
            if not (v < t5):
                continue
        elif not ((t2 - O.get(0x1dc, 0)) < O.get(0x1f4, 0) + 1):
            continue
        v = O.get(0x1e0, 0) - t1
        if v >= 0:
            if v < t7 + 1:
                return o
            continue
        if (t1 - O.get(0x1e0, 0)) < O.get(0x1f8, 0) + 1:
            return o
    return 0


def cell_ptr(w, col, row):
    return (w.tile_base + 0x604 + row * 416 + col * 8) & M32


def ground_height(w, a):
    E = w.ents[a]
    v0 = s32(E.get(0x114, 0) + E.get(0x1e4, 0))
    x1 = sra(v0, 16)
    x2 = sra(v0 + E.get(0x1f0, 0), 16)
    v0 = s32(E.get(0x118, 0) + E.get(0x1e8, 0))
    y1 = sra(v0, 16)
    y2 = sra(v0 + E.get(0x1f4, 0), 16)
    xs = [x1, x2, x1, x2]
    ys = [y1, y1, y2, y2]
    t2 = 0          # running maximum
    a2 = 0          # slope-hit bits: 1 = stairs, 2 = ladder entering, 4 = ladder exiting
    for k in range(4):
        x, y = xs[k], ys[k]
        col = s32(w.xtab[x])
        if col <= 0:
            col = 0
        elif col >= 0x34:
            col = 0x33
        row = sra(y, 4)
        if row <= 0:
            row = 0
        elif row >= 0x3c:
            row = 0x3b
        E[0x148 + 4 * k] = s32(cell_ptr(w, col, row))
        attr, slope, hb = w.cell.get((col, row), (0, 0, 0))
        sl = slope & 3
        if sl == 0:
            a1 = (hb << 4)
        else:
            a1 = (hb - 1) << 4
            if sl == 1:
                if (a2 & 6) == 0:
                    a1 = (a1 + 0x10) - mod16(y)
                else:
                    a1 += 0x10
                a2 |= 1
            elif sl == 2:
                if (a2 & 5) == 0:
                    a1 += w.stairs[23 - mod24(x)]
                else:
                    a1 += 0x10
                a2 |= 2
            else:
                if (a2 & 3) == 0:
                    a1 += w.stairs[mod24(x)]
                else:
                    a1 += 0x10
                a2 |= 4
        a1 = s32(a1 << 16)
        E[0x158 + 4 * k] = a1
        if t2 < a1:
            t2 = a1
    return t2


def _cell_attr(w, E, k):
    ptr = E.get(0x148 + 4 * k, 0) & M32
    off = ptr - w.tile_base - 0x604
    row, rem = divmod(off, 416)
    col = rem // 8
    return w.cell.get((col, row), (0, 0, 0))[0]


def wall_other(w, a):
    """0x800373E4 -> flags[4]"""
    E = w.ents[a]
    f = E.get(0x6c, 0)
    mask = 0x41 if (f & 8) else 0x40
    if f & 1:
        mask |= 0x1000
    t0 = E.get(0x1e0, 0)
    out = [0, 0, 0, 0]
    for k in range(4):
        if (_cell_attr(w, E, k) & mask) != 0 or not (E.get(0x158 + 4 * k, 0) < t0):
            out[k] = 1
    return out


def wall_hero(w, a):
    """0x80037488: everything is read from the hero entity (a is ignored). None = the debug 'no collision' bit (v0 = 0, nothing written)."""
    if s32(w.dbg58) < 0 and (w.dbg5c & 0x80000000):
        return None
    E = w.ents[HERO]
    f = E.get(0x6c, 0)
    t1 = 0x41 if (f & 8) else 0x40
    if f & 1:
        t1 |= 0x1000
    t0 = E.get(0x1e0, 0)
    t3 = 1 if s32(w.gravflag) < 2 else 0
    t4 = w.locktimer
    out = [0, 0, 0, 0]
    for k in range(4):
        attr = _cell_attr(w, E, k)
        if (attr & t1) != 0 or not (E.get(0x158 + 4 * k, 0) < t0):
            out[k] = 1
        if t3 and (attr & 0xe00) == 0x800:
            out[k] = 1
        if t4 == 0x20:
            if t0 != s32(E.get(0x158 + 4 * k, 0) + 1) or (attr & 0xe00) != 0x600:
                out[k] = 1
    return out


# ---------------------------------------------------------------------------------------------------------------------------------------
def set_mod(E):
    E[0x1d8] = s32(E.get(0x114, 0) + E.get(0x1e4, 0))
    E[0x1dc] = s32(E.get(0x118, 0) + E.get(0x1e8, 0))
    E[0x1e0] = s32(E.get(0x11c, 0) + E.get(0x1ec, 0))


def compute_xy(w, a, trace=None):
    """returns v0 at the return (= [sp+0x34]); the entity dict is updated in place. `trace` (list) receives one dict per attempt."""
    E = w.ents[a]
    sp28 = 0
    sp24 = 1 if (E.get(0x8c, 0) & 7) == 0 else 0
    while True:                                        # label 0x80037798: (re)start
        s2, s1 = E.get(0xe4, 0), E.get(0xe8, 0)
        if s2 == 0 and s1 == 0:                        # 0x800377A0 / 0x800377A8 -> 0x80037DC0
            set_mod(E)
            E[0x138] = s32(ground_height(w, a))
            return 0                                   # [sp+0x34] = 0
        sp2c = 0
        s4 = 0
        s5 = 0
        flags = [0, 0, 0, 0]
        while True:                                    # label 0x800377C0: one attempt
            s7, fp, sp30 = E.get(0x114, 0), E.get(0x118, 0), E.get(0x11c, 0)
            sp34 = s4
            flags = [0, 0, 0, 0]
            E[0x114] = s32(E.get(0x114, 0) + s2)
            E[0x1e0] = s32(E.get(0x11c, 0) + E.get(0x1ec, 0))
            E[0x118] = s32(E.get(0x118, 0) + s1)
            E[0x1d8] = s32(E[0x114] + E.get(0x1e4, 0))
            E[0x1dc] = s32(E[0x118] + E.get(0x1e8, 0))
            a2 = s32(ground_height(w, a))
            E[0x138] = a2
            rec = dict(s2=s2, s1=s1, s5=s5, sp34=sp34, cand=None, snapped=False, snapped_cand=None)
            blocked = False
            took_ground = False
            if (E.get(0x6c, 0) & 0x100) and E.get(0xb8, 0) == 0:       # 0x80037834 .. 0x80037884
                v = s32(a2 - E.get(0x1e0, 0) - 1)
                tol = 0x30000 if v >= 0 else 0x30003
                if abs(v) < tol:
                    took_ground = True
                    s3 = E.get(0x11c, 0)
                    E[0x11c] = s32(a2 + 1)
                    E[0x1d8] = s32(E[0x114] + E.get(0x1e4, 0))
                    E[0x1e0] = s32(E[0x11c] + E.get(0x1ec, 0))
                    E[0x1dc] = s32(E[0x118] + E.get(0x1e8, 0))
                    c = find_candidate(w, a)
                    rec["snapped"] = True
                    rec["snapped_cand"] = c
                    if c != 0:
                        E[0x11c] = s32(s3)
                        E[0x1d8] = s32(E[0x114] + E.get(0x1e4, 0))
                        E[0x1e0] = s32(E[0x11c] + E.get(0x1ec, 0))
                        E[0x1dc] = s32(E[0x118] + E.get(0x1e8, 0))
                        took_ground = False            # falls to 0x80037908: s4 IS assigned
                    # else: jump straight to the wall test (0x8003791C) with s4 left UNTOUCHED
            if not took_ground:
                s4 = find_candidate(w, a)              # 0x80037908-0x80037910
                rec["cand"] = s4
                if s4 != 0:
                    blocked = True
            if not blocked:
                if a == HERO:
                    r = wall_hero(w, a)
                    flags = r if r is not None else [0, 0, 0, 0]
                else:
                    flags = wall_other(w, a)
                if any(flags):
                    blocked = True
            rec["blocked"] = blocked
            rec["flags"] = list(flags)
            rec["pos"] = (E[0x114], E[0x118], E[0x11c])
            rec["s4_after"] = s4
            if trace is not None:
                trace.append(rec)
            if not blocked:                            # 0x80037D68: accepted
                sp2c = 1
                if s5 == 0:
                    return sp34                        # 0x80037DFC, no recompute
                s2 = 0 if s2 == -1 else (s2 >> 1)
                s1 = 0 if s1 == -1 else (s1 >> 1)
                if sp24:
                    if s2 == 0 and s1 == 0:
                        return sp34
                else:
                    if s2 == 0 or s1 == 0:
                        return sp34
                s5 += 1
                continue
            # 0x80037938: failed attempt: restore the position, halve
            E[0x114], E[0x118], E[0x11c] = s32(s7), s32(fp), s32(sp30)
            s2 = 0 if s2 == -1 else (s2 >> 1)
            s1 = 0 if s1 == -1 else (s1 >> 1)
            if sp24:
                if s2 != 0 or s1 != 0:
                    s5 += 1
                    continue
            else:
                if s2 != 0 and s1 != 0:
                    s5 += 1
                    continue
            break                                      # 0x8003799C
        # ---- the loop ended after a failed attempt ---------------------------------------------------------------------------
        fa = False
        if sp2c:
            pass
        elif sp28 == 1 or (E.get(0x6c, 0) & 0x2000) or s4 != 0:
            fa = True
        else:
            sp28 = 1
            d = E.get(0x8c, 0) & M32
            if d >= 0x20:
                fa = True
            else:
                if slide(d, flags, E) == "fa":
                    fa = True
                else:
                    continue                            # restart at 0x80037798
        if fa:
            E[0x13c] = 1
        set_mod(E)                                     # 0x80037D58 -> 0x80037DCC
        E[0x138] = s32(ground_height(w, a))
        return sp34


def slide(d, f, E):
    """the 32-entry jump table 0x80023734; f = [sp+0x10, +0x14, +0x18, +0x1C]; returns "fa" or "restart" (force words edited)."""
    f0, f1, f2, f3 = f
    if d == 0:
        if f2 and f3: return "fa"
        if f0 or f1: return "fa"
        E[0xe8] = 0
        if f2 and not f3: E[0xe4] = 0xC000
        elif (not f2) and f3: E[0xe4] = -0xC000
        return "restart"
    if 1 <= d <= 7:
        if f0:
            if f3: return "fa"
            E[0xe4] = 0
        elif f3:
            E[0xe8] = 0
        return "restart"
    if d == 8:
        if (f0 and f2) or f1 or f3: return "fa"
        E[0xe4] = 0
        if f0 and not f2: E[0xe8] = 0x8000
        elif (not f0) and f2: E[0xe8] = -0x8000
        return "restart"
    if 9 <= d <= 15:
        if not f1:
            if f2: E[0xe4] = 0
        else:
            if f2: return "fa"
            E[0xe8] = 0
        return "restart"
    if d == 16:
        if (f0 and f1) or f2 or f3: return "fa"
        E[0xe8] = 0
        if f0 and not f1: E[0xe4] = 0xC000
        elif (not f0) and f1: E[0xe4] = -0xC000
        return "restart"
    if 17 <= d <= 23:
        if f0 and f3: return "fa"
        if f3: E[0xe4] = 0
        elif f0: E[0xe8] = 0
        return "restart"
    if d == 24:
        if (f1 and f3) or f0 or f2: return "fa"
        E[0xe4] = 0
        if f1 and not f3: E[0xe8] = 0x8000
        elif (not f1) and f3: E[0xe8] = -0x8000
        return "restart"
    if f1 and f2: return "fa"          # 25..31
    if f2: E[0xe8] = 0
    elif f1: E[0xe4] = 0
    return "restart"
