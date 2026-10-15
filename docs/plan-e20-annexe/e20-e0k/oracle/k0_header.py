"""k0_header.py - map header words read LIVE by the physics pass: +4 Gravity, +6 ZViscosity, +8 (signed byte, 'SlideEffectId' in the exports) = right shift of the horizontal force of EVERY entity (0x8003673C-0x80036760).
Maps 159 and 160 are the only maps with (Gravity 3, ZViscosity 256, byte +8 = 1).  Harness writes the three header fields of the synthetic world (W.MAPHDR); the hero and the entity run unchanged.
Run: python -B -I k0_header.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
import world as W
import interp2
from k0_arrows import OUT


def run(g, v, b8, typ, z_px, n, ticker=None):
    m = K.make_world(cells=None, hero_px=(100, 100))
    m.poke16(W.MAPHDR + 4, g); m.poke16(W.MAPHDR + 6, v); m.poke8(W.MAPHDR + 8, b8)
    hz = K.hero_z(m)
    s = K.spawn_global(m, typ, 100, 100, hz + (z_px << 16), 24)
    rows = K.run(m, n, [s], ticker=ticker)
    return m, s, rows


lines = []
for (g, v, b8, nm) in ((128, 4096, 0, 'normal maps'), (3, 256, 1, 'maps 159 / 160')):
    for typ, z_px, nn in ((3, 16, 8), (11, 32, 40)):
        a = run(g, v, b8, typ, z_px, nn)
        b = run(g, v, b8, typ, z_px, nn, ticker=interp2.tick2)
        d = K.same_rows(a[2], b[2])
        m, s, rows = a
        lines.append('%s (G %d, V %d, byte+8 %d), type %d from hero Z+%d px: ForceX %s ; X %s ; ForceZ %s ; PosZ(px) %s ; first ground tick %s ; interp diff %s' % (
            nm, g, v, b8, typ, z_px, [r['slots'][s]['fx_C4'] for r in rows[:4]], [round(r['slots'][s]['px'] / 65536, 2) for r in rows[:4]],
            [r['slots'][s]['fz_B8'] for r in rows[:12:2]], [round(r['slots'][s]['pz'] / 65536, 2) for r in rows[:12:2]],
            next((r['tick'] for r in rows if r['slots'][s]['ground_144']), None), d))
txt = '\n'.join(lines)
open(os.path.join(OUT, 'header-summary.txt'), 'w').write(txt + '\n')
print(txt)
