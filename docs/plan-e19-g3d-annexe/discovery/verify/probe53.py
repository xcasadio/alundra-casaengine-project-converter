"""Independent probe: run the REAL 0x8003FE7C (opcode 0x82 handler) in the MIPS interpreter and compare with a model
written from my own reading of the disassembly of 0x800333AC. Written from scratch (not the report's harness)."""
import sys, os, random, struct
SP = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
sys.path.insert(0, os.path.join(SP, 'f3-disc', 'binary'))
sys.path.insert(0, os.path.join(SP, 'e19j-disc'))
import emu as E

ENT = 0x80127D30
POOL = 0x80138608
SEED = 0x80098708
CODE = 0x80180000   # scratch code buffer
CP = 0x80180100     # pointer word
ST = 0x80180200     # a3 state block (Result at +0x2C)
M32 = 0xFFFFFFFF


def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32


def run_real(sub, hp, hpmax, hx, hy, hz, seed, occupied):
    m = E.make_machine()
    sounds = []
    m.hooks[0x800490FC] = lambda mm: sounds.append(mm.r[4] & M32)
    m.ww(ENT + 0x14, hp)
    m.ww(ENT + 0x18, hpmax)
    m.ww(ENT + 0x114, hx)
    m.ww(ENT + 0x118, hy)
    m.ww(ENT + 0x11C, hz)
    m.ww(SEED, seed)
    for i in occupied:
        m.ww(POOL + 0x80 * i + 0x68, 2)
    m.wb(CODE, 0x82)
    m.wb(CODE + 1, sub)
    m.ww(CP, CODE)
    m.ww(ST + 0x2C, 0xDEADBEEF)
    size = m.call(0x8003FE7C, 0, 0, CP, ST) & M32
    slots = {}
    for i in range(128):
        b = POOL + 0x80 * i
        if m.rw(b + 0x68) != 0 and i not in occupied:
            slots[i] = dict(x=m.rws(b + 0x3C), y=m.rws(b + 0x40), z=m.rws(b + 0x44),
                            fx=m.rws(b + 0x54), fy=m.rws(b + 0x58), fz=m.rws(b + 0x5C),
                            eff=m.rb(b + 0x6E), anim=m.rb(b + 0x70), bank=m.rb(b + 0x6C), mode=m.rw(b + 0x34),
                            rec=m.rws(b + 0x30), status=m.rw(b + 0x68), recidx=m.rw(b + 4))
    return dict(size=size, result=m.rw(ST + 0x2C), hp=m.rws(ENT + 0x14), hpmax=m.rws(ENT + 0x18),
                seed=m.rw(SEED), sounds=sounds, slots=slots)


OX = [0, -543, -768, -543, 0, 543, 768, 543]
OY = [512, 362, 0, -362, -512, -362, 0, 362]


def model(hp, hpmax, hx, hy, hz, seed, occupied):
    occ = set(occupied)
    slots = {}

    def create():
        for i in range(128):
            if i not in occ:
                occ.add(i)
                s = dict(x=hx, y=hy, z=s32(hz + 0x100000), fx=0, fy=0, fz=0, eff=14, anim=0, bank=0, mode=0, rec=-1,
                         status=2, recidx=0)
                slots[i] = s
                return s
        return None
    hpmax2 = s32(hpmax + 1)
    for j in range(8):
        s = create()
        if s:
            s['x'] = s32(s['x'] + (OX[j] << 13))
            s['y'] = s32(s['y'] + (OY[j] << 13))
            s['fx'] = s32(-(OX[j] << 9))
            s['fy'] = s32(-(OY[j] << 9))
    for _ in range(4):
        s = create()
        if s:
            r1 = lcg(seed)
            r2 = lcg(r1)
            s['fx'] = s32(((r1 * 0x20001) >> 32) - 0x10000)
            s['fy'] = s32(((r2 * 0x30001) >> 32) - 0x18000)
            s['fz'] = 0x40000
            seed = r2
    return dict(size=2, result=1, hp=hpmax2, hpmax=hpmax2, seed=seed, sounds=[0x31], slots=slots)


def main(n=200, rseed=12345):
    rnd = random.Random(rseed)
    bad = 0
    for t in range(n):
        hp = rnd.randrange(0, 60)
        hpmax = rnd.randrange(1, 52)
        hx = rnd.randrange(-2**31, 2**31) if rnd.random() < 0.3 else rnd.randrange(0, 0x10000000)
        hy = rnd.randrange(-2**31, 2**31) if rnd.random() < 0.3 else rnd.randrange(0, 0x10000000)
        hz = rnd.choice([0, 0x100000, 0x7FF00000, 0x7FFFFFFF, rnd.randrange(-2**31, 2**31), rnd.randrange(0, 0x4000000)])
        seed = rnd.randrange(0, 2**32)
        k = rnd.choice([0, 0, 0, 1, 5, 20, 116, 117, 118, 119, 120, 124, 127, 128])
        if rnd.random() < 0.4:
            occupied = sorted(rnd.sample(range(128), min(128, rnd.randrange(0, 129))))
        else:
            occupied = list(range(k))
        # keep hx, hy as signed 32 stored values
        r = run_real(0x53, hp, hpmax, hx & M32, hy & M32, hz & M32, seed, occupied)
        mdl = model(hp, hpmax, s32(hx), s32(hy), s32(hz), seed, occupied)
        if r != mdl:
            bad += 1
            print("MISMATCH", t, dict(hp=hp, hpmax=hpmax, hx=hx, hy=hy, hz=hz, seed=hex(seed), occupied=len(occupied)))
            for key in ('size', 'result', 'hp', 'hpmax', 'seed', 'sounds'):
                if r[key] != mdl[key]:
                    print("  ", key, r[key], mdl[key])
            for i in sorted(set(r['slots']) | set(mdl['slots'])):
                if r['slots'].get(i) != mdl['slots'].get(i):
                    print("  slot", i, r['slots'].get(i), mdl['slots'].get(i))
                    break
    print("cases", n, "mismatches", bad)


if __name__ == '__main__':
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 200, int(sys.argv[2]) if len(sys.argv) > 2 else 12345)
