import json,sys
S=sys.argv[1]
d=json.load(open(S+'/bin/out/json/arrow3_east_wall.json',encoding='utf-8'))
rows=[]
for r in d:
    for k,v in r['slots'].items():
        if v.get('type68')==3:
            rows.append((r['tick'],k,v,r))
first=rows[0][0]
print('first tick of type 3 slot:', first, 'slot', rows[0][1], ' last tick', rows[-1][0], 'n', len(rows))
base=first
def rel(t): return t-base
st=[(rel(t),v['status']) for t,k,v,r in rows]
# status ranges
prev=None; ranges=[]
for t,s in st:
    if s!=prev: ranges.append([s,t,t]); prev=s
    else: ranges[-1][2]=t
print('status ranges (S-relative):', ranges)
px=[(rel(t),v['px']/65536) for t,k,v,r in rows]
print('px S..S+3:', px[:3], ' S+40..S+45:', [p for p in px if 40<=p[0]<=45], ' S+58..:', [p for p in px if p[0]>=57])
pz=[(rel(t),v['pz']/65536) for t,k,v,r in rows]
print('pz:', [round(p[1],3) for p in pz if p[0]>=41])
print('fx:', [(rel(t),v['fx']) for t,k,v,r in rows if rel(t) in (0,1,42,43,44,59)])
print('fz:', [(rel(t),v['fz']) for t,k,v,r in rows if rel(t)>=41][:6])
print('adj_13C:', [(rel(t),v['adj_13C']) for t,k,v,r in rows if v['adj_13C']])
print('dir_t/anim_t/flags6C changes:', [(rel(t),v['dir_t'],v['anim_t'],hex(v['flags6C'])) for i,(t,k,v,r) in enumerate(rows) if i==0 or (v['dir_t'],v['anim_t'],v['flags6C'])!=(rows[i-1][2]['dir_t'],rows[i-1][2]['anim_t'],rows[i-1][2]['flags6C'])])
print('box1d4 nonzero ticks:', [rel(t) for t,k,v,r in rows if v['box1d4']][:3], '...', [rel(t) for t,k,v,r in rows if v['box1d4']][-2:], ' zero from', [rel(t) for t,k,v,r in rows if not v['box1d4']][:2])
print('reset_AC:', [(rel(t)) for t,k,v,r in rows if v['reset_AC']])
print('sounds:', [(rel(r['tick']),r['journal']) for r in d if r['journal'] and r['tick']>=base])
print('last rows status', rows[-1][2]['status'], rel(rows[-1][0]))
print('hbz,bw,bd,bh at S+1:', [(v['hbz'],v['bw'],v['bd'],v['bh']) for t,k,v,r in rows if rel(t)==1])
print('atk_attr,pow:', rows[1][2]['atk_attr'], rows[1][2]['atk_pow'])
