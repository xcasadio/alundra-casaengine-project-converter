"""O-E19-77 counter-check, binary side (independent driver).
Frame f = the REAL slot dispatcher 0x80048054 (RenderScene's callback loop + post-process tail), then the WHOLE REAL Update(0)
0x8002BAEC (a0 = 0) - sampler 0x8002E38C at 0x8002BB5C (PadRead 0x8008A688 hooked to the physical word of frame f), soft-reset block,
UpdateWorld 0x8002E058 hooked to a no-op (no world), trigger block, 0x8004B1D4 stubbed.
Snapshot after each phase. Stubs for drawing/HUD/portrait/text/equip reuse the discovery's list (inv_bin.STUBS), nothing else.
Output: per sample s, the binary state after RS(s+1) (= after Update(s) and the next render, the port's tick s), and the
sounds of Update(s) + RS(s+1)."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import emu
sys.path.insert(1, os.path.join(HERE, '..', 'bin'))
import inv_bin  # only for its STUBS / QUIET tables

PAD = 0x80126F18
PADREAD, DISPATCHER, UPDATE = 0x8008A688, 0x80048054, 0x8002BAEC
UPDATEWORLD, AFTER_TRIG = 0x8002E058, 0x8004B1D4
FLAG6, SEL6, FLAG4, SEL4 = 0x8017FEF4, 0x8017FF28, 0x8017F340, 0x8017F734
CTRL, POST, CDREADY = 0x800DC4B8, 0x80153194, 0x800A82B0
SLOT = lambda s: 0x80153028 + 28 * s

START, SELECT, L1, L2, R2, TRI = 0x800, 0x100, 0x4, 0x1, 0x2, 0x10
UP, RIGHT, DOWN, LEFT, CROSS = 0x1000, 0x2000, 0x4000, 0x8000, 0x40


def machine():
    m = emu.make_machine()
    for a, name in inv_bin.STUBS.items():
        m.hooks[a] = (lambda nm: (lambda mm: (None if nm in inv_bin.QUIET else mm.log.append(('stub', nm)), mm.r.__setitem__(2, 0))))(name)
    m.hooks[UPDATEWORLD] = lambda mm: mm.r.__setitem__(2, 0)
    m.hooks[AFTER_TRIG] = lambda mm: mm.r.__setitem__(2, 0)
    m.ww(PAD, 20); m.ww(PAD + 4, 0); m.ww(PAD + 8, 0); m.ww(PAD + 12, 0)
    for o in (0x10, 0x12, 0x14, 0x16): m.wh(PAD + o, 0)
    m.ww(CDREADY, 1)
    for a in (FLAG6, SEL6, FLAG4, SEL4, CTRL, POST, 0x80127D50, 0x80127164, 0x800DC4B4, 0x800C4980, 0x801EB424):
        m.ww(a, 0)
    m.ww(0x801EB410, 0x40000000)
    for s in range(13): m.wh(SLOT(s), 0)
    return m


def snap(m):
    return (m.rw(FLAG6), m.rw(SEL6), m.rw(FLAG4), m.rw(SEL4), m.rw(POST), 1 if m.rw(CTRL) & 8 else 0)


def run(presses, P, frames):
    """presses: {rel sample: word}. Returns {rel sample s: (state after RS(s+1), sounds of Update(s)+RS(s+1))}."""
    m = machine()
    after_rs, after_up, snd_rs, snd_up = {}, {}, {}, {}
    for f in range(1, P + frames + 2):
        m.log.clear()
        m.call(DISPATCHER)
        after_rs[f] = snap(m); snd_rs[f] = [e[1] for e in m.log if e[0] == 'sound']
        m.log.clear()
        w = presses.get(f - P, 0)
        m.hooks[PADREAD] = (lambda word: (lambda mm: mm.r.__setitem__(2, word & 0xFFFF)))(w)
        m.call(UPDATE, 0)
        after_up[f] = snap(m); snd_up[f] = [e[1] for e in m.log if e[0] == 'sound']
    out = {}
    for f in range(1, P + frames + 1):
        out[f - P] = (after_rs[f + 1], snd_up[f] + snd_rs[f + 1])
    return out


if __name__ == '__main__':
    r = run({0: START}, 5, 25)
    for s in sorted(r): print(s, r[s])
