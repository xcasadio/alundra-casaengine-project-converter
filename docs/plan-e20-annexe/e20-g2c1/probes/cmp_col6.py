import re
Bf='D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g2c1-disc/replay/p1_col6.txt'
Df='dll/p1.txt'
names={'tonneau34':'tonneau22r21','tronc117':'tronc70r8','etoile107':'objet27r5'}
b={};cur=None
for l in open(Bf):
    m=re.match(r'== (\w+) moving',l)
    if m: cur=names[m.group(1)]; b[cur]={}; continue
    m=re.match(r'\s+T([+-]\d+) x=([\d.]+) z=([\d.]+) adj=(\d) ground=(\d) anim_t=(\d) dir_t=(\d+) lock=(\d)',l)
    if m: b[cur][int(m.group(1))]=tuple(map(float,m.groups()[1:]))
d={};cur=None
for l in open(Df,encoding='utf-8'):
    m=re.match(r'== (\w+?) moving wall column 6',l)
    if m: cur=m.group(1); d[cur]={}; continue
    if l.startswith('=='): cur=None; continue
    if not cur: continue
    m=re.match(r'\s+T([+-]?\d+) present=\w+ x=([\d.]+) y=[\d.]+ z=([\d.]+) adj=(\d) ground=(\d) anim_t=(\d) anim_c=\d+ dir_t=(\d+) lock=(\d)',l)
    if m: d[cur][int(m.group(1))]=tuple(map(float,m.groups()[1:]))
bad=0;n=0
for k,rows in b.items():
    for t,v in rows.items():
        n+=1
        if d[k].get(t)!=v: print('DIFF',k,t,v,d[k].get(t)); bad+=1
print('col6 compared',n,'diff',bad)
