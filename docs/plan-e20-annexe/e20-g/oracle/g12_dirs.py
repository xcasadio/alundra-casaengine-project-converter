"""g12_dirs.py - S12: throw direction.  The hero (carrying) walks 4 ticks in direction D1, then throws while holding D2; report hero.+0x98 (adir), crate +0x8C and the first flight step.
Run: python -B -I g12_dirs.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana

DIRS = [('none', 0), ('E', gl.RIGHT), ('W', gl.LEFT), ('N', gl.UP), ('S', gl.DOWN), ('NE', gl.UP | gl.RIGHT), ('SE', gl.DOWN | gl.RIGHT), ('NW', gl.UP | gl.LEFT), ('SW', gl.DOWN | gl.LEFT)]

if __name__ == '__main__':
    out = []
    for face_nm, face in (('E', gl.RIGHT), ('N', gl.UP), ('W', gl.LEFT), ('S', gl.DOWN), ('NE', gl.UP | gl.RIGHT)):
        for nm, hold in DIRS:
            m = gl.world(hero_px=(200, 200)); c = gl.add_crate(m, 230, 200)
            seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (4, face), (1, hold | gl.SQUARE), (6, 0)]
            pads, n = gl.pad_script(seq)
            rows = gl.run(m, n, pads, crates=[c])
            t = 65
            r = rows[t - 1]
            d = r['c'][c]
            h0, h1 = rows[t - 2]['hero'], r['hero']
            out.append('face %-2s then throw holding %-4s: hero adir %d dir_t %2d ; crate t24 %d dir_t %2d anim %d ; first step (dx,dy)=(%.3f,%.3f) px/tick ; hero moved (%.2f,%.2f)' % (
                face_nm, nm, r['hero']['adir'], r['hero']['dir_t'], d['t24'], d['dir_t'], d['anim_t'], d['fx'] / 65536, d['fy'] / 65536, (h1['px'] - h0['px']) / 65536, (h1['py'] - h0['py']) / 65536))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g12-dirs.txt'), 'w').write('\n'.join(out) + '\n')
