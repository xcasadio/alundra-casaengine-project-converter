"""a1_state2_flags.py - T0 of E20.g2c3, (c): the four vectors of the state 2 of C71 whose pair (CollidedWithEntityZ +0x140, IsOnGround +0x144) is (1,0), (0,1), (1,1), (0,0),
produced on the REAL code of C71 (0x8007B834) by the interpreter of c71_full.py (imported, N = 1), in the format of c71_goldens.json.  The other words of the drop are drawn as in c71_full.py (seed 20261026)
and fixed here for the state 2; the vector (1,0) is the one that kills the mutant "the state 2 ignores CollidedWithEntityZ", (0,1) the one that kills "ignores IsOnGround".
Run (next to work/): python -B -X utf8 a1_state2_flags.py"""
import sys, json, hashlib, os
sys.argv = ['c71_full.py', '1']
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, 'c71_full.py'), encoding='utf-8').read()
src = src.replace('sys.exit(1 if (mism_a or mism_c) else 0)', 'pass').replace("print('A19 (3", "pass  # print('A19 (3").replace("print('C71 whole", "pass  # print('C71 whole")
ns = {'__file__': os.path.join(HERE, 'c71_full.py'), '__name__': 'c71_full_inline'}
exec(compile(src, 'c71_full.py', 'exec'), ns)


class _NS:
    def __getattr__(self, k):
        return ns[k]


C = _NS()
out = []
for collz, ground in ((1, 0), (0, 1), (1, 1), (0, 0)):
    v = C.mk_vector()
    d = v[3]
    d.set8(C.B274, 2)
    d.set32(C.COLLZ, collz)
    d.set32(C.GROUND, ground)
    d.set32(C.TYPE, 0x187)
    r = C.run_real(C.C71, v)
    m = C.run_model('c71', v)
    assert r == m, 'model differs from the real code'
    READ = [C.STATUS, C.TYPE, C.FLAGS, C.ANIM, C.CARRIED, C.ANIMDONE, C.PROGC, C.FZ, C.PZ, C.SUPPORT, C.COLLZ, C.GROUND]
    Ent = C.Ent
    out.append(dict(cls='st2 collz=%d ground=%d' % (collz, ground),
                    drop_in={'0x%X' % o: d.u32(o) for o in READ} | {'0x274': d.u8(C.B274), '0x275': d.u8(C.B275), '0x282': d.u16(C.C282)},
                    hero_carried=v[0].u32(C.CARRIED), cand_types={s: v[s].u32(C.TYPE) for s in (5, 6, 7)},
                    drop_out_changed={'0x%X' % o: Ent(r[0][3]).u32(o) for o in range(0, C.STRIDE, 4) if Ent(r[0][3]).u32(o) != d.u32(o)},
                    others_changed={s: {'0x%X' % o: Ent(r[0][s]).u32(o) for o in range(0, C.STRIDE, 4) if Ent(r[0][s]).u32(o) != v[s].u32(o)} for s in (0, 5, 6, 7) if r[0][s] != bytes(v[s].raw)},
                    global_clear=r[1]))
blob = json.dumps(out, sort_keys=True, indent=0).encode()
open(os.path.join(C.HERE, 'a1_state2_flags.json'), 'wb').write(blob)
for o in out:
    print(o['cls'], 'animation after =', o['drop_out_changed'].get('0x88', 'unchanged(%s)' % o['drop_in']['0x88']), 'state after =', hex(o['drop_out_changed'].get('0x274', o['drop_in']['0x274'])) if '0x274' in o['drop_out_changed'] else 'state unchanged')
print('sha256 %s' % hashlib.sha256(blob).hexdigest())
