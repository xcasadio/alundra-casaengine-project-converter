"""Common row format of the g1d key files (E20.g1d T0 (c)). READ-ONLY use of the oracle (copies of the h3 / g1a oracles, never edited).
One line per tick where the object is in the lists, columns separated by ' | ':
scenario | tick | hero: anim dir idsv y(px) z(px) flags afl | obj: anim idsv y(px) z(px) flags afl carrier | hero key | obj key | slot hero | slot obj | sign
sign = FRONT (obj key > hero key, drawn later), BEHIND (<), EQUAL (the binary does not order two equal keys)."""
import os
import sys

ORC = os.environ['G1D_ORC']  # directory holding the copies oracle_h3, oracle_h3bin, oracle_g1a
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')


def slot(key):
    return 16 * min((key & 0xFFFFFFFF) >> 20, 59) + 6


def fields(m, e):
    return dict(st=m.peeks(e + 0x10), fl=m.peek(e + 0x6C) & 0xFFFFFFFF, afl=m.peek(e + 0xB4) & 0xFF, car=m.peek(e + 0x28) & 0xFFFFFFFF,
                idsv=m.peeks(e + 0x1A8), y=m.peeks(e + 0x118) / 65536, z=m.peeks(e + 0x11C) / 65536, anim=m.peeks(e + 0x90),
                dr=m.peeks(e + 0x8C), key=m.peek(e + 0x1BC) & 0xFFFFFFFF)


def sign(h, o):
    return 'FRONT' if o['key'] > h['key'] else ('BEHIND' if o['key'] < h['key'] else 'EQUAL')


HEADER = ("scenario | tick | hero: anim dir idsv y(px) z(px) flags afl | obj: anim idsv y(px) z(px) flags afl carrier | "
          "hero key | obj key | slot hero | slot obj | sign")


def line(label, t, h, o):
    return "%s | %d | %d %d %d %.5f %.5f %08X %02X | %d %d %.5f %.5f %08X %02X %08X | %08X | %08X | %d | %d | %s" % (
        label, t, h['anim'], h['dr'], h['idsv'], h['y'], h['z'], h['fl'], h['afl'],
        o['anim'], o['idsv'], o['y'], o['z'], o['fl'], o['afl'], o['car'],
        h['key'], o['key'], slot(h['key']), slot(o['key']), sign(h, o))
