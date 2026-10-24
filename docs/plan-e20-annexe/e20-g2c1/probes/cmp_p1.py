import re,sys
B='D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g2c1-disc/g2c1/out/q8_p1.txt'
D=sys.argv[1]
def parse_bin():
    res={};cur=None
    for l in open(B,encoding='utf-8'):
        m=re.match(r'== (\w+?)\d+ (moving|still)',l)
        if m: cur=(m.group(1),m.group(2)); res[cur]={}; continue
        m=re.match(r'\s+t\d+ \(T([+-]\d+)\) x=([\d.]+) y=([\d.]+) z=([\d.]+) adj\(\+0x13C\)=(\d) ground=(\d) anim_t=(\d) dir_t=(\d+) lock\(\+0x278\)=(\d) .*journal=(.*)$',l)
        if m:
            k=int(m.group(1)); fx=re.findall(r"fx\(id=(\d+),a2=0,x=([\d.]+),y=([\d.]+),z=([\d.]+)\)",m.group(10))
            res[cur][k]=dict(x=float(m.group(2)),z=float(m.group(4)),adj=int(m.group(5)),anim=int(m.group(7)),dir=int(m.group(8)),lock=int(m.group(9)),fx=[tuple(map(float,f)) for f in fx])
    return res
def parse_dll():
    res={};cur=None
    for l in open(D,encoding='utf-8'):
        m=re.match(r'== (\w+?)\d+r\d+ (moving|still) wall column 5',l)
        if m: cur=(m.group(1),m.group(2)); res[cur]={}; continue
        if l.startswith('=='): cur=None; continue
        if cur is None: continue
        m=re.match(r'\s+T([+-]?\d+) present=\w+ x=([\d.]+) y=([\d.]+) z=([\d.]+) adj=(\d) ground=(\d) anim_t=(\d) anim_c=\d+ dir_t=(\d+) lock=(\d) .* hit=\d+ e2=\d+ (.*)$',l)
        if m:
            k=int(m.group(1)); fx=re.findall(r"fx\(9,([\d.]+),([\d.]+),([\d.]+)\)",m.group(10))
            res[cur][k]=dict(x=float(m.group(2)),z=float(m.group(4)),adj=int(m.group(5)),anim=int(m.group(7)),dir=int(m.group(8)),lock=int(m.group(9)),fx=[(9.0,)+tuple(map(float,f)) for f in fx])
    return res
b=parse_bin(); d=parse_dll()
names={'tonneau':'tonneau','tronc':'tronc','etoile':'objet'}
bad=0
for (bn,mv),rows in b.items():
    dn=names[bn]
    for k,bv in rows.items():
        if k<0: continue
        dv=d[(dn,mv)].get(k)
        if dv is None: print('MISSING',bn,mv,k); bad+=1; continue
        diff={f:(bv[f],dv[f]) for f in bv if f!='fx' and abs(bv[f]-dv[f])>1e-6}
        if bv['fx']!=dv['fx']: diff['fx']=(bv['fx'],dv['fx'])
        if diff: print('DIFF',bn,mv,'T%+d'%k,diff); bad+=1
print('compared',sum(1 for r in b.values() for k in r if k>=0),'rows; differing',bad)
