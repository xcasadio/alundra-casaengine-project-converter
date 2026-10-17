import random, sys
from lh import *
import models as M
from c255model import Img
random.seed(int(sys.argv[1]) if len(sys.argv) > 1 else 3)
w = World(); m = w.m
E_ADDR = ENT0 + 9 * STRIDE; CHEST = ENT0 + 3 * STRIDE
GBANK = 0x801EB344; TBANK = 0x801EBA40
J = []
m.hooks[0x8003A774] = lambda mc: J.append(('destroy_light',))
m.hooks[0x8005A9E0] = lambda mc: J.append(('cd', mc.r[4] & M32))
def a255_model(e, ch, gb, tb):
    """0x80061BD4"""
    j = []
    if e.g(0x68) == 0x1E:
        if e.g(0x3C) != 0: return j
        e.s(0x88, 1); return j
    st = e.g(0x274)
    if st == 1: return j
    if st == 2:
        if ch.g(0x88) == 1:
            if e.g(0x288) != 0: j.append(('cd', 0xB))
            return j
        e.s(0x10, 1); return j
    e.s(0x6C, e.g(0x6C) & 0xFFFFFF7F)       # and with -0x81
    f = e.g(0x40)
    if f != 0:
        v = f & 0xFFFF
        bank = tb if (v & 0x8000) else gb
        word = bank[((v >> 3) & 0xFFC) // 4]
        # the bit index uses the full 32-bit word of +0x40 (lw), signed modulo like the binary: v1 - ((v1 + (v1<0?31:0)) >> 5 << 5)
        f32 = M.s32(f)
        idx = f32 - (((f32 + (31 if f32 < 0 else 0)) >> 5) << 5)
        bit = (word >> (idx & 31)) & 1
        if bit == 0:
            j.append(('destroy_light',)); return j
    e.s(0x27C, 0); e.s(0x280, M32); e.s(0x284, 0); e.s(0x278, f)
    return j
bad = 0; n = 0; cnt = {}
for it in range(20000):
    img = bytearray(STRIDE); I = Img(img)
    I.s(0x68, random.choice([0x1E, 0x1E, 0x45 + 0x1E, 0x24 + 0x1E, 0x54 + 0x1E, 0x1F]))
    I.s(0x3C, random.choice([0, 0, 5, 0x45]))
    I.s(0x274, random.choice([0, 0, 1, 2, 3]))
    I.s(0x280, random.choice([CHEST, CHEST, 0xFFFFFFFF, 7]))
    I.s(0x288, random.choice([0, 1]))
    if I.g(0x274) == 2: I.s(0x280, CHEST)
    I.s(0x6C, random.getrandbits(32))
    fl = random.choice([0, 0, 0, 1, 31, 32, 0x7FF, 0x8000, 0x8001, 0x8000 | 1720, 1720, 0xFFFF, 0x12345678, 0x1000F])
    I.s(0x40, fl)
    ch = bytearray(STRIDE); CH = Img(ch); CH.s(0x88, random.choice([0, 1, 2]))
    gb = [random.getrandbits(32) if random.random() < 0.5 else 0 for _ in range(1024)]
    tb = [random.getrandbits(32) if random.random() < 0.5 else 0 for _ in range(1024)]
    # banks are 256 bytes in the game; but the code may index up to 0xFFC bytes with a wild flag: keep them in distinct non-overlapping areas -> use 4 KiB windows
    # put the banks at their addresses but only 0x100 words so that they do not overlap each other; flags above 0x7FF would read the other bank: model with a single flat memory
    mem = bytearray(0x1000)  # unused
    for i in range(64): m.ww(GBANK + 4 * i, gb[i] & M32); m.ww(TBANK + 4 * i, tb[i] & M32)
    flat = {GBANK + 4 * i: gb[i] for i in range(64)}; flat.update({TBANK + 4 * i: tb[i] for i in range(64)})
    for a in range(0x0, 0x1000, 4):
        pass
    # restrict wild flags: skip vectors whose word index leaves the 64-word windows (the real game never has them: InitializeContents zeroes (f & 0x7FFF) >= 0x800, and A255 reads +0x40 which is that value)
    v = fl & 0xFFFF
    if fl != 0 and ((v >> 3) & 0xFFC) // 4 >= 64: continue
    if fl != 0 and (fl >> 16) != 0: continue
    for i in range(STRIDE): m.wb(E_ADDR + i, img[i]); m.wb(CHEST + i, ch[i])
    J.clear()
    w.call(0x80061BD4, E_ADDR)
    em = Img(bytearray(img)); chm = Img(bytearray(ch))
    jm = a255_model(em, chm, gb, tb)
    real = bytes(m.ram[E_ADDR & 0x1FFFFFFF:(E_ADDR & 0x1FFFFFFF) + STRIDE])
    n += 1
    if list(J) != jm or real != bytes(em.raw):
        bad += 1
        if bad < 6: print('MISMATCH A255', hex(fl), I.g(0x274), J, jm, [(o, hex(int.from_bytes(real[o:o+4], 'little')), hex(int.from_bytes(em.raw[o:o+4], 'little'))) for o in range(0, STRIDE, 4) if real[o:o+4] != em.raw[o:o+4]][:6])
print('A255 vectors', n, 'mismatches', bad)
fn = set(range(0x80061BD4, 0x80061D14, 4))
print(' coverage', len(m.cov & fn), '/', len(fn), [hex(a) for a in sorted(fn - m.cov)])
