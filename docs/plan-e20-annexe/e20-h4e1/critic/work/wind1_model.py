"""wind1_model.py - h4e1 discovery: MY OWN Python model of the wind-1 driver 0x80035EB0 (with the guard 0x800351B8 and the query 0x8003AF70 + sort 0x8003AE04), written from the disassembly of
ALUN_CD.EXE (France) (docs/plan-e20-annexe/e20-h4/work/lst/drv_80035EB0.txt, f_8003AF70.txt, f_8003AE04_sort.txt, re-read with capstone) and nothing else.  It is compared with the REAL MIPS code by
wind1_vec.py (random states, 0 mismatch) and mutated by wind1_mut.py (a mutant is a name in the set `mut`).  It is the shape of the C# port.

State (all integers are Python ints; 32-bit wraps are explicit):
  phase, counter, item, last (the stored last-target POINTER of [0x8012712C]), rng,
  hero = dict(anim, slope, x, y, z)           (hero entity at 0x80127D30: +0x88, +0x18C, +0x114, +0x118, +0x11C)
  ents = {slot: dict(status, f20, flags, c1d0, c1cc, b4, fiche (pointer or 0), resp (the byte at fiche + 2 + 11), x, y, z)}  slots 1..; count = [0x80134360]
  spawn = dict(ret (pointer or 0), ground (the +0x144 word of the returned entity))
"""
import copy

M32 = 0xFFFFFFFF
HERO = 0x80127D30
STRIDE = 0x294
TABLE = [30, 40, 50, 60, 70, 80, 90, 100, 0xFFFF]
LCG_M, LCG_A = 0x7D2B89DD, 0xE06A02E7
NO_MUT = frozenset()


def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


def ptr(slot):
    return HERO + STRIDE * slot


def slot_of(p):
    return (p - HERO) // STRIDE


def lcg(s):
    return (s * LCG_M + LCG_A) & M32


def hoare(P, K, lo, hi, mut=NO_MUT):
    """0x8003AE04: in-place, unstable; pivot fixed at the entry; unsigned compares; swap only if i < j (signed)"""
    i, j = lo, hi
    pivot = ((K[lo] + K[hi]) & M32) >> 1
    if 'pivot_signed' in mut:
        pivot = (s32(K[lo] + K[hi])) >> 1 & M32
    while True:
        while K[i] < pivot:
            i += 1
        while pivot < K[j]:
            j -= 1
        if (i < j) if 'swap_le' not in mut else (i <= j):
            K[i], K[j] = K[j], K[i]
            P[i], P[j] = P[j], P[i]
            i += 1
            j -= 1
            continue
        break
    a = i - 1
    if lo < a:
        hoare(P, K, lo, a, mut)
    b = j + 1
    if b < hi:
        hoare(P, K, b, hi, mut)


def query(ents, count, caller, mask, typ, mut=NO_MUT):
    """0x8003AF70: returns (pointers, keys)"""
    P, K = [], []
    cx, cy = caller['x'], caller['y']
    last_slot = count if 'q_inclusive' in mut else count - 1
    for k in range(1, last_slot + 1):
        e = ents.get(k)
        if e is None:
            continue
        if 'q_status_2_only' in mut:
            if e['status'] != 2:
                continue
        elif 'q_status_1_to_3' in mut:
            if e['status'] not in (1, 2, 3):
                continue
        elif e['status'] not in (2, 3):
            continue
        if e['f20'] != 0:
            continue
        if (e['flags'] & mask) == 0:
            continue
        if e['c1d0'] != 0 and 'q_no_c1d0' not in mut:
            continue
        if e['c1cc'] != 0 and 'q_no_c1cc' not in mut:
            continue
        if e['b4'] & 0x40 and 'q_no_b4' not in mut:
            continue
        if e['fiche'] == 0 and 'q_no_fiche' not in mut:
            continue
        rb = e['resp10'] if 'q_type10' in mut else e['resp']
        if (rb & 0xC0) == (0xC0 if 'q_resp_c0' in mut else 0x80):
            continue
        dx = s32(e['x'] - cx) >> 16
        dy = s32(e['y'] - cy) >> 16
        key = (dx * dx + dy * dy) & M32
        if 'q_key_64' in mut:
            key = dx * dx + dy * dy
        P.append(ptr(k))
        K.append(key)
    n = len(P)
    if 'q_sort_stable' in mut:
        order = sorted(range(n), key=lambda i: K[i])
        P, K = [P[i] for i in order], [K[i] for i in order]
        return P, K
    thr = 2 if 'q_sort2' in mut else 3
    if n >= thr:
        hoare(P, K, 0, n - 1, mut)
    return P, K


def model(st, mut=NO_MUT):
    """one call of the driver; returns (ret, st') with st' = deep copy updated, plus st'['spawns'], ['destroys'], ['force_z'] (list of (pointer, value)), ['query'] ((P, K) or None)"""
    st = copy.deepcopy(st)
    st['spawns'], st['destroys'], st['force_z'], st['query'] = [], [], [], None
    hero = st['hero']

    def cancel():
        if st['item'] in (0x31, 0x2B):          # 0x800350C0: neutral entries (0x800351A0): only the release
            st['item'] = 0
        elif st['item'] == 0:
            pass
        else:
            raise NotImplementedError('item %#x' % st['item'])

    def guard():                                  # 0x800351B8
        if hero['slope'] == 4 and 'guard_no_slope' not in mut:
            cancel()
            return 1
        if hero['anim'] in (0x32, 0x33):
            return 0
        if 'guard_accepts_34' in mut and hero['anim'] == 0x34:
            return 0
        if 'guard_no_cancel' in mut:
            return 1
        cancel()
        return 1

    def spawn(parent, page, sprite, x, y, z, direction):
        st['spawns'].append(dict(args=[parent, page, sprite, x & M32, y & M32, z & M32, direction]))
        return st['spawn']['ret']

    def pass_z(e):
        lim = hero['z'] + (0x3C0000 if 'z_limit_60' in mut else 0x500000)
        if 'z_gate_off' in mut:
            return True
        if 'z_ge' in mut:
            return not (s32(lim) <= s32(e['z']))
        return not (s32(lim) < s32(e['z']))

    def strike():
        P, K = query(st['ents'], st['count'], hero, 2 if 'q_mask2' in mut else 1, 10 if 'q_type10' in mut else 11, mut)
        st['query'] = (list(P), list(K))
        chosen = 0
        for k in range(len(P)):
            e = st['ents'][slot_of(P[k])]
            if not pass_z(e):
                continue
            if P[k] == st['last'] and 'last_ignored' not in mut:
                continue
            near = (s32(K[k]) <= 0x6401) if 'near_le' in mut else ((K[k] & M32) < 0x6401 if 'near_unsigned' in mut else s32(K[k]) < 0x6401)
            if near:
                chosen = P[k]
                break
            if 'far_continues' in mut:
                continue
            break
        if chosen:
            e = st['ents'][slot_of(chosen)]
            zadd = 0x400000 if 'target_z_64' in mut else 0x800000
            ex, ey = (e['y'], e['x']) if 'target_xy_swapped' in mut else (e['x'], e['y'])
            r = spawn(HERO, 0, 0x16, ex, ey, e['z'] + zadd, 1 if 'dir_1' in mut else 0)
            if r:
                st['force_z'].append((r, 0xFFFC0000 if 'forcez_4' in mut else 0xFFF80000))
                if 'ground_check_in_target' in mut and st['spawn']['ground'] != 0:
                    st['force_z'].pop()
                    st['destroys'].append(r)
                    st['last'] = 0
                    return
            if r or 'last_only_if_spawned' not in mut:
                if 'last_not_set' not in mut:
                    st['last'] = chosen
            return
        s1 = lcg(st['rng'])
        if 'one_draw' in mut:
            s2 = s1
            st['rng'] = s1
        else:
            s2 = lcg(s1)
            st['rng'] = s2
        a, b = (s2, s1) if 'draw_order_swapped' in mut else (s1, s2)
        xs, ys = ((0x800001, 0xC00001) if 'scale_swapped' in mut else (0xC00001, 0x800001))
        x = (hero['x'] + ((a * xs) >> 32) + (0xFFA00000 if 'offset_wrong' not in mut else 0xFFC00000)) & M32
        y = (hero['y'] + ((b * ys) >> 32) + (0xFFC00000 if 'offset_wrong' not in mut else 0xFFA00000)) & M32
        z = (hero['z'] + (0x400000 if 'rand_z_64' in mut else 0x800000)) & M32
        r = spawn(HERO, 0, 0x16, x, y, z, 0)
        if r:
            if st['spawn']['ground'] != 0 and 'ground_check_off' not in mut:
                st['destroys'].append(r)
            else:
                st['force_z'].append((r, 0xFFF80000))
        if 'last_not_cleared_random' not in mut:
            st['last'] = 0

    def phase1():
        g = guard()
        if g:
            return g
        if hero['anim'] != 0x33 and 'gate_off' not in mut:
            return 0
        c = st['counter']
        if c < TABLE[0]:
            return 0
        i = 0
        while True:
            if (TABLE[i] == c) if 'fire_every_tick' not in mut else (TABLE[i] <= c and TABLE[i] != 0xFFFF and i == 0):
                strike()
            i += 1
            if not (c >= TABLE[i]):
                break
        if 'end_one_call_late' in mut:
            if c > 100 and TABLE[i] == 0xFFFF:
                hero['anim'] = 0x34
                st['phase'] += 1
        elif TABLE[i] == 0xFFFF:
            hero['anim'] = 0x34
            if 'end_no_phase' not in mut:
                st['phase'] += 1
        return 0

    ph = st['phase']
    if ph == 1:
        return phase1(), st
    if ph < 2:
        if ph != 0:
            return 0, st
        if 'phase0_keeps_last' not in mut:
            st['last'] = 0
        st['phase'] = 1
        return phase1(), st
    if ph == 2:
        if 'phase2_guard' in mut and guard():
            return 1, st
        return (1 if hero['anim'] != 0x34 else 0) if 'phase2_inverted' not in mut else (1 if hero['anim'] == 0x34 else 0), st
    return 0, st
