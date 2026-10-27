"""cmp_probe.py - E20.h4c2 T0: compares the rows of the DLL probes (H4c2T0ProbeTests.cs.txt, CSV written to <rows dir>) with the predictions of t0/predictions.md (values read in the keys by t0/key_facts.py -> expected.json).
usage: python -B cmp_probe.py <rows dir> <expected.json> <annex e20-h4c2 dir>
Prints one block per probe: PROBE sN HOLDS / DIFFERS, the details, and the supplementary tick-by-tick comparison (information, not a stop)."""
import json
import os
import sys

R = sys.argv[1]
E = json.load(open(sys.argv[2]))
A = sys.argv[3]


def load(*p):
    return json.load(open(os.path.join(A, *p)))


KEYS = {
    'mid': load('critic', 'keys', 'cast_2c_pf_mid_x600y480.json'),
    'fix': load('rockfix', 'cast_2c_pf_rockfix.json'),
    'hb16fix': load('rockfix', 'cast_2c_pf_plateau_hb16_rockfix.json'),
    'hb17fix': load('rockfix', 'cast_2c_pf_plateau_hb17_rockfix.json'),
}
SCEN_KEY = {'mid': 'mid', 'mid_t2': 'mid', 'mid_t3': 'mid', 'edge': 'fix', 's9a': 'fix', 'hb16': 'hb16fix', 'hb17': 'hb17fix', 'water': 'fix', 'targets': 'fix', 'cancel100': 'fix'}


def csv(name, ext):
    p = os.path.join(R, name + '.' + ext + '.csv')
    rows = []
    if os.path.exists(p):
        for line in open(p):
            line = line.strip()
            if line:
                rows.append([int(x) for x in line.split(',')])
    return rows


ROCK_COLS = ['tick', 'slot', 'status', 'x', 'y', 'z', 'fx', 'fy', 'fz', 'dir', 'ta', 'flag', 'flags', 'fadj', 'ground', 'slope', 'fctc', 'bw', 'bd', 'bh', 'atk', 'pow', 'terrain']
C = {n: i for i, n in enumerate(ROCK_COLS)}


def lives(name):
    """the lives of the rocks: a list of dicts {slot, birth, rows{tick: row}} in the order of the births (tick, slot)"""
    rows = csv(name, 'rocks')
    by_tick = {}
    for r in rows:
        by_tick.setdefault(r[0], []).append(r)
    cur = {}
    out = []
    for t in sorted(by_tick):
        seen = set()
        for r in sorted(by_tick[t], key=lambda r: r[1]):
            s = r[1]
            seen.add(s)
            st = r[C['status']]
            if st == 0:
                continue
            life = cur.get(s)
            if life is None or life['last_status'] == 4 or life['last_tick'] != t - 1:
                life = dict(slot=s, birth=t, rows={}, last_status=0, last_tick=0)
                cur[s] = life
                out.append(life)
            life['rows'][t] = r
            life['last_status'] = st
            life['last_tick'] = t
    out.sort(key=lambda l: (l['birth'], l['slot']))
    return out


def verdict(name, ok, details):
    print('PROBE %s %s' % (name, 'HOLDS' if ok else 'DIFFERS'))
    for d in details:
        print('    ' + d)
    return ok


results = {}

# (s1) the generator
d = []
ok = True
for sc in ('mid', 'edge', 's9a', 'hb16', 'hb17'):
    ek = E['s1'][SCEN_KEY[sc]]
    gen = csv(sc, 'gen')
    bad = []
    for t, state, steps in gen:
        if t >= 330:
            continue
        if state != ek['rng0'][str(t + 1)] or steps != ek['steps'][str(t)]:
            bad.append((t, state, ek['rng0'][str(t + 1)], steps, ek['steps'][str(t)]))
    d.append('%-6s frames %d, mismatches %d %s' % (sc, len(gen), len(bad), bad[:3]))
    ok = ok and not bad and len(gen) >= 239
results['s1'] = verdict('s1', ok, d)

# (s2) the births
d = []
ok = True
for sc in ('mid', 'edge', 's9a', 'hb16', 'hb17', 'water'):
    kname = SCEN_KEY[sc]
    krows = sorted(E['s2'][kname], key=lambda q: (q['birth'], q['slot']))
    br = csv(sc, 'births')  # tick, counter, r1, r2, x, y, z, born, terrain, destroyed, ...
    lv = lives(sc)
    cat = {}
    ex = {}

    def bad(kind, msg):
        cat[kind] = cat.get(kind, 0) + 1
        ex.setdefault(kind, msg)

    run_ticks = max(r[0] for r in csv(sc, 'hero'))
    if len(br) != len([q for q in krows if q['birth'] <= run_ticks]):
        bad('births', 'births %d != key %d within the %d ticks of the run' % (len(br), len([q for q in krows if q['birth'] <= run_ticks]), run_ticks))
    pool = list(br)
    for q in krows:
        if q['birth'] > max(r[0] for r in csv(sc, 'hero')):
            continue
        m = [b for b in pool if b[0] == q['birth'] and b[2] == q['r1'] and b[3] == q['r2']]
        if not m:
            bad('tick/r1/r2', 'no birth for key id %d at %d (r1 %d r2 %d)' % (q['id'], q['birth'], q['r1'], q['r2']))
            continue
        b = m[0]
        pool.remove(b)
        want_destroy = 1 if (kname == 'hb17fix' and q['id'] in (9, 13, 14)) else 0
        if b[7] != 1:
            bad('born', 'id %d: no entity' % q['id'])
        if b[8] != q['t138']:
            bad('terrain', 'id %d: terrain %d (key %d)' % (q['id'], b[8], q['t138']))
        if b[9] != want_destroy:
            bad('destroyed', 'id %d: destroyed %d (want %d)' % (q['id'], b[9], want_destroy))
        if want_destroy:
            continue
        slot = b[10]
        cand = [l for l in lv if l['birth'] == q['birth'] and l['slot'] == slot]
        if not cand:
            bad('sample', 'id %d: no sample at the birth tick' % q['id'])
            continue
        r = cand[0]['rows'][q['birth']]
        if (r[C['x']], r[C['y']]) != (q['x'], q['y']):
            bad('x/y', 'id %d: x %d y %d (key %d %d; spawn %d %d)' % (q['id'], r[C['x']], r[C['y']], q['x'], q['y'], b[4], b[5]))
        if r[C['z']] != q['z'] - 1:
            bad('z', 'id %d: z %d (want %d)' % (q['id'], r[C['z']], q['z'] - 1))
        if r[C['status']] != q['st_birth']:
            bad('status', 'id %d: status %d (key %d)' % (q['id'], r[C['status']], q['st_birth']))
        if sc in ('mid', 'edge') and slot != q['slot']:
            bad('slot', 'id %d: slot %d (key %d)' % (q['id'], slot, q['slot']))
    d.append('%-6s births %d: differences by kind %s' % (sc, len(br), cat if cat else 'none'))
    for kind, msg in ex.items():
        d.append('         first %s: %s' % (kind, msg))
    ok = ok and not cat
results['s2'] = verdict('s2', ok, d)

# (s3) a birth outside the grid
q = [q for q in E['s2']['fix'] if q['id'] == 11][0]
lv = lives('edge')
cand = [l for l in lv if l['birth'] == q['birth']]
d = []
ok = bool(cand)
if cand:
    r = cand[0]['rows'][q['birth']]
    d.append('rock 11 at the birth tick %d: x %d (want %d) y %d (want %d) fadj %d status %d' % (q['birth'], r[C['x']], q['x'], r[C['y']], q['y'], r[C['fadj']], r[C['status']]))
    ok = r[C['x']] == q['x'] == 655360 and r[C['y']] == q['y'] and r[C['fadj']] != 0
else:
    d.append('no rock born at %d' % q['birth'])
results['s3'] = verdict('s3', ok, d)

# (s4) the end of the animation 1
d = []
ok = True
for sc in ('mid', 'mid_t2', 'mid_t3'):
    lv = lives(sc)
    shapes = {}
    bad = []
    for l in lv:
        rows = l['rows']
        e13 = next((t for t in sorted(rows) if rows[t][C['ta']] == 1), None)
        flag = next((t for t in sorted(rows) if rows[t][C['flag']] == 1), None)
        st4 = next((t for t in sorted(rows) if rows[t][C['status']] == 4), None)
        if e13 is None or flag is None or st4 is None:
            bad.append((l['birth'], e13, flag, st4))
            continue
        shape = (e13 - l['birth'], flag - e13, st4 - e13)
        shapes[shape] = shapes.get(shape, 0) + 1
        if flag - e13 != 46 or st4 - e13 != 47:
            bad.append((l['birth'], e13, flag, st4))
    d.append('%-7s lives %d  (E13 - birth, flag - E13, status4 - E13): %s ; off the prediction: %s' % (sc, len(lv), shapes, bad[:5]))
    ok = ok and not bad and len(lv) == 15
results['s4'] = verdict('s4', ok, d)

# (s5) the sounds
d = []
ok = True
for sc, kname in (('mid', 'mid'), ('edge', 'fix')):
    snd = csv(sc, 'sounds')
    mine = sorted((t, i) for t, i in snd if i in (45, 55))
    want = sorted(tuple(x) for x in E['s5'][kname])
    other = sorted(set(i for _, i in snd if i not in (45, 55)))
    d.append('%-5s 55/45 sounds %d (key %d) equal: %s ; other ids played: %s' % (sc, len(mine), len(want), mine == want, other))
    if mine != want:
        d.append('   only mine: %s ; only key: %s' % ([x for x in mine if x not in want][:6], [x for x in want if x not in mine][:6]))
    ok = ok and mine == want
results['s5'] = verdict('s5', ok, d)

# (s6) the attack and the box
lv = lives('mid')
bad = []
n = 0
statuses = {}
for l in lv:
    for t, r in l['rows'].items():
        n += 1
        statuses[r[C['status']]] = statuses.get(r[C['status']], 0) + 1
        if (r[C['atk']], r[C['pow']]) != (8, 64) or (r[C['bw']], r[C['bd']], r[C['bh']]) != (4718591, 3145727, 2555903):
            bad.append((l['birth'], t, r[C['status']], r[C['ta']], r[C['atk']], r[C['pow']], r[C['bw']], r[C['bd']], r[C['bh']]))
d = ['rows checked %d (statuses %s), off the prediction %d %s' % (n, statuses, len(bad), bad[:5])]
results['s6'] = verdict('s6', not bad and n > 0, d)

# (s7) the slope and the deep water
d = []
ok = True
for sc in ('mid', 'edge', 'water'):
    lv = lives(sc)
    slopes = set()
    for l in lv:
        for r in l['rows'].values():
            slopes.add(r[C['slope']])
    d.append('%-6s lives %d slopes seen %s' % (sc, len(lv), sorted(slopes)))
    ok = ok and slopes == {0}
lv = lives('water')
keylast = [q['st4'] for q in sorted(E['s2']['fix'], key=lambda q: (q['birth'], q['slot']))]
mylast = [max(l['rows']) for l in lv]
d.append('water: last tick of each rock %s' % mylast)
d.append('water: key (fix) st4          %s' % keylast)
ok = ok and mylast == keylast
results['s7'] = verdict('s7', ok, d)

# (s8) the draws per hit
gen = csv('targets', 'gen')
hero = csv('targets', 'hero')
br = csv('targets', 'births')
births = {}
for b in br:
    births[b[0]] = births.get(b[0], 0) + 1
sparks = {h[0]: h[12] for h in hero}
bad = []
hits = 0
for t, state, steps in gen:
    h = sparks.get(t, 0)
    hits += h
    if steps - 1 - 2 * births.get(t, 0) != 9 * h:
        bad.append((t, steps, births.get(t, 0), h))
d = ['ticks %d, hits %d, ticks off the prediction %d %s' % (len(gen), hits, len(bad), bad[:8])]
results['s8'] = verdict('s8', not bad and hits >= 1, d)

# (s9a) IsOnGround of a rock held in the air (mode None)
d = []
ok = True
for sc, bid, g, kn in (('s9a', 162, 194, 'fix'), ('hb16', 114, 146, 'hb16fix'), ('hb17', 114, 146, 'hb17fix')):
    lv = [l for l in lives(sc) if l['birth'] == bid]
    if not lv:
        d.append('%s: no rock born at %d' % (sc, bid))
        ok = False
        continue
    rows = lv[0]['rows']
    air = [t for t in range(bid, g) if t in rows]
    bad = [(t, rows[t][C['ground']]) for t in air if rows[t][C['ground']] != 0]
    at = rows.get(g)
    d.append('%-5s rock born %d: ticks %d..%d sampled %d, IsOnGround != 0 in the air: %s ; at the ground tick %d: %s' % (sc, bid, bid, g - 1, len(air), bad[:4], g, at[C['ground']] if at else 'no row'))
    d.append('       (info) ForceAdjusted != 0 at ticks %s ; status at the ground tick %s' % ([t for t in sorted(rows) if rows[t][C['fadj']] != 0][:3] + ['...'] + [t for t in sorted(rows) if rows[t][C['fadj']] != 0][-2:], at[C['status']] if at else None))
    ok = ok and not bad and len(air) == g - bid and at is not None and at[C['ground']] == 1
results['s9a'] = verdict('s9a', ok, d)

# (s10a) the bounds read in the keys
d = []
ok = True
s10 = E['s10a']
r8 = s10['hb16fix']['8']
r8b = s10['hb17fix']['8']
want = {'138': [102240, 45440], '139': [102240, 45440], '140': [102240, 45440], '141': [53472, 23764], '142': [0, 0], '146': [0, 0]}
for t, v in want.items():
    ok = ok and r8.get(t) == v and r8b.get(t) == v
d.append('rock 8 steps per tick (hb16fix == hb17fix): %s' % {t: r8.get(t) for t in want})
g = s10['hb16fix']['8_ground']
d.append('rock 8 ground tick %d at x %d y %d (plan: 146, 10289152, 4719956)' % tuple(g))
ok = ok and g == [146, 10289152, 4719956] and s10['hb17fix']['8_ground'] == g
bound = 7 * 102240
d.append('bound of the rock 8: 7 x 102240 = %d raw units = %.2f px on each axis' % (bound, bound / 65536))
r11 = s10['fix']['11']
okk = all(r11[str(t)] == [0, 80320] for t in (163, 164, 165, 192, 193, 194))
d.append('rock 11: dx = 0 and dy = 80320 at ticks 163..165, 192..194: %s ; ground %s (plan: 194, x 655360, y 9204160)' % (okk, s10['fix']['11_ground']))
ok = ok and okk and s10['fix']['11_ground'] == [194, 655360, 9204160] and s10['hb16fix']['11_ground'] == [194, 655360, 9204160] and s10['hb17fix']['11_ground'] == [194, 655360, 9204160]
d.append('bound of the rock 11: 0 on each axis')
results['s10a'] = verdict('s10a', ok and bound == 715680, d)

# (s11) E20.c2b present
hero = csv('cancel100', 'hero')
H = {h[0]: h for h in hero}
notes = open(os.path.join(R, 'cancel100.notes.txt')).read()
snd = csv('cancel100', 'sounds')
d = []
ok = True
d.append('tick 99: hero animation %#x item %d phase %d counter %d' % (H[99][1], H[99][2], H[99][3], H[99][4]))
d.append('tick 100: hero animation %#x item %d phase %d counter %d' % (H[100][1], H[100][2], H[100][3], H[100][4]))
ok = ok and H[99][2] == 44 and H[99][1] == 0x33 and H[100][1] == 0x31 and H[100][2] == 0
cancel_line = [l for l in notes.splitlines() if l.startswith('cancel ticks')][0]
d.append(cancel_line)
ok = ok and cancel_line.split(':')[1].split() == ['100']
s23 = [(t, i) for t, i in snd if i == 23]
d.append('sound 23: %s' % s23)
ok = ok and s23 == [(100, 23)]
seq = []
for t in sorted(H):
    if t >= 99 and (not seq or seq[-1][1] != H[t][1]):
        seq.append((t, H[t][1], H[t][2]))
d.append('(info) hero animation changes from 99: %s ; key: %s' % (seq, E['s11']['seq']))
results['s11'] = verdict('s11', ok, d)

# (s12) the cell under the hero
d = []
ok = True
first = open(os.path.join(R, 'mid.notes.txt')).read().splitlines()[0]
d.append('real map 14: ' + first)
ok = ok and '(& 0x180 = 0)' in first
for sc in ('mid', 'edge', 'water', 'cancel100'):
    hero = csv(sc, 'hero')
    last = 99 if sc == 'cancel100' else 10 ** 9
    badv = [h[0] for h in hero if h[0] <= last and (h[8] & 0x180) != 0]
    bada = [(h[0], h[1]) for h in hero if h[0] <= last and h[1] in (0x31, 0x3A, 0x1C)]
    d.append('%-9s ticks %d, hero vram flags & 0x180 != 0 at %s ; hero in 0x31/0x3A/0x1C at %s' % (sc, len(hero), badv[:3], bada[:3]))
    ok = ok and not badv and not bada
results['s12'] = verdict('s12', ok, d)

# supplementary: the rocks with E13 against the keys, tick by tick
print('== supplementary (information): the rocks with E13 against the key, tick by tick')
for sc, kname in (('mid', 'mid'), ('edge', 'fix'), ('water', 'fix'), ('mid_sub', 'mid'), ('edge_sub', 'fix'), ('hb16_sub', 'hb16fix'), ('hb17_sub', 'hb17fix')):
    key = KEYS[kname]
    lv = lives(sc)
    krows = sorted(key['rows'], key=lambda r: (r['birth'], r['slot']))
    diffs = {}
    first = {}
    last_tick = max((r[0] for r in csv(sc, 'rocks')), default=0)
    for l, kr in zip(lv, krows):
        for tr in kr['track']:
            t = tr[0]
            if t > last_tick:
                continue
            r = l['rows'].get(t)
            if r is None:
                diffs['missing'] = diffs.get('missing', 0) + 1
                first.setdefault('missing', (kr['id'], t))
                continue
            want = dict(status=tr[1], ta=tr[2], x=tr[3], y=tr[4], z=tr[5] - 1, flag=tr[6], ground=tr[8])
            for f, v in want.items():
                if r[C[f]] != v:
                    diffs[f] = diffs.get(f, 0) + 1
                    first.setdefault(f, (kr['id'], t, r[C[f]], v))
    print('  %-6s lives %d (key %d): field differences %s' % (sc, len(lv), len(krows), diffs))
    for f, v in first.items():
        print('      first %s: %s' % (f, v))

# (s2) diagnostic: where the difference comes from
print('== (s2) diagnostic: the position at the birth tick against floor(spawn position) + force of the tick')
for sc in ('mid', 'edge'):
    br = csv(sc, 'births')  # tick, counter, r1, r2, x, y, z, born, terrain, destroyed, slot, posx, posy, posz, fx, fy, fz, dir, status, fadj, joins
    rows = csv(sc, 'rocks')
    tot = 0
    eq_floor = 0
    eq_spawn = 0
    other = []
    for b in br:
        r = [x for x in rows if x[0] == b[0] and x[1] == b[10]][0]
        fx, fy = r[C['fx']], r[C['fy']]
        tot += 1
        if (r[C['x']], r[C['y']]) == ((b[4] >> 16 << 16) + fx, (b[5] >> 16 << 16) + fy):
            eq_floor += 1
        elif (r[C['x']], r[C['y']]) == (b[4] + fx, b[5] + fy):
            eq_spawn += 1
        else:
            other.append((b[0], r[C['x']], r[C['y']], b[4] + fx, b[5] + fy))
    print('  %-5s births %d: sample == floor(spawn) + force: %d ; == spawn + force: %d (whole-pixel spawns) ; other: %s' % (sc, tot, eq_floor, eq_spawn, other))

print()
print('SUMMARY', ' '.join('%s=%s' % (k, 'ok' if v else 'DIFF') for k, v in results.items()))
