"""checks.py - E20.c1: the checks of the oracle itself.

  diff2      every named scenario run on emu.py (the oracle) and on mips.py (a second interpreter, via interp2.py): rows, the slots (hero + targets, 0x294 bytes each, after every tick),
             the journal and the ComputeNewHp experiments must be identical.
  mutations  one-word patches of the emulated binary (RAM only) with the expected change WRITTEN BEFORE the run (the predicate is in this file), plus controls that must not change.
  hazards    load-delay check over every executed instruction (both interpreters ignore the MIPS-I load delay slot).
"""
import json
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import c1world as C
import scen as S
import world as W
import interp2
import lib


def dumps(x):
    return json.dumps(x, sort_keys=True, default=str)


# ---- two interpreters ---------------------------------------------------------------------------------------------------------
def diff2(specs):
    lines = []
    ok = True
    cov = set()
    for sp in specs:
        r1 = C.run(sp, capture=True)
        r2 = C.run(sp, ticker=interp2.tick2, capture=True)
        same_rows = dumps(r1['rows']) == dumps(r2['rows']) and dumps(r1['pre']) == dumps(r2['pre'])
        same_slots = r1['slotbytes'] == r2['slotbytes']
        same_exp = dumps(r1['experiments']) == dumps(r2['experiments'])
        same_j = dumps([(j[0], j[1], C.trim_args(j[1], j[2])) for j in r1['machine'].journal]) == dumps([(j[0], j[1], C.trim_args(j[1], j[2])) for j in r2['machine'].journal])
        same_rng = r1['machine'].peek(C.RNG) == r2['machine'].peek(C.RNG)
        this = same_rows and same_slots and same_exp and same_j and same_rng
        ok &= this
        lines.append('%-8s %3d ticks: rows %s, slots (hero + %d targets, %d bytes each, every tick) %s, journal %s, ComputeNewHp experiments (%d) %s, RNG word %s -> %s' % (
            sp['name'], sp['n'], 'equal' if same_rows else 'DIFFERENT', len(r1['meta']['target_slots']), C.STRIDE, 'equal' if same_slots else 'DIFFERENT',
            'equal' if same_j else 'DIFFERENT', len(r1['experiments']), 'equal' if same_exp else 'DIFFERENT', 'equal' if same_rng else 'DIFFERENT', 'OK' if this else 'FAIL'))
        cov |= r1['machine'].cov | r2['machine'].cov
    return lines, ok, cov


# ---- mutations --------------------------------------------------------------------------------------------------------------
def patcher(*patches):
    def f(m):
        for addr, old, new in patches:
            cur = m.peek(addr)
            if cur != old:
                raise RuntimeError('word at %08X is %08X, expected %08X' % (addr, cur, old))
            m.poke(addr, new)
    return f


def skipper(*passes):
    def tick(m, pad, hold):
        W.tick(m, pad, hold, skip=passes)
    return tick


def spec_named(name, reach):
    for sp in S.scenarios(reach):
        if sp['name'] == name:
            return dict(sp)
    raise KeyError(name)


def hits(res, victim=0):
    return [r['tick'] for r in res['rows'] if (r['hero'] if victim == 'hero' else r['targets'][victim])['c1d0'] == 0x19]


def n_sparks(res, tick):
    return len(S.sparks(res['rows'][tick - 1]))


def strip_rng(rows):
    return dumps([{k: v for k, v in r.items() if k != 'rng'} for r in rows])


def mutations(reach):
    """list of (name, address words, scenario, expectation text, predicate(base, mutated) -> bool).  The expectation text and the predicate are fixed before any mutated run."""
    A = lambda base, mut: None
    M = []
    M.append(dict(name='class mask: HitsClassA bit dropped (0x800393A0 andi 1 -> 0)', patch=[(0x800393A0, 0x30420001, 0x30420000)], scen='H1',
                  expect='the hero\'s swing no longer reaches the ClassA Beannoid: no hit on the target at any tick',
                  pred=lambda b, m: hits(b) == [5] and hits(m) == []))
    M.append(dict(name='class mask: HitsClassB bit dropped (0x800393A8 andi 8 -> 0)', patch=[(0x800393A8, 0x30630008, 0x30630000)], scen='H3',
                  expect='the Beannoid no longer reaches the hero: no hit on the hero, hero HP stays 10 for all 45 ticks',
                  pred=lambda b, m: hits(b, 'hero') == [17] and hits(m, 'hero') == [] and all(r['hero']['hp'] == 10 for r in m['rows'])))
    M.append(dict(name='target +0x1D0 guard removed (0x80039424 bnez -> nop)', patch=[(0x80039424, 0x144000C9, 0x00000000)], scen='H4a',
                  expect='the target is hit on every overlapping tick: ticks 6..11 of swing 1 (blocked in the baseline) and tick 27 of swing 2 (baseline: first hit of swing 2 at 30)',
                  pred=lambda b, m: set(range(5, 12)) <= set(hits(m)) and 27 in hits(m) and 27 not in hits(b) and 6 not in hits(b)))
    M.append(dict(name='AnimFlags 0x40 skip removed (0x80039448 bnez -> nop)', patch=[(0x80039448, 0x144000C0, 0x00000000)], scen='H4b',
                  expect='the target in animation 6 (flag 0x40) is hit at tick 5 like the H1 target (baseline H4b: no hit in 40 ticks)',
                  pred=lambda b, m: hits(b) == [] and 5 in hits(m)))
    M.append(dict(name='hit window value 0x19 -> 0x10 (0x8003966C)', patch=[(0x8003966C, 0x24020019, 0x24020010)], scen='H1',
                  expect='+0x1D0 is 16 on the hit tick (baseline 25), so it decrements to 0 nine ticks earlier',
                  pred=lambda b, m: b['rows'][4]['targets'][0]['c1d0'] == 25 and m['rows'][4]['targets'][0]['c1d0'] == 16 and b['rows'][-1]['targets'][0]['c1d0'] == 0 and m['rows'][26]['targets'][0]['c1d0'] == 0 and b['rows'][26]['targets'][0]['c1d0'] > 0))
    M.append(dict(name='spark loop count 3 -> 2 (0x800391A8)', patch=[(0x800391A8, 0x24130003, 0x24130002)], scen='H1',
                  expect='2 effect-9 calls on the hit tick (baseline 3)',
                  pred=lambda b, m: n_sparks(b, 5) == 3 and n_sparks(m, 5) == 2))
    M.append(dict(name='equipment power term dropped (0x8004473C addu t0,t0,v0 -> nop)', patch=[(0x8004473C, 0x01024021, 0x00000000)], scen='H1',
                  expect='ComputeNewHp with the filled equipment records deals 1 (baseline 2): the equipment power no longer counts',
                  pred=lambda b, m: b['experiments'][0]['variants']['live']['damage'] == 2 and m['experiments'][0]['variants']['live']['damage'] == 1 and m['experiments'][0]['variants']['empty']['damage'] == 1))
    M.append(dict(name='counters pass skipped', skip=(0x80038998,), scen='H1',
                  expect='HitCounter of the hero and TouchingEntity of the target are NOT cleared at tick 6 and +0x1D0 never decrements',
                  pred=lambda b, m: b['rows'][5]['hero']['hit_counter'] == 0 and m['rows'][5]['hero']['hit_counter'] == 1 and m['rows'][5]['targets'][0]['touching_slot'] == 0
                  and m['rows'][-1]['targets'][0]['c1d0'] == 25))
    M.append(dict(name='hit pass skipped', skip=(0x80039300,), scen='H1',
                  expect='no hit at any tick, HitCounter stays 0', pred=lambda b, m: hits(m) == [] and all(r['hero']['hit_counter'] == 0 for r in m['rows'])))
    M.append(dict(name='hero swing box one pixel wider (data byte of the frame-2 box, +1 width)', data_box_width=1, scen='H1-edge',
                  expect='the box of frame 2 (ticks 5-6) is 1 px wider: the baseline run at the reach limit still hits, and the run at reach + 1 (H2 position) now hits',
                  pred=None))
    M.append(dict(name='immunity test removed (0x800395EC beq on (response & 0xC0) == 0x80 -> nop)', patch=[(0x800395EC, 0x1043001F, 0x00000000)], scen='H6',
                  expect='the 0x80 response no longer skips the TouchingEntity store: the target\'s TouchingEntity is the hero (slot 0) at the hit tick (baseline: none), +0x1D0 and the hero HitCounter as before',
                  pred=lambda b, m: hits(b) == hits(m) and len(hits(b)) == 1 and b['rows'][hits(b)[0] - 1]['targets'][0]['touching_slot'] is None
                  and m['rows'][hits(m)[0] - 1]['targets'][0]['touching_slot'] == 0 and b['rows'][hits(b)[0] - 1]['hero']['hit_counter'] == 1 == m['rows'][hits(m)[0] - 1]['hero']['hit_counter']))
    M.append(dict(name='target +0x1D0 guard removed (0x80039424 bnez -> nop)', patch=[(0x80039424, 0x144000C9, 0x00000000)], scen='H7',
                  expect='the second attacker (slot 2) is no longer blocked by the hero\'s hit on the same tick: on tick 1 its HitCounter is 1 (baseline 0) and the target\'s TouchingEntity is slot 2 (baseline slot 0)',
                  pred=lambda b, m: b['rows'][0]['targets'][1]['hit_counter'] == 0 and m['rows'][0]['targets'][1]['hit_counter'] == 1
                  and b['rows'][0]['targets'][0]['touching_slot'] == 0 and m['rows'][0]['targets'][0]['touching_slot'] == 2))
    M.append(dict(name='CONTROL: other random seed', seed=0xDEADBEEF, scen='H1', control=True,
                  expect='nothing but the RNG word changes (the spark arguments do not depend on the random draws)',
                  pred=lambda b, m: strip_rng(b['rows']) == strip_rng(m['rows']) and b['rows'][4]['rng'] != m['rows'][4]['rng']))
    M.append(dict(name='CONTROL: effects pass 0x80038E84 skipped (it is a stub)', skip=(0x80038E84,), scen='H1', control=True,
                  expect='rows unchanged (the stub only journals)', pred=lambda b, m: dumps([{k: v for k, v in r.items() if k != 'journal' and k != 'journal_raw'} for r in b['rows']]) == dumps([{k: v for k, v in r.items() if k != 'journal' and k != 'journal_raw'} for r in m['rows']])))
    return M


def run_mutations(reach):
    lines = []
    ok = True
    cache = {}
    cov = set()
    for mu in mutations(reach):
        base_spec = spec_named(mu['scen'], reach)
        key = mu['scen']
        if key not in cache:
            cache[key] = C.run(base_spec)
            cov |= cache[key]['machine'].cov
        base = cache[key]
        sp = dict(base_spec)
        ticker = None
        if mu.get('patch'):
            sp['mutate'] = patcher(*mu['patch'])
        if mu.get('skip'):
            ticker = skipper(*mu['skip'])
        if mu.get('seed') is not None:
            sp['seed'] = mu['seed']
        if mu.get('data_box_width'):
            # the pointer of the frame-2 box is read from the baseline run (tick 5 of H1); the patched byte is its width (byte +3 of the record)
            ptr = cache.get('_H1ptr')
            if ptr is None:
                h1 = cache.get('H1') or C.run(spec_named('H1', reach))
                cache['H1'] = h1
                ptr = h1['rows'][4]['hero']['frame_box_1D4']
                cache['_H1ptr'] = ptr
            sp['mutate'] = (lambda p: (lambda m: m.poke8(p + 3, m.peek(p + 3) + 1)))(ptr)
        mutated = C.run(sp, ticker=ticker)
        cov |= mutated['machine'].cov
        if mu.get('data_box_width'):
            h2 = C.run(dict(spec_named('H2', reach), mutate=sp['mutate']))
            cov |= h2['machine'].cov
            base2 = C.run(spec_named('H2', reach))
            cov |= base2['machine'].cov
            good = (hits(base) == [5] and hits(mutated) == [5] and hits(base2) == [] and hits(h2) == [5])
            ok &= good
            lines.append('%-6s %s\n         expected: %s\n         got: baseline H1-edge hits %s, mutated H1-edge hits %s; baseline H2 hits %s, mutated H2 hits %s' % (
                'CAUGHT' if good else 'MISSED', mu['name'], mu['expect'], hits(base), hits(mutated), hits(base2), hits(h2)))
            continue
        good = bool(mu['pred'](base, mutated))
        ok &= good
        tag = ('OK    ' if good else 'FAILED') if mu.get('control') else ('CAUGHT' if good else 'MISSED')
        lines.append('%-6s %s [%s]\n         expected: %s' % (tag, mu['name'], mu['scen'], mu['expect']))
    return lines, ok, cov


# ---- load delay ----------------------------------------------------------------------------------------------------------------
def load_delay(cov):
    LOADS = (0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26)

    def reads(w):
        op = w >> 26
        rs, rt = (w >> 21) & 31, (w >> 16) & 31
        if op == 0:
            fn = w & 63
            if fn in (0x00, 0x02, 0x03):
                return {rt}
            if fn in (0x08, 0x09, 0x11, 0x13):
                return {rs}
            if fn in (0x10, 0x12, 0x0C, 0x0D):
                return set()
            return {rs, rt}
        if op == 1:
            return {rs}
        if op in (2, 3, 0xF):
            return set()
        if op in (4, 5):
            return {rs, rt}
        if op in (6, 7):
            return {rs}
        if op in (0x22, 0x26):
            return {rs, rt}
        if 0x20 <= op <= 0x26:
            return {rs}
        if 0x28 <= op <= 0x2E:
            return {rs, rt}
        return {rs}

    def is_branch(w):
        op = w >> 26
        return op in (1, 2, 3, 4, 5, 6, 7) or (op == 0 and (w & 63) in (8, 9))

    def target(a, w):
        op = w >> 26
        if op in (1, 4, 5, 6, 7):
            imm = w & 0xFFFF
            simm = imm - 0x10000 if imm & 0x8000 else imm
            return a + 4 + (simm << 2)
        if op in (2, 3):
            return ((a + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
        return None
    nloads = 0
    bad = []
    for pc in sorted(cov):
        if not (0x80020000 <= pc < 0x800CA800):
            continue
        w = lib.u32(pc)
        if (w >> 26) not in LOADS:
            continue
        dst = (w >> 16) & 31
        if dst == 0:
            continue
        nloads += 1
        succ = [pc + 4]
        pw = lib.u32(pc - 4)
        if is_branch(pw):
            t = target(pc - 4, pw)
            if t is not None:
                succ.append(t)
            elif (pw >> 26) == 0:
                succ.append(None)
        for sx in succ:
            if sx is None:
                bad.append((pc, 'load in a jr/jalr delay slot', w, 0, 0))
                continue
            nw = lib.u32(sx)
            pair = (w >> 26) == 0x22 and (nw >> 26) == 0x26 and ((nw >> 16) & 31) == dst
            if dst in reads(nw) and not pair:
                bad.append((pc, 'next uses the loaded register', w, sx, nw))
    L = ['executed distinct instructions: %d; executed loads checked: %d; loaded register read by the next instruction: %d' % (len(cov), nloads, len(bad))]
    for b in bad:
        L.append('  HAZARD load %08x: %s (load word %08x, next %08x word %08x)' % b)
    L.append('VERDICT: ' + ('no executed load is followed by a use of its register: ignoring the load delay slot does not change the result' if not bad else 'HAZARDS FOUND'))
    return L, not bad
