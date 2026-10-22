import csv, sys, collections
rows=list(csv.DictReader(open('D:/development/repo/alundra-casaengine-project-converter/docs/plan-e19-sm-annexe/skipped-sites.tsv',encoding='utf-8'),delimiter='\t'))
sm4={'72','88','96','97','B3'}
sel=[r for r in rows if r['opcode'].upper() in sm4]
sel.sort(key=lambda r:(r['opcode'],int(r['map']),r['slot'],int(r['program']),int(r['pc'])))
def cover(r):
    op=r['opcode'].upper(); m=int(r['map']); s=r['slot']; p=int(r['program']); pc=int(r['pc'])
    c=['RB-SHAPES (bytes at the pc)','V-'+op+' (shape)']
    exact=None
    if op=='96':
        if 452<=m<=470 and p==2 and pc==204: exact='ARC-452' if m==452 else 'RB-SAVEROOMS pin (same bytes as 452)'
        if m==394: exact='RB-394-C3'
        if m==475: exact='RB-475-B2'
        if m==22: exact='RB-22-C6'
    if op=='97':
        if 452<=m<=470 and p==3 and pc==330: exact='ARC-452' if m==452 else 'RB-SAVEROOMS pin (same bytes as 452)'
        if m==394: exact='RB-394-C3'
        if m==475: exact='RB-475-B2'
    if op=='88':
        exact={306:'ARC-306 + RB-306-E14',442:'ARC-442 + RB-442-E1'}[m]
    if op=='72':
        if m==40 and p==2: exact='ARC-40'
        if m==444 and pc==1302: exact='RB-444-D15'
        if m==140 and p==2: exact='RB-140-D2'
    if op=='B3': exact='ARC-161a/b + RB-161-B1'
    return exact or 'shape only'
out=['opcode\tmap\tslot\tprogram\tpc\toperands\texact_test']
cnt=collections.Counter()
for r in sel:
    e=cover(r)
    cnt[e]+=1
    out.append('\t'.join([r['opcode'],r['map'],r['slot'],r['program'],r['pc'],r['operands'],e]))
open('../corpus/sm4-sites-coverage.tsv','w',encoding='utf-8',newline='\n').write('\n'.join(out)+'\n')
for k,v in sorted(cnt.items()): print(v,k)
