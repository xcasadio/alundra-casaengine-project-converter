"""cmp_probe.py - E20.h4d2 T0: compares the rows written by the throwaway probe tests (H4d2T0ProbeTests, DLL) with the expected values read in the remade keys (t0/expected.json), probe by probe (P1 to P15).
usage: python -B cmp_probe.py EXPECTED_JSON PROBE_OUT_DIR   (prints PASS/FAIL per clause; exit code 0 iff no FAIL)
DLL entity row: [type, slot, status, x, y, z, fx, fy, fz, flags, anim_t, dir_t, animend, b274, b278_slot, ground, slope, atk_attr, atk_pow, hits, w, d, h, ox, oy, oz]
key row (expected.json 'fields'): slot, status, x, y, z(DLL = key - 1), fx, fy, fz, flags, anim_t, dir_t, animend, b274, b278_slot, ground, slope, atk_attr, atk_pow"""
import json
import os
import sys

EXP = json.load(open(sys.argv[1]))
OUT = sys.argv[2]
FAILS = []
CLAUSES = []

# DLL column index -> key column index
MAP = {1: 0, 2: 1, 3: 2, 4: 3, 5: 4, 6: 5, 7: 6, 8: 7, 9: 8, 10: 9, 11: 10, 12: 11, 13: 12, 14: 13, 15: 14, 16: 15, 17: 16, 18: 17}
NAMES = ['slot', 'status', 'x', 'y', 'z', 'fx', 'fy', 'fz', 'flags', 'anim_t', 'dir_t', 'animend', 'b274', 'b278_slot', 'ground', 'slope', 'atk_attr', 'atk_pow']


def check(name, ok, detail=''):
    CLAUSES.append((name, ok, detail))
    if not ok:
        FAILS.append(name)
    print(('PASS ' if ok else 'FAIL ') + name + ((' :: ' + detail) if detail else ''))


def rows(name):
    p = os.path.join(OUT, name + '.rows.jsonl')
    return [json.loads(l) for l in open(p) if l.strip()]


def csv(name, ext):
    p = os.path.join(OUT, name + '.' + ext)
    return [l.strip().split(',') for l in open(p) if l.strip()]


def ents(rs):
    """per entity (birth order): tick -> DLL row"""
    out = []
    for r in rs:
        for i, e in enumerate(r['ents']):
            while len(out) <= i:
                out.append({})
            out[i][r['t']] = e
    return out


def first(d, pred):
    for t in sorted(d):
        if pred(d[t]):
            return t
    return None


def kents(sc):
    return EXP['scenarios'][sc]['ents']


def ktick(e, t):
    return e['ticks'].get(str(t))


def live(e):
    return e[2] in (2, 3, 4) and e[1] >= 0


# ------------------------------------------------------------------ cast
rs = rows('cast')
D = ents(rs)
K = kents('fire1_cast_pf')
check('cast: number of entities born (1 column + 11 balls)', len(D) == len(K) == 12, 'dll %d key %d' % (len(D), len(K)))

# P1
c = D[0]
kc = K[0]
b = first(c, lambda e: e[2] != 0)
check('P1 column born at tick 34', b == 34 and kc['birth'] == 34, 'dll %s' % b)
check('P1 column in slot 1, status Normal at S, Z = key - 1 (1179649), flags 8260, attack (10, 64)', c[34][1] == 1 and c[34][2] == 2 and c[34][5] == 1179649 and c[34][9] == 8260 and c[34][17] == 10 and c[34][18] == 64,
      'slot %s status %s x,y,z %s %s %s flags %s atk %s %s' % (c[34][1], c[34][2], c[34][3], c[34][4], c[34][5], c[34][9], c[34][17], c[34][18]))
check('P1 only one entity of the type 20', sum(1 for e in D if e[min(e)][0] == 20) == 1)
# P2
births = [first(e, lambda x: x[2] != 0) for e in D[1:]]
check('P2 balls born at 34 + 8k, k = 0..10', births == [34 + 8 * k for k in range(11)], str(births))
check('P2 slots 2,3,4,5,6,7,8,9,2,10,11', [e[b][1] for e, b in zip(D[1:], births)] == [2, 3, 4, 5, 6, 7, 8, 9, 2, 10, 11], str([e[b][1] for e, b in zip(D[1:], births)]))
check('P2 directions 4,27,18,1,4,22,29,5,13,26,21', [e[b][11] for e, b in zip(D[1:], births)] == [4, 27, 18, 1, 4, 22, 29, 5, 13, 26, 21], str([e[b][11] for e, b in zip(D[1:], births)]))
check('P2 at S: ForceZ 524288, Z 1572865, b274 16; S+1: b274 15; first b274 == 0 at S+16', all(e[b][8] == 524288 and e[b][5] == 1572865 and e[b][13] == 16 and e[b + 1][13] == 15 and e[b + 16][13] == 0 and e[b + 15][13] == 1 for e, b in zip(D[1:], births)),
      str([(e[b][8], e[b][5], e[b][13], e[b + 1][13], e[b + 15][13], e[b + 16][13]) for e, b in zip(D[1:], births)][:2]))
# P3
gen = csv('cast', 'gen.csv')
kc_ = EXP['scenarios']['fire1_cast_pf']
bad = [(int(t), int(s), int(n)) for t, s, n in gen if int(t) < 200 and (int(s) != kc_['rng0'][str(int(t) + 1)] or int(n) != kc_['draws'][t] + 1)]
check('P3 generator state = rng0(t+1) and steps = draws + 1 at t = 1..199', not bad, 'bad ticks (t, state, steps): %s' % bad[:5])
check('P3 total steps of the 200 frames', sum(int(n) for _, _, n in gen) == sum(kc_['draws'][str(t)] + 1 for t in range(1, 201)), '%d vs %d' % (sum(int(n) for _, _, n in gen), sum(kc_['draws'][str(t)] + 1 for t in range(1, 201))))
# P4 / P5 / P6
s3 = [first(e, lambda x: x[2] == 3) for e in D[1:]]
ks3 = [first({int(k): v for k, v in e['ticks'].items()}, lambda x: x[1] == 3) for e in K[1:]]
check('P4/P5 status 3 tick of each ball = the key', s3 == ks3, 'dll %s key %s' % (s3, ks3))
air = [(i + 1, s3[i]) for i in range(11) if D[i + 1][s3[i]][15] == 0]
kair = [(i + 1, ks3[i]) for i in range(11) if K[i + 1]['ticks'][str(ks3[i])][14] == 0]
check('P4 balls stopped in the air (ground 0 at the status 3): same balls, same ticks (86, 109, 118, 135, 162 ...)', air == kair, 'dll %s key %s' % (air, kair))
g1 = [first(e, lambda x: x[15] == 1) for e in D[1:]]
kg1 = [first({int(k): v for k, v in e['ticks'].items()}, lambda x: x[14] == 1) for e in K[1:]]
check('P5 landing tick (IsOnGround) of each ball = the key (109 for the ball born at 42)', g1 == kg1 and g1[1] == 109, 'dll %s key %s' % (g1, kg1))
check('P5 at L+1: status 3, TargetAnimationId 1, flags 0x102044 (1056836), for the balls that land', all(D[i + 1][g1[i] + 1][2] == 3 and D[i + 1][g1[i] + 1][10] == 1 and D[i + 1][g1[i] + 1][9] == 1056836 for i in range(11) if g1[i] is not None and s3[i] == g1[i] + 1),
      'ex ball 2 at 110: %s' % D[2][110][:12])
dec = []
for i in range(11):
    t = s3[i]
    dec.append([D[i + 1][k][6] - D[i + 1][k + 1][6] for k in range(t, t + 3)])
kdec = [[K[i + 1]['ticks'][str(k)][5] - K[i + 1]['ticks'][str(k + 1)][5] for k in range(ks3[i], ks3[i] + 3)] for i in range(11)]
check('P5 fx decreases by the key\'s constant after the status 3 (ball born at 42: 10224; the 8688 of the plan are the balls of direction 4)', dec == kdec and dec[1] == [10224] * 3 and dec[0] == [-8688] * 3, 'dll ball2 %s ball1 %s' % (dec[1], dec[0]))
ae = [first(e, lambda x: x[12] == 1) for e in D[1:]]
kae = [first({int(k): v for k, v in e['ticks'].items()}, lambda x: x[11] == 1) for e in K[1:]]
check('P6 +0xAC first 1 at status3 + 10', ae == kae and all(a == s + 10 for a, s in zip(ae, s3)), 'dll %s key %s' % (ae, kae))
s4 = [first(e, lambda x: x[2] == 4) for e in D[1:]]
ks4 = [first({int(k): v for k, v in e['ticks'].items()}, lambda x: x[1] == 4) for e in K[1:]]
check('P6 FlagToDestroy at status3 + 11', s4 == ks4 and all(a == s + 11 for a, s in zip(s4, s3)), 'dll %s key %s' % (s4, ks4))
dead = [first(e, lambda x: x[2] == 0 and x[1] < 0) for e in D[1:]]
check('P6 (info) Destroyed with Slot < 0 at status3 + 12', all(a == s + 12 for a, s in zip(dead, s3)), str(dead))
# P7
a7 = first(c, lambda e: e[12] == 1)
f7 = first(c, lambda e: e[2] == 4)
d7 = first(c, lambda e: e[2] == 0 and e[1] < 0)
check('P7 column: +0xAC at 120 (S+86), FlagToDestroy at 121 (S+87), Destroyed and Slot < 0 at 122 (S+88)', (a7, f7, d7) == (120, 121, 122), '%s %s %s' % (a7, f7, d7))
check('P7 no break effect at the end of the column', not [l for l in csv('cast', 'effects.csv') if len(l) > 1], str(csv('cast', 'effects.csv')))
# P9
check('P9 ball 1 at S+17: CurrentAttack.Attribute & 0xF == 10, Power 32; column (10, 64)', (D[1][51][17] & 0xF) == 10 and D[1][51][18] == 32 and D[0][34][17] == 10 and D[0][34][18] == 64, 'ball %s %s' % (D[1][51][17], D[1][51][18]))
# P8 boxes
cb = D[0][34]
bb = D[1][34]
check('P8 column box at birth 1507327 x 1048575 x 3080191', tuple(cb[20:23]) == (1507327, 1048575, 3080191), str(cb[20:26]))
check('P8 ball box at birth 1507327 x 983039 x 983039', tuple(bb[20:23]) == (1507327, 983039, 983039), str(bb[20:26]))
# info: whole rows against the key
diffs = []
nrows = 0
for i in range(12):
    for t, e in D[i].items():
        k = ktick(K[i], t)
        if k is None or not live(e):
            continue
        nrows += 1
        for dc, kc2 in MAP.items():
            kv = k[kc2]
            if NAMES[kc2] == 'b278_slot' and kv is None:
                kv = 0
            if e[dc] != kv:
                diffs.append((i, t, NAMES[kc2], e[dc], kv))
                break
print('INFO cast: %d live rows compared with the key, %d rows differ; first: %s' % (nrows, len(diffs), diffs[:6]))
print('INFO cast: differing columns: %s' % sorted({d[2] for d in diffs}))

# ------------------------------------------------------------------ P10 hurt
for T in (10, 33, 34, 35, 60, 100, 121, 122, 124, 140):
    name = 'hurt%d' % T
    efx = csv(name, 'effects.csv')
    kev = EXP['scenarios']['fire1_hurt%d_pf' % T]['events'].get(str(T), [])
    want = any(e.startswith('fx(') for e in kev)
    got = [e for e in efx if len(e) > 1]
    ok = (len(got) == 1 and got[0][0] == str(T) and got[0][1:5] == ['19', '2', '6553600', '6553600'] and got[0][5] == '1179650') if want else not got
    check('P10 hurt%d: effect (19, 2, 6553600, 6553600, 1179650) at tick %d' % (T, T) if want else 'P10 hurt%d: no effect' % T, ok, 'dll %s' % got)
    Dh = ents(rows(name))
    kh = EXP['scenarios']['fire1_hurt%d_pf' % T]
    kst = sorted((e['type'], e['ticks'][str(T + 3)][1]) for e in kh['ents'] if str(T + 3) in e['ticks'])
    dst = sorted((e[T + 3][0], e[T + 3][2]) for e in Dh if (T + 3) in e and e[T + 3][2] != 0)
    check('P10 hurt%d: (type, status) of the entities at T+3 = the key' % T, kst == dst, 'dll %s key %s' % (dst, kst))
    hr = rows(name)
    kit = kh['item'][str(T + 2)]
    dit = hr[T + 1]['hero'][4:8]
    check('P10 hurt%d: item (inuse, phase, counter, mp) at T+2 = the key (information on the freed item: inuse 0)' % T, [dit[0] != 0 and 47 or 0, dit[1], dit[2], dit[3]] == kit or dit[0] == 0 and kit[0] == 0 and dit[3] == kit[3], 'dll %s key %s' % (dit, kit))

# ------------------------------------------------------------------ P11 water
Dw = ents(rows('water'))
check('P11 water: Slope_18c stays 0 for every ball at every sample', all(e[t][16] == 0 for e in Dw[1:] for t in e), '')
same = True
for i in range(12):
    for t, e in Dw[i].items():
        o = D[i].get(t)
        if o is None or e[:20] != o[:20]:
            same = False
check('P11 water == cast on every entity row (slot .. hits)', same)

# ------------------------------------------------------------------ P12 cells
check('P12 cast: the cells of the key', [e[b][1] for e, b in zip(D, [34] + births)] == [k['slot'] for k in K])
Dt = ents(rows('table_one_free'))
kt = kents('fire1_table_one_free_pf')
check('P12 table_one_free: the column in the slot 63, no ball', len(Dt) == 1 and Dt[0][34][1] == 63 and len(kt) == 1 and kt[0]['slot'] == 63, 'dll %d ents, slot %s' % (len(Dt), Dt[0][34][1] if Dt else None))
rf = rows('table_full')
nf = open(os.path.join(OUT, 'table_full.notes.txt')).read()
kitem = EXP['scenarios']['fire1_table_full_pf']['item']
check('P12 table_full: nothing is born, the item is freed at the tick 34 (inuse 0), mp 3', len(ents(rf)) == 0 and 'column refused at tick 34' in nf and rf[33]['hero'][4] == 0 and rf[33]['hero'][7] == 3 and kitem['34'][0] == 0 and kitem['34'][3] == 3, nf.strip()[:80])

# ------------------------------------------------------------------ P13
dump = json.load(open(sys.argv[3])) if len(sys.argv) > 3 else {}
for nm, key, kdump in (('cast', 'fire1_cast_pf', 'cast'), ('cast_seedB', 'fire1_cast_pf_seedB', 'seedB'), ('tgt_one', 'fire1_tgt_one_pf', 'one'), ('tgt_three', 'fire1_tgt_three_pf', 'three'), ('tgt_two_wrong_order', 'fire1_tgt_two_wrong_order_pf', 'two_wrong_order'),
                       ('tgt_topslot', 'fire1_tgt_topslot_pf', 'topslot'), ('tgt_vanish75', 'fire1_tgt_vanish75_pf', 'vanish75'), ('table_one_free', 'fire1_table_one_free_pf', 'table_one_free')):
    if not dump:
        break
    q = csv(nm, 'queries.csv')
    got = [[int(a[0]), int(a[1]), int(a[2])] for a in q]
    want = [[t, caller, count] for t, caller, count, _ in dump[kdump]]
    check('P13 %s: the count of D-H4D2-5 at each call of the query = the stored count (%d calls)' % (nm, len(want)), got == want, 'first diff %s' % ([(g, w) for g, w in zip(got, want) if g != w][:3] if len(got) == len(want) else 'lengths %d %d' % (len(got), len(want))))
    if nm.startswith('tgt_'):
        Dq = ents(rows(nm))
        Kq = kents(key)
        bad = []
        for i in range(min(len(Dq), len(Kq))):
            for t, e in Dq[i].items():
                k = ktick(Kq[i], t)
                if k is None or not live(e):
                    continue
                kv = k[13] or 0
                if e[14] != kv:
                    bad.append((i, t, e[14], kv))
        check('P13 %s: the target slot kept (+0x278) at each tick = the key\'s b278_slot' % nm, not bad and len(Dq) == len(Kq), 'bad %s ents dll %d key %d' % (bad[:4], len(Dq), len(Kq)))

# ------------------------------------------------------------------ P14 relief
for nm, key in (('relief_hb1', 'fire1_relief_plateau_hb1_cols6_12_pf'), ('relief_hb2', 'fire1_relief_plateau_hb2_cols6_12_pf'), ('relief_hero', 'fire1_relief_hero_on_plateau_hb2_pf')):
    Dr = ents(rows(nm))
    Kr = kents(key)
    g = [first(e, lambda x: x[15] == 1) for e in Dr[1:]]
    kg = [first({int(k): v for k, v in e['ticks'].items()}, lambda x: x[14] == 1) for e in Kr[1:]]
    zg = [e[t][5] if t else None for e, t in zip(Dr[1:], g)]
    kzg = [e['ticks'][str(t)][4] if t else None for e, t in zip(Kr[1:], kg)]
    sd = [first(e, lambda x: x[2] == 3) for e in Dr[1:]]
    ksd = [first({int(k): v for k, v in e['ticks'].items()}, lambda x: x[1] == 3) for e in Kr[1:]]
    check('P14 %s: landing ticks and heights = the key' % nm, g == kg and zg == kzg, 'dll %s / %s key %s / %s' % (g, zg, kg, kzg))
    check('P14 %s: status 3 ticks = the key' % nm, sd == ksd, 'dll %s key %s' % (sd, ksd))
    nd = 0
    n = 0
    for i in range(len(Kr)):
        for t, e in Dr[i].items():
            k = ktick(Kr[i], t)
            if k is None or not live(e):
                continue
            n += 1
            if any(e[dc] != (k[kc2] or 0 if NAMES[kc2] == 'b278_slot' else k[kc2]) for dc, kc2 in MAP.items()):
                nd += 1
    print('INFO %s: %d live rows compared, %d differ' % (nm, n, nd))

# ------------------------------------------------------------------ P8 edge
for axis in ('x', 'y'):
    for v, want in EXP['edge'][axis].items():
        rs8 = rows('edge_%s%s' % (axis, v))
        hit = first({r['t']: r for r in rs8}, lambda r: r['targets'][0][0] == 25)
        check('P8 block at %s=%s: first tick of a column hit' % (axis, v), hit == want, 'dll %s key/critic %s' % (hit, want))

# ------------------------------------------------------------------ P15
print(open(os.path.join(OUT, 'p15.txt')).read().strip().splitlines()[0])
check('P15 GetDirectionToTarget = the binary on the 1024 raw-difference pairs (and the numeric turn model = the binary\'s write)', open(os.path.join(OUT, 'p15.txt')).read().startswith('turn-grid vectors 1024 direction mismatches 0 turn mismatches 0'))

# ------------------------------------------------------------------ information: three ticks per frame
for nm in ('cast_t2', 'cast_t3'):
    Dx = ents(rows(nm))
    bx = [first(e, lambda x: x[2] != 0) for e in Dx[1:]] if Dx else []
    print('INFO %s: entities %d, ball births %s, column end %s' % (nm, len(Dx), bx, first(Dx[0], lambda e: e[2] == 4) if Dx else None))

print('\nSUMMARY: %d clauses, %d FAIL %s' % (len(CLAUSES), len(FAILS), FAILS))
sys.exit(1 if FAILS else 0)
