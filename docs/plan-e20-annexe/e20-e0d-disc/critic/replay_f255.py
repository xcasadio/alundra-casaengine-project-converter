import sys, os, itertools, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mips import *
HERO = 0x80127d30
CH = 0x80140000     # chest
OBJ = 0x80141000    # the spawned object
def run(contents, anim88, ctrl=0, ending=0, carried=0, dlg=0, spawn_ok=True, stock=None):
    m = Mem(); log = []
    cpu = CPU(m)
    stock = stock or {}
    m.w32(0x800DC4B8, ctrl); m.w32(0x800DC4C4, ending); m.w32(HERO + 0x2c, carried); m.w32(0x80152F08, dlg)
    m.w32(CH + 0x3c, contents); m.w32(CH + 0x88, anim88); m.w32(CH + 0x114, 1111); m.w32(CH + 0x118, 2222); m.w32(CH + 0x11c, 3333)
    m.w32(OBJ + 0x6c, 0x8ff0ff)  # some flags with bits 0x180
    def spawn(c):
        sp = c.r[29]
        a = (c.r[4], c.r[5], c.r[6], c.r[7], m.r32(sp + 0x10), m.r32(sp + 0x14), m.r32(sp + 0x18))
        log.append(('Spawn',) + tuple(hex(x) for x in a))
        return OBJ if spawn_ok else 0
    cpu.stubs[0x80039F58] = spawn
    cpu.stubs[0x8003AD30] = lambda c: (log.append(('Block', hex(c.r[4]))), 1)[1]
    cpu.stubs[0x800816D4] = lambda c: (log.append(('GetEtc', hex(c.r[4]))), 0x80300000 + c.r[4])[1]
    cpu.stubs[0x800450F0] = lambda c: (log.append(('OpenBox', hex(c.r[4]), c.r[5])), 0)[1]
    cpu.stubs[0x80049B7C] = lambda c: (log.append(('LoadBgm', c.r[4])), 0)[1]
    cpu.stubs[0x8004E428] = lambda c: stock.get(c.r[4], 0)
    cpu.call(0x8007FC88, (CH,))
    o = {k: m.r32(OBJ + k) for k in (0xb8, 0x278, 0x6c, 0x20, 0x274, 0x27c, 0x280, 0x284, 0x288)}
    return log, m.r32(CH + 0x88), o, cpu
if __name__ == '__main__':
    print(run(36, 0, stock={36: 3})[:3])
    print(run(36, 0, stock={36: 9})[:3])
    print(run(80, 0)[:3])
    print(run(2, 0)[:3])
    print(run(61, 0)[:3])
    print(run(0, 0)[:3])
    print(run(36, 1)[:3])
    print(run(36, 0, ctrl=4)[:3])
    print(run(36, 0, spawn_ok=False)[:3])
    print(run(36, 0, carried=5)[:3])
    print(run(36, 0, ending=1)[:3])
    print(run(36, 0, dlg=4)[:3])
