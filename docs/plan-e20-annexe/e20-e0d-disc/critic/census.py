import json,glob,re,collections,sys,io
sys.stdout=io.TextIOWrapper(sys.stdout.buffer,encoding='utf-8')
P='D:/development/repo/alundra-casaengine-project-converter/'
rows=[]
nmaps=0
keys=None
for fp in glob.glob(P+'data-extracted/data/map_*.json'):
    m=re.search(r'map_(\d+)\.json$',fp)
    if not m: continue
    mid=int(m.group(1)); nmaps+=1
    d=json.load(open(fp,encoding='utf-8'))
    si=d.get('SpriteInfo')
    if not si: continue
    ents=(si.get('Entities') or {}).get('Entities') or []
    for i,e in enumerate(ents):
        if not e: continue
        if keys is None: keys=sorted(e.keys())
        if e.get('SpriteTableIndex')==30:
            rows.append((mid,i,e))
print('maps',nmaps,'keys',keys)
print('all type-30 records',len(rows))
en=collections.Counter(e['IsEnabled'] for _,_,e in rows); print('IsEnabled',en)
G=[(m,i,e) for m,i,e in rows if not e['SpriteDirection']&0x80]
M=[(m,i,e) for m,i,e in rows if e['SpriteDirection']&0x80]
print('global page',len(G),'map page',len(M))
G0=[r for r in G if r[0]==0]; H=[r for r in G if r[0]!=0]
print('map0 global',len(G0),'outside map0',len(H),'on maps',len(set(r[0] for r in H)))
Hen=[r for r in H if r[2]['IsEnabled']]; print('enabled among H',len(Hen))
withc=[r for r in H if r[2]['Contents']]
print('with contents',len(withc),'maps',len(set(r[0] for r in withc)))
print('empty',len(H)-len(withc))
F=lambda e:e['EventCodesF_InteractIndex']
C=lambda e:e['EventCodesC_TickIndex']
print('native F (F==0) with contents',sum(1 for r in withc if F(r[2])==0),'; native F in H',sum(1 for r in H if F(r[2])==0), 'scripted F in H',sum(1 for r in H if F(r[2])!=0))
print('empty & F scripted',sum(1 for r in H if not r[2]['Contents'] and F(r[2])!=0),' empty & F native',sum(1 for r in H if not r[2]['Contents'] and F(r[2])==0))
print('with contents & scripted F',[ (m,i) for m,i,e in withc if F(e)!=0])
print('C scripted in H',sum(1 for r in H if C(r[2])!=0))
nf=[(m,i,e['Contents']) for m,i,e in withc if e['_10']==0]; print('flag0 with contents',nf)
print('flag bit15',sum(1 for r in withc if r[2]['_10']&0x8000),' >=0x800',sum(1 for r in withc if (r[2]['_10']&0x7fff)>=0x800))
print('flags distinct',len(set(e['_10'] for _,_,e in withc if e['_10'])),min(e['_10'] for _,_,e in withc if e['_10']),max(e['_10'] for _,_,e in withc))
print('contents >= 0x62',sum(1 for r in withc if r[2]['Contents']>=0x62), 'max',max(r[2]['Contents'] for r in withc))
print('dir', collections.Counter(r[2]['SpriteDirection'] for r in H))
print('dir&0x40 among withc',collections.Counter(bool(r[2]['SpriteDirection']&0x40) for r in withc))
print('dir&0x40 among native withc',collections.Counter(bool(r[2]['SpriteDirection']&0x40) for r in withc if F(r[2])==0))
cnt=collections.Counter(r[2]['Contents'] for r in withc)
print(sorted(cnt.items(),key=lambda x:-x[1])[:25])
print('distinct contents',len(cnt))
json.dump([(m,i,e) for m,i,e in H],open('H.json','w'))
# which contents on cap
caps={0x24:9,0x25:1,0x26:1,0x27:1,0x29:1}
print('capped', {hex(k):cnt.get(k,0) for k in caps}, sum(cnt.get(k,0) for k in caps))
