import json
fn=r"D:\development\repo\alundra-casaengine-project-converter\docs\plan-e20-annexe\e20-f3\oracle\out\wave_steel_east.json"
d=json.load(open(fn,encoding='utf-8'))
print({k:d[k] for k in d if k!='rows'})
rows=d['rows']
print(list(rows[0].keys()))
sl=None
for r in rows:
    s=r['slots'].get('1')
    if s: sl=s; break
print(sorted(sl.keys()))
S=None
prev=None
for r in rows:
    s=r['slots'].get('1')
    if not s: 
        if S is not None: print('tick',r['tick'],'slot 1 absent'); break
        continue
    if S is None: S=r['tick']
    print('S+%-2d'%(r['tick']-S), 'st',s['status'],'cnt1c',s['cnt1c'],'AC',s['reset_AC'],'hbx-px',(s['hbx']-s['px'])/65536,'box1d4',hex(s['box1d4']), 'bw',s['bw'], 'hbx',s['hbx']/65536)
