import re,sys
def parse(path):
    d={};cur=None
    for l in open(path,encoding='utf-8'):
        m=re.match(r'== (\S+) type (\d+) (\S+) tpf=(\d)',l)
        if m: cur=(m.group(1),m.group(3),int(m.group(4)));d[cur]=dict(st={},eff=None,snd=[],or_={});continue
        m=re.match(r'  K=(\d+) present=(\S+) st=(\S+) target=(\d+) flags=(\S+) ground=(\d) z=(\S+) x=(\S+) E2=(\d+) or=(\S+) and=(\S+) slope=(\d+)',l)
        if m: k=int(m.group(1));d[cur]['st'][k]=(m.group(2),m.group(3),m.group(9));d[cur]['or_'][k]=(m.group(10),m.group(11));continue
        m=re.match(r'  effects: \(tick (\d+) id (\d+)\)',l)
        if m: d[cur]['eff']=(int(m.group(1)),int(m.group(2)))
        if l.startswith('  sounds:'):
            d[cur]['snd']=[(int(a)-1,int(b)) for a,b in re.findall(r'tick (\d+) sfx (\d+)',l) if int(a)>1]
    return d
def summ(v):
    st=v['st'];first_f=next((k for k in sorted(st) if st[k][1] in('FlagToDestroy','Destroyed')),None)
    absent=next((k for k in sorted(st) if st[k][0]=='False'),None)
    e2=next((k for k in sorted(st) if st[k][2]!='0'),None)
    orf=next((k for k in sorted(st) if v['or_'][k][0] not in('0',)),None)
    return dict(or_first=orf, flag_destroy=first_f, absent=absent, e2_first=e2, eff=v['eff'], snd=v['snd'])
TPF=int(sys.argv[1])
for f in sys.argv[2:]:
    d=parse(f);print('###',f)
    for k in d:
        if k[2]==TPF:
            print(k[0],k[1],summ(d[k]))
