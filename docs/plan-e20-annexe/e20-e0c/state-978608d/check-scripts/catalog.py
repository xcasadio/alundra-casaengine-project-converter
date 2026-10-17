import json, sys, collections
d = json.load(open(sys.argv[1], encoding='utf-8'))
keys = collections.Counter()
byk = {}
for g, r in d.items():
    k = r.get('BankKey')
    keys[type(k).__name__ + ':' + (k if isinstance(k, str) and not k.isdigit() else 'digit')[:12]] += 1
    if isinstance(k, str) and k.isdigit():
        byk.setdefault(int(k), []).append(r)
print('bankkey kinds', keys.most_common(6))
multi = [k for k, v in byk.items() if len(v) > 1]
print('global keys with several records', multi[:10])
a255c255 = []
others = []
for t in range(30, 119):
    for r in byk.get(t, []):
        A, C, D, E, F = r['ProgramLoad'], r['ProgramTick'], r['ProgramTouch'], r['ProgramDeactivate'], r['ProgramInteract']
        flags = r['MoreFlags'] | (r['CanPickup'] << 8) | (r['FlagsPortraitShadowType'] << 16)
        if A == 255 and C == 255:
            a255c255.append(t)
        else:
            others.append((t, A, C, D, E, F, r['Contents']))
        if t in (30, 66, 99, 100, 101, 102, 111, 114, 115, 116, 72):
            print(t, 'A', A, 'C', C, 'D', D, 'E', E, 'F', F, 'Contents', r['Contents'], 'flags 0x%X' % flags, 'size', r['SizeX'], r['SizeY'], r['SizeZ'])
print('A=C=255 count', len(a255c255), 'range', min(a255c255), max(a255c255), 'missing in 31..118', [t for t in range(31, 119) if t not in a255c255])
print('others', others)
print('contents of A=C=255', collections.Counter(r['Contents'] for t in a255c255 for r in byk[t]))
# global registrations elsewhere with 255 programs
c255 = [t for t, v in byk.items() for r in v if r['ProgramTick'] == 255]
a255 = [t for t, v in byk.items() for r in v if r['ProgramLoad'] == 255]
print('global C=255', len(c255), 'A=255', len(a255), 'outside 30..118 C', [t for t in c255 if not 30 <= t <= 118], 'A', [t for t in a255 if not 30 <= t <= 118])
