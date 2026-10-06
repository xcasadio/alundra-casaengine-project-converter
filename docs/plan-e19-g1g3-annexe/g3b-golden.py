"""E19.g G3b-0: generates g3b-golden.tsv, the values the effect-opcode tests of G3b are written against.

The values are computed by running the REAL operands of the real event programs (the bytes of the export, at the pcs of the nine effect
opcodes) through fxpool.py, the model of the reservoir of ALUN_CD.EXE checked against the real code (docs/plan-e19-g1g3-annexe/README.md),
over the effect tables of the G1 export. They are cross-checked against scenarios_out.txt (the real code run in the MIPS interpreter:
S1 391, S2 476, S4 135, S5 pool) and against load_oracle.json (the slots at map entry). Nothing is typed by hand. Usage:

    python g3b-golden.py            writes g3b-golden.tsv next to this script
    python g3b-golden.py --check    fails when the committed table differs from what the programs and the model give

The sites come from the first generation, the closed list of the chain audit (Alundra.Tests/Data/story-chain-skipped-opcodes.tsv, the EFFET rows,
99 of them) read at the commit it was generated from; later the OP rows of the table itself are the list (the closed list loses those rows
when the opcodes are ported). Either way the opcode and the operand bytes of each row are re-read from the export.

Rows (first column):
  OP     map slot program pc opcode operand-bytes (the bytes after the opcode, decimal, comma separated) - every effect site of the chain.
  S476   the aura of map 476: create (x y z raw 16.16 and the target animation of record 0), pos93 (the 0x93 of pc 334), anims (the operand
         of each 0x92 of program 2 in pc order), last_drawn / freed (ticks after 0x92 [0,0]), revive n status (0x92 [0,1] after n updates,
         status of the slot 60 ticks later).
  S391a  (slot record x y z): the reservoir of 10 slots after the five 0xA2, before the first 0x94 (first named instant), raw 16.16.
  S391f  (record fx fy fz): the force each record holds once the five 0x94 are applied.
  S391b  (slot record dx dy dz): the move of each slot over N non-frozen effect ticks counted from just after the fifth 0x94 (second named
         instant); S391n gives N. No absolute position is pinned at that instant.
  S391c  (record count): the slots per record at the second instant; S391x: the four-0x94 cross-check against the oracle S1.
  S162   (slot record x y z): the 7 load slots then the 11 slots of the 0xA2 of B[9]; S162n (record count).
  S135   (step x y z): the altar slot before and after the two 0xA0 of B[5]; S135a3: the 0xA3 of C[9] with an entity, and without one.
  S10    the map-10 slots: entry (the load slot), 0x90 @2828 + 0x92 @2864: (slot record anim x y z), last_drawn / freed of the rolling rock.
  S5     the pool: after_load, filled, one_more, after_free, next_slot.
  S91    0x91 [3] after the 0xA2 of the 391: the slots freed in one instruction, the active count.
  S90    0x90 [0] and 0x90 [9] (outside the table) on map 391.
  SA1    0xA1 (synthetic operands over the 391 pool): the slots of the record before and after, and with no entity found.
"""
import importlib.util
import json
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
PROJECT = os.path.join(ROOT, "alundra-project")
CLOSED_LIST = "Alundra.Tests/Data/story-chain-skipped-opcodes.tsv"

spec = importlib.util.spec_from_file_location("fxpool", os.path.join(HERE, "fxpool.py"))
fx = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fx)

SIZES = {0x90: 2, 0x91: 2, 0x92: 3, 0x93: 8, 0x94: 8, 0xA0: 8, 0xA1: 9, 0xA2: 8, 0xA3: 9}
EFFECT_OPCODES = set(SIZES)


def folder_of(map_id):
    index = json.load(open(os.path.join(PROJECT, "Maps", "world-index.json"), encoding="utf-8"))
    rel = index[str(map_id)].replace("\\", "/")
    return os.path.join(PROJECT, os.path.dirname(rel.replace("/", os.sep)))


def load_map(map_id):
    folder = folder_of(map_id)
    name = os.path.basename(folder)
    events = json.load(open(os.path.join(folder, "events", name + ".events.json"), encoding="utf-8"))
    effects = json.load(open(os.path.join(folder, "effects", name + ".effects.json"), encoding="utf-8"))
    return events, effects


def bank_of(document):
    effects = []
    for e in document["Effects"]:
        anims = []
        for a in e["Animations"]:
            frames = []
            for delay, set_index in a["Frames"]:
                assert set_index >= 0, "an empty frame in the export is outside this model"
                frames.append((delay, set_index))
            anims.append(fx.Anim(frames, 0 if a["End"] == "Destroy" else 1))
        sets = {i: (s["Idsv"], len(s["Images"])) for i, s in enumerate(e["ImageSets"])}
        effects.append(fx.Effect(anims, sets))
    return fx.Bank(effects)


def records_of(document):
    return [fx.Record(r["X1"], r["Y1"], r["X2"], r["Y2"], r["Flags"], r["Effect"], r["X"], r["Y"], r["Z"], r["Anim"]) for r in document["Records"]]


GLOBAL = json.load(open(os.path.join(PROJECT, "Data", "effects", "effects-global.json"), encoding="utf-8"))
GLOBAL_BANK = bank_of(GLOBAL)


def pool_of(map_id):
    events, effects = load_map(map_id)
    pool = fx.Pool(bank_of(effects), GLOBAL_BANK, records_of(effects), hero_tile=(0, 0))
    pool.map_load()
    return pool, events["Codes"]


def code_at(codes, pc, opcode=None):
    op = codes[pc]
    assert opcode is None or op == opcode, (pc, op, opcode)
    return codes[pc:pc + SIZES[op]]


def active(pool):
    return [s for s in pool.slots if s.status != 0]


def px(raw):
    return raw >> 16


def tick_n(pool, n):
    for _ in range(n):
        pool.tick(0)


def sites_from_closed_list():
    text = subprocess.run(["git", "-C", ROOT, "show", "d28210c:" + CLOSED_LIST], capture_output=True, check=True).stdout.decode("utf-8")
    rows = []
    for line in text.replace("\r\n", "\n").split("\n")[1:]:
        c = line.split("\t")
        if len(c) > 9 and c[7] == "EFFET":
            rows.append((int(c[0]), c[1], int(c[2]), int(c[4]), int(c[5], 16)))
    return rows


def sites_from_table(path):
    rows = []
    for line in open(path, encoding="utf-8").read().replace("\r\n", "\n").split("\n"):
        c = line.split("\t")
        if c[0] == "OP":
            rows.append((int(c[1]), c[2], int(c[3]), int(c[4]), int(c[5], 16)))
    return rows


def main():
    path = os.path.join(HERE, "g3b-golden.tsv")
    sites = sites_from_table(path) if os.path.exists(path) else sites_from_closed_list()
    assert len(sites) == 99, len(sites)
    scen = open(os.path.join(HERE, "scenarios_out.txt"), encoding="utf-8").read()
    load = json.load(open(os.path.join(HERE, "load_oracle.json")))
    out = []
    out.append("# E19.g G3b golden table, generated by g3b-golden.py from the real event programs of the export, fxpool.py (the model of the binary) and the G1 export;")
    out.append("# cross-checked against scenarios_out.txt (the real code of ALUN_CD.EXE) and load_oracle.json. Raw values are 16.16.")

    # --- OP: every effect site, operands re-read from the export ---
    maps = {}
    for map_id in sorted({s[0] for s in sites}):
        maps[map_id] = load_map(map_id)[0]["Codes"]
    for map_id, slot, program, pc, opcode in sorted(sites):
        code = code_at(maps[map_id], pc, opcode)
        out.append("\t".join(["OP", str(map_id), slot, str(program), str(pc), "0x%02X" % opcode, ",".join(str(b) for b in code[1:])]))

    # --- S476: the aura ---
    pool, codes = pool_of(476)
    assert len(active(pool)) == 0
    pool.op(code_at(codes, 326, 0xA2))
    (s,) = active(pool)
    out.append("\t".join(["S476", "create", str(s.recidx), str(s.x), str(s.y), str(s.z), str(s.ta)]))
    assert (s.x, s.y, s.z, s.ta) == (62914560, 10485760, 3145729, 1), (s.x, s.y, s.z, s.ta)   # scenarios_out S2
    assert "x=62914560 y=10485760 z=3145729" in scen
    pool.op(code_at(codes, 334, 0x93))
    out.append("\t".join(["S476", "pos93", str(s.x), str(s.y), str(s.z)]))
    assert (s.x, s.y, s.z) == (62914560, 10485760, 3145729)
    anims = [code_at(codes, pc, 0x92) for (m, sl, pr, pc, op) in sorted(sites) if m == 476 and pr == 2 and op == 0x92]
    assert all(a[1] == 0 for a in anims) and len(anims) == 12
    out.append("\t".join(["S476", "anims", ",".join(str(a[2]) for a in anims)]))
    pool.op(code_at(codes, 342, 0x92))
    assert s.ta == 0
    drawn = []
    for t in range(300):
        listed = pool.tick(0)
        drawn.append(s in listed)
        if not drawn[-1]:
            break
    last_drawn = max(i for i, d in enumerate(drawn) if d)
    freed = drawn.index(False)
    out.append("\t".join(["S476", "last_drawn", str(last_drawn)]))
    out.append("\t".join(["S476", "freed", str(freed)]))
    mm = re.search(r"first tick with dead flag: (\d+) ; last tick drawn \(status != 0\): (\d+)\n\s*slot freed at tick: (\d+)", scen)
    assert (last_drawn, freed) == (int(mm.group(2)), int(mm.group(3))) == (222, 223)
    for n in range(219, 225):
        p2, c2 = pool_of(476)
        p2.op(code_at(c2, 326, 0xA2))
        p2.op(code_at(c2, 342, 0x92))
        tick_n(p2, n)
        p2.op([0x92, 0, 1])
        tick_n(p2, 60)
        s2 = p2.slots[0]
        out.append("\t".join(["S476", "revive", str(n), str(s2.status)]))
    for n, st in re.findall(r"applied after (\d+) ticks -> slot status after 60 more ticks: (\d)", scen):
        assert ("S476\trevive\t%s\t%s" % (n, st)) in out, (n, st)

    # --- S391: the two named instants ---
    pool, codes = pool_of(391)
    base = [(s.idx, s.recidx, s.x, s.y, s.z) for s in active(pool)]
    assert [b[0] for b in base] == [0, 1, 2, 3, 4]
    loaded = {int(k): v for k, v in load.items()}[391]
    assert [(b[1], b[2] >> 16, b[3] >> 16) for b in base] == [(r[0], r[1], r[2]) for r in loaded], "load_oracle"
    for pc in (228, 236, 244, 252, 408):
        pool.op(code_at(codes, pc, 0xA2))
    tick_n(pool, 3)   # the slots are animated, never moved: every force is still 0
    for s in active(pool):
        out.append("\t".join(["S391a", str(s.idx), str(s.recidx), str(s.x), str(s.y), str(s.z)]))
    assert [s.recidx for s in active(pool)] == [0, 1, 2, 3, 4, 3, 4, 0, 1, 2]
    # the four-0x94 cross-check against the oracle S1 (slots 0 to 3, 100 ticks)
    p4, c4 = pool_of(391)
    for pc in (417, 425, 433, 441):
        p4.op(code_at(c4, pc, 0x94))
    before = {s.idx: s.y for s in active(p4)}
    tick_n(p4, 100)
    deltas = {s.idx: s.y - before[s.idx] for s in active(p4)}
    m1 = re.search(r"y delta \(16\.16\) per slot: (\{.*?\})", scen).group(1)
    expected = {int(k): int(v) for k, v in re.findall(r"(\d+): (\d+)", m1)}
    assert {k: deltas[k] for k in expected} == expected, (deltas, expected)
    out.append("\t".join(["S391x", "S1", ",".join("%d:%d" % kv for kv in sorted(expected.items()))]))
    for pc in (417, 425, 433, 441, 503):
        pool.op(code_at(codes, pc, 0x94))
    forces = {}
    for s in active(pool):
        forces.setdefault(s.recidx, set()).add(s.forces)
    assert all(len(v) == 1 for v in forces.values()), forces
    for rec in sorted(forces):
        f = next(iter(forces[rec]))
        out.append("\t".join(["S391f", str(rec), str(f[0]), str(f[1]), str(f[2])]))
    n = 100
    before = {s.idx: (s.x, s.y, s.z) for s in active(pool)}
    tick_n(pool, n)
    out.append("\t".join(["S391n", str(n)]))
    for s in active(pool):
        b = before[s.idx]
        out.append("\t".join(["S391b", str(s.idx), str(s.recidx), str(s.x - b[0]), str(s.y - b[1]), str(s.z - b[2])]))
    counts = {}
    for s in active(pool):
        counts[s.recidx] = counts.get(s.recidx, 0) + 1
    for rec in sorted(counts):
        out.append("\t".join(["S391c", str(rec), str(counts[rec])]))
    # record 0 and its two slots: 50 << 8 = 12800 per tick, 1 280 000 at N = 100 (the plan's own example), 2 560 000 where the force is 100
    assert [(s.idx, s.y - before[s.idx][1]) for s in active(pool) if s.recidx == 0] == [(0, 1280000), (7, 1280000)]
    assert [s.y - before[s.idx][1] for s in active(pool) if s.recidx == 2] == [2560000, 2560000]

    # --- S162 ---
    pool, codes = pool_of(162)
    assert [(s.recidx, px(s.x) , px(s.y), s.z >> 16) for s in active(pool)] == [(r[0], r[1], r[2], r[3]) for r in load["162"]]
    for (m, sl, pr, pc, op) in sorted(sites):
        if m == 162:
            assert op == 0xA2 and pr == 9
            pool.op(code_at(codes, pc, 0xA2))
    assert len(active(pool)) == 18
    for s in active(pool):
        out.append("\t".join(["S162", str(s.idx), str(s.recidx), str(s.x), str(s.y), str(s.z)]))
    counts = {}
    for s in active(pool):
        counts[s.recidx] = counts.get(s.recidx, 0) + 1
    assert counts[5] == 12
    for rec in sorted(counts):
        out.append("\t".join(["S162n", str(rec), str(counts[rec])]))

    # --- S135 ---
    pool, codes = pool_of(135)
    (alt,) = active(pool)
    assert (alt.x, alt.y) == (30670848, 24641536) and alt.recidx == 0
    out.append("\t".join(["S135", "load", str(alt.x), str(alt.y), str(alt.z)]))
    pool.op(code_at(codes, 536, 0xA0))
    out.append("\t".join(["S135", "a0_1", str(alt.x), str(alt.y), str(alt.z)]))
    pool.op(code_at(codes, 549, 0xA0))
    out.append("\t".join(["S135", "a0_2", str(alt.x), str(alt.y), str(alt.z)]))
    assert px(alt.x) == 468 - 72 and px(alt.y) == 376 + 8, (px(alt.x), px(alt.y))   # scenarios_out S4
    # the 0xA3 of C[9] @2490 (record 1, global effect 13, search key 0x80 = the entity itself)
    code = code_at(codes, 2490, 0xA3)
    ent = fx.Entity(x=(300 << 16) + 12345, y=(200 << 16) - 7, z=(16 << 16) + 3)
    pool.entities[code[2]] = ent
    n0 = len(active(pool))
    pool.op(code)
    created = [s for s in active(pool) if s.recidx == code[1]]
    assert len(created) == 1 and len(active(pool)) == n0 + 1
    c = created[0]
    out.append("\t".join(["S135a3", "found", str(code[1]), str(code[2]), str(ent.x), str(ent.y), str(ent.z), str(c.idx), str(c.x), str(c.y), str(c.z), str(c.tb), str(c.ts), str(c.ta)]))
    pool2, codes2 = pool_of(135)
    n0 = len(active(pool2))
    pool2.op(code)
    assert len(active(pool2)) == n0
    out.append("\t".join(["S135a3", "none", str(n0), str(len(active(pool2)))]))

    # --- S10 ---
    pool, codes = pool_of(10)
    (entry,) = active(pool)
    out.append("\t".join(["S10", "entry", str(entry.idx), str(entry.recidx), str(entry.x), str(entry.y), str(entry.z)]))
    c90 = code_at(codes, 2828, 0x90)
    c92 = code_at(codes, 2864, 0x92)
    assert c90[1] == 0 and c92[1:] == [0, 1]
    pool.op(c90)
    rock = [s for s in active(pool) if s.recidx == 0][0]
    out.append("\t".join(["S10", "rock", str(rock.idx), str(rock.recidx), str(rock.ta), str(rock.x), str(rock.y), str(rock.z)]))
    pool.op(c92)
    out.append("\t".join(["S10", "rock92", str(rock.ta)]))
    drawn = []
    for t in range(400):
        listed = pool.tick(0)
        drawn.append(rock in listed)
        if not drawn[-1]:
            break
    out.append("\t".join(["S10", "last_drawn", str(max(i for i, d in enumerate(drawn) if d))]))
    out.append("\t".join(["S10", "freed", str(drawn.index(False))]))
    assert len([s for s in active(pool)]) == 1   # only the load slot is left: the rock is gone, the entry slot stays

    # --- S5: the pool is full at 128 ---
    pool, codes = pool_of(391)
    assert len(active(pool)) == 5
    out.append("\t".join(["S5", "after_load", "5"]))
    for _ in range(123):
        pool.op([0x90, 3])
    assert len(active(pool)) == 128
    out.append("\t".join(["S5", "filled", "128"]))
    pool.op([0x90, 3])
    assert len(active(pool)) == 128
    out.append("\t".join(["S5", "one_more", "128"]))
    pool.op([0x91, 4])
    assert len(active(pool)) == 127
    out.append("\t".join(["S5", "after_free", "127"]))
    pool.op([0x90, 3])
    nxt = [s for s in pool.slots if s.status != 0 and s.idx == 4]
    assert len(nxt) == 1 and nxt[0].recidx == 3
    out.append("\t".join(["S5", "next_slot", "4", "3"]))
    assert "after load (5) + 123 x 0x90 [3] active = 128 (full)" in scen

    # --- S91 ---
    pool, codes = pool_of(391)
    for pc in (228, 236, 244, 252, 408):
        pool.op(code_at(codes, pc, 0xA2))
    rec3 = [s.idx for s in active(pool) if s.recidx == 3]
    pool.op([0x91, 3])
    out.append("\t".join(["S91", "freed", ",".join(str(i) for i in rec3), str(len(active(pool)))]))
    assert rec3 == [3, 5]
    pool.op([0x91, 7])   # an index outside the table: nothing happens, no error
    assert len(active(pool)) == 8

    # --- S90 ---
    pool, codes = pool_of(391)
    pool.op([0x90, 0])
    new = [s for s in active(pool) if s.idx == 5][0]
    out.append("\t".join(["S90", "record0", str(new.idx), str(new.recidx), str(new.x), str(new.y), str(new.z), str(new.ta), str(new.mode)]))
    pool.op([0x90, 9])
    out.append("\t".join(["S90", "outside", str(len(active(pool)))]))
    assert len(active(pool)) == 6

    # --- SA1: 0xA1 on the 391 pool (synthetic operands: record 0, search key 0x81, d = (-3, 2, 1) px) ---
    pool, codes = pool_of(391)
    pool.op(code_at(codes, 228, 0xA2))
    pool.op(code_at(codes, 244, 0xA2))   # record 3 then record 0
    rec0 = [s for s in active(pool) if s.recidx == 0]
    assert len(rec0) == 2
    ent = fx.Entity(x=(100 << 16) + 77, y=(60 << 16) + 5, z=(8 << 16) + 1)
    op = [0xA1, 0, 0x81, 0xFD, 0xFF, 2, 0, 1, 0]
    pool.op(op)   # no entity registered for the key: nothing moves
    before = [(s.x, s.y, s.z) for s in rec0]
    out.append("\t".join(["SA1", "none", ",".join(str(v) for t in before for v in t)]))
    pool.entities[0x81] = ent
    pool.op(op)
    out.append("\t".join(["SA1", "entity", str(ent.x), str(ent.y), str(ent.z), ",".join(str(v) for s in rec0 for v in (s.idx, s.x, s.y, s.z))]))
    others = [s for s in active(pool) if s.recidx != 0]
    assert all((s.x, s.y, s.z) != (rec0[0].x, rec0[0].y, rec0[0].z) for s in others)

    text = "\n".join(out) + "\n"
    if "--check" in sys.argv:
        cur = open(path, encoding="utf-8", newline="").read().replace("\r\n", "\n")
        if cur != text:
            print("g3b-golden.tsv differs from what the programs and the model give")
            sys.exit(1)
        print("g3b-golden.tsv matches (%d lines)" % len(out))
        return
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    print("wrote %s (%d lines)" % (path, len(out)))


main()
