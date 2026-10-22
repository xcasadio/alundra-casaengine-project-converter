"""Critic's own fuzz: fresh random worlds (not the preparation's), the REAL handlers on the critic's interpreter versus the critic's
DLL-term model of rules R1..R5 (dllmodel.apply).  Usage: python -B fuzz_critic.py N SEED"""
import random, sys
import cmips, replay_critic as R, dllmodel_neg as D

N = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
SEED = int(sys.argv[2]) if len(sys.argv) > 2 else 424242
rng = random.Random(SEED)
m = cmips.Machine()


def rslot(i, n, hero):
    def ptr():
        r = rng.random()
        if r < 0.5:
            return -1
        return rng.randrange(0, n)
    return dict(
        status=rng.choice([0, 1, 2, 2, 2, 3, 3, 4, 5]) if not hero else rng.choice([1, 2, 2, 3, 4]),
        hp=rng.choice([0, 1, 5, 10, 30, 50, 120, -7, 0x7FFFFFF0, -0x80000000, 255]),
        hpMax=rng.choice([1, 5, 10, 30, 50, 51, 0, -3, 60]),
        refId=-1 if hero else rng.choice([0, 1, 2, 3, 4, 5, 6, 7, 20, 0x7F, 8, 9, 10, 11]),
        flags=rng.choice([0, 0x80, 0x80, 0x100, 0x4100, 0x180, 0x1, 0xFFFFFFFF]),
        animFlags=rng.choice([0, 0x80, 0x40, 0xC0, -1, 0x7F]),
        hasPlatform=rng.random() < 0.3,
        parentSlot=ptr(), ridingSlot=ptr(), xCollSlot=ptr(),
        targetAnimId=rng.choice([0, 1, 2, 9, 0x41, 0xFF, 0xFFFFFFFF, 0x80000000]),
        targetDir=rng.choice([0, 1, 5, 31, 32, 0xFFFFFFFF]),
        lastTargetAnimId=rng.choice([0, 7, 0xFFFFFFFF]), lastTargetDir=rng.choice([0, 3, 0x1F]),
    )


bad = 0
for it in range(N):
    n = rng.randint(1, 9)
    slots = [rslot(i, n, i == 0) for i in range(n)]
    st = dict(hp=rng.choice([0, 10, 50, -5]), hpMax=rng.choice([0, 10, 50]), mp=rng.choice([-32768, -3, -1, 0, 1, 2, 3, 4, 5, 100, 32767]),
              mpMax=rng.choice([-5, -1, 0, 1, 2, 3, 4, 5, 100, 32767]), money=rng.choice([0, 500, 9999]))
    op = rng.choice([0x72, 0x88, 0x96, 0x97, 0xB3])
    if op == 0x72:
        ins = [0x72]
    elif op == 0x88:
        t = rng.choice(list(range(0x80, 0x8C)) + [rng.randrange(0, 12)])
        ins = [0x88, t, rng.choice([0, 1, 2])]
    elif op == 0x96:
        t = rng.choice(list(range(0x80, 0x8C)) + [rng.randrange(0, 12), 20, 0x7F])
        ins = [0x96, t, rng.randrange(0, 256)]
    elif op == 0x97:
        ins = [0x97, rng.choice([0, 1, 2, 3, 4, 5, 10, 50, 127, 128, 200, 255])]
    else:
        ins = [0xB3] + [rng.randrange(0, 256) for _ in range(8)]
    pad = dict(hold=rng.randrange(0, 65536), pressed=rng.randrange(0, 65536), released=rng.randrange(0, 65536), interval=rng.randrange(0, 65536)) if op == 0xB3 else None
    v = dict(world=dict(slotCount=n, slots=slots, stats=st, pad=pad), instruction=ins, logicSlot=rng.randrange(0, n), ownerSlot=rng.randrange(0, n))
    h, ret, pre, post, stores, sr = R.run(m, v)
    size, prox, st2, pad2 = D.apply(v)
    ok = ret == size
    for i, p in enumerate(prox):
        for fld, attr in dict(status='status', hp='hp', hpMax='hpMax', targetAnimId='tanim', targetDir='tdir', lastTargetAnimId='lanim', lastTargetDir='ldir').items():
            off, sz, sg = R.F[fld]
            got = m.rw(R.slot_addr(i) + off)
            if (got & cmips.M) != (getattr(p, attr) & cmips.M):
                ok = False; print('mismatch', it, hex(op), ins, 'slot', i, fld, 'binary', got, 'dll-model', getattr(p, attr))
    for k, (off, sg) in R.STATF.items():
        got = cmips.s16(m.rh(R.STATS + off))
        if got != st2[k]:
            ok = False; print('mismatch', it, hex(op), ins, 'stats', k, 'binary', got, 'dll-model', st2[k])
    if op == 0xB3:
        for k, off in R.PADF.items():
            if m.rh(R.PAD + off) != pad2[k]:
                ok = False; print('mismatch pad', k)
    if not ok:
        bad += 1
        if bad > 15:
            break
print('fuzz', N, 'seed', SEED, 'mismatches', bad)
