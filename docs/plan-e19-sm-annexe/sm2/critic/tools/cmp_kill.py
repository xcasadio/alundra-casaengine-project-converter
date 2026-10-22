import re, sys, collections
sys.path.insert(0,'tools')
theirs={}
cur=None
for line in open('C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm2/evidence/killmatrix.txt'):
    m=re.match(r'\s+(M\d+)\s',line)
    if m: cur=m.group(1); continue
    m=re.match(r'\s+killed by: (.*)',line)
    if m and cur: theirs[cur]=set(x.strip() for x in m.group(1).split(','))
mine={}
for line in open('evidence/my_killmatrix.txt'):
    m=re.match(r'(M\d+b?) .*?\s+red\s+\d+: (.*)',line)
    if m: mine[m.group(1)]=m.group(2).split()
def canon(n):
    # map my names to their families
    if n.startswith('H2X<'): return 'H2X<'
    if n.startswith('H2X'): return 'H2X'
    if n.startswith('H2Y'): return 'H2Y'
    if n.startswith('H2Z'): return 'H2Z'
    if n.startswith('H5'): return 'H5'
    if n=='H7-120': return 'H7a'
    if n=='H7-119': return 'H7b'
    if n=='H9-6B': return 'H9X'
    if n=='H9-6C': return 'H9Y'
    if n=='H9-6D': return 'H9Z'
    return n
for k in sorted(theirs,key=lambda s:int(s[1:])):
    key=k if k!='M5' else 'M5'
    mm=set(canon(n) for n in mine.get(key,[]))
    t=theirs[k]
    print(k,'\n   mine only:',sorted(mm-t),'\n   theirs only:',sorted(t-mm))
