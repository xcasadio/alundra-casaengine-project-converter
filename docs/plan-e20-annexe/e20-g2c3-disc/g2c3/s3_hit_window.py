"""s3_hit_window.py - read-only replay: the XY window in which a falling drop extinguishes the hero's carried torch.  Same montage as s2 'carry' (REAL MovePlayer lifts the REAL torch; REAL A19 + C71),
the drop is placed at (hero.x + dx, hero.y + dy) for a sweep of dx and dy; reports for each offset the Z the drop stops at in its fall (32 = the hero's head, 0 = the ground), whether the torch was extinguished (torch status 3 with +0x274 = 1)
and the tick of the torch's destruction.   Run: python -B -I -X utf8 s3_hit_window.py"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'replay'))
sys.path.insert(0, os.path.join(HERE, 'replay', 'work', 'orc'))
import k0lib as K
from s2_drop_on_torch import run
from s1_drop_cycle import rd8


def probe(dx, dy):
    m, t, d, rows, log, pos = run('carry', 80, dropx=109 + dx, dropy=100 + dy)
    landed_z = None
    for r in log:
        D = r['D']
        if D and D[1] == 3:             # state 3 = the tick after the contact
            landed_z = D[4]
            break
    ext = next((r['tick'] for r in log if r['T'][0] == 3 and r['T'][1] == 1), None)
    return landed_z, ext


if __name__ == '__main__':
    print('drop offset (dx, dy) from the hero centre (hero x=109, y=100; hero box see k0lib) -> (Z of the contact, tick the torch is extinguished or None)')
    res = {}
    for dx in range(-14, 15, 1):
        res[dx] = probe(dx, 0)
    line = [(dx, res[dx]) for dx in sorted(res)]
    hit = [dx for dx, (z, e) in res.items() if e is not None]
    print('dy = 0: extinguished for dx in [%s .. %s] (%d offsets): %s' % (min(hit) if hit else None, max(hit) if hit else None, len(hit), line))
    res2 = {}
    for dy in range(-14, 15, 1):
        res2[dy] = probe(0, dy)
    hit2 = [dy for dy, (z, e) in res2.items() if e is not None]
    print('dx = 0: extinguished for dy in [%s .. %s] (%d offsets): %s' % (min(hit2) if hit2 else None, max(hit2) if hit2 else None, len(hit2), [(dy, res2[dy]) for dy in sorted(res2)]))
