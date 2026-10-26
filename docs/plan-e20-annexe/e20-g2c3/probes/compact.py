import sys,re
for f in sys.argv[1:]:
    prev=None
    out=[]
    for line in open(f,encoding='utf-8'):
        line=line.rstrip('\n')
        if not line.startswith('t'):
            out.append(line); continue
        key=re.sub(r'c282=\d+ ','',line.split(' ',1)[1]) if True else line
        key=re.sub(r' z=\S+ fz=\S+','',key)
        key=re.sub(r' root=.*','',key)
        key=re.sub(r' snd\(.*','',key)
        if key!=prev or 'snd(' in line:
            out.append(line); prev=key
    open(f.replace('.txt','.c.txt'),'w',encoding='utf-8').write('\n'.join(out)+'\n')
