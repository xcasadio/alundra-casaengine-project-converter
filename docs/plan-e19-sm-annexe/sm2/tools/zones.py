import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, tilemap2 as T, records as R
for m in sorted(R.by_map):
    print('=== map', m)
    for lname, o in T.objects(m):
        if lname != 'Entities': continue
        cp = o['custom_properties']
        raw = int(cp[R.KEYS['C']])
        if ('C', raw & 0x7F) not in R.by_map[m]: continue
        x0,x1,y0,y1 = (int(cp[k]) for k in ('XMin','XMax','YMin','YMax'))
        inside = x0 <= 33 <= x1 and y0 <= 59 <= y1
        print('  rec %-3s C[%3d] zone x[%d..%d] y[%d..%d] newgame(33,59) inside=%s  pos tile=(%s,%s) h=%s' % (cp['Index'], raw&0x7f, x0,x1,y0,y1, inside, cp['XPos'], cp['YPos'], cp['Height']))
