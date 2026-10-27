"""E20.g1d T0 (c): crate_keys. Derived from the discovery's crate_keys.py: map 392 record sprite 36 (collidable, IDSV 7), three lifts (east, from the north, from the south),
EVERY tick printed in the common format. Oracle: copy of docs/plan-e20-annexe/e20-g1a/work/orc in $G1D_ORC/oracle_g1a."""
import os
import sys
import keyrows as K
O = os.path.join(K.ORC, 'oracle_g1a')
sys.path.insert(0, O)
sys.path.insert(0, os.path.join(O, 'oracle'))
import gl  # noqa: E402
import world as W  # noqa: E402
import k0lib as KL  # noqa: E402

rows = []


def make_post(crate_slot):
    def post(m, t, r):
        W.run_pass(m, 0x800399B8)
        rows.append((t, K.fields(m, gl.HERO), K.fields(m, KL.ent(crate_slot))))
    return post


def go(label, hero_px, crate_px, seq, direction=24, n=90):
    rows.clear()
    m = gl.world(hero_px=hero_px, direction=direction)
    c = gl.add_crate(m, *crate_px)
    pads, nn = gl.pad_script(seq)
    gl.run(m, nn if nn else n, pads, crates=[c], post=make_post(c))
    for t, h, o in rows:
        print(K.line(label, t, h, o))


print(K.HEADER)
go("crate_east", (100, 100), (130, 100), [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (79, gl.RIGHT)])
go("crate_north", (100, 130), (100, 100), [(10, gl.UP), (1, gl.UP | gl.SQUARE), (79, gl.UP)], direction=16)
go("crate_south", (100, 100), (100, 130), [(10, gl.DOWN), (1, gl.DOWN | gl.SQUARE), (79, gl.DOWN)], direction=0)
