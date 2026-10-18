"""Critic: run the REAL 0x8004E0F8 (promotion) on the critic's CPU with the real item-properties table (alundra-project/Data/items-properties.json), a crafted inventory."""
import json, struct
from c_cpu import Cpu, MemErr

rows = json.load(open(r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\Data\items-properties.json", encoding="utf-8-sig"))
TABLE = 0x800B9FE8
STATS, INV = 0x801E1000, 0x801E2000


def run(equipped, owned):
    cpu = Cpu()
    cpu.provide(0x80176318, 4, STATS)
    cpu.provide(0x8017631C, 4, INV)
    cpu.provide(0x8017638C, 4, 0x63)
    cpu.provide(STATS + 0xC, 2, equipped)
    for i in range(128):
        r = rows[i] if i < len(rows) else [0x7FFF, 0, 0, 0, 0]
        for c, v in enumerate(r[:5]):
            cpu.provide(TABLE + 10 * i + 2 * c, 2, v & 0xFFFF)
        cpu.provide(INV + 4 * i, 2, 0)
        cpu.provide(INV + 4 * i + 2, 2, owned.get(i, 0))
    ret, _ = cpu.run(0x8004E0F8, [])
    eq = cpu.mem[STATS + 0xC] | (cpu.mem[STATS + 0xD] << 8)
    return ret & 0xFFFFFFFF, eq


for eqp, own in [(0x24, {0x24: 3}), (0x26, {0x26: 1}), (0x2A, {0x2A: 1}), (0x2A, {0x2A: 1, 0x26: 1}), (0x26, {0x26: 1, 0x2A: 1}), (0x24, {}), (0x2B, {0x2B: 1, 0x2C: 1}), (0x2C, {0x2B: 1, 0x2C: 1})]:
    r, e = run(eqp, own)
    print("equipped %02X owned %s -> returns %s, equipped after %02X" % (eqp, {hex(k): v for k, v in own.items()}, hex(r) if r < 0xFFFF0000 else "-1", e))
