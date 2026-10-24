"""a1_vec.py - disc-e20g3 A1: compares two Python models with the REAL code of ALUN_CD.EXE (France) on random vectors.
  (1) the collidable-list builder, the part of the lists pass 0x800384F4 that fills 0x80127C28 / 0x80127D2C;
  (2) FindEntityCollisionCandidate 0x80036F34 (the entity-versus-entity obstacle test of the movement code), over the list built by (1);
  (3) GetCollisionOnZ 0x80037F28 (the ground height under an entity with the entities counted), same list.
Usage: python -B -I a1_vec.py [N] [mutant_addr_hex new_word_hex]    (exit 0 iff no mismatch; the mutant form must report mismatches)
Machine: oracle/machine.py (E20 oracle).  The 64 slots are filled with random bytes in the fields the code reads; nothing else is assumed.
Coverage: every PC executed is recorded; the instruction ranges of the functions under test must be 100 % covered (reported)."""
import sys
import os
import random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L          # noqa: F401  (sets sys.path for the oracle)
from machine import Machine, M32, s32
import world as W

N0 = int(sys.argv[1]) if len(sys.argv) > 1 and __name__ == '__main__' else 400
MUT0 = (int(sys.argv[2], 16), int(sys.argv[3], 16)) if len(sys.argv) >= 4 and __name__ == '__main__' else None

ENT0, STRIDE = W.ENT0, W.STRIDE
LIST, CNT, LASTIDX = 0x80127C28, 0x80127D2C, 0x80134360
ACT, ACTCNT = 0x80127B28, 0x80127D28
TRANS, TRANS2 = 0x800DC058, 0x800DC05C


def ea(i):
    return ENT0 + STRIDE * i


def rnd_ent(rng, m, i, pos_range, ref=None):
    a = ea(i)
    m.zero(a, STRIDE)
    m.poke(a + 0x10, rng.choice([0, 1, 2, 2, 3, 3, 4]))
    m.poke(a + 0x20, rng.choice([0, 0, 0, 5]))
    m.poke(a + 0x28, rng.choice([0] * 8 + [ea(rng.randrange(0, 64))]))
    f = rng.getrandbits(32)
    if rng.random() < 0.85:
        f |= 0x80
    else:
        f &= ~0x80
    m.poke(a + 0x6C, f)
    m.poke(a + 0xB4, rng.choice([0] * 7 + [0x80, 0x40, 0xC0, rng.getrandbits(8)]))
    m.poke(a + 0x1CC, rng.getrandbits(8))          # third list reads this
    x = rng.randrange(0, pos_range)
    y = rng.randrange(0, pos_range)
    z = rng.randrange(0, pos_range)
    sc = rng.choice([1, 65536])                     # whole-pixel and 16.16 granularity
    m.poke(a + 0x1D8, (x * sc) & M32)
    m.poke(a + 0x1DC, (y * sc) & M32)
    m.poke(a + 0x1E0, (z * sc) & M32)
    w = rng.randrange(1, 40) * sc - (1 if sc > 1 else 0)
    h = rng.randrange(1, 40) * sc - (1 if sc > 1 else 0)
    d = rng.randrange(1, 40) * sc - (1 if sc > 1 else 0)
    m.poke(a + 0x1F0, w)
    m.poke(a + 0x1F4, h)
    m.poke(a + 0x1F8, d)
    m.poke(a + 0x138, rng.randrange(0, pos_range) * sc)     # terrain height for 0x80037F28
    if ref is not None and rng.random() < 0.8:
        # targeted boundary mode: exactly ONE axis is flush (or one unit off) against the reference entity's box, on either side, the two other axes are made to overlap it
        ra = ea(ref)
        axes = [(0x1D8, 0x1F0), (0x1DC, 0x1F4), (0x1E0, 0x1F8)]
        flush = rng.randrange(3)
        for k, (po, so) in enumerate(axes):
            rv, rs = s32(m.peek(ra + po)), s32(m.peek(ra + so))
            cs = s32(m.peek(a + so))
            if k == flush:
                d = rng.choice([-1, 0, 0, 0, 1])
                if rng.random() < 0.5:
                    v = rv + rs + 1 + d                    # this entity starts where the reference box ends (+1 = semi-open edge)
                else:
                    v = rv - (cs + 1) + d                  # this entity ends where the reference box starts
            else:
                v = rv + rng.randrange(-cs, rs + 1)        # overlapping on this axis
            m.poke(a + po, v & M32)


# ---- models --------------------------------------------------------------------------------------------------------------
def rd(m, a):
    return m.peeks(a)


THIRD, THIRDCNT = 0x80134250, 0x8013435C


def model_lists(m, n):
    act, col, third = [], [], []
    for i in range(0, n + 1):
        a = ea(i)
        st = m.peek(a + 0x10)
        if ((st - 2) & M32) < 2 and m.peek(a + 0x20) == 0:
            act.append(a)
        if (m.peek(a + 0x6C) & 0x80) and not (m.peek(a + 0xB4) & 0x80) and m.peek(a + 0x28) == 0:
            col.append(a)
        if ((st - 2) & M32) < 2 and (m.peek(a + 0x1CC) & 3) != 3:
            third.append(a)
    return act, col, third


GATES = {'hero_transition': 0, 'not_collidable_flag': 0, 'anim_flag_0x80': 0, 'carried_+0x28': 0, 'candidate_overlap': 0, 'no_overlap': 0}


def model_find(m, subj, col):
    """FindEntityCollisionCandidate 0x80036F34 (disassembly lst/phys_36400_38000.txt lines 718-816)"""
    if subj == ENT0 and s32(m.peek(TRANS)) < 0 and (m.peek(TRANS2) & 0x80000000):
        GATES['hero_transition'] += 1
        return 0
    if not (m.peek(subj + 0x6C) & 0x80):
        GATES['not_collidable_flag'] += 1
        return 0
    if m.peek(subj + 0xB4) & 0x80:
        GATES['anim_flag_0x80'] += 1
        return 0
    if m.peek(subj + 0x28):
        GATES['carried_+0x28'] += 1
        return 0
    X, Y, Z = rd(m, subj + 0x1D8), rd(m, subj + 0x1DC), rd(m, subj + 0x1E0)
    Wd, Hd, Dd = rd(m, subj + 0x1F0), rd(m, subj + 0x1F4), rd(m, subj + 0x1F8)
    for c in col:
        if c == subj:
            continue
        cx, cy, cz = rd(m, c + 0x1D8), rd(m, c + 0x1DC), rd(m, c + 0x1E0)
        dx = cx - X
        if dx < 0:
            if not (X - cx < rd(m, c + 0x1F0) + 1):
                continue
        elif not (dx < Wd + 1):
            continue
        dy = cy - Y
        if dy < 0:
            if not (Y - cy < rd(m, c + 0x1F4) + 1):
                continue
        elif not (dy < Hd + 1):
            continue
        dz = cz - Z
        if dz < 0:
            if not (Z - cz < rd(m, c + 0x1F8) + 1):
                continue
        elif not (dz < Dd + 1):
            continue
        GATES['candidate_overlap'] += 1
        return c
    GATES['no_overlap'] += 1
    return 0


def model_groundz(m, subj, col):
    """GetCollisionOnZ 0x80037F28 (lst/phys_37f28_38e18.txt lines 1-60): best = terrain + 1, raised by the tops of the entities strictly below the subject's feet that overlap in X and Y"""
    best = rd(m, subj + 0x138) + 1
    if not (m.peek(subj + 0x6C) & 0x80) or (m.peek(subj + 0xB4) & 0x80) or m.peek(subj + 0x28):
        return best
    X, Y, Z = rd(m, subj + 0x1D8), rd(m, subj + 0x1DC), rd(m, subj + 0x1E0)
    Wd, Hd = rd(m, subj + 0x1F0), rd(m, subj + 0x1F4)
    for c in col:
        if c == subj:
            continue
        top = rd(m, c + 0x1E0) + rd(m, c + 0x1F8)
        if not (top < Z):
            continue
        if top < best:
            continue
        cx, cy = rd(m, c + 0x1D8), rd(m, c + 0x1DC)
        dx = cx - X
        if dx < 0:
            if not (X - cx < rd(m, c + 0x1F0) + 1):
                continue
        elif not (dx < Wd + 1):
            continue
        dy = cy - Y
        if dy < 0:
            if not (Y - cy < rd(m, c + 0x1F4) + 1):
                continue
        elif not (dy < Hd + 1):
            continue
        best = top + 1
    return best


def _ovl_xy(m, X, Y, Wd, Hd, c):
    cx, cy = rd(m, c + 0x1D8), rd(m, c + 0x1DC)
    dx = cx - X
    if dx < 0:
        if not (X - cx < rd(m, c + 0x1F0) + 1):
            return False
    elif not (dx < Wd + 1):
        return False
    dy = cy - Y
    if dy < 0:
        if not (Y - cy < rd(m, c + 0x1F4) + 1):
            return False
    elif not (dy < Hd + 1):
        return False
    return True


def model_down(m, subj, col):
    """0x80036BFC (CheckEntityCollisionDown): (ret, new_z, entity) ; lst/phys_36400_38000.txt lines 512-613"""
    z, dz, terr = rd(m, subj + 0x1E0), rd(m, subj + 0xEC), rd(m, subj + 0x138)
    t1 = z + dz
    t6 = 0
    if not (terr < t1):
        t1 = terr + 1
        t6 = 1
    t7 = 0
    if not (m.peek(subj + 0x6C) & 0x80) or (m.peek(subj + 0xB4) & 0x80) or m.peek(subj + 0x28):
        return t6, t1, t7
    X, Y, Wd, Hd = rd(m, subj + 0x1D8), rd(m, subj + 0x1DC), rd(m, subj + 0x1F0), rd(m, subj + 0x1F4)
    for c in col:
        if c == subj:
            continue
        top = rd(m, c + 0x1E0) + rd(m, c + 0x1F8)
        if not (top < z):
            continue
        if top < t1:
            continue
        if not _ovl_xy(m, X, Y, Wd, Hd, c):
            continue
        t1 = top + 1
        t7 = c
        t6 = 1
    return t6, t1, t7


def model_up(m, subj, col):
    """0x80036D94 (CheckEntityCollisionUp): (ret, new_top, entity) ; lines 614-717"""
    top0 = rd(m, subj + 0x1E0) + rd(m, subj + 0x1F8)
    t2 = top0 + rd(m, subj + 0xEC)
    t7 = 0
    if 0x07800000 < t2:
        t2 = 0x077FFFFF
        t7 = 1
    t8 = 0
    if not (m.peek(subj + 0x6C) & 0x80) or (m.peek(subj + 0xB4) & 0x80) or m.peek(subj + 0x28):
        return t7, t2, t8
    X, Y, Wd, Hd = rd(m, subj + 0x1D8), rd(m, subj + 0x1DC), rd(m, subj + 0x1F0), rd(m, subj + 0x1F4)
    for c in col:
        if c == subj:
            continue
        cz = rd(m, c + 0x1E0)
        if not (top0 < cz):
            continue
        if t2 < cz:
            continue
        if not _ovl_xy(m, X, Y, Wd, Hd, c):
            continue
        t2 = cz - 1
        t8 = c
        t7 = 1
    return t7, t2, t8


OUTW = 0x801FE000


def main(N=None, MUT=None, quiet=False):
    N = N0 if N is None else N
    MUT = MUT0 if MUT is None else MUT
    rng = random.Random(0xA1)
    m = Machine()
    if MUT:
        m.ww(MUT[0], MUT[1])
    m.watch_uninit = False
    bad = {'lists': 0, 'find': 0, 'groundz': 0, 'down': 0, 'up': 0}
    tot = {'lists': 0, 'find': 0, 'groundz': 0, 'down': 0, 'up': 0}
    first = {}
    for it in range(N):
        n = rng.choice([-1, 0, 0, 1, 2, 5, 9, 12])
        pos = rng.choice([8, 12, 30, 80, 200])
        m.poke(LASTIDX, n)
        for i in range(64):
            if i <= max(n, 0) + 2:
                rnd_ent(rng, m, i, pos, ref=(rng.randrange(0, i) if i > 0 and rng.random() < 0.6 else None))
            else:
                m.zero(ea(i), STRIDE)
        m.poke(TRANS, rng.choice([0, 0, 0x80000000, 0x80000001]))
        m.poke(TRANS2, rng.choice([0, 0x80000000, 0x7FFFFFFF]))
        m.zero(LIST, 4 * 64)
        m.zero(THIRD, 4 * 64)
        m.poke(THIRDCNT, 0xBEEF)
        m.poke(CNT, 0xDEAD)
        m.call(0x800384F4)
        act_m, col_m, third_m = model_lists(m, n)
        if m.peek(CNT) < 60:
            m.poke(LIST + 4 * m.peek(CNT), ea(rng.randrange(0, max(n, 0) + 3)))   # a stale pointer after the end of the list, as the real arrays hold (never read by correct code)
        got_cnt = m.peek(CNT)
        got_list = [m.peek(LIST + 4 * k) for k in range(got_cnt)] if got_cnt < 70 else []
        got_act = [m.peek(ACT + 4 * k) for k in range(m.peek(ACTCNT))]
        got_third = [m.peek(THIRD + 4 * k) for k in range(m.peek(THIRDCNT))]
        tot['lists'] += 1
        if got_list != col_m or got_act != act_m or got_third != third_m:
            bad['lists'] += 1
            first.setdefault('lists', (it, n, [hex(x) for x in got_list], [hex(x) for x in col_m]))
        for i in range(0, max(n, 0) + 3):
            subj = ea(i)
            r = m.call(0x80036F34, subj) & M32
            want = model_find(m, subj, col_m)
            tot['find'] += 1
            if r != want:
                bad['find'] += 1
                first.setdefault('find', (it, i, hex(r), hex(want)))
            g = m.call(0x80037F28, subj)
            gw = model_groundz(m, subj, col_m)
            tot['groundz'] += 1
            if s32(g) != gw:
                bad['groundz'] += 1
                first.setdefault('groundz', (it, i, s32(g), gw))
            # the Z functions read +0xEC (the Z move of this tick): random small signed values around the box sizes
            top_now = rd(m, subj + 0x1E0) + rd(m, subj + 0x1F8)
            m.poke(subj + 0xEC, rng.choice([0, 1, -1, rng.randrange(-60, 60), rng.randrange(-60, 60) * 65536, 0x08000000, 0x07800000 - top_now + rng.choice([-1, 0, 1, 100, 0x8000, 0xFFFF, 0x10000, 0x10001])]) & M32)
            if rng.random() < 0.5 and n >= 1:
                # dedicated geometry for the Z functions: a partner entity placed so that its Z relation is satisfied (down: its top lies in [target, my feet); up: its bottom in (my top, target top])
                # and exactly one XY axis flush / one unit off, the other XY axis overlapping (the semi-open edge of the XY test)
                p = rng.choice([k for k in range(0, n + 3) if k != i])
                pe = ea(p)
                z0, d0 = rd(m, subj + 0x1E0), rd(m, subj + 0x1F8)
                sc = rng.choice([1, 65536])
                dz = -rng.randrange(1, 12) * sc if rng.random() < 0.6 else rng.randrange(1, 12) * sc
                m.poke(subj + 0xEC, dz & M32)
                m.poke(subj + 0x138, (z0 - rng.randrange(20, 40) * sc) & M32)       # terrain well below
                pd = rd(m, pe + 0x1F8)
                if dz < 0:
                    k = rng.randrange(0, -dz // sc + 1) * sc if sc else 0
                    top = z0 - sc - k + rng.choice([0, 0, -1, 1])                    # top in [z0+dz, z0): around the boundaries
                    m.poke(pe + 0x1E0, (top - pd) & M32)
                else:
                    k = rng.randrange(0, dz // sc + 1) * sc
                    m.poke(pe + 0x1E0, (z0 + d0 + sc + k + rng.choice([0, 0, -1, 1])) & M32)   # bottom in (my top, my top + dz]
                axes = [(0x1D8, 0x1F0), (0x1DC, 0x1F4)]
                fl = rng.randrange(2)
                for kx, (po, so) in enumerate(axes):
                    sv, ss = rd(m, subj + po), rd(m, subj + so)
                    cs = rd(m, pe + so)
                    if kx == fl:
                        dd = rng.choice([-1, 0, 0, 0, 1])
                        v = sv + ss + 1 + dd if rng.random() < 0.5 else sv - (cs + 1) + dd
                    else:
                        v = sv + rng.randrange(-cs, ss + 1)
                    m.poke(pe + po, v & M32)
                # keep both entities eligible for the list (the lists pass is re-run below)
                m.poke(subj + 0x6C, m.peek(subj + 0x6C) | 0x80); m.poke(subj + 0xB4, rng.choice([0, 1, 0x40, 0x41, 0x7F, 0x3F])); m.poke(subj + 0x28, 0)
                m.poke(pe + 0x6C, m.peek(pe + 0x6C) | 0x80); m.poke(pe + 0xB4, rng.choice([0, 1, 0x40, 0x41])); m.poke(pe + 0x28, 0)
                m.poke(subj + 0x2C, rng.choice([0, ea(3)]))                      # the carried-hands field: the gates read +0x28, never +0x2C
                gate = rng.random()
                if gate < 0.12:
                    m.poke(subj + 0x28, ea(7))                                        # carried subject: the Z functions must return before the loop
                elif gate < 0.24:
                    m.poke(subj + 0xB4, 0x80 | rng.choice([0, 1, 0x40]))             # no-entity-collision animation flag
                elif gate < 0.36:
                    m.poke(subj + 0x6C, m.peek(subj + 0x6C) & ~0x80 & M32)           # not Collidable
                    m.poke(pe + 0x6C, m.peek(pe + 0x6C) | 0x80)
                m.poke(subj + 0x10, 2); m.poke(pe + 0x10, 2)
                m.call(0x800384F4)
                col_z = [m.peek(LIST + 4 * kk) for kk in range(m.peek(CNT))]
                col_m = col_z                     # the real list in memory is rebuilt: the model follows it for the rest of this vector
            else:
                col_z = col_m
            for (name, addr, model) in (('down', 0x80036BFC, model_down), ('up', 0x80036D94, model_up)):
                m.poke(OUTW, 0xAAAA)
                m.poke(OUTW + 4, 0xBBBB)
                rr = m.call(addr, subj, OUTW, OUTW + 4)
                got = (rr & M32, m.peeks(OUTW), m.peek(OUTW + 4))
                wr, wz, we = model(m, subj, col_z)
                tot[name] += 1
                if got != (wr & M32, wz, we):
                    bad[name] += 1
                    first.setdefault(name, (it, i, got, (wr, wz, we)))
    cov = m.cov
    ranges = {'lists 0x800384F4-0x8003862C': (0x800384F4, 0x80038630), 'FindEntityCollisionCandidate 0x80036F34-0x800370BC': (0x80036F34, 0x800370C0),
              'GetCollisionOnZ 0x80037F28-0x80038060': (0x80037F28, 0x80038064),
              'CheckEntityCollisionDown 0x80036BFC-0x80036D8C': (0x80036BFC, 0x80036D90), 'CheckEntityCollisionUp 0x80036D94-0x80036F2C': (0x80036D94, 0x80036F30)}
    out = []
    for name, (a, b) in ranges.items():
        words = list(range(a, b, 4))
        hit = [w for w in words if w in cov]
        miss = [w for w in words if w not in cov]
        out.append('%-55s %d/%d instructions executed%s' % (name, len(hit), len(words), ('  NOT executed: ' + ' '.join('%X' % x for x in miss)) if miss else ''))
    if not quiet:
        print('vectors', N, 'totals', tot, 'mismatches', bad, 'first', first)
        print('find subject-gate / result counts (model):', GATES)
        print('\n'.join(out))
    return bad, tot


if __name__ == '__main__':
    b, t = main()
    sys.exit(0 if not any(b.values()) else 1)
