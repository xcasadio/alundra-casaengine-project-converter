import re,sys
pats=[('landing(ground=1)',r'D .*b274=2 .*ground=1'),('state3',r'D .*b274=3 '),('ride',r'ride=[HT]'),('state4',r'D .*b274=4 '),('snd145',r'snd\(145\)'),('torch st=Deactivated',r'T st=Deactivated'),('snd142',r'snd\(142\)'),('torch AC=1',r'T st=\w+ b274=1 anim_t=2 AC=1'),('torch FlagToDestroy',r'FlagToDestroy'),('torch gone',r'present=False'),('hero anim_t=0',r'H anim_t=0')]
for f in sys.argv[1:]:
    seen={}
    for line in open(f):
        if not line.startswith('t') : continue
        t=int(line.split(' ',1)[0][1:])
        if t>100: break
        for n,p in pats:
            if n not in seen and re.search(p,line): seen[n]=t
    print(f, seen)
