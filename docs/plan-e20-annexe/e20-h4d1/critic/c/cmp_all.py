import json,glob,os
def ev(rows):
    out={}
    for r in rows:
        t=r['t']; e=r['ents'].get('1')
        if e and 'born' not in out: out['born']=t
        if e and e['status']==2 and 'st2' not in out: out['st2']=t
        if e and e['animend'] and 'animend' not in out: out['animend']=t
        if e and e['status']==3 and 'st3' not in out: out['st3']=t
        if e and e['status']==4 and 'st4' not in out: out['st4']=t
        if any(j[0]=='effect_3BDD8' for j in r['journal']) and 'fx' not in out: out['fx']=t
        if r['hero_anim_t']==0x34 and 'h34' not in out: out['h34']=t
        if r['hero_anim_t']==0 and 'h34' in out and 'h0' not in out: out['h0']=t
        if r['inuse']==0 and t>5 and 'free' not in out: out['free']=t
    return out
for f in sorted(glob.glob('keys_faithful/fire2_*.json')):
    if 'targets' in f: continue
    A=json.load(open(f))['rows']
    B=json.load(open('../h4d1/keys/'+os.path.basename(f)))['rows']
    print(os.path.basename(f)); print('   faithful', ev(A)); print('   report  ', ev(B))
