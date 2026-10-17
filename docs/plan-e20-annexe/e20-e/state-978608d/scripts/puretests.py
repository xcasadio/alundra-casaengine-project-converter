import json,re,subprocess,collections,sys,os
t=json.load(open('tools/dll-structure/table.json'))
OLD='9502f75'
ren={}
for ty in t['types']:
    if ty['old_name']!=ty['final']['name']:
        ren[ty['old_name']]=ty['final']['name']
pat=re.compile(r'\b('+'|'.join(sorted(map(re.escape,ren),key=len,reverse=True))+r')\b')
def norm(txt):
    txt=pat.sub(lambda m:ren[m.group(1)],txt)
    txt=txt.replace('Alundra.Scripts.','').replace('\ufeff','')
    out=[]
    for l in txt.split('\n'):
        s=l.strip()
        if not s or s.startswith('using ') or s.startswith('namespace ') or s=='#nullable enable': continue
        out.append(s)
    return out
def run(*a): return subprocess.run(a,capture_output=True,text=True,encoding='utf-8',errors='replace').stdout
oldfiles=[f for f in run('git','ls-tree','-r','--name-only',OLD,'Alundra.Tests').split('\n') if f.endswith('.cs')]
newfiles=[f for f in run('git','ls-tree','-r','--name-only','HEAD','Alundra.Tests').split('\n') if f.endswith('.cs')]
print('old tests',len(oldfiles),'new tests',len(newfiles))
print('added:',sorted(set(newfiles)-set(oldfiles)))
print('removed:',sorted(set(oldfiles)-set(newfiles)))
nd=0
for f in sorted(set(oldfiles)&set(newfiles)):
    o=collections.Counter(norm(run('git','show',f'{OLD}:{f}')))
    n=collections.Counter(norm(open(f,encoding='utf-8',errors='replace').read()))
    lo=o-n; ln=n-o
    if lo or ln:
        nd+=1
        print(f'{f}: -{sum(lo.values())} +{sum(ln.values())}')
        if '-v' in sys.argv:
            for l,c in lo.items(): print('   - ',c,l[:200])
            for l,c in ln.items(): print('   + ',c,l[:200])
print('files with residual',nd)
