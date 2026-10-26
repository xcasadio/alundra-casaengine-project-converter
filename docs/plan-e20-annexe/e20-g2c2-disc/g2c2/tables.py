"""tables.py - locates the six native-handler tables of ALUN_CD.EXE (France) by anchoring on known handler addresses and prints the
addresses of A19, C71, C70, D34, E20, A0, E0. Run: python -B -I -X utf8 tables.py"""
import sys
sys.path.insert(0, '.')
import bx
anchors = {'C70': (0x8007B7B0, 70), 'E20': (0x8007F6C8, 20), 'D34': (0x8007E8AC, 34), 'C23': (0x8006B848, 23), 'C4': (0x80066984, 4), 'E2': (0x8007ED30, 2), 'C60': (0x8007A8A0, 60), 'E9': (0x8007EF50, 9)}
bases = {}
for name, (addr, code) in anchors.items():
    hits = [a for a in range(0x80020000, 0x800CA800 - 3, 4) if bx.u32(a) == addr]
    print(name, hex(addr), 'entries at', [hex(h) for h in hits], 'base', [hex(h - 4 * code) for h in hits])
    bases[name] = [h - 4 * code for h in hits]
# slot bases from anchors: C base, D base, E base; A base inferred by the table spacing
Cb = bases['C70'][0]; Db = bases['D34'][0]; Eb = bases['E20'][0]
print('C base %x, D base %x, E base %x' % (Cb, Db, Eb))
# the slots are probably consecutive tables of 256 entries: A, B, C, D, E, F
size = Db - Cb
print('C->D spacing', hex(size), 'D->E spacing', hex(Eb - Db))
Ab = Cb - 2 * size
Bb = Cb - size
for nm, b in (('A', Ab), ('B', Bb), ('C', Cb), ('D', Db), ('E', Eb)):
    print(nm, 'base %x' % b, 'code0 ->', hex(bx.u32(b)))
print('A19 ->', hex(bx.u32(Ab + 4 * 19)), ' C71 ->', hex(bx.u32(Cb + 4 * 71)), ' A0 ->', hex(bx.u32(Ab)), ' E0 ->', hex(bx.u32(Eb)), ' E1 ->', hex(bx.u32(Eb + 4)))
