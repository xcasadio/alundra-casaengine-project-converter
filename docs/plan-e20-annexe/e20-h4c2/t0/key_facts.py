"""key_facts.py - E20.h4c2 T0: the facts the probes (s1) to (s12) are predicted from, read in the filed keys ONLY (no value comes from the DLL).
usage: python -B key_facts.py <annex e20-h4c2 dir> > key_facts.txt   (also writes expected.json next to the output when --json FILE is given)
The keys: disc/keys (the remade keys), critic/keys (mid-map, plateaus), rockfix/ (the four 'fixed' keys of D-H4C2-4)."""
import json
import sys
import os

A = sys.argv[1]
OUT_JSON = sys.argv[sys.argv.index('--json') + 1] if '--json' in sys.argv else None
M32 = 0xFFFFFFFF
MUL, INC = 0x7D2B89DD, 0xE06A02E7


def load(*p):
    return json.load(open(os.path.join(A, *p)))


def step(s):
    return (s * MUL + INC) & M32


def steps_between(a, b, limit=64):
    s = a
    for k in range(limit + 1):
        if s == b:
            return k
        s = step(s)
    return -1


keys = {
    'orig': load('disc', 'keys', 'cast_2c_pf.json'),
    'mid': load('critic', 'keys', 'cast_2c_pf_mid_x600y480.json'),
    'fix': load('rockfix', 'cast_2c_pf_rockfix.json'),
    'hb16fix': load('rockfix', 'cast_2c_pf_plateau_hb16_rockfix.json'),
    'hb17fix': load('rockfix', 'cast_2c_pf_plateau_hb17_rockfix.json'),
}
exp = {}

print('== (s1) the generator: steps of the DLL during the frame of tick t = steps(rng0(t) -> rng0(t+1)) = draws(t) + 1')
for name, k in keys.items():
    pt = k['per_tick']
    ok = True
    steps = {}
    for i in range(len(pt) - 1):
        n = steps_between(pt[i]['rng0'], pt[i + 1]['rng0'])
        steps[pt[i]['t']] = n
        d = steps_between(pt[i]['rng0'], pt[i]['rng1'])
        if n != d + 1 or n < 0:
            ok = False
    births = {}
    for r in k['rows']:
        births[r['birth']] = births.get(r['birth'], 0) + 1
    nonzero = {t: n for t, n in steps.items() if n != 1}
    print('  %-8s ticks %d  rng0(t+1)=step(rng1(t)) and steps=draws+1 on every tick: %s ; ticks with steps != 1: %s' % (name, len(pt) - 1, ok, nonzero))
    exp.setdefault('s1', {})[name] = dict(rng0={str(p['t']): p['rng0'] for p in pt}, steps={str(t): n for t, n in steps.items()}, births={str(t): n for t, n in births.items()})

print('== (s2)/(s3) the births: tick, raw x y z of the key (the DLL Z is the key Z - 1), the terrain word +0x138, the first tick ForceAdjusted (+0x13C)')
for name, k in keys.items():
    rows = []
    for r in k['rows']:
        t0 = r['track'][0]
        rows.append(dict(id=r['id'], slot=r['slot'], birth=r['birth'], x=t0[3], y=t0[4], z=t0[5], st_birth=t0[1], t138=r['t138'], fadj=r['fadj'], ground=r['ground'], st3=r['st3'], ta1=r['ta1'], animend=r['animend'], st4=r['st4'], last=r['last_seen'], r1=r['r1'], r2=r['r2'], dir=r['dir']))
    exp.setdefault('s2', {})[name] = rows
    print('  %-8s' % name)
    for q in rows:
        print('    id%2d slot%d b%3d  x=%9d y=%9d z=%9d (DLL z=%9d) st@b=%d t138=%9d fadj=%s ground=%s st3=%s ta1=%s animend=%s st4=%s' % (q['id'], q['slot'], q['birth'], q['x'], q['y'], q['z'], q['z'] - 1, q['st_birth'], q['t138'], q['fadj'], q['ground'], q['st3'], q['ta1'], q['animend'], q['st4']))

print('== (s4) the end of the animation 1: +0xAC - ta1 and st4 - ta1 (mid key, interior rocks)')
d = set()
for q in exp['s2']['mid']:
    d.add((q['animend'] - q['ta1'], q['st4'] - q['ta1'], q['ta1'] - q['birth'], q['ground'] - q['birth']))
print('  (animend - ta1, st4 - ta1, ta1 - birth, ground - birth) over the 15 rocks:', sorted(d))

print('== (s5) the sounds (tick, id), ids 55 and 45')
for name, k in keys.items():
    sn = [(t, i) for t, i in k['sounds'] if i in (45, 55)]
    exp.setdefault('s5', {})[name] = sn
    print('  %-8s %d sounds; 55 at %s ; 45 at %s' % (name, len(sn), [t for t, i in sn if i == 55], [t for t, i in sn if i == 45]))
    print('           ids in the key:', sorted(set(i for _, i in k['sounds'])))

print('== (s9a) the in-air ticks of the rocks 11 and 8 (IsOnGround 0 up to the tick before the ground tick; +0x13C set during the held ticks)')
for name in ('fix', 'hb16fix', 'hb17fix'):
    for r in keys[name]['rows']:
        if r['id'] in (8, 11):
            tr = r['track']
            held = [t[0] for t in tr if t[7] != 0]
            air = [t[0] for t in tr if t[8] == 0]
            print('  %-8s id%2d birth %d: +0x144 first at %s ; ticks with +0x144 == 0: %d..%d ; ticks with +0x13C != 0: %s..%s (%d ticks)' % (name, r['id'], r['birth'], r['ground'], min(air), max(air), min(held) if held else None, max(held) if held else None, len(held)))
            exp.setdefault('s9a', {}).setdefault(name, {})[str(r['id'])] = dict(birth=r['birth'], ground=r['ground'], air=[min(air), max(air)], held=[min(held), max(held)] if held else None)

print('== (s10a) the position bounds: the steps per tick of the rocks 8 and 11 (raw words)')
for name in ('fix', 'hb16fix', 'hb17fix'):
    for r in keys[name]['rows']:
        if r['id'] not in (8, 11):
            continue
        tr = r['track']
        print('  %-8s id%2d' % (name, r['id']))
        prev = None
        out = []
        for t in tr:
            if prev is not None:
                out.append((t[0], t[3] - prev[3], t[4] - prev[4], t[5] - prev[5], t[7], t[8]))
            prev = t
        if r['id'] == 8:
            sel = [o for o in out if 136 <= o[0] <= 148]
        else:
            sel = [o for o in out if o[0] in (162, 163, 164, 165, 192, 193, 194, 195)]
        for o in sel:
            print('     tick %3d  dx=%8d dy=%8d dz=%9d fadj=%s gnd=%s' % o)
        last = tr[[x[0] for x in tr].index(r['ground'])]
        print('     at the ground tick %d: x=%d y=%d z=%d' % (r['ground'], last[3], last[4], last[5]))
        exp.setdefault('s10a', {}).setdefault(name, {})[str(r['id'])] = {str(o[0]): [o[1], o[2]] for o in out}
        exp['s10a'][name][str(r['id']) + '_ground'] = [r['ground'], last[3], last[4]]
        exp['s10a'][name][str(r['id']) + '_track'] = {str(t[0]): [t[3], t[4], t[5]] for t in tr}

print('== (s8) the draws of a hit: the key records a hit one tick after the pass that drew (the draws are at tick hit - 1): steps(rng0 -> rng1) at that tick minus 2 per birth, per hit')
tk = load('disc', 'keys', 'targets_2c_pf_inv0.json')
births_by_tick = {}
for r in keys['orig']['rows']:
    births_by_tick[r['birth']] = births_by_tick.get(r['birth'], 0) + 1
hit_ticks = {}
for tg in tk['targets']:
    for h in tg['hits']:
        hit_ticks[h[0] - 1] = hit_ticks.get(h[0] - 1, 0) + 1
ks = set()
mismatch = []
for t in range(1, len(tk['rng']) + 1):
    r0, r1 = tk['rng'][t - 1]
    steps = steps_between(r0, r1)
    nb = births_by_tick.get(t, 0)
    n = hit_ticks.get(t, 0)
    if steps - 2 * nb != 9 * n:
        mismatch.append((t, steps, nb, n))
for t, n in sorted(hit_ticks.items()):
    r0, r1 = tk['rng'][t - 1]
    steps = steps_between(r0, r1)
    nb = births_by_tick.get(t, 0)
    ks.add((steps - 2 * nb) / n)
print('  hit ticks (draw ticks): %d ticks, %d hits; draws per hit over them: %s ; ticks where steps != 2*births + 9*hits: %s' % (len(hit_ticks), sum(hit_ticks.values()), sorted(ks), mismatch))
exp['s8'] = dict(per_hit=sorted(ks), hit_ticks={str(t): n for t, n in hit_ticks.items()}, targets=[(g['x'], g['y']) for g in tk['targets']])

print('== (s11) the interruption T = 100: hero animation (decimal: 49 = 0x31, 28 = 0x1C), item, sound 23')
rows = load('disc', 'keys', 'cancel_2c_pf_t100.json')
seq = []
for r in rows:
    if not seq or seq[-1][1] != r['hero_ta'] or seq[-1][2] != r['item']:
        seq.append([r['t'], r['hero_ta'], r['item']])
print('  (tick, hero_ta, item) at each change:', seq)
exp['s11'] = dict(seq=seq, sound23=[100])

if OUT_JSON:
    json.dump(exp, open(OUT_JSON, 'w'))
