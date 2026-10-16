"""pass_trace.py - O-F3-6: which PASS creates, steps, flags and frees the hit effect slot.
For the hit of tick T = 8 (hero attacker, Beannoid target; nibble 6 -> effect 4, nibble 7 -> effect 5; binary order) the tracked slot is read AFTER EACH PASS of the ticks
T-1, T, T+1 and of the ticks around the end of its life (S-1 .. S+2 where S = 40 / 95). 'pass' names: the eight passes of 0x8003B388 then 'slotpass 0x8003C410'.
usage: python -B pass_trace.py   (writes out/pass_trace.txt)
"""
import sys
import os

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fxlib as F      # noqa: E402
import world as W      # noqa: E402
import run_hit_effect as R   # noqa: E402

NAMES = {0x80038634: 'slots 0x80038634', 0x800386D0: 'events 0x800386D0', 0x80038998: 'counters 0x80038998', 0x800384F4: 'lists 0x800384F4', 0x80038E18: 'animation 0x80038E18',
         0x80038364: 'physics 0x80038364', 0x80038E84: 'entity-effects 0x80038E84', 0x80039300: 'HIT 0x80039300', 0x8003C410: 'SLOT PASS 0x8003C410'}


def trace(nib, effect, S):
    m, meta = F.build()
    pad = W.Pad()
    T = R.T_HIT
    lines = ['## nibble %d -> effect %d (S = %d), hit tick T = %d; slot state after each pass ("-" = no slot of this effect on the target yet)' % (nib, effect, S, T)]
    windows = set([T - 1, T, T + 1, T + 2] + list(range(T + S - 2, T + S + 3)))
    tracked = None
    for t in range(1, T + S + 4):
        m.tick_no = t
        m.poke(F.DISPLAY_PTR, F.DISPLAY_LIST)
        m.poke(F.DISPLAY_COUNT, 0)
        F.pad_tick_prefix(m, pad)
        saved = None
        for a in list(W.PASSES) + [0x8003C410]:
            if a == 0x80039300 and t == T:
                saved = R.inject(m, R.HERO, R.BEAN, nib)
            W.run_pass(m, a)
            if a == 0x80039300 and t == T:
                R.restore_attacker(m, saved)
                for i in F.active_slots(m):
                    s = F.slot_read(m, i)
                    if s['id'] == effect and s['anchor'] == R.BEAN and tracked is None:
                        tracked = i
            if t in windows:
                if tracked is None:
                    lines.append('tick %3d d=%+4d after %-26s : -' % (t, t - T, NAMES[a]))
                else:
                    s = F.slot_read(m, tracked)
                    o, w = F.frame_ordinal(m, s)
                    lines.append('tick %3d d=%+4d after %-26s : slot %d status=%d counter=%3d done=%d frame#=%s drawn=%s' % (
                        t, t - T, NAMES[a], tracked, s['status'], s['counter'], s['done'], o, (F.slot_addr(tracked) + 0xC) in F.display_entries(m)[2]))
    return lines


def main():
    out = []
    for nib, effect, S in ((6, 4, 40), (7, 5, 95)):
        out += trace(nib, effect, S)
        out.append('')
    open(os.path.join(R.OUT, 'pass_trace.txt'), 'w').write('\n'.join(out) + '\n')
    print('\n'.join(out))


if __name__ == '__main__':
    main()
