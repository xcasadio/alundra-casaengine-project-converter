import sys,re
sys.path.insert(0, sys.argv[0].rsplit('/',1)[0] if '/' in sys.argv[0] else '.')
from linemap import build
old_path,new_path=sys.argv[1],sys.argv[2]
old,new,m,sm=build(old_path,new_path)
strip=lambda s: re.sub(r'Alundra(?=[A-Z])','',s).strip()
print(f"== {old_path} -> {new_path}  ratio={sm.ratio():.4f}")
n=0;trivial=0
for tag,i1,i2,j1,j2 in sm.get_opcodes():
    if tag=='equal': continue
    o=[strip(x) for x in old[i1:i2]]; w=[strip(x) for x in new[j1:j2]]
    if o==w or all(re.match(r'^(using |namespace )',x) for x in o+w) or (tag=='insert' and all(re.match(r'^(using |namespace |\s*$)',x) for x in w)):
        trivial+=1; continue
    n+=1
    print(f"-- {tag} old[{i1+1}-{i2}] new[{j1+1}-{j2}]")
    for x in old[i1:min(i2,i1+8)]: print("   - "+x.strip()[:170])
    for x in new[j1:min(j2,j1+8)]: print("   + "+x.strip()[:170])
print("nontrivial hunks:",n,"trivial:",trivial)
