"""Critic: classify every disagreement between the REAL handlers (my interpreter) and MY model of the DLL over random worlds
(my own generator).  prod = hero in SpawnedEntities and PlayerEntity; ctx = hero only PlayerEntity."""
import sys, random, collections
sys.argv = ['x', 'dummy']
sys.path.insert(0, 'work')
import gen_compare as G, mymodel as M, replay_binary_lib as RB

total = collections.Counter()
cls = collections.Counter()
examples = {}
for seed in (11, 12, 13, 14):
    r = random.Random(seed)
    for k in range(20000):
        op = G.OPS[k % 4]
        w = G.make_world(r, op)
        v = G.to_bin_vector(w)
        res, v0, bad, above = RB.run(v, True)
        res2, _, _, _ = RB.run(v, False)
        assert res == res2 and not bad and not above, (seed, k)
        total[hex(op)] += 1
        for montage in ('prod', 'ctx'):
            px, sp, pl = G.dll_world(w, montage)
            got = M.run(op, px[w['logic']], w['code'], sp, pl)
            if got != res:
                typ = w['code'][1] if op in (0x60, 0x61) else None
                anon_involved = bool(w['anon'])
                key = (montage, hex(op), hex(typ) if typ is not None else '-', 'anon' if anon_involved else 'noanon')
                cls[key] += 1
                examples.setdefault(key, (seed, k, w))
print('worlds per opcode', dict(total))
for k in sorted(cls):
    print(k, cls[k])
