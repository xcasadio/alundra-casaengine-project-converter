"""kmut.py - E20.c1b: mutation tests ("teeth").  One-word patches of the emulated binary (RAM only; the old word is checked).  The expected change of each mutation is written HERE,
before any mutated run: a predicate on (baseline probe, mutated probe).  A mutation is CAUGHT when the predicate holds AND the independent model of the same probe no longer agrees
(ok = False).  A control must leave the probe unchanged AND ok = True.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import kworld as K
import k1
import k2
import k3


# ---- probes: small dictionaries of the values the mutations are expected to move ---------------------------------------------------------
def pick(entries, label, hp):
    for lab, x in entries:
        if lab == label and x['hp_before'] == hp:
            return x['hp_after']
    raise KeyError((label, hp))


def probe_k1():
    out = {'ok': True}
    for name in ('kit', 'all'):
        r, m = k1.case(K.INV[name])
        out['ok'] &= r['ok']
        out[name] = dict(records=[tuple(x) for x in r['record_words']], sheet3=bytes.fromhex(r['sheet'])[3])
    return out


def probe_k2():
    out = {'ok': True}
    for name in ('kit', 'holy'):
        lines, res, ok = k2.run_inventory(K.INV[name])
        out['ok'] &= ok
        out[name] = dict(
            natural=pick(res['HERO'], 'natural record, response 0x10', 100),
            kill=pick(res['HERO'], 'copy of the record with response[nibble 1] = 0x40', 9),
            zero_resp=pick(res['HERO'], 'copy of the record with response[nibble 1] = 0x00', 100),
            syn_latch1=(pick(res['NH'], 'synthetic 3-attack record in [2], A attr 0x81 power 1, latch := 1', 100) if 'NH' in res and name == 'kit' else None),
            all_hp=[x['hp_after'] for k in ('HERO', 'NH', 'HT') for _, x in res[k]])
    return out


def probe_k3():
    lines, ok, n, m = k3.run()
    return {'ok': ok, 'r': dict(k3.RESULTS)}


PROBES = dict(k1=probe_k1, k2=probe_k2, k3=probe_k3)


def mutations():
    M = []

    def add(**kw):
        M.append(kw)
    add(id='M1', name='equipment power term dropped (0x8004473C addu t0,t0,v0 -> nop)', patch=[(0x8004473C, 0x01024021, 0x00000000)], probe='k2',
        expect='hero hit on the natural Beannoid at HP 100: kit 98 -> 99 (the sword record no longer adds 1), Holy Sword (hero attack 8 + record 8) 84 -> 92',
        pred=lambda b, m: (b['kit']['natural'], m['kit']['natural'], b['holy']['natural'], m['holy']['natural']) == (98, 99, 84, 92))
    add(id='M2', name='minimum damage 1 forced (0x8004475C bnez a1 -> nop: the damage is always set to 1)', patch=[(0x8004475C, 0x14A00003, 0x00000000)], probe='k2',
        expect='Holy Sword natural hit at HP 100: 84 -> 99 (damage 1 whatever the formula gives); the kit stays at 98 -> 99',
        pred=lambda b, m: (b['holy']['natural'], m['holy']['natural'], m['kit']['natural']) == (84, 99, 99))
    add(id='M3', name='kill response test removed (0x800446B4 beq a1,0x40 -> nop)', patch=[(0x800446B4, 0x10A20045, 0x00000000)], probe='k2',
        expect='the copy of the record with response 0x40 no longer kills: kit HP 9 stays 9 (baseline 0)',
        pred=lambda b, m: (b['kit']['kill'], m['kit']['kill']) == (0, 9))
    add(id='M4', name='response with no top bits no longer reaches the damage code (0x800446AC beqz a1 -> nop)', patch=[(0x800446AC, 0x10A00005, 0x00000000)], probe='k2',
        expect='a plain response 0x10 leaves HP unchanged: kit natural hit at HP 100 stays 100 (baseline 98)',
        pred=lambda b, m: (b['kit']['natural'], m['kit']['natural']) == (98, 100))
    add(id='M5', name='damage shift 4 -> 3 (0x80044758 sra a1,t4,4 -> sra a1,t4,3)', patch=[(0x80044758, 0x000C2903, 0x000C28C3)], probe='k2',
        expect='the damage doubles: kit natural hit at HP 100: 98 -> 96',
        pred=lambda b, m: (b['kit']['natural'], m['kit']['natural']) == (98, 96))
    add(id='M6', name='attack index g + 1 -> g + 2 in the bound test (0x800446E4 addiu t3,v0,1 -> 2)', patch=[(0x800446E4, 0x244B0001, 0x244B0002)], probe='k2',
        expect='synthetic 3-attack record, latch 1: the index test g+2 < 3 fails, Attacks[0] (+2) is used instead of Attacks[2] (+5): HP 93 -> 96',
        pred=lambda b, m: (b['kit']['syn_latch1'], m['kit']['syn_latch1']) == (93, 96))
    add(id='M7', name='the hero working sheet is used for every target (0x8003A3A0 bne s2,hero -> nop)', patch=[(0x8003A3A0, 0x16420004, 0x00000000)], probe='k2',
        expect='the copy of the record with response 0x00 is ignored (the hero sheet response 0x10 is used): kit HP 100 -> 99 becomes 98',
        pred=lambda b, m: (b['kit']['zero_resp'], m['kit']['zero_resp']) == (99, 98))
    add(id='M8', name='the attack is read from the target itself instead of the toucher (0x8003A3BC lw a0,0x1c8(v1) -> 0x1c8(s2))', patch=[(0x8003A3BC, 0x8C6401C8, 0x8E4401C8)], probe='k2',
        expect='the Beannoid\'s own attack (attr 0x01, power 1, no equipment bit) is used: kit natural hero hit at HP 100: 98 -> 99',
        pred=lambda b, m: (b['kit']['natural'], m['kit']['natural']) == (98, 99))
    add(id='M9', name='record [0] category test removed (0x800308D0 bne v0,2 -> nop)', patch=[(0x800308D0, 0x14510005, 0x00000000)], probe='k1',
        expect='record [0] of the kit becomes the highest owned item of any category, the normal boots (25): item word 47 -> 55',
        pred=lambda b, m: (b['kit']['records'][0][1], m['kit']['records'][0][1]) == (47, 55))
    add(id='M10', name='response combination: 0x80 branch forced (0x80030BA4 bne a0,0x80 -> nop)', patch=[(0x80030BA4, 0x14890003, 0x00000000)], probe='k1',
        expect='every response of the kit sheet becomes 0x80: sheet[3] 0x10 -> 0x80',
        pred=lambda b, m: (b['kit']['sheet3'], m['kit']['sheet3']) == (0x10, 0x80))
    add(id='M11', name='weapon record stored in slot [2] (0x80030940 sw v0,8(v1) -> sw v0,0x10(v1))', patch=[(0x80030940, 0xAC620008, 0xAC620010)], probe='k1',
        expect='kit record [1] pointer becomes null and record [2] gets the sword record pointer',
        pred=lambda b, m: (b['kit']['records'][1][0] != 0 and m['kit']['records'][1][0] == 0 and b['kit']['records'][2][0] == 0 and m['kit']['records'][2][0] == b['kit']['records'][1][0]))
    add(id='M12', name='0x86 stores zero (0x8004002C sw s0,0x1cc(v0) -> sw zero)', patch=[(0x8004002C, 0xAC5001CC, 0xAC4001CC)], probe='k3',
        expect='after [0x86, 0, 0x2C, 0x01] T.+0x1CC stays 0 (baseline 0x12C)',
        pred=lambda b, m: (b['r']['search record 0 (T), value 0x012C'][1], m['r']['search record 0 (T), value 0x012C'][1]) == (0x12C, 0))
    add(id='M13', name='0x87 nibble comparison removed (0x800400E0 bne v0,s0 -> nop)', patch=[(0x800400E0, 0x14500003, 0x00000000)], probe='k3',
        expect='T touched by the hero sword (nibble 1) answers 1 to typeIdx 3 (nibble 4): baseline 0',
        pred=lambda b, m: (b['r']['T (hero sword, nibble 1) typeIdx 3 -> nibble 4'], m['r']['T (hero sword, nibble 1) typeIdx 3 -> nibble 4']) == (0, 1))
    add(id='M14', name='0x95 mask test removed (0x800408BC beqz -> nop)', patch=[(0x800408BC, 0x10400033, 0x00000000)], probe='k3',
        expect='the mask 0x0040 that no entity has now matches: Result 0 -> 1',
        pred=lambda b, m: (b['r']['same rect, mask 0x0040 (no entity has the bit)'], m['r']['same rect, mask 0x0040 (no entity has the bit)']) == (0, 1))
    add(id='M15', name='0x95 reads typeIdx from byte 8 instead of byte 9 (0x80040824 lbu 9 -> 8)', patch=[(0x80040824, 0x90C30009, 0x90C30008)], probe='k3',
        expect='typeIdx 3 is taken as index 0 (maskHi = 0): the hero sword rectangle answers 1 (baseline 0)',
        pred=lambda b, m: (b['r']['same rect, typeIdx 3 (nibble 4: no attacker of that kind there)'], m['r']['same rect, typeIdx 3 (nibble 4: no attacker of that kind there)']) == (0, 1))
    add(id='M16', name='0x95 Y size one raw unit larger (0x80040808 addiu t7,v1,-1 -> 0)', patch=[(0x80040808, 0x246FFFFF, 0x246F0000)], probe='k3',
        expect='the first outside point on the Y axis (d = size) is now inside: Result 0 -> 1',
        pred=lambda b, m: (b['r']['hero box Y axis: d = size (first outside), rect (10,6,0,1,1)'], m['r']['hero box Y axis: d = size (first outside), rect (10,6,0,1,1)']) == (0, 1))
    add(id='M17', name='0x71 reads +0x224 instead of +0x220 (0x8003FA10 lw 0x220 -> 0x224)', patch=[(0x8003FA10, 0x8C820220, 0x8C820224)], probe='k3',
        expect='Result of 0x71 on the hero is his TouchingEntity (0) instead of HitCounter 2; on T it is the pointer to the hero 0x80127D30',
        pred=lambda b, m: (b['r']['self = hero (HitCounter in memory 2)'], m['r']['self = hero (HitCounter in memory 2)'], m['r']['self = T (HitCounter in memory 0)']) == (2, 0, 0x80127D30))
    add(id='C1', control=True, name='CONTROL: debug-print read of ComputeNewHp patched (0x8003A3E0 lw -> nop; reached only when [0x800DC058] < 0)', patch=[(0x8003A3E0, 0x8C420004, 0x00000000)], probe='k2',
        expect='nothing changes: every value of the K2 probe equal', pred=lambda b, m: b == m)
    add(id='C2', control=True, name='CONTROL: spark loop count 3 -> 2 (0x800391A8, the hit pass)', patch=[(0x800391A8, 0x24130003, 0x24130002)], probe='k3',
        expect='nothing the K3 probe records changes (the sparks do not feed the opcodes)', pred=lambda b, m: b == m)
    add(id='C3', control=True, name='CONTROL: spark loop count 3 -> 2 (0x800391A8)', patch=[(0x800391A8, 0x24130003, 0x24130002)], probe='k2',
        expect='nothing the K2 probe records changes (damage does not depend on the sparks)', pred=lambda b, m: b == m)
    return M


def run():
    lines = ['# one-word patches of the emulated binary (RAM only); the expected change is written in tools/kmut.py before any run; CAUGHT = predicate holds and the independent model disagrees']
    ok = True
    base = {}
    for name, fn in PROBES.items():
        K.PATCHES[:] = []
        base[name] = fn()
        if not base[name]['ok']:
            ok = False
            lines.append('BASELINE probe %s FAILED' % name)
    for mu in mutations():
        K.PATCHES[:] = list(mu['patch'])
        try:
            mut = PROBES[mu['probe']]()
        finally:
            K.PATCHES[:] = []
        b = base[mu['probe']]
        try:
            changed = bool(mu['pred'](b, mut)) if not mu.get('control') else bool(mu['pred'](b, mut))
        except Exception as e:           # noqa
            changed = False
            lines.append('   (predicate error: %r)' % (e,))
        if mu.get('control'):
            good = changed and mut['ok']
            tag = 'OK    ' if good else 'FAILED'
        else:
            good = changed and not mut['ok']
            tag = 'CAUGHT' if good else 'MISSED'
        ok &= good
        lines.append('%s %s [%s]\n         expected: %s\n         predicate %s; independent model %s with the patch' % (
            tag, mu['name'], mu['probe'], mu['expect'], 'holds' if changed else 'DOES NOT HOLD', 'still agrees' if mut['ok'] else 'disagrees'))
    return lines, ok


if __name__ == '__main__':
    ls, ok = run()
    print('\n'.join(ls))
    print('MUTATIONS', 'PASS' if ok else 'FAIL')
