"""D-E19-93, binary proof by execution: the REAL code of slot 3 (the choice box, 0x800501FC chain) and of slot 10 (the file menu update 0x80058F24)
run in the dispatcher's own order (slots 0..12 ascending, 0x8004813C-0x80048174), frame by frame, on a pad sequence that goes through the real pad update
0x8002E250. Slot 10 starts idle; its REAL code opens the question when the by-interval word has Cross (0x800594C8-0x80059504, the opener 0x80050BA8, the result
word 0x80180124), then polls it (0x8005912C-0x80059150). Stubs: libc, sound, VRAM upload, cell init, the string lookup (as drive.py) and the text call 0x800472D0.
Prints, per frame: slot 3's update pointer, the result word before and after slot 3, slot 10's flags word 0x800C4198 before and after slot 10.
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import emu, drive

FLAGS10 = 0x800C4198        # 0x4198(0x800C0000): bit 8 = the OUI/NON question waits, bit 2 = the closing slide (set at 0x80059150)
RESULT10 = 0x80180124       # the file menu's result word
SLOT10 = 0x80153028 + 28 * 10
CROSS = drive.CROSS


def build():
    m = drive.new_machine()
    m.hooks[0x800472D0] = lambda mm: mm.log.append(('text', 0x800472D0))
    # runtime slot 10 as OpenSlot(10) would leave it after InitializeMemoryCardMenu (0x800583EC: sw 0x80058F24 to entry +0x14)
    tbl = 0x800A731C + 28 * 10
    for k in range(7):
        m.ww(SLOT10 + 4 * k, m.rw(tbl + 4 * k))
    m.wh(SLOT10, 1)
    m.ww(SLOT10 + 0x14, 0x80058F24)
    m.ww(FLAGS10, 0)
    m.ww(RESULT10, 0)
    return m


def dispatcher(m, log):
    """0x80048054's slot loop over the 13 runtime entries, ascending; records what slots 3 and 10 do."""
    for slot in range(13):
        a = 0x80153028 + 28 * slot
        if m.rh(a) & 1:
            m.ww(drive.SLOT_PTR, a)
            upd = m.rw(a + 0x14)
            if not upd:
                continue
            before = (m.rw(RESULT10), m.rw(FLAGS10), upd)
            note = ''
            try:
                m.call(upd, drive.SLOT_PTR)
            except RuntimeError as exc:   # slot 10's closing slide divides by the (zeroed) slide steps of this stand-alone state: after the poll accepted the word
                note = 'stopped at %08x (%s)' % (m.pc, exc)
            after = (m.rw(RESULT10), m.rw(FLAGS10), m.rw(a + 0x14))
            log.append((slot, before, after, note))


def run(seen, frames):
    m = build()
    rows = []
    for f in range(1, frames + 1):
        drive.pad_update(m, seen(f))          # the word sampled in the previous frame's Update(0): the boxes of frame f see it
        pw = drive.pad_words(m)
        log = []
        m.log.clear()
        dispatcher(m, log)
        rows.append((f, pw, log, list(m.log)))
    return m, rows


if __name__ == '__main__':
    OPEN_AT = 5
    PRESS_AT = OPEN_AT + 19                    # N + 19 = S + 19: the earliest interactive pass of slot 3
    seen = lambda f: CROSS if f in (OPEN_AT, PRESS_AT) else 0
    m, rows = run(seen, 50)
    print('frame  pad(pressed,interval)  slot calls (slot: result word before->after, flags 0x800C4198 before->after, update ptr)   sounds')
    for f, pw, log, ev in rows:
        if f < OPEN_AT - 1 or (f > OPEN_AT + 3 and f < PRESS_AT - 1) or f > PRESS_AT + 20:
            continue
        calls = '; '.join('slot%d: %x->%x flags %x->%x upd %08x %s' % (s, b[0], a[0], b[1], a[1], b[2], n) for s, b, a, n in log)
        snd = [e[1] for e in ev if e[0] == 'sound']
        print('%3d  %04x,%04x  %s  sounds=%s' % (f, pw['pressed'], pw['interval'], calls, snd))
