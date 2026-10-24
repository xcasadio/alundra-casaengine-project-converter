"""g17_ladder.py - S17: the slope-6 ladder/climb entry at 0x80031D20 refuses to start when hero.+0x2C != 0 (explicit test at 0x80031D54).
Cells (harness): rows 6-7, cols 2..6 = ground property 0x0C (slope kind 6 under all four hero corners, height 0); row 5 cols 2..6 = height byte 3 (48 px) without walkability bits (the climbable face; 0x8003A9E0 reads the
highest non-blocking cell 1 px north of the footprint).  The hero (x=100,y=108) presses UP: unladen vs carrying a crate lifted at the west (x=70).  Climb states: 0x0E (up) / 0x35 (down).
Run: python -B -I g17_ladder.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana

def cells(col, row):
    if 2 <= col <= 6 and row in (6, 7):
        return dict(gp=0x0C)
    if 2 <= col <= 6 and row == 5:
        return dict(h=3)
    return None

if __name__ == '__main__':
    out = []
    for carry in (False, True):
        m = gl.world(cells=cells, hero_px=(100, 108), direction=16)
        c = gl.add_crate(m, 70, 108) if carry else None
        seq = ([(14, gl.LEFT), (1, gl.LEFT | gl.SQUARE), (45, 0)] if carry else [(60, 0)]) + [(40, gl.UP)]
        pads, n = gl.pad_script(seq)
        rows = gl.run(m, n, pads, crates=[c] if carry else [])
        txt = gl.table(rows, [c] if carry else [], first=58, last=100, every=2, hcols=['anim_t', 'carried2C', 'px', 'py', 'pz', 'fz', 'slope', 'adj', 'ground'], ccols=['pz'], title='LADDER carry=%s' % carry)
        open(os.path.join(gl.OUT, 'g17-ladder-carry%s.txt' % carry), 'w').write(txt + '\n')
        out.append('carry=%s: slope seen %s ; hero anim changes after t60 %s ; hero Z max %.2f ; y end %.2f' % (carry, sorted(set(r['hero']['slope'] for r in rows)), ana.changes(rows, 'hero', ['anim_t'], first=58)[:6], max(r['hero']['pz'] for r in rows) / 65536, rows[-1]['hero']['py'] / 65536))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g17-ladder.txt'), 'w').write('\n'.join(out) + '\n')
