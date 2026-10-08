"""For every iteration-1 note-on whose CC7/CC10 differs from iteration 2: offset (frames from loop start), and
whether the CC rewrite of that channel happens in the same frame (after the note in stream order) or later."""
import sys
from seqsim import run

for n in [int(a) for a in sys.argv[1:]] or range(1, 47):
    _, s = run(n, "bin", njumps=2)
    if not s.jumps:
        continue
    i0 = s.entry_evi
    i1, i2 = s.jump_evi
    it1, it2 = s.ev[i0:i1], s.ev[i1:i2]
    rows = []
    for x, y in zip(it1, it2):
        if x[2] == "on" and y[2] == "on":
            for idx, nm in ((3, 7), (4, 10)):
                if x[3][idx] != y[3][idx]:
                    ch = x[3][0]
                    # first rewrite of (ch, nm) in iteration 1 at or after this note
                    rw = None
                    for z in it1:
                        if z[2] == "cc" and z[3][0] == ch and z[3][1] == nm and z[1] != x[1]:
                            if z[0] >= x[0]:
                                rw = z[0]
                                break
                    rows.append((x[0] - s.loop_first, ch, "cc%d" % nm, x[3][idx], y[3][idx],
                                 "same frame" if rw == x[0] else ("rewrite +%s" % (None if rw is None else rw - s.loop_first))))
    if rows:
        same = sum(1 for r in rows if r[5] == "same frame")
        print(n, "notes", len(rows), "same-frame", same, rows[:4] if len(rows) < 5 else rows[:2] + ["..."] + rows[-1:])
