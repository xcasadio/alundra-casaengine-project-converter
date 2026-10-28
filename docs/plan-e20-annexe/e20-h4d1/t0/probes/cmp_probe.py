"""cmp_probe.py - E20.h4d1 T0: compares the rows of the DLL probes (H4d1T0ProbeTests) with the predictions (t0/predictions.md, values from t0/expected.json) and with the faithful key.
usage: python -I cmp_probe.py <probe out dir> <expected.json> <keys_faithful dir>"""
import csv, json, sys, os
out, expf, kdir = sys.argv[1:4]
E = json.load(open(expf))
key = json.load(open(kdir + '/fire2_cast_pf.json'))['rows']
fails = []


def rows(name, ext):
    p = os.path.join(out, name + '.' + ext + '.csv')
    return [list(map(int, r)) for r in csv.reader(open(p)) if r]


def chk(label, ok, detail=''):
    print(('OK   ' if ok else 'FAIL ') + label + (' : ' + detail if detail else ''))
    if not ok:
        fails.append(label)


# ---- (d1)..(d4) cast
sp = {r[0]: r for r in rows('cast', 'sprite')}
ticks = sorted(sp)
chk('(d1) first sample of the sprite at tick 34', ticks[0] == 34, str(ticks[0]))
chk('(d1) status Normal(2) at every sample 34..153', all(sp[t][2] == 2 for t in range(34, 154)))
chk('(d1) one entity of the type 21 alive at every sample 34..154', all(sp[t][12] == 1 for t in range(34, 155)))
notes = open(os.path.join(out, 'cast.notes.txt')).read()
chk('(d1) birth at the driver call of tick 34, counter 32', 'birth tick 34 counter 32' in notes)
chk('(d2) position (6553600, 6553600, 1) and forces 0 at 34..154', all(sp[t][3:9] == [6553600, 6553600, 1, 0, 0, 0] for t in range(34, 155)))
chk('(d3) +0xAC 0 at 34..152, 1 at 153', all(sp[t][10] == 0 for t in range(34, 153)) and sp[153][10] == 1, 'at 153: %d, at 152: %d' % (sp[153][10], sp[152][10]))
chk('(d4) E28 once at tick 154', notes.rstrip().endswith('e28 ticks: 154'))
chk('(d4) status FlagToDestroy(4) at the sample 154', sp[154][2] == 4)
chk('(d4) status Destroyed(0) at the samples 155..199', all(sp[t][2] == 0 for t in range(155, 200)))
eff = rows('cast', 'effects')
kx = [(6553600, 9383424), (3421033, 8641978), (3294976, 6553600), (3333067, 4406578), (6553600, 5288960), (7846483, 5691678), (10161664, 6553600), (7978432, 7503488)]
chk('(d4) the 8 effects at tick 154, (19, 2, X, Y, 2) as the key', [(r[0], r[1], r[2], r[3], r[4], r[5], r[6]) for r in eff] == [(154, k, 19, 2, x, y, 2) for k, (x, y) in enumerate(kx)])
kj = [j[1] for r in key if r['t'] == 154 for j in r['journal'] if j[0] == 'effect_3BDD8']
chk('(d4) the key journal at 154 has the same 8 (X, Y, Z)', [(a[3], a[4], a[5]) for a in kj] == [(x, y, 2) for x, y in kx])
# ---- (d5)
blk = {r[0]: r[1:] for r in rows('cast', 'block')}
hero = {r[0]: r for r in rows('cast', 'hero')}
bad = [t for t in range(1, 155) if [blk[t][i] for i in (0, 1, 2, 3, 4)] != [key[t - 1]['block'][i] for i in (0, 1, 2, 3, 4)]]
chk('(d5) Flag, LimitX, LimitY, SpeedX, SpeedY equal the key at ticks 1..154', not bad, 'differences at ' + str(bad[:10]))
chk('(d5) the nine words at 155 are 0 (the captured ones)', blk[155] == [0] * 9, str(blk[155]))
chk('(d5) hero 0x34 and phase 2 at the sample 155', hero[155][1] == 0x34 and hero[155][3] == 2, 'ta %#x phase %d' % (hero[155][1], hero[155][3]))
chk('(d5) hero animation 0 at the sample 181', hero[181][1] == 0, '%#x' % hero[181][1])
chk('(d5) ItemInUse 0 first at the sample 182', hero[181][2] != 0 and hero[182][2] == 0, 'inuse 181: %#x, 182: %#x' % (hero[181][2], hero[182][2]))
# ---- (e) generator
gen = {r[0]: r for r in rows('cast', 'gen')}
exp_rng = E['cast']['rng0']
badg = [t for t in range(1, 200) if gen[t][1] != exp_rng[str(t + 1)]]
chk('(e) state after frame t == rng0(t+1) of the key, t = 1..199', not badg, str(badg[:10]))
bads = [t for t in range(1, 200) if gen[t][2] != (9 if t == 154 else 1)]
chk('(e) steps per tick: 1, and 9 at the tick 154', not bads, str([(t, gen[t][2]) for t in bads[:10]]))
chk('(e) state after frame 153 = 0xA468A517, after 154 = 0xEC3BCBAA', gen[153][1] == 0xA468A517 and gen[154][1] == 0xEC3BCBAA, '%#x %#x' % (gen[153][1], gen[154][1]))


# ---- (d6) hits
def hits(name, n):
    t = {r[0]: r[1:] for r in rows(name, 'targets')}
    res = []
    for i in range(n):
        w = [tk for tk in sorted(t) if t[tk][i] == 25 and (tk == 1 or t[tk - 1][i] != 25)]
        res.append(w)
    return res, t


for name, key_name, n in (('targets', 'fire2_targets_pf', 3), ('edge', 'fire2_targets_edge_pf', 4)):
    res, t = hits(name, n)
    exp = [E[key_name]['writes'][str(i)] for i in range(n)]
    chk('(d6) %s: the ticks of the hit-pass writes equal the key' % name, res == exp, 'DLL %s ; key %s' % (res, exp))
    sparks = {tk: t[tk][n] for tk in t}
    hit_ticks = sorted(set(x for w in exp for x in w))
    chk('(d6) %s: sparks = 3 per hit (x blocks hit that tick) and 0 elsewhere' % name,
        all(sparks[tk] == 3 * sum(1 for w in exp if tk in w) for tk in hit_ticks) and all(sparks[tk] == 0 for tk in sparks if tk not in hit_ticks),
        str({tk: sparks[tk] for tk in sparks if sparks[tk]}))
    chk('(d6-erratum) %s: the seam HitSparksForTests is called ONCE PER HIT (WorldProxy.EmitHitSparks, before the three sparks are queued): its count per tick = the number of blocks hit that tick' % name,
        all(sparks[tk] == sum(1 for w in exp if tk in w) for tk in sparks))
    h = {r[0]: r for r in rows(name, 'hero')}
    chk('(d6) %s: the hero is never hit (counter and HitCounter 0)' % name, all(h[tk][6] == 0 and h[tk][7] == 0 for tk in h))
    g = {r[0]: r for r in rows(name, 'gen')}
    draws = {tk: g[tk][2] - 1 for tk in g}
    kd = {tk: s for tk, s in E[key_name]['draws']}
    ok = all(draws[tk] == kd.get(tk, 0) for tk in g)
    print(('INFO ' if ok else 'INFO(differs) ') + '%s: draws per tick (steps-1) equal the key' % name, '' if ok else str({tk: (draws[tk], kd.get(tk, 0)) for tk in g if draws[tk] != kd.get(tk, 0)}))
# ---- supplementary: the hero, the item, the MP against the key, tick by tick
bad = []
for t in range(1, 201):
    k = key[t - 1]
    h = hero[t]
    if (h[1], h[2], h[3], h[5]) != (k['hero_anim_t'], k['inuse'], k['phase'], k['mp']):
        bad.append((t, (hex(h[1]), hex(h[2]), h[3], h[5]), (hex(k['hero_anim_t']), hex(k['inuse']), k['phase'], k['mp'])))
print('INFO hero (target animation, item, phase, MP) vs the key at ticks 1..200: %d differences' % len(bad), bad[:6])
badc = [(t, hero[t][4], key[t - 1]['counter']) for t in range(1, 201) if hero[t][4] != key[t - 1]['counter']]
print('INFO ItemUseCounter vs the key: %d differences' % len(badc), badc[:4])
snd = [tuple(r) for r in rows('cast', 'sounds')]
print('INFO sounds played in cast (tick, id):', snd)
print('INFO sprite 21: attack (attribute, power) =', set((r[18], r[19]) for r in sp.values() if r[2] != 0), '; flags', set(r[11] for r in sp.values()))
bx = [(t, sp[t][13], sp[t][14], sp[t][15]) for t in (34, 36, 70, 100, 140, 153) if t in sp]
print('INFO box (w, d, h raw) at ticks 34, 36, 70, 100, 140, 153:', bx)
for name in ('cast_t2', 'cast_t3'):
    spn = rows(name, 'sprite')
    nt = open(os.path.join(out, name + '.notes.txt')).read()
    gn = rows(name, 'gen')
    print('INFO %s: sprite first sample tick %d, status 4 at %s, last notes line "%s", total steps %d' % (name, spn[0][0], [r[0] for r in spn if r[2] == 4][:1], nt.strip().splitlines()[-1], sum(r[2] for r in gn)))
print('RESULT', 'ALL PREDICTIONS HOLD' if not fails else 'CONTRADICTED: ' + '; '.join(fails))
only_seam = all('sparks = 3 per hit' in f for f in fails)
print('RESULT-AFTER-ERRATUM', 'the only contradicted clauses are the two seam-grain clauses of the prediction (3 per hit), replaced by the erratum clauses which hold' if fails and only_seam else ('no other contradiction' if not fails else 'OTHER CONTRADICTIONS'))
