import re
b={};sec=False
for l in open('../t0replay/rep/q3_corners.txt'):
    if l.startswith('== cells 0x1001'): sec=True; continue
    if l.startswith('==') : sec=False
    if sec:
        m=re.match(r't(\d+) pos=\(([\d.]+),([\d.]+),([\d.]+)\) adj=(\d) ground=(\d) anim=(\d) status=(\d)',l)
        if m: b[int(m.group(1))-61]=(float(m.group(2)),float(m.group(4)),int(m.group(5)),int(m.group(6)),int(m.group(7)))
res={}
cur=None
for l in open('dll/p2.txt'):
    m=re.match(r'== (\w+?)\d+r\d+ band',l)
    if m: cur=m.group(1); res[cur]={}; continue
    m=re.match(r'\s+T\+(\d+) present=\w+ x=([\d.]+) y=[\d.]+ z=([\d.]+) adj=(\d) ground=(\d) anim_t=(\d) ',l)
    if m: res[cur][int(m.group(1))]=(float(m.group(2)),float(m.group(3)),int(m.group(4)),int(m.group(5)),int(m.group(6)))
for k,v in res.items():
    bad=[(t,b[t],v[t]) for t in b if t in v and b[t]!=v[t]]
    print(k,'compared',len([t for t in b if t in v]),'diff',len(bad),bad[:5])
