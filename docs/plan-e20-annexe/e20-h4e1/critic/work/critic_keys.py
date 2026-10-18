"""critic_keys.py - h4e1 CRITIC: the keys the discovery lacks, made with the discovery's own harness (wind1_key.run, FAITHFUL slot init + per-frame generator step), nothing else changed.
  * wind1_cliff_hb{2,8,9}_pf        : terrain of height byte HB (x16 px) for x >= 120 px (hero (100,100)): births inside terrain >= hero Z + 128 px (+1 raw) are destroyed without effect (the 'dead' branch is live)
  * wind1_cliff_hb9_gateoff_pf     : the same with the first strike kept (production default if Q-H4E1-1 keeps B)
  * wind1_full_table_gateoff_pf     : (p2) under the production default B
  * wind1_hurt{33,41,42,60,102,103}_gateoff_pf : (p4)/(w3) under the production default B (the T >= 33 interruptions: the strike of tick 32 exists)
  * wind1_cast_pf_seed2            : a second seed (0xA468A517) for the cast, to make the draws of (w1) less seed-lucky
usage: python -B critic_keys.py OUTDIR [--only NAME ...]"""
import sys
import os
import json
import argparse
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import wind1_key as K


def cliff(hb):
    return lambda col, row: (hb, 0, 0, 0) if col >= 5 else (0, 0, 0, 0)


def scen():
    S = []
    for hb in (2, 8, 9):
        S.append(('wind1_cliff_hb%d_pf' % hb, dict(ticks=140, cells=cliff(hb))))
    S.append(('wind1_cliff_hb9_gateoff_pf', dict(ticks=140, cells=cliff(9), gate_off=True)))
    S.append(('wind1_full_table_gateoff_pf', dict(ticks=140, gate_off=True, targets=[dict(x=500 + (i % 10) * 10, y=300 + (i // 10) * 10, class_a=False) for i in range(63)])))
    for T_ in (33, 41, 42, 60, 102, 103):
        S.append(('wind1_hurt%d_gateoff_pf' % T_, dict(ticks=T_ + 60, hurt=T_, gate_off=True)))
    S.append(('wind1_cast_pf_seed2', dict(ticks=140, seed=0xA468A517)))
    return S


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('outdir')
    ap.add_argument('--only', nargs='*')
    a = ap.parse_args()
    os.makedirs(a.outdir, exist_ok=True)
    for name, kw in scen():
        if a.only and name not in a.only:
            continue
        res = K.run(name, **kw)
        json.dump(res, open(os.path.join(a.outdir, name + '.json'), 'w'), sort_keys=True)
        open(os.path.join(a.outdir, name + '.txt'), 'w').write(K.summary(res))
        print(name, 'ok', flush=True)
