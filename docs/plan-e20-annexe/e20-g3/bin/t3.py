import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import time, lib392 as L
t0=time.time()
m, S = L.world392(hero_px=(700, 296), direction=8)
tj=int(sys.argv[1]) if len(sys.argv)>1 else 12
pads, n = L.pad_seq([(tj-1, L.LEFT), (1, L.LEFT|L.CROSS), (60-tj, L.LEFT)])
rows = L.gl.run(m, n, pads, crates=[S[5]])
print(L.gl.table(rows, [S[5]], first=tj-1, last=tj+40, every=1, hcols=['anim_t','px','py','pz','fx','fz','ground','xcoll','support'], ccols=['px','pz']))
