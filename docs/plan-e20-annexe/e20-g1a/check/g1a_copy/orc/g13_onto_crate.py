"""g13_onto_crate.py - S13: the hero carrying crate A jumps onto a static crate B (16 px) and walks on it; also the unladen control.  A = west of the hero (lifted for real), B = east (static).
Run: python -B -I g13_onto_crate.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana
import k0lib as K

def run(carry, jump_tick, n=140):
    m = gl.world(hero_px=(100, 100))
    b = gl.add_crate(m, 150, 100, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    a = gl.add_crate(m, 70, 100) if carry else None
    seq = []
    if carry:
        seq += [(14, gl.LEFT), (1, gl.LEFT | gl.SQUARE), (45, 0)]
    else:
        seq += [(60, 0)]
    seq += [(jump_tick - 60, gl.RIGHT), (1, gl.RIGHT | gl.CROSS), (n - jump_tick - 1, gl.RIGHT)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[b] + ([a] if carry else []))
    return m, a, b, rows

if __name__ == '__main__':
    out = []
    for carry in (False, True):
        for jt in (66, 70, 74):
            m, a, b, rows = run(carry, jt)
            sup = ana.first_tick(rows, lambda r: r['hero']['support'] == K.ent(b), first=jt)
            zs = sorted(set(round(r['hero']['pz'] / 65536, 2) for r in rows if r['tick'] > jt + 12))
            hx = [round(r['hero']['px'] / 65536, 1) for r in rows if r['tick'] in (jt, jt + 10, jt + 20, jt + 40)]
            out.append('carry=%s jump at t%d (hero X %s): support==crate B from tick %s ; hero Z values after t%d %s ; anim changes %s' % (
                carry, jt, hx, sup, jt + 12, zs[:6], ana.changes(rows, 'hero', ['anim_t'], first=jt)[:8]))
            if carry and jt == 70:
                txt = gl.table(rows, [a, b], first=jt - 1, last=jt + 40, hcols=['anim_t', 'px', 'pz', 'fx', 'fz', 'ground', 'support', 'adj'], ccols=['px', 'pz'], title='carry onto crate B')
                open(os.path.join(gl.OUT, 'g13-onto-crate.txt'), 'w').write(txt + '\n')
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g13-summary.txt'), 'w').write('\n'.join(out) + '\n')
