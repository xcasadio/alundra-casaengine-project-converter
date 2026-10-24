"""g1_lift.py - S1: lift a crate (map 392 sprite 36) with the REAL MovePlayer (Square edge while blocked by it), walk with it, put it down/throw.
Run: python -B -I g1_lift.py [tag]"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl

HC = ['anim_t', 'anim_c', 'carried2C', 'off30', 'off34', 'off38', 'px', 'pz', 'fx', 'ground', 'xcoll', 'support']
CC = ['t24', 'carrier28', 'anim_t', 'anim_c', 'flags6C', 'px', 'pz', 'ground', 'adj', 'status']


def lift_walk(n=70, hero_px=(100, 100), crate_px=(130, 100), seq=None, cells=None, direction=24, jar=None):
    m = gl.world(cells=cells, hero_px=hero_px, direction=direction)
    c = gl.add_crate(m, *crate_px)
    seq = seq or [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (n - 11, gl.RIGHT)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[c])
    return m, c, rows


if __name__ == '__main__':
    tag = sys.argv[1] if len(sys.argv) > 1 else 'lift'
    m, c, rows = lift_walk(70)
    txt = gl.table(rows, [c], hcols=HC, ccols=CC, title='lift then walk east')
    open(os.path.join(gl.OUT, 'g1-%s.txt' % tag), 'w').write(txt + '\n')
    print(txt.replace('flags6C=', 'f=')[:12000])
