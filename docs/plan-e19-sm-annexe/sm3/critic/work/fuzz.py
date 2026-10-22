import random
import sys
import collections
import models as md
import replay_mips as rp


def rworld(rng, free_ok=True, zero_id=False):
    n = rng.randint(2, 9)
    S = []
    for i in range(n):
        if i == 0:
            st = rng.choice([2, 2, 2, 3, 1])
            ref = -1
        else:
            st = rng.choice([0, 1, 2, 2, 3, 3, 4, 5] if free_ok else [1, 2, 2, 3, 3, 4, 5])
            ref = rng.choice([1, 2, 3, 4, 5, 6, 7, 8, 24, 24, 0x7F, 3, 4] + ([0] if zero_id else []))

        def ptr():
            return rng.choice([None, None] + list(range(n)))
        S.append(dict(status=st, ref_id=ref, flags=rng.choice([0, 0x80, 0x80, 0x100, 0x180]), anim_flags=rng.choice([0, 0, 0x80]),
                      force_z=rng.choice([0, 0, 1, -1, 5, -5, 0x7FFFFFFF, -0x80000000, 0x10000, -0x10000, 0xFFFF, -0xFFFF, 0x8000, 0x100, -0x100]),
                      target_anim=rng.choice([0, 1, 2, 0x41, 0x141, 0xFF, 0xFFFFFFFF, 0x100, 9]),
                      platform=ptr(), riding=ptr(), xcoll=ptr(), parent=ptr()))
    for i in range(1, n):
        if S[i]['status'] == 0:
            S[i].update(ref_id=0, flags=0, anim_flags=0, force_z=0, target_anim=0, platform=None, riding=None, xcoll=None, parent=None)
    for i in range(n):
        for k in ('platform', 'riding', 'xcoll', 'parent'):
            j = S[i][k]
            if j is not None and S[j]['status'] == 0:
                S[i][k] = None
    N = rng.choice([n - 1, n - 1, n - 1, rng.randint(1, n - 1)])
    ref = rng.choice([0] + list(range(1, n)) * 2)
    if S[ref]['status'] == 0:
        ref = 0
    return dict(n_last=N, slots=S, ref=ref)


def mk_vec(w, instr, rb=7):
    return dict(op='0x%02X' % instr[0], instr=['0x%02X' % b for b in instr], ref=w['ref'], n_last=w['n_last'], slots=w['slots'], result_before=rb)


def main(seed, N, zero_id=False):
    rng = random.Random(seed)
    tg = [0x80 + i for i in range(12)]
    raw = [3, 4, 5, 6, 8, 24, 1, 2, 0x7F]
    stat = collections.Counter()
    dis = collections.defaultdict(list)
    for it in range(N):
        w = rworld(rng, zero_id=zero_id)
        for op in (0xA9, 0xAA, 0xB2, 0xB4, 0xB5, 0xB6, 0xB7):
            t1 = rng.choice(tg + raw)
            instr = [op, t1]
            if op == 0xB2:
                instr.append(rng.choice(tg + raw))
            if op == 0xB7:
                instr.append(rng.choice([0, 1, 2, 9, 0x41, 0xFF]))
            vec = mk_vec(w, instr)
            v0, res, mem = rp.run(vec)
            mb = md.handlers(md.bin_search, w, w['ref'], instr)
            md_ = md.handlers(md.dll_search, w, w['ref'], instr)
            stat[(op, 'n')] += 1
            if res != mb:
                stat[(op, 'MIPS!=mybin')] += 1
                dis[(op, 'mybin')].append((w, instr, res, mb))
            if res != md_:
                stat[(op, 'MIPS!=dll')] += 1
                dis[(op, 'dll')].append((w, instr, res, md_))
    for k in sorted(stat):
        print(k, stat[k])
    return dis


if __name__ == '__main__':
    dis = main(int(sys.argv[1]), int(sys.argv[2]), zero_id=(len(sys.argv) > 3))
    for k, v in dis.items():
        print(k, len(v))
        for w, instr, res, mm_ in v[:2]:
            print("  instr", [hex(x) for x in instr], "MIPS", res, "model", mm_, "ref", w['ref'], "N", w['n_last'])
            for i, s in enumerate(w['slots']):
                print("    ", i, s)
