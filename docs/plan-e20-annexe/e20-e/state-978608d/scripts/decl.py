import re,sys
f=sys.argv[1]; lines=open(f,encoding='utf-8',errors='replace').read().split('\n')
pat=re.compile(r'^\s*(public|internal|private)\s+(static\s+)?(async\s+)?(void|Task|\w[\w<>\[\],\s\(\)]*?)\s+(\w+)\s*\(')
for spec in sys.argv[2:]:
    n=int(spec)
    i=n-1
    while i>=0:
        m=pat.match(lines[i])
        if m and not lines[i].strip().startswith('return') and '=>' not in lines[i].split('(')[0]:
            # find attribute start
            j=i
            while j>0 and lines[j-1].strip().startswith('['): j-=1
            print(f'{f}:{n} -> in {m.group(5)} (decl line {i+1}, attrs from {j+1})')
            break
        i-=1
