"""hmodel.py - E14.a0b: independent models of the shared helpers of the enemy AI, written from the disassembly of ALUN_CD.EXE (France) (docs/plan-e14-annexe/e14a/oracle/out/dis.txt) in plain Python,
sharing with the emulated binary only the data tables of the EXE (the atan tables 0x80022488 / 0x80022688 and the quarter-turn table 0x80028B34).  Nothing here calls the emulator.

Each model takes the inputs the helper reads (as signed 32-bit integers unless stated) and returns the outputs it writes.  The model is the PREDICTED value written before any run: a run where the
emulated binary disagrees with it is a failure of the model (or of the understanding), never re-fitted.  The C# port is compared with the OUTPUT OF THE EMULATED BINARY, not with this file.

Also here: the deterministic generator of the random states (SplitMix64, no dependency on the Python version).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lib

M32 = 0xFFFFFFFF
M64 = 0xFFFFFFFFFFFFFFFF
LCG_MUL = 0x7D2B89DD
LCG_ADD = 0xE06A02E7


def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


def u32(x):
    return x & M32


def negu(x):
    """subu zero, x (two's complement: the minimum stays the minimum)"""
    return s32(-x)


def absw(x):
    """the `bgez / negu` pair of the helpers"""
    x = s32(x)
    return x if x >= 0 else negu(x)


# ---- the tables of the EXE ------------------------------------------------------------------------------------------------
def quarter_turn_table():
    return [lib.DATA[lib.OFF + (0x80028B34 - lib.BASE) + i] for i in range(32)]


def atan_table():
    out = []
    for i in range(256):
        v = lib.DATA[lib.OFF + (0x80022488 - lib.BASE) + 2 * i] | (lib.DATA[lib.OFF + (0x80022488 - lib.BASE) + 2 * i + 1] << 8)
        out.append(v - 0x10000 if v & 0x8000 else v)
    return out


def atan_thresholds():
    return [lib.u32(0x80022688 + 4 * i) for i in range(29)]


_QT = None
_AT = None
_TH = None


def tables():
    global _QT, _AT, _TH
    if _QT is None:
        _QT, _AT, _TH = quarter_turn_table(), atan_table(), atan_thresholds()
    return _QT, _AT, _TH


# ---- 0x8002E814: direction (0..31) from the vector (dx, dy) --------------------------------------------------------------------
def atan_dir(dx, dy):
    qt, at, th = tables()
    flip = (2 if s32(dy) < 1 else 0) + (1 if s32(dx) < 0 else 0)
    ax = u32(absw(dx))
    ay = u32(absw(dy))
    big = ay if ax < ay else ax
    shift = 0
    if th[0] < big:
        shift = 1
        while th[shift] < big:
            shift += 1
    ax >>= shift
    ay >>= shift
    v = at[(ay << 4) + ax]
    if flip == 0:
        v = v + 0x18
    elif flip == 1:
        v = 8 - v
    elif flip == 2:
        v = 0x18 - v
    else:
        v = v + 8
    return v & 0x1F


# ---- 0x8007FE20 / 0x8007FE8C: relative position ----------------------------------------------------------------------------------
def relpos(sx, sy, sz, hx, hy, hz):
    """out = [|dx|, |dy|, |dz|, dx, dy, dz]; dz = sz - hz where sz is the floor (+0x134) of the entity for 0x8007FE20 and its Z (+0x11C) for 0x8007FE8C; hz is the floor of the hero"""
    dx, dy, dz = s32(sx - hx), s32(sy - hy), s32(sz - hz)
    return [absw(dx), absw(dy), absw(dz), dx, dy, dz]


# ---- 0x8008048C / 0x800805C8: the attack windows --------------------------------------------------------------------------------------
def try_attack(rel, direction, a, b, d3):
    """TryAttackPlayer2(rel, dir, a3 = a, d2 = b, d3) and TryAttackPlayerFront (a2 = a, a3 = b, d3).  rel = the six words [|dx|, |dy|, |dz|, dx, dy, dz]"""
    r0, r1, r2, r3, r4, r5 = [s32(x) for x in rel]
    a, b, d3 = s32(a), s32(b), s32(d3)
    if d3 < r2:
        return 0
    if direction == 0:
        if a < r0 or r4 > 0:
            return 0
        return 0 if b < r1 else 1
    if direction == 1:
        if a < r0 or r4 < 0:
            return 0
        return 0 if b < r1 else 1
    if direction == 2:
        if a < r1 or r3 < 0:
            return 0
        return 0 if b < r0 else 1
    if direction == 3:
        if a < r1 or r3 > 0:
            return 0
        return 0 if b < r0 else 1
    return 0


def try_attack2(rel, dir_byte, a3, d2, d3):
    return try_attack(rel, dir_byte & 0xFF, a3, d2, d3)


def try_attack_front(box_dir_word, rel, a2, a3, d3):
    return try_attack(rel, s32(box_dir_word), a2, a3, d3)


# ---- 0x80080144: HandleAnimationDirection --------------------------------------------------------------------------------------------
def handle_animation_direction(floor, corners, target_dir, anim, dist):
    """returns (result, new_target_anim or None, new_target_dir or None).  thr = dist + 1; t[k] = floor - corner[k]; the decision tree of 0x80080170-0x80080258, label by label"""
    qt, _, _ = tables()
    thr = s32(dist + 1)
    t = [s32(floor - c) for c in corners]       # t2, t0, t1, v1 of the disassembly are t[0], t[1], t[2], t[3]
    d = s32(target_dir)
    A, B, D, C = thr < t[0], thr < t[1], thr < t[2], thr < t[3]

    def in_range(lo, count):                    # (d - lo) <u count  (addiu / sltiu)
        return u32(d - lo) < count

    def turn():
        return (1, anim & 0xFF, qt[d])

    no = (0, None, None)
    state = '19c'
    if A:                                       # 0x80080174
        if B:                                   # 0x8008017C
            if in_range(9, 15):                 # 0x80080184-0x80080194
                return turn()
        else:
            state = '1cc'
    if state == '19c':                          # 0x8008019C
        if not B:
            state = '1c8'
        elif not C:                             # 0x800801A8
            state = '1f8'
        else:
            if in_range(0x11, 15):              # 0x800801B0-0x800801C0
                return turn()
            state = '1c8'
    if state == '1c8':
        state = '1cc'
    if state == '1cc':                          # 0x800801CC: C?
        if not C:
            state = '1f4'
        else:
            if not D:                           # 0x800801D4
                return no
            if not in_range(8, 0x11):           # 0x800801DC-0x800801EC: beqz -> turn
                return turn()
            state = '1f4'
    if state == '1f4':
        state = '1f8'
    # 0x800801F8
    if not D or not A:
        return no
    if d < 0x10:                                # 0x80080208-0x80080218 (signed)
        return turn()
    return no


# ---- 0x8007FEF8: StartWalk -----------------------------------------------------------------------------------------------------------------
def lcg(state):
    return (state * LCG_MUL + LCG_ADD) & M32


def start_walk(rng, self_x, self_y, hero_x, hero_y, anim, rng_range, chance, pct=0x65):
    """returns (rng after, target_dir, target_anim, half or None).  half = the value stored at +0x282 (16 bits) when the range argument has a non-zero low half"""
    s1 = lcg(rng)
    r1 = (s1 * pct) >> 32
    if s32(r1) < (chance & 0xFF):
        direction = atan_dir(s32(hero_x - self_x), s32(hero_y - self_y))
        state = s1
    else:
        state = lcg(s1)
        direction = (state * 0x20) >> 32
        direction = s32(direction)
    half = None
    if (rng_range & 0xFFFF) != 0:
        state = lcg(state)
        half = (((state * 0x10) >> 32) + rng_range) & 0xFFFF
    return (state, direction, anim & 0xFF, half)


# ---- D1 / D9 skeleton -------------------------------------------------------------------------------------------------------------------------
def hit_reaction(self_x, self_y, hero_x, hero_y, killed, old_byte, anim):
    """returns (+0x277 after, +0x88 after, +0x8C after): ComputeNewHp is stubbed in the oracle and returns `killed`"""
    return (1 if killed else old_byte & 0xFF, anim, atan_dir(s32(self_x - hero_x), s32(self_y - hero_y)))


# ---- random states ------------------------------------------------------------------------------------------------------------------------------
class Rng:
    """SplitMix64: the same sequence on every Python version"""
    def __init__(self, seed):
        self.s = seed & M64

    def next64(self):
        self.s = (self.s + 0x9E3779B97F4A7C15) & M64
        z = self.s
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & M64
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & M64
        return z ^ (z >> 31)

    def u32(self):
        return self.next64() >> 32

    def s32(self):
        return s32(self.u32())

    def below(self, n):
        return (self.u32() * n) >> 32

    def range(self, lo, hi):
        """lo..hi inclusive"""
        return lo + self.below(hi - lo + 1)

    def chance(self, num, den):
        return self.below(den) < num

    def pick(self, seq):
        return seq[self.below(len(seq))]


# ---- 0x800370C4: the four corner heights of the box of an entity (ComputeEntityGroundHeight) ----------------------------------------------------------
def slope_table():
    return [lib.u32(0x800236D4 + 4 * i) for i in range(24)]


def c_mod(a, n):
    """the remainder of C (the sign of the dividend)"""
    q = abs(a) // n
    q = q if a >= 0 else -q
    return a - q * n


def ground_model(word_at, pos_x, pos_y, off_x, off_y, size_x, size_y, sub=1):
    """word_at(row, col) -> the first 32-bit word of the cell (walkability | ground << 8 | slope << 16 | height << 24).  Returns (terrain, [corner0..corner3]) with
    the corners as the signed 32-bit words stored at +0x158 + 4k and the terrain = max(0, corners) (the value the callers store at +0x138)."""
    st = slope_table()
    x1 = s32(pos_x + off_x) >> 16
    x2 = s32(pos_x + off_x + size_x) >> 16
    y1 = s32(pos_y + off_y) >> 16
    y2 = s32(pos_y + off_y + size_y) >> 16
    xs = [x1, x2, x1, x2]
    ys = [y1, y1, y2, y2]
    seen = 0
    best = 0
    corners = []
    for k in range(4):
        x, y = xs[k], ys[k]
        assert 0 <= x < 1300, 'x pixel %d outside the column table of the oracle world' % x
        col = min(x // 24, 51)
        col = 0 if col <= 0 else (col if col < 0x34 else 0x33)
        row = y >> 4
        row = 0 if row <= 0 else (row if row < 0x3C else 0x3B)
        w = word_at(row, col)
        slope = (w >> 16) & 3
        hb = (w >> 24) & 0xFF
        if slope == 0:
            a1 = hb << 4
        else:
            a1 = (hb - sub) << 4
            if slope == 1:
                if seen & 6:
                    a1 += 0x10
                else:
                    a1 = a1 + 0x10 - c_mod(y, 16)
                seen |= 1
            elif slope == 2:
                if seen & 5:
                    a1 += 0x10
                else:
                    a1 += s32(st[0x17 - c_mod(x, 24)])
                seen |= 2
            else:
                if seen & 3:
                    a1 += 0x10
                else:
                    a1 += s32(st[c_mod(x, 24)])
                seen |= 4
        a1 = s32(a1 << 16)
        corners.append(a1)
        if best < a1:
            best = a1
    return best, corners
