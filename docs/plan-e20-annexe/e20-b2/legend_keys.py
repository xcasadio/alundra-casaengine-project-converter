"""legend_keys.py - E20.b2 T0 (b): the c1b damage oracle (K1, K2) with two more inventories, 'legend' (kit + item 8) and 'legend_holy' (kit + Elnard + item 8).

usage: python -I -B legend_keys.py <copy of e20-c1b/oracle/tools> <output folder>

The oracle tools are NOT changed: they are imported from a COPY of the frozen folder (docs/plan-e20-annexe/e20-c1b/oracle/tools), the two inventories are
appended here, and the real code of ALUN_CD.EXE (France) is run in the oracle's interpreter. Needs capstone on the path (the oracle reads the binary
at the path written in its lib.py). Outputs: K1-legend.txt/.json, K2-legend.txt/.json, and a control: the 'kit' and 'holy' entries replayed in the same
process equal the frozen K1.json / K2.json of the c1b annex (checked when that folder is found next to the copy; see results.md).
Output is byte-stable (no clock, no host path).
"""
import json
import os
import sys

TOOLS = os.path.abspath(sys.argv[1])
OUT = os.path.abspath(sys.argv[2])
sys.path.insert(0, TOOLS)
os.makedirs(OUT, exist_ok=True)
import kworld as K
import k1
import k2

K.use_engine('emu')
K.INVENTORIES.append(('legend', (1, 17, 25, 8), 1, None, 'EXTRA b2: item 8 (Epee legendaire) owned and equipped (slot 1, rank 4), kit otherwise'))
K.INVENTORIES.append(('legend_holy', (1, 17, 25, 4, 8), 1, None, 'EXTRA b2: Elnard (4) and item 8 owned, slot 1, kit otherwise'))
for n, o, w, i, d in K.INVENTORIES[-2:]:
    K.INV[n] = dict(name=n, owned=o, weapon=w, item=i, desc=d)


def jdump(x):
    return json.dumps(x, sort_keys=True, indent=1, default=str)


def write(name, text):
    with open(os.path.join(OUT, name), 'w', encoding='utf-8', newline='\n') as f:
        f.write(text if text.endswith('\n') else text + '\n')


names = ('legend', 'legend_holy')
k1_lines, k1_res, k2_lines, k2_res = [], {}, [], {}
ok = True
for name in names:
    r, m = k1.case(K.INV[name])
    k1_res[name] = r
    k1_lines += k1.fmt_case(r) + ['']
    ok &= r['ok']
    l, res, o = k2.run_inventory(K.INV[name])
    k2_res[name] = res
    k2_lines += l + ['']
    ok &= o
write('K1-legend.txt', '\n'.join(['# K1 with the inventories legend and legend_holy (E20.b2 T0 b): equipment records and hero sheet, real UpdateItemEffectState 0x800307E8', ''] + k1_lines))
write('K1-legend.json', jdump(k1_res))
write('K2-legend.txt', '\n'.join(['# K2 with the inventories legend and legend_holy (E20.b2 T0 b): the real ComputeNewHp 0x8003A374 on a Beannoid (record 335, level D 0)', ''] + k2_lines))
write('K2-legend.json', jdump(k2_res))

# control: the frozen inventories replayed in the same process must equal the frozen keys
FROZEN = os.path.normpath(os.path.join(TOOLS, '..', 'out'))
ctl = []
if os.path.exists(os.path.join(FROZEN, 'K1.json')):
    f1 = json.load(open(os.path.join(FROZEN, 'K1.json'), encoding='utf-8'))
    f2 = json.load(open(os.path.join(FROZEN, 'K2.json'), encoding='utf-8'))
    for name in ('kit', 'holy'):
        r, m = k1.case(K.INV[name])
        a = json.loads(jdump(r)) == f1[name]
        l, res, o = k2.run_inventory(K.INV[name])
        b = json.loads(jdump(res)) == f2[name]
        ctl.append('control %s: K1 equals frozen K1.json: %s; K2 equals frozen K2.json: %s' % (name, a, b))
        ok &= a and b
write('control.txt', '\n'.join(ctl) if ctl else 'no frozen keys found next to the tools copy')
print('\n'.join(ctl))
print('legend keys', 'PASS' if ok else 'FAIL')
sys.exit(0 if ok else 1)
