"""cast1.py - first experiment (h4): the hero with a tome, Circle pressed at tick 1, real MovePlayer + real driver, SpawnWarpEntity stubbed.
Writes nothing outside its own folder (prints to stdout)."""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from machine import Machine, s32
import world as W
import oracle as O


def setup_cast(item, mp=5, mpmax=5, weapon=1, seed=0x12345678, extra_items=()):
    m, meta = O.setup(seed=seed)
    STATS = m.peek(0x80176318)
    # add the tome(s) and extra items with the real AddItem 0x8004E530
    m.call(0x8004E530, item)
    for it in extra_items:
        m.call(0x8004E530, it)
    # stats: +4 Mp, +6 MpMax, +0xA weapon slot, +0xC current item id
    m.poke16(STATS + 4, mp)
    m.poke16(STATS + 6, mpmax)
    if weapon is not None:
        m.call(0x8004E484, weapon)
    m.poke16(STATS + 0xC, item)
    return m, STATS


def run(item, nticks=420, **kw):
    m, STATS = setup_cast(item, **kw)
    pad = W.Pad()
    rows = []
    P = W.ent(0)
    for t in range(1, nticks + 1):
        hold = W.CIRCLE if t == 1 else 0
        m.tick_no = t
        W.tick(m, pad, hold)
        rows.append((t, m.peeks(P + 0x88), m.peeks(P + 0x90), m.peeks(0x80127164), m.peeks(0x80127160), m.peeks(0x8012715C),
                     s32(m.peek(STATS + 4) & 0xFFFF) if False else (m.peek(STATS) >> 16) & 0xFFFF))
    return m, STATS, rows


if __name__ == '__main__':
    item = int(sys.argv[1], 0) if len(sys.argv) > 1 else 0x2B
    m, STATS, rows = run(item)
    print('MP after (halfword +4):', m.peek(STATS + 4) & 0xFFFF, ' hero stats ptr', hex(STATS))
    last = None
    for r in rows:
        key = (r[1], r[2], r[3], r[4])
        if key != last:
            print('tick %3d  target_anim %#x cur_anim %#x  item_in_use %#x phase %d counter %d' % (r[0], r[1], r[2], r[3], r[4], r[5]))
            last = key
    print('journal:')
    for j in m.journal:
        if j[1] in ('sound', 'spawn_39F58', 'effect_3BDD8', 'effect_3BE74'):
            print(j)
