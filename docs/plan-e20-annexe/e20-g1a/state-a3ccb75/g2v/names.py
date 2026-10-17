import sys
sys.path.insert(0, sys.argv[1])
import scan_types, check_names
ext = scan_types.external_types()
decls, paths = scan_types.dll_decls()
top = [d for d in decls if not d['nested']]
names = [(d['path'], d['ns'], d['name']) for d in top]
base = check_names.collision_findings(names, ext)
new = [('Alundra/Ai/Objects/LiftableAi.cs', 'Alundra.Ai.Objects', 'LiftableAi'),
       ('Alundra/Hero/CarryRules.cs', 'Alundra.Hero', 'CarryRules'),
       ('Alundra/Ai/Objects/PickupAi.cs', 'Alundra.Ai.Objects', 'PickupAi')]
withnew = check_names.collision_findings(names + new, ext)
print('base findings', len(base))
print('with new findings', len(withnew))
for l in withnew:
    if l not in base:
        print('NEW', l)
dll_names = {d['name'] for d in decls}
for _, _, n in new:
    print(n, 'in DLL decls (incl nested):', n in dll_names, '| external:', [e['label'] + ':' + e['ns'] + '.' + e['name'] for e in ext.get(n.lower(), [])])
