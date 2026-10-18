import sys, collections
sys.path.insert(0, 'tools/dll-structure')
import scan_types
ext = scan_types.external_types()
decls, paths = scan_types.dll_decls()
dll = collections.defaultdict(list)
segs = set()
for d in decls:
    dll[d['name'].lower()].append(d)
    for s in (d['ns'] or '').split('.'):
        if s: segs.add(s)
for n in sys.argv[1:]:
    e = [x for x in ext.get(n.lower(), []) if not x['nested']]
    print(n, '| external:', [(x['label'], x['ns'], x['name']) for x in e][:5], '| in DLL:', [(x['path'], x['name']) for x in dll.get(n.lower(), []) if not x['nested']][:3], '| is a segment:', n in segs)
