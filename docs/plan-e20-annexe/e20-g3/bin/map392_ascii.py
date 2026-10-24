import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L
C=L.CELLS
# legend: '#' walkability & 0x41 != 0 (hero blocked, class B bit 3 -> mask 0x41), '+' walkability&1 only, digits = height byte, '.' height 4, gp != 0 marked by letter
ents={}
for i,e in enumerate(L.RECORDS):
    x,y,h=L.rec_px(i); ents[(int(x//24),int(y//16))]=ents.get((int(x//24),int(y//16)),'')+str(i%10)
print('     '+''.join('%d'%(c%10) for c in range(52)))
for r in range(0,60):
    line=''
    for c in range(52):
        k=C[(c,r)]
        ch='.' if k['Height']==4 else str(k['Height'])
        if k['Walkability']&0x41: ch='#'
        elif k['Walkability']: ch='w'
        if k['GroundProperty']: ch='g'
        if (c,r) in ents: ch='C'
        line+=ch
    print('r%02d  %s'%(r,line))
print('hero spawn B1 (720,216) -> cell', 720//24, 216//16, ' portal0 cell (22,23)')
