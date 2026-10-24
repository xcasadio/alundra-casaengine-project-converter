"""c4_climb_lift.py - E20.g T0, written 2026-10-24 (the fourth scenario of the plan, after the critic's c1, c2, c3): Square pressed in the climb state (0x0E, anim_t 14) with a liftable in contact.
The binary calls TryUseItem then PlayerTryAction in the climb state (0x800321D4-0x800321E4); PlayerTryAction lifts when the contact word hero.+0x130 holds a liftable.
Prediction written before the run: with the contact, the crate is lifted (hero.+0x2C != 0) and anim_t is 5 or 11; without the contact, nothing is lifted and the climb goes on (anim_t 14).
Harness writes: the cell field of g17 (slope-6 ladder), the crate (REAL map-record spawner of map 392 sprite 36) at (122,104), the pad words, and ONE poke per row A: hero.+0x130 := the crate entity,
at the tick of the Square edge (the climb never steps into a crate by itself: UP|RIGHT stays a walk and UP alone does not touch the crate, see x5 of the session scratchpad, so the contact of the
previous step cannot be produced by the pad).
Rows: A = contact poked at the Square tick (climb, t72) ; B = same Square, no poke ; C = contact poked, Square at a later climb tick (t80) ; D = contact poked, no climb (idle on the ladder, t62) as the reference.
Run: python -B -I c4_climb_lift.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl
import ana
import k0lib as K


def cells(col, row):
    if 2 <= col <= 6 and row in (6, 7):
        return dict(gp=0x0C)
    if 2 <= col <= 6 and row == 5:
        return dict(h=3)
    return None


def row(tag, sq, poke, climb=True):
    m = gl.world(cells=cells, hero_px=(100, 108), direction=16)
    c = gl.add_crate(m, 122, 104)
    seq = [(60, 0), (sq - 61, gl.UP if climb else 0), (1, (gl.UP if climb else 0) | gl.SQUARE), (8, gl.UP if climb else 0)]
    pads, n = gl.pad_script(seq)
    pre = (lambda mm, t: mm.poke(gl.HERO + 0x130, K.ent(c)) if t == sq else None) if poke else None
    rows = gl.run(m, n, pads, crates=[c], pre=pre)
    first = ana.first_tick(rows, lambda r: r['hero']['carried2C'] != 0)
    h = rows[sq - 2]['hero']
    after = rows[sq - 1]['hero']
    return ('%s: Square edge t%d, poke=%s ; before: anim_t %d z %.2f ; lifted at tick %s ; hero anim_t at t%d %d, at t%d %d ; hero.+0x2C %X ; changes %s' %
            (tag, sq, poke, h['anim_t'], h['pz'] / 65536, first, sq, after['anim_t'], sq + 3, rows[min(sq + 2, n - 1)]['hero']['anim_t'], rows[-1]['hero']['carried2C'], ana.changes(rows, 'hero', ['anim_t', 'carried2C'], first=sq - 1)[:6]))


if __name__ == '__main__':
    out = [row('A climb, contact', 72, True), row('B climb, no contact', 72, False), row('C climb later, contact', 80, True), row('D idle on the ladder, contact', 62, True, climb=False)]
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'c4-climb-lift.txt'), 'w').write('\n'.join(out) + '\n')
