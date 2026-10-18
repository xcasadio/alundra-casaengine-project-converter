"""states_herb.py - E20.h1 discovery: from which hero states does Circle drink an instant item?  For EVERY state s of the table 0x80022D3C (target animation +0x88 = s, 0x00-0x4F), on the ground and in the air,
the Herb (0x24) equipped with 3 of them, life 3/10, Circle pressed for one tick on the real MovePlayer: "drank" (count 3 -> 2, life up, sound 0x31), "refused" (sound 3), "none" (neither).
A copy of the method of e20-h3/critic/run/scen_allstates.py (bomb) with the herb: the herb start checks neither the carried object nor the animation nor the slope.
The state of the bench (flat ground, no ladder, no climbing) is the one of the critic; the states whose own handler needs a context (ladder, climbing) are not explored beyond what the tick does.
usage: python -B states_herb.py [item_hex]"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'h4o'))
import spellworld as S      # noqa: E402
import world as W           # noqa: E402

HERO = W.ent(0)


def trial(m, snap, STATS, item, s, air):
    m.ram[:] = snap[0]
    m.init[:] = snap[1]
    m.journal.clear()
    m.hi = m.lo = 0
    m.poke(HERO + 0x88, s)
    m.poke(HERO + 0x90, s)
    if air:
        m.poke(HERO + 0x144, 0)
        m.poke(HERO + 0x11C, 40 << 16)
    pad = W.Pad()
    m.tick_no = 1
    W.tick(m, pad, 0, check_pad=False)
    m.poke(HERO + 0x88, s)
    m.poke(HERO + 0x14, 3)
    m.poke(HERO + 0x18, 10)
    inv = m.peek(0x8017631C)
    before = m.peek(inv + 4 * item) >> 16 & 0xFFFF
    m.journal.clear()
    m.tick_no = 2
    W.tick(m, pad, W.CIRCLE, check_pad=False)
    snd = [x[2] for x in m.journal if x[1] == 'sound']
    after = m.peek(inv + 4 * item) >> 16 & 0xFFFF
    drank = after == before - 1
    return drank, (3 in snd), snd, m.peeks(HERO + 0x88), m.peeks(HERO + 0x14)


def main(item):
    m, STATS, placed = S.make(item=item, mp=1, mpmax=4, indices=(1, 2), extra_items=[item, item])
    snap = (bytes(m.ram), bytes(m.init))
    for air in (False, True):
        ok, refused, none = [], [], []
        for s in range(0, 0x50):
            try:
                drank, ref, snd, after, hp = trial(m, snap, STATS, item, s, air)
            except Exception as ex:           # a state whose handler needs a context the bench lacks
                none.append('%02x(EXC %s)' % (s, str(ex)[:24]))
                continue
            if drank:
                ok.append(s)
            elif ref:
                refused.append(s)
            else:
                none.append(s)
        print('air' if air else 'ground', 'DRINKS from %d states:' % len(ok), ['%02x' % x for x in ok])
        print('   refused with sound 3:', ['%02x' % x for x in refused])
        print('   no reaction (%d):' % len(none), ['%02x' % x if isinstance(x, int) else x for x in none])


if __name__ == '__main__':
    main(int(sys.argv[1], 16) if len(sys.argv) > 1 else 0x24)
