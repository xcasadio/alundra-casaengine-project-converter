"""op_be_sites.py - every program site of 0xBE (binary: StopSoundEffect 0x80049634) and 0xBD (PlaySoundEffect) over all exported maps; the ids; the 30 chain maps. Read-only."""
import sys, glob, re, collections
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
chain={389,390,476,478,392,391,416,163,162,165,164,172,169,170,171,173,174,175,10,179,176,177,178,180,181,182,135,183,184,185}
maps=sorted({int(re.search(r'-(\d+)\.events\.json$',f.replace(chr(92),'/')).group(1)) for f in glob.glob(de.ROOT+'/alundra-project/Maps/*/*/events/*.events.json')})
sites=collections.defaultdict(list)
for mp in maps:
    doc=de.load_map(mp)
    if doc is None: continue
    for slot,idx,st,en in de.all_programs(doc):
        for pc,op,v,bad in de.sweep(doc,st,en):
            if bad: break
            if op in (0xBD,0xBE): sites[op].append((mp,slot,idx,pc,v[1]|(v[2]<<8)))
for op in (0xBD,0xBE):
    s=sites[op]
    ids=collections.Counter(x[4] for x in s)
    print('0x%02X: %d sites on %d maps ; on chain maps: %d ; ids (id:count) %s' % (op,len(s),len({x[0] for x in s}),sum(1 for x in s if x[0] in chain),dict(sorted(ids.items(),key=lambda kv:-kv[1])[:12])))
# pairs: a 0xBD id followed (within the same program) by a 0xBE of the same id
pairs=0
for mp in maps:
    doc=de.load_map(mp)
    if doc is None: continue
    for slot,idx,st,en in de.all_programs(doc):
        seq=[(op,v[1]|(v[2]<<8)) for pc,op,v,bad in de.sweep(doc,st,en) if not bad and op in (0xBD,0xBE)]
        for i,(op,i_) in enumerate(seq):
            if op==0xBD and any(o==0xBE and j==i_ for o,j in seq[i+1:i+3]): pairs+=1
print('0xBD immediately followed (<=2 sound opcodes later) by a 0xBE of the same id:',pairs)
