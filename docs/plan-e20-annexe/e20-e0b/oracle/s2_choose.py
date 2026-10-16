"""s2_choose.py - ChooseRandomlyAnItem 0x80032968 on the REAL code (MIPS machine), exhaustive over the decision tree of every code 0..255.
For every code the model enumerates all nibble paths (a draw is: s' = lcg(s); nibble = s' >> 28); for each path a witness state is found by search; the real function is called
with the RNG word set to the witness and its return value and RNG word out are compared with the model.  Output: out/choose_vectors.json, out/choose_summary.txt."""
import sys
import json
sys.path.insert(0, 'tools')
sys.path.insert(0, '.')
from machine import Machine, M32

RNG = 0x80098708
TAB = 0x80028E0C


def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32


m = Machine()
m.watch_uninit = False


def rb(a):
    return m.peek(a & ~3) >> (8 * (a & 3)) & 0xFF


rows = [[rb(TAB + 16 * r + c) for c in range(16)] for r in range(128)]


def model_paths(code):
    """yield (nibbles, item) for every path of the decision tree of `code`"""
    out = []

    def rec(c, path):
        if c >= 0x100:
            out.append((tuple(path), 0))
        elif c & 0x80:
            for k in range(16):
                rec(rows[c & 0x7F][k], path + [k])
        else:
            out.append((tuple(path), c if c < 0x62 else 0))
    rec(code, [])
    return out


# witnesses: state s such that nibble(lcg(s)) = k1 and nibble(lcg(lcg(s))) = k2 ; and for depth 3 (not needed, checked below)
W = {}
gen = 0x0BADF00D
need = {(k1, k2) for k1 in range(16) for k2 in range(16)}
count = 0
while need:
    gen = lcg(gen)
    s = gen
    s1 = lcg(s)
    s2 = lcg(s1)
    key = (s1 >> 28, s2 >> 28)
    if key in need:
        W[key] = s
        need.discard(key)
    count += 1
print('witness search: %d states for 256 pairs' % count)

max_depth = 0
vectors = []
bad = 0
summary = []
for code in range(512):      # E20.e0b2 T0: 0x100..0x1FF added (the function takes a full word; code >= 0x100 gives 0, no draw)
    paths = model_paths(code)
    depth = max(len(p) for p, _ in paths)
    max_depth = max(max_depth, depth)
    dist = {}
    draws_hist = {}
    for nib, item in paths:
        if len(nib) == 0:
            s = 0x12345678
        elif len(nib) == 1:
            s = W[(nib[0], 0)]
        else:
            assert len(nib) == 2, (code, nib)
            s = W[(nib[0], nib[1])]
        m.poke(RNG, s)
        got = m.call(0x80032968, code) & M32
        out_state = m.peek(RNG)
        exp_state = s
        for _ in nib:
            exp_state = lcg(exp_state)
        if got != item or out_state != exp_state:
            bad += 1
            print('MISMATCH code %02X path %s: real item %d state %08X; model item %d state %08X' % (code, nib, got, out_state, item, exp_state))
        vectors.append([code, s, got, out_state, len(nib)])
        w = 16 ** (-len(nib))      # probability of this path
        dist[got] = dist.get(got, 0) + 16 ** (2 - len(nib)) if len(nib) <= 2 else 0     # in 1/256 units
        draws_hist[len(nib)] = draws_hist.get(len(nib), 0) + 16 ** (2 - len(nib))
    summary.append((code, depth, sorted(dist.items()), sorted(draws_hist.items())))

print('max tree depth over the 512 codes:', max_depth)
print('mismatches between the model and the real function:', bad, 'of', len(vectors), 'calls')
json.dump([v for v in vectors if v[0] < 256], open('out/choose_vectors.json', 'w'))      # the 2626 of the discovery
json.dump([v for v in vectors if v[0] >= 256], open('out/choose_high_vectors.json', 'w'))     # E20.e0b2 T0: the 256 codes 0x100..0x1FF
with open('out/choose_summary.txt', 'w') as f:
    f.write('code draws(max) item distribution in 1/256 | draws distribution in 1/256 (draws:weight)\n')
    for code, depth, dist, dh in summary:
        if (code < 0x80 or code >= 0x100) and depth == 0:
            continue
        f.write('%02X depth %d items(1/256) %s draws %s\n' % (code, depth, dist, dh))
    f.write('codes < 0x80 (single byte): item = code if code < 0x62 else 0, no draw\n')
print(open('out/choose_summary.txt').read())
