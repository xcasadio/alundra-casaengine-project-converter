import sys, glob, re, json, collections
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
R='D:/development/repo/alundra-casaengine-project-converter'
res=collections.defaultdict(list)
for f in sorted(glob.glob(R+'/alundra-project/Maps/*/*/events/*.events.json')):
    f=f.replace(chr(92),'/'); m=int(re.search(r'-(\d+)\.events\.json$',f).group(1))
    ev=json.load(open(f,encoding='utf-8'))
    try: mp=json.load(open(R+'/data-extracted/data/map_%d.json'%m,encoding='utf-8-sig'))
    except Exception: continue
    progs={(s,i):(st,en) for s,i,st,en in de.all_programs(ev)}  # keyed (slot, first index)
    # build map from (slot, table index) -> (start,end)
    def span(slot,idx):
        tab=ev['EventCodes%sTable'%slot]
        if idx>=len(tab): return None
        st=tab[idx]
        starts=sorted({s for X in 'ABCDEF' for s in ev['EventCodes%sTable'%X]}|{len(ev['Codes'])})
        nx=[s for s in starts if s>st]
        return st,(nx[0] if nx else len(ev['Codes']))
    for k,e in enumerate(mp['SpriteInfo']['Entities']['Entities']):
        if e is None or not e['IsEnabled']: continue
        for slot,key in (('A','EventCodesA_LoadIndex'),('C','EventCodesC_TickIndex')):
            idx=e[key]
            if idx==0: continue
            idx7=idx&0x7F
            sp=span(slot,idx7)
            if not sp: continue
            for pc,op,v,bad in de.sweep(ev,sp[0],sp[1]):
                if v is None: break
                if op==0x41 and v[1]==2 and v[2]==4:
                    res[m].append((k,e['SpriteTableIndex'],slot,idx,pc))
tot=0
for m,l in sorted(res.items()):
    print(m, len(l), [(k,t) for k,t,_,_,_ in l]); tot+=len(l)
print('total',tot)
