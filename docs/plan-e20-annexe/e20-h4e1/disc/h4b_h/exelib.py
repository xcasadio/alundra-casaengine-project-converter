"""Read-only access to ALUN_CD.EXE (PS-X EXE, French). Fact source for the native handler tables.
Usage: imported by the other scripts. Never writes outside the scratch folder."""
import struct

EXE_PATH = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"

with open(EXE_PATH, "rb") as _f:
    DATA = _f.read()

assert DATA[:8] == b"PS-X EXE", "not a PS-X EXE"
PC0, GP0, T_ADDR, T_SIZE = struct.unpack_from("<IIII", DATA, 0x10)


def off(addr):
    """File offset of a virtual address inside the text segment."""
    o = 0x800 + (addr - T_ADDR)
    if not (0x800 <= o < len(DATA)):
        raise ValueError(f"address {addr:#x} outside the EXE image")
    return o


def u32(addr):
    return struct.unpack_from("<I", DATA, off(addr))[0]


def u8(addr):
    return DATA[off(addr)]


def s16(addr):
    return struct.unpack_from("<h", DATA, off(addr))[0]


# master table: pointer array at 0x80098F4C = {A, B, C, D, E, F} (slot index 0..5)
MASTER = 0x80098F4C
SLOT_NAMES = "ABCDEF"


def slot_table_base(slot):
    return u32(MASTER + 4 * slot)


def handler(slot, code):
    base = slot_table_base(slot)
    if base == 0:
        return 0
    return u32(base + 4 * code)


if __name__ == "__main__":
    print(f"pc={PC0:#x} gp={GP0:#x} t_addr={T_ADDR:#x} t_size={T_SIZE:#x} file={len(DATA)}")
    for s in range(6):
        print(SLOT_NAMES[s], hex(slot_table_base(s)))
