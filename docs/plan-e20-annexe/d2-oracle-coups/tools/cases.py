"""cases.py - D2: the SIX hand-built validation cases of docs/plan-e20-heros.md section 1.2 (D2), plus extra cases, run on the real code.

The expected results are WRITTEN HERE BY HAND from the plan text (section 0.2.5 / 1.2); nothing is computed by the emulator or by model.py.
Every run prints: its inputs, the expected result, the result got from the real code of ALUN_CD.EXE, and the verdict.
Balance records come from alundra-project/Data/balance.json (encoded by balance_enc.py, equal to the disc blob); where the corpus has no record with
the needed property (a 0xC0 response exists nowhere in the corpus) the record is a copy of a real one with the stated byte changed.

usage: python -B cases.py [outdir]    -> outdir/cases.txt, outdir/cases.json ; exit code 0 = all six cases pass
"""
import copy
import json
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import hitworld as H
import balance_enc as B

ENTRIES = B.load_entries()
RECS = [(i, r) for i, e in enumerate(ENTRIES) for r in B.chain(e)]
SHEET_SCRATCH = 0x801E4000


def E(**kw):
    d = dict(cnt1c=0, status=2, hp=10, hpmax=10, ctl20=0, c28=0, flags=0, animflags=0, sheet=0, atk=0, c1cc=0, c1d0=0, box=0, mp=(0, 0, 0), tsize=(0, 0, 0),
             hb=(0, 0, 0), asize=(0, 0, 0), hitcounter=0, touching=0)
    d.update(kw)
    if d['box'] is True:
        d['box'] = H.BOX_REC
    return d


def rec_addr(r):
    return H.BAL_BASE + r['FileOffset']


def atk_addr(r, k):
    return H.BAL_BASE + r['FileOffset'] + 0xF + 2 * k


def find_attack(pred):
    for i, r in RECS:
        for k, a in enumerate(r['Attacks'] or []):
            if pred(a, r):
                return i, r, k, a
    raise LookupError('no attack in the corpus')


def find_sheet(pred):
    for i, r in RECS:
        if pred(r):
            return i, r
    raise LookupError('no sheet in the corpus')


class Scratch:
    """sheets that are copies of corpus records with stated changes, encoded with balance_enc.encode_record, placed in RAM after the world restore"""

    def __init__(self, world):
        self.w = world
        self.next = SHEET_SCRATCH

    def sheet(self, base, responses=None, max_hp=None, attacks=None):
        r = copy.deepcopy(base)
        if responses is not None:
            r['DamageResponses'] = list(responses)
        if max_hp is not None:
            r['MaxHp'] = max_hp
        if attacks is not None:
            r['Attacks'] = [dict(AttackType=a & 0xF, UsesEquipmentPower=bool(a & 0x80), AttackAttribute=a, Power=p) for a, p in attacks]
            r['AttackCount'] = len(attacks)
            r['RecordSize'] = 15 + 2 * len(attacks)
        b = B.encode_record(r)
        a = self.next
        self.w.m.poke_bytes(a, b)
        self.next += (len(b) + 0xF) & ~0xF
        return a, r


def run_case(W, build, hero_anim=0, seed=0x12345678):
    """restore, build (a callable (W, S) -> (specs, opts)), run the real passes, return everything observable"""
    W.restore()
    S = Scratch(W)
    specs, opts = build(W, S)
    hero_anim = opts.get('hero_anim', hero_anim)
    W.blank_slots(set(specs))
    for slot, sp in specs.items():
        W.put(slot, **sp)
    if 0 not in specs:
        W.put(0, anim_target=hero_anim)
    else:
        W.put(0, anim_target=specs[0].get('anim_target', hero_anim))
    W.set_equipment(opts.get('equip', ()))
    if 'hero_sheet' in opts:
        W.load_hero_sheet(opts['hero_sheet'])
    W.m.poke(H.RNG, seed)
    W.m.tick_no = 1
    W.pre_passes()
    active = W.active_list()
    W.hit_pass()
    slots = sorted(specs)
    after_hit = W.snapshot(slots)
    latched = W.m.peek(H.G_ANIM)
    if opts.get('hero_anim_after') is not None:
        W.m.poke(H.ent(0) + 0x88, opts['hero_anim_after'])
    dead = W.damage_all(slots)
    if opts.get('counters'):
        W.counters()                       # the real counters pass 0x80038998 of the NEXT tick; 'after' is then the state after it
    after = W.snapshot(slots)
    res = dict(active=active, after_hit=after_hit, after=after, dead=dead, effects=W.effects(), latched=latched,
               rng=W.m.peek(H.RNG), uninit=sorted((pc, a, n, c) for (pc, a, n), c in W.m.uninit_reads.items()), specs=specs, opts=opts,
               journal=list(W.m.journal))
    return res


def fmt_spec(slot, sp):
    keys = ['status', 'flags', 'hp', 'box', 'atk', 'sheet', 'mp', 'tsize', 'hb', 'asize', 'c1d0', 'c1cc', 'animflags', 'ctl20']
    out = []
    for k in keys:
        v = sp[k]
        if isinstance(v, tuple):
            out.append('%s=(%s)' % (k, ','.join('%d' % x if abs(x) < 4096 else '0x%X' % (x & 0xFFFFFFFF) for x in v)))
        elif k in ('atk', 'sheet', 'box'):
            out.append('%s=%s' % (k, '0x%08X' % v if v else '0'))
        elif k == 'flags':
            out.append('flags=0x%X' % v)
        else:
            out.append('%s=%s' % (k, v))
    return 'slot %d: %s' % (slot, ' '.join(out))


def pick(res, slot, key, phase='after'):
    return res[phase][slot][key]


def check(res, expected):
    """expected: {slot: {key: value}} on the state after the hit pass AND the D-handler damage ('after'); key 'touching_slot', 'c1d0', 'hitcounter', 'hp'
    plus the special keys '_effects' (list of (kind, id)) and '_dead' ({slot: flag})"""
    ok = True
    diffs = []
    for slot, kv in expected.items():
        if isinstance(slot, str):
            continue
        for k, v in kv.items():
            g = res['after'][slot][k]
            if g != v:
                ok = False
                diffs.append('slot %d %s: expected %r got %r' % (slot, k, v, g))
    if '_effects' in expected:
        g = [(k, a[1]) for k, a in res['effects']]
        if g != expected['_effects']:
            ok = False
            diffs.append('effects: expected %r got %r' % (expected['_effects'], g))
    if '_centre' in expected:
        sp = [tuple(a[3:6]) for k, a in res['effects'] if k == 'effect_3BDD8']
        want = [tuple(x & 0xFFFFFFFF for x in expected['_centre'])] * 3
        if sp != want:
            ok = False
            diffs.append('spark centres: expected %r got %r' % ([tuple(hex(x) for x in w) for w in want], [tuple(hex(x) for x in g) for g in sp]))
    if '_latched' in expected and res['latched'] != expected['_latched']:
        ok = False
        diffs.append('latched [0x80146DF8]: expected %r got %r' % (expected['_latched'], res['latched']))
    if '_dead' in expected:
        if res['dead'] != expected['_dead']:
            ok = False
            diffs.append('dead flags: expected %r got %r' % (expected['_dead'], res['dead']))
    if res['uninit']:
        ok = False
        diffs.append('reads of unset bytes: %r' % (res['uninit'][:5],))
    return ok, diffs


def got_summary(res, slots):
    return {s: {k: res['after'][s][k] for k in ('touching_slot', 'c1d0', 'hitcounter', 'hp')} for s in slots}


# ---------------------------------------------------------------------------------------------------------------------------------------------
# shared corpus picks
# ---------------------------------------------------------------------------------------------------------------------------------------------
SWORD = find_attack(lambda a, r: a['AttackAttribute'] == 1 and a['Power'] == 3)               # attribute 1, power 3, no equipment flag
NEUTRAL = find_sheet(lambda r: r['DamageResponses'][0] == 16 and r['MaxHp'] >= 12)             # response 16 for attribute 1 (100 %)
IMMUNE = find_sheet(lambda r: r['DamageResponses'][0] == 0x80)                                 # response 0x80 for attribute 1
KILL = find_sheet(lambda r: r['DamageResponses'][0] == 0x40)                                   # response 0x40 for attribute 1
LOW = find_sheet(lambda r: r['DamageResponses'][0] == 8)                                       # response 8 for attribute 1 (50 %)
PW1 = find_attack(lambda a, r: a['AttackAttribute'] == 1 and a['Power'] == 1)                  # attribute 1, power 1

BASE_MP = (0x00640000 + 0x1234, 0x00500000 + 0x0F0F, 0x00300000 + 0x0777)    # a target position with a non zero fractional part


def attacker(attack_addr, hb, asize=None, flags=H.HITS_A, **kw):
    """an attacker: has a box (+0x1D4), an attack (+0x1C8), flags with a Hits* bit; its HitBox sits at hb, size 4 px per axis unless given"""
    if asize is None:
        asize = (H.size(4),) * 3
    return E(flags=flags, box=True, atk=attack_addr, hb=hb, asize=asize, **kw)


def overlapping(mp=BASE_MP):
    """an attacker box of 4 px sitting exactly on the target position, target size 8 px: overlaps on all three axes"""
    return mp


# ---------------------------------------------------------------------------------------------------------------------------------------------
# the six validation cases (plan 1.2 D2)
# ---------------------------------------------------------------------------------------------------------------------------------------------

def case1_masks(W):
    """CASE 1: reordered class mask.  an attacker with ONLY HitsClassA hits a ClassA target and not a ClassB target; an attacker with ONLY HitsClassB
    hits a ClassB target and not a ClassA target."""
    runs = []
    for name, aflags, expect_hit_flag, expect_miss_flag in (('HitsClassA only', H.HITS_A, H.CLASS_A, H.CLASS_B), ('HitsClassB only', H.HITS_B, H.CLASS_B, H.CLASS_A)):
        def build(W, S, aflags=aflags, hitf=expect_hit_flag, missf=expect_miss_flag):
            specs = {
                1: attacker(atk_addr(SWORD[1], SWORD[2]), BASE_MP, flags=aflags),
                2: E(flags=hitf, sheet=rec_addr(NEUTRAL[1]), mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10),
                3: E(flags=missf, sheet=rec_addr(NEUTRAL[1]), mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10),
            }
            return specs, {}
        exp = {1: dict(hitcounter=1), 2: dict(touching_slot=1, c1d0=0x19, hitcounter=0), 3: dict(touching_slot=None, c1d0=0, hitcounter=0)}
        runs.append((name + ' (slot 2 flags 0x%X must be hit, slot 3 flags 0x%X must not)' % (expect_hit_flag, expect_miss_flag), build, exp))
    return runs


def case2_overlap(W):
    """CASE 2: overlap in raw 16.16.  target of n px: d = (n<<16)-1 hits, d = n<<16 does not.  attacker box of m px: d = -((m<<16)-1) hits, d = -(m<<16)
    does not.  Each axis separately (the two others at d = 0), three (n, m) pairs."""
    runs = []
    for axis, an in enumerate('XYZ'):
        for n, m in ((1, 1), (5, 4), (16, 12)):
            for label, d, hit in (('d=(n<<16)-1', (n << 16) - 1, True), ('d=n<<16', n << 16, False),
                                  ('d=-((m<<16)-1)', -((m << 16) - 1), True), ('d=-(m<<16)', -(m << 16), False)):
                def build(W, S, axis=axis, n=n, m=m, d=d):
                    hb = list(BASE_MP)
                    hb[axis] += d
                    specs = {
                        1: attacker(atk_addr(SWORD[1], SWORD[2]), tuple(hb), asize=(H.size(m),) * 3),
                        2: E(flags=H.CLASS_A, sheet=rec_addr(NEUTRAL[1]), mp=BASE_MP, tsize=(H.size(n),) * 3, hp=10),
                    }
                    return specs, {}
                exp = ({1: dict(hitcounter=1), 2: dict(touching_slot=1, c1d0=0x19)} if hit else {1: dict(hitcounter=0), 2: dict(touching_slot=None, c1d0=0)})
                runs.append(('axis %s, target %d px, attacker box %d px, %s = %d (0x%X) -> %s' % (an, n, m, label, d, d & 0xFFFFFFFF, 'HIT' if hit else 'MISS'), build, exp))
    return runs


def case3_responses(W):
    """CASE 3: response 0x80 -> no TouchingEntity, but +0x1D0 = 0x19 on the target and HitCounter + 1 on the attacker.  Response 0xC0 -> TouchingEntity set,
    then HP unchanged by ComputeNewHp."""
    runs = []

    def b80(W, S):
        return {1: attacker(atk_addr(SWORD[1], SWORD[2]), BASE_MP),
                2: E(flags=H.CLASS_A, sheet=rec_addr(IMMUNE[1]), mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10)}, {}
    runs.append(('response 0x80 (corpus record #%d @%d, attribute 1)' % (IMMUNE[0], IMMUNE[1]['FileOffset']), b80,
                 {1: dict(hitcounter=1), 2: dict(touching_slot=None, c1d0=0x19, hp=10)}))

    def bc0(W, S):
        base = NEUTRAL[1]
        resp = list(base['DamageResponses'])
        resp[0] = 0xC0
        a, _ = S.sheet(base, responses=resp)
        return {1: attacker(atk_addr(SWORD[1], SWORD[2]), BASE_MP),
                2: E(flags=H.CLASS_A, sheet=a, mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10)}, {}
    runs.append(('response 0xC0 (no corpus record has one: copy of corpus record #%d with response[attr 1] = 0xC0)' % NEUTRAL[0], bc0,
                 {1: dict(hitcounter=1), 2: dict(touching_slot=1, c1d0=0x19, hp=10), '_dead': {2: 0}}))
    return runs


def case4_min_damage(W):
    """CASE 4: when power * response >> 4 is 0 the target still loses 1 HP."""
    runs = []

    def b(W, S):
        return {1: attacker(atk_addr(PW1[1], PW1[2]), BASE_MP),
                2: E(flags=H.CLASS_A, sheet=rec_addr(LOW[1]), mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10)}, {}
    runs.append(('attack power %d attr %d (corpus record #%d), target response 8 (corpus record #%d): 1*8>>4 = 0; HP 10 -> 9' % (PW1[3]['Power'], PW1[3]['AttackAttribute'], PW1[0], LOW[0]), b,
                 {2: dict(touching_slot=1, hp=9), '_dead': {2: 0}}))

    def b2(W, S):
        return {1: attacker(atk_addr(SWORD[1], SWORD[2]), BASE_MP),
                2: E(flags=H.CLASS_A, sheet=rec_addr(LOW[1]), mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10)}, {}
    runs.append(('control: power %d, response 8: 3*8>>4 = 1; HP 10 -> 9' % SWORD[3]['Power'], b2, {2: dict(hp=9)}))

    def b3(W, S):
        return {1: attacker(atk_addr(PW1[1], PW1[2]), BASE_MP),
                2: E(flags=H.CLASS_A, sheet=rec_addr(LOW[1]), mp=BASE_MP, tsize=(H.size(8),) * 3, hp=1)}, {}
    runs.append(('minimum at HP 1: 1*8>>4 = 0 -> 1 damage, HP 1 -> 0, dead', b3, {2: dict(hp=0), '_dead': {2: 1}}))
    return runs


def case5_first_attacker(W):
    """CASE 5: two attackers on the same target in the same tick: only the first of the list touches it; the second one's HitCounter stays 0."""
    runs = []
    for label, a_slot, b_slot, t_slot in (('attackers 1,2 target 3', 1, 2, 3), ('attackers 3,1 target 2 (target between them)', 3, 1, 2), ('attackers 5,4 target 9', 5, 4, 9)):
        def build(W, S, a_slot=a_slot, b_slot=b_slot, t_slot=t_slot):
            return {a_slot: attacker(atk_addr(SWORD[1], SWORD[2]), BASE_MP),
                    b_slot: attacker(atk_addr(PW1[1], PW1[2]), BASE_MP),
                    t_slot: E(flags=H.CLASS_A, sheet=rec_addr(NEUTRAL[1]), mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10)}, {}
        first = min(a_slot, b_slot)
        second = max(a_slot, b_slot)
        exp = {first: dict(hitcounter=1), second: dict(hitcounter=0), t_slot: dict(touching_slot=first, c1d0=0x19)}
        runs.append((label + ': first in the list = slot %d' % first, build, exp))
    return runs


def case6_multi_target(W):
    """CASE 6: one attacker on two targets touches both, HitCounter 2 (a third, out of range, is a control)."""
    def build(W, S):
        far = (BASE_MP[0] + (40 << 16), BASE_MP[1], BASE_MP[2])
        return {1: attacker(atk_addr(SWORD[1], SWORD[2]), BASE_MP),
                2: E(flags=H.CLASS_A, sheet=rec_addr(NEUTRAL[1]), mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10),
                3: E(flags=H.CLASS_A, sheet=rec_addr(NEUTRAL[1]), mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10),
                4: E(flags=H.CLASS_A, sheet=rec_addr(NEUTRAL[1]), mp=far, tsize=(H.size(8),) * 3, hp=10)}, {}
    return [('one attacker (slot 1), targets slots 2 and 3 in range, slot 4 out of range', build,
             {1: dict(hitcounter=2), 2: dict(touching_slot=1, c1d0=0x19), 3: dict(touching_slot=1, c1d0=0x19), 4: dict(touching_slot=None, c1d0=0)})]


SIX = [('1 reordered class mask', case1_masks), ('2 overlap in raw 16.16', case2_overlap), ('3 response 0x80 / 0xC0', case3_responses),
       ('4 minimum damage', case4_min_damage), ('5 first attacker', case5_first_attacker), ('6 several targets', case6_multi_target)]


# ---------------------------------------------------------------------------------------------------------------------------------------------
# extra cases (not among the six; facts of 0.2.5 or of the disassembly, each with a hand-written expectation)
# ---------------------------------------------------------------------------------------------------------------------------------------------

def x_skip_conditions(W):
    runs = []
    base_t = dict(flags=H.CLASS_A, sheet=rec_addr(NEUTRAL[1]), mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10)
    for label, change, hit in (('target +0x1D0 != 0', dict(c1d0=5), False), ('target +0x1CC != 0', dict(c1cc=7), False),
                               ('target AnimFlags & 0x40', dict(animflags=0x40), False), ('target +0x20 != 0 (not in the list)', dict(ctl20=1), False),
                               ('target status 1 (not in the list)', dict(status=1), False), ('target status 5 (not in the list)', dict(status=5), False),
                               ('target status 3 (in the list)', dict(status=3), True), ('control: target with none of the above', dict(), True)):
        def build(W, S, change=change):
            t = dict(base_t)
            t.update(change)
            return {1: attacker(atk_addr(SWORD[1], SWORD[2]), BASE_MP), 2: E(**t)}, {}
        if hit:
            exp = {1: dict(hitcounter=1), 2: dict(touching_slot=1)}
        else:
            exp = {1: dict(hitcounter=0), 2: dict(touching_slot=None)}
            if 'c1d0' in change:
                exp[2]['c1d0'] = 5
        runs.append((label, build, exp))
    for label, ch in (('attacker without box (+0x1D4 = 0)', dict(box=0)), ('attacker without attack (+0x1C8 = 0)', dict(atk=0)),
                      ('attacker with attribute byte 0', dict(atk='zero')), ('attacker without flags', dict(flags=0)),
                      ('attacker +0x20 != 0 (not in the list)', dict(ctl20=1)), ('attacker status 5 (not in the list)', dict(status=5))):
        def build(W, S, ch=ch):
            a = attacker(atk_addr(SWORD[1], SWORD[2]), BASE_MP)
            ch = dict(ch)
            if ch.get('atk') == 'zero':
                ch['atk'] = W.put_attack(0, 0, 9)
            a.update(ch)
            return {1: a, 2: E(**base_t)}, {}
        runs.append((label, build, {1: dict(hitcounter=0), 2: dict(touching_slot=None, c1d0=0)}))
    return runs


def x_class_c(W):
    runs = []
    for label, af, tf, hit in (('HitsClassC vs ClassC', H.HITS_C, H.CLASS_C, True), ('HitsClassC vs ClassA', H.HITS_C, H.CLASS_A, False),
                               ('HitsClassA vs ClassC', H.HITS_A, H.CLASS_C, False), ('HitsClassA|HitsClassB|HitsClassC vs ClassB', H.HITS_A | H.HITS_B | H.HITS_C, H.CLASS_B, True),
                               ('attacker with the target class bits only (ClassA|ClassB|ClassC) vs ClassA', H.CLASS_A | H.CLASS_B | H.CLASS_C, H.CLASS_A, False)):
        def build(W, S, af=af, tf=tf):
            return {1: attacker(atk_addr(SWORD[1], SWORD[2]), BASE_MP, flags=af),
                    2: E(flags=tf, sheet=rec_addr(NEUTRAL[1]), mp=BASE_MP, tsize=(H.size(8),) * 3)}, {}
        runs.append((label, build, {2: dict(touching_slot=1 if hit else None)}))
    return runs


def x_effects(W):
    runs = []
    for attr, eff in ((6, 4), (10, 4), (7, 5), (9, 5), (1, None), (11, None)):
        def build(W, S, attr=attr):
            a = W.put_attack(0, attr, 3)
            sh, _ = S.sheet(NEUTRAL[1], responses=[16] * 11)
            return {1: attacker(a, BASE_MP), 2: E(flags=H.CLASS_A, sheet=sh, mp=BASE_MP, tsize=(H.size(8),) * 3)}, {}
        exp = {2: dict(touching_slot=1, c1d0=0x19), '_effects': ([('effect_3BE74', eff)] if eff else []) + [('effect_3BDD8', 9)] * 3}
        runs.append(('attack attribute %d (synthetic attack record, power 3): effect %s attached, then 3 sparks' % (attr, eff), build, exp))

    def build_imm(W, S):
        base = NEUTRAL[1]
        resp = list(base['DamageResponses'])
        resp[5] = 0x80                                            # attribute 6
        a, _ = S.sheet(base, responses=resp)
        return {1: attacker(W.put_attack(0, 6, 3), BASE_MP), 2: E(flags=H.CLASS_A, sheet=a, mp=BASE_MP, tsize=(H.size(8),) * 3)}, {}
    runs.append(('attribute 6 on an immune (0x80) target: NO effect attached, 3 sparks', build_imm,
                 {2: dict(touching_slot=None, c1d0=0x19), '_effects': [('effect_3BDD8', 9)] * 3}))
    return runs


def x_zero_size(W):
    """size 0 (n = 0): target stored 0 (0x80039CC8), attacker stored -1 (0x80038C80-88 has no zero test)"""
    runs = []
    for label, tsz, asz, d, hit in (('target size stored 0, d = 0', 0, H.size(4), 0, True), ('target size stored 0, d = 1', 0, H.size(4), 1, False),
                                    ('attacker size stored -1 (n = 0), d = 0', H.size(8), (0 << 16) - 1, 0, True),
                                    ('attacker size stored -1 (n = 0), d = -1', H.size(8), (0 << 16) - 1, -1, False)):
        def build(W, S, tsz=tsz, asz=asz, d=d):
            hb = (BASE_MP[0] + d, BASE_MP[1], BASE_MP[2])
            return {1: attacker(atk_addr(SWORD[1], SWORD[2]), hb, asize=(asz,) * 3),
                    2: E(flags=H.CLASS_A, sheet=rec_addr(NEUTRAL[1]), mp=BASE_MP, tsize=(tsz,) * 3)}, {}
        runs.append((label, build, {2: dict(touching_slot=1 if hit else None)}))
    return runs


def x_centre(W):
    """spark centre = ((max of lower bounds) + (min of upper bounds)) / 2 truncated toward zero, per axis; upper bound = position + (n << 16) - 1.
    positive case: attacker at (10, 30, 7) px sizes (4, 6, 8) px; target at (12, 28, 5) px sizes 8 px -> X [12 px, 14 px - 1], Y [30 px, 36 px - 1], Z [7 px, 13 px - 1]"""
    runs = []

    def build(W, S):
        hb = (px(10), px(30), px(7))
        mp = (px(12), px(28), px(5))
        return {1: attacker(atk_addr(SWORD[1], SWORD[2]), hb, asize=(H.size(4), H.size(6), H.size(8))),
                2: E(flags=H.CLASS_A, sheet=rec_addr(NEUTRAL[1]), mp=mp, tsize=(H.size(8), H.size(8), H.size(8)))}, {}
    runs.append(('centre, positive coordinates', build, {2: dict(touching_slot=1),
                 '_centre': ((px(12) + px(14) - 1) // 2, (px(30) + px(36) - 1) // 2, (px(7) + px(13) - 1) // 2)}))

    def build2(W, S):
        hb = (-px(14), px(30), px(7))
        mp = (-px(16), px(28), px(5))
        return {1: attacker(atk_addr(SWORD[1], SWORD[2]), hb, asize=(H.size(4), H.size(6), H.size(8))),
                2: E(flags=H.CLASS_A, sheet=rec_addr(NEUTRAL[1]), mp=mp, tsize=(H.size(8), H.size(8), H.size(8)))}, {}
    # X: lower max(-14 px, -16 px) = -14 px; upper min(-14 px + 4 px - 1, -16 px + 8 px - 1) = -10 px - 1; sum = -24 px - 1 (odd) -> truncated toward zero
    runs.append(('centre, negative odd sum (truncation toward zero, not floor)', build2,
                 {2: dict(touching_slot=1), '_centre': (-((px(24) + 1) // 2), (px(30) + px(36) - 1) // 2, (px(7) + px(13) - 1) // 2)}))
    return runs


def px(n):
    return n << 16


def x_damage_equipment(W):
    """attribute byte with bit 0x80: power + the Attacks[anim+1].Power of each of the three equipment sheets (anim latched at the hit pass)"""
    runs = []

    def build(W, S, anim=2, later=9):
        # equipment sheets: 1) armour with attacks[0..5], 2) none, 3) a sheet with 2 attacks (anim+1 >= count -> falls back to Attacks[0])
        s1, _ = S.sheet(NEUTRAL[1], attacks=[(0, 1), (0, 2), (0, 4), (0, 8), (0, 16), (0, 32)])
        s3, _ = S.sheet(NEUTRAL[1], attacks=[(0, 5), (0, 6)])
        a = W.put_attack(0, 0x81, 10)
        specs = {0: E(flags=0, hp=40, anim_target=anim), 1: attacker(a, BASE_MP),
                 2: E(flags=H.CLASS_A, sheet=rec_addr(NEUTRAL[1]), mp=BASE_MP, tsize=(H.size(8),) * 3, hp=40)}
        return specs, dict(equip=(s1, 0, s3), hero_anim_after=later)
    # hero anim latched = 2: sheet1 Attacks[3] power 8 (index anim+1 = 3 < 6); sheet3: anim+1 = 3 >= 2 -> Attacks[0] power 5: (10 + 8 + 5) * 16 >> 4 = 23 -> 40-23 = 17
    runs.append(('equipment sum, hero anim latched 2 (changed to 9 after the hit pass): (10 + 8 + 5) * 16 >> 4 = 23; HP 40 -> 17', build,
                 {2: dict(touching_slot=1, hp=17)}))
    return runs


def x_latch(W):
    def build(W, S):
        return {0: E(flags=0, hp=5, anim_target=7), 1: attacker(W.put_attack(0, 1, 1), BASE_MP),
                2: E(flags=H.CLASS_A, sheet=rec_addr(NEUTRAL[1]), mp=BASE_MP, tsize=(H.size(8),) * 3)}, dict(hero_anim_after=9)
    return [('hero TargetAnimationId 7 at the hit pass, 9 later: [0x80146DF8] = 7', build, {'_latched': 7})]


def x_hero_sheet(W):
    """the hit pass reads the response in the hero's +0x1C4; ComputeNewHp reads the sheet 0x80127008 for the entity at 0x80127D30"""
    runs = []

    def build_a(W, S):
        resp = [16] * 11
        a_hit, _ = S.sheet(NEUTRAL[1], responses=resp)                       # +0x1C4: neutral -> touched
        hs, rec = S.sheet(NEUTRAL[1], responses=[0x80] + [16] * 10)           # 0x80127008 content: immune to attribute 1 -> ComputeNewHp changes nothing
        raw = B.encode_record(rec)
        return {0: E(flags=H.CLASS_A, sheet=a_hit, mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10, anim_target=0),
                1: attacker(atk_addr(SWORD[1], SWORD[2]), BASE_MP)}, dict(hero_sheet=raw)
    runs.append(('hero: +0x1C4 sheet neutral (touched), 0x80127008 sheet immune to attribute 1: touched, HP unchanged', build_a,
                 {0: dict(touching_slot=1, c1d0=0x19, hp=10), 1: dict(hitcounter=1)}))

    def build_b(W, S):
        a_imm, _ = S.sheet(NEUTRAL[1], responses=[0x80] + [16] * 10)          # +0x1C4: immune -> not touched (the hit pass reads +0x1C4)
        hs, rec = S.sheet(NEUTRAL[1], responses=[16] * 11)
        return {0: E(flags=H.CLASS_A, sheet=a_imm, mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10, anim_target=0),
                1: attacker(atk_addr(SWORD[1], SWORD[2]), BASE_MP)}, dict(hero_sheet=B.encode_record(rec))
    runs.append(('hero: +0x1C4 sheet immune (the hit pass reads it), 0x80127008 sheet neutral: NOT touched, HP unchanged', build_b,
                 {0: dict(touching_slot=None, c1d0=0x19, hp=10), 1: dict(hitcounter=1)}))

    def build_c(W, S):
        a_n, _ = S.sheet(NEUTRAL[1], responses=[16] * 11)
        hs, rec = S.sheet(NEUTRAL[1], responses=[8] + [16] * 10)              # 0x80127008: 50 % for attribute 1
        return {0: E(flags=H.CLASS_A, sheet=a_n, mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10, anim_target=0),
                1: attacker(atk_addr(SWORD[1], SWORD[2]), BASE_MP)}, dict(hero_sheet=B.encode_record(rec))
    runs.append(('hero: damage from the 0x80127008 sheet (response 8): power 3 * 8 >> 4 = 1, HP 10 -> 9 (with the +0x1C4 sheet it would be 3)', build_c,
                 {0: dict(touching_slot=1, hp=9)}))
    return runs


def x_kill(W):
    def build(W, S):
        return {1: attacker(atk_addr(PW1[1], PW1[2]), BASE_MP),
                2: E(flags=H.CLASS_A, sheet=rec_addr(KILL[1]), mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10)}, {}
    return [('response 0x40 (corpus record #%d): HP goes to 0 whatever the damage' % KILL[0], build, {2: dict(touching_slot=1, hp=0), '_dead': {2: 1}})]


def x_latch_empty(W):
    def build(W, S):
        return {}, dict(hero_anim=7)
    return [('no entity at all (empty list): the delay slot of 0x80039344 still latches the hero animation 7 into [0x80146DF8]', build, {'_latched': 7})]


def x_counters(W):
    def build(W, S):
        return {1: attacker(atk_addr(SWORD[1], SWORD[2]), BASE_MP),
                2: E(flags=H.CLASS_A, sheet=rec_addr(NEUTRAL[1]), mp=BASE_MP, tsize=(H.size(8),) * 3, hp=10)}, dict(counters=True)
    return [('after the hit pass and the damage, the counters pass (next tick): TouchingEntity and HitCounter cleared, +0x1D0 0x19 -> 0x18', build,
             {1: dict(hitcounter=0), 2: dict(touching_slot=None, c1d0=0x18, hp=7)})]


EXTRA = [('x1 skip conditions', x_skip_conditions), ('x2 class C and class combinations', x_class_c), ('x3 effects attached by attribute', x_effects),
         ('x4 zero sizes', x_zero_size), ('x5 spark centre', x_centre), ('x6 equipment sum', x_damage_equipment), ('x7 hero sheet', x_hero_sheet), ('x8 response 0x40', x_kill), ('x9 latch', x_latch), ('x10 latch with an empty list', x_latch_empty), ('x11 counters pass', x_counters)]


def run_group(W, group, out):
    """runs every (label, build, expected) of a group; writes the details to out; returns (n_ok, n_total)"""
    ok_n = 0
    results = []
    for label, build, exp in group:
        res = run_case(W, build)
        ok, diffs = check(res, exp)
        ok_n += ok
        slots = sorted(res['specs'])
        out.write('  - %s\n' % label)
        out.write('      inputs:\n')
        for s in slots:
            out.write('        %s\n' % fmt_spec(s, res['specs'][s]))
        out.write('        hero TargetAnimationId (slot 0 +0x88) = %s; active list (slots) = %s\n' % (res['opts'].get('hero_anim', res['specs'].get(0, {}).get('anim_target', 0)), res['active']))
        out.write('      expected: %s\n' % json.dumps({str(k): v for k, v in exp.items()}))
        out.write('      got:      %s   effects=%s dead=%s\n' % (json.dumps({str(k): v for k, v in got_summary(res, slots).items()}),
                                                            [(k, a[1]) for k, a in res['effects']], res['dead']))
        out.write('      verdict:  %s%s\n' % ('PASS' if ok else 'FAIL', '' if ok else '   ' + '; '.join(diffs)))
        results.append(dict(label=label, ok=ok, diffs=diffs))
    return ok_n, len(group), results


def main(outdir, W=None):
    os.makedirs(outdir, exist_ok=True)
    W = W or H.World()
    summary = []
    allok = True
    with open(os.path.join(outdir, 'cases.txt'), 'w', encoding='utf-8') as out:
        out.write('# D2 validation cases on the REAL hit pass 0x80039300 / ComputeNewHp 0x8003A374 / ResolveBalanceTarget 0x8004464C of ALUN_CD.EXE (France)\n')
        out.write('# expected results are hand-written from docs/plan-e20-heros.md 0.2.5 / 1.2; got = read from the emulated machine after: slot pass, list pass, hit pass, ComputeNewHp for each TouchingEntity\n')
        out.write('# slots: 1.. (slot 0 = hero slot, empty unless stated). lengths in raw 16.16 units unless stated. rng seed 0x12345678\n')
        out.write('# keys: touching_slot = slot of the +0x224 TouchingEntity (None = 0); c1d0 = +0x1D0; hitcounter = +0x220; hp = +0x14\n\n')
        for title, fn in SIX:
            group = fn(W)
            out.write('=== CASE %s  (%d runs)\n' % (title, len(group)))
            n_ok, n, res = run_group(W, group, out)
            verdict = 'PASS' if n_ok == n else 'FAIL'
            allok &= n_ok == n
            out.write('=== CASE %s: %s (%d/%d runs)\n\n' % (title, verdict, n_ok, n))
            summary.append(dict(case=title, verdict=verdict, ok=n_ok, total=n, runs=res))
        out.write('############ extra cases (not part of the six)\n\n')
        for title, fn in EXTRA:
            group = fn(W)
            out.write('=== EXTRA %s  (%d runs)\n' % (title, len(group)))
            n_ok, n, res = run_group(W, group, out)
            out.write('=== EXTRA %s: %s (%d/%d runs)\n\n' % (title, 'PASS' if n_ok == n else 'FAIL', n_ok, n))
            summary.append(dict(case=title, verdict='PASS' if n_ok == n else 'FAIL', ok=n_ok, total=n, runs=res, extra=True))
    json.dump(summary, open(os.path.join(outdir, 'cases.json'), 'w'), indent=1)
    for s in summary:
        print('%-40s %s (%d/%d)' % (s['case'], s['verdict'], s['ok'], s['total']))
    return 0 if all(s['verdict'] == 'PASS' for s in summary if not s.get('extra')) else 1


if __name__ == '__main__':
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out')))
