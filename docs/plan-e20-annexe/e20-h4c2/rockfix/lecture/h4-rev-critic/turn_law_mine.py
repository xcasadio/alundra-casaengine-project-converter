def step_bin(cur,d):
    if cur==d: return cur
    if d<cur: return d if cur-d<4 else cur-4
    return d if d-cur<4 else cur+4
def steps_bin(cur,d):
    n=0
    while cur!=d:
        cur=step_bin(cur,d); n+=1
        assert n<20
    return n
def step_short(cur,d):
    diff=abs(d-cur)
    if diff<=16: return step_bin(cur,d)
    # opposite side, step 4 max, &31, not overshooting
    # distance going the wrap way
    dist = 32-diff
    if dist<4: return d
    # direction: if d>cur go down (cur-4) wrapping, else go up (cur+4)
    return (cur-4)&31 if d>cur else (cur+4)&31
def steps_short(cur,d):
    n=0
    while cur!=d:
        cur=step_short(cur,d); n+=1
        assert n<20
    return n
big=[(c,d) for c in range(32) for d in range(32) if abs(c-d)>16]
print('pairs',32*32,'|diff|>16:',len(big))
sb=[steps_bin(c,d) for c,d in big]; ss=[steps_short(c,d) for c,d in big]
print('binary steps min/max/mean',min(sb),max(sb),sum(sb)/len(sb))
print('short steps min/max/mean',min(ss),max(ss),sum(ss)/len(ss))
print('31->0',steps_bin(31,0),'0->31',steps_bin(0,31))
# path of 31->0
c=31;p=[c]
while c!=0: c=step_bin(c,0);p.append(c)
print(p)
# diff exactly 16
print('diff==16 pairs bin vs short differ?', any(step_bin(c,d)!=step_short(c,d) for c in range(32) for d in range(32) if abs(c-d)==16))
