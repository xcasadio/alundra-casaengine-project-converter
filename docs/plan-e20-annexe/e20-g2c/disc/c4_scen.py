"""c4_scen.py - whole-pipeline replays (REAL MovePlayer, REAL map-record spawner, REAL C4/C23/E2, REAL physics passes, inert stubs elsewhere) of the C4 family.
Harness writes: the pad words, the cell field, the object placement (as gl.add_crate), nothing else.  Uses g1a/work/orc (copied under ./work/orc).
Run: python -B -I -X utf8 c4_scen.py [scenario-name ...]"""
import os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'work', 'orc')
sys.path.insert(0, ORC)
import gl
import ana
import k0lib as K
from machine import M32

OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)
gl.CFIELDS.extend([('bounced', 0x278, 1), ('slope18C', 0x18C, 1), ('t68', 0x68, 1), ('mode80', 0x80, 1)])
C4, E0 = 0x80066984, 0x8007ED10

OBJ = {  # name -> (map, sprite, extra real handlers)
    'tonneau34': ('map_22', 34, ()),
    'pierre37': ('map_24', 37, ()),
    'etoile107': ('map_27', 107, (E0,)),
    'lune111': ('map_27', 111, (E0,)),
    'eau112': ('map_27', 112, (E0,)),
    'soleil113': ('map_27', 113, (E0,)),
    'tronc117': ('map_70', 117, ()),
    'glace304_c23': ('map_103', 48, ()),
}


def widen(mapname, sprite):
    """harness: the whole-map spawn zone of the record (the real zone test of 0x8003A200-0x8003A25C needs the hero's cell inside the record's zone; docs g1a report 3.6)"""
    key = (mapname, sprite)
    if key not in K._CACHE:
        K._CACHE[key] = K._record(mapname, sprite)
    rec_, ents_, ts_ = K._CACHE[key]
    native = [e for e in ents_ if e['EventCodesC_TickIndex'] == 0 and e['EventCodesA_LoadIndex'] == 0 and e['EventCodesE_DeactivateIndex'] == 0]   # a placement with NO scripted program: the native header handlers run
    tmpl = native[0] if native else ents_[0]
    K._CACHE[key] = (rec_, [dict(tmpl, XMin=0, YMin=0, XMax=255, YMax=255)] + ents_[1:], ts_)


def throw_run(name, cells=None, throw_hold=gl.RIGHT, obj_x=130, n=150, throw_t=61, hero_px=(100, 100), after=None, platform=None):
    mapname, sprite, extra = OBJ[name]
    keep = {C4} | set(extra)
    m = gl.world(keep=keep, cells=cells, hero_px=hero_px)
    widen(mapname, sprite)
    c = gl.add_crate(m, obj_x, hero_px[1], mapname=mapname, sprite=sprite)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (throw_t - 12, 0), (1, throw_hold | gl.SQUARE), (n - throw_t, 0)]
    if after:
        seq = seq[:-1] + after
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[c])
    return m, c, rows


def snaps(rows, c, ticks, cols=('status', 'anim_t', 'flags6C', 'px', 'py', 'pz', 'ground', 'adj', 'collz', 'support', 'hitc', 'bounced', 'slope18C', 'dir_t')):
    out = []
    for r in rows:
        if r['tick'] in ticks:
            d = r['c'][c]
            out.append('t%03d ' % r['tick'] + ' '.join('%s=%s' % (k, gl.fmt(k, d[k])) for k in cols))
    return out


def jr(rows, lo, hi):
    return [(r['tick'], [x for x in r['journal'] if not x.startswith('stub')]) for r in rows if lo <= r['tick'] <= hi and [x for x in r['journal'] if not x.startswith('stub')]]


def summary(name, rows, c, throw_t=61):
    st = lambda t: rows[t - 1]['c'][c]
    land = ana.first_tick(rows, lambda r: r['c'][c]['ground'] == 1 and r['c'][c]['carrier28'] == 0, first=throw_t + 1)
    chg = ana.changes(rows, c, ['status', 'anim_t', 'flags6C', 'dir_t', 'bounced'], first=throw_t - 1)[:14]
    dead = ana.first_tick(rows, lambda r: r['c'][c]['status'] == 0, first=throw_t)
    last = rows[-1]['c'][c]
    return ('%s: land t%s ; first status 0 t%s ; changes %s ; final status %d anim %d flags %X pos (%.2f,%.2f,%.2f) ground %d hitc %d bounced %d' % (
        name, land, dead, chg, last['status'], last['anim_t'], last['flags6C'], last['px'] / 65536, last['py'] / 65536, last['pz'] / 65536, last['ground'], last['hitc'], last['bounced']))


SCEN = {}


def scen(f):
    SCEN[f.__name__] = f
    return f


@scen
def s1_flat():
    """S1: throw east on flat ground, moving (3 px/tick) and still, for each member of the family + the ice block (C23) as a contrast"""
    out = []
    for name in OBJ:
        for hold in (gl.RIGHT, 0):
            m, c, rows = throw_run(name, throw_hold=hold)
            tag = '%s %s' % (name, 'moving' if hold else 'still')
            out.append(summary(tag, rows, c))
            out.append('   journal(t>=58): %s' % jr(rows, 58, 100)[:8])
            open(os.path.join(OUT, 's1-%s-%s.txt' % (name, 'moving' if hold else 'still')), 'w').write(gl.table(rows, [c], first=58, last=110, every=1, hcols=['anim_t', 'carried2C', 'px', 'pz', 'ground'], ccols=['status', 't24', 'anim_t', 'flags6C', 'px', 'py', 'pz', 'ground', 'adj', 'hitc', 'bounced', 'support'], title=tag) + '\n')
    return out


@scen
def s2_wall():
    """S2: wall at column COL (walkability 0x40; cells are 24 px wide), throw east: the bounce of C4 (effect 9, direction + 0x10) needs the contact while the object rises (+0xB8 > 0)"""
    out = []
    for name in ('tonneau34', 'pierre37', 'tronc117', 'etoile107', 'glace304_c23'):
        for col in (5, 6, 7, 8):
            for hold in (gl.RIGHT, 0):
                cells = lambda cc, rr, col=col: dict(wk=0x40) if cc == col else None
                m, c, rows = throw_run(name, cells=cells, throw_hold=hold)
                fx = [(r['tick'], x) for r in rows if r['tick'] > 60 for x in r['journal'] if x.startswith('fx')]
                path = [(r['tick'], round(r['c'][c]['px'] / 65536, 1), round(r['c'][c]['pz'] / 65536, 1), r['c'][c]['dir_t'], r['c'][c]['adj'], r['c'][c]['anim_t']) for r in rows if 61 <= r['tick'] <= 100 and r['tick'] % 3 == 1]
                last = rows[-1]['c'][c]
                out.append('%s wallcol=%d %s: fx %s ; final status %d anim %d dir %d pos (%.1f,%.1f,%.1f) bounced %d ; path(t,x,z,dir,adj,anim) %s' % (
                    name, col, 'moving' if hold else 'still', fx[:3], last['status'], last['anim_t'], last['dir_t'], last['px'] / 65536, last['py'] / 65536, last['pz'] / 65536, last['bounced'], path[:10]))
    return out


@scen
def s3_landing_words():
    """S3: the object lands (x ~ 178, column 7) on cells whose word is 0x1001 (walkability 0x01 + ground_property 0x10), with and without slope 4"""
    out = []
    for name in ('tonneau34', 'pierre37', 'tronc117', 'etoile107', 'glace304_c23'):
        for slope in (0, 4):
            cells = lambda cc, rr, slope=slope: dict(wk=0x01, gp=0x10, slope=slope) if 6 <= cc <= 9 else None
            m, c, rows = throw_run(name, cells=cells, throw_hold=gl.RIGHT)
            out.append(summary('%s over 0x1001 cells slope=%d' % (name, slope), rows, c))
            out.append('   journal(t>=58) %s' % jr(rows, 58, 140)[:10])
            out.append('   states: %s' % ' | '.join(snaps(rows, c, {80, 83, 84, 85, 86, 90, 100, 140}, cols=('status', 'anim_t', 'flags6C', 'pz', 'ground', 'slope18C', 'hitc'))))
    return out


@scen
def s3b_water():
    """S3b: the object lands on cells whose word has the water bit (ground_property 0x08 -> pente 4 = (word & 0xE00) >> 9, plan section 1.21) with the wall bits (0x1000 / 0x0001) or without"""
    out = []
    for label, gp, wk in (('word 0x0800 (swim water)', 0x08, 0x00), ('word 0x1801 (water + both wall bits)', 0x18, 0x01), ('word 0x0200 (pente 1)', 0x02, 0x00)):
        for name in ('tonneau34', 'etoile107', 'tronc117'):
            cells = lambda cc, rr, gp=gp, wk=wk: dict(wk=wk, gp=gp) if 6 <= cc <= 9 else None
            m, c, rows = throw_run(name, cells=cells, throw_hold=gl.RIGHT, n=170)
            snd = sum(1 for r in rows for x in r['journal'] if x == 'snd(24)')
            fx = [(r['tick'], x[:60]) for r in rows for x in r['journal'] if x.startswith('fx')][:3]
            dead = ana.first_tick(rows, lambda r: r['c'][c]['status'] == 0, first=62)
            last = rows[-1]['c'][c]
            chg = ana.changes(rows, c, ['status', 'anim_t', 'slope18C'], first=60)[:8]
            out.append('%s | %s: dead t%s ; snd24 x%d ; fx %s ; changes %s ; final st %d anim %d slope18C %d pos %.1f' % (label, name, dead, snd, fx, chg, last['status'], last['anim_t'], last['slope18C'], last['px'] / 65536))
    return out


def add_plate(m, xpx, ypx, mapname='map_27', sprite=108, rec=None):
    widen(mapname, sprite)
    kw = dict(rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    return gl.add_crate(m, xpx, ypx, mapname=mapname, sprite=sprite, **kw)


@scen
def s5_plate():
    """S5: the thrown object lands on a plate (map 27 sprite 108: 48x32x16 slab, MoreFlags 0xC0 = Collidable) - does it rest on it (+0x12C), at which height, and can the hero lift it back from the plate"""
    out = []
    for name in ('etoile107', 'tonneau34'):
        for px in (160, 178, 190):
            mapname, sprite, extra = OBJ[name]
            keep = {C4} | set(extra)
            widen(mapname, sprite)
            m = gl.world(keep=keep)
            c = gl.add_crate(m, 130, 100, mapname=mapname, sprite=sprite)
            p = add_plate(m, px, 100)
            pe = K.ent(p)
            seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (60, 0)]
            pads, nn = gl.pad_script(seq)
            rows = gl.run(m, nn, pads, crates=[c, p])
            land = ana.first_tick(rows, lambda r: r['c'][c]['ground'] == 1 and r['c'][c]['carrier28'] == 0, first=62)
            st = lambda t: rows[t - 1]['c'][c]
            pl = rows[-1]['c'][p]
            fl = m.peek(pe + 0x6C)
            out.append('%s on plate@%d: plate flags6C=%X pz=%.2f box depth(+0x1F8)=%.2f ; land t%s ; at t%d: obj pos (%.2f,%.2f,%.2f) ground=%d support==plate:%s status %d anim %d flags %X ; end: support==plate:%s pz=%.2f' % (
                name, px, fl, pl['pz'] / 65536, m.peeks(pe + 0x1F8) / 65536, land, nn, st(nn)['px'] / 65536, st(nn)['py'] / 65536, st(nn)['pz'] / 65536, st(nn)['ground'], st(nn)['support'] == pe, st(nn)['status'], st(nn)['anim_t'], st(nn)['flags6C'], st(nn)['support'] == pe, st(nn)['pz'] / 65536))
    return out


@scen
def s5b_lift_from_plate():
    """S5b: the object rests on the plate (x=172, z=16); the hero jumps onto the slab and lifts it back; and the hero standing on the ground next to the slab tries to lift it"""
    out = []
    name = 'etoile107'
    mapname, sprite, extra = OBJ[name]
    keep = {C4} | set(extra)
    for variant, tail in (('jump onto the slab then Square', [(35, gl.RIGHT), (1, gl.RIGHT | gl.CROSS), (30, gl.RIGHT), (20, 0), (1, gl.RIGHT | gl.SQUARE), (40, 0)]),
                          ('ground, walk to the slab, Square', [(60, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (40, 0)])):
        widen(mapname, sprite)
        m = gl.world(keep=keep)
        c = gl.add_crate(m, 130, 100, mapname=mapname, sprite=sprite)
        p = add_plate(m, 190, 100)
        pe = K.ent(p)
        seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (30, 0)] + tail
        pads, nn = gl.pad_script(seq)
        rows = gl.run(m, nn, pads, crates=[c, p])
        tr = [(r['tick'], round(r['hero']['px'] / 65536, 1), round(r['hero']['pz'] / 65536, 1), r['hero']['anim_t'], r['hero']['support'] == pe, r['hero']['carried2C'] != 0, r['hero']['xcoll'] == pe, r['hero']['xcoll'] == K.ent(c), round(r['c'][c]['pz'] / 65536, 1)) for r in rows if r['tick'] >= 92 and r['tick'] % 6 == 2]
        out.append('%s: (t, heroX, heroZ, heroAnim, hero.support==plate, carrying, hero.xcoll==plate, hero.xcoll==obj, objZ) %s' % (variant, tr))
    return out


@scen
def s6_touch():
    """S6: the touch question (O-G-9): the object thrown at a class A target (Beannoid map 14 sprite 79 by the REAL record spawner, handlers inert, made hittable as the real hit pass sees it); REAL hit pass 0x80039300; damage = the target's ComputeNewHp 0x8003A374 called by the harness at the tick of the touch (as g1a thrown_damage.py)"""
    OBJ2 = dict(OBJ); OBJ2['caisse392_c23'] = ('map_392', 36, ())
    out = []
    for name in ('caisse392_c23', 'tonneau34', 'pierre37', 'tronc117', 'etoile107'):
        for tx in (165,):
            mapname, sprite, extra = OBJ2[name]
            keep = {C4} | set(extra)
            widen(mapname, sprite)
            m = gl.world(keep=keep)
            c = gl.add_crate(m, 130, 100, mapname=mapname, sprite=sprite)
            pl, _ = K.spawn_record_entities(m, 'map_14', 79, [(10, 11, 0)], rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
            p = pl[0]; pe = K.ent(p)
            K.poke_entity(m, p, px=tx * 65536, py=100 * 65536)
            m.poke(pe + 0x1D8, (tx * 65536 + m.peeks(pe + 0x1E4)) & M32); m.poke(pe + 0x1DC, (100 * 65536 + m.peeks(pe + 0x1E8)) & M32)
            T = m.call(0x800370C4, pe)
            m.poke(pe + 0x138, T); m.poke(pe + 0x11C, T + 1); m.poke(pe + 0x1E0, T + 1)
            seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (45, 0)]
            pads, nn = gl.pad_script(seq)
            res = {}

            def post(m_, t, r, pe=pe, res=res):
                if m_.peek(pe + 0x224) != 0 and 'tick' not in res:
                    res['tick'] = t
                    res['hp0'] = m_.peeks(pe + 0x14)
                    res['dead'] = m_.call(0x8003A374, pe)
                    res['hp1'] = m_.peeks(pe + 0x14)
            rows = gl.run(m, nn, pads, crates=[c], extra=[p], post=post)
            ch = ana.changes(rows, c, ['status', 'anim_t', 'flags6C', 'hitc'], first=60)[:8]
            last = rows[-1]['c'][c]
            out.append('%s thrown at the Beannoid x=%d: target touched at t%s hp %s -> %s dead=%s ; object changes %s ; final status %d anim %d pos %.1f' % (
                name, tx, res.get('tick'), res.get('hp0'), res.get('hp1'), res.get('dead'), ch, last['status'], last['anim_t'], last['px'] / 65536))
    return out


@scen
def s7_stack():
    """S7: the pile of 27 C[36] in miniature: object 1 thrown onto a plate (lands on it, z 16), the hero lifts object 2 and throws it onto object 1 (z 16 + 14?): who rests on whom (+0x12C), at which height"""
    out = []
    mapname1, sprite1, extra1 = OBJ['etoile107']
    mapname2, sprite2, extra2 = OBJ['lune111']
    keep = {C4} | set(extra1) | set(extra2)
    widen(mapname1, sprite1); widen(mapname2, sprite2)
    m = gl.world(keep=keep)
    c1 = gl.add_crate(m, 130, 100, mapname=mapname1, sprite=sprite1)
    c2 = gl.add_crate(m, 130, 100, mapname=mapname2, sprite=sprite2, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    widen('map_27', 108)
    p = gl.add_crate(m, 190, 100, mapname='map_27', sprite=108, rec_addr=0x801C4000, ent_addr=0x801CD000, ptr_addr=0x801CC000)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (25, 0),            # lift #1 (t11), throw #1 (t61) -> lands t81 on the plate
           (6, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (60, 0), (1, gl.RIGHT | gl.SQUARE), (45, 0)]             # walk into #2, lift #2, throw #2
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[c1, c2, p])
    e1, e2, ep = K.ent(c1), K.ent(c2), K.ent(p)
    for t in (85, 100, 110, 120, 150, 170, 180, 190, nn):
        r = rows[t - 1]
        out.append('t%d: obj1 (x%.1f z%.1f ground %d support==plate %s carrier %s) | obj2 (x%.1f z%.1f ground %d support==obj1 %s support==plate %s carrier %s) | hero carrying %s anim %d' % (
            t, r['c'][c1]['px'] / 65536, r['c'][c1]['pz'] / 65536, r['c'][c1]['ground'], r['c'][c1]['support'] == ep, gl.fmt('carrier28', r['c'][c1]['carrier28']),
            r['c'][c2]['px'] / 65536, r['c'][c2]['pz'] / 65536, r['c'][c2]['ground'], r['c'][c2]['support'] == e1, r['c'][c2]['support'] == ep, gl.fmt('carrier28', r['c'][c2]['carrier28']),
            r['hero']['carried2C'] != 0, r['hero']['anim_t']))
    return out


@scen
def s8_torch():
    """S8: the torch (map 86 sprite 137, C70 + E20): thrown flat; REAL C70 0x8007B7B0 and E20 0x8007F6C8; the flame E20 spawns (REAL SpawnWarpEntity, type 12); then thrown into a Goutte d'eau (type 391)-like contact is not run (no such entity)"""
    out = []
    C70, E20 = 0x8007B7B0, 0x8007F6C8
    widen('map_86', 137)
    for hold in (gl.RIGHT, 0):
        m = gl.world(keep={C70, E20})
        c = gl.add_crate(m, 130, 100, mapname='map_86', sprite=137)
        seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, hold | gl.SQUARE), (60, 0)]
        pads, nn = gl.pad_script(seq)
        rows = gl.run(m, nn, pads, crates=[c])
        ch = ana.changes(rows, c, ['status', 'anim_t', 'flags6C'], first=60)[:8]
        j = [(r['tick'], [x for x in r['journal'] if not x.startswith('stub')]) for r in rows if r['tick'] > 60 and [x for x in r['journal'] if not x.startswith('stub')]]
        act = []
        for s_ in K.active(m):
            e = K.ent(s_)
            act.append((s_, hex(m.peek(e + 0x68)), m.peek(e + 0x10), m.peek(e + 0x88), hex(m.peek(e + 0x6C)), round(m.peeks(e + 0x114) / 65536, 1), round(m.peeks(e + 0x11C) / 65536, 1)))
        out.append('torch %s: changes %s ; journal %s ; active slots at the end (slot, type, status, anim, flags, x, z) %s' % ('moving' if hold else 'still', ch, j[:8], act))
    return out


@scen
def s9_lock():
    """S9 (O-G-10): the control lock ([0x800DC4B8] = 4 from t60 for 40 ticks) with the object in the hands, FULL tick, each header family incl. the ones the old harness could not spawn (AttackCount 0 and 7): REAL UpdatePlayerCarriedEntity mode 1"""
    OBJ2 = dict(OBJ); OBJ2['herbe41_c23'] = ('map_106', 41, ()); OBJ2['caisse392_c23'] = ('map_392', 36, ())
    out = []
    for name in ('caisse392_c23', 'tonneau34', 'pierre37', 'tronc117', 'etoile107', 'lune111', 'herbe41_c23'):
        mapname, sprite, extra = OBJ2[name]
        keep = {C4} | set(extra)
        widen(mapname, sprite)
        m = gl.world(keep=keep)
        c = gl.add_crate(m, 130, 100, mapname=mapname, sprite=sprite)
        sheet = m.peek(K.ent(c) + 0x1C4)
        ac = (m.peek(sheet + 0xC) >> 16) & 0xFF
        seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (n_ := 120, 0)]
        pads, nn = gl.pad_script(seq)

        def lock(m_, t):
            if t == 60:
                m_.poke(0x800DC4B8, 4)
            if t == 100:
                m_.poke(0x800DC4B8, 0)
        rows = gl.run(m, nn, pads, crates=[c], pre=lock)
        ch = ana.changes(rows, c, ['status', 'carrier28'], first=58)[:5]
        hc = ana.changes(rows, 'hero', ['carried2C'], first=58)[:3]
        out.append('%s AttackCount=%d: object changes %s ; hero changes %s ; end status %d carrier %s' % (name, ac, ch, hc, rows[-1]['c'][c]['status'], gl.fmt('carrier28', rows[-1]['c'][c]['carrier28'])))
    return out


@scen
def s10_ride():
    """S10: a landed object resting on a platform that walks (a collidable Beannoid, as g1a g5): does the object ride with it?  (another way an object could be 'pushed': by its support)"""
    out = []
    for name in ('tonneau34', 'etoile107'):
        mapname, sprite, extra = OBJ[name]
        widen(mapname, sprite)
        m = gl.world(keep={C4} | set(extra))
        c = gl.add_crate(m, 300, 100, mapname=mapname, sprite=sprite)
        pl, _ = K.spawn_record_entities(m, 'map_14', 79, [(10, 11, 0)], rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
        p = pl[0]; pe = K.ent(p)
        K.poke_entity(m, p, px=300 * 65536, py=100 * 65536)
        m.poke(pe + 0x6C, m.peek(pe + 0x6C) | 0x80)
        m.poke(pe + 0x1D8, (300 * 65536 + m.peeks(pe + 0x1E4)) & M32); m.poke(pe + 0x1DC, (100 * 65536 + m.peeks(pe + 0x1E8)) & M32)
        T = m.call(0x800370C4, pe)
        m.poke(pe + 0x138, T); m.poke(pe + 0x11C, T + 1); m.poke(pe + 0x1E0, T + 1)
        top = T + 1 + m.peeks(pe + 0x1F8)
        ce = K.ent(c)
        pads, nn = gl.pad_script([(100, 0)])

        def pre(m_, t):
            if t == 1:
                m_.poke(ce + 0x11C, (top + 1) & M32)               # harness: the object stands on the platform top
                m_.poke(ce + 0x1E0, (top + 1 + m_.peeks(ce + 0x1EC)) & M32)
            if t == 20:
                K.poke_entity(m_, p, anim_t=7, dir_t=24)           # the platform walks east
        rows = gl.run(m, nn, pads, crates=[c], extra=[p], pre=pre)
        xs = [(r['tick'], round(r['c'][p]['px'] / 65536, 1), round(r['c'][c]['px'] / 65536, 1), round(r['c'][c]['pz'] / 65536, 1), r['c'][c]['support'] == pe) for r in rows if r['tick'] in (10, 19, 25, 40, 60, 80, 100)]
        out.append('%s on a walking platform (t, platformX, objectX, objectZ, support==platform): %s' % (name, xs))
    return out


@scen
def s4_hero_and_stone():
    """S4: after the stone has landed (t83), what the hero can do with it: walk into it (does it move?), stand on it, lift it again"""
    out = []
    for name in ('tonneau34', 'pierre37', 'etoile107', 'tronc117'):
        after = [(30, 0), (60, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (60, 0)]       # walk east into the landed object, lift it again, wait
        m, c, rows = throw_run(name, throw_hold=gl.RIGHT, n=61, after=after)
        land = ana.first_tick(rows, lambda r: r['c'][c]['ground'] == 1 and r['c'][c]['carrier28'] == 0, first=62)
        xs = [(r['tick'], round(r['c'][c]['px'] / 65536, 2), round(r['hero']['px'] / 65536, 2), r['hero']['carried2C'] != 0, r['hero']['xcoll'] == gl.K.ent(c), r['hero']['anim_t']) for r in rows if r['tick'] >= 84 and r['tick'] % 5 == 0]
        out.append('%s: land t%s ; (t, objX, heroX, carried, heroXcoll==obj, heroAnim) %s' % (name, land, xs))
    return out


if __name__ == '__main__':
    names = sys.argv[1:] or list(SCEN)
    for nm in names:
        res = SCEN[nm]()
        txt = '\n'.join(res)
        print('== %s\n%s' % (nm, txt))
        open(os.path.join(OUT, nm + '.txt'), 'w', encoding='utf-8').write(txt + '\n')
