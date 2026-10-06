"""D-E19-99 binary proof, by execution of the REAL code, with the binary's own main-loop order and the REAL pad sampler.
One main-loop frame (0x8002C3F4-0x8002C45C) = RenderScene (0x8002BD60: memory card process 0x8005EC98, dispatcher 0x80048054 = slots 0..12 ascending)
then Update(0) (0x8002BAEC), whose first call is the pad sampler 0x8002E38C (0x8002BB5C): BIOS PadRead (0x8008A688, hooked: returns the PHYSICAL
buttons of the frame) then the real pad update 0x8002E250 on block 0x80126F18 (hold +0x10, pressed +0x12, released +0x14, interval +0x16).
Here the pad is fed by a PHYSICAL schedule phys(f) = the buttons held when Update(0) of frame f samples; nothing is shifted by hand.
Slot 3 and slot 10 run their real code (slotorder.build/dispatcher); the memory card process is idle in this scenario."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import emu, drive, slotorder as so

CROSS, RIGHT, SQUARE = drive.CROSS, drive.RIGHT, drive.SQUARE
PADREAD = 0x8008A688
SAMPLER = 0x8002E38C


def make(phys):
    m = so.build()
    state = {'f': 0}
    m.hooks[PADREAD] = lambda mm: mm.__setitem__('_', 0) if False else _padread(mm, state, phys)
    return m, state


def _padread(m, state, phys):
    m.r[2] = phys(state['f']) & 0xFFFF       # PadRead returns the two pads in one word; pad 1 = low half


def run(phys, frames):
    m, state = make(phys)
    rows = []
    for f in range(1, frames + 1):
        state['f'] = f
        log = []
        m.log.clear()
        # --- RenderScene: the dispatcher (slots 0..12 ascending); reads the pad block as Update(0) of the previous frame left it
        pw_seen = drive.pad_words(m)
        so.dispatcher(m, log)
        ev = list(m.log)
        # --- Update(0): the real pad sampler, first thing
        m.call(SAMPLER)
        rows.append((f, pw_seen, log, ev))
    return m, rows


def milestones(rows, open_at=None):
    first3 = written = accepted = opened = None
    for f, pw, log, ev in rows:
        for slot, b, a, n in log:
            if slot == 10 and opened is None and (a[1] & 8) and not (b[1] & 8):
                opened = f
            if slot == 3 and first3 is None and opened is not None and f > opened:
                first3 = f
            if slot == 3 and b[0] == 0 and a[0] != 0 and written is None:
                written = f
            if slot == 10 and b[1] & 8 and (a[1] & 8) == 0 and a[1] & 2 and accepted is None:
                accepted = f
    return opened, first3, written, accepted


if __name__ == '__main__':
    P = 5                                   # physical frame of the opening Cross (held in Update(0) of frame P)
    print('physical Cross at frame %d opens the question in frame S = ?' % P)
    for q in range(P + 14, P + 24):
        phys = lambda f, q=q: CROSS if f in (P, q) else 0
        m, rows = run(phys, 90)
        o, f3, w, a = milestones(rows)
        print('  answer Cross physically at P+%2d (frame %d): slot 10 opens in frame %s (= P+%s), first slot-3 pass %s, result written %s, slot 10 accepts %s' % (q - P, q, o, None if o is None else o - P, f3, w, a))
