"""c2_platform.py - CRITIC: the gap of g5 (the hero was TELEPORTED onto the platform).  Here the hero (carrying a crate lifted for real at t11, or not) WALKS and JUMPS onto a collidable platform
(a Beannoid of map 14 made Collidable by the harness, exactly as g5/k0_carry F2), stationary or walking toward him, then stays on it.  Also: walks into the stationary platform without jumping.
Harness writes: the platform placement/collidable bit/animation (as g5), the pad.  No teleport of the hero.
Run: python -B -I c2_platform.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl
import ana
import k0lib as K
from machine import M32

P = 65536


def build(carry, jump_tick, plat_x=300, plat_anim=0, plat_dir=8, plat_start=1, n=150, walk_from=60, hold_after=None):
    m = gl.world(hero_px=(109 if not carry else 100, 100), direction=24)
    c = gl.add_crate(m, 130, 100) if carry else None
    pl, _ = K.spawn_record_entities(m, 'map_14', 79, [(10, 11, 0)], **({} if not carry else dict(rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)))
    p = pl[0]
    pe = K.ent(p)
    K.poke_entity(m, p, px=plat_x * P, py=100 * P)
    m.poke(pe + 0x6C, m.peek(pe + 0x6C) | 0x80)
    m.poke(pe + 0x1D8, (plat_x * P + m.peeks(pe + 0x1E4)) & M32); m.poke(pe + 0x1DC, (100 * P + m.peeks(pe + 0x1E8)) & M32)
    T = m.call(0x800370C4, pe)
    m.poke(pe + 0x138, T); m.poke(pe + 0x11C, T + 1); m.poke(pe + 0x1E0, T + 1)
    top = (T + 1 + m.peeks(pe + 0x1F8))
    seq = []
    if carry:
        seq += [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (walk_from - 11, 0)]
    else:
        seq += [(walk_from, 0)]
    seq += [(jump_tick - walk_from, gl.RIGHT), (1, gl.RIGHT | gl.CROSS), (n - jump_tick - 1, hold_after if hold_after is not None else gl.RIGHT)]
    pads, nn = gl.pad_script(seq)

    def pre(m_, t):
        if t == plat_start and plat_anim:
            K.poke_entity(m_, p, anim_t=plat_anim, dir_t=plat_dir)

    rows = gl.run(m, nn, pads, crates=[c] if carry else [], extra=[p], pre=pre)
    return m, c, p, top, rows


def summarize(carry, jt, rows, c, p):
    pe = gl.K.ent(p)
    sup = [r['tick'] for r in rows if r['tick'] > jt and r['hero']['support'] == pe]
    first = sup[0] if sup else None
    last = sup[-1] if sup else None
    dxs = sorted(set(round((r['hero']['px'] - r['c'][p]['px']) / P, 2) for r in rows if first and first + 3 <= r['tick'] <= (last or 0)))
    hz = sorted(set(round(r['hero']['pz'] / P, 2) for r in rows if first and first + 3 <= r['tick'] <= (last or 0)))
    cz = sorted(set(round((r['c'][c]['pz'] - r['hero']['pz']) / P, 3) for r in rows if first and first + 3 <= r['tick'] <= (last or 0))) if carry else None
    cx = sorted(set(round((r['c'][c]['px'] - r['hero']['px']) / P, 3) for r in rows if first and first + 3 <= r['tick'] <= (last or 0))) if carry else None
    return 'carry=%s jump t%d: support==platform ticks %s..%s (%d) ; hero-platform X offsets %s ; hero Z %s ; crate-hero Z %s ; crate-hero X %s ; hero X end %.1f platform X end %.1f' % (
        carry, jt, first, last, len(sup), dxs[:4], hz[:3], cz, cx, rows[-1]['hero']['px'] / P, rows[-1]['c'][p]['px'] / P)


def main():
    out = []
    for (label, plat_anim, plat_x, jts) in (('STATIONARY platform at x=300', 0, 300, tuple(range(100, 128, 3))), ('platform WALKING WEST (anim 7) from x=420', 7, 420, tuple(range(76, 100, 3)))):
        out.append('== ' + label)
        for carry in (False, True):
            for jt in jts:
                m, c, p, top, rows = build(carry, jt, plat_x=plat_x, plat_anim=plat_anim, n=jt + 70, hold_after=0)
                out.append('  [top %.2f px] ' % ((top - 0) / P) + summarize(carry, jt, rows, c, p))
    # control: the walk into the stationary platform without jumping
    for carry in (False, True):
        m, c, p, top, rows = build(carry, 149, plat_x=300, plat_anim=0, n=150)
        out.append('walk into the stationary platform, no jump, carry=%s: hero X end %.2f, Z %.2f, blocked(adj) ticks %d' % (carry, rows[-1]['hero']['px'] / P, rows[-1]['hero']['pz'] / P, sum(1 for r in rows if r['hero']['adj'])))
    txt = '\n'.join(out)
    print(txt)
    open(os.path.join(gl.OUT, 'c2-platform.txt'), 'w').write(txt + '\n')


if __name__ == '__main__':
    main()
