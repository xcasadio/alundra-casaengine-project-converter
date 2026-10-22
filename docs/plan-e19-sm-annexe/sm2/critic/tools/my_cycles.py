import sys, json
sys.path.insert(0,'tools')
from mycorpus import *
from my_memoflow import successors
SUSP={0x00,0x37,0x1C,0x1D,0x36,0x0B,0x1E,0x1F,0x20,0x21,0x22,0x23,0x24,0x25,0x26,0xFF}
sites=json.load(open('evidence/my_sites_6A_6D.json'))
progs=sorted(set((m,s,i) for m,s,i,pc,op in sites))
bad=0
for m,s,i in progs:
    ev,tm,b=load(m); codes=ev['Codes']; entry=table(ev,s)[i]
    # reachable & marks
    seen=walk_program(codes,entry,set())
    marks=set()
    for pc,op in seen.items():
        if op in (0x78,0x79): marks.add(pc+3)
        if op in (0x7B,0x7C): marks.add(pc+5)
    # graph restricted to non-suspending nodes
    nodes=[pc for pc,op in seen.items() if op not in SUSP]
    ns=set(nodes)
    adj={pc:[q for q in successors(codes,pc,entry,marks) if q in ns] for pc in nodes}
    # detect cycle via DFS
    color={}
    def dfs(u):
        color[u]=1
        for v in adj[u]:
            if color.get(v,0)==1: return True
            if color.get(v,0)==0 and dfs(v): return True
        color[u]=2; return False
    sys.setrecursionlimit(10000)
    cyc=any(color.get(u,0)==0 and dfs(u) for u in nodes)
    if cyc: bad+=1; print('non-suspending cycle in',m,s,i)
print('programs',len(progs),'with a non-suspending cycle:',bad)
