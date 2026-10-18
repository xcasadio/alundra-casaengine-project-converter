"""b2_slots.py - brick (2): the highest live slot [0x80134360].
 (a) the slot pass 0x80038634 (recompute + recycle of destroyed slots)
 (b) the lists pass 0x800384F4 (which entities are in the physics list; slots 0..count INCLUSIVE)
 (c) the raise at birth, 0x80039D24..0x80039D3C (real code of 0x80039D04 run up to 0x80039D40)
Own models written from the disassembly (../out/dis_80038634.txt, dis_800384f4.txt, dis_80039D04.txt) compared with the REAL code on random vectors, with mutants
and replayable vector files (../vec/vec_slotpass.json, vec_listspass.json, vec_birthraise.json).
usage: python -B b2_slots.py [N] [seed]"""
import json, os, random, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import vtrace as V
from machine import s32, M32

OUT = os.path.join(HERE, '..', 'vec')
ENT0, STRIDE = 0x80127D30, 0x294
COUNT = 0x80134360
TEMPLATE = 0x80134368
L1, N1 = 0x80127B28, 0x80127D28          # physics list
L2, N2 = 0x80127C28, 0x80127D2C          # collideable list
L3, N3 = 0x80134250, 0x8013435C          # draw list


def ent(i):
    return ENT0 + STRIDE * i


def fill_entities(m, rnd, statuses=None):
    for i in range(64):
        for k in range(STRIDE // 4):
            m.poke(ent(i) + 4 * k, rnd.randrange(0, 1 << 32) if rnd.random() < 0.3 else 0)
        m.poke(ent(i) + 0x10, (statuses or [0] * 64)[i])


# ---------------------------------------------------------------------------------- (a) slot pass
def slot_model(status, mut=None):
    """returns (count, recycled slots)"""
    t2 = 0
    rec = []
    for i in range(64):
        st = status[i]
        if st == 4:
            rec.append(i)
        elif st != 0:
            t2 = i
    if mut == 'four_counts':
        t2 = max([i for i in range(64) if status[i] != 0] or [0])
    if mut == 'status123':
        t2 = max([i for i in range(64) if status[i] in (1, 2, 3)] or [0])
    if mut == 'count_excl_hero0':
        t2 = max([i for i in range(1, 64) if status[i] not in (0, 4)] or [0])
    if mut == 'recycle_not':
        rec = []
    return t2, rec


def run_slot(N, seed):
    rnd = random.Random(seed)
    m = V.TM()
    m.cov.clear()
    vecs, mism = [], 0
    mut_names = ['four_counts', 'status123', 'recycle_not']
    killed = {k: 0 for k in mut_names}
    for n in range(N):
        pop = rnd.choice([0.1, 0.3, 0.6])
        status = [rnd.choice([0, 1, 2, 3, 4, 4, 5, 0xFF, 0xFFFFFFFF]) if rnd.random() < pop else 0 for _ in range(64)]
        if rnd.random() < 0.3:
            status[63] = rnd.choice([0, 4])
        fill_entities(m, rnd, status)
        tmpl_random = rnd.random() < 0.3
        for k in range(STRIDE // 4):
            m.poke(TEMPLATE + 4 * k, rnd.randrange(0, 1 << 32) if tmpl_random else 0)
        m.poke(COUNT, rnd.randrange(0, 64))
        before = {i: [m.peek(ent(i) + 4 * k) for k in range(STRIDE // 4)] for i in range(64)}
        tmpl = [m.peek(TEMPLATE + 4 * k) for k in range(STRIDE // 4)]
        vec = V.run(m, 0x80038634, ())
        cnt, rec = slot_model(status)
        ok = m.peek(COUNT) == cnt
        for i in range(64):
            now = [m.peek(ent(i) + 4 * k) for k in range(STRIDE // 4)]
            if i in rec:
                want = list(tmpl)
                want[0] = i
            else:
                want = before[i]
            if now != want:
                ok = False
        if not ok:
            mism += 1
            if mism <= 3:
                print('SLOT MISMATCH', n, hex(m.peek(COUNT)), cnt)
        for mu in mut_names:
            c2, r2 = slot_model(status, mut=mu)
            if c2 != cnt or r2 != rec:
                killed[mu] += 1
        if n < 40:
            vecs.append({"id": n, **vec})
    print('slot pass: vectors', N, 'seed', seed, 'mismatches', mism)
    cov = len([p for p in range(0x80038634, 0x800386D0, 4) if p in m.cov])
    print('  coverage 0x80038634..0x800386D0: %d/%d' % (cov, (0x800386D0 - 0x80038634) // 4))
    print('  mutants distinguished (of %d vectors):' % N, killed)
    bad = sum(1 for v in vecs if not V.replay(v)[0])
    print('  replay from empty memory: %d/%d' % (len(vecs) - bad, len(vecs)))
    json.dump({"function": "0x80038634", "note": "slot pass: count = highest slot with status not in {0,4}; status-4 slots are overwritten by the 0x294-byte template at 0x80134368 then +0 = slot index", "vectors": vecs}, open(os.path.join(OUT, 'vec_slotpass.json'), 'w'))
    return mism


# ---------------------------------------------------------------------------------- (b) lists pass
def lists_model(E, count, mut=None):
    l1, l2, l3 = [], [], []
    last = count if mut != 'exclusive' else count - 1
    for i in range(0, last + 1):
        e = E[i]
        st = e['st']
        if mut == 'status2only':
            in1 = st == 2 and e[0x20] == 0
        else:
            in1 = st in (2, 3) and e[0x20] == 0
        if in1:
            l1.append(i)
        if (e[0x6C] & 0x80) and not (e[0xB4] & 0x80) and e[0x28] == 0:
            l2.append(i)
        if st in (2, 3) and (e[0x1CC] & 3) != 3:
            l3.append(i)
    return l1, l2, l3


def run_lists(N, seed):
    rnd = random.Random(seed)
    m = V.TM()
    m.cov.clear()
    vecs, mism = [], 0
    mut_names = ['exclusive', 'status2only']
    killed = {k: 0 for k in mut_names}
    for n in range(N):
        E = {}
        for i in range(64):
            e = {'st': rnd.choice([0, 1, 2, 2, 3, 4]), 0x20: rnd.choice([0, 0, 0, 5]), 0x28: rnd.choice([0, 0, 0, 7]),
                 0x6C: rnd.choice([0, 0x80, 0x81, 0x101, 0x180]), 0xB4: rnd.choice([0, 0, 0x80, 0x40, 0xC0]),
                 0x1CC: rnd.choice([0, 1, 2, 3, 3, 0x103, 0x100])}
            E[i] = e
            m.poke(ent(i) + 0x10, e['st'])
            for off in (0x20, 0x28, 0x6C, 0xB4, 0x1CC):
                m.poke(ent(i) + off, e[off])
        count = rnd.choice([0, 1, 2, rnd.randrange(0, 64), rnd.randrange(0, 64), 63])
        m.poke(COUNT, count)
        for a in range(0, 64):
            m.poke(L1 + 4 * a, 0xDEAD0000 + a); m.poke(L2 + 4 * a, 0xDEAD0000 + a); m.poke(L3 + 4 * a, 0xDEAD0000 + a)
        vec = V.run(m, 0x800384F4, ())
        l1, l2, l3 = lists_model(E, count)
        got = [[m.peek(a + 4 * k) for k in range(m.peek(c))] for a, c in ((L1, N1), (L2, N2), (L3, N3))]
        exp = [[ent(i) for i in l] for l in (l1, l2, l3)]
        if got != exp:
            mism += 1
            if mism <= 3:
                print('LISTS MISMATCH', n, count)
        for mu in mut_names:
            r = lists_model(E, count, mut=mu)
            if [[ent(i) for i in l] for l in r] != exp:
                killed[mu] += 1
        if n < 60:
            vecs.append({"id": n, **vec})
    print('lists pass: vectors', N, 'seed', seed, 'mismatches', mism)
    cov = len([p for p in range(0x800384F4, 0x80038634, 4) if p in m.cov])
    print('  coverage 0x800384F4..0x80038634: %d/%d' % (cov, (0x80038634 - 0x800384F4) // 4))
    print('  mutants distinguished:', killed)
    bad = sum(1 for v in vecs if not V.replay(v)[0])
    print('  replay from empty memory: %d/%d' % (len(vecs) - bad, len(vecs)))
    json.dump({"function": "0x800384F4", "note": "lists pass over slots 0..[0x80134360] INCLUSIVE; list1 (physics) = status in {2,3} and +0x20==0", "vectors": vecs}, open(os.path.join(OUT, 'vec_listspass.json'), 'w'))
    return mism


# ---------------------------------------------------------------------------------- (c) birth raise
def run_birth(N, seed):
    """real 0x80039D04 run from its entry to 0x80039D40: the raise is  if (count < [ent+0]) count = [ent+0]"""
    rnd = random.Random(seed)
    m = V.TM()
    vecs, mism = [], 0
    for n in range(N):
        slot = rnd.randrange(0, 64)
        e = ent(slot)
        m.poke(e, slot)
        cnt = rnd.randrange(0, 64)
        m.poke(COUNT, cnt)
        vec = V.run(m, 0x80039D04, (e, 0, 0x801D0000, 0, 0, 0), stop_at=0x80039D40)
        want = max(cnt, slot)
        if m.peek(COUNT) != want:
            mism += 1
        if n < 30:
            vecs.append({"id": n, **vec})
    print('birth raise: vectors', N, 'mismatches', mism, '(model: count = max(count, slot index), strict <)')
    bad = sum(1 for v in vecs if not V.replay(v)[0])
    print('  replay from empty memory: %d/%d' % (len(vecs) - bad, len(vecs)))
    json.dump({"function": "0x80039D04", "stop_at": "0x80039D40", "note": "birth prefix: slot index read at ent+0, raise of [0x80134360]", "vectors": vecs}, open(os.path.join(OUT, 'vec_birthraise.json'), 'w'))
    return mism


if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 600
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261018
    os.makedirs(OUT, exist_ok=True)
    bad = run_slot(N, seed) + run_lists(N, seed) + run_birth(N, seed)
    sys.exit(1 if bad else 0)
