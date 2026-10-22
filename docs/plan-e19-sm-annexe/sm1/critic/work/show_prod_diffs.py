import sys
sys.argv=['x','dummy']
sys.path.insert(0,'work')
import gen_compare as G, mymodel as M, replay_binary_lib as RB, random, json
seed=int(sys.argv[1]) if False else 1
r=random.Random(seed)
OPS=G.OPS
found=[]
for k in range(20000):
    op=OPS[k%4]
    w=G.make_world(r,op)
    v=G.to_bin_vector(w)
    res,v0,bad,above=RB.run(v,True)
    proxies,spawned,player=G.dll_world(w,'prod')
    mp=M.run(op,proxies[w['logic']],w['code'],spawned,player)
    if mp!=res:
        found.append((k,w,res,mp))
print(len(found))
for k,w,res,mp in found:
    print(k,'op',hex(w['op']),'bin',res,'model',mp,'code',w['code'],'logic',w['logic'],'n',w['n'],'anon',w['anon'])
    for i,s in enumerate(w['slots']): print('  ',i,s)
