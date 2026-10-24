"""a1_scen.py - disc-e20g3 A1: whole-tick scenarios (real tick passes of 0x8003B388, real MovePlayer / MoveEntity / lists / physics) that show which movers a liftable crate blocks.
 S1  a non-hero walker (Beannoid of map 14, sprite 79, driven by the harness with animation 7 = the walk) moves west into crate rec5 of map 392 (real spawner, real geometry):
       (a) header flags as they are (MoreFlags 0x03: Collidable bit CLEAR)   -> passes through the crate
       (b) the harness sets +0x6C |= 0x80 (what a Collidable header gives)     -> stops against the crate, its +0x130 names the crate
 S2  a Collidable walker, then the crate is lifted by the hero (carried: +0x28 set): the walker is no longer stopped -> passes through the carried crate
 S3  hero carries rec5 and walks into rec6 at y = 312 (hero overlaps only the lower crate): the carried crate does not stop the hero; rec6 does.
Each prints per-tick positions for the walker/hero; the counts of the collidable list (0x80127D2C) are printed too."""
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L
from machine import M32

OUT = []


def P(s=''):
    OUT.append(s)
    print(s, flush=True)


def beannoid(m, x, y, collidable, anim=7, dir_t=8):
    pl, _ = L.K.spawn_record_entities(m, 'map_14', 79, [(10, 11, 0)], rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    p = pl[0]
    pe = L.K.ent(p)
    L.K.poke_entity(m, p, px=int(x * 65536), py=int(y * 65536))
    if collidable:
        m.poke(pe + 0x6C, m.peek(pe + 0x6C) | 0x80)
    m.poke(pe + 0x1D8, (int(x * 65536) + m.peeks(pe + 0x1E4)) & M32)
    m.poke(pe + 0x1DC, (int(y * 65536) + m.peeks(pe + 0x1E8)) & M32)
    T = m.call(0x800370C4, pe)
    m.poke(pe + 0x138, T)
    m.poke(pe + 0x11C, T + 1)
    m.poke(pe + 0x1E0, T + 1 + m.peeks(pe + 0x1EC))
    L.K.poke_entity(m, p, anim_t=anim, dir_t=dir_t)
    return p


def s1(collidable):
    m, S = L.world392(hero_px=(560, 330), direction=24, rec_ids=(5,))
    p = beannoid(m, 700, 296, collidable)
    pads, n = L.pad_seq([(45, 0)])
    rows = L.gl.run(m, n, pads, crates=[S[5]], extra=[p])
    xs = [round(r['c'][p]['px'] / 65536, 3) for r in rows]
    xc = [r['c'][p]['xcoll'] for r in rows]
    cnt = [r['lists']['coll'] for r in rows][0]
    first_c = next((r['tick'] for r in rows if r['c'][p]['xcoll']), None)
    P('  walker header flags %s: +0x6C=%X ; collidable-list count %d ; x at ticks 1,10,20,30,45 = %s ; first tick with a contact %s -> %s ; final z=%.2f' % (
        'with 0x80 (harness)' if collidable else 'as in the record', rows[0]['c'][p]['flags6C'], cnt, [xs[i] for i in (0, 9, 19, 29, 44)], first_c,
        ('crate rec5' if rows[first_c - 1]['c'][p]['xcoll'] == L.K.ent(S[5]) else hex(rows[first_c - 1]['c'][p]['xcoll'])) if first_c else None, rows[-1]['c'][p]['pz'] / 65536))
    return m, S, p, rows


def s2():
    """list membership around the lift and the throw of rec5 (hero at y = 296, real C23 / E2 / TryLift)"""
    m, S = L.world392(hero_px=(720, 296), direction=8, rec_ids=(4, 5, 6))
    slot5 = S[5]
    ent_to_slot = {L.K.ent(i): i for i in range(64)}

    def post(m_, t, r):
        n = m_.peek(0x80127D2C)
        r['members'] = [ent_to_slot[m_.peek(0x80127C28 + 4 * k)] for k in range(n)]
    seq = [(28, L.LEFT), (1, L.LEFT | L.SQUARE), (60, L.LEFT), (1, L.LEFT | L.SQUARE), (40, 0)]
    pads, n = L.pad_seq(seq)
    rows = L.gl.run(m, n, pads, crates=list(S.values()), post=post)
    tl = next(r['tick'] for r in rows if r['hero']['carried2C'])
    tt = next(r['tick'] for r in rows if r['tick'] > tl and r['hero']['carried2C'] == 0)
    P('  slots: hero 0, rec4 slot %d, rec5 slot %d, rec6 slot %d' % (S[4], S[5], S[6]))
    for t in (tl - 1, tl, tl + 1, tt - 1, tt, tt + 1, tt + 30):
        r = rows[t - 1]
        P('  tick %3d collidable list (slots) %s   rec5: status=%d +0x28=%s flags=%X anim=%d' % (t, r['members'], r['c'][slot5]['status'], hex(r['c'][slot5]['carrier28']) if r['c'][slot5]['carrier28'] else '0', r['c'][slot5]['flags6C'], r['c'][slot5]['anim_t']))
    return rows


def main():
    P('== S1  walker vs crate rec5 (y=296), Beannoid of map 14 walking west from x=700')
    s1(False)
    s1(True)
    P('== S2  collidable list membership of rec5 around the lift (tick %d: Square edge) and the throw' % 29)
    s2()
    open(os.path.join(L.OUT, 'a1_scen.txt'), 'w', encoding='utf-8').write('\n'.join(OUT) + '\n')


if __name__ == '__main__':
    main()
