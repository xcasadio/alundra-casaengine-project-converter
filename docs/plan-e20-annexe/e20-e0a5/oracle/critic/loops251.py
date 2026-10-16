import os,sys
os.environ['SPR']=sys.argv[1] if len(sys.argv)>1 else '251'
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)),'tools'))
import trace_kinds as K
m,run,t0=K.scenario('move')
by={}
for t,nm,d in run.log:
    by.setdefault(t,{})[nm]=d
S=run.created_tick
print('S=',S)
for t in range(S,S+14):
    a=by[t]['anim']
    print('S+%-2d frame=%s cnt=%d loops(+B0)=%d'%(t-S,a['frame'],a['cnt'],a['b0']))
