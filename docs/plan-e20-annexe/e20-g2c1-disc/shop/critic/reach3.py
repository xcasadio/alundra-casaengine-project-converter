import sys, json
sys.path.insert(0,'.')
import evlib as L
R='D:/development/repo/alundra-casaengine-project-converter'
m=int(sys.argv[1]); starts=[tuple(map(int,s.split(','))) for s in sys.argv[2].split(';')]
mask=int(sys.argv[3],16); K=int(sys.argv[4]); diag = len(sys.argv)>5 and sys.argv[5]=='8'
c=L.load_cells(m); d=L.load_map(m); W=d['Map']['Width']; H=d['Map']['Height']
portals=[p for p in d['Info']['Portals'] if not(p['X1']==0 and p['X2']==255) and p['DestMapId']==m]
def free(x,y): return 0<=x<W and 0<=y<H and (c['walkability'][y*W+x] & mask)==0
seen=set(); stack=list(starts)
nb=[(1,0),(-1,0),(0,1),(0,-1)]+([(1,1),(1,-1),(-1,1),(-1,-1)] if diag else [])
used=set()
while stack:
    x,y=stack.pop()
    if (x,y) in seen or not free(x,y): continue
    seen.add((x,y))
    for dx,dy in nb:
        nx,ny=x+dx,y+dy
        if free(nx,ny) and abs(c['height'][ny*W+nx]-c['height'][y*W+x])<=K and (nx,ny) not in seen: stack.append((nx,ny))
    for i,p in enumerate(portals):
        if p['X1']<=x<=p['X2'] and p['Y1']<=y<=p['Y2']:
            used.add(i); stack.append((p['DestTileX'],p['DestTileY']))
shop=[(x,y) for x in range(3,15) for y in range(39,56) if (x,y) in seen]
print('mask %X K=%d diag=%s: reached %d cells; shop-room cells reached: %d; (5,46) reached: %s; portals used: %s'%(mask,K,diag,len(seen),len(shop),(5,46) in seen,sorted(used)))
print(' bbox reached', min(x for x,_ in seen),min(y for _,y in seen),max(x for x,_ in seen),max(y for _,y in seen))
