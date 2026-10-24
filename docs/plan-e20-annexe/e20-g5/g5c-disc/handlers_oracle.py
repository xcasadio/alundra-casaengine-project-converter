"""handlers_oracle.py - the four sound opcode handlers on the REAL code (minimal MIPS interpreter): 0xBD 0x80041C38 (Play, 16-bit id), 0xBE 0x80041C6C (Stop, 16-bit id), 0x12 0x8003D6C0 (Play, 8-bit id), 0x75 0x8003FA9C (Stop, 8-bit id).
Each handler is called as the dispatcher does (a2 = address of the code pointer, [a2] -> the instruction bytes) with the PlaySoundEffect 0x800490FC and StopSoundEffect 0x80049634 journalled; the model: the id read, the callee, the returned length.
Run: python -B -X utf8 handlers_oracle.py [N]"""
import os, random, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'work')); sys.path.insert(0, os.path.join(HERE, 'work', 'emu'))
from h import World, M32
W = World(); m = W.m; J = []
m.hooks[0x800490FC] = lambda mm: J.append(('play', mm.r[4] & M32))
m.hooks[0x80049634] = lambda mm: J.append(('stop', mm.r[4] & M32))
N = int(sys.argv[1]) if len(sys.argv) > 1 and sys.argv[1].isdigit() else 400
HANDLERS = {0xBD: (0x80041C38, 'play', 16, 3), 0xBE: (0x80041C6C, 'stop', 16, 3), 0x12: (0x8003D6C0, 'play', 8, 2), 0x75: (0x8003FA9C, 'stop', 8, 2)}
PTR, CODE = 0x80140100, 0x80140200
rnd = random.Random(1024)
bad = 0
for k in range(N):
    for op, (addr, callee, bits, length) in HANDLERS.items():
        b1, b2 = rnd.choice([0, 1, 25, 85, 255, rnd.randrange(256)]), rnd.choice([0, 0, 1, 2, 3, 255, rnd.randrange(256)])
        m.ww(PTR, CODE)
        for i, b in enumerate((op, b1, b2, rnd.randrange(256), rnd.randrange(256))): m.wb(CODE + i, b)
        J.clear()
        ret = m.call(addr, 0, 0, PTR, 0)
        want_id = (b1 | (b2 << 8)) if bits == 16 else b1
        if J != [(callee, want_id)] or ret != length:
            bad += 1
            if bad < 4: print('MISMATCH', hex(op), b1, b2, J, ret)
print('vectors %d x 4 handlers, mismatches %d' % (N, bad))
for op, (addr, callee, bits, length) in HANDLERS.items():
    print('  0x%02X @0x%08X: %s(id from %s), returns %d' % (op, addr, 'StopSoundEffect' if callee == 'stop' else 'PlaySoundEffect', '(b2<<8)|b1' if bits == 16 else 'b1', length))
