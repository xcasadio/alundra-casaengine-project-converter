"""Critic's own mutation run: the rules R1..R5 in DLL terms (dllmodel.py) plus each mutant of the preparation's list (named by their
descriptions), run against (a) the 43 value tests of named-tests-audit.json (their `start` and `expected`) and (b) the 348 vectors.
A mutant is killed by a test when its final fields differ from the test's `expected`."""
import json, sys, copy
import dllmodel as D

SM4 = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm4'
I32, U32 = D.I32, D.U32


def clamp16(x):
    return max(-32768, min(32767, x))


def apply_mut(v, mut):
    prox = D.world_from(v)
    st = dict(v['world']['stats'])
    pad = dict(v['world']['pad']) if v['world'].get('pad') else None
    ins = v['instruction']
    op = ins[0]
    b = list(ins) + [0] * 12
    logic = prox[v['logicSlot']]
    owner = prox[v['ownerSlot']]
    player = prox[0]
    if op == 0x72:
        t = owner if mut == 'M72b' else logic
        if mut == 'M72d' and not (1 <= t.status <= 3):
            pass
        elif mut == 'M72c':
            t.tanim, t.tdir = t.lanim, t.ldir
        else:
            t.lanim = t.tanim
            if mut != 'M72a':
                t.ldir = t.tdir
    elif op == 0x88:
        ref = owner if mut == 'M88f' else logic
        ms = D.search(ref, b[1], prox, player)
        idx = b[2]
        table = [3, 2, 4] if mut == 'M88a' else [2, 3, 4]
        if idx >= 3:
            if mut == 'M88e':
                val = 0
            else:
                val = None
        else:
            val = table[idx]
        if val is not None:
            if mut == 'M88c':
                ms = ms[:1]
            for m in ms:
                m.status = val
    elif op == 0x96:
        amount = b[2] if mut == 'M96a' else b[1]
        for m in D.search(logic, b[1], prox, player):
            if mut == 'M96f' and not (1 <= m.status <= 3):
                continue
            if mut == 'M96g' and m.hp == 0:
                continue
            if mut == 'M96e':
                total = m.hp + amount
                m.hp = min(m.hpMax, total)
                continue
            total = I32(m.hp + amount)
            normal = m.hpMax if m.hpMax < total else total
            if mut == 'M96b':
                m.hp = total
            elif mut == 'M96c':
                m.hp = max(0, normal)
            elif mut == 'M96d':
                m.hp = m.hp if m.hp > m.hpMax else normal
            else:
                m.hp = normal
    elif op == 0x97:
        if mut == 'M97a':
            # spends money: SpendMoney(v1): money - v1 clamped to [0, 9999] (decompilation)
            nm = st['money'] - b[1]
            st['money'] = 0 if nm < 0 else min(nm, 9999)
        else:
            s = st['mp'] + b[1]
            if mut == 'M97b':
                s = ((s + 32768) % 65536) - 32768  # cast to 16 bits instead of saturating
            else:
                s = clamp16(s)
            cap = 4 if mut == 'M97e' else st['mpMax']
            if mut == 'M97c':
                nv = cap if cap < s else s
            elif mut == 'M97d':
                nv = 0 if s < 0 else s
            else:
                nv = cap if cap < s else (0 if s < 0 else s)
            st['mp'] = nv
    elif op == 0xB3:
        w = lambda lo, hi: b[lo] | b[hi] << 8
        if mut == 'MB3a':
            pad = dict(pad); pad['hold'] = w(1, 2)
        elif mut == 'MB3b':
            pad = dict(hold=pad['hold'] | w(1, 2), pressed=pad['pressed'] | w(3, 4), released=pad['released'] | w(5, 6), interval=pad['interval'] | w(7, 8))
        elif mut == 'MB3c':
            be = lambda lo, hi: b[hi] | b[lo] << 8
            pad = dict(hold=be(1, 2), pressed=be(3, 4), released=be(5, 6), interval=be(7, 8))
        elif mut == 'MB3d':
            pad = dict(hold=w(1, 2), pressed=w(5, 6), released=w(3, 4), interval=w(7, 8))
        elif mut == 'MB3e':
            pad = dict(hold=w(1, 2) & 0xFF, pressed=w(3, 4) & 0xFF, released=w(5, 6) & 0xFF, interval=w(7, 8) & 0xFF)
        else:
            pad = dict(hold=w(1, 2), pressed=w(3, 4), released=w(5, 6), interval=w(7, 8))
    return prox, st, pad


MUTS = {
    0x72: ['M72a', 'M72b', 'M72c', 'M72d'],
    0x88: ['M88a', 'M88c', 'M88e', 'M88f'],
    0x96: ['M96a', 'M96b', 'M96c', 'M96d', 'M96e', 'M96f', 'M96g'],
    0x97: ['M97a', 'M97b', 'M97c', 'M97d', 'M97e'],
    0xB3: ['MB3a', 'MB3b', 'MB3c', 'MB3d', 'MB3e'],
}


def fields(prox, st, pad):
    out = {}
    for p in prox:
        for k in ('status', 'hp', 'hpMax', 'tanim', 'tdir', 'lanim', 'ldir'):
            out['%d.%s' % (p.i, k)] = getattr(p, k)
    for k, val in st.items():
        out['stats.' + k] = val
    if pad:
        for k, val in pad.items():
            out['pad.' + k] = val
    return out


def main():
    audit = json.load(open(SM4 + '/named-tests-audit.json'))
    res = {}
    for e in audit:
        if e['binaryAudit'] != 'ok':
            continue
        st = e['start']
        ins = list(e['instruction'])
        if ins[-1] == 0xFF:
            ins = ins[:-1]
        v = dict(world=dict(slotCount=len(st['slots']), slots=st['slots'], stats=st['stats'], pad=st.get('pad')),
                 instruction=ins, logicSlot=e['logicSlot'], ownerSlot=e['ownerSlot'])
        op = ins[0]
        # the baseline (no mutant) must satisfy `expected`
        for mut in [None] + MUTS[op]:
            prox, stt, pad = apply_mut(v, mut)
            f = fields(prox, stt, pad)
            ok = True
            for key, val in e['expected'].get('slot', {}).items():
                i, fld = key.split('.')
                a = {'lastTargetAnimId': 'lanim', 'lastTargetDir': 'ldir', 'targetAnimId': 'tanim', 'targetDir': 'tdir'}.get(fld, fld)
                if U32(f['%s.%s' % (i, a)]) != U32(val):
                    ok = False
            for fld, val in e['expected'].get('stats', {}).items():
                if f['stats.' + fld] != val:
                    ok = False
            for fld, val in e['expected'].get('pad', {}).items():
                if f['pad.' + fld] != val:
                    ok = False
            if mut is None:
                if not ok:
                    print('BASELINE FAILS', e['id'])
            else:
                res.setdefault(mut, []).append((e['id'], not ok))
    mine = {m: sorted(t for t, k in lst if k) for m, lst in res.items()}
    theirs = json.load(open(SM4 + '/mutants-matrix.json'))
    print('mutant: killers(mine) | equal to prep?')
    for m in sorted(mine):
        t = sorted(theirs[m]['killedByUnitTests'])
        print(m, mine[m], '==' if mine[m] == t else 'DIFF prep=%s' % t)
    unk = [m for m in mine if not mine[m]]
    print('mutants killed by NO named unit test (mine):', unk)
    # vectors
    kv = {}
    for opn in ['72', '88', '96', '97', 'B3']:
        doc = json.load(open(SM4 + '/vectors/op%s.json' % opn))
        for v in doc['vectors']:
            if v.get('outOfTable'):
                continue
            prox0, st0, pad0 = apply_mut(v, None)
            f0 = fields(prox0, st0, pad0)
            op = v['instruction'][0]
            for mut in MUTS[op]:
                prox, stt, pad = apply_mut(v, mut)
                f = fields(prox, stt, pad)
                kv.setdefault(mut, [0, 0])
                kv[mut][1] += 1
                if f != f0:
                    kv[mut][0] += 1
    print('vector rows killing (mine) vs prep:')
    for m in sorted(kv):
        print(m, '%d/%d' % tuple(kv[m]), 'prep', theirs[m]['killedByVectorRows'])


main()
