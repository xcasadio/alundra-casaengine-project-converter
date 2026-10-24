import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L
m, S = L.world392(hero_px=(720, 296), direction=8, rec_ids=(4, 5, 6))
seq = [(28, L.LEFT), (1, L.LEFT | L.SQUARE), (35, L.LEFT), (1, L.LEFT | L.SQUARE), (40, 0)]
pads, n = L.pad_seq(seq)
rows = L.gl.run(m, n, pads, crates=list(S.values()))
for r in rows:
    if r['tick'] in (26,27,28,29,30,31,40,50,60,63,64,65,66,67,68,70):
        H=r['hero']; print(r['tick'], hex(r['hold']), 'anim',H['anim_t'],'x %.2f'%(H['px']/65536),'carried',hex(H['carried2C']),'xcoll',hex(H['xcoll']),'p180',hex(H['p180']))
