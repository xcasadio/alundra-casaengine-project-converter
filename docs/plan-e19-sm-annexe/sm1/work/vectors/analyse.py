"""Reads the frozen vectors and prints: the result histogram per opcode, the union of fields read per opcode (the mapping to the DLL), the mutant kill table
(over all vectors, over the curated vectors only, and the first killing curated vectors), and the equivalent mutants.
Usage: python -B analyse.py VECTORS.json OUTDIR"""
import json
import os
import re
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sm1lib as L  # noqa: E402


def main():
    doc = json.load(open(sys.argv[1], encoding='utf-8'))
    outdir = sys.argv[2]
    os.makedirs(outdir, exist_ok=True)
    vs = doc['vectors']
    lines = []
    P = lines.append
    # histogram
    hist = defaultdict(Counter)
    hist_cur = defaultdict(Counter)
    for v in vs:
        hist[v['op']][v['binary']['result']] += 1
        if 'random' not in v['tags']:
            hist_cur[v['op']][v['binary']['result']] += 1
    P('== Result histogram (all / curated) ==')
    for op in sorted(hist):
        P('0x%02X all %s curated %s' % (op, dict(sorted(hist[op].items())), dict(sorted(hist_cur[op].items()))))
    # reads
    P('')
    P('== fields read, per opcode (field kind: how many vectors read it) ==')
    for op in sorted(hist):
        c = Counter()
        for v in vs:
            if v['op'] != op:
                continue
            kinds = set()
            for r in v['binary']['reads']:
                kinds.add(re.sub(r'slot\[\d+\]', 'slot[i]', re.sub(r'code\[\d+\]', 'code[k]', r)))
            for k in kinds:
                c[k] += 1
        P('0x%02X: %s' % (op, ', '.join('%s=%d' % kv for kv in sorted(c.items()))))
    # mutants
    P('')
    P('== mutants: kill counts over all vectors, over curated vectors; first killing curated vector ids ==')
    kill_rows = []
    for op_group, mutants in (((0x60, 0x61), L.MUTANTS_60_61), ((0x3C, 0x3D), L.MUTANTS_3C_3D)):
        for mut in mutants:
            killers_all = []
            killers_cur = []
            for v in vs:
                if v['op'] not in op_group:
                    continue
                for hero_in_list in (True, False):
                    mt = L.Montage(v, hero_in_list=hero_in_list, anon=v.get('anon', ()))
                    ref = L.dll_case(mt, v['op'], v['code'], None)
                    got = L.dll_case(mt, v['op'], v['code'], mut)
                    if got != ref:
                        if v['id'] not in killers_all:
                            killers_all.append(v['id'])
                            if 'random' not in v['tags']:
                                killers_cur.append(v['id'])
            kill_rows.append((mut, op_group, killers_all, killers_cur))
    # DLL-only montages (a bare proxy without identity): evaluated on dedicated worlds below
    for mut, op_group, ka, kc in kill_rows:
        P('%-30s ops %-10s all=%-4d curated=%-3d %s' % (mut, '/'.join('%02X' % o for o in op_group), len(ka), len(kc), ' '.join(kc[:6])))
    # equivalent mutants (killed by nothing)
    P('')
    P('== mutants killed by no vector (candidates for "equivalent") ==')
    for mut, op_group, ka, kc in kill_rows:
        if not ka:
            P(mut)
    open(os.path.join(outdir, 'analysis.txt'), 'w', encoding='utf-8', newline='\n').write('\n'.join(lines) + '\n')
    json.dump([dict(mutant=m, ops=['0x%02X' % o for o in og], killed_by_all=len(ka), killed_by_curated=kc, killed_by_random_only=[x for x in ka if x not in kc][:5])
               for m, og, ka, kc in kill_rows], open(os.path.join(outdir, 'mutant-kills.json'), 'w', encoding='utf-8', newline='\n'), indent=1, sort_keys=True)
    print('\n'.join(lines))


main()
