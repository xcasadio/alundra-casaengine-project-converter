import json, sys
d = json.load(open(sys.argv[1], encoding='utf-8'))
lift = []
hero = None
for key, v in d.items():
    flags = v['MoreFlags'] | (v['CanPickup'] << 8) | (v['FlagsPortraitShadowType'] << 16)
    if key.startswith('192c2eeb'):
        hero = (key, v)
    if flags & 0x600:
        lift.append((key, v, flags))
print('liftable headers', len(lift))
for key, v, flags in lift:
    print(key[:8], 'bank', v['BankKey'], 'flags %06X' % flags, 'kind', (flags & 0x600) >> 9,
          'A', v['ProgramLoad'], 'C', v['ProgramTick'], 'D', v['ProgramTouch'], 'E', v['ProgramDeactivate'], 'F', v['ProgramInteract'],
          'brk', v['BreakEffect'], 'cont', v['Contents'])
print('C23', sorted(v['BankKey'] for k, v, f in lift if v['ProgramTick'] == 23))
print('E2', sorted(v['BankKey'] for k, v, f in lift if v['ProgramDeactivate'] == 2))
print('all C23 anywhere', sorted(v['BankKey'] for k, v in d.items() if v['ProgramTick'] == 23))
print('all E2 anywhere', sorted(v['BankKey'] for k, v in d.items() if v['ProgramDeactivate'] == 2))
for key, v, flags in lift:
    if v['ProgramTick'] == 23:
        sets = {a['Anim']: (a['Speed'], a['Acceleration'], a['IsZForceApplied'], a['Sfx']) for a in v['AnimSets']}
        ends = {}
        for e in v['IdsvAnimDirs']:
            ends.setdefault(e['Anim'], set()).add((e['End'], e.get('ChainTo')))
        print(v['BankKey'], 'sets', {a: sets[a] for a in sorted(sets) if a <= 5}, 'ends', {a: sorted(map(str, ends[a])) for a in sorted(ends) if a <= 5})
if hero:
    k, v = hero
    sets = {a['Anim']: (a['Speed'], a['Acceleration'], a['IsZForceApplied'], a['Sfx']) for a in v['AnimSets']}
    print('hero', k[:8], 9, sets.get(9), 10, sets.get(10))
