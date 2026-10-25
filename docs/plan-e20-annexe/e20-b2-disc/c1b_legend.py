"""c1b_legend.py - replay the E20.c1b damage oracle (copy in t2-proj/c1b) with the inventories 'legend' (kit + item 8) and 'legend+holy' (kit + Elnard + item 8): K1 (equipment records, hero sheet)
and K2 (the natural hit of the hero on a Beannoid of 100 and of 9 life). Real code of ALUN_CD.EXE on the c1 world; nothing is written outside t2-proj."""
import os
import sys

HERE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'c1b')
sys.path.insert(0, HERE)
import kworld as K
import k1
import k2

K.INVENTORIES.append(('legend', (1, 17, 25, 8), 1, None, 'EXTRA t2: item 8 (Objet 008) owned and equipped (slot 1, rank 4), kit otherwise'))
K.INVENTORIES.append(('legend_holy', (1, 17, 25, 4, 8), 1, None, 'EXTRA t2: Elnard (4) and item 8 owned, slot 1, kit otherwise'))
K.INV.update({n: dict(name=n, owned=o, weapon=w, item=i, desc=d) for n, o, w, i, d in K.INVENTORIES[-2:]})

for name in ('legend', 'legend_holy'):
    inv = K.INV[name]
    r, m = k1.case(inv)
    print('\n'.join(k1.fmt_case(r)) if isinstance(k1.fmt_case(r), list) else k1.fmt_case(r))
    lines, results, ok = k2.run_inventory(inv)
    print('\n'.join(lines))
    print(name, 'K2 ok =', ok)
