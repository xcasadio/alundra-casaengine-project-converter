"""w4_adj_sweep.py - walk-release-disc W4 (side observation): in the c3 scene (hero from x = 100, crate at x = 130, RIGHT held) the binary shows ForceAdjusted (+0x13C) at t07 while the DLL
probe of g1b shows it at t06 (the tick after the flush tick t05). Sweep of the start x0 = 100 + k/32 (k = 0..31): for each start, the tick of the flush (x stops growing), the first tick with a
contact, the first tick with +0x13C = 1, on the REAL code of ALUN_CD.EXE. Read-only.
Run from the copy of critic-scenarios:  python -B -I w4_adj_sweep.py
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl

R = gl.RIGHT


def run(x0):
    m = gl.world(hero_px=(100, 100), direction=24)
    m.poke(gl.HERO + 0x114, int(round(x0 * 65536)))
    c = gl.add_crate(m, 130, 100)
    pads, n = gl.pad_script([(14, R)])
    rows = gl.run(m, n, pads, crates=[c])
    xs = [r['hero']['px'] for r in rows]
    flush = next((i + 1 for i in range(1, len(xs)) if xs[i] == xs[i - 1] or (rows[i]['hero']['xcoll'] != 0 and i > 0)), None)
    contact = next((r['tick'] for r in rows if r['hero']['xcoll'] != 0), None)
    adj = next((r['tick'] for r in rows if r['hero']['adj'] == 1), None)
    last_x = xs[-1] / 65536
    return contact, adj, last_x


def main():
    print('x0        contact_tick  adj1_tick  final_x   (adj1 - contact)')
    seen = {}
    for k in range(32):
        x0 = 100 + k / 32
        contact, adj, fx = run(x0)
        d = None if contact is None or adj is None else adj - contact
        seen.setdefault(d, []).append(k)
        print('%-9.5f %-13s %-10s %-9.4f %s' % (x0, contact, adj, fx, d))
    print('histogram of (adj1 - contact):', {d: len(v) for d, v in seen.items()})


if __name__ == '__main__':
    main()
