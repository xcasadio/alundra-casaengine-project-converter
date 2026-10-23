"""op_sites_all.py - every program site of the opcodes 0x72, 0xA9, 0xAA over all exported maps (linear sweep limited to each program's range) + which maps are in the story chain. Read-only."""
import sys, glob, re, collections, os
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
chain={389,390,476,478,392,391,416,163,162,165,164,172,169,170,171,173,174,175,10,179,176,177,178,180,181,182,135,183,184,185}
want=(0x72,0xA9,0xAA)
res=collections.defaultdict(list)
maps=sorted({int(re.search(r'-(\d+)\.events\.json$',f.replace(chr(92),'/')).group(1)) for f in glob.glob(de.ROOT+'/alundra-project/Maps/*/*/events/*.events.json')})
for mp in maps:
    doc=de.load_map(mp)
    if doc is None: continue
    for slot,idx,st,en in de.all_programs(doc):
        for pc,op,v,bad in de.sweep(doc,st,en):
            if bad: break
            if op in want: res[op].append((mp,slot,idx,pc))
for op in want:
    sites=res[op]
    bymap=collections.Counter(s[0] for s in sites)
    print('opcode 0x%02X: %d sites on %d maps; in chain: %s' % (op,len(sites),len(bymap),sorted(m for m in bymap if m in chain)))
    print('   maps:',dict(sorted(bymap.items())))
