"""b3_query.py - brick (3): the target query 0x8003AF70 and its sort 0x8003AE04.
Extends the critic's own model (e20-h4/check/o/my_query.py: written from the disassembly, 0 mismatch there) to the TM machine with replayable vectors
and a larger mutant set.  Signature read in ../out/dis_8003AE04.txt:
   query(a0 = caller entity, a1 = mask on ent+0x6C, a2 = attack type (index in the balance record), a3 = out pointer array, [sp+0x10] = out key array) -> n
   sort (a0 = pointer array, a1 = key array, a2 = lo, a3 = hi), unsigned keys, Hoare, called only for n >= 3.
usage: python -B b3_query.py [N] [seed]"""
import json, os, random, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import vtrace as V
from machine import s32, M32

OUT = os.path.join(HERE, '..', 'vec')
ENT0, STRIDE = 0x80127D30, 0x294
COUNT = 0x80134360
PTRS, DIST = 0x800CC058, 0x800CC158           # the real global scratch arrays used by the four callers
SHEET = 0x801E7000                            # balance records (0x20 bytes each in this harness; the real ones are 14+ bytes of responses at +2)


def ent(i):
    return ENT0 + STRIDE * i


def u32(x):
    return x & M32


def hoare(keys, ptrs, lo, hi, mut=None):
    """the sort read at 0x8003AE04: pivot = (key[lo] + key[hi]) mod 2^32 >> 1, unsigned compares, crossing scans, swap, recurse (lo, i-1) and (j+1, hi)"""
    i, j = lo, hi
    pivot = (u32(keys[hi] + keys[lo])) >> 1
    lt = (lambda a, b: s32(a) < s32(b)) if mut == 'signed_cmp' else (lambda a, b: u32(a) < u32(b))
    while True:
        while lt(keys[i], pivot):
            i += 1
        while lt(pivot, keys[j]):
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
        hoare(keys, ptrs, lo, i, mut)
    if j + 1 < hi:
        hoare(keys, ptrs, j + 1, hi, mut)


def model(m, caller, mask, attr, count, mut=None):
    keys, ptrs = [], []
    last = count if mut == 'inclusive' else count - 1
    for i in range(1, last + 1):
        e = ent(i)
        if e == caller and mut != 'caller_in':
            continue
        st = u32(m.peek(e + 0x10))
        if mut == 'status2only':
            if st != 2:
                continue
        elif u32(st - 2) >= 2:
            continue
        if mut != 'ign20' and m.peek(e + 0x20) != 0:
            continue
        if mut == 'mask_eq':
            if (m.peek(e + 0x6C) & mask) != mask:
                continue
        elif (m.peek(e + 0x6C) & mask) == 0:
            continue
        if mut != 'ign1d0' and m.peek(e + 0x1D0) != 0:
            continue
        if mut != 'ign1cc' and m.peek(e + 0x1CC) != 0:
            continue
        if mut != 'ignb4' and m.peek(e + 0xB4) & 0x40:
            continue
        rec = m.peek(e + 0x1C4)
        if rec == 0:
            continue
        r = m.rb(rec + attr + 2) & 0xC0
        if mut == 'immune_any':
            if r != 0:
                continue
        elif r == 0x80:
            continue
        sh = 0 if mut == 'nodist_shift' else 16
        dx = s32(u32(m.peek(e + 0x114) - m.peek(caller + 0x114))) >> sh
        dy = s32(u32(m.peek(e + 0x118) - m.peek(caller + 0x118))) >> sh
        keys.append(u32(dx * dx + dy * dy))
        ptrs.append(e)
    thr = 2 if mut == 'sort_from2' else 3
    if mut == 'stable':
        order = sorted(range(len(keys)), key=lambda k: keys[k]) if len(keys) >= 3 else range(len(keys))
        keys, ptrs = [keys[k] for k in order], [ptrs[k] for k in order]
    elif len(keys) >= thr:
        hoare(keys, ptrs, 0, len(keys) - 1, mut)
    return keys, ptrs


MUTS = ['inclusive', 'sort_from2', 'status2only', 'ign20', 'ign1d0', 'ign1cc', 'ignb4', 'mask_eq', 'immune_any', 'signed_cmp', 'nodist_shift', 'stable', 'caller_in']


def main(N, seed):
    rnd = random.Random(seed)
    m = V.TM()
    m.cov.clear()
    mism, ties, bigcount = 0, 0, 0
    killed = {k: 0 for k in MUTS}
    vecs = []
    for n in range(N):
        for b in range(0, 16 * 0x20):
            m.poke8(SHEET + b, 0)
        for k in range(16):
            for t in range(0, 14):
                m.poke8(SHEET + 0x20 * k + 2 + t, rnd.choice([0x10, 0x10, 0x20, 0x80, 0xC0, 0x40, 0x00, 0x8F, 0x90]))
        cnt = rnd.choice([0, 1, 2, 3, 4, 5, 7, 9, 12, 20, 40, 63])
        m.poke(COUNT, cnt)
        caller = ent(rnd.choice([0, 0, rnd.randrange(0, 64)]))
        wide = n % 10 == 9
        hx, hy = rnd.randrange(100 << 16, 300 << 16), rnd.randrange(100 << 16, 300 << 16)
        m.poke(caller + 0x114, hx)
        m.poke(caller + 0x118, hy)
        for i in range(1, 64):
            e = ent(i)
            if e == caller:
                continue
            m.poke(e + 0x10, rnd.choice([2, 2, 2, 3, 3, 0, 1, 4]))
            m.poke(e + 0x20, 0 if rnd.random() < 0.9 else 7)
            m.poke(e + 0x6C, rnd.choice([1, 1, 1, 0, 5, 9, 0x101]))
            m.poke(e + 0x1D0, 0 if rnd.random() < 0.9 else 1)
            m.poke(e + 0x1CC, 0 if rnd.random() < 0.9 else 1)
            m.poke(e + 0xB4, 0 if rnd.random() < 0.9 else 0x40)
            m.poke(e + 0x1C4, 0 if rnd.random() < 0.05 else SHEET + 0x20 * rnd.randrange(16))
            ox = rnd.choice([0, 8, 16, 24, 32, -8, -16, 40, 64, 160, 161, -200])
            oy = rnd.choice([0, 8, 16, 24, 32, -8, -16, 40, 64, 0, 1])
            if wide:                                   # not reachable in the game (positions are within 0..1248 x 0..960 px): keys >= 2^31 to separate signed/unsigned
                ox = rnd.choice([ox, 30000, -30000, 32000]); oy = rnd.choice([oy, 30000, -31000])
            m.poke(e + 0x114, (hx + (ox << 16) + rnd.choice([0, 0, 0, 0x4000])) & M32)
            m.poke(e + 0x118, (hy + (oy << 16)) & M32)
        mask = rnd.choice([1, 1, 1, 4, 5])
        attr = rnd.choice([8, 9, 10, 11, 11, 0, 3])
        keys, ptrs = model(m, caller, mask, attr, cnt)
        for k in range(64):
            m.poke(PTRS + 4 * k, 0xDEAD0000 + k)
            m.poke(DIST + 4 * k, 0xBEEF0000 + k)
        m.poke(0x801FFF10, DIST)
        vec = V.run(m, 0x8003AF70, (caller, mask, attr, PTRS))
        ret = s32(vec["ret"])
        rptrs = [m.peek(PTRS + 4 * k) for k in range(max(ret, 0))]
        rkeys = [m.peek(DIST + 4 * k) for k in range(max(ret, 0))]
        if len(set(keys)) < len(keys):
            ties += 1
        if cnt >= 20:
            bigcount += 1
        if ret != len(keys) or rptrs != ptrs or rkeys != keys:
            mism += 1
            if mism <= 5:
                print('MISMATCH vec', n, 'count', cnt, 'real', ret, 'model', len(keys))
        for mu in MUTS:
            try:
                k2, p2 = model(m, caller, mask, attr, cnt, mut=mu)
            except (RecursionError, IndexError):
                k2, p2 = None, None            # a mutated model that does not terminate / runs off the array: distinguishable
            if k2 != keys or p2 != ptrs:
                killed[mu] += 1
        if n < 120:
            vecs.append({"id": n, "mask": mask, "attr": attr, "caller": "%#010x" % caller, **vec})
    def cov(a, b):
        return len([p for p in range(a, b, 4) if p in m.cov]), (b - a) // 4
    print('vectors', N, 'seed', seed, 'mismatches', mism, '| vectors with tied keys', ties, '| count>=20', bigcount)
    print('coverage query 0x8003AF70..0x8003B0E8: %d/%d' % cov(0x8003AF70, 0x8003B0E8))
    print('coverage sort  0x8003AE04..0x8003AF70: %d/%d' % cov(0x8003AE04, 0x8003AF70))
    print('mutants distinguished by the vectors (of %d):' % N, killed)
    survivors = [k for k, v in killed.items() if v == 0]
    print('mutants never distinguished:', survivors)
    bad = 0
    for v in vecs:
        ok, msg = V.replay(v)
        if not ok:
            bad += 1
            if bad <= 3:
                print('REPLAY FAIL', v['id'], msg)
    print('replay from empty memory: %d/%d vectors reproduce the writes and the return value' % (len(vecs) - bad, len(vecs)))
    json.dump({"function": "0x8003AF70", "note": "target query; out arrays at 0x800CC058 (pointers) and 0x800CC158 (keys); 5th argument at [sp+0x10]=0x801FFF10; writes include the sorted arrays", "vectors": vecs}, open(os.path.join(OUT, 'vec_query_8003AF70.json'), 'w'))
    return mism


if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 1500
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261018
    os.makedirs(OUT, exist_ok=True)
    sys.exit(1 if main(N, seed) else 0)
