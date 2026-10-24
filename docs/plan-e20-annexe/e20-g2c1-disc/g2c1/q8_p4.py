"""q8_p4.py - P4 (Q-G2C-9): tick-by-tick position, ground flag and support (+0x12C, RidingEntity) of an object thrown east onto a plate (sprite 108 of map 27, 48x32x16, MoreFlags 0xC0, flags6C 0x60C0)
centered at x=178 (y=100), hero (109,100), Square+Right at T=61. REAL code (c4_scen harness: add_plate). Run: python -B -I -X utf8 q8_p4.py"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import c4_scen as S
import gl
import k0lib as K

for name in ('tonneau34', 'etoile107'):
    mapname, sprite, extra = S.OBJ[name]
    keep = {S.C4} | set(extra)
    S.widen(mapname, sprite)
    m = gl.world(keep=keep)
    c = gl.add_crate(m, 130, 100, mapname=mapname, sprite=sprite)
    p = S.add_plate(m, 178, 100)
    pe = K.ent(p)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (50, 0)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[c, p])
    print('== %s on the plate (X 178, Y 100) : plate pos (%.1f,%.1f,%.2f), plate depth(+0x1F8) %.2f, width/height words' % (name, rows[0]['c'][p]['px'] / 65536, rows[0]['c'][p]['py'] / 65536, rows[0]['c'][p]['pz'] / 65536, m.peeks(pe + 0x1F8) / 65536))
    for t in range(76, 90):
        d = rows[t - 1]['c'][c]
        print('   t%03d (T%+d) x=%.2f z=%.2f ground=%d adj=%d collz=%d support==plate:%s support=%X status=%d anim=%d' % (t, t - 61, d['px'] / 65536, d['pz'] / 65536, d['ground'], d['adj'], d['collz'], d['support'] == pe, d['support'], d['status'], d['anim_t']))
    d = rows[-1]['c'][c]
    print('   end t%d x=%.2f z=%.2f ground=%d support==plate:%s' % (nn, d['px'] / 65536, d['pz'] / 65536, d['ground'], d['support'] == pe))
