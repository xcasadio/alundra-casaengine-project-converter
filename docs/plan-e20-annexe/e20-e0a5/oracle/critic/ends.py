import re,sys
# parse trace rows: first tick where end(+AC)=1, status 4, frame boxes
def parse(fn):
    rows=[]
    for l in open(fn):
        m=re.match(r't(\d+)\s+(\w+)\s+st=(\d+) c1c=(\d+)\s*\|.*frame_shown=(\S+) next=(\S+) cnt\(\+A8\)=(-?\d+)\s+end\(\+AC\)=(\d+).*box1D4=(\w+) b=\((-?\d+),(-?\d+),(-?\d+),(\d+),(\d+),(\d+)\) world=\((-?[\d.]+)',l)
        if m: rows.append(m.groups())
    return rows
for fn in sys.argv[1:]:
    rows=parse(fn)
    S=117
    first_end=next(r for r in rows if r[7]=='1')
    first_st4=next(r for r in rows if r[2]=='4')
    # world box X after physics per tick
    wx={}
    for r in rows:
        if r[1]=='physics': wx[int(r[0])]=float(r[15])
    ch=[t-S for t in sorted(wx) if t-1 in wx and wx[t]!=wx[t-1] and S-1<=t<=S+12]
    print(fn.split('/')[-1],'end flag first at S+%d'%(int(first_end[0])-S),'st4 first at S+%d'%(int(first_st4[0])-S),'world X changes at S+',ch)
