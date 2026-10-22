import sys; sys.path.insert(0,'tools')
from mycorpus import *
import collections
sites=[]; ninstr=0
for mid in sorted(int(k) for k in IDX):
    ev,tm,b=load(mid)
    w=walk_map(ev,tm)
    ninstr+=len(w)
    for slot,idx,pc,op in w:
        if op in (0x6A,0x6B,0x6C,0x6D): sites.append((mid,slot,idx,pc,op))
print("maps",len(IDX),"instructions",ninstr,"sites 6A-6D",len(sites))
bymap=collections.Counter(s[0] for s in sites)
print(dict(sorted(bymap.items())))
byop=collections.Counter(s[4] for s in sites); print({hex(k):v for k,v in byop.items()})
# compare with skipped-sites.tsv
import csv
ref=set()
for r in csv.DictReader(open(REPO+'/docs/plan-e19-sm-annexe/skipped-sites.tsv',encoding='utf-8'),delimiter='\t'):
    if r['opcode'] in ('6A','6B','6C','6D'): ref.add((int(r['map']),r['slot'],int(r['program']),int(r['pc']),int(r['opcode'],16)))
mine=set(sites)
print("multiset equal:", len(sites)==len(mine), "set equal:", mine==ref, len(mine),len(ref), "diff", sorted(mine^ref)[:10])
json.dump(sorted(sites),open('evidence/my_sites_6A_6D.json','w'))
