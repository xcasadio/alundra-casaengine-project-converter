"""key_facts.py - E20.h4d1 T0: the facts read in the faithful keys (critic/keys_faithful/), written BEFORE the probes; nothing here comes from the DLL.
usage: python -I key_facts.py <keys_faithful dir>"""
import json, sys
K = sys.argv[1]
def load(n): return json.load(open(K + '/' + n + '.json'))
MULT, ADD, M = 0x7D2B89DD, 0xE06A02E7, 0xFFFFFFFF
def step(s): return (s * MULT + ADD) & M
def steps(a, b):
    s = a
    for k in range(0, 40):
        if s == b: return k
        s = step(s)
    return -1
def facts(name):
    d = load(name); r = d['rows']
    print('==', name, 'ticks', len(r), 'options', {k: v for k, v in d['options'].items() if k != 'out'})
    prev = None
    sprite = {}
    for row in r:
        t = row['t']
        for sl, e in row['ents'].items():
            sprite.setdefault(sl, []).append((t, e))
    # births / lives
    for sl, lst in sprite.items():
        t0, e0 = lst[0]; t1, e1 = lst[-1]
        print(' slot', sl, 'type', e0['type'], 'flags %#x' % e0['flags'], 'first tick with state', t0, 'status', e0['status'], 'pos', (e0['x'], e0['y'], e0['z']), 'last tick with state', t1, 'status', e1['status'])
        # status changes and animend
        last = None
        for t, e in lst:
            if last is None or e['status'] != last['status']:
                print('    t=%d status %d' % (t, e['status']))
            if (last is None and e['animend']) or (last is not None and e['animend'] != last['animend']):
                print('    t=%d animend(+0xAC)=%d' % (t, e['animend']))
            if last is not None and e['hitc'] != last['hitc']:
                print('    t=%d hitc %d' % (t, e['hitc']))
            last = e
    # hero / item timeline
    last = None
    for row in r:
        k = (row['hero_anim_t'], row['inuse'], row['phase'])
        if last != k:
            print('  t=%d hero_anim_t %#x inuse %#x phase %d counter %d mp %d' % (row['t'], row['hero_anim_t'], row['inuse'], row['phase'], row['counter'], row['mp']))
            last = k
    # block / buf changes
    lb = None; lbuf = None
    for row in r:
        if row['block'] != lb:
            print('  t=%d block %s' % (row['t'], row['block'])); lb = row['block']
        if row['buf'] != lbuf:
            print('  t=%d buf %s' % (row['t'], row['buf'])); lbuf = row['buf']
    # generator
    mism = 0
    for a, b in zip(r, r[1:]):
        n = steps(a['rng0'], b['rng0'])
        if n != 1:
            print('  rng0 t=%d -> t=%d: %d steps (rng0 %#x -> %#x)' % (a['t'], b['t'], n, a['rng0'], b['rng0']))
    # effects journal
    for row in r:
        for j in row['journal']:
            if j[0] not in ('sound', 'pass_38E84_effects_stub') :
                print('  t=%d journal %s' % (row['t'], j))
            if j[0] == 'sound' and row['t'] > 1:
                print('  t=%d sound %s' % (row['t'], j[1]))
    pr = [(row['t'], row['ptr'], row['ident']) for row in r]
    lp = None
    for t, p, i in pr:
        if (p, i) != lp: print('  t=%d ptr %#x ident %d' % (t, p, i)); lp = (p, i)
for n in [a for a in sys.argv[2:] if not a.endswith(".json")]:
    facts(n)


def targets_facts(name):
    d = load(name); L = d['log']
    print('==', name, 'targets', d['targets'], 'ticks', len(L))
    wr = {k: [] for k in range(len(d['targets']))}
    for row in L:
        for k in row['hit_targets']:
            wr[k].append(row['t'])
    for k, v in wr.items():
        print('  target', k, d['targets'][k], 'hit-pass writes at', v)
    draws = [(row['t'], steps(row['rng0'], row['rng1'])) for row in L if row['rng1'] != row['rng0']]
    print('  ticks where the passes drew (rng0 != rng1), with the number of steps:', draws)
    b = [(row['t'], row['sprite21_box']) for row in L if row['sprite21_box']]
    print('  sprite 21 present (box logged) from', b[0][0] if b else None, 'to', b[-1][0] if b else None)
    return wr, draws


def emit_expected(path):
    cast = load('fire2_cast_pf')['rows']
    exp = {'cast': {'birth': 34, 'animend': 153, 'status4': 154, 'absent_from': 155, 'pos': [6553600, 6553600, 2],
                    'rng0': {r['t']: r['rng0'] for r in cast},
                    'steps': {a['t']: steps(a['rng0'], b['rng0']) for a, b in zip(cast, cast[1:])}}}
    for n in ('fire2_targets_pf', 'fire2_targets_edge_pf'):
        wr, draws = targets_facts(n)
        exp[n] = {'writes': wr, 'draws': draws}
    json.dump(exp, open(path, 'w'))


if len(sys.argv) > 1 and sys.argv[-1].endswith('.json'):
    emit_expected(sys.argv[-1])
