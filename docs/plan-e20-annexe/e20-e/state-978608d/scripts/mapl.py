import subprocess,re,sys
OLD='9502f75'
def sh(*a):
    return subprocess.run(a,capture_output=True,text=True,encoding='utf-8',errors='replace').stdout
def hunks(oldp,newp):
    out=sh('git','diff','-M','-U0','--no-color',OLD,'HEAD','--',oldp,newp)
    hs=[]
    for m in re.finditer(r'^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@',out,re.M):
        a=int(m.group(1)); b=int(m.group(2)) if m.group(2) is not None else 1
        c=int(m.group(3)); d=int(m.group(4)) if m.group(4) is not None else 1
        hs.append((a,b,c,d))
    return hs
def mapline(hs,L):
    off=0
    for a,b,c,d in hs:
        # for b==0 a is the line AFTER which insertion happens
        if b==0:
            if a< L: off+=d
            elif a>=L: break
            continue
        if L<a: break
        if L<a+b:
            if b==d: return c+(L-a),'chg'
            return None,'hunk(-%d,%d +%d,%d)'%(a,b,c,d)
        off+= d-b
    return L+off,'same'
def old_lines(p): return sh('git','show',f'{OLD}:{p}').split('\n')
def new_lines(p): return open(p,encoding='utf-8',errors='replace').read().split('\n')
if __name__=='__main__':
    oldp,newp=sys.argv[1],sys.argv[2]
    hs=hunks(oldp,newp)
    ol=old_lines(oldp); nl=new_lines(newp)
    for spec in sys.argv[3:]:
        if '-' in spec: a,b=map(int,spec.split('-'))
        else: a=b=int(spec)
        for L in range(a,b+1):
            n,k=mapline(hs,L)
            o=ol[L-1].strip()[:110] if L-1<len(ol) else '<eof>'
            nn=nl[n-1].strip()[:110] if n and n-1<len(nl) else '-'
            print(f'{L:5d} -> {n} [{k}]\n   old: {o}\n   new: {nn}')
