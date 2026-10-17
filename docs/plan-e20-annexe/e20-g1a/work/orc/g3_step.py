"""g3_step.py - S3: walk into a step (cells height byte 1..3 = 16/32/48 px) with and without a crate in the hands; jump onto it carrying.
The crate starts west of the hero (x=70); the hero walks west into it (Square edge), then east.  Cells: height byte hb on columns 6..9 (x 144..239), flat elsewhere (harness).
Run: python -B -I g3_step.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana

STEP_COLS = (6, 9)


def cells_step(hb):
    return lambda col, row: dict(h=hb) if STEP_COLS[0] <= col <= STEP_COLS[1] else None


def scenario(hb, carry, jump_tick=None, n=170, walk_east_from=60, crate_px=(70, 100), hero_px=(100, 100)):
    m = gl.world(cells=cells_step(hb), hero_px=hero_px, direction=24)
    c = gl.add_crate(m, *crate_px)
    seq = []
    if carry:
        seq += [(14, gl.LEFT), (1, gl.LEFT | gl.SQUARE), (walk_east_from - 15, 0)]
    else:
        seq += [(walk_east_from, 0)]
    if jump_tick is None:
        seq += [(n - walk_east_from, gl.RIGHT)]
    else:
        seq += [(jump_tick - walk_east_from, gl.RIGHT), (1, gl.RIGHT | gl.CROSS), (n - jump_tick - 1, gl.RIGHT)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[c])
    return m, c, rows


def stats(rows, c, first):
    xs = [r['hero']['px'] / 65536 for r in rows if r['tick'] >= first]
    zs = [r['hero']['pz'] / 65536 for r in rows if r['tick'] >= first]
    return 'final hero X %.3f Z %.3f ; max Z %.2f ; blocked(adj) ticks %d ; anim changes %s ; crate Z-hero Z at end %s' % (
        xs[-1], zs[-1], max(zs), sum(1 for r in rows if r['tick'] >= first and r['hero']['adj']),
        ana.changes(rows, 'hero', ['anim_t'], first=first)[:14], (rows[-1]['c'][c]['pz'] - rows[-1]['hero']['pz']) / 65536)


if __name__ == '__main__':
    out = []
    for hb in (1, 2, 3):
        for carry in (False, True):
            m, c, rows = scenario(hb, carry)
            txt = gl.table(rows, [c], first=58, last=170, every=2, hcols=['anim_t', 'carried2C', 'off38', 'px', 'pz', 'fx', 'fz', 'ground', 'adj', 'xcoll'], ccols=['px', 'pz', 'status'], title='WALK into step hb=%d carry=%s' % (hb, carry))
            open(os.path.join(gl.OUT, 'g3-walk-step%d-carry%s.txt' % (hb, carry)), 'w').write(txt + '\n')
            out.append('WALK hb=%d carry=%s: %s' % (hb, carry, stats(rows, c, 60)))
    for hb in (1, 2):
        for carry in (False, True):
            for jt in (86, 92):      # jump from x ~ 130..140 (step edge at x=144)
                m, c, rows = scenario(hb, carry, jump_tick=jt)
                txt = gl.table(rows, [c], first=jt - 3, last=170, hcols=['anim_t', 'carried2C', 'off38', 'px', 'pz', 'fx', 'fz', 'ground', 'adj', 'xcoll', 'terr'], ccols=['px', 'pz', 'status'], title='JUMP onto step hb=%d carry=%s jump_tick=%d' % (hb, carry, jt))
                open(os.path.join(gl.OUT, 'g3-jump-step%d-carry%s-t%d.txt' % (hb, carry, jt)), 'w').write(txt + '\n')
                hx = [round(r['hero']['px'] / 65536, 1) for r in rows if r['tick'] == jt]
                out.append('JUMP hb=%d carry=%s at t%d (hero X at jump %s): %s' % (hb, carry, jt, hx, stats(rows, c, jt)))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g3-step-summary.txt'), 'w').write('\n'.join(out) + '\n')
