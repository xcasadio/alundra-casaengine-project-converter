import csv, json, collections
rows=list(csv.DictReader(open('D:/development/repo/alundra-casaengine-project-converter/docs/plan-e19-sm-annexe/skipped-sites.tsv',encoding='utf-8'),delimiter='\t'))
owner={}
for op in ('60','61','3C','3D'): owner[op]='sm1'
for op in ('6A','6B','6C','6D'): owner[op]='sm2'
for op in ('A9','AA','B2','B4','B5','B6','B7','9E'): owner[op]='sm3'
for op in ('72','88','96','97','B3','15','66','7A'): owner[op]='sm4'
owner['56']='sm5'
for op in ('B9','BA'): owner[op]='sm7'
for op in ('BB','C2'): owner[op]='sm8'
def stats(rs):
    return dict(lines=len(rs),
                instructions=len({(r['map'],r['pc']) for r in rs}),
                maps=len({r['map'] for r in rs}),
                opcodes=len({r['opcode'].upper() for r in rs}))
for r in rows: r['owner']=owner[r['opcode'].upper()]
print('before', stats(rows))
by=collections.Counter(r['owner'] for r in rows)
print('per owner before', dict(sorted(by.items())))
after=[r for r in rows if r['owner']!='sm4']
print('after sm4', stats(after))
print('per owner after', dict(sorted(collections.Counter(r['owner'] for r in after).items())))
sm4=[r for r in rows if r['owner']=='sm4']
print('sm4 own', stats(sm4))
# maps that disappear entirely
maps_sm4={r['map'] for r in sm4}
maps_other={r['map'] for r in after}
gone=sorted(maps_sm4-maps_other,key=int)
print('maps that lose all their sites', len(gone), gone)
# opcodes by count among sm4
print(collections.Counter(r['opcode'].upper() for r in sm4))
# duplicates among instruction keys inside sm4
c=collections.Counter((r['map'],r['pc']) for r in sm4)
print('sm4 duplicated (map,pc):', [k for k,v in c.items() if v>1])
c2=collections.Counter((r['map'],r['pc']) for r in rows)
print('global duplicated (map,pc):', [(k,v) for k,v in c2.items() if v>1])
# 'P' class (Result consumer) among sm4
print('sm4 next_is_Result_consumer=Y:', sum(1 for r in sm4 if r['next_is_Result_consumer']=='Y'))
json.dump(dict(before=stats(rows), perOwnerBefore=dict(sorted(by.items())), afterSm4=stats(after), perOwnerAfter=dict(sorted(collections.Counter(r['owner'] for r in after).items())), sm4=stats(sm4), mapsLosingAllSites=gone),
          open('../corpus/sm0-rule5-counts.json','w'),indent=1,sort_keys=True)
