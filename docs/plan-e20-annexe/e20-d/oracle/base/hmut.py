"""hmut.py - E20.c2-0: mutation tests ("teeth") of the oracle.  One-word patches of the emulated binary (RAM only; the old word is checked by kworld.setup_inv), applied to every machine a probe builds.
The EXPECTED CHANGE of each mutation is written HERE, before any mutated run: a predicate on (baseline probe, mutated probe).  A mutation is CAUGHT when the predicate holds AND
the independent model (hmodel.py) no longer agrees with the mutated run.  A control must leave the probe unchanged AND the model must still agree.
Mutations of c2-oracle.md section 5 that bear on the kept scenarios (2, 3, 5, 6, 7, 8, 11, 12, 13 and 14 of that list, plus 1, 4 and 9 below); item 10 (the `& 0x7C` mask of the regeneration gate) is
not retained: it is observable only through control flags 0x08 / 0x40 / 0x48 / 0x28 (the tick gate stops MovePlayer, hence the END, before the regeneration gate reads them) or through scenario F3 (removed by
check.md section c).  The tick-dispatcher control is not a patch of the binary: the same probe with the base world's ungated dispatcher.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
import kworld as K
import hworld as H
import hscen as HS
import hmodel as HM

# ---- probes: name -> list of (run name, ticks) ------------------------------------------------------------------------------------------------
PROBES = {
    'A1': [('A1', 140)],
    'A1+C1': [('A1', 140), ('C1-bean', 6)],
    'A4': [('A4-%s' % b, 3) for b in ('E', 'W', 'S', 'N', 'SE', 'SW', 'NE', 'NW', 'ENE', 'WSW', 'SSE', 'NNW')],
    'A6': [('A6-back120', 130), ('A6-back121', 130)],
    'B1-boots3': [('B1-080-b1c', 4), ('B1-180-b1c', 4)],
    'B1-cost': [('B1-080-kit', 4), ('B1-100-kit', 4), ('B1-180-kit', 4)],
    'B2': [('B2-hp2-cost3', 4)],
    'D1': [('D1-%s' % v, 4) for v in ('20', '24', '30', '10', '04', 'A0')],
    'F1-short': [('F1', 8)],
    'F1-long': [('F1', 210)],
    'F2': [('F2', 305)],
    'E3': [('E3-12-4', 3)],
    'D1-gate': [('D1-48', 8), ('D1-08', 8), ('D1-40', 8), ('D1-28', 8)],
}


def run_probe(key, patches=(), gate=True):
    K.PATCHES[:] = list(patches)
    try:
        out = {}
        for nm, n in PROBES[key]:
            sp = dict(HS.find(nm))
            sp['n'] = n
            if not gate:
                sp['gate'] = False
            res = H.run(sp)
            res['model'] = HM.check(res)
            res['model_ok'] = HM.ok(res['model'])
            out[nm] = res
        return out
    finally:
        K.PATCHES[:] = []


# ---- readers for the predicates -------------------------------------------------------------------------------------------------------------
def hp(r):
    return [x['hero']['hp'] for x in r['rows']]


def anim(r):
    return [x['hero']['anim_target'] for x in r['rows']]


def c1cc(r):
    return [x['hero']['c1cc'] for x in r['rows']]


def vis(r):
    return [x['hero']['visible'] for x in r['rows']]


def dirs(r):
    return [x['hero']['dir_target'] for x in r['rows']]


def first_touch(r):
    """first tick after the hurt (tick 2) at which the hit pass touches the hero again"""
    t = [x['tick'] for x in r['rows'] if x['hero']['touching_slot'] is not None and x['tick'] > 2]
    return t[0] if t else None


def hidden(r, a, b):
    return sum(1 for x in r['rows'][a - 1:b] if not x['hero']['visible'])


def at(lst, t):
    return lst[t - 1]


def stats_hp(r):
    return [x['g']['stats_hp'] for x in r['rows']]


def same_rows(b, m):
    import json
    return all(json.dumps(b[k]['rows'], sort_keys=True, default=str) == json.dumps(m[k]['rows'], sort_keys=True, default=str) for k in b)


def mutations():
    M = []

    def add(**kw):
        M.append(kw)
    add(id='M1', name='toucher branch forced off (0x80031A88 beqz s0 -> b: the damage entry never takes the toucher branch)', patch=[(0x80031A88, 0x1200000C, 0x1000000C)], probe='A1', scen='A1',
        expect='A1: no hurt at all (no hazard cell): HP stays 10 for the 140 ticks and the animation is never 0x31 (baseline: hurt at tick 18, HP 9, animation 0x31)',
        pred=lambda b, m: at(hp(b['A1']), 18) == 9 and all(v == 10 for v in hp(m['A1'])) and at(anim(b['A1']), 18) == 0x31 and 0x31 not in anim(m['A1']))
    add(id='M2', name='direction operands: the X difference reversed (0x80031AA8 subu a0,v1,a0 -> subu a0,a0,v1)', patch=[(0x80031AA8, 0x00642023, 0x00832023)], probe='A4', scen='A4',
        expect='A4: the knock-back direction at the hurt tick (tick 2) is mirrored about the vertical axis for every bearing: d -> (-d) & 31 (so E and W swap, N and S stay)',
        pred=lambda b, m: all(at(dirs(m[k]), 2) == ((-at(dirs(b[k]), 2)) & 31) for k in b) and sum(1 for k in b if at(dirs(m[k]), 2) != at(dirs(b[k]), 2)) >= 8)
    add(id='M3', name='boots test slti 3 -> 4 (0x80031AD8): boots level 3 no longer protects', patch=[(0x80031AD8, 0x28420003, 0x28420004)], probe='B1-boots3', scen='B1',
        expect='B1: with boots level 3 the hazard now hurts: cost 1 -> HP 9 at tick 2, cost 3 -> HP 7 at tick 2 (baseline: HP 10, no hurt)',
        pred=lambda b, m: all(v == 10 for v in hp(b['B1-080-b1c'])) and all(v == 10 for v in hp(b['B1-180-b1c'])) and at(hp(m['B1-080-b1c']), 2) == 9 and at(hp(m['B1-180-b1c']), 2) == 7)
    add(id='M4', name='cost index shift srl 7 -> srl 8 (0x80031AF0)', patch=[(0x80031AF0, 0x000219C2, 0x00021A02)], probe='B1-cost', scen='B1',
        expect='B1: cell word 0x80 gives index 0 -> no hurt (HP 10), 0x100 gives index 1 -> cost 1 (HP 9, baseline 8), 0x180 gives index 1 -> cost 1 (HP 9, baseline 7)',
        pred=lambda b, m: (at(hp(b['B1-080-kit']), 2), at(hp(b['B1-100-kit']), 2), at(hp(b['B1-180-kit']), 2)) == (9, 8, 7) and (at(hp(m['B1-080-kit']), 2), at(hp(m['B1-100-kit']), 2), at(hp(m['B1-180-kit']), 2)) == (10, 9, 9))
    add(id='M5', name='invulnerability value 0x78 -> 0x77 (0x80031C90)', patch=[(0x80031C90, 0x24030078, 0x24030077)], probe='A1', scen='A1 + A6',
        expect='A1: +0x1CC at the hurt tick (18) is 118 (baseline 119), the hero is NOT hidden on the hurt tick (baseline hidden), 29 hidden ticks in 18..137 (baseline 30)',
        pred=lambda b, m: (at(c1cc(b['A1']), 18), at(c1cc(m['A1']), 18)) == (119, 118) and (at(vis(b['A1']), 18), at(vis(m['A1']), 18)) == (0, 1) and (hidden(b['A1'], 18, 137), hidden(m['A1'], 18, 137)) == (30, 29))
    add(id='M5b', name='invulnerability value 0x78 -> 0x77, the window boundary (A6)', patch=[(0x80031C90, 0x24030078, 0x24030077)], probe='A6', scen='A6',
        expect='A6: contact restored at hurt + 118 (tick 120): first touch after the hurt at tick 121 in the baseline, at 120 with the shorter window; contact restored at hurt + 119 (tick 121): touch at 121 in both',
        pred=lambda b, m: (first_touch(b['A6-back120']), first_touch(m['A6-back120'])) == (121, 120) and (first_touch(b['A6-back121']), first_touch(m['A6-back121'])) == (121, 121))
    add(id='M6', name='0x31 / 0x3A selector inverted (0x80031C78 bne s2,4 -> beq)', patch=[(0x80031C78, 0x16420002, 0x12420002)], probe='A1+C1', scen='A1 + C1',
        expect='A1: the hurt on land is 0x3A (baseline 0x31) at tick 18; C1-bean: the hurt in deep water is 0x31 (baseline 0x3A) at tick 2',
        pred=lambda b, m: (at(anim(b['A1']), 18), at(anim(m['A1']), 18)) == (0x31, 0x3A) and (at(anim(b['C1-bean']), 2), at(anim(m['C1-bean']), 2)) == (0x3A, 0x31))
    add(id='M7', name='blink mask andi 3 -> 7 (0x800385F0)', patch=[(0x800385F0, 0x30420003, 0x30420007)], probe='A1', scen='A1',
        expect='A1: the hero is visible on the hurt tick (+0x1CC 119 & 7 = 7, baseline hidden) and hidden on 15 ticks of 18..137 instead of 30',
        pred=lambda b, m: (at(vis(b['A1']), 18), at(vis(m['A1']), 18)) == (0, 1) and (hidden(b['A1'], 18, 137), hidden(m['A1'], 18, 137)) == (30, 15))
    add(id='M8', name='regeneration comparison inverted (0x80030CD4 slt v0,v0,v1 -> slt v0,v1,v0)', patch=[(0x80030CD4, 0x0043102A, 0x0062102A)], probe='F1-short', scen='F1',
        expect='F1: HP +1 on every call of the END regeneration until HpMax; the setup warm-up is such a call, so the start state (tick 0) already reads 6, then HP 7, 8, 9, 10 at ticks 1..4 (baseline 5 for 200 ticks)',
        pred=lambda b, m: hp(b['F1'])[:5] == [5] * 5 and m['F1']['pre']['hero']['hp'] == 6 and hp(m['F1'])[:5] == [7, 8, 9, 10, 10])
    add(id='M8b', name='regeneration counter step 2 (0x80030CC4 addiu v1,v1,1 -> 2)', patch=[(0x80030CC4, 0x24630001, 0x24630002)], probe='F1-long', scen='F1',
        expect='F1: the period halves: HP 6 at tick 101 and 7 at tick 202 (baseline 6 at 201)',
        pred=lambda b, m: (at(hp(b['F1']), 100), at(hp(b['F1']), 101), at(hp(b['F1']), 201)) == (5, 5, 6) and (at(hp(m['F1']), 100), at(hp(m['F1']), 101), at(hp(m['F1']), 202)) == (5, 6, 7))
    add(id='M9', name='regeneration pad gate removed (0x80030C5C bnez v0 -> nop)', patch=[(0x80030C5C, 0x14400034, 0x00000000)], probe='F2', scen='F2',
        expect='F2: Up held at tick 100 no longer zeroes the counters: HP 6 at tick 201 (baseline: 5 at 201, 6 at 301)',
        pred=lambda b, m: (at(hp(b['F2']), 201), at(hp(b['F2']), 301)) == (5, 6) and at(hp(m['F2']), 201) == 6)
    add(id='M11', name='whole-word compare with 0x20 removed (0x80031C0C bne v1,v0 -> nop)', patch=[(0x80031C0C, 0x14620341, 0x00000000)], probe='D1', scen='D1',
        expect='D1: flags 0x24, 0x30, 0x10, 0x04 and 0xA0 now reach the damage entry: HP 9 at tick 2 (baseline 10); flags 0x20 unchanged (HP 9 in both)',
        pred=lambda b, m: all(at(hp(b['D1-%s' % v]), 2) == 10 and at(hp(m['D1-%s' % v]), 2) == 9 for v in ('24', '30', '10', '04', 'A0')) and at(hp(b['D1-20']), 2) == 9 == at(hp(m['D1-20']), 2))
    add(id='M12', name='SetPlayerHp clamp direction (0x8004DD44 slt v0,v0,a0 -> slt v0,a0,v0)', patch=[(0x8004DD44, 0x0044102A, 0x0082102A)], probe='E3', scen='E3',
        expect='E3: stats HpMax 12, HP 4: the clamp now stores HpMax when HP < HpMax: the hero is created with HP 12 (baseline 4) and the stats read 12',
        pred=lambda b, m: (b['E3-12-4']['pre']['hero']['hp'], m['E3-12-4']['pre']['hero']['hp']) == (4, 12) and (b['E3-12-4']['pre']['g']['stats_hp'], m['E3-12-4']['pre']['g']['stats_hp']) == (4, 12))
    add(id='M13', name='hazard HP floor inverted (0x80031B14 slt v0,a0,v1 -> slt v0,v1,a0)', patch=[(0x80031B14, 0x0083102A, 0x0064102A)], probe='B2', scen='B2',
        expect='B2: HP 2 with cost 3: the entity HP is -1 after the hurt (baseline 0, the stats clamp it to 0)',
        pred=lambda b, m: at(hp(b['B2-hp2-cost3']), 2) == 0 and at(hp(m['B2-hp2-cost3']), 2) == -1 and at(stats_hp(m['B2-hp2-cost3']), 2) == 0)
    add(id='C1', control=True, name='CONTROL: debug-print read of ComputeNewHp patched (0x8003A3E0 lw -> nop; reached only when [0x800DC058] < 0)', patch=[(0x8003A3E0, 0x8C420004, 0x00000000)], probe='A1', scen='A1',
        expect='nothing changes: every row of A1 (140 ticks) equal', pred=lambda b, m: same_rows(b, m))
    add(id='C2', control=True, name='CONTROL: the tick dispatcher without the [0x800DC4B8] & 0x48 gate (world.tick behaviour): the 0x48 line of D1 MUST change', patch=[], gate=False, probe='D1-gate', scen='D1',
        expect='D1 with flags 0x48, 0x08, 0x40, 0x28: with the gate the hero\'s +0x1C stays at its start value and nothing moves; without it +0x1C increments every tick and the touch is registered (TouchingEntity at tick 1) '
               '-> the line changes and the model\'s gate rule disagrees (this one is the tick-dispatcher check, not a binary mutation)',
        pred=lambda b, m: all(len(set(x['hero']['frame_counter_1C'] for x in b[k]['rows'])) == 1 and len(set(x['hero']['frame_counter_1C'] for x in m[k]['rows'])) > 1 for k in b), mutation_catch=True)
    return M


def run():
    lines = ['# one-word patches of the emulated binary (RAM only); the expected change is written in hmut.py before any run; CAUGHT = predicate holds and the independent model disagrees']
    ok = True
    base = {}
    results = []
    for mu in mutations():
        key = mu['probe']
        if key not in base:
            base[key] = run_probe(key)
            if not all(r['model_ok'] for r in base[key].values()):
                ok = False
                lines.append('BASELINE probe %s: the model disagrees with the baseline: %s' % (key, [k for k, r in base[key].items() if not r['model_ok']]))
        mut = run_probe(key, mu['patch'], gate=mu.get('gate', True))
        try:
            changed = bool(mu['pred'](base[key], mut))
        except Exception as e:                    # noqa
            changed = False
            lines.append('   (predicate error: %r)' % (e,))
        model_ok = all(r['model_ok'] for r in mut.values())
        if mu.get('control') and not mu.get('mutation_catch'):
            good = changed and model_ok
            tag = 'OK    ' if good else 'FAILED'
        else:
            good = changed and not model_ok
            tag = 'CAUGHT' if good else 'MISSED'
        ok &= good
        lines.append('%s %s [%s, %s]\n         expected: %s\n         predicate %s; independent model %s with the change (%s)' % (
            tag, mu['name'], mu['scen'], key, mu['expect'], 'holds' if changed else 'DOES NOT HOLD', 'still agrees' if model_ok else 'disagrees',
            '; '.join('%s: %s' % (k, ','.join(n.split(':')[0][:28] for n, o, d in r['model'] if not o) or 'agrees') for k, r in mut.items())[:200]))
        results.append((mu['id'], tag.strip(), good))
    lines.append('MUTATIONS: %d, %s' % (len(results), 'all as expected' if ok else 'SOME NOT AS EXPECTED'))
    return lines, ok, results


if __name__ == '__main__':
    ls, ok, _r = run()
    print('\n'.join(ls))
    sys.exit(0 if ok else 1)
