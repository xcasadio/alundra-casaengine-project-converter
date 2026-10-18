import json,sys
S=sys.argv[1]
for name in sys.argv[2:]:
    d=json.load(open(S+'/bin/out/json/%s.json'%name,encoding='utf-8'))
    rows=[]
    for r in d:
        for k,v in r['slots'].items():
            if v.get('type68')==3 or v.get('type68')==4:
                rows.append((r['tick'],k,v,r))
    if not rows:
        print(name,'no arrow rows'); continue
    base=rows[0][0]
    rel=lambda t:t-base
    prev=None; ranges=[]
    for t,k,v,r in rows:
        s=v['status']
        if s!=prev: ranges.append([s,rel(t),rel(t)]); prev=s
        else: ranges[-1][2]=rel(t)
    last=rows[-1]
    hits=[(rel(t),v['hitc'],v['c1d0'],v.get('touch_slot')) for t,k,v,r in rows if v['hitc']]
    draws=[(rel(r['tick']),r['draws']) for r in d if r['draws'] and r['tick']>=base]
    print(name,'S tick',base,'status ranges',ranges,'last arrow row rel',rel(last[0]),'hits',hits[:3],'draws',draws[:4], 'adj', [rel(t) for t,k,v,r in rows if v['adj_13C']][:3])
