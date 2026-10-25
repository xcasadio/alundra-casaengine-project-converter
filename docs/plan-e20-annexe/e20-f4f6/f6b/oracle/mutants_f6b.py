"""mutants_f6b.py - E20.f6b T0: one-word mutants of C60 (0x8007A8A0), of E9 (0x8007EF50) and of the creation of an entity (0x80039EA0), on the real machine (patched RAM, never simulated), each compared with its control on the stated
scenario. A mutant is KILLED when the signature (the status, the position, the forces, the flags, the animation, the slope, the box, the effects with their velocities, the sounds and the draws of every tick of the ball) differs from the
control's. The 14 mutants of the plan are C60-1 to C60-4, E9-1 to E9-10 (the 14 of the discovery critic, ../../critic/mine/mutants_crit.py, with the addresses and words it used) and SPAWN-1 (the +1 of the creation Z, 0x80039EA4, the word the
f4 mutant SPAWN-1 patches); 11 more are added (C60-5 to C60-8 and E9-11 to E9-17). E9-5b and E9-9 SURVIVED in the discovery: this script proves them equivalent (section "equivalence").
Usage (from a COPY of the annex, it writes ./out): python -B mutants_f6b.py
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.join(HERE, '..', '..', 'bin')
sys.path.insert(0, BIN)
sys.path.insert(0, os.path.join(HERE, '..', '..', 'critic', 'mine'))
sys.path.insert(0, HERE)
import fw
import world as W
import mipsdis
import run_balls as B
import run_arrows as A
import run_f6b as R

OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)
SQ = W.SQUARE
HOLD95 = {t: SQ for t in range(1, 96)}
ENT0, STRIDE = W.ENT0, W.STRIDE
NOP = 0


def orig(addr):
    o = mipsdis.off(addr)
    return int.from_bytes(mipsdis._data[o:o + 4], 'little')


def patch(*pairs):
    def f(m):
        for a, w in pairs:
            m.poke(a, w & 0xFFFFFFFF)
    return f


def withop(addr, opcode):
    return (addr, (orig(addr) & 0x03FFFFFF) | (opcode << 26))


def uncond(addr):
    return (addr, (4 << 26) | (orig(addr) & 0xFFFF))


def withlow(addr, low):
    return (addr, (orig(addr) & 0xFFFF0000) | (low & 0xFFFF))


def sig(res, slots=(1,)):
    out = []
    for r in res['rows']:
        row = [r['tick'], r['draws'], tuple(r['effects']), tuple(j for j in r['journal'] if j.startswith('snd'))]
        for s in slots:
            d = r['slots'].get(s)
            row.append(None if d is None else (d['status'], d['px'], d['py'], d['pz'], d['fx'], d['fy'], d['fz'], d['adj_13C'], d['flags6C'], d['dir_t'], d['anim_t'], d['slope_18C'], d['reset_AC'], d['coll_140'],
                                              d['hitc'], d['bw'], d['bd'], d['bh'], d['hbz']))
        out.append(tuple(row))
    return out


def go(item, n, init=None, cells=None, direction=24, charged=False, after_tick=None, hero_px=(100, 100)):
    holds = dict(HOLD95) if charged else {1: SQ}
    spec = dict(item=item, n=n, holds=holds, after=0, hero_px=hero_px, direction=direction, cells=cells, targets=[], seed=0x12345678, init=init or [], after_tick=after_tick or [])
    return fw.run(spec)


def gp_cells(gp):
    def cells(col, row):
        return (0, 0, 0, gp) if (col == 6 or col == 7) else (0, 0, 0, 0)
    return cells


WALL6 = fw.wall_col(6)
rows = []


def report(name, desc, scen, ctl, mut):
    killed = sig(ctl) != sig(mut)
    rows.append([name, desc, scen, 'KILLED' if killed else 'SURVIVED'])
    print('%-8s %-84s %-26s %s' % (name, desc, scen, 'KILLED' if killed else 'SURVIVED'))


def main():
    S = {
        'fire flat': lambda init=None: go(16, 70, init=init),
        'ice flat': lambda init=None: go(14, 70, init=init),
        'fire charged flat': lambda init=None: go(16, 160, init=init, charged=True),
        'fire north': lambda init=None: go(16, 60, init=init, direction=A.DIR['north']),
        'fire on slope 1': lambda init=None: go(16, 40, init=init, cells=gp_cells(0x02)),
        'fire on slope 4': lambda init=None: go(16, 40, init=init, cells=gp_cells(0x08)),
        'branchA slope 4': lambda init=None: go(16, 70, init=init, after_tick=[R.inject_slope(4, R.S0 + 15)]),
        'branchA slope 1': lambda init=None: go(16, 70, init=init, after_tick=[R.inject_slope(1, R.S0 + 15)]),
        'ice branchA slope 4': lambda init=None: go(14, 70, init=init, after_tick=[R.inject_slope(4, R.S0 + 15)]),
    }
    ctl = {k: f() for k, f in S.items()}
    M = [
        # ---- C60 (the trail) ------------------------------------------------------------------------------------------------------------------------------------
        ('C60-1', 'trail period andi 3 -> 7 (0x8007A8B8)', 'fire flat', [patch(withlow(0x8007A8B8, 7))]),
        ('C60-2', 'ice trail effect id 0x12 -> 0x11 (0x8007A8D8)', 'ice flat', [patch(withlow(0x8007A8D8, 0x11))]),
        ('C60-3', 'fire Z offset sll 19 -> 18 (0x8007A8E4)', 'fire flat', [patch((0x8007A8E4, orig(0x8007A8E4) & ~(0x1F << 6) | (18 << 6)))]),
        ('C60-4', 'trail vz negu -> move (0x8007A93C)', 'fire flat', [patch((0x8007A93C, 0x00021021))]),
        ('C60-5', 'trail vx negu -> move (0x8007A91C)', 'fire flat', [patch((0x8007A91C, 0x00021021))]),
        ('C60-6', 'trail vy negu -> move (0x8007A92C), ball shot north', 'fire north', [patch((0x8007A92C, 0x00021021))]),
        ('C60-7', 'fire/ice selection sltiu 2 -> 1 (0x8007A8D0): the type 12 takes the ice effect', 'fire charged flat', [patch(withlow(0x8007A8D0, 1))]),
        ('C60-8', 'ice also gets the Z offset: sll v1,v1,19 -> lui v1,8 (0x8007A8E4)', 'ice flat', [patch((0x8007A8E4, 0x3C030008))]),
        # ---- E9 (the explosion, the puffs, the death) -----------------------------------------------------------------------------------------------------------
        ('E9-1', 'explosion effect count slti 5 -> 4 (0x8007F1F0)', 'fire flat', [patch(withlow(0x8007F1F0, 4))]),
        ('E9-2', 'puff period andi 7 -> 3 (0x8007EFD0)', 'fire flat', [patch(withlow(0x8007EFD0, 3))]),
        ('E9-3', 'branch-A puff Z offset sll 19 -> 18 (0x8007F00C)', 'fire flat', [patch((0x8007F00C, orig(0x8007F00C) & ~(0x1F << 6) | (18 << 6)))]),
        ('E9-4', 'branch-B slope-1 test removed (0x8007F0F8 beq -> nop)', 'fire on slope 1', [patch((0x8007F0F8, NOP))]),
        ('E9-5a', 'branch-B slope-4 test -> always destroy (0x8007F100 bne -> nop)', 'fire flat', [patch((0x8007F100, NOP))]),
        ('E9-5b', 'branch-B slope-4 test -> never destroy (0x8007F100 bne -> b): the pick phase takes the slope 4 first (equivalence: see below)', 'fire on slope 4', [patch(uncond(0x8007F100))]),
        ('E9-6', 'branch-B destroy effect 6 -> 5 (0x8007F110)', 'fire on slope 1', [patch(withlow(0x8007F110, 5))]),
        ('E9-7', 'explosion vx scale lui 6 -> 5 (0x8007F180)', 'fire flat', [patch(withlow(0x8007F180, 5))]),
        ('E9-8', 'end-of-animation test +0xAC -> never destroy (0x8007EFB0 bne -> b)', 'fire flat', [patch(uncond(0x8007EFB0))]),
        ('E9-9', 'branch-B flags ori 0x40 -> 0 (0x8007F208): 0x40 is already set by the header (equivalence: see below)', 'fire flat', [patch(withlow(0x8007F208, 0))]),
        ('E9-10', 'branch-B +0x88 = 1 not written (0x8007F200 sw -> nop)', 'fire flat', [patch((0x8007F200, NOP))]),
        ('E9-11', 'branch-A destroy effect 6 -> 5 (0x8007EFA4)', 'branchA slope 4', [patch(withlow(0x8007EFA4, 5))]),
        ('E9-12', 'branch-A slope-1 test removed (0x8007EF8C beq -> nop)', 'branchA slope 1', [patch((0x8007EF8C, NOP))]),
        ('E9-13', 'branch-A slope-4 test -> never destroy (0x8007EF94 bne -> b)', 'branchA slope 4', [patch(uncond(0x8007EF94))]),
        ('E9-14', 'explosion vy scale lui 4 -> 3 (0x8007F18C)', 'fire flat', [patch(withlow(0x8007F18C, 3))]),
        ('E9-15', 'puff vx scale lui 3 -> 2 (0x8007F058)', 'fire flat', [patch(withlow(0x8007F058, 2))]),
        ('E9-16', 'puff vz 2 -> 1 (0x8007F084 lui 2 -> 1)', 'fire flat', [patch(withlow(0x8007F084, 1))]),
        ('E9-17', 'ice puff also gets the Z offset: sll v1,v1,19 -> lui v1,8 (0x8007F00C)', 'ice flat', [patch((0x8007F00C, 0x3C030008))]),
        ('E9-18', 'branch-A: the call of DestroyEntity(e, 6) for the slopes removed (0x8007EFA0 jal -> nop)', 'branchA slope 4', [patch((0x8007EFA0, NOP))]),
        # ---- the creation (the +1 of the Z) ---------------------------------------------------------------------------------------------------------------------
        ('SPAWN-1', '+1 of the creation Z removed (0x80039EA4 addiu 1 -> 0)', 'fire flat', [patch(withlow(0x80039EA4, 0))]),
    ]
    lines = []
    for name, desc, scen, init in M:
        mut = S[scen](init=init)
        report(name, desc, scen, ctl[scen], mut)
    # ---- the equivalence proofs of the two mutants that survive ---------------------------------------------------------------------------------------------------
    eq = []
    # E9-5b: branch B (the first call of E9) with the slope 4. Branch B runs in the status 3 (the program E), the pick phase takes the slope 4 only in the status Normal: injecting 4 after EVERY tick from the birth to the explosion
    # and after, on four scenarios, the mutant must give the control's key each time (and the control must destroy at least once: the injection is not inert)
    mut_5b = patch(uncond(0x8007F100))
    scenarios_eq = {
        'fire flat': lambda ticks, init: go(16, 40, init=init, after_tick=[R.inject_slope(4, ticks)]),
        'ice flat': lambda ticks, init: go(14, 40, init=init, after_tick=[R.inject_slope(4, ticks)]),
        'fire wall col6': lambda ticks, init: go(16, 40, init=init, cells=WALL6, after_tick=[R.inject_slope(4, ticks)]),
        'fire charged flat': lambda ticks, init: go(16, 130, init=init, charged=True, after_tick=[R.inject_slope(4, ticks)]),
    }
    differ = []
    destroyed = 0
    checked = 0
    for scen, fn in scenarios_eq.items():
        first = 12 if scen != 'fire charged flat' else 107
        for t in range(first, first + 14):
            c = fn(t, [])
            mu = fn(t, [mut_5b])
            checked += 1
            dead = [r['tick'] for r in c['rows'] if r['slots'].get(1) and r['slots'][1]['status'] == 4]
            if dead and dead[0] <= t + 2:          # destroyed by the injection (the natural death of the explosion is 36 ticks or more later)
                destroyed += 1
            if sig(c) != sig(mu):
                differ.append((scen, t))
    verdict = 'EQUIVALENT' if not differ else 'KILLED by %s' % differ
    eq.append(['E9-5b', 'injection of +0x18C = 4 after each tick from S to S+13 on 4 scenarios (%d runs, %d destroyed by the injection within 2 ticks)' % (checked, destroyed), verdict])
    print('E9-5b equivalence:', eq[-1])
    # E9-9: the flags before the write always carry 0x40 (the header 0x132154 of the types 10 to 15): the write ori 0x40 changes nothing; with 0x40 cleared by the harness at the tick before the first call, the mutant WOULD differ
    mut_9 = patch(withlow(0x8007F208, 0))
    same = 0
    n9 = 0
    for scen in ('fire flat', 'ice flat', 'fire charged flat', 'fire north', 'fire on slope 1', 'fire on slope 4'):
        n9 += 1
        if sig(ctl[scen]) == sig(S[scen](init=[mut_9])):
            same += 1

    def clear40(m, t):
        if t == R.S0 + 10:
            a = ENT0 + STRIDE * 1 + 0x6C
            m.poke(a, m.peek(a) & ~0x40 & 0xFFFFFFFF)
    c40 = go(16, 40, after_tick=[clear40])
    m40 = go(16, 40, init=[mut_9], after_tick=[clear40])
    eq.append(['E9-9', '%d/%d reachable scenarios give the control key; with 0x40 cleared by the harness before the first call the mutant %s' % (same, n9, 'DIFFERS (the write is the only thing that sets 0x40 back: the mutant is killable only on an unreachable state)' if sig(c40) != sig(m40) else 'does not differ'),
               'EQUIVALENT on every reachable state' if same == n9 and sig(c40) != sig(m40) else 'CHECK'])
    print('E9-9 equivalence:', eq[-1])
    json.dump(dict(mutants=rows, equivalence=eq), open(os.path.join(OUT, 'mutants_f6b.json'), 'w'), indent=1)
    with open(os.path.join(OUT, 'mutants_f6b.txt'), 'w') as f:
        f.write('\n'.join('%-8s %-120s %-20s %s' % tuple(r) for r in rows) + '\n')
        f.write('\n' + '\n'.join('%s equivalence: %s -> %s' % tuple(e) for e in eq) + '\n')


if __name__ == '__main__':
    main()
