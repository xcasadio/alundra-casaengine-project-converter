"""READ-ONLY oracle run on a COPY of docs/plan-e20-annexe/e20-g1a/work/orc (./oracle_g1a, never edited): REAL code of ALUN_CD.EXE (France), the crate = map 392 record sprite 36 (collidable, IDSV 7).
Scenario g1_lift.lift_walk: the hero walks east, lifts the crate with the Square, walks with it. After each tick the binary's sort driver 0x800399B8 is run; keys +0x1BC of the hero and of the crate are printed.
Larger key = drawn later = in front."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
O = os.path.join(HERE, 'oracle_g1a')
sys.path.insert(0, O)
sys.path.insert(0, os.path.join(O, 'oracle'))
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
import gl  # noqa: E402
import world as W  # noqa: E402
import k0lib as K  # noqa: E402

rows = []


def slot(key):
    return 16 * min((key & 0xFFFFFFFF) >> 20, 59) + 6


def make_post(crate_slot):
    def post(m, t, r):
        W.run_pass(m, 0x800399B8)
        h = gl.HERO
        c = K.ent(crate_slot)

        def f(e):
            return dict(st=m.peeks(e + 0x10), fl=m.peek(e + 0x6C), afl=m.peek(e + 0xB4) & 0xFF, car=m.peek(e + 0x28), idsv=m.peeks(e + 0x1A8), y=m.peeks(e + 0x118) / 65536,
                        z=m.peeks(e + 0x11C) / 65536, ta=m.peeks(e + 0x88), key=m.peek(e + 0x1BC) & 0xFFFFFFFF)
        rows.append((t, f(h), f(c)))
    return post


def go(label, hero_px, crate_px, seq, direction=24, n=90):
    rows.clear()
    m = gl.world(hero_px=hero_px, direction=direction)
    c = gl.add_crate(m, *crate_px)
    pads, nn = gl.pad_script(seq)
    gl.run(m, nn if nn else n, pads, crates=[c], post=make_post(c))
    print("== %s (crate slot %d)" % (label, c))
    print("tick | hero st anim idsv  y(px)   z(px)  key      slot | crate st fl       afl car        idsv  y(px)   z(px)  key      slot | crate-hero | crate is")
    prev = None
    cnt = {'front': 0, 'behind': 0, 'equal': 0}
    for t, h, cr in rows:
        d = cr['key'] - h['key']
        v = 'IN FRONT' if d > 0 else ('BEHIND' if d < 0 else 'equal')
        cnt['front' if d > 0 else ('behind' if d < 0 else 'equal')] += 1
        st = (cr['car'] != 0, v, h['idsv'], cr['idsv'], round(cr['z']))
        if st != prev or t % 10 == 0 or t <= 4:
            print("%4d | h: %2d %3d %3d  %7.2f %6.2f %08X %3d | c: %2d %08X %02X %08X %3d  %7.2f %6.2f %08X %3d | %+9d | %s" % (
                t, h['st'], h['ta'], h['idsv'], h['y'], h['z'], h['key'], slot(h['key']), cr['st'], cr['fl'], cr['afl'], cr['car'], cr['idsv'], cr['y'], cr['z'], cr['key'], slot(cr['key']), d, v))
        prev = st
    print("ticks:", len(rows), cnt)
    print()


# the g1_lift scenario: hero (100,100) facing east, crate at (130,100): 10 ticks east, the Square edge, then east.
go("lift a crate east then walk east", (100, 100), (130, 100), [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (79, gl.RIGHT)])
# the crate NORTH of the hero (lift from behind): hero faces up and presses Square against the crate.
go("lift a crate standing north (the hero faces up)", (100, 130), (100, 100), [(10, gl.UP), (1, gl.UP | gl.SQUARE), (79, gl.UP)], direction=16)
# the crate SOUTH of the hero.
go("lift a crate standing south (the hero faces down)", (100, 100), (100, 130), [(10, gl.DOWN), (1, gl.DOWN | gl.SQUARE), (79, gl.DOWN)], direction=0)
