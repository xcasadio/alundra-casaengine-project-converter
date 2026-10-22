import csv, sys, io
sys.path.insert(0,'.')
import corpus
rows=list(csv.DictReader(open('D:/development/repo/alundra-casaengine-project-converter/docs/plan-e19-sm-annexe/skipped-sites.tsv',encoding='utf-8'),delimiter='\t'))
sm4={'72','88','96','97','B3','15','66','7A'}
sel=[r for r in rows if r['opcode'].upper() in sm4]
sel.sort(key=lambda r:(r['opcode'],int(r['map']),r['slot'],int(r['program']),int(r['pc'])))
out=io.StringIO()
for r in sel:
    mid=int(r['map']); slot=r['slot']; prog=int(r['program']); pc=int(r['pc'])
    c=corpus.context(mid,slot,prog,pc,before=8,after=4)
    out.write('=== op 0x%s map %d (%s) %s[%d] @%d operands [%s]\n'%(r['opcode'],mid,r['map_name'],slot,prog,pc,r['operands']))
    if c is None:
        out.write('   pc not on the linear decode\n'); continue
    rows_,entry=c
    out.write('   entry=%d\n'%entry)
    for p,o,ops,hit in rows_:
        out.write('   %s%5d  %02X %-38s %s\n'%('>>' if hit else '  ',p,o,corpus.NAMES.get(o,'?'),ops))
open('../corpus/sites-context.txt','w',encoding='utf-8',newline='\n').write(out.getvalue())
print(len(sel),'sites written')
