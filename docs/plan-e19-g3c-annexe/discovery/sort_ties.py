"""Relative order of two EQUAL-key elements after the REAL quicksort 0x8002DA04 (descending on element+0x10), over random lists.
Emission order = sorted order; the element emitted FIRST is inserted first at the head of its OT slot and ends up drawn LAST (on top)."""
import sys, os, random
H = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, H)
import fxm
f = fxm.FxMachine(None); m = f.m
LIST = 0x8011CB60; ELEMS = 0x80190000
random.seed(11)
def sort_list(keys):
    n = len(keys)
    for i, k in enumerate(keys):
        el = ELEMS + 0x40 * i
        m.ww(el + 0x10, k & 0xFFFFFFFF); m.ww(el, i)           # element id at +0
        m.ww(LIST + 4 * i, el)
    m.ww(0x8011CE60, n)
    if n >= 2: m.call(0x8002DA04, 0, n - 1, limit=5_000_000)
    return [m.rw(m.rw(LIST + 4 * k)) for k in range(n)]        # element ids in sorted order
for n in (2, 3, 5, 8, 16, 32, 64):
    low_first = 0; trials = 400
    for _ in range(trials):
        keys = [random.randint(0, 40) << 16 for _ in range(n)]
        a, b = random.sample(range(n), 2); a, b = min(a, b), max(a, b)
        keys[b] = keys[a]
        order = sort_list(keys)
        low_first += order.index(a) < order.index(b)
    print("n=%2d  P(lower index emitted first, i.e. drawn last / on top) = %.2f" % (n, low_first / trials))
# stable on sorted-with-ties input of the 391 shape
print("391 shape [a<b<c<d=d] ->", sort_list([0x1f8, 0x258, 0x2fd, 0x323, 0x323]))
