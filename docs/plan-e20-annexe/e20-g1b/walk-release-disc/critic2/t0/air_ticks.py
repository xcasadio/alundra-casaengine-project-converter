"""air_ticks.py - E20.g1b T0 (b1): the oracle (REAL code of ALUN_CD.EXE, France) of the contact in the air, tick by tick, WITHOUT Square: the script of g15 a)
(e20-g1a/work/orc/g15_misc.py: crate at (130, 100), hero from (100, 100) facing east, RIGHT for 3 ticks, RIGHT+CROSS at t4, RIGHT afterwards), plus the
three scripts of g15 a) with their Square edge (t6, t7, t8) for the check against out/g15-misc.txt. Read-only on the repository: it needs a COPY of
e20-g1a/critic-scenarios (gl.py, k0lib.py, ana.py, oracle/) as ./cs next to it, or the path in the environment variable G1B_CS.
Run: python -B -I air_ticks.py > out/air-ticks.txt"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
CS = os.environ.get('G1B_CS') or os.path.join(HERE, 'cs')
sys.path.insert(0, CS)
sys.path.insert(0, os.path.join(CS, 'oracle'))
sys.path.insert(0, r"C:/Users/casad/AppData/Roaming/Python/Python314/site-packages")
import gl


def run(edge):
    m = gl.world()
    c = gl.add_crate(m, 130, 100)
    if edge is None:
        seq = [(3, gl.RIGHT), (1, gl.RIGHT | gl.CROSS), (14, gl.RIGHT)]
    else:
        seq = [(3, gl.RIGHT), (1, gl.RIGHT | gl.CROSS), (edge - 5, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (14, gl.RIGHT)]
    pads, n = gl.pad_script(seq)
    return gl.run(m, n, pads, crates=[c])


def main():
    for edge in (None, 6, 7, 8):
        print('== air, Square edge: %s' % ('none' if edge is None else 't%d' % edge))
        for r in run(edge)[:14]:
            h = r['hero']
            print('t%02d hold=0x%04X x=%.4f z=%.1f xcoll=%s anim=%d carried2C=%X' % (r['tick'], r['hold'], h['px'] / 65536, h['pz'] / 65536, 'crate' if h['xcoll'] else '-', h['anim_t'], h['carried2C']))


if __name__ == '__main__':
    main()
