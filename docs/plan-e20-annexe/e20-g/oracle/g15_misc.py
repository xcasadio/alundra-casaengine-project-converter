"""g15_misc.py - S15: the experiments that were run inline during the survey, kept as a script:
 (a) lift in the air (Square edge at t6/t7/t8), (b) deep water with the water boots (item 0x1B) and without, (c) a stair/exit cell (+0x188 & 0x80) ahead, (d) thrown crate over pit/water cells,
 (e) ComputeNewHp(Beannoid) called by hand after the thrown crate touched it, (f) the lift timing rule (+0x130 is set by the physics of the PREVIOUS tick).
Run: python -B -I g15_misc.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana
import k0lib as K
import g8_events as G8
import g4_throw as G4
import g9_lift_rules as G9
from machine import M32

if __name__ == '__main__':
    out = []
    # (a) air lift
    for edge in (6, 7, 8):
        m = gl.world(); c = gl.add_crate(m, 130, 100)
        pads, n = gl.pad_script([(3, gl.RIGHT), (1, gl.RIGHT | gl.CROSS), (edge - 5, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (14, gl.RIGHT)])
        rows = gl.run(m, n, pads, crates=[c])
        out.append('a) air lift, Square edge t%d: lifted at %s ; hero anim changes %s ; (tick,anim,Z,xcoll) %s' % (edge, G9.lifted(rows, c), ana.changes(rows, 'hero', ['anim_t', 'carried2C'], first=3)[:6],
                   [(r['tick'], r['hero']['anim_t'], round(r['hero']['pz'] / 65536, 1), hex(r['hero']['xcoll'])) for r in rows[3:9]]))
    # (b) deep water with / without water boots
    for item in (None, 0x1B):
        def trig(m_, t, item=item):
            if t == 1 and item:
                m_.call(0x8004E530, item)
        cells = lambda col, row: dict(gp=0x08) if col >= 6 else None
        m, c, rows = G8.base(cells=cells, walk=[(48, 0), (60, gl.RIGHT)], n=119, trig_fn=trig)
        out.append('b) deep water, boots item %s: %s' % (item, G8.report('', rows, c, first=48)))
    # (c) stair/exit cell ahead
    cells = lambda col, row: dict(slope=0xC4) if col >= 6 else None
    m, c, rows = G8.base(cells=cells, walk=[(48, 0), (60, gl.RIGHT)], n=119)
    out.append('c) stair/exit cell slope byte 0xC4 for col>=6: %s ; +0x188 values %s' % (G8.report('', rows, c, first=48), sorted(set(hex(r['hero']['p188']) for r in rows))))
    # (d) thrown crate over pit / water cells
    for nm, cell in (('wk=0x04', dict(wk=0x04)), ('gp=0x80', dict(gp=0x80)), ('water gp=0x08', dict(gp=0x08))):
        cells = lambda col, row, cell=cell: dict(cell) if col >= 6 else None
        m, c, rows = G4.scenario(throw_hold=gl.RIGHT, cells=cells, n=100)
        s = G4.throw_stats(rows, c, 60)
        j = [(r['tick'], [x for x in r['journal'] if x.startswith('fx')]) for r in rows if any(x.startswith('fx') for x in r['journal'])]
        out.append('d) thrown crate over cells %s: crate changes %s ; fx %s' % (nm, s['change_crate'][1:], j))
    # (e) ComputeNewHp by hand after the touch
    tx = 172
    m = gl.world(hero_px=(100, 100), direction=24)
    c = gl.add_crate(m, 130, 100, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    pl, _ = K.spawn_record_entities(m, 'map_14', 79, [(10, 11, 0)])
    p = pl[0]; pe = K.ent(p)
    K.poke_entity(m, p, px=tx * 65536, py=100 * 65536)
    m.poke(pe + 0x1D8, (tx * 65536 + m.peeks(pe + 0x1E4)) & M32); m.poke(pe + 0x1DC, (100 * 65536 + m.peeks(pe + 0x1E8)) & M32)
    T = m.call(0x800370C4, pe); m.poke(pe + 0x138, T); m.poke(pe + 0x11C, T + 1); m.poke(pe + 0x1E0, T + 1)
    pads, n = gl.pad_script([(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (30, 0)])
    res = {}

    def post(m_, t, r):
        if t == 82:
            hp0 = m_.peeks(pe + 0x14)
            ret = m_.call(0x8003A374, pe)
            res['t82'] = dict(hp_before=hp0, hp_after=m_.peeks(pe + 0x14), ret=ret, touch_is_crate=m_.peek(pe + 0x224) == K.ent(c))
    rows = gl.run(m, n, pads, crates=[c], extra=[p], post=post)
    out.append('e) ComputeNewHp(Beannoid) by hand at t82 after the thrown crate touched it: %s' % res)
    # (f) lift timing: Square edge at t5, t6, t7 relative to the first contact
    for edge in (4, 5, 6, 7):
        m = gl.world(); c = gl.add_crate(m, 130, 100)
        pads, n = gl.pad_script([(edge - 1, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (12, gl.RIGHT)])
        rows = gl.run(m, n, pads, crates=[c])
        out.append('f) Square edge at t%d (first contact: +0x130 set at the end of t5): lifted at %s ; hero anim changes %s' % (edge, G9.lifted(rows, c), ana.changes(rows, 'hero', ['anim_t'], first=2)[:3]))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g15-misc.txt'), 'w').write('\n'.join(out) + '\n')
