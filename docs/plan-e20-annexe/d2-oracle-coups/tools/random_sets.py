"""random_sets.py - D2: synthetic entity sets drawn at random (noted seed) and run through the REAL hit pass; the result is cross-checked against the
independent model (model.py) and the coverage of the three functions is reported.

Sets: 4-12 entities in slots 0-23 (slot 0 = the hero, whose damage sheet is 0x80127008), random status / flags / class bits / boxes / attacks, random
balance level D (real 0x80044520) and sprite index (real 0x80044550) for the target sheets, random equipment sheets, random raw 16.16 positions
with boundary-aligned attacker boxes (d = (n<<16)-1, n<<16, -((m<<16)-1), -(m<<16) on one axis).  Attacks come from alundra-project/Data/balance.json
(85 %), a synthetic random byte pair (5 %), or none (10 %); target sheets from the real record selection (80 %) or a copy of a corpus record with random
responses including 0xC0 (20 %).

usage: python -B random_sets.py [outdir] [nsets] [seed]
writes outdir/random.txt (summary, coverage, first disagreements) ; exit code 0 = no disagreement between the real code and the model
"""
import os
import random
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import hitworld as H
import balance_enc as B
import cases as C
import model as M

SEED = 20261009
ATTACKS_NZ = [(r, k) for i, r in C.RECS for k, a in enumerate(r['Attacks'] or []) if a['AttackAttribute']]
ATTACKS_EQ = [(r, k) for i, r in C.RECS for k, a in enumerate(r['Attacks'] or []) if a['AttackAttribute'] & 0x80]
ATTACKS_ALL = [(r, k) for i, r in C.RECS for k, a in enumerate(r['Attacks'] or [])]
RECS_ATK = [r for i, r in C.RECS if r['AttackCount']]


def gen_build(rng, stats):
    def build(W, S):
        D = rng.randint(0, 11)
        W.set_level(D)
        n = rng.randint(4, 12)
        slots = sorted(rng.sample(range(0, 24), n))
        base = [H.px(100) + rng.randint(0, 0xFFFF), H.px(80) + rng.randint(0, 0xFFFF), H.px(40) + rng.randint(0, 0xFFFF)]
        specs = {}
        next_atk = [0]
        for i in slots:
            st = rng.choices([2, 3, 1, 5], [60, 15, 5, 5])[0]
            has_box = rng.random() < 0.55
            f = 0
            for bit, p in ((H.CLASS_A, .5), (H.CLASS_B, .4), (H.CLASS_C, .2), (H.HITS_A, .35), (H.HITS_B, .3), (H.HITS_C, .15), (0x80, .5), (0x100, .5), (0x10, .1), (0x2000, .2)):
                if rng.random() < p:
                    f |= bit
            tn = [rng.randint(0, 16) for _ in range(3)]
            an = [rng.randint(0, 16) for _ in range(3)]
            mp = [base[k] + rng.randint(-H.px(12), H.px(12)) for k in range(3)]
            hb = [base[k] + rng.randint(-H.px(14), H.px(14)) for k in range(3)]
            r = rng.random()
            if not has_box or r < 0.10:
                atk = 0
            elif r < 0.15:
                a = W.put_attack(next_atk[0], rng.randint(0, 255), rng.randint(0, 255))
                next_atk[0] += 1
                atk = a
            else:
                rec, k = rng.choice(ATTACKS_EQ if rng.random() < 0.25 else (ATTACKS_NZ if rng.random() < 0.9 else ATTACKS_ALL))
                atk = C.atk_addr(rec, k)
            if rng.random() < 0.8 or i == 0:
                sheet = W.record_addr(rng.randint(0, 511))
            else:
                resp = [rng.choice([0, 1, 4, 8, 12, 15, 16, 16, 16, 0x40, 0x80, 0xC0, 0x10, rng.randint(0, 255)]) for _ in range(11)]
                sheet, _ = S.sheet(rng.choice(RECS_ATK), responses=resp)
            specs[i] = C.E(status=st, hp=rng.randint(1, 60), flags=f, animflags=rng.choice([0, 0, 0, 0x40, 0x8, 0x80, 0x41]), sheet=sheet, atk=atk,
                           c1cc=0 if rng.random() < 0.92 else rng.randint(1, 120), c1d0=0 if rng.random() < 0.92 else rng.randint(1, 25),
                           ctl20=0 if rng.random() < 0.94 else 1, box=has_box, mp=tuple(mp), tsize=tuple(H.size(x) for x in tn),
                           hb=tuple(hb), asize=tuple(H.size(x) for x in an))
        # boundary alignment: put some attacker boxes exactly on a boundary of a target
        ids = list(specs)
        for a in ids:
            if specs[a]['box'] and rng.random() < 0.35:
                t = rng.choice(ids)
                if t == a:
                    continue
                k = rng.randrange(3)
                tsz, asz = specs[t]['tsize'][k], specs[a]['asize'][k]
                dd = rng.choice([tsz, tsz + 1, -asz, -(asz + 1), tsz - 1, 0, -1, 1])
                hb = [specs[t]['mp'][j] + rng.choice([0, 0, rng.randint(-H.px(2), H.px(2))]) for j in range(3)]
                hb[k] = specs[t]['mp'][k] + dd
                specs[a]['hb'] = tuple(hb)
        hero_anim = rng.choice([rng.randint(0, 0x4F), rng.randint(0, 6)])
        if 0 in specs:
            specs[0]['anim_target'] = hero_anim
        # equipment sheets
        equip = tuple(0 if rng.random() < 0.25 else C.rec_addr(rng.choice(RECS_ATK if rng.random() < 0.8 else [r for i, r in C.RECS])) for _ in range(3))
        opts = dict(equip=equip, hero_anim=hero_anim, D=D)
        if 0 in specs:
            hs = B.encode_record(rng.choice(RECS_ATK) if rng.random() < 0.5 else rng.choice([r for i, r in C.RECS]))
            opts['hero_sheet'] = hs
        return specs, opts
    return build


def classify(res, st, stats, ram):
    """counts, from the model's view of each touch/hit, which branches of the damage arithmetic the sets exercised"""
    mem = M.Mem(ram)
    specs = res['specs']
    for i, v in st.items():
        if v['hit'] == 0:
            continue
    for i, v in st.items():
        if v['touching'] is None:
            continue
        a = specs[v['touching']]
        attr = mem.u8(a['atk'])
        sheet = H.HERO_SHEET if i == 0 else specs[i]['sheet']
        r = mem.u8(sheet + (attr & 0xF) + 2)
        top = r & 0xC0
        key = {0x00: 'touch_resp_00', 0x40: 'touch_resp_40', 0xC0: 'touch_resp_C0'}.get(top, 'touch_resp_other')
        stats[key] = stats.get(key, 0) + 1
        if top == 0:
            if attr & 0x80:
                stats['touch_with_equipment_bit'] = stats.get('touch_with_equipment_bit', 0) + 1
            power = mem.u8(a['atk'] + 1)
            if (power * r) >> 4 == 0 and not attr & 0x80:
                stats['min_damage_applied'] = stats.get('min_damage_applied', 0) + 1
    for i, sp in specs.items():
        if sp['box'] and sp['atk'] and mem.u8(sp['atk']) == 0:
            stats['attacker_attr_zero'] = stats.get('attacker_attr_zero', 0) + 1
    for k, v in st.items():
        if v['hit'] and False:
            pass
    immune = sum(v['hit'] for v in st.values()) - sum(1 for v in st.values() if v['touching'] is not None)
    stats['immune_80'] = stats.get('immune_80', 0) + immune
    if any(v['hit'] for v in st.values()):
        stats['sets_with_hit'] = stats.get('sets_with_hit', 0) + 1


def compare(res, W):
    specs = res['specs']
    opts = res['opts']
    ram = bytes(W.m.ram)
    equip = list(opts['equip']) + [0] * (3 - len(opts['equip']))
    st, ev, rng_final, dead = M.run(specs, ram, res['latched'] if False else opts['hero_anim'], 0x12345678, equip, H.HERO_SHEET, hero_slot=0)
    diffs = []
    for i in sorted(specs):
        g = res['after'][i]
        e = st[i]
        gt = g['touching_slot']
        if gt != e['touching']:
            diffs.append('slot %d touching: real %r model %r' % (i, gt, e['touching']))
        if g['c1d0'] != e['c1d0']:
            diffs.append('slot %d c1d0: real %r model %r' % (i, g['c1d0'], e['c1d0']))
        if g['hitcounter'] != e['hit']:
            diffs.append('slot %d hitcounter: real %r model %r' % (i, g['hitcounter'], e['hp'] if False else e['hit']))
        if g['hp'] != e['hp']:
            diffs.append('slot %d hp: real %r model %r' % (i, g['hp'], e['hp']))
    if res['dead'] != dead:
        diffs.append('dead flags: real %r model %r' % (res['dead'], dead))
    real_ev = []
    for k, a in res['effects']:
        if k == 'effect_3BE74':
            real_ev.append((k, a[1], (a[3] - H.ENT0) // H.STRIDE))
        else:
            real_ev.append((k, a[1], tuple(M.s32(x) for x in a[3:6])))
    if real_ev != ev:
        diffs.append('effects: real %r model %r' % (real_ev[:6], ev[:6]))
    if res['rng'] != rng_final:
        diffs.append('rng: real %08X model %08X' % (res['rng'], rng_final))
    if res['latched'] != opts['hero_anim']:
        diffs.append('latched anim: real %r expected %r' % (res['latched'], opts['hero_anim']))
    if res['uninit']:
        diffs.append('reads of unset bytes: %r' % (res['uninit'][:4],))
    return diffs, st, ev


def main(outdir, nsets=3000, seed=SEED, W=None):
    os.makedirs(outdir, exist_ok=True)
    W_owned = W is None
    W = W or H.World()
    rng = random.Random(seed)
    stats = dict(sets=0, hits=0, touched=0, kills=0, multi_target=0, hero_touched=0, effect4=0, effect5=0, dmg_calls=0)
    bad = []
    for s in range(nsets):
        build = gen_build(rng, stats)
        res = C.run_case(W, build)
        diffs, st, ev = compare(res, W)
        stats['sets'] += 1
        if diffs:
            bad.append((s, diffs, res['specs']))
        classify(res, st, stats, ram=bytes(W.m.ram))
        stats['effect4'] += sum(1 for e in ev if e[0] == 'effect_3BE74' and e[1] == 4)
        stats['effect5'] += sum(1 for e in ev if e[0] == 'effect_3BE74' and e[1] == 5)
        hits = sum(v['hit'] for v in st.values())
        stats['hits'] += hits
        stats['touched'] += sum(1 for v in st.values() if v['touching'] is not None)
        stats['dmg_calls'] += len(res['dead'])
        stats['kills'] += sum(res['dead'].values())
        stats['multi_target'] += sum(1 for v in st.values() if v['hit'] >= 2)
        if 0 in st and st[0]['touching'] is not None:
            stats['hero_touched'] += 1
    cov = coverage(W) if W_owned else ''
    with open(os.path.join(outdir, 'random.txt'), 'w', encoding='utf-8') as f:
        f.write('# D2 random sets: seed %d, %d sets, each run on the REAL passes and compared with model.py\n' % (seed, nsets))
        f.write('# stats: %s\n' % stats)
        f.write('# disagreements between the real code and the model: %d\n' % len(bad))
        for s, diffs, specs in bad[:20]:
            f.write('set %d: %s\n' % (s, '; '.join(diffs)))
            for i in sorted(specs):
                f.write('    %s\n' % C.fmt_spec(i, specs[i]))
        f.write(cov)
    print('random sets: %d, disagreements: %d, stats %s' % (nsets, len(bad), stats))
    print(cov)
    return 0 if not bad else 1


RANGES = [('UpdateBalanceRecords 0x80039300-0x80039780', 0x80039300, 0x80039780), ('spark routine 0x80039188-0x80039300', 0x80039188, 0x80039300),
          ('ComputeNewHp 0x8003A374-0x8003A59C', 0x8003A374, 0x8003A59C), ('ResolveBalanceTarget 0x8004464C-0x80044820', 0x8004464C, 0x80044820)]


def coverage(W):
    out = ['# coverage of the executed instructions over all runs (every distinct PC executed; the debug printf branches need [0x800DC058] < 0 and are not reachable here)']
    for name, a, b in RANGES:
        pcs = [p for p in range(a, b, 4)]
        hit = [p for p in pcs if p in W.m.cov]
        miss = [p for p in pcs if p not in W.m.cov]
        out.append('# %s: %d of %d instruction words executed; not executed: %s' % (name, len(hit), len(pcs), ' '.join('%08X' % p for p in miss)))
    return '\n'.join(out) + '\n'


if __name__ == '__main__':
    outdir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out')
    n = int(sys.argv[2]) if len(sys.argv) > 2 else 3000
    seed = int(sys.argv[3]) if len(sys.argv) > 3 else SEED
    sys.exit(main(outdir, n, seed))
