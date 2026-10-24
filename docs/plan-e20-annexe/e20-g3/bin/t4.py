import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L
m, S = L.world392(hero_px=(720, 296), direction=8)
h = L.K.ent(0)
y=float(sys.argv[1]); tj=int(sys.argv[2])
m.poke(h + 0x118, int(round(y * 65536)))
pads, n = L.pad_seq([(tj-1, L.LEFT), (1, L.LEFT|L.CROSS), (50-tj, L.LEFT)])
rows = L.gl.run(m, n, pads, crates=[S[4],S[5],S[8]])
rec={L.K.ent(s):r for r,s in S.items()}
for r in rows:
    if 28<=r['tick']<=42:
        H=r['hero']
        print('t%d x=%.3f y=%.3f z=%.3f fx=%.3f fz=%.3f ground=%d adj=%d xcoll=%s' % (r['tick'],H['px']/65536,H['py']/65536,H['pz']/65536,H['fx']/65536,H['fz']/65536,H['ground'],H['adj'],rec.get(H['xcoll'],hex(H['xcoll']))))
