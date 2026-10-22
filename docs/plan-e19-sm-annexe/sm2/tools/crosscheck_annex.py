"""Runs the two ANNEX oracles (copies of sm1-sm4/work/oracle/oracle.py and sm1-sm4/check/myoracle.py) restricted to 0x6A..0x6D, as an
independent cross-check of the frozen vectors. python -B crosscheck_annex.py [N] [SEED]"""
import os, random, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import oracle            # copy of the discovery's differential oracle
import myoracle          # copy of the critic's independent oracle

N = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
SEED = int(sys.argv[2]) if len(sys.argv) > 2 else 11

# discovery oracle (random worlds, python model per opcode)
rng = random.Random(SEED)
m = oracle.make_machine()
oracle.STATUS_TABLE = lambda idx: m.rw(0x80023D2C + 4 * idx)
for op in (0x6A, 0x6B, 0x6C, 0x6D):
    bad = 0
    hist = {}
    first = None
    for it in range(N):
        w = oracle.World(rng)
        si = rng.randrange(w.n)
        b = oracle.operand_bytes(rng, op, w)
        err = oracle.run_one(m, w, op, si, b, oracle.model_search)
        e_ = oracle.MODELS[op](w, si, b, oracle.model_search)
        hist[e_.result] = hist.get(e_.result, 0) + 1
        if err:
            bad += 1
            first = first or (it, b[:oracle.SIZES[op]], err)
    print('discovery oracle  op %#04x: %d random states, %d mismatches, Result histogram %s %s' % (op, N, bad, dict(sorted(hist.items(), key=str)), first or ''))

# critic oracle (whole-window diff)
for op in (0x6A, 0x6B, 0x6C, 0x6D):
    bad, hist = myoracle.run(op, SEED, N)
    print('critic oracle     op %#04x: %d runs, %d mismatches, Result word histogram %s' % (op, N, bad, dict(sorted(hist.items(), key=lambda x: str(x[0])))))
