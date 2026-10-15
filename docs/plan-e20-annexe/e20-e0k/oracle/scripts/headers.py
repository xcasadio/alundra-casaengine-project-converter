import sys, os, json
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import pworld as P
recs = P.global_records()
FL = {0x1:'ClassA',0x2:'?02',0x4:'?04',0x8:'ClassB',0x10:'DeactImpact',0x20:'DeactHit',0x40:'DeactAnimEnd',0x80:'Collidable',0x100:'Gravity',0x200:'Pick1',0x400:'Pick2',0x800:'ClassC',0x1000:'HitsC',0x2000:'NoObstSlide',0x4000:'NoRiders',0x8000:'InteractBtn'}
def names(f):
    return ','.join(v for k,v in FL.items() if f & k)
def u(b): return b if b >= 0 else b+256
for t in (3,4,7,11,12,14,15,27,2,1,0x10,0x17):
    if t >= len(recs) or recs[t] is None:
        print(t, None); continue
    r = recs[t]; H = r['Header']
    ub = H['Ubuff']
    flags = ub[0] | (ub[1] << 8) | (ub[2] << 16)
    print('type', t, 'Ubuff', ub, 'flags=%06X' % flags, names(flags & 0xFFFF))
    print('   off=(%d,%d,%d) size=(%d,%d,%d) break=%d contents=%d' % (ub[8],ub[9],ub[10],ub[11],ub[12],ub[13],ub[14],ub[15]) if len(ub) >= 16 else '')
    for i, s in enumerate(r['AnimSets']):
        imp = (s['Unknown'] & 0xFF) | ((s['Flags'] & 0xFF) << 8)
        if imp & 0x8000: imp -= 0x10000
        print('   anim %d speed=%d impulse=%d(raw %04X) animflags(+0xD)=%02X sfx=%d  offs=%s' % (i, s['Speed'], imp, (s['Unknown'] & 0xFF) | ((s['Flags'] & 0xFF) << 8), s['Acceleration'], s['Sfx'], s['AnimationOffsets']))
