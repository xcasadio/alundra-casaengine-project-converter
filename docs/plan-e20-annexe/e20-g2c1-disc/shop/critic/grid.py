import sys
sys.path.insert(0,'.')
import evlib as L
m=int(sys.argv[1]); x0,x1,y0,y1=map(int,sys.argv[2:6])
c=L.load_cells(m); d=L.load_map(m); W=d['Map']['Width']
print('     '+''.join('%x'%(x%16) for x in range(x0,x1+1)))
for y in range(y0,y1+1):
    row=''
    for x in range(x0,x1+1):
        w=c['walkability'][y*W+x]
        row+= '.' if w==0 else ('#' if w&0x40 else ('o' if w==1 else '?'))
    print('%3d  %s'%(y,row))
