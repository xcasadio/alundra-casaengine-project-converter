import json,re,subprocess,collections,sys
t=json.load(open('tools/dll-structure/table.json'))
OLD='9502f75'
ren={}
for ty in t['types']:
    if ty['old_name']!=ty['final']['name']:
        ren[ty['old_name']]=ty['final']['name']
pat=re.compile(r'\b('+'|'.join(sorted(map(re.escape,ren),key=len,reverse=True))+r')\b')
def norm(txt):
    txt=pat.sub(lambda m:ren[m.group(1)],txt)
    txt=txt.replace('Alundra.Scripts.','')
    out=[]
    for l in txt.split('\n'):
        s=l.strip()
        if not s or s.startswith('using ') or s.startswith('namespace ') or s=='#nullable enable': continue
        out.append(s)
    return out
byold=collections.defaultdict(list)
for ty in t['types']:
    byold[ty['old_file']].append(ty['final']['file'])
def show(p): return subprocess.run(['git','show',f'{OLD}:{p}'],capture_output=True,text=True,encoding='utf-8',errors='replace').stdout
res={}
for of,news in sorted(byold.items()):
    o=collections.Counter(norm(show(of)))
    nt=''
    for nf in sorted(set(news)):
        nt+=open(nf,encoding='utf-8',errors='replace').read()+'\n'
    n=collections.Counter(norm(nt))
    lo=o-n; ln=n-o
    res[of]=(sum(lo.values()),sum(ln.values()),lo,ln)
tot=0
for of,(a,b,lo,ln) in res.items():
    if a or b:
        print(f'{of}: only-old={a} only-new={b}')
        if '-v' in sys.argv:
            for l,c in lo.items(): print('   - ',c,l[:170])
            for l,c in ln.items(): print('   + ',c,l[:170])
