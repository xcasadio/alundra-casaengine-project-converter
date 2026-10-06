import sys, os
SP = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
sys.path.insert(0, os.path.join(SP, 'f3-disc', 'binary')); sys.path.insert(0, os.path.join(SP,'e19j-disc'))
import emu as E
from probe53 import *
for occ in ([], list(range(121)), list(range(128))):
    m = E.make_machine()
    m.hooks[0x800490FC] = lambda mm: None
    m.ww(ENT+0x14, 5); m.ww(ENT+0x18, 10); m.ww(ENT+0x114, 1000); m.ww(ENT+0x118, 2000); m.ww(ENT+0x11C, 3000)
    for i in occ: m.ww(POOL+0x80*i+0x68, 2)
    m.wb(CODE,0x82); m.wb(CODE+1,0x53); m.ww(CP,CODE); m.ww(ST+0x2c,0)
    before = bytes(m.ram)
    m.call(0x8003FE7C, 0,0,CP,ST)
    after = bytes(m.ram)
    diffs = [i for i in range(len(before)) if before[i]!=after[i]]
    # group in 0x80-byte regions
    regs = {}
    for d in diffs:
        a = 0x80000000 + d
        if POOL <= a < POOL+0x80*128: key = 'pool slot %d' % ((a-POOL)//0x80)
        elif 0x801FFE00 <= a < 0x80200000: key='stack'
        else: key = hex(a & ~3)
        regs[key] = regs.get(key,0)+1
    keys = list(regs)
    print("occ", len(occ), "n changed bytes", len(diffs))
    print([k for k in keys if not k.startswith('pool slot')], "pool slots:", len([k for k in keys if k.startswith('pool')]))
