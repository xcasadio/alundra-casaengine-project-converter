"""c9b_vecpower.py - how strong is the shipped vector pack as a SPECIFICATION?  Re-implements (critic's models) the bricks directly on the vectors' sparse "reads"
and checks (1) my model reproduces every shipped vector, (2) which semantic mutants of mine the pack does NOT distinguish (a pack that cannot refute a
plausible wrong port is a weak spec)."""
import json, os
from cmips import *
import c1_push, c1b_physpass, c3_query
from c9_replay import VEC, load_ram

HERO = 0x80127D30
STR = 0x294


def vecs(name):
    return json.load(open(os.path.join(VEC, name)))["vectors"]


def wd(v):
    return {(a, n): val for a, n, val in v["writes"]}


# ---------- push ----------
def run_push_model(v, mut=None):
    ram = load_ram(v["reads"])
    base = v["args"][0]
    W = c1b_physpass.W(ram)
    c1b_physpass.pushfn(W, base, mut)
    out = {}
    for a, byte in W.w.items():
        out[a] = byte
    return out


def bytes_of(writes):
    d = {}
    for (a, n), val in writes.items():
        for i in range(n):
            d[a + i] = (val >> (8 * i)) & 0xFF
    return d


for mut in (None, "push_shifted", "nopush"):
    bad = 0
    for v in vecs("vec_push_800366FC.json"):
        try:
            got = run_push_model(v, mut)
        except Incomplete:
            got = None
        if got is None or got != bytes_of(wd(v)):
            bad += 1
    print("push pack: mutant %-14s differs on %d of 200 vectors" % (mut, bad))

# ---------- query ----------
def q_model(v, variant=None):
    ram = load_ram(v["reads"])
    def g(a, n):
        t = 0
        for i in range(n):
            if a + i in ram: t |= ram[a + i] << (8 * i)
            elif BASE <= a + i < IMG_END: t |= IMG[a + i - BASE + TEXT_OFF] << (8 * i)
            else: raise Incomplete('absent %x' % (a + i))
        return t
    # the caller & params
    caller, mask, atk = v["args"][0], v["args"][1], v["args"][2]
    count = g(0x80134360, 4)
    return c3_query.query_model(g, caller, mask, atk, count, variant)


for var in (None, "inclusive", "status2only", "ign20", "sort_from2"):
    bad = 0
    tie = 0; n2 = 0; n3tie = 0
    for v in vecs("vec_query_8003AF70.json"):
        try:
            n, P, K = q_model(v, var)
        except Incomplete:
            bad += 1; continue
        w = wd(v)
        outp = 0x800CC058; outk = 0x800CC158
        ok = n == v["ret"] and all(w.get((outp + 4 * i, 4)) == P[i] and w.get((outk + 4 * i, 4)) == K[i] for i in range(n))
        if not ok: bad += 1
        if var is None:
            if len(set(K)) < len(K): tie += 1
            if n == 2: n2 += 1
            if n >= 3 and len(set(K)) < len(K): n3tie += 1
    print("query pack: variant %-12s differs on %d of 120" % (var, bad), ("| vectors with tied keys %d, with n==2 %d, with n>=3 and ties %d" % (tie, n2, n3tie)) if var is None else "")

# ---------- slot pass / lists pass ----------
def slot_model(v, variant=None):
    ram = load_ram(v["reads"])
    g = lambda a, n=4: sum(ram.get(a + i, 0) << (8 * i) for i in range(n))
    t = 0
    tmpl = [ram.get(0x80134368 + i, 0) for i in range(0x294)]
    for i in range(64):
        e = HERO + STR * i
        st = g(e + 0x10)
        if st == 4:
            pass
        elif st != 0:
            t = i
    return t


bad = 0
for v in vecs("vec_slotpass.json"):
    t = slot_model(v)
    w = wd(v)
    if w.get((0x80134360, 4)) != t: bad += 1
print("slotpass pack: my count model differs on %d of 40" % bad)
# how many slotpass vectors have a status-4 slot ABOVE the highest live slot (the distinguishing case for 'recycled does not count')
cnt = 0
for v in vecs("vec_slotpass.json"):
    ram = load_ram(v["reads"])
    g = lambda a, n=4: sum(ram.get(a + i, 0) << (8 * i) for i in range(n))
    sts = [g(HERO + STR * i + 0x10) for i in range(64)]
    live = [i for i, s in enumerate(sts) if s not in (0, 4)]
    top = max(live) if live else 0
    if any(s == 4 for s in sts[top + 1:]): cnt += 1
print("slotpass vectors where a status-4 slot lies above the highest live slot:", cnt, "of 40")

# lists pass: boundary vectors (entity at slot == count in physics list)
cnt = 0
for v in vecs("vec_listspass.json"):
    ram = load_ram(v["reads"])
    g = lambda a, n=4: sum(ram.get(a + i, 0) << (8 * i) for i in range(n))
    c = g(0x80134360)
    e = HERO + STR * c
    st = g(e + 0x10)
    if st in (2, 3): cnt += 1
print("listspass vectors where slot==count is live (distinguishes inclusive/exclusive):", cnt, "of 60")
