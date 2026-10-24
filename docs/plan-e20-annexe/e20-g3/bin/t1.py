import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import time, lib392 as L
t0=time.time()
m, S = L.world392(hero_px=(590, 296))
pads, n = L.pad_seq([(30, L.RIGHT)])
rows = L.gl.run(m, n, pads, crates=list(S.values()))
print('run', time.time()-t0)
print(L.gl.table(rows, [S[5]], first=1, last=30, hcols=['anim_t','px','py','pz','fx','ground','xcoll','support'], ccols=['px','py','pz','xcoll','support']))
