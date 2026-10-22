"""Self-test: an address the handler reads that is not in the mapping stops the generator (here: the field +0x6C is removed from the mapping
and the search of 0x88 reads it)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import temu, sm4model as S
import gen_vectors as G
m = temu.make_machine()
w = G.world([G.hero(flags=0x80), G.npc(1, flags=0x80)])
G.make_vector(m, 0x88, 'selftest/ok', 'edge', 'ok', bytes([0x88, 0x84, 1]), w, 1, 0, 3)
print('mapped run: ok')
saved = S.SLOT_FIELDS.pop(0x6C)
try:
    G.make_vector(m, 0x88, 'selftest/unmapped', 'edge', 'x', bytes([0x88, 0x84, 1]), w, 1, 0, 3)
    print('NOT STOPPED (bad)')
    sys.exit(1)
except G.Stop as e:
    print('STOPPED as expected:', e)
finally:
    S.SLOT_FIELDS[0x6C] = saved
