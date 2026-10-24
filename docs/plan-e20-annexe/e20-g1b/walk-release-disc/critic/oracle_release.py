import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gl
R=gl.RIGHT
for crate_x in (330, 130):
    m=gl.world(hero_px=(100,100), direction=24); c=gl.add_crate(m, crate_x, 100)
    pads,n=gl.pad_script([(9,R),(4,0)])
    rows=gl.run(m,n,pads,crates=[c])
    print('== oracle release, crate at x=%d' % crate_x)
    for r in rows:
        h=r['hero']; print('t%02d x=%.4f fx=%.4f adj=%d xcoll=%s anim=%d'%(r['tick'],h['px']/65536,h['fx']/65536,h['adj'],'crate' if h['xcoll'] else '-',h['anim_t']))
