"""s7_frame.py - the REAL frame function 0x8002BAEC (and the real UpdateWorld 0x8002E058, map events 0x8003C67C, entity block 0x8003B388 with its real passes, 0x8003C410) on the MIPS
machine, with the scratchpad (0x1F8003FC, the entity block's private stack) mapped to free RAM.  Stubs (journalled): the pad sampler 0x8002E38C, the sound frame 0x8004B1D4,
DisplayInventory 0x80055570 (return value chosen by the vector), MovePlayer 0x80031B50, the effects pass 0x80038E84 and the hit pass 0x80039300; each of the last three performs ONE real
draw (ChooseRandomlyAnItem(0x80080) -> row 0, always exactly one draw) so the order of draws against the advance is observable in the RNG word.
Output: out/frame_trace.json, printed table."""
import sys
import json
sys.path.insert(0, 'tools')
sys.path.insert(0, '.')
import emu
import c1world as C
from machine import Machine, M32, s32
import espawn as X

RNG = 0x80098708
GATE = 0x800DC4B8
REQ = 0x800DC4C4          # the loop-exit request word
SCR = 0x1FD000            # physical RAM used for the scratchpad 0x1F800000..0x1F8003FF
SIDE_SP = 0x801FE800
NAMES = {0x8002E38C: 'pad_sampler(stub)', 0x8002E058: 'UpdateWorld', 0x8003C67C: 'map_events', 0x8003B388: 'entity_block', 0x80038634: 'pass_slots', 0x800386D0: 'pass_events',
         0x80038998: 'pass_counters', 0x800384F4: 'pass_lists', 0x80038E18: 'pass_anim', 0x80038364: 'pass_physics', 0x80038E84: 'pass_effects(stub+draw)',
         0x80039300: 'pass_hits(stub+draw)', 0x8003C410: 'effects_list', 0x80055570: 'DisplayInventory(stub)', 0x8004B1D4: 'sound_frame(stub)', 0x80031B50: 'MovePlayer(stub+draw)'}


def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32


class FM(Machine):
    def __init__(self):
        super().__init__()
        self.trace = []
        self.watch_calls = set(NAMES)

    def _a(self, addr):
        a = addr & 0x1FFFFFFF
        if 0x1F800000 <= a < 0x1F800400:
            return SCR + (a - 0x1F800000)
        return a

    def _chk(self, a, n):
        pass

    def _mark(self, a, n):
        pass

    def exec1(self, pc, w):
        if pc == 0x8002BD34:                                   # the store of the advance: v0 = new state, memory still holds the old one
            self.trace.append(('ADVANCE', 'RNG store at 0x8002BD34', self.peek(RNG), self.r[2] & M32))
        br = super().exec1(pc, w)
        if br is not None and br[0] == 'j' and (w >> 26) == 3 and br[1] in self.watch_calls:
            self.trace.append(('call', NAMES[br[1]], self.peek(RNG), None))
        return br


X.Machine = FM
w = X.World(map_no=14)
m = w.m
m.watch_uninit = False
inv_ret = {'v': 1}


def stub(name, draw=False, ret=0):
    def f(mm):
        if draw:
            C.side_call(mm, 0x80032968, (0x80,), SIDE_SP)
        mm.r[2] = ret(mm) if callable(ret) else ret
    return f


m.hooks[0x8002E38C] = stub('pad')
m.hooks[0x8004B1D4] = stub('sound')
def inv_stub(mm):
    mm.r[2] = inv_ret['v']


m.hooks[0x80055570] = inv_stub
m.hooks[0x80031B50] = stub('move', draw=True)
m.hooks[0x80038E84] = stub('fx', draw=True)
m.hooks[0x80039300] = stub('hits', draw=True)


def run(a0, gate, seed, trigger=False, inv=1, expect_req=None, label=''):
    m.trace = []
    m.cov.clear()
    m.poke(RNG, seed)
    m.poke(GATE, gate)
    m.poke(REQ, 0)
    m.poke(0x800DC4B4, 0)
    m.poke(0x80127164, 0)
    m.poke(0x800C4980, 0)
    m.poke(0x80127D50, 0)          # hero +0x20
    m.poke(0x80126F28, 0)          # hold word (hu at +0x10 of 0x80126F18)
    m.poke(0x80126F2A, 0x0800 if trigger else 0)   # pressed word (hu at +0x12): Start edge
    m.poke(0x80126E68, 0)
    inv_ret['v'] = inv
    # make the machine's own free slot state sane: hero only
    m.poke(0x80134360, 0)
    m.call(0x8002BAEC, a0, limit=20_000_000)
    st = m.peek(RNG)
    n = 0
    s = seed
    while s != st and n < 12:
        s = lcg(s)
        n += 1
    ok = (s == st)
    trace = [(k, nm, ('%08X' % r) if r is not None else None, ('%08X' % r2) if r2 is not None else None) for (k, nm, r, r2) in m.trace]
    req = m.peek(REQ)
    # which real code ran: did the map event table walk (0x8003C6C4) run ?  did the entity passes run ?
    cov = {'events_walk_0x8003C6C4': 0x8003C6C4 in m.cov, 'slot_pass_0x80038634': 0x80038634 in m.cov, 'lists_0x800384F4': 0x800384F4 in m.cov, 'events_pass_0x800386D0': 0x800386D0 in m.cov,
           'effects_list_body_0x8003C444': 0x8003C444 in m.cov, 'advance_0x8002BD34': 0x8002BD34 in m.cov}
    return dict(label=label, a0=a0, gate=gate, trigger=trigger, inventory_returns=inv, rng_in='%08X' % seed, rng_out='%08X' % st, steps=n, steps_ok=ok, request_word=req, trace=trace, coverage=cov)


results = []
S = 0x12345678
for a0 in (0, 1):
    for gate in (0x00, 0x08, 0x40, 0x48, 0x34, 0x10, 0x80, 0xFF):
        results.append(run(a0, gate, S, label='gate sweep'))
# trigger vectors: Start edge, everything else clear
results.append(run(0, 0x00, S, trigger=True, inv=1, label='trigger, DisplayInventory returns 1 (opened)'))
results.append(run(0, 0x00, S, trigger=True, inv=0, label='trigger, DisplayInventory returns 0 (refused: the loop exits)'))
results.append(run(1, 0x00, S, trigger=True, inv=0, label='post-load call a0=1, trigger, returns 0: request cleared at 0x8002BD3C'))
results.append(run(0, 0x08, S, trigger=True, inv=0, label='gate 0x08: trigger refused (gate word must be 0)'))
for seed in (0x00000000, 0xFFFFFFFF, 0xB017C93D, 0x80000000):
    results.append(run(0, 0x48, seed, label='advance only, seed sweep'))

json.dump(results, open('out/frame_trace.json', 'w'), indent=0)
for r in results:
    chain = ' > '.join(('ADV' if k == 'ADVANCE' else nm) for (k, nm, a, b) in r['trace'])
    print('%-62s a0=%d gate=%02X in %s out %s steps %d %s req=%d' % (r['label'], r['a0'], r['gate'], r['rng_in'], r['rng_out'], r['steps'], 'ok' if r['steps_ok'] else 'NOT-LCG', r['request_word']))
    if r['label'] != 'gate sweep' or r['gate'] in (0x00, 0x48):
        print('     ', chain)
        print('      cov', {k: int(v) for k, v in r['coverage'].items()})
