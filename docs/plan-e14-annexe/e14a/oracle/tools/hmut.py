"""hmut.py - E14.a0b: mutation tests ("teeth").  One-word patches of the emulated binary (RAM only; the old word is checked).  The expected change of each mutation is written HERE, before any mutated run: a predicate on
(baseline probe, mutated probe) built from the INPUTS of the states and from variants of the independent model, never from the mutated output.  A mutation is CAUGHT when the predicate holds AND the independent model of the same
probe no longer agrees (ok = False).  A control must leave the probe unchanged AND ok = True.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lib
import hmodel as H
import h1
import h2
from hmodel import s32, M32

PROBE_N = 800
_HELP = {h['name']: h for h in h1.helpers()}
_BASE = {}


def probe_h1(name):
    h = _HELP[name]
    rows, st = h1.run_helper(h, 'emu', PROBE_N, h1.cases_of(h, PROBE_N))
    return dict(rows=rows, ok=not st['model_bad'] and not st['bad_write'] and not st['uninit'])


def probe_h2():
    rows, problems, st = h2.run_map(116, 'emu', light=(24, 25))      # the bodies of the grate whose sweep crosses sloped cells (the slope term of 0x800370C4)
    return dict(rows=rows, ok=not problems and not st['uninit'], words=st['words'])


PROBES = {'relpos': lambda: probe_h1('relpos'), 'relpos2': lambda: probe_h1('relpos2'), 'tap2': lambda: probe_h1('tap2'), 'tapfront': lambda: probe_h1('tapfront'),
          'had': lambda: probe_h1('had'), 'startwalk': lambda: probe_h1('startwalk'), 'atan': lambda: probe_h1('atan'), 'd1': lambda: probe_h1('d1'), 'd9': lambda: probe_h1('d9'),
          'ground': probe_h2}


def P(addr, new):
    return (addr, lib.u32(addr), new)


def pair(b, m):
    return list(zip(b['rows'], m['rows']))


def changed(b, m):
    return any(x[1] != y[1] for x, y in pair(b, m))


def all_rows(b, m, f):
    return all(f(x[0], x[1], y[1]) for x, y in pair(b, m))


def had_model(c, dist_delta=0):
    floor, c0, c1, c2, c3, direction, anim, dist = c
    res, na, nd = H.handle_animation_direction(floor, [c0, c1, c2, c3], direction, anim, dist + dist_delta)
    return [1, na, nd] + [0] * 6 if res else [0, h1.MARK_ANIM, direction] + [s32(h1.MARK_FORCE)] * 6


def mutations():
    M = []

    def add(**kw):
        M.append(kw)
    add(id='M1', name='relpos: |dx| always negated (0x8007FE50 bgez -> nop: the skip over the negu is gone)', patch=[P(0x8007FE50, 0)], probe='relpos',
        expect='the first word equals -dx on every state (the instruction after the branch, negu v1,v1, now always runs); at least one state changes',
        pred=lambda b, m: changed(b, m) and all(o[0] == s32(-o[3]) for _, o in m['rows']))
    add(id='M2', name='relpos: dy is a sum (0x8007FE44 subu -> addu)', patch=[P(0x8007FE44, 0x00C83021)], probe='relpos',
        expect='dy = self_tile_y + hero_tile_y on every state',
        pred=lambda b, m: changed(b, m) and all(o[4] == s32(c[1] + c[4]) for c, o in m['rows']))
    add(id='M3', name='relpos2: Z read from +0x120 instead of +0x11C (0x8007FEA4 lw)', patch=[P(0x8007FEA4, 0x8C840120)], probe='relpos2',
        expect='dz = self_tile_x - hero_floor on every state',
        pred=lambda b, m: changed(b, m) and all(o[5] == s32(c[0] - c[5]) for c, o in m['rows']))
    add(id='M4', name='tap2: the Z test inverted (0x8008049C bnez -> beqz)', patch=[P(0x8008049C, 0x10400048)], probe='tap2',
        expect='every state with d3 >= |dz| answers 0',
        pred=lambda b, m: changed(b, m) and all(o[0] == 0 for c, o in m['rows'] if s32(c[9]) >= s32(c[2])))
    add(id='M5', name='tap2: direction 0 tests the opposite sign of dy (0x80080500 bgtz -> bltz)', patch=[P(0x80080500, 0x0440002F)], probe='tap2',
        expect='states of direction other than 0 unchanged; direction 0 answers 0 when d3 < |dz|, a3 < |dx|, dy < 0 or d2 < |dy|, else 1',
        pred=lambda b, m: changed(b, m) and all(
            (o[0] == bo[0]) if (c[6] & 0xFF) != 0 else
            (o[0] == (0 if (s32(c[9]) < s32(c[2]) or s32(c[7]) < s32(c[0]) or s32(c[4]) < 0 or s32(c[8]) < s32(c[1])) else 1))
            for (c, bo), (_, o) in zip(b['rows'], m['rows'])))
    add(id='M6', name='tapfront: the direction is read from +0x9C (AnimationDirection) instead of +0x98 (0x800805E0 lw)', patch=[P(0x800805E0, 0x8C84009C)], probe='tapfront',
        expect='every state answers as the model for the decoy direction (box_direction + 1) & 3',
        pred=lambda b, m: changed(b, m) and all(o[0] == H.try_attack_front((c[0] + 1) & 3, c[1:7], c[7], c[8], c[9]) for c, o in m['rows']))
    add(id='M7', name='had: threshold = dist instead of dist + 1 (0x80080148 addiu -> nop)', patch=[P(0x80080148, 0)], probe='had',
        expect='every state answers as the model with dist - 1',
        pred=lambda b, m: changed(b, m) and all([x & M32 for x in o] == [x & M32 for x in had_model(c, -1)] for c, o in m['rows']))
    add(id='M8', name='had: +0xD8 not cleared on a turn (0x80080234 sw -> nop)', patch=[P(0x80080234, 0)], probe='had',
        expect='a turn leaves the old +0xD8 word (the marker) and clears the five others; states that do not turn unchanged',
        pred=lambda b, m: changed(b, m) and all(
            (o == bo) if bo[0] == 0 else (o[:8] == bo[:8] and o[8] == s32(h1.MARK_FORCE)) for (c, bo), (_, o) in zip(b['rows'], m['rows'])))
    add(id='M9', name='had: last range test d < 0x10 -> d < 0x11 (0x80080210 slti)', patch=[P(0x80080210, 0x28820011)], probe='had',
        expect='only states whose target direction is 16 can change',
        pred=lambda b, m: changed(b, m) and all(c[5] == 16 for (c, bo), (_, o) in zip(b['rows'], m['rows']) if o != bo))
    add(id='M10', name='startwalk: the chance scale 0x65 -> 0x64 (0x8007FF18 addiu)', patch=[P(0x8007FF18, 0x24020064)], probe='startwalk',
        expect='every state answers as the model with the scale 0x64',
        pred=lambda b, m: changed(b, m) and all(
            [x & M32 for x in o] == [x & M32 for x in (lambda r: [s32(r[0]), r[1], r[2], r[3] if r[3] is not None else h1.MARK_HALF])(H.start_walk(c[0] & M32, c[1], c[2], c[3], c[4], c[5], c[6], c[7], pct=0x64))]
            for c, o in m['rows']))
    add(id='M11', name='startwalk: the aim reads the Y of the entity for its X (0x8007FF70 lw 0x114 -> 0x118)', patch=[P(0x8007FF70, 0x8E040118)], probe='startwalk',
        expect='every state answers as the model with self_x := self_y',
        pred=lambda b, m: changed(b, m) and all(
            [x & M32 for x in o] == [x & M32 for x in (lambda r: [s32(r[0]), r[1], r[2], r[3] if r[3] is not None else h1.MARK_HALF])(H.start_walk(c[0] & M32, c[2], c[2], c[3], c[4], c[5], c[6], c[7]))]
            for c, o in m['rows']))
    add(id='M12', name='startwalk: the 16-bit value stored at +0x280 instead of +0x282 (0x80080010 sh)', patch=[P(0x80080010, 0xA6020280)], probe='startwalk',
        expect='the half at +0x282 keeps its marker on every state',
        pred=lambda b, m: changed(b, m) and all(o[3] == h1.MARK_HALF for _, o in m['rows']))
    add(id='M13', name='atan: the flip bit of a negative dx never set (0x8002E820 addiu t0,t0,1 -> nop)', patch=[P(0x8002E820, 0)], probe='atan',
        expect='states with dx >= 0 unchanged; states with dx < 0 answer as the baseline does for -dx',
        pred=lambda b, m: changed(b, m) and all((o == bo) if s32(c[0]) >= 0 else (o[0] == H.atan_dir(-s32(c[0]), c[1])) for (c, bo), (_, o) in zip(b['rows'], m['rows'])))
    add(id='M14', name='d1: animation 6 instead of 5 (0x8007D9C4 addiu)', patch=[P(0x8007D9C4, 0x24020006)], probe='d1',
        expect='the animation word is 6 on every state', pred=lambda b, m: changed(b, m) and all(o[1] == 6 for _, o in m['rows']))
    add(id='M15', name='d9: the death byte written at +0x276 instead of +0x277 (0x8007DE20 sb)', patch=[P(0x8007DE20, 0xA2020276)], probe='d9',
        expect='+0x277 keeps its old value on every state', pred=lambda b, m: changed(b, m) and all(o[0] == (c[5] & 0xFF) for c, o in m['rows']))
    add(id='M16', name='d1: the hero X read from +0x118 (0x8007D9D8 lw 0x114 -> 0x118)', patch=[P(0x8007D9D8, 0x8C440118)], probe='d1',
        expect='the direction is the model direction of (self_x - hero_y, self_y - hero_y)',
        pred=lambda b, m: changed(b, m) and all(o[2] == H.atan_dir(s32(c[0] - c[3]), s32(c[1] - c[3])) for c, o in m['rows']))
    add(id='M17', name='floor: terrain + 2 (0x80037F3C addiu t3,v1,1 -> 2)', patch=[P(0x80037F3C, 0x246B0002)], probe='ground',
        expect='floor - terrain is 2 for every body and every hero row',
        pred=lambda b, m: changed(b, m) and all(e[12] - e[11] == 2 and h[12] - h[11] == 2 for _, e, h, r in m['rows']))
    add(id='M18', name='corners: a sloped cell uses height instead of height - 1 (0x800371EC addiu -1 -> 0)', patch=[P(0x800371EC, 0x24A20000)], probe='ground',
        expect='terrain and corners equal the model with sub = 0 on every row (and some row differs from the baseline)',
        pred=lambda b, m: changed(b, m) and all(
            (lambda t, c: (v[11], v[13:17]) == (t, c))(*H.ground_model(lambda r, c_: m['words'][(r, c_)], v[0], v[1], v[3], v[4], v[6], v[7], sub=0))
            for _, e, h, r in m['rows'] for v in (e, h)))
    add(id='M19', name='corners: the running value is a minimum (0x8003732C slt t2,a1 -> slt a1,t2)', patch=[P(0x8003732C, 0x00AA102A)], probe='ground',
        expect='terrain = min(0, corners) on every row; the stored corners are unchanged',
        pred=lambda b, m: changed(b, m) and all(v[11] == min([0] + v[13:17]) and v[13:17] == H.ground_model(lambda r, c_: m['words'][(r, c_)], v[0], v[1], v[3], v[4], v[6], v[7])[1]
                                                 for _, e, h, r in m['rows'] for v in (e, h)))
    add(id='C1', control=True, name='CONTROL: an unreachable instruction of tap2 (0x800804C4 j -> nop)', patch=[P(0x800804C4, 0)], probe='tap2',
        expect='nothing changes', pred=lambda b, m: b['rows'] == m['rows'])
    add(id='C2', control=True, name='CONTROL: an unreachable instruction of atan (0x8002E8C0 j -> nop)', patch=[P(0x8002E8C0, 0)], probe='atan',
        expect='nothing changes', pred=lambda b, m: b['rows'] == m['rows'])
    return M


def dis_of(addr, word):
    import capstone
    md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
    r = list(md.disasm(word.to_bytes(4, 'little'), addr))
    return '%s %s' % (r[0].mnemonic, r[0].op_str) if r else '.word 0x%08x' % word


def run():
    lines = ['# one-word patches of the emulated binary (RAM only); the expected change of each is written in tools/hmut.py before any run; CAUGHT = predicate holds and the independent model disagrees']
    ok = True
    base = {}
    h1.PATCHES[:] = []
    for name in PROBES:
        base[name] = PROBES[name]()
        if not base[name]['ok']:
            ok = False
            lines.append('BASELINE probe %s FAILED' % name)
    for mu in mutations():
        h1.PATCHES[:] = list(mu['patch'])
        try:
            mut = PROBES[mu['probe']]()
        finally:
            h1.PATCHES[:] = []
        b = base[mu['probe']]
        try:
            holds = bool(mu['pred'](b, mut))
        except Exception as e:           # noqa
            holds = False
            lines.append('   (predicate error: %r)' % (e,))
        if mu.get('control'):
            good = holds and mut['ok']
            tag = 'OK    ' if good else 'FAILED'
        else:
            good = holds and not mut['ok']
            tag = 'CAUGHT' if good else 'MISSED'
        ok &= good
        patch = '; '.join('0x%08X %08x (%s) -> %08x (%s)' % (a, o, dis_of(a, o), n, dis_of(a, n)) for a, o, n in mu['patch'])
        lines.append('%s %s %s [%s]\n         patch: %s\n         expected: %s\n         predicate %s; independent model %s with the patch' % (
            tag, mu['id'], mu['name'], mu['probe'], patch, mu['expect'], 'holds' if holds else 'DOES NOT HOLD', 'still agrees' if mut['ok'] else 'disagrees'))
    return lines, ok


if __name__ == '__main__':
    ls, ok = run()
    print('\n'.join(ls))
    print('MUTATIONS', 'PASS' if ok else 'FAIL')
