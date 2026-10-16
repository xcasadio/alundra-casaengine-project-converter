import json
fn=r"D:\development\repo\alundra-casaengine-project-converter\docs\plan-e20-annexe\e20-e0a\oracle\out\v6.json"
rows=[json.loads(l) for l in open(fn) if l.strip()]
for r in rows:
    seq=[]
    for t,tk in enumerate(r['ticks'],1):
        seq.append('t%d:%s'%(t,'/'.join('%s=%s'%(p[:3],tk[p]['delay']) for p in ('events','anim','physics') if p in tk)))
    print(r['id'],r['kind'],'| delay@events/anim/physics:',' '.join(seq[:3]),'| firstAnimWithDelayChange')
