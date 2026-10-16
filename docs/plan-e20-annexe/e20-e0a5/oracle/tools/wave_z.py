"""wave_z.py - bin agent, E20.f3 discovery: the sword wave's Z behaviour (no gravity bit): flat raised ground, and a ledge under its footprint.  Real code (pworld)."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
import c1world as C
from wave_run import run_wave, DIRS, RELEASE
OUT = os.path.join(HERE, '..', 'out')

def peek_fields(res, tick, slot):
    pass

def run(name, hero_px, cells, direction='east', sword='steel'):
    res = run_wave(name, sword=sword, direction=direction, n=150, cells=cells, hero_px=hero_px)
    m = res['m']
    lines = ['# %s hero_px=%s' % (name, hero_px)]
    for r in res['rows']:
        if r['tick'] in (95, 96) or 114 <= r['tick'] <= 121 or r['tick'] in (130, 142, 143, 144):
            h = r['slots'][0]
            w = r['slots'].get(1)
            ws = ('wave st=%d rawZ=%d adj13C=%d gnd144=%d terr138=%d reset=%d box=%d fl=%X' % (w['status'], w['pz'], w['adj_13C'], w['ground_144'], w['terr_138'], w['reset_AC'], 1 if w['box1d4'] else 0, w['flags6C'])) if w else 'wave absent'
            lines.append('t%03d hero rawZ=%d gnd144=%d terr138=%d pos=(%.1f,%.1f) | %s' % (r['tick'], h['pz'], h['ground_144'], h['terr_138'], h['px'] / 65536, h['py'] / 65536, ws))
    return lines, res

if __name__ == '__main__':
    out = []
    flat2 = lambda c, r: (2, 0, 0, 0)
    ledge1 = lambda c, r: (1 if c >= 5 else 0, 0, 0, 0)
    ledge1_far = lambda c, r: (1 if c >= 6 else 0, 0, 0, 0)
    for name, hp, cells in (('flat height byte 2 (32 px) everywhere', (100, 100), flat2),
                            ('ledge: columns >= 5 (x >= 120) have height byte 1, hero x=112', (112, 100), ledge1),
                            ('ledge: columns >= 5 have height byte 1, hero x=104', (104, 100), ledge1),
                            ('ledge: columns >= 6 (x >= 144) have height byte 1, hero x=130', (130, 100), ledge1_far),
                            ):
        lines, res = run(name, hp, cells)
        out += lines + ['']
        print('\n'.join(lines))
    open(os.path.join(OUT, 'wave_z.txt'), 'w').write('\n'.join(out) + '\n')
