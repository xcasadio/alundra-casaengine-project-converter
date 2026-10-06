"""g3d_golden.py: generates the golden table of E19.g G3d (the life vessel, opcode 0x82 sub-case 0x53) from the REAL code of ALUN_CD.EXE (France)
run in the MIPS interpreter (lib/emu.py via lib/fxm.py): the whole chain 0x8003FE7C -> HandleMapTriggerCommand 0x80034108 ->
IncreaseHpMaxAndCreateEffect 0x800333AC -> CreateEffectEntity 0x8003BDD8 (and, for the hero life mirror, SetPlayerHpMax 0x8004DC68 /
SetPlayerHp 0x8004DD30, the tail of MovePlayer 0x80032920-0x80032930).
Output: g3d-golden.tsv next to this file (stdout with --check compares instead of writing).

Row kinds (tab separated):
  V  <case> <seed_in> <hero x y z (16.16 raw, signed)> <prefill> <free_slots> <hp_in> <hpmax_in> | <result> <size> <sounds> <hp_out> <hpmax_out> <seed_out> <n_created> <n_sparks>
  S  <case> <slot> <x> <y> <z> <fx> <fy> <fz>        state of every active vessel slot right after the call, before any tick (raw 16.16)
  T  <case> <updates> <slot> <x> <y> <z>               the same slots after N effect updates (N = 1, 2, 16, 27, 28, 29; N updates = ticks 0..N-1) ; 'free' when the slot is inactive
  F  clean <last_drawn_tick> <free_tick>               lifetime of the 12 vessel slots (all alike), 0-based update index: drawn on ticks 0..S (S = 27), freed on tick S + 1 = 28
  P  <hp_in> <hpmax_in> <stats_hp_out> <stats_hpmax_out> the hero life mirror: real SetPlayerHpMax(entity.HpMax); SetPlayerHp(entity.Hp) after the vessel,
                                                      with the stats object (0x80176318) starting equal to the entity
"""
import sys, os, struct
H = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, H)
import fxm

HERO = fxm.HERO
M32 = 0xFFFFFFFF
PRNG = 0x80098708
PROG, PC_WORD, CODE, STATE = 0x801F0000, 0x801F0100, 0x801F0200, 0x801F0400
STATS_PTR = 0x80176318
STATS = 0x80190100


def s32(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


def new_machine(hero, seed, prefill, free_slots, hp, hpmax):
    f = fxm.FxMachine(476)
    m = f.m
    sounds = []
    m.hooks[0x800490FC] = lambda mm: sounds.append(mm.r[4])
    for k, v in zip((0x114, 0x118, 0x11C), hero):
        m.ww(HERO + k, v & M32)
    m.ww(HERO + 0x14, hp)
    m.ww(HERO + 0x18, hpmax)
    m.ww(PRNG, seed)
    for _ in range(prefill):
        f.create_free(0, 14, 0, 0, 0, 0)
    for s in free_slots:
        m.ww(f.slot_addr(s) + 0x68, 0)
    return f, m, sounds


def call_82(m, sub):
    code = bytes([0x82, sub, 0xFF])
    for i, c in enumerate(code):
        m.wb(CODE + i, c)
    m.ww(PC_WORD, CODE)
    m.ww(STATE + 0x2C, 7)
    size = m.call(0x8003FE7C, PROG, 0, PC_WORD, STATE) & M32
    return size, m.rw(STATE + 0x2C)


def slot_row(f, i):
    s = f.slot(i)
    return s


# the cases whose slots are also followed tick by tick: empty reservoir on two seeds, the negative ring, the wrap of z, the near-full reservoir, the holes
TICKED = {"c00", "c05", "c07", "c18", "c29", "c30"}


def scenarios():
    base = (996 << 16, 504 << 16, 0)
    small = (5 << 16, 3 << 16, 0)                 # the ring reaches negative x and y (wraps in 32 bits are not needed, but the sign is)
    z_up = (100 << 16, 80 << 16, 3 << 16)
    z_wrap = (100 << 16, 80 << 16, 0x7FF80000)    # z + 0x100000 wraps to a negative 32-bit value
    seeds = [0xB017C93D, 0x35E36190, 0x00000000, 0xFFFFFFFF]
    out = []
    n = 0
    for seed in seeds:                            # 4 seeds x 4 positions on an empty reservoir
        for hero in (base, small, z_up, z_wrap):
            out.append(dict(case="c%02d" % n, hero=hero, seed=seed, prefill=0, free=[], hp=5, hpmax=7)); n += 1
    for prefill in (1, 5, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128):
        out.append(dict(case="c%02d" % n, hero=z_up, seed=0xB017C93D, prefill=prefill, free=[], hp=10, hpmax=10)); n += 1
    out.append(dict(case="c%02d" % n, hero=z_up, seed=0xB017C93D, prefill=10, free=[3, 7], hp=10, hpmax=10)); n += 1
    out.append(dict(case="c%02d" % n, hero=z_up, seed=0xB017C93D, prefill=124, free=[10, 50, 99], hp=1, hpmax=12)); n += 1
    return out


def main(check):
    rows = []
    for sc in scenarios():
        f, m, sounds = new_machine(sc["hero"], sc["seed"], sc["prefill"], sc["free"], sc["hp"], sc["hpmax"])
        pre_active = set(f.active())
        size, result = call_82(m, 0x53)
        new = [i for i in f.active() if i not in pre_active]
        # a vessel creates at most 12 slots: 8 ring then up to 4 sparks; a spark is a slot with a non-zero z force
        n_sparks = sum(1 for i in new if f.slot(i)["forces"][2] != 0)
        rows.append("\t".join(["V", sc["case"], "0x%08X" % sc["seed"], *["%d" % s32(v) for v in sc["hero"]], str(sc["prefill"]), ",".join(map(str, sc["free"])) or "-",
                               str(sc["hp"]), str(sc["hpmax"]), "|", str(result), str(size), ",".join(map(str, sounds)) or "-",
                               str(s32(m.rw(HERO + 0x14))), str(s32(m.rw(HERO + 0x18))), "0x%08X" % m.rw(PRNG), str(len(new)), str(n_sparks)]))
        for i in new:
            s = f.slot(i)
            rows.append("\t".join(["S", sc["case"], str(i), *[str(v) for v in (s["x"], s["y"], s["z"], *s["forces"])]]))
            assert s["recidx"] == -1 and s["mode"] == 0 and s["tb"] == 0 and s["ts"] == 14 and s["ta"] == 0, ("unexpected slot fields", s)
        # ticks (the vessel slots only; the prefilled ones are other effects that also age, ignored here)
        for t in range(1, 30):
            f.tick()
            if sc["case"] in TICKED and t in (1, 2, 16, 27, 28, 29):
                act = set(f.active())
                for i in new:
                    if i in act:
                        s = f.slot(i)
                        rows.append("\t".join(["T", sc["case"], str(t), str(i), str(s["x"]), str(s["y"]), str(s["z"])]))
                    else:
                        rows.append("\t".join(["T", sc["case"], str(t), str(i), "free", "free", "free"]))
    # lifetime: all 12 slots alike, measured on a clean run
    f, m, sounds = new_machine((100 << 16, 80 << 16, 0), 0xB017C93D, 0, [], 5, 7)
    call_82(m, 0x53)
    new = f.active()
    last = None
    freed = None
    pushed_ticks = []
    for t in range(0, 40):
        ptrs = f.tick()
        act = set(f.active())
        if all(i not in act for i in new) and freed is None:
            freed = t
        if any(i in act for i in new):
            last = t
    rows.append("\t".join(["F", "clean", str(last), str(freed)]))
    # the hero life mirror: real chain vessel then the tail of MovePlayer
    for hp, hpmax in [(10, 10), (3, 10), (0, 10), (1, 1), (0, 0), (5, 7), (48, 49), (49, 49), (50, 50), (45, 50), (7, 49)]:
        f, m, sounds = new_machine((100 << 16, 80 << 16, 0), 0xB017C93D, 0, [], hp, hpmax)
        m.ww(STATS_PTR, STATS)
        m.wh(STATS + 0, hp)
        m.wh(STATS + 2, hpmax)
        call_82(m, 0x53)
        m.call(0x8004DC68, m.rw(HERO + 0x18))           # SetPlayerHpMax(entity.HpMax)
        m.call(0x8004DD30, m.rw(HERO + 0x14))           # SetPlayerHp(entity.Hp)
        rows.append("\t".join(["P", str(hp), str(hpmax), str(m.rhs(STATS + 0)), str(m.rhs(STATS + 2)), "entity", str(s32(m.rw(HERO + 0x14))), str(s32(m.rw(HERO + 0x18)))]))
    text = "\n".join(rows) + "\n"
    path = os.path.join(H, "g3d-golden.tsv")
    if check:
        print("MATCH" if open(path, encoding="utf-8").read() == text else "DIFFERENT")
    else:
        open(path, "w", encoding="utf-8", newline="\n").write(text)
        print("wrote", path, len(rows), "rows")


if __name__ == "__main__":
    main("--check" in sys.argv)
