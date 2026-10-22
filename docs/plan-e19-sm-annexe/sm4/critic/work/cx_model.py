"""Critic's own model of the five handlers (from the capstone listing read by me) + random differential test against my interpreter.
Usage: python -B cx_model.py [N] [seed]"""
import random, sys, collections
import cx_exe as X
import cx_world as W
from cx_mips import M32, s32, s16

STATUS_TABLE = [2, 3, 4]


def s32m(v):
    return s32(v)


def active(st):
    return 1 <= (st & M32) <= 3 if True else False


def search(t, logic, E, N):
    """E: list of slot dicts (my keys). returns list of slot indices in buffer order. N = slotCount-1 (as read by binary)."""
    ok = lambda i: 1 <= E[i]['status'] <= 3
    ptr = lambda i: W.ent(i)
    out = []
    if (t & 0x80) == 0:
        if N <= 0:
            return out
        if ok(logic):
            for i in range(1, N + 1):
                if E[i]['refId'] == t - 0 or (E[i]['refId'] & M32) == t:
                    out.append(i)
        return out
    f = t & 0x7F
    if f == 0:
        return [logic]
    if f == 1:
        return [0]
    if f >= 12:
        raise RuntimeError('fatal')
    rng0 = range(0, N + 1) if N >= 0 else []
    rng1 = range(1, N + 1) if N >= 1 else []
    if f == 2:
        return [i for i in rng0 if ok(i)]
    if f == 3:
        return [i for i in rng1 if ok(i)]
    if f == 4:
        if not ok(logic):
            return []
        return [i for i in rng0 if (E[i]['flags'] & 0x80) and not (E[i]['animFlags'] & 0x80) and E[i]['hasPlatform'] == 0]
    P = lambda s: 0 if s < 0 else W.ent(s)
    if f == 5:
        return [i for i in rng1 if ok(i) and P(E[logic]['ridingSlot']) == ptr(i)]
    if f == 6:
        return [i for i in rng1 if ok(i) and P(E[i]['ridingSlot']) == ptr(logic)]
    if f == 7:
        return [i for i in rng1 if ok(i) and P(E[logic]['xCollSlot']) == ptr(i)]
    if f == 8:
        return [i for i in rng1 if ok(i) and P(E[i]['xCollSlot']) == ptr(logic)]
    if f == 9:
        return [i for i in rng1 if ok(i) and P(E[i]['parentSlot']) == ptr(logic)]
    if f == 10:
        return [i for i in rng1 if ok(i) and P(E[logic]['parentSlot']) == ptr(i)]
    if f == 11:
        return [i for i in rng1 if ok(i) and E[i]['hasPlatform']]
    raise RuntimeError


def m72(E, S, ins, logic):
    E[logic]['lastAnim'] = E[logic]['targetAnimId']
    E[logic]['lastDir'] = E[logic]['targetDir']
    return 1


def m88(E, S, ins, logic):
    N = len(E) - 1
    idx = ins[2]
    word = STATUS_TABLE[idx] if idx < 3 else None
    if word is None:
        raise KeyError('out of table')
    for i in search(ins[1], logic, E, N):
        E[i]['status'] = word
    return 3


def m96(E, S, ins, logic):
    N = len(E) - 1
    for i in search(ins[1], logic, E, N):
        s = s32(E[i]['hp'] + ins[1])
        E[i]['hp'] = E[i]['hpMax'] if E[i]['hpMax'] < s else s
    return 3


def m97(E, S, ins, logic):
    a = S['mp'] + ins[1]
    if S['mpMax'] < a:
        S['mp'] = S['mpMax']
    elif a < 0:
        S['mp'] = 0
    else:
        S['mp'] = s16(a)
    return 2


def mB3(E, S, ins, logic, pad):
    pad['hold'] = ins[1] | ins[2] << 8
    pad['pressed'] = ins[3] | ins[4] << 8
    pad['released'] = ins[5] | ins[6] << 8
    pad['interval'] = ins[7] | ins[8] << 8
    return 9


def random_slots(rng, n):
    out = []
    for i in range(n):
        d = dict(status=rng.choice([0, 1, 2, 2, 3, 3, 4, 5, 6]) if i else rng.choice([2, 3]),
                 hp=rng.choice([0, 1, 5, 10, 50, -7, 0x7FFFFFFF, -0x80000000, 1000]),
                 hpMax=rng.choice([0, 1, 10, 50, -1, 0x7FFFFFFF, -100]),
                 refId=(-1 if i == 0 else rng.choice([0, 1, 2, 3, 20, 127, -1])),
                 flags=rng.choice([0, 0x80, 0x80, 0x100, 0x180]),
                 animFlags=rng.choice([0, 0x80, 0x40, -1]),
                 hasPlatform=rng.choice([0, 1]),
                 parentSlot=rng.choice([-1] + list(range(n))),
                 ridingSlot=rng.choice([-1] + list(range(n))),
                 xCollSlot=rng.choice([-1] + list(range(n))),
                 targetAnimId=rng.choice([0, 1, 7, 0xFF, 0xFFFFFFFF]),
                 targetDir=rng.choice([0, 3, 31, 0xFFFFFFFF]),
                 lastTargetAnimId=rng.choice([0, 9, 0xFFFFFFFF]),
                 lastTargetDir=rng.choice([0, 5, 0xFFFFFFFF]))
        out.append(d)
    return out


def main(N, seed):
    rng = random.Random(seed)
    tot = collections.Counter()
    for it in range(N):
        n = rng.randint(1, 9)
        slots = random_slots(rng, n)
        for s in slots:
            s['parentSlot'] = s['parentSlot'] if s['parentSlot'] < n else -1
            s['ridingSlot'] = s['ridingSlot'] if s['ridingSlot'] < n else -1
            s['xCollSlot'] = s['xCollSlot'] if s['xCollSlot'] < n else -1
        stats = dict(hp=rng.randint(-32768, 32767), hpMax=rng.randint(-5, 50), mp=rng.choice([-32768, -3, 0, 2, 4, 32767]),
                     mpMax=rng.choice([-1, 0, 1, 4, 32767]), money=rng.randint(0, 9999))
        pad = dict(hold=rng.getrandbits(16), pressed=rng.getrandbits(16), released=rng.getrandbits(16), interval=rng.getrandbits(16))
        logic = rng.randrange(n)
        owner = rng.randrange(n)
        for op in (0x72, 0x88, 0x96, 0x97, 0xB3):
            if op == 0x72:
                ins = [0x72, 0xFF]
            elif op == 0x88:
                ins = [0x88, rng.choice([0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x8B, 0, 1, 2, 3, 20, 127]), rng.choice([0, 1, 2, 0, 1, 2, 3, 4, 255])]
            elif op == 0x96:
                ins = [0x96, rng.choice([0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x8B, 0, 1, 2, 3, 20, 127, 255, 200]), rng.getrandbits(8)]
            elif op == 0x97:
                ins = [0x97, rng.choice([0, 1, 2, 3, 4, 10, 50, 255, 128])]
            else:
                ins = [0xB3] + [rng.getrandbits(8) for _ in range(8)]
            ins = ins + [0xFF]
            # real
            try:
                sl = [dict(s) for s in slots]
                m, cpu, ret = W.run(op, sl, stats, ins, logic, owner, pad)
            except Exception as ex:
                tot['real-exc-' + str(ex)[:20]] += 1
                continue
            snap = W.snapshot(m, n)
            # model
            E = []
            for s in slots:
                e = dict(s)
                e['lastAnim'] = s['lastTargetAnimId']
                e['lastDir'] = s['lastTargetDir']
                for k in ('status', 'targetAnimId', 'targetDir', 'lastAnim', 'lastDir', 'flags'):
                    e[k] = e[k] & M32
                E.append(e)
            S = dict(stats)
            P = dict(pad)
            try:
                if op == 0x72:
                    r = m72(E, S, ins, logic)
                elif op == 0x88:
                    r = m88(E, S, ins, logic)
                elif op == 0x96:
                    r = m96(E, S, ins, logic)
                elif op == 0x97:
                    r = m97(E, S, ins, logic)
                else:
                    r = mB3(E, S, ins, logic, P)
            except KeyError:
                tot['model-outOfTable'] += 1
                continue
            except RuntimeError:
                tot['model-fatal'] += 1
                continue
            bad = []
            if r != ret:
                bad.append(('ret', r, ret))
            for i in range(n):
                for f in ('status', 'hp', 'hpMax', 'targetAnimId', 'targetDir', 'lastAnim', 'lastDir', 'refId', 'flags', 'animFlags'):
                    a = snap['slots'][i][f]
                    b = E[i][f]
                    if f in ('hp', 'hpMax', 'refId', 'animFlags'):
                        ok = a == s32(b)
                    else:
                        ok = (a & M32) == (b & M32)
                    if not ok:
                        bad.append((i, f, a, b))
            for k in ('mp', 'mpMax', 'hp', 'hpMax', 'money'):
                if snap['stats'][k] != S[k]:
                    bad.append(('stats', k, snap['stats'][k], S[k]))
            for k in P:
                if snap['pad'][k] != P[k]:
                    bad.append(('pad', k, snap['pad'][k], P[k]))
            tot['cases'] += 1
            tot['op%X' % op] += 1
            if bad:
                tot['BAD'] += 1
                if tot['BAD'] < 10:
                    print(hex(op), ins, 'logic', logic, bad[:4])
    print(dict(tot))


if __name__ == '__main__':
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 3000, int(sys.argv[2]) if len(sys.argv) > 2 else 424242)
