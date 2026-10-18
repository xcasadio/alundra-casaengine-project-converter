"""wind1_compare.py - h4e1 discovery: cross-checks between keys.  usage: python -B wind1_compare.py KEYDIR STALEDIR"""
import json, sys, os
K, S = sys.argv[1], sys.argv[2]
def load(d, n): return json.load(open(os.path.join(d, n + '.json')))
def births(res):
    out = []
    seen = set()
    for r in res['rows']:
        for i, e in r['ents'].items():
            if e['type'] == 22 and (i, e['id']) not in seen:
                seen.add((i, e['id']))
                out.append((r['t'], int(i), e['x'], e['y'], e['z']))
    return out
def ends(res):
    out = {}
    for r in res['rows']:
        for i, e in r['ents'].items():
            if e['type'] == 22:
                out.setdefault((i, e['id']), [r['t'], r['t']])[1] = r['t']
    return out
east, south = load(K, 'wind1_cast_pf'), load(K, 'wind1_cast_pf_south')
print('(a) east vs south: strike births identical:', births(east) == births(south))
print('    generator states identical at every tick:', [r['rng2'] for r in east['rows']] == [r['rng2'] for r in south['rows']])
g = load(K, 'wind1_cast_pf_gateoff')
print('(b) gate off (what-if): births (tick, slot, x px, y px):', [(t, s, round(x / 65536, 3), round(y / 65536, 3)) for (t, s, x, y, z) in births(g)])
print('    faithful births                                        :', [(t, s, round(x / 65536, 3), round(y / 65536, 3)) for (t, s, x, y, z) in births(east)])
print('    gate-off object freed at', next(r['t'] for r in g['rows'] if r['item']['inuse'] == 0 and r['t'] > 1), ' faithful', next(r['t'] for r in east['rows'] if r['item']['inuse'] == 0 and r['t'] > 1))
ts = load(S, 'wind1_cast_pf')
print('(c) h4 stale-slot harness (FAITHFUL=0) vs remade (FAITHFUL=1), no target:')
print('    stale   births:', [(t, s) for (t, s, x, y, z) in births(ts)][:3], ' life of the first strike (ticks seen):', list(ends(ts).values())[0])
print('    remade  births:', [(t, s) for (t, s, x, y, z) in births(east)][:3], ' life of the first strike (ticks seen):', list(ends(east).values())[0])
sx = [(r['t'], r['ents'].get('1', {}).get('z'), r['ents'].get('1', {}).get('status')) for r in ts['rows'][40:46]]
rx = [(r['t'], r['ents'].get('1', {}).get('z'), r['ents'].get('1', {}).get('status')) for r in east['rows'][40:46]]
print('    stale  z/status ticks 41..46:', sx)
print('    remade z/status ticks 41..46:', rx)
print('    first tick the two keys differ in the strike (z):', next((a['t'] for a, b in zip(ts['rows'], east['rows']) if a['ents'].get('1') != b['ents'].get('1')), None))
tsh, esh = load(S, 'wind1_one_near_dec_pf'), load(K, 'wind1_one_near_dec_pf')
print('(d) one near target (decoy): first hit tick stale', next((r['t'] for r in tsh['rows'] if r['hit_targets']), None), 'remade', next((r['t'] for r in esh['rows'] if r['hit_targets']), None))
