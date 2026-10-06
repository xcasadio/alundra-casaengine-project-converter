"""Independent fuzz of the REAL emitter 0x8002DB48 (ALUN_CD.EXE France) with a hand-built element (not an effect machine).
Expectations written from my own reading of the disassembly:
  screen anchor = ((x >> 16) - camX, ((y - z) >> 16) - camY)  [int32]
  cull          = emitted iff -128 <= ax <= 448 and -128 <= ay <= 368
  OT slot       = 16 * min(key >> 20 [signed], 59) + 6
  vertex i      = anchor + (Xi, Yi) as signed bytes
  UV            = (u,v) (u+w,v) (u,v+h) (u+w,v+h) bytes (8-bit wrap)
  semi          = code bit 1 == flags bit 3
  chain         = image 0 inserted first, so the OT slot chain head is image N-1
"""
import sys, os, random
SP = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
sys.path.insert(0, os.path.join(SP, "f3-disc", "binary")); sys.path.insert(0, os.path.join(SP, "e19j-disc"))
import emu as E

m = E.make_machine()
ELEM = 0x801E0000; IMGS = 0x801E1000; PRIMS = 0x801C0000; OT = 0x801D8000; PTRCELL = 0x801E8000
CAMX, CAMY = 0x80126F10, 0x80126F14

def i32(v):
    v &= 0xFFFFFFFF
    return v - (1 << 32) if v & 0x80000000 else v
def s8(b): return b - 256 if b & 0x80 else b

random.seed(12345)
bad = 0; emitted_cases = 0; culled_cases = 0; chain_bad = 0
for trial in range(2500):
    n = random.randint(1, 4)
    imgs = []
    for k in range(n):
        flags = random.randint(0, 255)
        im = dict(flags=flags, clut=random.randint(0, 255), u=random.randint(0, 255), v=random.randint(0, 255), w=random.randint(0, 255), h=random.randint(0, 255),
                  c=[random.randint(-128, 127) for _ in range(8)])
        imgs.append(im)
        a = IMGS + 14 * k
        for j, b in enumerate([flags, im["clut"], im["u"], im["v"], im["w"], im["h"]] + [c & 0xFF for c in im["c"]]):
            m.wb(a + j, b)
    if random.random() < 0.7:
        x = random.randint(-(200 << 16), 600 << 16); y = random.randint(-(200 << 16), 600 << 16)
    else:
        x = random.randint(-(1 << 31), (1 << 31) - 1); y = random.randint(-(1 << 31), (1 << 31) - 1)
    z = random.choice([0, random.randint(-(100 << 16), 100 << 16), (random.randint(0, 64) << 16) + random.choice((0, 1, -1)), random.randint(-(1 << 31), (1 << 31) - 1)])
    cx = random.randint(-100, 900); cy = random.randint(-100, 700)
    key = random.choice([random.randint(0, 70 << 20), random.randint(-(3 << 20), 0), random.randint(0, (1 << 31) - 1)])
    m.ww(CAMX, cx & 0xFFFFFFFF); m.ww(CAMY, cy & 0xFFFFFFFF)
    m.ww(ELEM + 0, IMGS); m.ww(ELEM + 4, x & 0xFFFFFFFF); m.ww(ELEM + 8, y & 0xFFFFFFFF); m.ww(ELEM + 0xC, z & 0xFFFFFFFF)
    m.ww(ELEM + 0x10, key & 0xFFFFFFFF); m.ww(ELEM + 0x18, n); m.ww(ELEM + 0x1C, 0); m.ww(ELEM + 0x20, 0)
    m.ww(PTRCELL, PRIMS)
    # clear the OT window around every slot that could be touched (negative indexes too)
    for i in range(-60000, 1100): m.ww(OT + 4 * i, 0)
    ret = m.call(0x8002DB48, ELEM, PTRCELL, OT, 0, limit=5_000_000)
    ax = (x >> 16 if x >= 0 else -((-x + 0xFFFF) >> 16)) if False else (i32(x) >> 16) - cx
    ay = (i32(i32(y) - i32(z)) >> 16) - cy
    expect = (-128 <= ax <= 448) and (-128 <= ay <= 368)
    if not expect:
        culled_cases += 1
        if ret != 0:
            bad += 1; print("cull mismatch", trial, (x, y, z, cx, cy), ret)
        continue
    emitted_cases += 1
    if ret != n:
        bad += 1; print("count mismatch", trial, ret, n); continue
    kslot = max(min(i32(key) >> 20, 59), -10**6) if True else 0
    kslot = min(i32(key) >> 20, 59)
    otslot = 16 * kslot + 6
    for k, im in enumerate(imgs):
        p = PRIMS + 0x28 * k
        verts = [(m.rhs(p + 0x08), m.rhs(p + 0x0A)), (m.rhs(p + 0x10), m.rhs(p + 0x12)), (m.rhs(p + 0x18), m.rhs(p + 0x1A)), (m.rhs(p + 0x20), m.rhs(p + 0x22))]
        exp = [(ax + im["c"][0], ay + im["c"][1]), (ax + im["c"][2], ay + im["c"][3]), (ax + im["c"][4], ay + im["c"][5]), (ax + im["c"][6], ay + im["c"][7])]
        uv = [(m.rb(p + 0x0C), m.rb(p + 0x0D)), (m.rb(p + 0x14), m.rb(p + 0x15)), (m.rb(p + 0x1C), m.rb(p + 0x1D)), (m.rb(p + 0x24), m.rb(p + 0x25))]
        u, v, w, h = im["u"], im["v"], im["w"], im["h"]
        euv = [(u, v), ((u + w) & 255, v), (u, (v + h) & 255), ((u + w) & 255, (v + h) & 255)]
        semi = (m.rb(p + 7) >> 1) & 1
        ok = verts == exp and uv == euv and semi == ((im["flags"] >> 3) & 1)
        if not ok:
            bad += 1; print("prim mismatch", trial, k, verts, exp, uv, euv, semi)
    # OT chain at the expected slot: head = last image
    a = m.rw(OT + 4 * otslot) & 0x00FFFFFF
    chain = []
    for _ in range(8):
        if a == 0: break
        full = 0x80000000 | a
        chain.append((full - PRIMS) // 0x28)
        a = m.rw(full) & 0x00FFFFFF
    if chain != list(range(n - 1, -1, -1)):
        chain_bad += 1; print("chain mismatch", trial, "key", key, "slot", otslot, chain)
print("trials", 2500, "emitted", emitted_cases, "culled", culled_cases, "mismatches", bad, "chain mismatches", chain_bad)
