"""g5_platform.py - S5: the hero (carrying a crate or not) on a moving platform.
Platform = a Beannoid of map 14 (sprite 79) spawned by the REAL record spawner, made Collidable by the harness (+0x6C |= 0x80, the Beannoid header has no 0x80) and walking east by the harness (anim 7), exactly as the
E20.e0k discovery (k0_carry.py F2).  At t=TELE the harness writes the hero's X, Y, Z so that it stands on the platform's top (documented harness write); the crate was lifted for real at t11.
Run: python -B -I g5_platform.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana
import k0lib as K
from machine import M32

TELE = 55


def build(carry, pad_after=None, tele=TELE, n=110, platform_anim=7, hero_walk=None, platform_top_hint=None):
    m = gl.world(hero_px=(100, 100), direction=24)
    c = gl.add_crate(m, 130, 100, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000) if carry else None
    pl, _ = K.spawn_record_entities(m, 'map_14', 79, [(10, 11, 0)])
    p = pl[0]
    pe = K.ent(p)
    K.poke_entity(m, p, px=300 * 65536, py=100 * 65536)
    m.poke(pe + 0x6C, m.peek(pe + 0x6C) | 0x80)
    m.poke(pe + 0x1D8, (300 * 65536 + m.peeks(pe + 0x1E4)) & M32); m.poke(pe + 0x1DC, (100 * 65536 + m.peeks(pe + 0x1E8)) & M32)
    T = m.call(0x800370C4, pe)
    m.poke(pe + 0x138, T); m.poke(pe + 0x11C, T + 1); m.poke(pe + 0x1E0, T + 1)
    top = T + 1 + m.peeks(pe + 0x1F8)
    seq = []
    if carry:
        seq += [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (n - 11, 0)]
    else:
        seq += [(n, 0)]
    if hero_walk:
        # replace the pad after tele
        seq = [(tele, 0)] if not carry else [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (tele - 11, 0)]
        seq += hero_walk
    pads, nn = gl.pad_script(seq)

    def pre(m_, t):
        if t == tele:
            K.poke_entity(m_, p, anim_t=platform_anim, dir_t=24)
            hero = gl.HERO
            m_.poke(hero + 0x114, (m_.peek(pe + 0x114)) & M32)
            m_.poke(hero + 0x118, (m_.peek(pe + 0x118)) & M32)
            m_.poke(hero + 0x11C, (top + 1) & M32)
            if carry:                       # harness: teleport the carried crate with the hero (its offset (0,0,32 px) is preserved; otherwise UpdatePlayerCarriedEntity re-derives the offset from the crate's old position)
                ce = K.ent(c)
                m_.poke(ce + 0x114, m_.peek(pe + 0x114)); m_.poke(ce + 0x118, m_.peek(pe + 0x118)); m_.poke(ce + 0x11C, (top + 1 + 0x200000) & M32)
    rows = gl.run(m, nn, pads, crates=[c] if carry else [], extra=[p], pre=pre)
    return m, c, p, top, rows


if __name__ == '__main__':
    out = []
    for carry in (False, True):
        m, c, p, top, rows = build(carry)
        sup = [(r['tick'], r['hero']['support'] == gl.K.ent(p)) for r in rows if r['tick'] >= TELE]
        dx = [round((r['hero']['px'] - r['c'][p]['px']) / 65536, 3) for r in rows if TELE <= r['tick'] <= TELE + 40]
        cx = [round((r['c'][c]['px'] - r['hero']['px']) / 65536, 3) for r in rows if TELE <= r['tick'] <= TELE + 40] if carry else None
        hz = sorted(set(round(r['hero']['pz'] / 65536, 3) for r in rows if r['tick'] >= TELE))
        txt = gl.table(rows, [c] if carry else [], first=TELE - 2, last=TELE + 14, hcols=['anim_t', 'carried2C', 'off38', 'px', 'pz', 'fx', 'fz', 'ground', 'adj', 'support', 'floor'],
                       ccols=['px', 'pz', 'status', 'support', 'ground'], title='RIDE carry=%s platform top=%.2f' % (carry, top / 65536))
        open(os.path.join(gl.OUT, 'g5-ride-carry%s.txt' % carry), 'w').write(txt + '\n')
        out.append('carry=%s: hero.support==platform from t%s ; hero X - platform X over 40 ticks %s ; hero Z values %s ; crate X - hero X %s ; platform X end %.2f hero X end %.2f' % (
            carry, next((t for t, ok in sup if ok), None), sorted(set(dx)), hz, sorted(set(cx)) if cx else None, rows[-1]['c'][p]['px'] / 65536, rows[-1]['hero']['px'] / 65536))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g5-ride-summary.txt'), 'w').write('\n'.join(out) + '\n')
