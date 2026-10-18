import json, sys, collections
d = json.load(open(sys.argv[1], encoding='utf-8-sig'))
banks = collections.Counter(v['BankKey'] for v in d.values())
print('n', len(d), 'sample bank keys', list(banks.items())[:15])
rows = []
for k, v in d.items():
    f = v['MoreFlags'] | (v['CanPickup'] << 8) | (v['FlagsPortraitShadowType'] << 16)
    rows.append((v['BankKey'], k, f, v))
def show(bk):
    for b, k, f, v in rows:
        if b == bk:
            print(b, hex(f), 'L', v['ProgramLoad'], 'C', v['ProgramTick'], 'D', v['ProgramTouch'], 'E', v['ProgramDeactivate'], 'F', v['ProgramInteract'],
                  'off', (v['OffsetX'], v['OffsetY'], v['OffsetZ']), 'size', (v['SizeX'], v['SizeY'], v['SizeZ']), 'brk', v['BreakEffect'], 'cont', v['Contents'])
for bk in sys.argv[2:]:
    show(bk)
# DestroyOnSlidingSlope census
ds = [(b, hex(f), bool(f & 0x100)) for b, k, f, v in rows if f & 0x100000]
print('DestroyOnSlidingSlope count', len(ds))
for x in sorted(ds, key=lambda t: t[0]):
    print('  ', x)
dv = [(b, hex(f)) for b, k, f, v in rows if f & 0x200000]
print('DestroyOnVramFlags count', len(dv), dv)
