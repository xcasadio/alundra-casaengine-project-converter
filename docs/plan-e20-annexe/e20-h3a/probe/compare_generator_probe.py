"""compare_generator_probe.py - E20.h3a T0 (f): compares the generator of the DLL after each frame (probe_generator_dll.txt, the throwaway test probe_generator.cs.txt, one tick per frame)
with the key rng_probe_pf.txt (the oracle, no grass, no target, the per-frame step added after rng1).  DLL tick n = key tick k + n; the state after the frame n must equal rng0(k + n + 1) of the key
and the steps of the DLL during the frame must equal draws(k + n) + 1.  usage: python -B compare_generator_probe.py ../rng_probe_pf.txt probe_generator_dll.txt"""
import re
import sys
key = {}
for l in open(sys.argv[1]):
    m = re.match(r'k\s*([+-]\d+) rng0=(\w+) rng1=(\w+) draws=(\d+)', l)
    if m:
        key[int(m.group(1))] = (m.group(2), m.group(3), int(m.group(4)))
dll = {}
for l in open(sys.argv[2]):
    m = re.match(r'frame (\d+): after=(\w+) steps=(\d+)', l)
    if m:
        dll[int(m.group(1))] = (m.group(2), int(m.group(3)))
bad = 0; compared = 0
for n, (state, steps) in sorted(dll.items()):
    nxt = key.get(n + 1)
    if nxt is None:
        print('frame', n, ': no key line k%+d (the bomb slot is free in the key: nothing to compare)' % (n + 1)); continue
    compared += 1
    if state != nxt[0] or steps != key[n][2] + 1:
        bad += 1; print('DEVIATION frame', n, state, steps, 'key', nxt, key[n])
print('frames compared', compared, 'deviations', bad)
sys.exit(1 if bad else 0)
