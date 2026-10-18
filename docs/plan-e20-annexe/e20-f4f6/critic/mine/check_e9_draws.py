"""Critic: independent model of E9's draws, compared to the oracle's recorded puff velocities (json from the re-run)."""
import json, re
K, A, M = 0x7D2B89DD, 0xE06A02E7, 0xFFFFFFFF
def step(s): return (s*K + A) & M
def hi(x, k): return (x*k) >> 32
def s32(v): return v-(1<<32) if v & 0x80000000 else v
rows = json.load(open('../bin/out/json/fire11_flat.json'))
def vel(txt):
    m = re.search(r'v=\(([-\d.]+),([-\d.]+),([-\d.]+)\)', txt)
    return tuple(float(x) for x in m.groups())
ok = True
for r in rows:
    if not r['draws']: continue
    fx = [e for e in r['effects'] if e.startswith('fx(id=17')]
    s = r['rng0']
    if r['draws'] == 10:
        exp = []
        for i in range(5):
            s1 = step(s); s2 = step(s1)
            vx = s32(hi(s1, 0x60001)) - 0x30000 if False else (hi(s1, 0x60001) - 0x30000)
            vy = hi(s2, 0x40001) - 0x20000
            exp.append((vx/65536, vy/65536, 0.0)); s = s2
    elif r['draws'] == 2:
        s1 = step(s); s2 = step(s1)
        exp = [((hi(s1, 0x30001) - 0x18000)/65536, (hi(s2, 0x20001) - 0x10000)/65536, 2.0)]; s = s2
    else:
        continue
    got = [vel(e) for e in fx]
    good = all(abs(a[0]-b[0]) < 1e-3 and abs(a[1]-b[1]) < 1e-3 and abs(a[2]-b[2]) < 1e-3 for a, b in zip(exp, got)) and len(exp) == len(got) and s == r['rng1']
    ok &= good
    print('tick', r['tick'], 'draws', r['draws'], 'model==oracle', good)
print('ALL', ok)
