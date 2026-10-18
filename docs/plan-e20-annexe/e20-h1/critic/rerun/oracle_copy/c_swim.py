"""Critic experiment (run from a COPY of the discovery's harness): do the five instant items drink in DEEP WATER (cells word 0x0800 = slope 4), swim boots 0x1B (level 2)?
Method of states_herb.py / keys_h1.py (real MovePlayer, TryUseItem, dispatcher, starts, heals).  Prints per scenario: the animation and slope timelines, the life/magic/count, the sounds."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'h4o'))
import spellworld as S      # noqa: E402
import world as W           # noqa: E402

HERO = W.ent(0)
RIGHT, CIRCLE = W.RIGHT, W.CIRCLE


def make_water(m):
    for row in range(60):
        for col in range(52):
            a = W.TILES + 0x604 + row * W.ROW + col * 8
            m.poke16(a, 0x0800)
            m.poke8(a + 2, 0)
            m.poke8(a + 3, 0)


def run(item, hp, hpmax, mp, mpmax, water, boots, pad_fn, ticks, start_anim=None, label=''):
    m, STATS, placed = S.make(item=item, mp=mp, mpmax=mpmax, indices=(1, 2), extra_items=[0x1B] if boots else [])
    if water:
        make_water(m)
    m.poke(HERO + 0x14, hp)
    m.poke(HERO + 0x18, hpmax)
    if start_anim is not None:
        m.poke(HERO + 0x88, start_anim)
    pad = W.Pad()
    inv = m.peek(0x8017631C)
    rows = []
    for t in range(1, ticks + 1):
        m.journal.clear()
        m.tick_no = t
        W.tick(m, pad, pad_fn(t), check_pad=False)
        snd = [x[2] for x in m.journal if x[1] == 'sound']
        eff = [x for x in m.journal if x[1] == 'effect_3BDD8']
        rows.append(dict(t=t, anim=m.peeks(HERO + 0x88) & 0xFF, cur=m.peeks(HERO + 0x90) & 0xFF, slope=m.peeks(HERO + 0x18C), hp=m.peeks(HERO + 0x14),
                         mp=m.peek(STATS + 4) & 0xFFFF, count=m.peek(inv + 4 * item) >> 16 & 0xFFFF, snd=snd, neff=len(eff), z=m.peeks(HERO + 0x11C)))
    return rows


def show(label, rows, ticks_of_interest):
    print('--', label)
    print('   anim timeline :', ' '.join('%d:%02X' % (r['t'], r['anim']) for r in rows if r['t'] in ticks_of_interest))
    print('   slope timeline:', ' '.join('%d:%d' % (r['t'], r['slope']) for r in rows if r['t'] in ticks_of_interest))
    for r in rows:
        if r['snd'] or r['neff']:
            print('   tick %d: sounds %s effects %d hp %d mp %d count %d anim %02X slope %d z %d' % (r['t'], r['snd'], r['neff'], r['hp'], r['mp'], r['count'], r['anim'], r['slope'], r['z']))
    last = rows[-1]
    print('   final: hp %d mp %d count %d anim %02X slope %d' % (last['hp'], last['mp'], last['count'], last['anim'], last['slope']))


def main():
    HERB, LPOT, ELIX, ALE, SPOT = 0x24, 0x25, 0x26, 0x27, 0x29
    cases = [(HERB, 3, 10, 4, 4), (LPOT, 3, 10, 4, 4), (ELIX, 10, 10, 1, 4), (ALE, 3, 10, 1, 4), (SPOT, 3, 10, 4, 4)]
    interesting = {1, 2, 3, 5, 9, 10, 11, 12, 14, 19, 20, 21, 22, 30}
    for item, hp, hpmax, mp, mpmax in cases:
        # dry baseline, Circle at tick 10
        rows = run(item, hp, hpmax, mp, mpmax, False, True, lambda t: CIRCLE if t == 10 else 0, 30)
        show('item %02X DRY idle, Circle at 10' % item, rows, interesting)
        # deep water, boots 2, idle in the water, Circle at tick 10 (settled after the entry)
        rows = run(item, hp, hpmax, mp, mpmax, True, True, lambda t: CIRCLE if t == 10 else 0, 30)
        show('item %02X DEEP WATER (boots 2) idle, Circle at 10' % item, rows, interesting)
        # swimming right, Circle at tick 20
        rows = run(item, hp, hpmax, mp, mpmax, True, True, lambda t: (RIGHT | CIRCLE) if t == 20 else RIGHT, 40)
        show('item %02X DEEP WATER (boots 2) swimming right, Circle at 20' % item, rows, {1, 2, 3, 5, 10, 19, 20, 21, 22, 30, 40})


if __name__ == '__main__':
    main()
