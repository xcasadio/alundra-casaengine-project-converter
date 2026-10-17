import sys, os, json
sys.path.insert(0, 'tools/dll-structure')
import scan_types, check_names
sys.stdout.reconfigure(encoding='utf-8')
ext = scan_types.external_types()
cands = [
  ('Alundra.Ai.Objects', 'LiftableAi'),
  ('Alundra.Hero', 'CarryRules'),
  ('Alundra.Ai.Objects', 'CarryRules'),
  ('Alundra.Hero', 'ThrowCarried'),
  ('Alundra.Hero', 'Lifting'),
  ('Alundra.Ai.Objects', 'LiftableObjects'),
]
# DLL declared names at HEAD
decls, paths = scan_types.dll_decls(root=None)
top = [(d['path'], d['ns'], d['name']) for d in decls if not d['nested']]
print('DLL top-level types at HEAD:', len(top), 'files:', len(paths))
names_dll = {n for _,_,n in top}
segs = {s for _, ns, _ in top for s in (ns or '').split('.') if s}
for ns, n in cands:
    in_dll = n in names_dll
    in_ext = [(e['label'], e['ns'], e['name']) for e in ext.get(n.lower(), []) if not e['nested']]
    seg = n in segs
    print(f'{ns}.{n}: in DLL={in_dll}; external={in_ext}; equals a DLL namespace segment={seg}')
print()
# collision findings through the tool's own function
findings = check_names.collision_findings([('new', ns, n) for ns, n in cands], ext)
print('collision_findings:', findings)
print('existing segments under Alundra.Ai:', sorted(s for s in segs if s in ('Ai','Objects','Enemies','Projectiles')))
