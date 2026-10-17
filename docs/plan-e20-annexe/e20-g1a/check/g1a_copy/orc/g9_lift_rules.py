"""g9_lift_rules.py - S9: when does PlayerTryAction lift?  (A) idle next to the crate, (B) Square first then walk, (C) diagonal/other directions, (D) heavy stone (global sprite 234, kind 3) with/without item 0x3B,
(E) lift in the air, (F) kind 0 target (Beannoid), (G) an entity standing on the hero's head (+0x12C == hero).
Run: python -B -I g9_lift_rules.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana
import k0lib as K
import pworld as P
from machine import M32


def lifted(rows, c):
    return ana.first_tick(rows, lambda r: r['hero']['carried2C'] != 0)


def mk(crate_px=(130, 100), hero_px=(100, 100), direction=24, types=P.PROJECTILE_TYPES, cells=None):
    m = K.make_world(cells=K.cells_fn(cells) if cells else None, hero_px=hero_px, direction=direction, types=types)
    return m


if __name__ == '__main__':
    out = []
    # A: stand still at the crate (hero walked to contact, then released RIGHT), Square alone
    m = gl.world(); c = gl.add_crate(m, 130, 100)
    pads, n = gl.pad_script([(10, gl.RIGHT), (5, 0), (1, gl.SQUARE), (5, 0), (1, gl.SQUARE | gl.RIGHT), (5, gl.RIGHT)])
    rows = gl.run(m, n, pads, crates=[c])
    out.append('A idle at contact: Square alone at t16 -> hero.+0x2C=%X xcoll=%X anim=%d ; Square+RIGHT at t22 -> lifted at tick %s' % (rows[15]['hero']['carried2C'], rows[15]['hero']['xcoll'], rows[15]['hero']['anim_t'], lifted(rows, c)))
    # B: Square held while walking into the crate (edge happens before contact), then released/pressed
    m = gl.world(); c = gl.add_crate(m, 130, 100)
    pads, n = gl.pad_script([(1, gl.RIGHT | gl.SQUARE), (14, gl.RIGHT | gl.SQUARE)])     # Square held from t1 (edge only at t1)
    rows = gl.run(m, n, pads, crates=[c])
    out.append('B Square held from t1 while walking into the crate (no new edge at contact t5): lifted at %s' % lifted(rows, c))
    # B2: edge exactly on the first contact tick (t5)
    m = gl.world(); c = gl.add_crate(m, 130, 100)
    pads, n = gl.pad_script([(4, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (10, gl.RIGHT)])
    rows = gl.run(m, n, pads, crates=[c])
    out.append('B2 Square edge at t5 = the tick the hero reaches the crate (xcoll set during t5 physics): hero xcoll per tick %s ; lifted at %s' % ([hex(r['hero']['xcoll']) for r in rows[:7]], lifted(rows, c)))
    # B3: edge at t6 (first tick when xcoll is already set)
    m = gl.world(); c = gl.add_crate(m, 130, 100)
    pads, n = gl.pad_script([(5, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (10, gl.RIGHT)])
    rows = gl.run(m, n, pads, crates=[c])
    out.append('B3 Square edge at t6: lifted at %s' % lifted(rows, c))
    # C: other directions: crate N/S/W of the hero, walking into it
    for nm, (cx, cy), d in (('north', (100, 70), gl.UP), ('south', (100, 130), gl.DOWN), ('west', (70, 100), gl.LEFT)):
        m = gl.world(); c = gl.add_crate(m, cx, cy)
        pads, n = gl.pad_script([(10, d), (1, d | gl.SQUARE), (5, d)])
        rows = gl.run(m, n, pads, crates=[c])
        out.append('C %s: lifted at %s ; hero adir %d ; crate offset after lift (off30,off34) %s' % (nm, lifted(rows, c), rows[10]['hero']['adir'], (rows[10]['hero']['off30'] / 65536, rows[10]['hero']['off34'] / 65536)))
    # C2: diagonal: crate to the east, pad RIGHT+UP (pressing up-right): blocked in X only?
    m = gl.world(); c = gl.add_crate(m, 130, 100)
    pads, n = gl.pad_script([(10, gl.RIGHT | gl.UP), (1, gl.RIGHT | gl.UP | gl.SQUARE), (5, 0)])
    rows = gl.run(m, n, pads, crates=[c])
    out.append('C2 diagonal RIGHT+UP into the crate at (130,100): hero path y %s ; xcoll at t10 %X ; lifted at %s' % ([round(r['hero']['py'] / 65536, 2) for r in rows[:11:2]], rows[9]['hero']['xcoll'], lifted(rows, c)))
    # D: heavy stone, global sprite 234 (kind 3)
    for item, nm in ((None, 'without item 0x3B'), (0x3B, 'with item 0x3B')):
        types = tuple(P.PROJECTILE_TYPES) + (234,)
        m = mk(types=types)
        if item:
            m.call(0x8004E530, item)
        s = K.spawn_global(m, 234, 130, 100, 0, 24)
        e = K.ent(s)
        m.poke(e + 0x10, 2)
        pads, n = gl.pad_script([(12, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (50, gl.RIGHT), (1, gl.RIGHT | gl.CROSS), (30, gl.RIGHT)])
        rows = gl.run(m, n, pads, crates=[s])
        hx = [round(r['hero']['px'] / 65536, 2) for r in rows]
        out.append('D heavy stone %s: slot %s size/flags %X ; lifted at %s ; hero anim changes %s ; hero X t20 %.2f t60 %.2f t90 %.2f ; max hero Z %.2f ; stone offsets at t60 (%.2f,%.2f,%.2f)' % (
            nm, s, m.peek(e + 0x6C), lifted(rows, s), ana.changes(rows, 'hero', ['anim_t'], first=10)[:8], hx[19], hx[59], hx[89], max(r['hero']['pz'] for r in rows) / 65536,
            rows[59]['hero']['off30'] / 65536, rows[59]['hero']['off34'] / 65536, rows[59]['hero']['off38'] / 65536))
    # E: lift in the air: hero jumps east toward the crate (Cross), Square while blocked in the air
    m = gl.world(); c = gl.add_crate(m, 130, 100)
    pads, n = gl.pad_script([(3, gl.RIGHT), (1, gl.RIGHT | gl.CROSS), (12, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (4, gl.RIGHT)])
    rows = gl.run(m, n, pads, crates=[c])
    out.append('E air: hero anim/Z/xcoll per tick t4..t20 %s ; lifted at %s ; hero anim after %s' % ([(r['tick'], r['hero']['anim_t'], round(r['hero']['pz'] / 65536, 1), hex(r['hero']['xcoll'])) for r in rows[3:20:2]], lifted(rows, c), ana.changes(rows, 'hero', ['anim_t'], first=3)[:8]))
    # F: kind 0 target (Beannoid)
    m = gl.world()
    pl, _ = K.spawn_record_entities(m, 'map_14', 79, [(10, 11, 0)])
    p = pl[0]
    K.poke_entity(m, p, px=130 * 65536, py=100 * 65536)
    m.poke(K.ent(p) + 0x6C, m.peek(K.ent(p) + 0x6C) | 0x80)
    pads, n = gl.pad_script([(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (5, gl.RIGHT)])
    rows = gl.run(m, n, pads, extra=[p])
    out.append('F kind 0 (Beannoid made Collidable): hero xcoll at t10 %X ; lifted at %s ; hero anim t11 %d' % (rows[9]['hero']['xcoll'], lifted(rows, p), rows[10]['hero']['anim_t']))
    # G: an entity standing on the hero's head: a crate placed on top of the hero (Z = hero top + 1), hero idle: PlayerTryAction loop lifts it without any button
    m = gl.world(); c = gl.add_crate(m, 100, 100, zraw=None)
    e = K.ent(c)
    hero_top = m.peeks(gl.HERO + 0x11C) + m.peeks(gl.HERO + 0x1F8)         # PosZ + Depth (32 px)
    m.poke(e + 0x11C, (hero_top + 1) & M32); m.poke(e + 0x1E0, (hero_top + 1 + m.peeks(e + 0x1EC)) & M32)
    pads, n = gl.pad_script([(10, 0)])
    rows = gl.run(m, n, pads, crates=[c])
    out.append('G crate dropped on the hero head at Z=%.2f: crate support (+0x12C) per tick %s ; lifted at %s ; hero anim changes %s ; crate Z %s' % (
        (hero_top + 1) / 65536, [hex(r['c'][c]['support']) for r in rows[:4]], lifted(rows, c), ana.changes(rows, 'hero', ['anim_t'])[:5], [round(r['c'][c]['pz'] / 65536, 1) for r in rows[:6]]))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g9-lift-rules.txt'), 'w').write('\n'.join(out) + '\n')
