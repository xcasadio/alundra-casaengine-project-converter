"""summary.py - bin agent: computes the key wave facts from runs of the oracle (nothing typed by hand).  usage: python -B summary.py > ../out/summary.txt"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from wave_run import run_wave, RELEASE

def facts(sword, direction):
    res = run_wave('s', sword=sword, direction=direction, n=150)
    rows = res['rows']
    life = [(r['tick'], r['slots'][1]) for r in rows if r['slots'].get(1)]
    S = life[0][0]
    out = ['%s %s: release tick R=%d; wave first seen S=%d (R+%d); status-4 tick %d (S+%d); first absent tick %d (S+%d)' % (
        sword, direction, RELEASE, S, S - RELEASE, [t for t, d in life if d['status'] == 4][0], [t for t, d in life if d['status'] == 4][0] - S, life[-1][0] + 1, life[-1][0] + 1 - S)]
    pos = set((d['px'], d['py'], d['pz']) for t, d in life)
    forces = set((d['fx'], d['fy'], d['fz']) for t, d in life)
    out.append('  positions seen (x,y,rawZ px/65536): %s ; forces seen: %s ; ground_144 values %s ; ForceAdjusted values %s' % (
        sorted((a / 65536, b / 65536, c) for a, b, c in pos), sorted(forces), sorted(set(d['ground_144'] for t, d in life)), sorted(set(d['adj_13C'] for t, d in life))))
    out.append('  flags6C %s ; attack (attr,power) %s ; type68 %s ; dir_t %s' % (sorted(set('%X' % d['flags6C'] for t, d in life)), sorted(set((d['atk_attr'], d['atk_pow']) for t, d in life)), sorted(set(d['type68'] for t, d in life)), sorted(set(d['dir_t'] for t, d in life))))
    hb = []
    prev = None
    for t, d in life:
        key = (d['hbx'], d['hby'], d['hbz'], d['bw'], d['bd'], d['bh'])
        if key != prev:
            hb.append('S+%d: box (%.0f,%.0f) z%.5f size (%.5f,%.5f,%.5f)' % (t - S, d['hbx'] / 65536, d['hby'] / 65536, d['hbz'] / 65536, (d['bw'] + 1) / 65536, (d['bd'] + 1) / 65536, (d['bh'] + 1) / 65536))
            prev = key
    out.append('  HitBox changes: ' + ' | '.join(hb))
    rst = [t - S for t, d in life if d['reset_AC']][0]
    out.append('  ForceResetAnimationFlag (+0xAC) first 1 at S+%d ; frame counter +0x1C at S: %d, at S+25: %d' % (rst, life[0][1]['cnt1c'], [d['cnt1c'] for t, d in life if t - S == 25][0]))
    snd = [(r['tick'] - S, j) for r in rows for j in r['journal'] if j.startswith('snd') and r['tick'] >= S]
    out.append('  sounds journalled from S: %s' % snd)
    hand = [(r['tick'] - S, j) for r in rows for j in r['journal'] if j.startswith('stub') and r['tick'] >= S - 1 and '@' in j and 'slot1' in j]
    out.append('  inert-handler stubs for slot 1 (letter@addr): %s' % hand[:6])
    return out

for sw, d in (('steel', 'east'), ('steel', 'north'), ('steel', 'west'), ('steel', 'south'), ('demon', 'east'), ('holy', 'east')):
    print('\n'.join(facts(sw, d)))
