import sys, corpus
# usage: dump_site.py MAP SLOT PROG [count]
mid=int(sys.argv[1]); slot=sys.argv[2]; prog=int(sys.argv[3]); count=int(sys.argv[4]) if len(sys.argv)>4 else 60
base,d=corpus.load(mid)
codes=d['Codes']
entry=d[corpus.TABLE_KEYS[slot]][prog]
print('map',mid,base,'slot',slot,'prog',prog,'entry',entry,'ncodes',len(codes))
for pc,op,ops in corpus.decode(codes,entry,count=count,stop_at_end=True):
    print('%6d  %02X %-40s %s'%(pc,op,corpus.NAMES.get(op,'?'),ops))
