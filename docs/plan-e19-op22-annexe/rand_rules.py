"""rand_rules.py - which target rules equal the binary in the integer model (no engine quantisation), over random constant-force waits.
binary: PosZ_b = X + 1 at the first call, ForceZ = F (1B before the wait), PosZ += ForceZ after every call, the handler 0x8003DA70 on PosZ_b.
port  : PosZ_dll = X, the same steps, the handler on a target chosen by the rule.
Counts the first-call-to-return distance; a stall (never ends within 5000 calls) is reported separately."""
import random

def run(posz, f, target, maxc=5000):
    fz = f
    n = 0
    # first call: memo only
    while n < maxc:
        n += 1
        # physics after the first call, then the next call tests
        posz += fz
        if posz == target:
            return n
        gap = target - posz
        if gap > 0:
            if gap < fz: fz = gap
        else:
            if fz < gap: fz = gap
    return None

def calls(rule, X, F, H):
    lit = H << 19
    if rule == 'bin':   return run(X + 1, F, lit)
    if rule == 'lit':   return run(X, F, lit)
    if rule == 'shift': return run(X, F, lit - 1)
    if rule == 'dir':   return run(X, F, lit if F > 0 else lit - 1)
    if rule == 'hyb':   return run(X, F, lit - 1 if lit - 1 < (1 << 24) else lit)   # integer model: the rule as written (controller entity)

random.seed(22)
stats = {r: {'ok': 0, 'bad': 0, 'stall': 0} for r in ('lit', 'shift', 'dir', 'hyb')}
by_class = {}
N = 40000
for _ in range(N):
    H = random.choice([2, 6, 10, 12, 14, 20, 22, 30, 32, 38])
    lit = H << 19
    F = random.choice([16384, 32768, 65536, 98304, 131072, -16384, -32768, -65536])
    # a start that a wait can really start from: an arbitrary 16.16 position on the correct side of the target
    span = random.randint(1, 40) * 65536 + random.choice([0, 0, 0, 1, 7, 32767, 32768, 65535])
    X = lit - span if F > 0 else lit + span
    if X < 0: continue
    b = calls('bin', X, F, H)
    aligned = (span % abs(F) == 0)
    cls = ('asc' if F > 0 else 'desc') + ('/aligned' if aligned else '/misaligned')
    for r in stats:
        v = calls(r, X, F, H)
        key = (r, cls)
        by_class.setdefault(key, [0, 0, 0])
        if v is None: stats[r]['stall'] += 1; by_class[key][2] += 1
        elif v == b: stats[r]['ok'] += 1; by_class[key][0] += 1
        else: stats[r]['bad'] += 1; by_class[key][1] += 1
for r, s in stats.items():
    print("%-6s equal to the binary %6d   different %6d   stall %6d   (of %d)" % (r, s['ok'], s['bad'], s['stall'], sum(s.values())))
print()
for cls in ('asc/aligned', 'asc/misaligned', 'desc/aligned', 'desc/misaligned'):
    print(cls, {r: by_class.get((r, cls), [0, 0, 0]) for r in stats})
