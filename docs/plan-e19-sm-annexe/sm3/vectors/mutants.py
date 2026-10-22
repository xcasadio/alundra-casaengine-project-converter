"""E19.sm3: which vectors kill which one-line mutant of the planned C# rules.  Each mutant is a python model of the DLL-shaped rule (EntitySearchService python
translation + the planned case body) with ONE change; it is run on every vector world and compared with the binary's Result recorded in the vectors.
A mutant killed by a HAND-WRITTEN vector (not a sweep or a random row) has a named killing test; the sweeps and the random rows are extra nets.

Usage: python -B mutants.py VECTORS_DIR      (reads sm3-vectors.jsonl, rebuilds the worlds from gen_vectors.hand_vectors/sweep_vectors/random_vectors)
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gen_vectors as g   # noqa: E402
from oracle import ent_addr, s32  # noqa: E402


def ctx(spec):
    w = g.W(spec)
    return w


def group(w, ref, t):
    return g.dll_search2(w, ref, t)


# -------------------------------------------------------------------------------------------------------- the planned rules (reference = no mutation)
def r_a9(w, ref, b, mut=None):
    t = b[1]
    ms = group(w, ref, t) if mut != 'hero_ref' else group(w, 0, t)
    me = ref if mut != 'hero_ref' else 0
    S = w.e[me]
    if mut == 'first_only':
        ms = ms[:1]
    if mut == 'swapped':
        return int(any(w.e[m]['xcoll'] == ent_addr(me) for m in ms))
    return int(any(S['xcoll'] == ent_addr(m) for m in ms))


def r_aa(w, ref, b, mut=None):
    t = b[1]
    me = ref if mut != 'hero_ref' else 0
    ms = group(w, ref, t) if mut != 'hero_ref' else group(w, 0, t)
    if mut == 'first_only':
        ms = ms[:1]
    if mut == 'swapped':
        return int(any(w.e[me]['xcoll'] == ent_addr(m) for m in ms))
    return int(any(w.e[m]['xcoll'] == ent_addr(me) for m in ms))


def r_b2(w, ref, b, mut=None):
    g1 = group(w, ref, b[1])
    g2 = group(w, ref, b[2] if mut != 'second_uses_first_type' else b[1])
    if mut == 'g1_only':
        return int(bool(g1))
    if mut == 'self_intersect':
        return int(bool(g1) and bool(g2))
    if mut == 'all_not_any':
        return int(bool(g1) and bool(g2) and all(x in g2 for x in g1))
    if mut == 'by_ref_id':
        ids1 = {w.e[x]['ref'] for x in g1}
        return int(any(w.e[y]['ref'] in ids1 for y in g2))
    if mut == 'first_of_each':
        return int(bool(g1) and bool(g2) and g1[0] == g2[0])
    if mut == 'only_last_of_g2':
        return int(bool(g1) and bool(g2) and g2[-1] in g1)
    return int(any(x in g2 for x in g1))


def fz(w, i):
    return s32(w.e[i]['forcez'])


def r_b4(w, ref, b, mut=None):
    ms = group(w, ref, b[1])
    if not ms:
        return 0
    f = fz(w, ms[-1] if mut == 'last' else ms[0])
    if mut == 'ge':
        return int(f >= 0)
    if mut == 'unsigned':
        return int((f & 0xFFFFFFFF) > 0)
    if mut == 'short':
        return int(((f + 0x8000) & 0xFFFF) - 0x8000 > 0)
    if mut == 'any':
        return int(any(fz(w, m) > 0 for m in ms))
    return int(f > 0)


def r_b5(w, ref, b, mut=None):
    ms = group(w, ref, b[1])
    if not ms:
        return 0
    f = fz(w, ms[-1] if mut == 'last' else ms[0])
    if mut == 'le':
        return int(f <= 0)
    if mut == 'unsigned':
        return int((f & 0xFFFFFFFF) < 0)
    if mut == 'short':
        return int(((f + 0x8000) & 0xFFFF) - 0x8000 < 0)
    if mut == 'any':
        return int(any(fz(w, m) < 0 for m in ms))
    if mut == 'ge_label':          # the wrong LABEL of the table (">= 0") ported as code
        return int(f >= 0)
    return int(f < 0)


def r_b6(w, ref, b, mut=None):
    ms = group(w, ref, b[1])
    if not ms:
        return 0 if mut != 'empty_true' else 1
    f = fz(w, ms[-1] if mut == 'last' else ms[0])
    if mut == 'ne_label':          # the wrong LABEL of the table ("!= 0") ported as code
        return int(f != 0)
    if mut == 'short':
        return int(((f + 0x8000) & 0xFFFF) - 0x8000 == 0)
    if mut == 'any':
        return int(any(fz(w, m) == 0 for m in ms))
    return int(f == 0)


def r_b7(w, ref, b, mut=None):
    t, v = b[1], b[2]
    if mut == 'swapped':
        t, v = v, t
    ms = group(w, ref, t)
    if mut == 'first_only':
        ms = ms[:1]
    for m in ms:
        ta = w.e[m]['tanim']
        if mut == 'low_byte':
            ok = (ta & 0xFF) == v
        elif mut == 'sign_extended':
            ok = ta == (((v ^ 0x80) - 0x80) & 0xFFFFFFFF)
        else:
            ok = ta == v
        if ok:
            return 1
    return 0


def r_9e(w, ref, b, mut=None):
    x1, y1, ww, hh, mn = b[1], b[2], b[3], b[4], b[5]
    if mut == 'swap_xy':
        x1, y1 = y1, x1
    if mut == 'swap_wh':
        ww, hh = hh, ww
    mask = 4 if mut == 'mask4' else 2
    cnt = 0
    for r in range(hh):
        for c in range(ww):
            col = x1 if mut == 'no_x_advance' else x1 + c
            v = w.cells.get((y1 + r, col), 0)
            if mut == 'no_wrap':
                if x1 + c >= g.GRID_W:
                    continue
            if mut == 'high_byte':
                v = (v >> 8) | (v & 0xFF)
            if v & mask:
                cnt += 1
    if mut == 'gt':
        return int(cnt > mn)
    return int(cnt >= mn)


RULES = {0xA9: r_a9, 0xAA: r_aa, 0xB2: r_b2, 0xB4: r_b4, 0xB5: r_b5, 0xB6: r_b6, 0xB7: r_b7, 0x9E: r_9e}
MUTANTS = {
    0xA9: ['swapped', 'first_only', 'hero_ref'],
    0xAA: ['swapped', 'first_only', 'hero_ref'],
    0xB2: ['g1_only', 'self_intersect', 'all_not_any', 'by_ref_id', 'second_uses_first_type', 'first_of_each', 'only_last_of_g2'],
    0xB4: ['ge', 'unsigned', 'short', 'last', 'any'],
    0xB5: ['le', 'unsigned', 'short', 'last', 'any', 'ge_label'],
    0xB6: ['ne_label', 'short', 'last', 'any', 'empty_true'],
    0xB7: ['swapped', 'low_byte', 'sign_extended', 'first_only'],
    0x9E: ['no_x_advance', 'mask4', 'high_byte', 'gt', 'swap_xy', 'swap_wh', 'no_wrap'],
}


def main():
    vdir = sys.argv[1]
    recs = [json.loads(l) for l in open(os.path.join(vdir, 'sm3-vectors.jsonl'), encoding='utf-8')]
    specs = {s['id']: s for s in g.hand_vectors() + g.sweep_vectors() + g.random_vectors()}
    report = {}
    for r in recs:
        op = int(r['op'], 16)
        spec = specs[r['id']]
        w = ctx(spec)
        b = [int(x, 16) for x in r['instr']] + [0] * 6
        base = RULES[op](w, r['ref'], b)
        assert base == r['expect']['result'], ('reference rule disagrees with the binary', r['id'], base, r['expect'])
        for mut in MUTANTS[op] + ['keep_stale_on_false', 'keep_stale_on_true']:
            v = RULES[op](w, r['ref'], b) if mut.startswith('keep_stale') else RULES[op](w, r['ref'], b, mut)
            if mut == 'keep_stale_on_false' and v == 0:
                v = r['result_before']
            if mut == 'keep_stale_on_true' and v == 1:
                v = r['result_before']
            if v != r['expect']['result']:
                kind = 'hand' if not (r['id'].startswith('SWEEP') or r['id'].startswith('RAND')) else 'net'
                report.setdefault('0x%02X' % op, {}).setdefault(mut, {'hand': [], 'net': 0})
                if kind == 'hand':
                    report['0x%02X' % op][mut]['hand'].append(r['id'])
                else:
                    report['0x%02X' % op][mut]['net'] += 1
    # a mutant with no kill at all is reported too
    for op, muts in MUTANTS.items():
        for mut in muts + ['keep_stale_on_false', 'keep_stale_on_true']:
            report.setdefault('0x%02X' % op, {}).setdefault(mut, {'hand': [], 'net': 0})
    out = {}
    for op in sorted(report):
        for mut in sorted(report[op]):
            e = report[op][mut]
            out['%s:%s' % (op, mut)] = dict(hand_killers=len(e['hand']), first_hand=e['hand'][:4], net_killers=e['net'])
    with open(os.path.join(vdir, 'mutants.json'), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(out, f, indent=1, sort_keys=True)
        f.write('\n')
    for k, v in out.items():
        print('%-26s hand=%-3d net=%-3d %s' % (k, v['hand_killers'], v['net_killers'], v['first_hand'][:2]))
    survivors = [k for k, v in out.items() if v['hand_killers'] == 0]
    print('mutants with no hand-written killer:', survivors)


if __name__ == '__main__':
    main()
