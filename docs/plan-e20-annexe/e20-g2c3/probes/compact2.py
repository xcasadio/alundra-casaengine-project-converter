import sys,re
# contact files: keep rows as printed (they already filter on change) but drop c282 and root noise
for f in sys.argv[1:]:
    prev=None; out=[]
    for line in open(f,encoding='utf-8'):
        line=line.rstrip('\n')
        if not line.startswith('t'):
            out.append(line); continue
        k=re.sub(r'c282=\d+ ','',line.split(' ',1)[1])
        k=re.sub(r' z=\S+ fz=\S+','',k)
        k=re.sub(r' snd\([^|]*','',k)
        if k!=prev or 'snd(' in line:
            out.append(line); prev=k
    open(f.replace('.txt','.c.txt'),'w',encoding='utf-8').write('\n'.join(out)+'\n')
