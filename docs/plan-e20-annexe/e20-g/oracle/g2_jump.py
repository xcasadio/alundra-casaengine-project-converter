"""g2_jump.py - S2: the hero jumps while carrying (Cross edge in the carry states) vs unladen, still and moving.
Run: python -B -I g2_jump.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana


def scenario(carry=True, jump_at=60, jump_dir=0, n=130, cells=None, hero_px=(100, 100), crate_px=(130, 100), jump_len=1, hold_after=None, extra_pre=None):
    m = gl.world(cells=cells, hero_px=hero_px, direction=24)
    c = gl.add_crate(m, *crate_px)
    if carry:
        seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (jump_at - 11, 0)]
    else:
        seq = [(jump_at, 0)]
    seq += [(jump_len, jump_dir | gl.CROSS), (n - jump_at - jump_len, jump_dir if hold_after is None else hold_after)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[c], pre=extra_pre)
    return m, c, rows


def summarize(tag, rows, c, jump_at=60):
    ch = ana.changes(rows, 'hero', ['anim_t'], first=jump_at - 1)
    ap = ana.apex(rows, 'hero', first=jump_at)
    land = ana.first_tick(rows, lambda r: r['hero']['ground'] == 1 and r['tick'] > jump_at + 2, first=jump_at + 2)
    hx = [round(r['hero']['px'] / 65536, 3) for r in rows[jump_at - 1:jump_at + 30:3]]
    cz = ana.apex(rows, c, first=jump_at)
    s = '%s: hero anim changes %s ; apex(tick, px) %s ; landing tick %s ; hero X every 3 ticks %s ; crate pz apex %s ; crate-hero dZ at apex %s' % (
        tag, ana.changes(rows, 'hero', ['anim_t'], first=jump_at - 1), ap, land, hx, cz,
        (rows[ap[0] - 1]['c'][c]['pz'] - rows[ap[0] - 1]['hero']['pz']) / 65536)
    return s


if __name__ == '__main__':
    out = []
    for carry in (False, True):
        for jd, nm in ((0, 'still'), (gl.RIGHT, 'moving-east')):
            m, c, rows = scenario(carry=carry, jump_dir=jd, n=110, hold_after=jd)
            txt = gl.table(rows, [c], first=58, last=110, hcols=['anim_t', 'anim_c', 'off38', 'px', 'pz', 'fx', 'fz', 'izf', 'ground', 'collz', 'adj'], ccols=['px', 'pz', 'status', 'ground'], title='JUMP %s carry=%s' % (nm, carry))
            open(os.path.join(gl.OUT, 'g2-jump-%s-carry%s.txt' % (nm, carry)), 'w').write(txt + '\n')
            out.append(summarize('%s carry=%s' % (nm, carry), rows, c))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g2-jump-summary.txt'), 'w').write('\n'.join(out) + '\n')
