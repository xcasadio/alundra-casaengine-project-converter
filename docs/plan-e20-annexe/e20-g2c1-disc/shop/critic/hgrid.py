import sys
sys.path.insert(0,'.')
import evlib as L
m=int(sys.argv[1]); x0,x1,y0,y1=map(int,sys.argv[2:6])
c=L.load_cells(m); d=L.load_map(m); W=d['Map']['Width']
def cell(x,y):
    w=c['walkability'][y*W+x]
    return ('%d%s'%(c['height'][y*W+x], 'X' if w&0x40 else ('o' if w else '.'))).rjust(3)
print('     '+' '.join('%3d'%x for x in range(x0,x1+1)))
for y in range(y0,y1+1):
    print('%3d  '%y+' '.join(cell(x,y) for x in range(x0,x1+1)))
