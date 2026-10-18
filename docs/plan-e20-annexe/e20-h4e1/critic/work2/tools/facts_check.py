"""facts_check.py - h4e1 discovery: re-reads, in ALUN_CD.EXE (France, SHA-1 a705f8da...), the data and code points the h4e1 facts rest on (read-only; capstone through tools/lib.py)."""
import sys, os, struct, hashlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
print('exe sha1', hashlib.sha1(lib.DATA).hexdigest())
def u32(a): return lib.u32(a)
def u16(a):
    o = a - lib.BASE + lib.OFF
    return struct.unpack_from('<H', lib.DATA, o)[0]
print('table 0x800235E0 (u16 x9):', [u16(0x800235E0 + 2 * k) for k in range(9)])
print('pilot jump table 0x800235F4, item 0x31 (index %d): %#010x ; 0x2B (index %d): %#010x ; 0x30: %#010x' % (0x31 - 0x1F, u32(0x800235F4 + 4 * (0x31 - 0x1F)), 0x2B - 0x1F, u32(0x800235F4 + 4 * (0x2B - 0x1F)), u32(0x800235F4 + 4 * (0x30 - 0x1F))))
print('cancel table 0x800233EC: item 0x31 -> %#010x ; 0x2B -> %#010x ; 0x2C -> %#010x ; 0x30 -> %#010x ; 0x2F -> %#010x' % tuple(u32(0x800233EC + 4 * (i - 0x1F)) for i in (0x31, 0x2B, 0x2C, 0x30, 0x2F)))
print('neutral cancel body 0x800351A0:')
for i in lib.dis(0x800351A0, 6):
    print('   %08x %-8s %s' % (i.address, i.mnemonic, i.op_str))
print('start table 0x8002339C (holes), item 0x31 entry:', '%#010x' % u32(0x8002339C + 4 * (0x31 - 0x1F)) if True else '')
print('tome XA byte 0x80023365+item 0x31:', lib.DATA[0x80023365 - lib.BASE + lib.OFF + 0x31])
# the map-load template init 0x8003B1F0-0x8003B24C
print('template init code 0x8003B1F0..0x8003B250:')
for i in lib.dis(0x8003B1F0, 24):
    print('   %08x %-8s %s' % (i.address, i.mnemonic, i.op_str))
print('slot init loop 0x8003B24C..0x8003B2D8:')
for i in lib.dis(0x8003B24C, 38):
    print('   %08x %-8s %s' % (i.address, i.mnemonic, i.op_str))
