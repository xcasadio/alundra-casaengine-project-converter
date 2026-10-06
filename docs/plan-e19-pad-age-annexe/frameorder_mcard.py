"""D-E19-99: the memory card process (0x8005EC98, the save screen's state machine = the port's RunState) and slot 0's Square reader, real code, binary frame order
(RenderScene then Update(0) with the real pad sampler 0x8002E38C fed by a PHYSICAL schedule). Reports the frame in which each reader reacts to a physical press."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import emu, drive
import frameorder as fo

SQUARE, CROSS = drive.SQUARE, drive.CROSS
STATE = 0x800C4980            # memory card process state (0x3f5 = wait for Square after the Saved/Failed message)
MODE = 0x80191098             # 1 or 2: 0x8005F458 (the big state function); 3: 0x8005ED2C
FLAG98C = 0x800C498C          # set to 1 by the Square branch together with the call 0x80050C64


def new():
    m = drive.new_machine()
    m.ww(MODE, 1)
    m.ww(STATE, 0x3f5)
    m.ww(0x800C4984, 0x3e8)     # the handler's own counter compare (0x800601B0-0x800601B8): 0x3e8 = armed
    m.ww(FLAG98C, 0)
    return m


def run(phys, frames, hook_sq):
    m = new()
    st = {'f': 0}
    m.hooks[fo.PADREAD] = lambda mm: mm.__setitem__(2, 0) if False else _pr(mm, st, phys)
    hits = []
    def h50c64(mm):
        hits.append(st['f'])
    m.hooks[0x80050C64] = h50c64
    for f in range(1, frames + 1):
        st['f'] = f
        m.call(0x8005EC98)               # RenderScene: UpdateMemoryCardProcess, then (dispatcher: nothing here)
        m.call(fo.SAMPLER)               # Update(0): pad sampler
    return hits, m


def _pr(mm, st, phys):
    mm.r[2] = phys(st['f']) & 0xFFFF


if __name__ == '__main__':
    P = 7
    hits, m = run(lambda f: SQUARE if f == P else 0, 15, None)
    print('state 0x3f5, physical Square (one sample) at frame %d: 0x80050C64 (Square branch) called in frames %s -> P+%s' % (P, hits, [h - P for h in hits]))
    hits, m = run(lambda f: SQUARE if f in (P, P + 1, P + 2) else 0, 15, None)
    print('Square held 3 samples from %d: calls in frames %s' % (P, hits))
