import csv, json, re, collections
P='C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm3/'
vec={}
for l in open(P+'vectors/sm3-vectors.jsonl',encoding='utf-8'):
    v=json.loads(l); vec[v['id']]=v
hand=[k for k in vec if not (k.startswith('SWEEP') or k.startswith('RAND'))]
print('vectors',len(vec),'hand',len(hand))
rows=list(csv.DictReader(open(P+'tests.tsv',encoding='utf-8'),delimiter='\t'))
print('tests',len(rows))
pinned=collections.Counter(); bad=[]
for r in rows:
    col=r['vectors(expected result)']
    if not col.strip(): continue
    for item in col.split(';'):
        item=item.strip()
        if not item: continue
        name,val=item.rsplit('=',1)
        if name not in vec: bad.append(('missing',r['id'],name)); continue
        pinned[name]+=1
        exp=vec[name]['expect']['result']
        if int(val)!=exp: bad.append(('value',r['id'],name,val,exp))
print('bad',bad[:10],len(bad))
unp=[h for h in hand if pinned[h]==0]
print('hand vectors not pinned:',len(unp),unp[:20])
# per-opcode counts of hand
c=collections.Counter(vec[h]['op'] for h in hand); print(c)
# stale (result_before !=7) vectors count
print(collections.Counter(vec[h]['result_before'] for h in hand))
