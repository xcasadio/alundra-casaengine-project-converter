"""a3_routes.py - disc-e20g3 A3: named routes through the barricade of map 392 (real geometry, real MovePlayer, real C23/E2, real spawner) -> out/a3_routes.txt (+ detail tables out/a3_route_*.txt).
Routes (hero comes from the east, the story direction, unless 'west'):
  R1  walk west along the doorway's north wall from the east room: where does the hero stop, which crate is the contact (+0x130)?
  R2  jump onto rec5 (running jump) and walk west over it; drop on the far side
  R3  lift rec5 (Square at contact), carry it, walk west through the gap; then throw it (Square) west; keep walking
  R4  lift the lower crate of the row-19 stack (rec6) at y = 312; the upper crate (rec10) is watched
  R5  lift rec5 with the hero at y = 298 (hero overlaps rec5 AND rec6 in Y): which crate is the contact, does the hero fit through afterwards?
  R6  from the west: running jump onto rec5, walk east over it, drop into the doorway
  R7  hero stands on rec5 (R2) and pushes south into the stack of row 19: which crate is the contact, can he lift it from there?
Each route prints the events by tick; nothing is assumed."""
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L

OUT = []


def P(s=''):
    OUT.append(s)
    print(s, flush=True)


def recname(S):
    return {L.K.ent(s): 'rec%d' % r for r, s in S.items()}


def hero_line(r, nm):
    H = r['hero']
    return 't%03d pad=%04X anim=%d x=%.3f y=%.3f z=%.3f fx=%.3f fz=%.3f ground=%d carried=%s xcoll=%s support=%s' % (
        r['tick'], r['hold'], H['anim_t'], H['px'] / 65536, H['py'] / 65536, H['pz'] / 65536, H['fx'] / 65536, H['fz'] / 65536, H['ground'],
        nm.get(H['carried2C'], hex(H['carried2C']) if H['carried2C'] else '0'), nm.get(H['xcoll'], hex(H['xcoll']) if H['xcoll'] else '0'),
        nm.get(H['support'], hex(H['support']) if H['support'] else '0'))


def crate_line(r, s, nm_rec):
    d = r['c'][s]
    return '%s(st=%d t24=%d carrier=%s x=%.2f y=%.2f z=%.2f fz=%.2f ground=%d flags=%X)' % (nm_rec, d['status'], d['t24'], hex(d['carrier28']) if d['carrier28'] else '0', d['px'] / 65536, d['py'] / 65536, d['pz'] / 65536, d['fz'] / 65536, d['ground'], d['flags6C'])


def run(hero_px, direction, seq, y=None, rec_ids=(4, 5, 6, 7, 8, 10, 11), watch=None):
    m, S = L.world392(hero_px=hero_px, direction=direction, rec_ids=rec_ids)
    h = L.K.ent(0)
    if y is not None:
        m.poke(h + 0x118, int(round(y * 65536)))
    pads, n = L.pad_seq(seq)
    rows = L.gl.run(m, n, pads, crates=list(S.values()))
    return m, S, rows


def first(rows, pred):
    for r in rows:
        if pred(r):
            return r['tick']
    return None


def main():
    # ---- R1 -----------------------------------------------------------------------------------------------------------------
    P('== R1  walk west along the north wall of the doorway (start 720, 291 -> pushed to the wall) and at y = 296, 312')
    for y0 in (291.0, 295.0, 296.0, 297.0, 298.0, 304.0, 312.0, 328.0):
        m, S, rows = run((720, 296), 8, [(60, L.LEFT)], y=y0)
        nm = recname(S)
        t = first(rows, lambda r: r['hero']['xcoll'] != 0)
        r = rows[-1]
        P('  start y=%.1f : first contact tick %s with %s ; final x=%.3f y=%.3f z=%.3f' % (y0, t, nm.get(rows[t - 1]['hero']['xcoll']) if t else None, r['hero']['px'] / 65536, r['hero']['py'] / 65536, r['hero']['pz'] / 65536))
    # ---- R2 -----------------------------------------------------------------------------------------------------------------
    P('== R2  running jump onto rec5, east to west (start 720,296, jump tick tj = 14)')
    m, S, rows = run((720, 296), 8, [(13, L.LEFT), (1, L.LEFT | L.CROSS), (60, L.LEFT)], y=296.0)
    nm = recname(S)
    t_land = first(rows, lambda r: r['hero']['support'] == L.K.ent(S[5]) and r['hero']['ground'])
    t_off = first(rows, lambda r: r['tick'] > (t_land or 0) and r['hero']['support'] == 0)
    P('  lands on rec5 at tick %s (z=%.3f), leaves it at tick %s, final x=%.3f z=%.3f' % (t_land, rows[t_land - 1]['hero']['pz'] / 65536, t_off, rows[-1]['hero']['px'] / 65536, rows[-1]['hero']['pz'] / 65536))
    open(os.path.join(L.OUT, 'a3_route_R2.txt'), 'w').write('\n'.join(hero_line(r, nm) for r in rows) + '\n')
    # ---- R3 -----------------------------------------------------------------------------------------------------------------
    P('== R3  lift rec5 from the east at y = 296, carry it west through the gap, throw it west')
    m, S, rows0 = run((720, 296), 8, [(60, L.LEFT)], y=296.0)
    t_contact = first(rows0, lambda r: r['hero']['xcoll'] != 0)
    P('  contact first seen at tick %s' % t_contact)
    seq = [(t_contact + 1, L.LEFT), (1, L.LEFT | L.SQUARE), (60, L.LEFT), (1, L.LEFT | L.SQUARE), (40, 0)]
    m, S, rows = run((720, 296), 8, seq, y=296.0)
    nm = recname(S)
    t_lift = first(rows, lambda r: r['hero']['carried2C'] != 0)
    P('  Square edge at tick %d ; hero carries %s from tick %s (state %s)' % (t_contact + 2, nm.get(rows[t_lift - 1]['hero']['carried2C']) if t_lift else None, t_lift, rows[t_lift - 1]['hero']['anim_t'] if t_lift else None))
    t_throw = first(rows, lambda r: r['hero']['carried2C'] == 0 and r['tick'] > (t_lift or 0))
    P('  throw (carried cleared) at tick %s ; hero x at lift %.3f, at throw %.3f, final x=%.3f y=%.3f z=%.3f' % (t_throw, rows[t_lift - 1]['hero']['px'] / 65536, rows[t_throw - 1]['hero']['px'] / 65536 if t_throw else -1, rows[-1]['hero']['px'] / 65536, rows[-1]['hero']['py'] / 65536, rows[-1]['hero']['pz'] / 65536))
    xs_blocked = [r['tick'] for r in rows if r['tick'] > (t_lift or 0) and r['hero']['xcoll'] != 0][:3]
    P('  ticks with a contact after the lift: %s' % xs_blocked)
    open(os.path.join(L.OUT, 'a3_route_R3.txt'), 'w').write('\n'.join(hero_line(r, nm) + ' | ' + crate_line(r, S[5], 'rec5') for r in rows) + '\n')
    # ---- R4 -----------------------------------------------------------------------------------------------------------------
    P('== R4  lift the lower crate rec6 of the row-19 stack at y = 312 ; watch the upper crate rec10')
    m, S, rows0 = run((720, 312), 8, [(60, L.LEFT)], y=312.0)
    t_contact = first(rows0, lambda r: r['hero']['xcoll'] != 0)
    nm = recname(S)
    P('  contact at tick %s with %s' % (t_contact, nm.get(rows0[t_contact - 1]['hero']['xcoll'])))
    seq = [(t_contact + 1, L.LEFT), (1, L.LEFT | L.SQUARE), (60, 0)]
    m, S, rows = run((720, 312), 8, seq, y=312.0)
    t_lift = first(rows, lambda r: r['hero']['carried2C'] != 0)
    up = S[10]
    z_up = [(r['tick'], round(r['c'][up]['pz'] / 65536, 2)) for r in rows if t_lift - 1 <= r['tick'] <= t_lift + 9]
    P('  lift tick %s carried=%s ; upper crate rec10 z by tick %s' % (t_lift, nm.get(rows[t_lift - 1]['hero']['carried2C']), z_up))
    last = rows[-1]
    P('  at the end rec10: z=%.3f ground=%d support=%s, flags=%X ; hero carried=%s' % (last['c'][up]['pz'] / 65536, last['c'][up]['ground'], nm.get(last['c'][up]['support'], hex(last['c'][up]['support'])), last['c'][up]['flags6C'], nm.get(last['hero']['carried2C'])))
    # ---- R5 -----------------------------------------------------------------------------------------------------------------
    P('== R5  lift at y = 298 (overlaps rec5 [289,304) and rec6/rec10 [305,320) in Y): which crate is the contact, can the hero then pass?')
    m, S, rows0 = run((720, 298), 8, [(60, L.LEFT)], y=298.0)
    nm = recname(S)
    t_contact = first(rows0, lambda r: r['hero']['xcoll'] != 0)
    P('  contact at tick %s with %s (hero y %.3f)' % (t_contact, nm.get(rows0[t_contact - 1]['hero']['xcoll']), rows0[t_contact - 1]['hero']['py'] / 65536))
    seq = [(t_contact + 1, L.LEFT), (1, L.LEFT | L.SQUARE), (80, L.LEFT)]
    m, S, rows = run((720, 298), 8, seq, y=298.0)
    t_lift = first(rows, lambda r: r['hero']['carried2C'] != 0)
    P('  lifted %s at tick %s ; 80 ticks later hero x=%.3f y=%.3f contact=%s' % (nm.get(rows[t_lift - 1]['hero']['carried2C']), t_lift, rows[-1]['hero']['px'] / 65536, rows[-1]['hero']['py'] / 65536, nm.get(rows[-1]['hero']['xcoll'])))
    # ---- R6 -----------------------------------------------------------------------------------------------------------------
    P('== R6  from the WEST: running jump onto rec5 at y = 296 (start 560, 296), walk east over it, drop into the doorway')
    res = []
    for tj in range(8, 22):
        m, S, rows = run((560, 296), 24, [(tj - 1, L.RIGHT), (1, L.RIGHT | L.CROSS), (70 - tj, L.RIGHT)], y=296.0)
        nm = recname(S)
        t_land = first(rows, lambda r: r['hero']['support'] == L.K.ent(S[5]) and r['hero']['ground'])
        res.append((tj, t_land, round(rows[-1]['hero']['px'] / 65536, 1), round(rows[-1]['hero']['pz'] / 65536, 1), nm.get(rows[-1]['hero']['xcoll'])))
    P('  (tj, tick of landing on rec5, final x, final z, final contact) ' + str(res))
    # ---- R7 -----------------------------------------------------------------------------------------------------------------
    P('== R7  hero on rec5 pushes south into the row-19 stack: contact and lift')
    seq = [(13, L.LEFT), (1, L.LEFT | L.CROSS), (17, L.LEFT), (5, 0), (6, L.DOWN), (1, L.DOWN | L.SQUARE), (40, 0)]
    m, S, rows = run((720, 296), 8, seq, y=296.0)
    nm = recname(S)
    for r in rows:
        if r['tick'] in (30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45):
            P('  ' + hero_line(r, nm))
    open(os.path.join(L.OUT, 'a3_routes.txt'), 'w', encoding='utf-8').write('\n'.join(OUT) + '\n')


if __name__ == '__main__':
    main()
