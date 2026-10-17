import sys, time, random
sys.path.insert(0, ".")
import vec_p as V
rnd = random.Random(1)
v = V.gen_vector(rnd)
t0 = time.time(); p = V.OP.P(gravity=128, visc=4096, cells=V.OP.flat(0)); print("Sim init", time.time()-t0)
t0 = time.time()
p = V.build_real(v); print("build_real", time.time()-t0, "n", v["n"])
t0 = time.time(); 
import cProfile, pstats
p.tick(); print("one tick", time.time()-t0, "steps(lastcall)", )
