"""f1model.py - h4d2 discovery (read-only): MY OWN Python models of the fire-1 functions, written from my capstone listings of ALUN_CD.EXE (France) (lst/*.txt), NOT from the e20-h4 models:
  driver  0x80035A84 (phases 0, 1, 2; slope guard 0x800351B8; general cancel 0x800350C0 restricted to the items 0 / 0x2F / the neutral 0x2B)
  cancel  0x80034D2C (fire 1)
  C63     0x8007AC60 (the ball) with the target query 0x8003AF70 + sort 0x8003AE04 (the h4b model, re-written here) and the turn without the shortest path
  E3      0x8007EDA0
The models read the machine state through S (a state view with g(addr) / p(addr, v) over 32-bit words, journal list) and are compared by the generators with the REAL MIPS code run by the harness.
`mode` arguments select the reproduce / correct variants of the unplanned defects (labelled INFERENCE in the report): corrected = inclusive bound, sort from 2 results, shortest-path turn."""
M = 0xFFFFFFFF
ENT0, STRIDE = 0x80127D30, 0x294
HERO = ENT0
INUSE, PHASE, COUNTER, PTR, IDENT = 0x80127164, 0x80127160, 0x8012715C, 0x8012711C, 0x80127120
RNG, COUNT = 0x80098708, 0x80134360
PTRS, KEYS = 0x800CC058, 0x800CC158


def s32(x):
    x &= M
    return x - (1 << 32) if x & 0x80000000 else x


def ent(i):
    return ENT0 + STRIDE * i


def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M


def draw(S, n):
    """one generator step, the draw is hi(s * n) = (s * n) >> 32 (unsigned product), as the multu/mfhi of the binary"""
    s = lcg(S.g(RNG))
    S.p(RNG, s)
    return (s * n) >> 32


def guard(S):                      # 0x800351B8
    slope = S.g(HERO + 0x18C)
    anim = S.g(HERO + 0x88)
    if slope == 4 or ((anim - 0x32) & M) >= 2:
        cancel_general(S)
        return 1
    return 0


def cancel_general(S):             # 0x800350C0, restricted to the items the generators use
    it = S.g(INUSE)
    if it == 0:
        return
    idx = (it - 0x1F) & M
    if idx < 0x14 and it == 0x2F:
        cancel_fire1(S)
    elif idx < 0x14 and it not in (0x2B, 0x31):
        raise NotImplementedError('item %#x cancel is another function' % it)
    S.p(INUSE, 0)


def destroy_nofx(S, e):            # 0x8003A774
    child = S.g(e + 0x1B8)
    S.p(e + 0x10, 4)
    S.p(e + 0x228, M)
    if child:
        S.p(child + 0x68, 0)
        S.p(e + 0x1B8, 0)
    parent = S.g(e + 0x28)
    if parent:
        S.p(parent + 0x2C, 0)


def cancel_fire1(S):               # 0x80034D2C
    count = s32(S.g(COUNT))
    if count <= 0:
        return
    i = 1
    while True:
        e = ent(i)
        if S.g(e + 0x10) == 2:
            t = S.g(e + 0x68)
            if t == 0x14:
                S.j.append(('effect', 0, 0x13, 2, S.g(e + 0x114), S.g(e + 0x118), S.g(e + 0x11C)))
                destroy_nofx(S, e)
                t = S.g(e + 0x68)
            if t == 0x17:
                S.p(e + 0x10, 3)
                S.p(e + 0x20, 0)
        i += 1
        if count < i:
            break


def driver(S, spawn_ret):          # 0x80035A84 ; spawn_ret(type) -> pointer or 0 (the stub of 0x80039F58)
    ph = s32(S.g(PHASE))
    if ph == 1:
        pass
    elif ph == 0:
        if guard(S):
            return 1
        if S.g(HERO + 0x88) != 0x33:
            return 0
        p = spawn_ret(0x14)
        S.j.append(('spawn', HERO, 0, 0x14, S.g(HERO + 0x114), S.g(HERO + 0x118), S.g(HERO + 0x11C), 0))
        S.p(PTR, p)
        if p == 0:
            return 1
        ident = S.g(p + 4)
        S.p(PHASE, (S.g(PHASE) + 1) & M)
        S.p(IDENT, ident)
    elif ph == 2:
        return 0 if S.g(HERO + 0x88) == 0x34 else 1
    else:
        return 0
    # phase 1 (also reached from phase 0 in the same tick)
    if guard(S):
        return 1
    p = S.g(PTR)
    if S.g(p + 4) != S.g(IDENT):
        S.p(HERO + 0x88, 0x34)
        S.p(PHASE, (S.g(PHASE) + 1) & M)
        return 0
    if S.g(COUNTER) & 7:
        return 0
    d = draw(S, 0x20)
    q = spawn_ret(0x17)
    S.j.append(('spawn', HERO, 0, 0x17, S.g(HERO + 0x114), S.g(HERO + 0x118), (S.g(HERO + 0x11C) + 0x100000) & M, d))
    if q:
        S.p(q + 0xB8, 0x80000)
        S.p(q + 0x274, 0x10)
        S.p(q + 0x278, 0)
    return 0


# ---- the query 0x8003AF70 and its sort 0x8003AE04 (my model of the h4b brick, re-written) -----------------------------------------------------------------------------------------------
def hoare(keys, ptrs, lo, hi):
    i, j = lo, hi
    pivot = ((keys[hi] + keys[lo]) & M) >> 1
    while True:
        while keys[i] < pivot:
            i += 1
        while pivot < keys[j]:
            j -= 1
        if i < j:
            keys[i], keys[j] = keys[j], keys[i]
            ptrs[i], ptrs[j] = ptrs[j], ptrs[i]
            i += 1
            j -= 1
            continue
        break
    i -= 1
    if lo < i:
        hoare(keys, ptrs, lo, i)
    if j + 1 < hi:
        hoare(keys, ptrs, j + 1, hi)


def query(S, caller, mask, attr, mode='binary'):
    count = s32(S.g(COUNT))
    keys, ptrs = [], []
    last = count if mode == 'corrected' else count - 1
    for i in range(1, last + 1):
        e = ent(i)
        if e == caller:
            continue
        if ((S.g(e + 0x10) - 2) & M) >= 2:
            continue
        if S.g(e + 0x20) != 0:
            continue
        if (S.g(e + 0x6C) & mask) == 0:
            continue
        if S.g(e + 0x1D0) != 0 or S.g(e + 0x1CC) != 0:
            continue
        if S.g(e + 0xB4) & 0x40:
            continue
        rec = S.g(e + 0x1C4)
        if rec == 0:
            continue
        if (S.b(rec + attr + 2) & 0xC0) == 0x80:
            continue
        dx = s32((S.g(e + 0x114) - S.g(caller + 0x114)) & M) >> 16
        dy = s32((S.g(e + 0x118) - S.g(caller + 0x118)) & M) >> 16
        keys.append((dx * dx + dy * dy) & M)
        ptrs.append(e)
    thr = 2 if mode == 'corrected' else 3
    if len(keys) >= thr:
        hoare(keys, ptrs, 0, len(keys) - 1)
    return keys, ptrs


def turn_binary(cur, tgt):
    """0x8007AD6C-0x8007ADB8: numeric comparison, never wraps, step 4 (the defect Q-H4-2)"""
    if cur == tgt:
        return None
    if tgt < cur:
        return tgt if (cur - tgt) < 4 else cur - 4
    return tgt if (tgt - cur) < 4 else cur + 4


def turn_corrected(cur, tgt):
    """INFERENCE (the author's rule: a proven defect of the original is corrected): the same law, but through the shortest way of the 32-direction circle when the numeric gap exceeds 16;
    equal to the binary on every pair with |tgt - cur| <= 16 (784 pairs of 1024)."""
    if cur == tgt:
        return None
    d = tgt - cur
    if abs(d) <= 16:
        return turn_binary(cur, tgt)
    # the other way round: the signed gap on the circle
    d2 = d - 32 if d > 0 else d + 32
    if abs(d2) < 4:
        return tgt
    return (cur + (4 if d2 > 0 else -4)) & 31


def c63(S, a0, dirto, mode='binary'):
    """dirto(dx, dy) -> direction 0..31 (the real 0x8002E814 in the generators) ; returns nothing, writes through S"""
    d = S.g(a0 + 0x274)
    if d != 0:
        S.p(a0 + 0x274, (d - 1) & M)
        return
    tp = S.g(a0 + 0x278)
    S.p(a0 + 0xB8, 0xFFFD0000)
    if tp == 0:
        atk = S.g(a0 + 0x1C8)
        attr = S.b(atk) & 0xF
        keys, ptrs = query(S, a0, 1, attr, mode)
        n = len(keys)
        S.query_calls.append(n)
        for k in range(n):
            S.p(PTRS + 4 * k, ptrs[k])
            S.p(KEYS + 4 * k, keys[k])
        if n == 0:
            S.p(a0 + 0x274, draw(S, 9))
            return
        tp = ptrs[0]
        S.p(a0 + 0x278, tp)
        S.p(a0 + 0x27C, S.g(tp + 4))
    else:
        if S.g(tp + 0x10) != 2 or S.g(tp + 4) != S.g(a0 + 0x27C):
            S.p(a0 + 0x278, 0)
            return
    dx = (S.g(tp + 0x114) - S.g(a0 + 0x114)) & M
    dy = (S.g(tp + 0x118) - S.g(a0 + 0x118)) & M
    t = dirto(dx, dy)
    cur = S.g(a0 + 0x8C)
    if mode == 'corrected':
        nd = turn_corrected(cur, t) if cur < 32 and t < 32 else turn_binary(cur, t)
    else:
        nd = turn_binary(cur, t)
    if nd is None:
        return
    S.p(a0 + 0x8C, nd & M)
    S.p(a0 + 0x274, 2)


def e3(S, a0):
    if S.g(a0 + 0x88) == 1 and S.g(a0 + 0xAC) == 1:
        S.j.append(('destroy', a0, M))
    else:
        S.p(a0 + 0x88, 1)
        S.p(a0 + 0x6C, ((S.g(a0 + 0x6C) | 0x40) & (0xFFFFFFCF)) & M)
