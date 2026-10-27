import json,collections,sys
HERO=0x80127D30;STR=0x294
A='D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-h4b/'
files=[A+'bin/vec/vec_query_8003AF70.json',A+'critic/vec_extra/vec_extra_query.json']
cnt=collections.Counter(); other=collections.Counter(); sizes=collections.defaultdict(set)
for f in files:
    for v in json.load(open(f))['vectors']:
        for a,n,val in v['reads']:
            if HERO<=a<HERO+64*STR:
                off=(a-HERO)%STR
                cnt[off]+=1; sizes[off].add(n)
            elif a==0x80134360: other['count']+=1
            elif a==0x801FFF10: other['arg5']+=1
            else: other[hex(a&~0xFFF)+('_n%d'%n)]+=1
for o in sorted(cnt): print(hex(o),sorted(sizes[o]),cnt[o])
print(other)
# fiche check: every non-entity, non-count, non-arg5 read must be a 1-byte read at (pointer read at +0x1C4 of some entity) + type + 2
bad=0; offs=collections.Counter(); tot=0; arg5=set()
for f in files:
    for v in json.load(open(f))['vectors']:
        atk=v['args'][2]
        ptrs={}
        for a,n,val in v['reads']:
            if HERO<=a<HERO+64*STR and (a-HERO)%STR==0x1c4: ptrs[val]=1
        for a,n,val in v['reads']:
            if HERO<=a<HERO+64*STR or a in (0x80134360,0x801FFF10): 
                if a==0x801FFF10: arg5.add(val)
                continue
            tot+=1
            if n==1 and any(a==p+atk+2 for p in ptrs): offs[atk+2]+=1
            else: bad+=1; print('BAD',hex(a),n,hex(atk))
print('fiche reads',tot,'bad',bad,'offsets (type+2)',sorted(offs),'arg5 values',[hex(x) for x in arg5])
