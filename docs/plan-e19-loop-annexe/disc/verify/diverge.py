"""Where do the binary-exact and analyser-exact drivers diverge (track 14), and which tracks have the LS event one
frame after the 99=20 marker."""
import sys
from seqsim import run

for n in [int(a) for a in sys.argv[1:]] or [14]:
    _, b = run(n, "bin")
    _, a = run(n, "ana")
    eb = [(f, p, k) for f, p, k, _ in b.ev]
    ea = [(f, p, k) for f, p, k, _ in a.ev]
    for i, (x, y) in enumerate(zip(eb, ea)):
        if x != y:
            print(n, "first divergence at event", i, "bin", x, "ana", y)
            # context: tempo changes near that frame
            fr = min(x[0], y[0])
            print("  bin tempos near:", [t for t in b.tempos if fr - 3 <= t[0] <= fr + 1])
            print("  ana tempos near:", [t for t in a.tempos if fr - 3 <= t[0] <= fr + 1])
            print("  bin events in frame", fr, [e for e in eb if e[0] == fr][:12])
            print("  ana events in frame", fr, [e for e in ea if e[0] == fr][:12])
            break
    else:
        print(n, "no divergence over", len(eb), len(ea))

print("marker frame vs loop-position event frame:")
for n in range(1, 47):
    _, s = run(n, "bin", njumps=1)
    if s.ls_marker:
        if s.ls_marker[0] != s.loop_first:
            print("  track", n, "marker frame", s.ls_marker[0], "LS", s.loop_first)
