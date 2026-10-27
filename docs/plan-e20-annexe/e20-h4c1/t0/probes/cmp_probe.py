"""cmp_probe.py (h4c1 T0): compares the CSV rows of the throwaway probe of the DLL (H4c1T0ProbeTests.cs.txt) with the filed keys and checks the nine predictions P1 to P9 of t0/predictions.md.
usage: python -B cmp_probe.py KEYDIR PROBEDIR   (exit code 0 iff P1..P9 all hold). Read-only."""
import csv
import json
import os
import sys
from collections import Counter, defaultdict

KEYDIR, OUT = sys.argv[1], sys.argv[2]
NAMES = ['earth1_flat_east', 'earth1_flat_south', 'earth1_flat_west', 'earth1_flat_north', 'earth1_flat_east_x200', 'earth1_deepwater_cols7_9']
COLS = 'tick slot status px py pz fx fy fz adir dir anim_t anim_c ac flags adj ground slope c1d0 raw_w raw_d raw_h ev_w ev_d ev_h type'.split()  # 26 columns, as Row() of the probe writes them
BOX42 = (2752511, 1835007, 720895)
BOX23 = (1507327, 1507327, 983039)
fails = Counter()
notes = []


def fail(p, msg):
    fails[p] += 1
    if fails[p] <= 6:
        notes.append('  FAIL %s: %s' % (p, msg))


def load_probe(name):
    rows = []
    with open(os.path.join(OUT, name + '.rocks.csv')) as f:
        for line in f:
            v = line.strip().split(',')
            if len(v) != len(COLS):
                continue
            rows.append(dict(zip(COLS, [int(x) for x in v])))
    return rows


def lives_of(rows):
    """lives = per slot, a new life starts at a status 2 row that follows an absent tick or a status 4 row."""
    byslot = defaultdict(list)
    for r in rows:
        byslot[r['slot']].append(r)
    lives = []
    for slot, rs in byslot.items():
        cur = []
        for r in rs:
            if cur and (r['tick'] != cur[-1]['tick'] + 1 or cur[-1]['status'] == 4):
                lives.append(cur)
                cur = []
            cur.append(r)
        if cur:
            lives.append(cur)
    return sorted(lives, key=lambda l: (l[0]['tick'], l[0]['slot']))


def key_lives(rows):
    cur = {}
    done = []
    for r in rows:
        t = r['tick']
        seen = set()
        for s, e in r['rocks'].items():
            seen.add(s)
            rec = dict(e, tick=t, slot=int(s))
            if e['cnt1c'] == 1 and s in cur:
                done.append(cur.pop(s))
            cur.setdefault(s, []).append(rec)
        for s in list(cur):
            if s not in seen:
                done.append(cur.pop(s))
    done.extend(cur.values())
    return sorted(done, key=lambda l: (l[0]['tick'], l[0]['slot']))


total_diff = Counter()
row_delay = Counter()
for name in NAMES:
    key = json.load(open(os.path.join(KEYDIR, name + '.json')))
    krows = key['rows']
    hero_x = key['meta']['hero'][0] * 65536
    probe = load_probe(name)
    plives, klives = lives_of(probe), key_lives(krows)
    notes.append('== %s: probe rows %d lives %d | key lives %d' % (name, len(probe), len(plives), len(klives)))
    if len(plives) != len(klives):
        fail('lives', '%s: %d lives in the probe, %d in the key' % (name, len(plives), len(klives)))
    pby = {(r['tick'], r['slot']): r for r in probe}
    kby = {(r['tick'], r['slot']): r for l in klives for r in l}

    # ---- supplementary: the whole comparison, field by field
    mapping = [('status', 'status'), ('px', 'px'), ('py', 'py'), ('fx', 'fx'), ('fy', 'fy'), ('fz', 'fz'), ('adir', 'adir'), ('dir', 'dir_t'), ('anim_t', 'anim_t'), ('ac', 'reset_AC'), ('flags', 'flags6C'),
               ('adj', 'adj_13C'), ('ground', 'ground_144'), ('slope', 'slope_18C'), ('c1d0', 'c1d0'), ('ev_w', 'bw'), ('ev_d', 'bd'), ('ev_h', 'bh')]
    diffs = Counter()
    only_probe = sorted(set(pby) - set(kby))
    only_key = sorted(set(kby) - set(pby))
    for k in sorted(set(pby) & set(kby)):
        p, q = pby[k], kby[k]
        for a, b in mapping:
            if p[a] != q[b]:
                diffs[a] += 1
        if p['pz'] != q['pz'] - 1:
            diffs['pz(-1)'] += 1
        pc = 0 if p['anim_c'] == 4294967295 else p['anim_c']
        if pc != q['anim_c']:
            diffs['anim_c'] += 1
    notes.append('   whole comparison: %d (tick, slot) pairs in both, only in the probe %d %s, only in the key %d %s, field differences %s' % (
        len(set(pby) & set(kby)), len(only_probe), only_probe[:4], len(only_key), only_key[:4], dict(diffs) or 'none'))
    for a, n in diffs.items():
        total_diff[a] += n

    # ---- P1, P2, P3, P5, P7, P8 per life
    s3_ticks = []
    for i, (pl, kl) in enumerate(zip(plives, klives)):
        t0 = pl[0]['tick']
        rec = {r['tick']: r for r in pl}
        s3 = next((r['tick'] for r in pl if r['status'] == 3), None)
        ks3 = next((r['tick'] for r in kl if r['status'] == 3), None)
        if s3 != ks3:
            fail('P5', '%s life %d (slot %d, birth %d): status 3 at %s, key %s' % (name, i, pl[0]['slot'], t0, s3, ks3))
        if t0 != kl[0]['tick'] or pl[0]['slot'] != kl[0]['slot']:
            fail('P9', '%s life %d: probe birth %d slot %d, key birth %d slot %d' % (name, i, t0, pl[0]['slot'], kl[0]['tick'], kl[0]['slot']))
        # P2 as written (at the birth tick) and P2b (from the third tick of the life, the row the DLL keeps)
        if pl[0]['adir'] != kl[0]['adir']:
            fail('P2', '%s life %d (slot %d, birth %d): row %d at the birth tick, key %d' % (name, i, pl[0]['slot'], t0, pl[0]['adir'], kl[0]['adir']))
        late = [(r['tick'], r['adir'], k['adir']) for r, k in zip(pl, kl) if r['tick'] >= t0 + 2 and r['adir'] != k['adir']]
        if late:
            fail('P2b', '%s life %d: row differs from the third tick of the life: %s' % (name, i, late[:3]))
        first_ok = next((r['tick'] - t0 for r, k in zip(pl, kl) if r['adir'] == k['adir'] and all(x['adir'] == y['adir'] for x, y in zip(pl[r['tick'] - t0:], kl[r['tick'] - t0:]))), None)
        row_delay[first_ok] += 1
        # P8: the record at the birth tick
        b, kb = pl[0], kl[0]
        if not (b['status'] == 2 and b['px'] == hero_x + b['fx'] and b['pz'] == kb['pz'] - 1 and b['fz'] == kb['fz'] and b['c1d0'] == 0 and b['px'] == kb['px'] and b['py'] == kb['py']):
            fail('P8', '%s life %d (slot %d, birth %d): probe x=%d z=%d fz=%d status=%d, key x=%d z=%d fz=%d' % (name, i, b['slot'], t0, b['px'], b['pz'], b['fz'], b['status'], kb['px'], kb['pz'], kb['fz']))
        if len(pl) > 1 and pl[1]['pz'] != kl[1]['pz'] - 1:
            fail('P8', '%s life %d: the second tick Z %d, key %d' % (name, i, pl[1]['pz'], kl[1]['pz']))
        # P3 for every tick of the life
        row = kl[0]['adir']  # the key's row (the DLL keeps it from the third tick of the life: P2b)
        for r in pl:
            want = BOX42
            if s3 is not None and row == 0 and s3 + 10 <= r['tick'] <= s3 + 13:
                want = BOX23
            if (r['ev_w'], r['ev_d'], r['ev_h']) != want:
                fail('P3', '%s life %d tick %d (S3=%s, row %d): evaluated box %s, expected %s' % (name, i, r['tick'], s3, row, (r['ev_w'], r['ev_d'], r['ev_h']), want))
            if (r['raw_w'], r['raw_d'], r['raw_h']) != want:
                fail('P3raw', '%s life %d tick %d: raw box %s, expected %s' % (name, i, r['tick'], (r['raw_w'], r['raw_d'], r['raw_h']), want))
        if s3 is None:
            # deep water: no E3, the status 4 at L+1, nothing else
            continue
        s3_ticks.append((s3, pl[0]['tick'], pl[0]['dir'], pl[0]['slot']))
        L = s3 - 1
        # P1
        if (rec[s3]['px'], rec[s3]['py']) != (rec[L]['px'], rec[L]['py']):
            fail('P1', '%s life %d (slot %d): step at the E3 tick (%d, %d)' % (name, i, pl[0]['slot'], rec[s3]['px'] - rec[L]['px'], rec[s3]['py'] - rec[L]['py']))
        # P5 position at the status 3 equals the key
        kr = {r['tick']: r for r in kl}
        if (rec[s3]['px'], rec[s3]['py']) != (kr[ks3]['px'], kr[ks3]['py']):
            fail('P5', '%s life %d: position at the status 3 %s, key %s' % (name, i, (rec[s3]['px'], rec[s3]['py']), (kr[ks3]['px'], kr[ks3]['py'])))
        # P7
        for r in pl:
            d = r['tick'] - s3
            if r['tick'] >= s3:
                want_ac = 1 if d >= 12 else 0
                if r['ac'] != want_ac:
                    fail('P7', '%s life %d tick %d (S3+%d): AC %d, expected %d' % (name, i, r['tick'], d, r['ac'], want_ac))
        if pl[-1]['tick'] != s3 + 13 or pl[-1]['status'] != 4:
            fail('P7', '%s life %d: last tick %d status %d, expected S3+13 = %d status 4' % (name, i, pl[-1]['tick'], pl[-1]['status'], s3 + 13))
        if any(r['status'] == 4 and r['tick'] != s3 + 13 for r in pl):
            fail('P7', '%s life %d: a status 4 before S3+13' % (name, i))
    # P5 (east): the three lives that stop at the west edge (births 77, 87, 92; slots 12, 3, 14)
    if name == 'earth1_flat_east':
        got = {(b, sl): s for s, b, d, sl in s3_ticks if (b, sl) in ((77, 12), (87, 3), (92, 14))}
        notes.append('   P5 east: status 3 of the lives (birth, slot) %s' % (sorted(got.items()),))
        if sorted(got.values()) != [94, 103, 109]:
            fail('P5', 'east: status 3 of the west-edge rocks at %s, predicted [94, 103, 109]' % (sorted(got.values()),))
        notes.append('   P5 east: S3 - birth for every life: %s' % (sorted(Counter(s - b for s, b, d, sl in s3_ticks).items()),))

    # ---- P4 (deep water): slope timeline
    if name == 'earth1_deepwater_cols7_9':
        n4 = 0
        for i, (pl, kl) in enumerate(zip(plives, klives)):
            ks = [r['slope_18C'] for r in kl]
            if 4 in ks:
                n4 += 1
                first4 = next(r['tick'] for r in pl if r['slope'] == 4)
                kfirst4 = next(r['tick'] for r in kl if r['slope_18C'] == 4)
                if first4 != kfirst4:
                    fail('P4', 'life %d: first slope 4 at %d, key %d' % (i, first4, kfirst4))
                L = first4
                for r in pl:
                    want = 4 if r['tick'] >= L else 0
                    if r['slope'] != want:
                        fail('P4', 'life %d tick %d: slope %d, expected %d' % (i, r['tick'], r['slope'], want))
                if not (pl[-1]['status'] == 4 and pl[-1]['tick'] == L + 1):
                    fail('P4', 'life %d: last row tick %d status %d, expected status 4 at L+1 = %d' % (i, pl[-1]['tick'], pl[-1]['status'], L + 1))
                if any(r['ac'] != 0 or r['anim_t'] != 0 for r in pl):
                    fail('P4', 'life %d: a rock in deep water ran E3 (AC or TargetAnimationId non zero)' % i)
            else:
                if any(r['slope'] != 0 for r in pl):
                    fail('P4', 'dry life %d has a non-zero slope' % i)
        notes.append('   P4: lives that touch the deep water: %d' % n4)
        if n4 != 14:
            fail('P4', 'predicted 14 lives on the deep water, found %d' % n4)

    # ---- P6: the sounds
    snd = Counter()
    with open(os.path.join(OUT, name + '.sounds.csv')) as f:
        for line in f:
            if line.strip():
                t, s = line.strip().split(',')
                snd[(int(t), int(s))] += 1
    ksnd = Counter()
    for r in krows:
        for s in r['sounds']:
            ksnd[(r['tick'], int(s[4:-1]))] += 1
    if snd != ksnd:
        fail('P6', '%s: sounds differ: probe-only %s, key-only %s' % (name, sorted((snd - ksnd).items())[:6], sorted((ksnd - snd).items())[:6]))
    notes.append('   P6: sounds %d (probe), %d (key); all sfx ids %s' % (sum(snd.values()), sum(ksnd.values()), sorted({s for _, s in snd})))

    # ---- P9: the slots of the births, in order, and the slots 1 and 2 at the ticks 51 and 52 (east)
    pslots = [l[0]['slot'] for l in plives]
    kslots = [l[0]['slot'] for l in klives]
    if pslots != kslots:
        fail('P9', '%s: slots of the births %s, key %s' % (name, pslots, kslots))
    if name == 'earth1_flat_east':
        if not (pby[(51, 1)]['status'] == 4 and pby[(51, 2)]['status'] == 4 and pby[(52, 1)]['status'] == 2 and pby[(52, 2)]['status'] == 2 and pby[(52, 1)]['pz'] == 229377):
            fail('P9', 'east: the slots 1 and 2 at the ticks 51 and 52 are not (4, 4) then (2, 2) born')
        notes.append('   P9 east: births slots %s' % pslots)

    # ---- the hero and the item (supplementary): the item, the phase, the counter, the animation, the position, tick by tick
    hero = {}
    with open(os.path.join(OUT, name + '.hero.csv')) as f:
        for line in f:
            v = [int(x) for x in line.strip().split(',')]
            hero[v[0]] = v
    hd = 0
    for r in krows:
        h = hero.get(r['tick'])
        it = r['item']
        want = [r['tick'], it['hero_anim'], it['item_in_use'], it['phase'], it['counter']]
        # the key's counter is the counter AFTER the tick's increment; the probe reads the same field at the same pass
        if h is None or h[1] != it['hero_anim'] or h[2] != it['item_in_use'] or h[3] != it['phase']:
            hd += 1
            if hd <= 3:
                notes.append('   hero/item difference at tick %d: probe %s, key %s' % (r['tick'], h, it))
    notes.append('   hero/item (animation, item in use, phase) differences over %d ticks: %d' % (len(krows), hd))
    if name in ('earth1_flat_east',):
        notes.append('   item freed: probe ticks with item 0: first %s; key first %s' % (next((t for t in sorted(hero) if hero[t][2] == 0), None), next((r['tick'] for r in krows if r['item']['item_in_use'] == 0), None)))

print('\n'.join(notes))
print('row (AnimationDirection) first equal to the key at life tick index (0 = birth tick):', dict(row_delay))
print('supplementary field differences over the six keys (info, not a stop):', dict(total_diff) or 'none')
print('PREDICTIONS:', {p: ('FAIL x%d' % fails[p]) if fails[p] else 'holds' for p in ['P1', 'P2', 'P3', 'P4', 'P5', 'P6', 'P7', 'P8', 'P9']})
extra = {p: n for p, n in fails.items() if p not in ('P1', 'P2', 'P3', 'P4', 'P5', 'P6', 'P7', 'P8', 'P9')}
if extra:
    print('other failures:', extra)
sys.exit(1 if fails else 0)
