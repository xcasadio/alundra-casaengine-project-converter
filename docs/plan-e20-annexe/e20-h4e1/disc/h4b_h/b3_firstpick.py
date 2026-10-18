"""b3_firstpick.py - how often does the FIRST result of the query (what C63/C65/C75/wind-1 use) differ from the true nearest candidate, with the original's rules?
Synthetic: random candidate sets drawn on a 16-px grid (ties are frequent on purpose) and on a fine grid. Order of magnitude only; the exact sort is b3_query.py's model."""
import random, sys
import b3_query as Q
from machine import M32
def hoare_first(keys, ptrs):
    k, p = list(keys), list(ptrs)
    if len(k) >= 3:
        Q.hoare(k, p, 0, len(k) - 1)
    return p[0] if p else None
def run(n_items, grid, trials, seed):
    rnd = random.Random(seed)
    diff_orig = diff_stable = tie_first = 0
    for _ in range(trials):
        pts = [(rnd.randrange(-6, 7) * grid, rnd.randrange(-6, 7) * grid) for _ in range(n_items)]
        keys = [(x * x + y * y) & M32 for x, y in pts]
        ptrs = list(range(n_items))
        mn = min(keys)
        first = hoare_first(keys, ptrs)
        if keys[first] != mn:
            diff_orig += 1
        if keys.count(mn) > 1:
            tie_first += 1
            order = sorted(range(n_items), key=lambda i: keys[i])
            if order[0] != first:
                diff_stable += 1
    return diff_orig / trials, tie_first / trials, diff_stable / max(1, tie_first)
for n in (1, 2, 3, 4, 6, 10):
    for grid in (1, 16):
        a, b, c = run(n, grid, 20000, 1)
        print('n=%2d grid=%2d px: first result is NOT the nearest: %5.1f %% | the nearest is tied: %5.1f %% | among ties, differs from a stable sort: %5.1f %%' % (n, grid, 100 * a, 100 * b, 100 * c))
