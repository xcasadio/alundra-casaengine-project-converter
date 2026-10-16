"""slot_dump.py - O-F3-6: the 0x80 bytes of the hit-effect slot (effect 4, nibble 6) right after 0x8003BE74 created it (inside the hit pass, tick T), after the first slot pass (tick T),
after the last step (tick T+40) and after its release (tick T+41). Non-zero words only, with the field name when decoded.
usage: python -B slot_dump.py   (writes out/slot_dump.txt)
"""
import sys
import os

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fxlib as F      # noqa: E402
import world as W      # noqa: E402
import run_hit_effect as R   # noqa: E402

NAMES = {0x00: 'slot index (kept by InitEffect)', 0x04: 'a1 = 0 (no record)', 0x08: 'bank pointer (table of the effect, written by the first step)', 0x0C: 'frame data pointer (bank + 2*word + 2)',
         0x10: 'display copy of X (0x8003C4A4-0x8003C4B4)', 0x14: 'display copy of Y', 0x18: 'display copy of Z', 0x1C: 'display copy of the depth key (+0x64)', 0x20: 'IDSV byte of the image set (struct +0x14)', 0x24: 'image count byte of the image set (struct +0x18)',
         0x28: 'lookup constant out #1 (0x0B for the global bank)', 0x2C: 'lookup constant out #2 (0x60 for the global bank)', 0x30: '-1 (a1 == 0)', 0x34: 'MODE (1 = attached to an anchor)',
         0x38: 'ANCHOR (the target entity)', 0x3C: 'X (anchor +0x114)', 0x40: 'Y (anchor +0x118)', 0x44: 'Z (anchor +0x11C)', 0x48: 'offset X', 0x4C: 'offset Y', 0x50: 'offset Z',
         0x60: 'DEPTH OFFSET (1)', 0x64: 'depth key (anchor +0x1BC + 0x60)', 0x68: 'STATUS (2 active, 0 free)', 0x6C: 'bytes: +6C selector(0 global) +6D(1) +6E effect id +6F (~id)',
         0x70: 'bytes: +70 sequence target +71 sequence current (~target until the first step) ', 0x74: 'sequence pointer', 0x78: 'sequence loop pointer', 0x7C: 'bytes: +7C COUNTER, +7D DONE flag'}


def dump(m, slot, title):
    a = F.slot_addr(slot)
    out = ['-- ' + title]
    for off in range(0, 0x80, 4):
        v = m.peek(a + off)
        if v:
            out.append('   +0x%02X = %08X   %s' % (off, v, NAMES.get(off, '')))
    return out


def main():
    m, meta = F.build()
    pad = W.Pad()
    T = R.T_HIT
    lines = ['effect 4 slot of a nibble-6 hit on the Beannoid (hero attacker), tick T = %d' % T]
    tracked = None
    for t in range(1, T + 43):
        m.tick_no = t
        m.poke(F.DISPLAY_PTR, F.DISPLAY_LIST)
        m.poke(F.DISPLAY_COUNT, 0)
        F.pad_tick_prefix(m, pad)
        saved = None
        for a in list(W.PASSES) + [0x8003C410]:
            if a == 0x80039300 and t == T:
                saved = R.inject(m, R.HERO, R.BEAN, 6)
            W.run_pass(m, a)
            if a == 0x80039300 and t == T:
                R.restore_attacker(m, saved)
                for i in F.active_slots(m):
                    s = F.slot_read(m, i)
                    if s['id'] == 4 and s['anchor'] == R.BEAN:
                        tracked = i
                lines += dump(m, tracked, 'after 0x8003BE74 (created in the hit pass of tick T), slot %d' % tracked)
            if tracked is not None and a == 0x8003C410 and t in (T, T + 40, T + 41):
                lines += dump(m, tracked, 'after the slot pass of tick T%+d' % (t - T))
    open(os.path.join(R.OUT, 'slot_dump.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))


if __name__ == '__main__':
    main()
