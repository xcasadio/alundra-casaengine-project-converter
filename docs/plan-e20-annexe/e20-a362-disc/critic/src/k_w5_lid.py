"""k_w5_lid.py - h3a (w5) candidate key at the oracle level: a lid (global sprite 243, header E = 19, break effect 11) born by the real SpawnWarpEntity, put to status 3 (Deactivated) by the harness after tick K,
as the opcode 0x19 of its program D does.  The native E19 (0x8007F690, real code) runs at the next tick: what the journal shows (sound, break effect), the status 4 and the free slot.
usage: k_w5_lid.py [K] [N].   The harness writes: the lid's position (SpawnWarpEntity), its status (+0x10 = 3 once, after tick K).   Written for the a362-h3a discovery (2026-10-25)."""
import sys
import world as W
from scen_lib import *
from scen_zone import spawn
K = int(sys.argv[1]) if len(sys.argv) > 1 else 5
N = int(sys.argv[2]) if len(sys.argv) > 2 else 20
m, STATS = mk(None, indices=(1, 2, 243))
H = W.ent(0)
pad = W.Pad()
lid = spawn(m, H, 0, 243, 300, 300, 0)
assert lid, 'the lid was not born'
print('lid slot %#x born at (%.2f,%.2f,%.2f) status %d type %d flags %#x break-effect byte' % (lid, m.peeks(lid + 0x114) / 65536, m.peeks(lid + 0x118) / 65536, m.peeks(lid + 0x11C) / 65536, m.peeks(lid + 0x10), m.peeks(lid + 0x68), m.peek(lid + 0x6C)))
prev = None
for t in range(0, N + 1):
    m.tick_no = t
    j0 = len(m.journal)
    W.tick(m, pad, 0, check_pad=False)
    if t == K:
        m.poke(lid + 0x10, 3)
    jr = [(x[1], x[2] if x[1] == 'sound' else (x[1], tuple(x[2][:10]) if isinstance(x[2], (list, tuple)) else x[2])) for x in m.journal[j0:] if x[1] in ('sound', 'effect_3BDD8', 'effect_3BE74', 'destroy') and not (x[1] == 'sound' and x[2] == 0)]
    st = (m.peeks(lid + 0x10), m.peeks(lid + 0x68))
    if (st, tuple(map(str, jr))) != prev:
        print('k%+3d lid(status,type)=%s journal=%s' % (t - K, st, jr))
    prev = (st, tuple(map(str, jr)))
print('journal kinds seen:', sorted({x[1] for x in m.journal}))
