import json
A=json.load(open('c1_pworld.json'))['rows']
B=json.load(open('c1_idx.json'))
# build per-rock tracks from pworld rows keyed (slot, birth)
tracks={}
live={}
for r in A:
    t=r['tick']
    cur=set()
    for s,d in r['slots'].items():
        s=int(s)
        if d['type68']!=16: continue
        cur.add(s)
        if s in live and live[s]['track'][-1]['status']==4:
            del live[s]
        if s not in live:
            live[s]=dict(slot=s,birth=t,track=[])
            tracks.setdefault('all',[]).append(live[s])
        live[s]['track'].append(dict(t=t,status=d['status'],x=d['px'],y=d['py'],z=d['pz'],fz=d['fz'],tdir=d['dir_t'],tanim=d['anim_t'],canim=d['anim_c'],row98=d['adir'],animend=d['reset_AC'],flags=d['flags6C']))
    for s in list(live):
        if s not in cur: del live[s]
allr=tracks['all']
print(len(allr))
bk=sorted(B['births'].values(), key=lambda b:(b['birth'],b['slot']))
ak=sorted(allr, key=lambda b:(b['birth'],b['slot']))
bad=0
for i,(x,y) in enumerate(zip(ak,bk)):
    ta=[(r['t'],r['status'],r['x'],r['y'],r['z'],r['fz'],r['tanim'],r['row98'],r['animend']) for r in x['track']]
    tb=[(r['t'],r['status'],r['x'],r['y'],r['z'],r['fz'],r['tanim'],r['row98'],r['animend']) for r in y['track']]
    if ta!=tb:
        bad+=1
        print('rock',i,'differs; first diff:')
        for p,q in zip(ta,tb):
            if p!=q: print('   pworld',p,'\n   spell ',q); break
        if len(ta)!=len(tb): print('   len',len(ta),len(tb))
print('rocks differing:',bad)
