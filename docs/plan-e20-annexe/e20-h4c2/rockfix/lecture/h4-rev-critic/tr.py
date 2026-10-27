import json,sys
f=sys.argv[1]; rid=int(sys.argv[2]); lo=int(sys.argv[3]); hi=int(sys.argv[4])
d=json.load(open(f))
for r in d['rows']:
    if r['id']==rid:
        print('birth',r['birth'],'x0,y0',r['x0'],r['y0'])
        for t in r['track']:
            if lo<=t[0]<=hi:
                print('t=%d st=%d ta=%d x=%.4f y=%.4f z=%.2f ae=%d fadj=%d gnd=%d fl=%#x'%(t[0],t[1],t[2],t[3]/65536,t[4]/65536,t[5]/65536,t[6],t[7],t[8],t[9]))
