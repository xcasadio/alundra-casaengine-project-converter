import sys, time, random
sys.path.insert(0, ".")
import vec_p as V
rnd = random.Random(1)
for k in range(40):
    v = V.gen_vector(rnd)
    t0 = time.time()
    try:
        rec = {}
        mm = V.run_vector(v, record=rec)
        print(k, "n", v["n"], "ticks", len(v["script"]), "%.2fs" % (time.time()-t0), "mm", len(mm))
    except Exception as e:
        print(k, "EXC", repr(e)[:200], "%.2fs" % (time.time()-t0))
        print(v)
        break
