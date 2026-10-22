"""Critic's own mutation run on the 44 audited named unit tests (their DATA, my model).  A test = start world + bytes + expected fields +
size assertion.  A mutant is a variant of MY model of the handler (cx_model-like, rewritten here with switches).
Usage: python -B cx_mut.py <named-tests-audit.json>"""
import json, sys, copy
from cx_mips import M32, s32, s16

SIZE = {0x72: 1, 0x88: 3, 0x96: 3, 0x97: 2, 0xB3: 9}
TABLE = [2, 3, 4]


def active(st):
    return 1 <= (st & M32) <= 3


def search(t, logic, E, N, mut):
    ok = (lambda i: active(E[i]['status']))
    if 'search_ignores_status' in mut:
        ok = (lambda i: True)
    P = lambda s: 0 if s < 0 else (s + 1)
    out = []
    if (t & 0x80) == 0:
        if N <= 0:
            return out
        gate = active(E[logic]['status']) or 'raw_ignores_reference_status' in mut
        if gate:
            for i in range(1, N + 1):
                if (E[i]['refId'] & M32) == t:
                    out.append(i)
        return out
    f = t & 0x7F
    if f == 0:
        if 'self_needs_active' in mut and not active(E[logic]['status']):
            return []
        return [logic]
    if f == 1:
        if 'hero_needs_hp' in mut and E[0]['hp'] <= 0:
            return []
        return [0]
    rng0 = range(0, N + 1)
    rng1 = range(1, N + 1)
    if 'group_skips_hero' in mut:
        rng0 = rng1
    if f == 2:
        return [i for i in rng0 if ok(i)]
    if f == 3:
        return [i for i in rng1 if ok(i)]
    if f == 4:
        if not active(E[logic]['status']):
            return []
        return [i for i in rng0 if (E[i]['flags'] & 0x80) and not (E[i]['animFlags'] & 0x80) and not E[i]['hasPlatform']]
    if f == 5:
        return [i for i in rng1 if ok(i) and P(E[logic]['ridingSlot']) == i + 1]
    if f == 6:
        return [i for i in rng1 if ok(i) and P(E[i]['ridingSlot']) == logic + 1]
    if f == 7:
        return [i for i in rng1 if ok(i) and P(E[logic]['xCollSlot']) == i + 1]
    if f == 8:
        return [i for i in rng1 if ok(i) and P(E[i]['xCollSlot']) == logic + 1]
    if f == 9:
        return [i for i in rng1 if ok(i) and P(E[i]['parentSlot']) == logic + 1]
    if f == 10:
        return [i for i in rng1 if ok(i) and P(E[logic]['parentSlot']) == i + 1]
    if f == 11:
        return [i for i in rng1 if ok(i) and E[i]['hasPlatform']]
    return []


def handler(op, E, S, PAD, ins, logic, owner, mut):
    """returns the number of bytes; mutates E S PAD."""
    N = len(E) - 1
    if op == 0x72:
        tgt = owner if 'owner' in mut else logic
        if 'status_guard' in mut and not active(E[tgt]['status']):
            return 1
        if 'inverse' in mut:
            E[tgt]['targetAnimId'] = E[tgt]['lastTargetAnimId']
            E[tgt]['targetDir'] = E[tgt]['lastTargetDir']
            return 1
        E[tgt]['lastTargetAnimId'] = E[tgt]['targetAnimId']
        if 'no_dir' not in mut:
            E[tgt]['lastTargetDir'] = E[tgt]['targetDir']
        if 'both' in mut:
            E[owner]['lastTargetAnimId'] = E[owner]['targetAnimId']
            E[owner]['lastTargetDir'] = E[owner]['targetDir']
        return 1
    if op == 0x88:
        ref = owner if 'ref_owner' in mut else logic
        idx = ins[2]
        word = None
        if idx < 3:
            word = TABLE[idx]
            if 'table_swap' in mut:
                word = [3, 2, 4][idx]
            if 'index_is_status' in mut:
                word = idx
        elif 'idx_writes_zero' in mut:
            word = 0
        if word is None:
            return 3
        ms = search(ins[1], ref, E, N, mut)
        if 'first_only' in mut:
            ms = ms[:1]
        for i in ms:
            E[i]['status'] = word
        return 3
    if op == 0x96:
        ref = logic
        amount = ins[2] if 'amount_b2' in mut else ins[1]
        ms = search(ins[1], ref, E, N, mut)
        for i in ms:
            e = E[i]
            if 'only_active_targets' in mut and not active(e['status']):
                continue
            if 'skip_hp0' in mut and e['hp'] == 0:
                continue
            raw = e['hp'] + amount
            s = raw if 'sat64' in mut else s32(raw)
            if 'no_ceiling' not in mut:
                s2 = e['hpMax'] if e['hpMax'] < s else s
            else:
                s2 = s
            if 'never_lowers' in mut and s2 < e['hp']:
                s2 = e['hp']
            if 'floor0' in mut and s2 < 0:
                s2 = 0
            e['hp'] = s2
            if 'mirror_stats' in mut and i == 0:
                S['hp'] = max(-32768, min(32767, s2))
        return 3
    if op == 0x97:
        if 'spend_money' in mut:
            S['money'] = max(0, min(9999, S['money'] - ins[1]))
            return 2
        a = S['mp'] + ins[1]
        if 'cut16' in mut:
            a = s16(a)
        if ('no_ceiling' not in mut) and S['mpMax'] < a:
            S['mp'] = S['mpMax'] if 'const4' not in mut else 4
        elif ('no_floor' not in mut) and a < 0:
            S['mp'] = 0
        else:
            S['mp'] = s16(a)
        return 2
    if op == 0xB3:
        b = ins
        if 'big_endian' in mut:
            w = [b[2] | b[1] << 8, b[4] | b[3] << 8, b[6] | b[5] << 8, b[8] | b[7] << 8]
        else:
            w = [b[1] | b[2] << 8, b[3] | b[4] << 8, b[5] | b[6] << 8, b[7] | b[8] << 8]
        if 'mask8' in mut:
            w = [x & 0xFF for x in w]
        if 'swap_pr' in mut:
            w[1], w[2] = w[2], w[1]
        if 'only_hold' in mut:
            PAD['hold'] = w[0]
        elif 'or' in mut:
            for k, x in zip(('hold', 'pressed', 'released', 'interval'), w):
                PAD[k] |= x
        else:
            for k, x in zip(('hold', 'pressed', 'released', 'interval'), w):
                PAD[k] = x
        return 9


def run_test(t, op, mut):
    E = copy.deepcopy(t['start']['slots'])
    S = dict(t['start']['stats'])
    PAD = dict(t['start']['pad']) if t['start']['pad'] else {'hold': 0, 'pressed': 0, 'released': 0, 'interval': 0}
    ins = t['instruction']
    if mut == 'TODAY':
        ret = SIZE[op]
    else:
        ret = handler(op, E, S, PAD, ins, t['logicSlot'], t['ownerSlot'], mut)
    fails = []
    if ret != SIZE[op]:
        fails.append('size')
    for k, v in t['expected'].get('slot', {}).items():
        i, f = k.split('.')
        got = E[int(i)][f]
        if f in ('hp', 'hpMax', 'refId', 'animFlags'):
            ok = got == s32(v)
        else:
            ok = (got & M32) == (v & M32)
        if not ok:
            fails.append(('slot', k, got, v))
    for k, v in t['expected'].get('stats', {}).items():
        if S[k] != v:
            fails.append(('stats', k, S[k], v))
    for k, v in t['expected'].get('pad', {}).items():
        if PAD[k] != v:
            fails.append(('pad', k, PAD[k], v))
    return fails


MUTANTS = {
    # the report's names (my reading of their one-line descriptions)
    0x72: {'M72a': {'no_dir'}, 'M72b': {'owner'}, 'M72c': {'inverse'}, 'M72d': {'status_guard'}, 'x72-both': {'both'}},
    0x88: {'M88a': {'table_swap'}, 'M88c': {'first_only'}, 'M88e': {'idx_writes_zero'}, 'M88f': {'ref_owner'},
           'x88-index-as-status': {'index_is_status'}, 'x88-group-skips-hero': {'group_skips_hero'},
           'x88-search-ignores-candidate-status': {'search_ignores_status'}, 'x88-raw-ignores-reference-status': {'raw_ignores_reference_status'},
           'x88-self-needs-active': {'self_needs_active'}},
    0x96: {'M96a': {'amount_b2'}, 'M96b': {'no_ceiling'}, 'M96c': {'floor0'}, 'M96d': {'never_lowers'}, 'M96e': {'sat64'},
           'M96f': {'only_active_targets'}, 'M96g': {'skip_hp0'}, 'MS-1': {'mirror_stats'},
           'x96-self-needs-active': {'self_needs_active'}, 'x96-hero-needs-hp': {'hero_needs_hp'},
           'x96-group-skips-hero': {'group_skips_hero'}, 'x96-raw-ignores-reference-status': {'raw_ignores_reference_status'}},
    0x97: {'M97a': {'spend_money'}, 'M97b': {'cut16'}, 'M97c': {'no_floor'}, 'M97d': {'no_ceiling'}, 'M97e': {'const4'}},
    0xB3: {'MB3a': {'only_hold'}, 'MB3b': {'or'}, 'MB3c': {'big_endian'}, 'MB3d': {'swap_pr'}, 'MB3e': {'mask8'}},
}

if __name__ == '__main__':
    rows = json.load(open(sys.argv[1], encoding='utf-8'))
    by_op = {}
    for t in rows:
        by_op.setdefault(t['instruction'][0], []).append(t)
    # correct handler + today
    allok = True
    for op, ts in by_op.items():
        for t in ts:
            f = run_test(t, op, set())
            if f:
                allok = False
                print('CORRECT HANDLER FAILS', t['id'], f)
    print('correct handler passes the 44 tests:', allok)
    print('--- red today (my model) vs their redToday')
    for op, ts in by_op.items():
        for t in ts:
            f = run_test(t, op, 'TODAY')
            red = bool(f)
            if red != t['redToday']:
                print('DISAGREE redToday', t['id'], 'mine', red, 'theirs', t['redToday'], f[:2])
    print('--- mutants')
    for op, muts in MUTANTS.items():
        for name, flags in muts.items():
            killed = [t['id'] for t in by_op[op] if run_test(t, op, flags)]
            print('%-34s killed by %d: %s' % (name, len(killed), ' '.join(killed)))
