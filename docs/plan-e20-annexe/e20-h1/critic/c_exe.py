"""Critic: own loader + capstone listings of ALUN_CD.EXE (France). Read-only on the binary.
Written from scratch by the critic (no reuse of the discovery's code)."""
import hashlib
import struct
import sys

EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
SHA1 = "a705f8dab665645af5dfaf8fb348c7926b5caa58"


def load():
    data = open(EXE, "rb").read()
    h = hashlib.sha1(data).hexdigest()
    if h != SHA1:
        raise SystemExit("bad SHA-1 " + h)
    assert data[:8] == b"PS-X EXE"
    pc0, gp0, tbase, tsize = struct.unpack_from("<IIII", data, 0x10)
    img = data[0x800:0x800 + tsize]
    return pc0, gp0, tbase, tsize, img


def u32(img, base, addr):
    o = addr - base
    return struct.unpack_from("<I", img, o)[0]


if __name__ == "__main__":
    pc0, gp0, tbase, tsize, img = load()
    print(hex(pc0), hex(gp0), hex(tbase), hex(tsize))
