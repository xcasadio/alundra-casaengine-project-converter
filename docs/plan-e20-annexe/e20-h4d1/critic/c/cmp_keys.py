import json, sys
def load(p): return json.load(open(p))['rows']
def events(rows):
    ev = {}
    for r in rows:
        t = r['t']
        e = r['ents'].get('1')
        if e and 'born' not in ev: ev['born']=(t, e['status'])
        if e and e['status']==2 and 'st2' not in ev: ev['st2']=t
        if e and e['animend'] and 'animend' not in ev: ev['animend']=t
        if e and e['status']==4 and 'st4' not in ev: ev['st4']=t
        if r['journal'] and any(j[0]=='effect_3BDD8' for j in r['journal']) and 'fx' not in ev: ev['fx']=t
        if r['hero_anim_t']==0x34 and 'h34' not in ev: ev['h34']=t
        if r['ents'].get('1') is None and 'born' in ev and 'gone' not in ev: ev['gone']=t
        if r['inuse']==0 and t>5 and 'free' not in ev: ev['free']=t
        if r['hero_anim_t']==0 and 'h34' in ev and 'h0' not in ev: ev['h0']=t
    return ev
a = load(sys.argv[1]); b = load(sys.argv[2])
print('A', events(a)); print('B', events(b))
