import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import time, lib392 as L
t0=time.time()
m, S = L.world392(hero_px=(700, 312), direction=8)   # direction 8 = left? check
print('slots', S)
# walk left 40 ticks, Square edge at 31, then wait
pads, n = L.pad_seq([(30, L.LEFT), (1, L.LEFT|L.SQUARE), (80, 0)])
rows = L.gl.run(m, n, pads, crates=[S[6], S[10]])
print(L.gl.table(rows, [S[6], S[10]], first=24, last=60, hcols=['anim_t','px','py','pz','carried2C','xcoll','support'], ccols=['t24','carrier28','px','py','pz','fz','ground','support','status']))
