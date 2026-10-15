"""k0_carry.py - carried entities (+0x28 / +0x2C) and riders (+0x12C) in the REAL physics pass.
F1 a crate in the hero's hands (+0x28 = hero, hero.+0x2C = crate, hero.+0x30..0x38 = offset), the hero is moved 1 px per tick by the harness (the hero's own motion is not under test).
F2 a rider crate standing on a walking platform (a Beannoid made Collidable by the harness: header flags |= 0x80, because the Beannoid is the only walker record at hand; the platform logic reads only
   Collidable + Gravity + the box tops).
F3 the platform is teleported away (harness write, Z + 64 px): is +0x12C cleared?  does the rider keep inheriting the platform's forces?
Run: python -B -I k0_carry.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
import interp2
from k0_arrows import OUT

M = K.M32


def lists(m):
    return dict(active=m.peeks(0x80127D28), coll=m.peeks(0x80127D2C))


def carried(n=12, ticker=None):
    m = K.make_world(hero_px=(100, 100))
    slots, info = K.spawn_record_entities(m, 'map_392', 36, [(10, 11, 0)])
    s = slots[0]
    hero = K.ent(0)
    hz = m.peeks(hero + 0x11C)
    # state of a carried crate (TryLift 0x8002EDBC): crate.+0x28 = hero, hero.+0x2C = crate, hero.+0x30..0x38 = crate.Pos - hero.Pos (here 0, 0, 32 px = the carry pose of UpdatePlayerCarriedEntity mode 0)
    K.poke_entity(m, s, carrier28=hero, t24=0, px=m.peek(hero + 0x114), py=m.peek(hero + 0x118), pz=hz + 0x200000)
    m.poke(hero + 0x2C, K.ent(s))
    m.poke(hero + 0x30, 0); m.poke(hero + 0x34, 0); m.poke(hero + 0x38, 0x200000)
    init = K.snap(m, s)

    def pre(m_, t):
        m_.poke(hero + 0x114, (100 + t) * 65536)          # the hero walks 1 px per tick (harness write)

    def post(m_, t, r):
        r['lists'] = lists(m_)
        r['hero'] = dict(px=m_.peeks(hero + 0x114), pz=m_.peeks(hero + 0x11C))
    rows = K.run(m, n, [s], pre=pre, post=post, ticker=ticker)
    return m, s, init, rows


def rider(n=40, ticker=None, tele_at=None, tele_dz=64 * 65536, platform_anim=7):
    m = K.make_world(hero_px=(30, 30))
    pl, _ = K.spawn_record_entities(m, 'map_14', 79, [(10, 11, 0)])
    p = pl[0]
    pe = K.ent(p)
    K.poke_entity(m, p, px=100 * 65536, py=100 * 65536)
    m.poke(pe + 0x6C, m.peek(pe + 0x6C) | 0x80)           # harness: Collidable (the Beannoid header has no 0x80)
    # place the platform on the terrain (what InitializeEntity does)
    m.poke(pe + 0x1D8, (100 * 65536 + m.peeks(pe + 0x1E4)) & M); m.poke(pe + 0x1DC, (100 * 65536 + m.peeks(pe + 0x1E8)) & M)
    T = m.call(0x800370C4, pe)
    m.poke(pe + 0x138, T); m.poke(pe + 0x11C, T + 1); m.poke(pe + 0x1E0, T + 1)
    top = T + 1 + m.peeks(pe + 0x1F8)                      # ModdedPosZ + Depth
    cr, _ = K.spawn_record_entities(m, 'map_392', 36, [(10, 11, 0)], rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    c = cr[0]
    ce = K.ent(c)
    K.poke_entity(m, c, px=100 * 65536, py=100 * 65536, pz=top + 1)
    init = dict(platform=K.snap(m, p), rider=K.snap(m, c), top=top)

    def pre(m_, t):
        if t == 1:
            K.poke_entity(m_, p, anim_t=platform_anim, dir_t=24)
        if tele_at and t == tele_at:
            K.poke_entity(m_, p, pz=m_.peeks(pe + 0x11C) + tele_dz)
    rows = K.run(m, n, [p, c], pre=pre, ticker=ticker)
    return m, (p, c), init, rows


def save(name, txt):
    open(os.path.join(OUT, 'carry-%s.txt' % name), 'w').write(txt + '\n')


if __name__ == '__main__':
    lines = []
    a = carried()
    b = carried(ticker=interp2.tick2)
    d = K.same_rows(a[3], b[3])
    m, s, init, rows = a
    cols = ['status', 'anim_t', 'carrier28', 'fz_B8', 'fx_C4', 'fnx_E4', 'fnz_EC', 'px', 'pz', 'terr_138', 'floor_134', 'collz_140', 'adj_13C', 'ground_144', 'moved_168']
    txt = K.table(rows, s, cols, title='F1 carried crate; hero X = 100 + t (harness); crate offsets (0,0,32 px) from hero.+0x30..0x38; interp diff %s' % d)
    txt += '\nlist counts (active, collidable) per tick: ' + str([(r['tick'], r['lists']['active'], r['lists']['coll']) for r in rows])
    txt += '\nhero PosX per tick: ' + str([r['hero']['px'] / 65536 for r in rows])
    save('carried', txt)
    lines.append('F1 carried crate: crate X per tick %s (hero X + 0); Z %s ; forces %s ; CollidedZ %s ; ForceAdjusted %s ; active/collidable counts %s ; interp diff %s' % (
        [r['slots'][s]['px'] / 65536 for r in rows[:6]], sorted(set(r['slots'][s]['pz'] / 65536 for r in rows)), sorted(set((r['slots'][s]['fz_B8'], r['slots'][s]['fx_C4'], r['slots'][s]['fnx_E4'], r['slots'][s]['fnz_EC']) for r in rows)),
        sorted(set(r['slots'][s]['collz_140'] for r in rows)), sorted(set(r['slots'][s]['adj_13C'] for r in rows)), sorted(set((r['lists']['active'], r['lists']['coll']) for r in rows)), d))

    a = rider()
    b = rider(ticker=interp2.tick2)
    d = K.same_rows(a[3], b[3])
    m, (p, c), init, rows = a
    cols = ['anim_t', 'fx_C4', 'dx_DC', 'fnx_E4', 'px', 'pz', 'fz_B8', 'fnz_EC', 'floor_134', 'ground_144', 'collz_140', 'support_12C', 'moved_168']
    txt = 'F2 rider crate on a walking platform; platform top (ModdedPosZ+Depth) = %d ; interp diff %s\n' % (init['top'], d)
    txt += K.table(rows, p, cols, last=14, title='-- platform (slot %d)' % p) + '\n' + K.table(rows, c, cols, last=14, title='-- rider crate (slot %d)' % c)
    save('rider', txt)
    lines.append('F2 rider on platform: rider X-platform X per tick %s ; rider +0x12C = platform slot? %s ; rider Z %s ; interp diff %s' % (
        [round((r['slots'][c]['px'] - r['slots'][p]['px']) / 65536, 3) for r in rows[:12]], [r['slots'][c]['support_12C'] == K.ent(p) for r in rows[:4]], sorted(set(r['slots'][c]['pz'] for r in rows)), d))

    a = rider(n=60, tele_at=10)
    b = rider(n=60, tele_at=10, ticker=interp2.tick2)
    d = K.same_rows(a[3], b[3])
    m, (p, c), init, rows = a
    txt = 'F3 platform teleported up 64 px at tick 10 (harness write of its PosZ); rider crate afterwards; interp diff %s\n' % d
    txt += K.table(rows, p, cols, first=8, last=40, title='-- platform') + '\n' + K.table(rows, c, cols, first=8, last=40, title='-- rider crate')
    save('rider-teleported', txt)
    lines.append('F3 after the platform leaves: rider +0x12C still platform? %s ; rider X-platform X (ticks 10..30) %s ; rider Z (ticks 10..30) %s ; interp diff %s' % (
        sorted(set(r['slots'][c]['support_12C'] == K.ent(p) for r in rows[10:])), [round((r['slots'][c]['px'] - r['slots'][p]['px']) / 65536, 2) for r in rows[10:30:4]], [round(r['slots'][c]['pz'] / 65536, 2) for r in rows[10:30:2]], d))
    txt = '\n'.join(lines)
    open(os.path.join(OUT, 'carry-summary.txt'), 'w').write(txt + '\n')
    print(txt)


def bump(collidable, n=40, ticker=None):
    """F4: a walker (Beannoid, anim 7 east) walks into a stationary crate placed 40 px east; the walker is a subject (Collidable) or not (its real header has no 0x80)"""
    m = K.make_world(hero_px=(30, 30))
    pl, _ = K.spawn_record_entities(m, 'map_14', 79, [(10, 11, 0)])
    p = pl[0]
    pe = K.ent(p)
    K.poke_entity(m, p, px=100 * 65536, py=100 * 65536)
    if collidable:
        m.poke(pe + 0x6C, m.peek(pe + 0x6C) | 0x80)
    m.poke(pe + 0x1D8, (100 * 65536 + m.peeks(pe + 0x1E4)) & M); m.poke(pe + 0x1DC, (100 * 65536 + m.peeks(pe + 0x1E8)) & M)
    T = m.call(0x800370C4, pe)
    m.poke(pe + 0x138, T); m.poke(pe + 0x11C, T + 1); m.poke(pe + 0x1E0, T + 1)
    cr, _ = K.spawn_record_entities(m, 'map_392', 36, [(10, 11, 0)], rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    c = cr[0]
    K.poke_entity(m, c, px=140 * 65536, py=100 * 65536, pz=T + 1)
    m.poke(K.ent(c) + 0x1D8, (140 * 65536 + m.peeks(K.ent(c) + 0x1E4)) & M); m.poke(K.ent(c) + 0x1DC, (100 * 65536 + m.peeks(K.ent(c) + 0x1E8)) & M)

    def pre(m_, t):
        if t == 1:
            K.poke_entity(m_, p, anim_t=7, dir_t=24)
    rows = K.run(m, n, [p, c], pre=pre, ticker=ticker)
    return m, (p, c), rows


if __name__ == '__main__' and True:
    lines2 = []
    for col in (True, False):
        a = bump(col)
        b = bump(col, ticker=interp2.tick2)
        d = K.same_rows(a[2], b[2])
        m, (p, c), rows = a
        cols = ['anim_t', 'fnx_E4', 'px', 'pz', 'adj_13C', 'xcoll_130', 'collz_140', 'support_12C']
        txt = 'F4 walker (Collidable=%s) walks east into a stationary crate at x=140; interp diff %s\n' % (col, d) + K.table(rows, p, cols, first=8, last=30, title='-- walker') + '\n' + K.table(rows, c, ['px', 'adj_13C', 'xcoll_130'], first=8, last=12, title='-- crate')
        save('bump-collidable-%s' % col, txt)
        t = next((r['tick'] for r in rows if r['slots'][p]['adj_13C']), None)
        lines2.append('F4 walker Collidable=%s: first ForceAdjusted tick %s ; walker X at end %.3f ; +0x130 at that tick %s (crate slot address %X) ; crate X at end %.3f ; interp diff %s' % (
            col, t, rows[-1]['slots'][p]['px'] / 65536, ('%X' % rows[t - 1]['slots'][p]['xcoll_130']) if t else None, K.ent(c), rows[-1]['slots'][c]['px'] / 65536, d))
    open(os.path.join(OUT, 'carry-summary.txt'), 'a').write('\n'.join(lines2) + '\n')
    print('\n'.join(lines2))
