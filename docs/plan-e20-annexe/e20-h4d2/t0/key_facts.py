"""key_facts.py - E20.h4d2 T0: reads the remade keys (disc/keys, disc/out_qfix, critic/keys_proposed) and writes the EXPECTED values of the probes P1 to P14 (expected.json) and a readable summary (key_facts.txt).
Written BEFORE the first run of the DLL probes; nothing here comes from the DLL.  usage: python -B key_facts.py ANNEX_DIR OUT_DIR
Conventions: the key's Z is the binary's; the DLL's Z is the binary's minus 1 (ADR-0026): 'z' below is already converted (key - 1) for the entities and for the hero."""
import json
import os
import sys

ANNEX = sys.argv[1]
OUT = sys.argv[2]
os.makedirs(OUT, exist_ok=True)
KEYS = [os.path.join(ANNEX, 'disc', 'keys'), os.path.join(ANNEX, 'disc', 'out_qfix'), os.path.join(ANNEX, 'critic', 'keys_proposed')]


def load(name):
    for d in KEYS:
        p = os.path.join(d, name + '.json')
        if os.path.exists(p):
            return json.load(open(p))
    raise FileNotFoundError(name)


F = ('slot', 'status', 'x', 'y', 'z', 'fx', 'fy', 'fz', 'flags', 'anim_t', 'dir_t', 'animend', 'b274', 'b278_slot', 'ground', 'slope', 'atk_attr', 'atk_pow')


def ents_of(d):
    """entities of type 20/23 by birth order (the key's id): per tick the values F, the DLL's Z."""
    by = {}
    for r in d['rows']:
        for k, e in r['ents'].items():
            if e['type'] not in (20, 23):
                continue
            by.setdefault(e['id'], dict(type=e['type'], birth=r['t'], slot=int(k), ticks={}))
            by[e['id']]['ticks'][str(r['t'])] = [int(k), e['status'], e['x'], e['y'], e['z'] - 1, e['fx'], e['fy'], e['fz'], e['flags'], e['anim_t'], e['dir_t'], e['animend'], e['b274'], e['b278_slot'], e['ground'], e['slope'], e['atk_attr'], e['atk_pow']]
    return [by[i] for i in sorted(by)]


def scen(name):
    d = load(name)
    rows = d['rows']
    return dict(ticks=len(rows), ents=ents_of(d), rng0={str(r['t']): r['rng0'] for r in rows}, draws={str(r['t']): r['draws'] for r in rows}, count={str(r['t']): r.get('count') for r in rows},
                hero={str(r['t']): [r['hero']['anim_t'], r['hero']['x'], r['hero']['y'], None if r['hero']['z'] is None else r['hero']['z'] - 1] for r in rows},
                item={str(r['t']): [r['item']['inuse'], r['item']['phase'], r['item']['counter'], r['item']['mp']] for r in rows},
                events={str(r['t']): r['events'] for r in rows if r['events']}, hits={str(r['t']): r['hits'] for r in rows if r['hits']}, meta=d['meta'])


SCEN = ['fire1_cast_pf', 'fire1_cast_pf_seedB', 'fire1_water_cols6_12_pf', 'fire1_relief_plateau_hb1_cols6_12_pf', 'fire1_relief_plateau_hb2_cols6_12_pf', 'fire1_relief_hero_on_plateau_hb2_pf',
        'fire1_tgt_one_pf', 'fire1_tgt_three_pf', 'fire1_tgt_two_wrong_order_pf', 'fire1_tgt_topslot_pf', 'fire1_tgt_vanish75_pf', 'fire1_col_hit_pf', 'fire1_table_full_pf', 'fire1_table_one_free_pf'] + ['fire1_hurt%d_pf' % t for t in (10, 33, 34, 35, 60, 100, 121, 122, 124, 140)]
exp = {'fields': F, 'scenarios': {n: scen(n) for n in SCEN}}
exp['edge'] = {'x': {'75': None, '76': 34, '77': 34, '121': 34, '122': 34, '123': None}, 'y': {'84': None, '85': 34, '86': 34, '114': 34, '115': 34, '116': None}, 'source': 'critic/results/edge_sweep_my_cpu.txt'}
json.dump(exp, open(os.path.join(OUT, 'expected.json'), 'w'), sort_keys=True, separators=(',', ':'))

L = []


def P(*a):
    L.append(' '.join(str(x) for x in a))


c = exp['scenarios']['fire1_cast_pf']
P('== fire1_cast_pf (seed 0x12345678, no target)')
col = c['ents'][0]
t = col['ticks']
P('P1 column: birth tick', col['birth'], 'slot', col['slot'], 'z(DLL)', t[str(col['birth'])][4], 'status at S', t[str(col['birth'])][1], 'flags', t[str(col['birth'])][8], 'atk', t[str(col['birth'])][16], t[str(col['birth'])][17])
P('P7 column: animend first at', min(int(k) for k, v in t.items() if v[11] == 1), 'status 4 first at', min(int(k) for k, v in t.items() if v[1] == 4), 'last seen', max(int(k) for k in t))
for i, e in enumerate(c['ents'][1:], 1):
    tt = e['ticks']
    b = e['birth']
    first = tt[str(b)]
    g1 = [int(k) for k, v in tt.items() if v[14] == 1]
    s3 = [int(k) for k, v in tt.items() if v[1] == 3]
    ae = [int(k) for k, v in tt.items() if v[11] == 1]
    s4 = [int(k) for k, v in tt.items() if v[1] == 4]
    P('ball', i, 'birth', b, 'slot', e['slot'], 'dir', first[10], 'fz', first[7], 'z', first[4], 'b274@S', first[12], 'b274@S+1', tt.get(str(b + 1), [0] * 18)[12],
      'first b274==0 tick', min([int(k) for k, v in tt.items() if v[12] == 0] or [-1]), 'ground@', g1[:1], 'status3@', s3[:1], 'ground@s3', tt[str(s3[0])][14] if s3 else None, 'animend@', ae[:1], 'status4@', s4[:1],
      'fx diffs after s3', [tt[str(k)][5] - tt[str(k + 1)][5] for k in range(s3[0], s3[0] + 3) if str(k + 1) in tt] if s3 else None, 'flags@s3', tt[str(s3[0])][8] if s3 else None, 'anim@s3', tt[str(s3[0])][9] if s3 else None)
P('P3 rng0: t=1', c['rng0']['1'], 't=34', c['rng0']['34'], 't=35', c['rng0']['35'], 't=200', c['rng0']['200'], 'draws at 34', c['draws']['34'], 'ticks with draws>0', [int(k) for k, v in c['draws'].items() if v > 0][:12])
P('P9 atk@S+17 of ball 1:', c['ents'][1]['ticks'][str(c['ents'][1]['birth'] + 17)][16:18])
P('P10 hurt60 events:', {k: v for k, v in exp['scenarios']['fire1_hurt60_pf']['events'].items()})
h = exp['scenarios']['fire1_hurt60_pf']
P('hurt60 hero anim/x/y/z 58..64', [h['hero'][str(k)] for k in range(58, 65)])
for T in (10, 33, 34, 35, 60, 100, 121, 122, 124, 140):
    s = exp['scenarios']['fire1_hurt%d_pf' % T]
    P('hurt', T, 'events', s['events'], 'item at T..T+2', [s['item'][str(k)] for k in range(T, T + 3)], 'ents (type, status) at T+3', sorted((e['type'], e['ticks'][str(T + 3)][1]) for e in s['ents'] if str(T + 3) in e['ticks']))
w = exp['scenarios']['fire1_water_cols6_12_pf']
P('P11 water: slope values seen on balls:', sorted({v[15] for e in w['ents'] if e['type'] == 23 for v in e['ticks'].values()}), 'flags seen', sorted({v[8] for e in w['ents'] if e['type'] == 23 for v in e['ticks'].values()}))
P('water == cast on every compared field:', all(a['ticks'] == b['ticks'] for a, b in zip(w['ents'], c['ents'])))
for n in ('fire1_relief_plateau_hb1_cols6_12_pf', 'fire1_relief_plateau_hb2_cols6_12_pf', 'fire1_relief_hero_on_plateau_hb2_pf'):
    r = exp['scenarios'][n]
    P('P14', n)
    for i, e in enumerate(r['ents'][1:], 1):
        tt = e['ticks']
        g1 = [int(k) for k, v in tt.items() if v[14] == 1]
        s3 = [int(k) for k, v in tt.items() if v[1] == 3]
        P('  ball', i, 'birth', e['birth'], 'ground@', g1[:1], 'z@ground', tt[str(g1[0])][4] if g1 else None, 'status3@', s3[:1], 'ground@s3', tt[str(s3[0])][14] if s3 else None, 'z@s3', tt[str(s3[0])][4] if s3 else None)
    P('  column z', r['ents'][0]['ticks'][str(r['ents'][0]['birth'])][4], 'hero z', r['hero']['34'])
for n in ('fire1_tgt_two_wrong_order_pf', 'fire1_tgt_topslot_pf', 'fire1_tgt_vanish75_pf', 'fire1_tgt_one_pf', 'fire1_tgt_three_pf'):
    r = exp['scenarios'][n]
    P('targets', n, 'ball b278 slot changes:', [(i, k, v[13]) for i, e in enumerate(r['ents'][1:], 1) for k, v in e['ticks'].items() if v[13] and (str(int(k) - 1) not in e['ticks'] or e['ticks'][str(int(k) - 1)][13] != v[13])][:14])
P('P12 slots at birth, cast:', [(e['type'], e['birth'], e['slot']) for e in c['ents']])
P('P12 slots at birth, table_one_free:', [(e['type'], e['birth'], e['slot']) for e in exp['scenarios']['fire1_table_one_free_pf']['ents']])
P('P12 slots at birth, table_full:', [(e['type'], e['birth'], e['slot']) for e in exp['scenarios']['fire1_table_full_pf']['ents']], 'item', [exp['scenarios']['fire1_table_full_pf']['item'][str(k)] for k in (33, 34, 35)])
P('P8 edge sweep (first tick of a hit by the column):', exp['edge']['x'], exp['edge']['y'])
open(os.path.join(OUT, 'key_facts.txt'), 'w').write('\n'.join(L) + '\n')
print('\n'.join(L))
