"""tracker_check.py - D2: proves the unset-byte tracker is live.  One target is written WITHOUT its +0x1D0 field; the real hit pass must be reported as
reading the unset bytes (pc 0x8003941C).  writes out/tracker_check.txt"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import hitworld as H, cases as C

W = H.World()
W.restore()
W.blank_slots({1, 2})
a = C.attacker(C.atk_addr(C.SWORD[1], C.SWORD[2]), C.BASE_MP)
t = C.E(flags=H.CLASS_A, sheet=C.rec_addr(C.NEUTRAL[1]), mp=C.BASE_MP, tsize=(H.size(8),) * 3)
del t['c1d0']                                    # the field the hit pass tests first on a target
W.put(1, **a); W.put(2, **t); W.put(0, anim_target=0)
W.pre_passes(); W.hit_pass()
rows = sorted((hex(pc), hex(ad), n, c) for (pc, ad, n), c in W.m.uninit_reads.items())
txt = 'unset-byte reads reported when target +0x1D0 is left unwritten: %r\n' % (rows,)
open(os.path.join(HERE, '..', 'out', 'tracker_check.txt'), 'w').write(txt)
print(txt)
