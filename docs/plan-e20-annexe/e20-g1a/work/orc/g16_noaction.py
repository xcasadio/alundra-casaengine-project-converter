"""g16_noaction.py - S16: ground-property gates in the carry states: hero.+0x180 & 0x2000 (cell gp bit 0x20) blocks the throw (and the charge release); & 0x4000 (gp 0x40) blocks the jump.  Also: Cross/Square on plain ground for contrast.
The cells under the hero (cols 3..5, x 72..143) get gp 0x20 or 0x40 (harness); the crate is lifted at t11 (those cells do not matter for the lift: PlayerTryAction does not read +0x180).
Run: python -B -I g16_noaction.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana

if __name__ == '__main__':
    out = []
    for nm, gp in (('plain', 0), ('gp=0x20', 0x20), ('gp=0x40', 0x40)):
        cells = (lambda col, row, gp=gp: dict(gp=gp) if 3 <= col <= 5 else None) if gp else None
        for act, pad in (('throw (Square)', gl.SQUARE), ('jump (Cross)', gl.CROSS)):
            m = gl.world(cells=cells); c = gl.add_crate(m, 130, 100)
            pads, n = gl.pad_script([(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, pad), (20, 0)])
            rows = gl.run(m, n, pads, crates=[c])
            out.append('%-8s %-15s: hero anim changes after t60 %s ; hero.+0x2C at end %X ; +0x180 at t60 %X' % (nm, act, ana.changes(rows, 'hero', ['anim_t', 'carried2C'], first=59)[:4], rows[-1]['hero']['carried2C'], rows[59]['hero']['p180']))
    # PlayerTryAction/lift under gp=0x20: is the lift refused too?
    cells = lambda col, row: dict(gp=0x20) if 3 <= col <= 5 else None
    m = gl.world(cells=cells); c = gl.add_crate(m, 130, 100)
    pads, n = gl.pad_script([(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (10, 0)])
    rows = gl.run(m, n, pads, crates=[c])
    out.append('lift on gp=0x20 cells: lifted=%s' % (rows[-1]['hero']['carried2C'] != 0))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g16-noaction.txt'), 'w').write('\n'.join(out) + '\n')
