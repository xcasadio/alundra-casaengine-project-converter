"""D-E19-93: for several answers, the frame where slot 3 writes the result word and the frame where slot 10's REAL poll accepts it (flags 0x800C4198: 8 -> 2),
and the first slot-3 update after the opener (real code, dispatcher order). Frames are main-loop iterations: RenderScene (the dispatcher) then Update(0)."""
import slotorder as so

CROSS, RIGHT = 0x40, 0x2000
OPEN_AT = 5                       # S: the frame where slot 10's real code sees Cross (by-interval) and opens the question


def scenario(name, presses):
    seen = lambda f: presses.get(f, 0)
    m, rows = so.run(seen, 120)
    first3 = written = accepted = None
    for f, pw, log, ev in rows:
        for slot, b, a, n in log:
            if slot == 3 and first3 is None and f > OPEN_AT:
                first3 = f
            if slot == 3 and b[0] == 0 and a[0] != 0 and written is None:
                written = f
            if slot == 10 and b[1] & 8 and (a[1] & 8) == 0 and a[1] & 2 and accepted is None:
                accepted = f
    print('%-34s opener S=%d, first slot-3 pass %s (S+%s), result written %s (S+%s), slot 10 accepts %s (S+%s)  -> same frame: %s' % (
        name, OPEN_AT, first3, first3 - OPEN_AT, written, written - OPEN_AT, accepted, accepted - OPEN_AT, written == accepted))


scenario('OUI at the earliest (Cross S+19)', {OPEN_AT: CROSS, OPEN_AT + 19: CROSS})
scenario('NON at the earliest (Right S+19, Cross S+20)', {OPEN_AT: CROSS, OPEN_AT + 19: RIGHT, OPEN_AT + 20: CROSS})
scenario('OUI late (Cross S+30)', {OPEN_AT: CROSS, OPEN_AT + 30: CROSS})
scenario('NON late (Right S+25, Cross S+31)', {OPEN_AT: CROSS, OPEN_AT + 25: RIGHT, OPEN_AT + 31: CROSS})
