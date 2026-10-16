import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fxlib as F
import world as W

m, meta = F.build()
print('target slots', meta['target_slots'], 'bank tables', len(meta['bank']))
pad = W.Pad()
for t in range(1, 6):
    m.tick_no = t
    m.poke(F.DISPLAY_PTR, F.DISPLAY_LIST)
    m.poke(F.DISPLAY_COUNT, 0)
    F.pad_tick_prefix(m, pad)
    for a in W.PASSES:
        W.run_pass(m, a)
    W.run_pass(m, 0x8003C410)
    act = F.active_slots(m)
    print('tick', t, 'active slots', [(i, F.slot_read(m, i)['id'], F.slot_read(m, i)['status'], F.slot_read(m, i)['mode'], F.slot_read(m, i)['counter'], F.slot_read(m, i)['done']) for i in act], 'display', F.display_entries(m)[:2])
print('journal', [str(j)[:80] for j in m.journal[:20]])
print('uninit reads:', len(m.uninit_reads))
for k, v in sorted(m.uninit_reads.items())[:40]:
    print('  pc %08X addr %08X size %d x%d' % (k[0], k[1], k[2], v))
