"""Turn law of C63 (0x8007AC60) as the plan states it (FAIT, plan 1.31.6 'Le tour'): numeric comparison, no wrap, step of 4.
Computes, on the 32 x 32 grid of (current direction, wanted direction): the number of steps of the binary law and of the refused 'shortest' law,
and the count of pairs where the numeric gap exceeds 16 (the pairs where the binary goes the long way). Pure arithmetic, no binary read."""
def step_binary(cur, d):
    if d == cur:
        return cur
    if d < cur:
        return d if cur - d < 4 else cur - 4
    return d if d - cur < 4 else cur + 4

def steps(law, cur, d):
    n = 0
    seq = [cur]
    while cur != d:
        cur = law(cur, d)
        seq.append(cur)
        n += 1
        assert n < 40
    return n, seq

def step_short(cur, d):
    gap = (d - cur) & 31
    if gap == 0:
        return cur
    if gap <= 16:
        return d if gap < 4 else (cur + 4) & 31
    return d if 32 - gap < 4 else (cur - 4) & 31

long_pairs = [(c, d) for c in range(32) for d in range(32) if abs(d - c) > 16]
print('pairs with numeric gap > 16:', len(long_pairs), 'of 1024')
nb = [steps(step_binary, c, d)[0] for c, d in long_pairs]
ns = [steps(step_short, c, d)[0] for c, d in long_pairs]
print('binary law on those pairs: steps min %d max %d mean %.2f' % (min(nb), max(nb), sum(nb) / len(nb)))
print('shortest law on those pairs: steps min %d max %d mean %.2f' % (min(ns), max(ns), sum(ns) / len(ns)))
print('31 -> 0 binary:', steps(step_binary, 31, 0))
print('0 -> 31 binary:', steps(step_binary, 0, 31))
print('31 -> 0 shortest:', steps(step_short, 31, 0))
# every 3 ticks one step (+0x274 = 2 after a turn): extra ticks of the long way, worst case
print('worst extra steps (binary - shortest):', max(a - b for a, b in zip(nb, ns)), '=> extra ticks at 3 ticks per step:', 3 * max(a - b for a, b in zip(nb, ns)))
# gap > 16 pairs where both laws agree?  (they must differ on all 240)
diff = sum(1 for c, d in long_pairs if steps(step_binary, c, d)[1] != steps(step_short, c, d)[1])
print('pairs where the two laws take a different path:', diff)
