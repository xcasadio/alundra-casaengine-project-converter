"""The REAL render entry 0x8002E130(OT, camX, camY): sorts the shared list (quicksort 0x8002DA04, descending on element+0x10) and emits each element.
Reports the sorted order (= emission order) and so the draw order (OT head = last emitted = drawn first), for ties included."""
import sys, os
H = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, H)
import fxm
def run(map_id, hero=(26, 30), cam=(836, 560), pre=None):
    f = fxm.FxMachine(map_id, hero_tile=hero); m = f.m
    f.map_load_spawn()
    if pre: pre(f)
    pushed = f.tick(0)
    OT = 0x80140000
    for i in range(0x400): m.ww(OT + 4 * i, 0)
    before = [(el - fxm.POOL - 0x0C) // 0x80 for el in pushed]
    keys = {(el - fxm.POOL - 0x0C) // 0x80: m.rw(el + 0x10) for el in pushed}
    m.call(0x8002E130, OT, cam[0] & 0xFFFFFFFF, cam[1] & 0xFFFFFFFF, limit=80_000_000)
    n = m.rws(0x8011CE60)
    after = [(m.rw(0x8011CB60 + 4 * k) - fxm.POOL - 0x0C) // 0x80 for k in range(n)]
    print("map", map_id, "pushed (slot order)", before, "keys", {k: hex(v) for k, v in keys.items()})
    print("   sorted list (emission order, descending key):", after, "-> draw order (ascending key, OT head first):", list(reversed(after)))
run(391)
run(163)
run(391, pre=lambda f: [f.m.ww(f.slot_addr(4) + 0x3C, f.m.rw(f.slot_addr(3) + 0x3C)) for _ in (0,)])
