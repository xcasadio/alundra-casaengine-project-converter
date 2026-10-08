#!/usr/bin/env python
"""recompute_z.py - independent re-derivation (checker of the E19.h2 D1 annex) of the decision-carrying values of the annex.

NOT used here (on purpose): zsim.py, mips.py, emu.py, vz.py, zmodel.py, zmodel_dll.py, rules_vs_emu.py, m.py, scenarios_h2.py, giles_ledge_h2.py.
Used: the python standard library, `capstone` (only to decode instruction words of ALUN_CD.EXE (France) and to assert that the model below is the
one written in the disassembly - the "anchors"), the exported map 10 (json read by this file) and, READ-ONLY, the annex outputs (to compare).

Source of the model: the disassembly read with docs/plan-e19-backlog-1006-annexe/mipsdis.py :
  forces of a non-hero entity ........ 0x80036A84-0x80036BBC   (called by 0x80036828 for every entity but the hero 0x80127D30)
  landing test (terrain, strict) ..... 0x80036BFC               (0x80036C14-0x80036C34)
  ComputeZ ........................... 0x800375E0               (F <= 0: 0x8003768C.., F > 0: 0x80037604..)
  terrain probe ...................... 0x800370C4               (max of the 4 corners of the box, flat cell = byte3 * 16 px)
  FloorHeight / IsOnGround ........... 0x80037F28 / 0x80038064  (0x800380F8-0x8003810C)
  start of the pass P ................ 0x80038364               (0x800383B0-0x800383D0)
  order of the tick .................. 0x8003B388               (scripts 0x800386D0, lists 0x800384F4, UpdateAnimation 0x80038E18, P 0x80038364)
  handlers ........................... 0x25 -> 0x8003DB7C, 0x1A -> 0x8003D7B4, 0x1B -> 0x8003D7D0, 0x16 -> 0x8003D774, 0x37 -> 0x8003E3DC
Convention: PosZ_bin is what the binary stores, PosZ_dll = PosZ_bin - 1 (ADR-0026). Units 16.16. Update k = pass P of the tick s+k-1 (s = tick of the
`1A` / `1B`); a condition that exists after update k is read by a `0x25` at the tick s+k.

Limits (what this file does NOT recompute): the XY step (ComputeXY 0x80037730, its blocking by walls, the 3 px magnet 0x80037848: the XY positions of Giles
are INPUTS taken from the plan), the ceiling (0x80036D94), the support by other entities and the carriers (+0x28), UpdateAnimation, the script runner (RunScript),
the DLL side (the DLL rule is only the `<=` comparison, expressed below; the 4 px magnet (c) and the IsGrounded source (d) of the DLL are not modelled).
Run: python -B recompute_z.py [outfile]    (read-only on the repository; writes only the optional outfile)
"""
import glob
import json
import os
import re
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ANNEX = os.path.normpath(os.path.join(HERE, ".."))
ROOT = os.path.normpath(os.path.join(ANNEX, "..", ".."))
EXE = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"

OUT = []


def p(*a):
    OUT.append(" ".join(str(x) for x in a))


# ------------------------------------------------------------------------------------------------------------------------------------------------
# 0. anchors: the instruction words of the EXE that the model below relies on
# ------------------------------------------------------------------------------------------------------------------------------------------------
ANCHORS = [
    # forces of a non-hero entity
    (0x80036A84, "lw $v0, 0x28($s0)", "carried entity (+0x28): all forces zero (not modelled: no carrier)"),
    (0x80036AB8, "lw $v0, 0xf8($s0)", "impulse word +0xF8"),
    (0x80036AC0, "beqz $v0, 0x80036b08", "no impulse -> gravity branch"),
    (0x80036AC8, "lhu $v0, 0xf8($s0)", "marker compare (0x8000): not met by IZF 768..1360"),
    (0x80036AFC, "sll $v0, $v0, 8", "ForceZ = IZF << 8"),
    (0x80036B04, "sw $v0, 0xb8($s0)", "ForceZ store (impulse branch)"),
    (0x80036B10, "andi $v0, $v0, 0x100", "Gravity bit of the flags +0x6C"),
    (0x80036B14, "beqz $v0, 0x80036b64", "no gravity: ForceZ unchanged"),
    (0x80036B24, "lhu $v0, 4($a0)", "map word +4 = G"),
    (0x80036B2C, "sll $v0, $v0, 8", "G << 8"),
    (0x80036B30, "subu $v1, $v1, $v0", "ForceZ - (G << 8)"),
    (0x80036B34, "lhu $v0, 6($a0)", "map word +6 = V"),
    (0x80036B44, "sll $a1, $v0, 8", "V << 8"),
    (0x80036B48, "slt $v0, $a1, $a0", "(V<<8) < |v|"),
    (0x80036B54, "bgtz $v1, 0x80036b60", "clamp: delay slot move $v1,$a1 runs in both cases"),
    (0x80036B58, "move $v1, $a1", "clamp positive"),
    (0x80036B5C, "negu $v1, $a1", "clamp negative"),
    (0x80036B60, "sw $v1, 0xb8($s0)", "ForceZ store (gravity branch)"),
    (0x80036BB8, "sw $a0, 0xec($s0)", "FinalForceZ(+0xEC) = ForceZ(+0xB8)"),
    # start of the pass P
    (0x800383B0, "sw $zero, 0x168($a2)", "start of P: +0x168 cleared"),
    (0x800383B4, "sw $zero, 0x140($a2)", "start of P: contact +0x140 cleared"),
    (0x800383B8, "sw $zero, 0x13c($a2)", "start of P: +0x13C cleared"),
    (0x800383D0, "addu $v1, $v1, $a1", "ModdedPosZ = PosZ(+0x11C) + OffsetZ(+0x1EC)"),
    (0x800383E0, "sw $v1, 0x1e0($a2)", "ModdedPosZ store (+0x1E0)"),
    # ComputeZ
    (0x800375F4, "lw $s1, 0xec($s0)", "F = FinalForceZ"),
    (0x800375FC, "blez $s1, 0x8003768c", "F <= 0 -> landing-test branch"),
    (0x80037694, "sw $v0, 0x138($s0)", "terrain probe BEFORE the XY step stored in +0x138"),
    (0x800376A0, "jal 0x80036bfc", "landing test"),
    (0x800376A8, "beqz $v0, 0x80037708", "no landing -> PosZ += F"),
    (0x800376E0, "sw $v0, 0x140($s0)", "contact = 1"),
    (0x800376F0, "subu $v0, $v0, $a0", "PosZ = top - OffsetZ"),
    (0x800376F4, "andi $v1, $v1, 0x100", "Gravity bit"),
    (0x800376FC, "sw $v0, 0x11c($s0)", "PosZ store (landing; delay slot of the beqz at 0x800376F8)"),
    (0x80037704, "sw $zero, 0xb8($s0)", "ForceZ = 0 at a landing, only with Gravity"),
    (0x80037710, "addu $v0, $v0, $s1", "PosZ += F"),
    # landing test
    (0x80036C14, "lw $a3, 0x1e0($a0)", "ModdedPosZ"),
    (0x80036C18, "lw $v0, 0xec($a0)", "FinalForceZ"),
    (0x80036C1C, "lw $v1, 0x138($a0)", "T"),
    (0x80036C20, "addu $t1, $a3, $v0", "t1 = ModdedPosZ + F"),
    (0x80036C24, "slt $v0, $v1, $t1", "T < t1 ?"),
    (0x80036C28, "bnez $v0, 0x80036c38", "yes -> NO landing (strict)"),
    (0x80036C30, "addiu $t1, $v1, 1", "landing: top = T + 1"),
    (0x80036C34, "addiu $t6, $zero, 1", "landing: result 1"),
    # FloorHeight, IsOnGround
    (0x80037F3C, "addiu $t3, $v1, 1", "FloorHeight = T(+0x138) + 1"),
    (0x80038060, "move $v0, $t3", "FloorHeight returned (no other entity)"),
    (0x800380F8, "slt $v0, $v0, $v1", "FloorHeight < PosZ ?"),
    (0x80038100, "xori $v0, $v0, 1", "IsOnGround = not (...)"),
    (0x8003810C, "sw $v0, 0x144($s0)", "IsOnGround store"),
    # handlers
    (0x8003DB7C, "lw $v0, 0x140($a0)", "0x25 reads contact"),
    (0x8003DB8C, "lw $v0, 0x144($a0)", "0x25 reads IsOnGround"),
    (0x8003D7C4, "sw $v0, 0x88($a0)", "0x1A writes only the target animation +0x88"),
    (0x8003D7F0, "sw $v0, 0xb8($a0)", "0x1B writes ForceZ"),
    (0x8003D780, "sw $v0, 0x6c($a0)", "0x16 sets the Gravity bit"),
    # UpdateAnimation: impulse
    (0x80038AE4, "sw $zero, 0xf8($s0)", "UpdateAnimation clears +0xF8 at every call"),
    (0x80038B5C, "lh $v0, 0xa($a0)", "IZF = signed half word at +0xA of the AnimSet"),
    (0x80038B64, "sw $v0, 0xf8($s0)", "impulse written when the switch block is entered"),
    # order of the tick
    (0x8003B3A8, "jal 0x800386d0", "1: entity scripts"),
    (0x8003B3D0, "jal 0x800384f4", "2: lists"),
    (0x8003B3D8, "jal 0x80038e18", "3: UpdateAnimation"),
    (0x8003B3E0, "jal 0x80038364", "4: physics pass P"),
    # entity init: the box fields are (size << 16) - 1 (0x1F0 width, 0x1F4 depth in Y, 0x1F8 height), 0 when the size is 0
    (0x80039CB8, "sll $v0, $v1, 0x10", "width field: size << 16 ..."),
    (0x80039CBC, "addiu $v0, $v0, -1", "... minus 1 unit"),
    (0x80039CC4, "sw $v0, 0x1f0($t0)", "width field +0x1F0"),
    # terrain probe corners: x_l = (PosX + OffsetX) >> 16, x_r = (PosX + OffsetX + width_field) >> 16
    (0x800370E8, "lw $v0, 0x114($a0)", "PosX"),
    (0x800370EC, "lw $v1, 0x1e4($a0)", "OffsetX"),
    (0x800370F8, "sra $v1, $v0, 0x10", "x_l"),
    (0x80037104, "lw $v1, 0x1f0($a3)", "width field"),
    (0x8003710C, "addu $v0, $v0, $v1", "PosX + OffsetX + width field"),
    (0x80037110, "sra $v0, $v0, 0x10", "x_r"),
    # x -> column table at 0x80127168, filled by the loop 0x8003B190-0x8003B1D4: table[24*c + j] = c for c in 0..51 (0x34), j in 0..23 (0x18)
    (0x8003B198, "addiu $a1, $v0, 0x7168", "table base 0x80127168"),
    (0x8003B1B4, "sh $s3, ($v0)", "table[a0 + s1] = column s3"),
    (0x8003B1BC, "slti $v0, $s1, 0x18", "24 entries per column"),
    (0x8003B1CC, "slti $v0, $s3, 0x34", "52 columns"),
    (0x8003B1D4, "addiu $a0, $a0, 0x18", "next column starts 24 entries later"),
    # terrain probe
    (0x80037324, "sll $a1, $a1, 4", "flat cell: height = byte3 * 16 (px) ..."),
    (0x80037328, "sll $a1, $a1, 0x10", "... in 16.16"),
    (0x8003732C, "slt $v0, $t2, $a1", "max over the 4 corners (t2 starts at 0)"),
    (0x800371E8, "beqz $v1, 0x80037324", "slope & 3 == 0 -> flat"),
    # 0x37: wait
    (0x8003E3E8, "bne $v0, $a2, 0x8003e414", "first call at this pc: store pc, counter 0, yield"),
    (0x8003E400, "slt $v0, $v0, $a0", "counter < N ?"),
    (0x8003E404, "xori $v0, $v0, 1", "counter >= N -> 1"),
    (0x8003E408, "sll $v0, $v0, 1", "-> returns 2 (advance) when done, 0 (yield) otherwise"),
]


def anchors():
    try:
        import capstone
    except Exception as e:  # pragma: no cover
        p("anchors: capstone not importable (%s): the model is NOT tied to the EXE bytes in this run" % e)
        return None
    data = open(EXE, "rb").read()
    md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
    bad = 0
    for addr, text, why in ANCHORS:
        off = 0x800 + addr - 0x80020000
        ins = list(md.disasm(data[off:off + 4], addr))
        got = "%s %s" % (ins[0].mnemonic, ins[0].op_str) if ins else ".word"
        ok = got == text
        bad += 0 if ok else 1
        p("  %s %08x  %-28s  %s%s" % ("ok " if ok else "BAD", addr, got, why, "" if ok else "   <-- expected: " + text))
    p("anchors: %d checked, %d differ" % (len(ANCHORS), bad))
    return bad


# ------------------------------------------------------------------------------------------------------------------------------------------------
# 1. the vertical step of ONE entity (no carrier, no other entity, no ceiling), binary convention
# ------------------------------------------------------------------------------------------------------------------------------------------------
M16 = 65536
ceil_cap = 0x07800000          # 0x80036DC4 (lui 0x780): the ceiling test is only reached for F > 0; asserted not to trigger


class Ent:
    def __init__(self, T0, hover=0, gravity=True, offz=0):
        self.gravity = gravity
        self.offz = offz
        self.P = T0 + 1 + hover        # PosZ_bin: at rest on the floor it is T + 1 (0x80036C30, 0x800376F0)
        self.F = 0                     # ForceZ (+0xB8)
        self.Fin = 0                   # FinalForceZ (+0xEC)
        self.contact = 0               # +0x140
        self.ground = 0                # +0x144


def pass_P(e, T_land, T_floor, impulse=0, rule="strict", G=128, V=4096):
    """One physics pass P for one entity. T_land: terrain probed BEFORE the XY step (ComputeZ); T_floor: terrain after the XY step (+0x138 at the end of the pass).
    rule 'strict' = the binary (T >= ModdedPosZ + F lands); rule 'le' = the DLL comparison `P_dll + F <= T` written in the binary convention (T + 1 >= ModdedPosZ + F)."""
    e.contact = 0                                                   # 0x800383B4
    modded = e.P + e.offz                                           # 0x800383D0 / 0x800383E0
    # forces 0x80036A84
    if impulse != 0:
        if (impulse & 0xFFFF) == 0x8000 and not e.gravity:          # marker 0x8000 (0x80036AD0): not met in this study
            e.F = 0
        else:
            e.F = impulse << 8                                      # 0x80036AFC
    elif e.gravity:
        v = e.F - (G << 8)                                          # 0x80036B30
        lim = V << 8
        if lim < abs(v):                                            # 0x80036B48
            v = lim if v > 0 else -lim                              # 0x80036B54-0x80036B5C
        e.F = v
    e.Fin = e.F                                                     # 0x80036BB8
    F = e.Fin
    # ComputeZ 0x800375E0
    if F > 0:
        assert modded + 0 + F <= ceil_cap, "ceiling cap would trigger"   # 0x80036DC4 (height field +0x1F8 taken as 0 here: the assertion is only a sanity bound)
        e.P += F                                                    # 0x80037708-0x80037714 (no ceiling in open space)
    else:
        t1 = modded + F                                             # 0x80036C20
        landing = (T_land >= t1) if rule == "strict" else (T_land + 1 >= t1)
        if landing:
            e.contact = 1                                           # 0x800376E0
            e.P = T_land + 1 - e.offz                               # 0x80036C30 / 0x800376F0
            if e.gravity:
                e.F = 0                                             # 0x80037704
        else:
            e.P += F                                                # 0x80037710
    # end pass 0x80038064 : FloorHeight = T_floor + 1 ; IsOnGround = not (FloorHeight < PosZ)
    e.ground = 0 if (T_floor + 1) < e.P else 1                      # 0x800380F8-0x8003810C


def settle(e, T, n=3):
    for _ in range(n):
        pass_P(e, T, T)


def run(T0, izf=None, hover=0, gravity=True, force_at_1=None, tail_after_ret=False, n=26, rule="strict", Tfn=None):
    """Timeline of n updates. izf: impulse at update 1. force_at_1: value written to ForceZ by a `1B` of the script of tick s (before update 1).
    tail_after_ret: `1B [0,0]` in the call in which the 0x25 returns (tick s+K, before update K+1). Tfn(k) -> (T_land, T_floor) for update k."""
    e = Ent(T0, hover, gravity)
    if gravity:
        settle(e, T0)
    if force_at_1 is not None:
        e.F = force_at_1
    rows = []
    K = None
    for k in range(1, n + 1):
        TL, TF = Tfn(k) if Tfn else (T0, T0)
        pass_P(e, TL, TF, (izf or 0) if k == 1 else 0, rule)
        rows.append(dict(k=k, P_bin=e.P, P_dll=e.P - 1, F=e.F, Fin=e.Fin, contact=e.contact, ground=e.ground))
        if K is None and (e.contact or e.ground):
            K = k
            if tail_after_ret:
                e.F = 0
    return rows, K


def fmt_rows(rows, lo, hi):
    for r in rows:
        if lo <= r["k"] <= hi:
            p("    k=%2d tick s+%-2d  P_dll=%8d  ForceZ=%8d  FinalForceZ=%8d  contact=%d  IsOnGround=%d   0x25 at s+%d reads %d" % (
                r["k"], r["k"] - 1, r["P_dll"], r["F"], r["Fin"], r["contact"], r["ground"], r["k"], 1 if (r["contact"] or r["ground"]) else 0))


# ------------------------------------------------------------------------------------------------------------------------------------------------
# 2. terrain probe on the real cells (map 10) : max over the 4 corners of the box of the flat cell height (0x800370C4)
# ------------------------------------------------------------------------------------------------------------------------------------------------
def load_cells(mid):
    for f in glob.glob(os.path.join(ROOT, "alundra-project", "Maps", "*", "*", "tilemap", "*.tileMap")):
        f = f.replace("\\", "/")
        if re.search(r"-%d/tilemap/" % mid, f):
            tm = json.load(open(f, encoding="utf-8"))
            cells = json.loads(tm["custom_properties"]["AlundraCells"])
            return cells, tm["map_size"]["w"], tm["map_size"]["h"], f
    raise KeyError(mid)


def make_terrain(cells, W, offx, sizex, offy, sizey, posy, colw=24):
    """T(PosX) for a box (offx,sizex,offy,sizey in 16.16; sizex / sizey are the FIELD values (size << 16) - 1) at the fixed PosY (16.16). Column = table[x] = x // 24 for x < 1248 (table filled by the loop 0x8003B190-0x8003B1D4, read in the EXE [R]), clipped to 0..51 (0x80037178: 0x33).
    Row = y >> 4 clipped to 0..59 (0x80037190-0x800371B0). A cell is flat if slope & 3 == 0 (0x800371E4-0x800371E8): height = byte3 * 16 px."""
    yt = (posy + offy) >> 16
    yb = (posy + offy + sizey) >> 16
    rows = [min(max(yt >> 4, 0), 59), min(max(yb >> 4, 0), 59)]

    def col(x):
        return 0 if x <= 0 else min(x // colw, 51)

    def T(posx):
        xs = [(posx + offx) >> 16, (posx + offx + sizex) >> 16]
        best = 0
        for r in rows:
            for x in xs:
                i = r * W + col(x)
                assert cells["slope"][i] & 3 == 0, "sloped cell in play: probe not modelled"
                best = max(best, cells["height"][i] * 16 * M16)
        return best

    return T


# ------------------------------------------------------------------------------------------------------------------------------------------------
# 3. main
# ------------------------------------------------------------------------------------------------------------------------------------------------
def main():
    p("recompute_z.py - independent re-derivation (no zsim / mips / emu / zmodel / vz / rules_vs_emu).  Python %s" % sys.version.split()[0])
    p("")
    p("== 0. anchors: the model is the disassembly (instruction words of ALUN_CD.EXE France)")
    bad = anchors()
    p("")

    R = json.load(open(os.path.join(ANNEX, "out", "replay_all.json"), encoding="utf-8"))
    verdict = []

    # --------------------------------------------------------------------------------------------------------------------------------------------
    p("== 1. IZF 1360 (flat terrain 0, G 128, V 4096, Gravity)")
    rows, K = run(0, izf=1360)
    fmt_rows(rows, 1, 2)
    p("    ...")
    fmt_rows(rows, 20, 25)
    P22 = [r["P_dll"] for r in rows[:22]]
    p("  PosZ_dll of the updates 1..22 =", P22)
    p("  K (first update with contact or IsOnGround) = %d ; ForceZ=%d FinalForceZ=%d at K ; contact at %s ; IsOnGround at %s" % (
        K, rows[K - 1]["F"], rows[K - 1]["Fin"], next(r["k"] for r in rows if r["contact"]), next(r["k"] for r in rows if r["ground"])))
    p("  => 0x25 returns at s+%d.  First poll after `1A [3]; 37 [3]` = s+4 (0x37 handler: done at the call where the counter reaches N, i.e. s+N+1) -> %d polls." % (K, K - 3))
    p("  hand check of the landing update: update 22 : P_bin=430081 + F(-339968) = 90113 ; T(0) >= 90113 ? no -> falls to 90113 (P_dll 90112).  update 23 : F = 348160-22*32768 = -372736 ;")
    p("     P_bin 90113 + F = -282623 ; 0 >= -282623 -> LANDING (strict or <=: same, the sum is not on the boundary).")
    for rule in ("strict", "le"):
        r2, K2 = run(0, izf=1360, rule=rule)
        p("  rule %-6s: K=%s   (contact at %s)" % (rule, K2, next((r["k"] for r in r2 if r["contact"]), None)))
    verdict.append(("IZF1360 K=23 (0x25 at s+23), 22 air PosZ = UJ-1 list, FinalForceZ -372736, contact 1 & IsOnGround 1 at 23", K == 23 and rows[22]["Fin"] == -372736 and rows[22]["F"] == 0))

    # --------------------------------------------------------------------------------------------------------------------------------------------
    p("")
    p("== 2. IZF 1280")
    rows12, K12 = run(0, izf=1280)
    fmt_rows(rows12, 18, 24)
    P20 = [r["P_dll"] for r in rows12[:20]]
    p("  PosZ_dll of the updates 1..20 =", P20)
    first_p0 = next(r["k"] for r in rows12 if r["P_dll"] == 0)
    r21 = rows12[20]
    p("  first update with P_dll = 0 : %d  (ForceZ %d, FinalForceZ %d, contact %d, IsOnGround %d)" % (first_p0, r21["F"], r21["Fin"], r21["contact"], r21["ground"]))
    p("  hand check, update 21 : F = 327680 - 20*32768 = -327680 ; P_bin(after 20) = 327681 ; t1 = 327681 - 327680 = 1 ; T(0) >= 1 ? NO (strict) -> no landing, P_bin = 1, P_dll = 0, ForceZ stays -327680, contact 0 ;")
    p("     IsOnGround = not(FloorHeight(1) < PosZ(1)) = 1.   update 22 : F = -360448 ; t1 = 1 - 360448 ; 0 >= t1 -> LANDING (contact 1, ForceZ 0).")
    rle, Kle = run(0, izf=1280, rule="le")
    p("  rule 'le' (DLL comparison, P_dll + F <= T) : update 21 -> ForceZ=%d contact=%d  (the DLL zeroes ForceZ at 21 ; contact is a separate DLL flag, see UJ1b comment)" % (rle[20]["F"], rle[20]["contact"]))
    p("  K (strict) = %d ; contact first at %d ; 0x25 returns at s+%d" % (K12, next(r["k"] for r in rows12 if r["contact"]), K12))
    verdict.append(("IZF1280 P=0 at update 21 with ForceZ -327680, contact 0, IsOnGround 1; contact & ForceZ 0 at 22; 0x25 at s+21",
                    first_p0 == 21 and r21["F"] == -327680 and r21["contact"] == 0 and r21["ground"] == 1 and rows12[21]["contact"] == 1 and rows12[21]["F"] == 0 and K12 == 21))

    # --------------------------------------------------------------------------------------------------------------------------------------------
    p("")
    p("== 3. other families (cross-check of the whole table of the annex, same recurrence)")
    fam = {}
    for izf in (768, 1024):
        rws, Kx = run(0, izf=izf)
        fam[izf] = (rws, Kx)
        p("  IZF %4d : K=%d  ForceZ at K=%d  contact first at %d  last air point %.4f px" % (izf, Kx, rws[Kx - 1]["F"], next(r["k"] for r in rws if r["contact"]),
                                                                                    rws[Kx - 2]["P_dll"] / M16))
    p("  last air point of 1360 = %.4f px, of 1280 = %.4f px" % (rows[21]["P_dll"] / M16, rows12[19]["P_dll"] / M16))
    trv, Kt = run(0, gravity=False, hover=8 * M16, force_at_1=-32768, tail_after_ret=True, n=24)
    trk, Ktk = run(0, gravity=False, hover=8 * M16, force_at_1=-32768, tail_after_ret=False, n=24)
    p("  TR-V (no gravity, 8 px, 1B [128,255]) : K=%d (0x25 at t+%d) ; PosZ_dll-floor of updates 1..15 = %s" % (Kt, Kt, [r["P_dll"] for r in trv[:15]]))
    p("     with the `1B [0,0]` tail : contact raised at %s (never) ; WITHOUT the tail : contact first at %s" % (
        next((r["k"] for r in trv if r["contact"]), None), next((r["k"] for r in trk if r["contact"]), None)))
    p("     foot at update 8 = %d (= 4.000 px exactly: %s)" % (trv[7]["P_dll"], trv[7]["P_dll"] == 4 * M16))
    verdict.append(("TR-V K=16 (t+16); contact never with the 1B[0,0] tail, 17 without it", Kt == 16 and all(r["contact"] == 0 for r in trv) and Ktk == 16 and next(r["k"] for r in trk if r["contact"]) == 17))
    verdict.append(("IZF768 K=13 ForceZ -196608 contact 14; IZF1024 K=17 ForceZ -262144 contact 18",
                    fam[768][1] == 13 and fam[768][0][12]["F"] == -196608 and fam[1024][1] == 17 and fam[1024][0][16]["F"] == -262144
                    and next(r["k"] for r in fam[768][0] if r["contact"]) == 14 and next(r["k"] for r in fam[1024][0] if r["contact"]) == 18))

    # --------------------------------------------------------------------------------------------------------------------------------------------
    p("")
    p("== 4. field-by-field comparison with out/replay_all.json (engine A rows; my numbers are NOT taken from it)")
    cases = {
        "S-1360": dict(T0=0, izf=1360), "S-1280": dict(T0=0, izf=1280), "S-768": dict(T0=0, izf=768), "S-1024": dict(T0=0, izf=1024),
        "R-1360-Giles10": dict(T0=0, izf=1360), "R-1360-Bergus165": dict(T0=0, izf=1360), "R-1360-Bergus179": dict(T0=16 * M16, izf=1360),
        "R-1360-Bergus172": dict(T0=16 * M16, izf=1360), "R-768-Dog478a": dict(T0=16 * M16, izf=768), "R-768-Dog478b": dict(T0=16 * M16, izf=768),
        "R-1280-GilesAnim16": dict(T0=0, izf=1280), "R-1024-Bomb362": dict(T0=16 * M16, izf=1024),
        "S-TRV": dict(T0=0, gravity=False, hover=8 * M16, force_at_1=-32768, tail_after_ret=True),
        "S-TRV-keep": dict(T0=0, gravity=False, hover=8 * M16, force_at_1=-32768, tail_after_ret=False),
        "R-TRV-rec69": dict(T0=0, gravity=False, hover=8 * M16, force_at_1=-32768, tail_after_ret=True),
        "R-TRV-rec81": dict(T0=0, gravity=False, hover=8 * M16, force_at_1=-32768, tail_after_ret=True),
    }
    allok = True
    for name, kw in cases.items():
        A = R[name]["A"]
        mine, Km = run(n=len(A), **kw)
        diffs = []
        for a, m in zip(A, mine):
            ours = (m["P_bin"], m["F"], m["Fin"], m["contact"], m["ground"])
            theirs = (a["posz"], a["forcez"], a["finalz"], a["coll"], a["ground"])
            if ours != theirs:
                diffs.append((m["k"], ours, theirs))
        Ka = R[name]["first_poll_update"]
        ok = not diffs and Km == Ka == R[name]["return_A"] == R[name]["return_B"]
        allok &= ok
        p("  %-20s K mine=%-3s annex(free poll)=%-3s annex(real program A/B)=%s/%s  rows compared=%d  field differences=%d  => %s" % (
            name, Km, Ka, R[name]["return_A"], R[name]["return_B"], len(A), len(diffs), "AGREE" if ok else "DISAGREE"))
        for d in diffs[:5]:
            p("       k=%d mine(P_bin,F,Fin,contact,ground)=%s annex=%s" % d)
    verdict.append(("all 16 cases of replay_all.json: every row (PosZ_bin, ForceZ, FinalForceZ, contact, IsOnGround) and K", allok))
    p("  note: R-1024-Bomb362 starts with Gravity OFF in the annex rest state; the script `1A [2]` + `0x16` sets the Gravity bit (handler 0x8003D774, anchor ok) in the call of the jump,")
    p("        so the impulse of update 1 meets Gravity ON: same recurrence as S-1024.")

    # DLL-side independent source: the flights pinned by the tests (read, not run)
    tfile = os.path.join(ROOT, "Alundra.Tests", "AlundraAnimationImpulseTests.cs")
    src = open(tfile, encoding="utf-8-sig").read()

    def arr(name):
        m = re.search(name + r"\s*=\s*\{([^}]*)\}", src, re.S)
        return [int(x) for x in re.findall(r"-?\d+", m.group(1))]

    f1360, f1280 = arr("Flight1360"), arr("Flight1280")
    ok1 = f1360 == P22 and f1280 == P20
    p("  DLL test lists (Alundra.Tests/AlundraAnimationImpulseTests.cs, Flight1360 / Flight1280, READ not run) equal my PosZ_dll lists: 1360 -> %s, 1280 -> %s" % (f1360 == P22, f1280 == P20))
    verdict.append(("the in-air PosZ lists equal the DLL test lists Flight1360 (22) and Flight1280 (20)", ok1))

    # --------------------------------------------------------------------------------------------------------------------------------------------
    p("")
    p("== 5. Giles (map 10 rec98, `1A [3]` @6389), IZF 1360, onto the 16 px ledge; strict vs <= ; terrain before / after the XY step")
    cells, W, H, f = load_cells(10)
    p("  cells: %s (W=%d H=%d)" % (os.path.relpath(f, ROOT).replace("\\", "/"), W, H))
    posy = 56098816
    # prefab 9b582582: off (-10,-7,0) size (20,14,32) (survey out/prefabs_survey.txt, read); the probe uses the width / depth FIELDS = (size << 16) - 1 (0x80039CB8-0x80039CC4)
    box = dict(offx=-10 * M16, sizex=20 * M16 - 1, offy=-7 * M16, sizey=14 * M16 - 1)
    T = make_terrain(cells, W, posy=posy, **box)
    rowi = ((posy + box["offy"]) >> 16) >> 4
    p("  Giles PosY 856 px -> box rows y = %d..%d -> row %d (both) ; height byte of columns 18..27 on that row = %s ; slope bytes & 3 = %s" % (
        (posy + box["offy"]) >> 16, (posy + box["offy"] + box["sizey"]) >> 16, rowi, [cells["height"][rowi * W + c] for c in range(18, 28)],
        sorted(set(cells["slope"][rowi * W + c] & 3 for c in range(18, 28)))))
    ledge_col = next(c for c in range(18, 52) if cells["height"][rowi * W + c] == 1)
    p("  ledge in front of Giles = first column >= 18 with height 1 on that row : column %d -> x >= %d px (column = x // 24, table 0x80127168 filled by the loop 0x8003B190-0x8003B1D4 [R]); columns %d..51 all height 1" % (
        ledge_col, 24 * ledge_col, ledge_col))

    def giles(x0, step1, step, order, rule, n=30):
        e = Ent(0)
        settle(e, 0)
        x = x0
        rows_g = []
        K_g = None
        first_pre = first_post = None
        for k in range(1, n + 1):
            xn = x + (step1 if k == 1 else step)
            tpre, tpost = T(x), T(xn)
            if tpre and first_pre is None:
                first_pre = k
            pass_P(e, tpre if order == "pre" else tpost, tpost, 1360 if k == 1 else 0, rule)
            if tpost and first_post is None:
                first_post = k
                giles.p_first_post = e.P - 1          # PosZ_dll after the Z step of the update at which the ledge first enters the box (post-step probe)
            rows_g.append(dict(k=k, x=xn, P_dll=e.P - 1, F=e.F, Fin=e.Fin, contact=e.contact, ground=e.ground, tpre=tpre, tpost=tpost))
            if K_g is None and (e.contact or e.ground):
                K_g = k
            x = xn
        return rows_g, K_g, first_pre, first_post

    x0 = 32329728                       # plan docs/plan-e19-d2c-valeurs.md:28 (DLL-derived): x at the tick of the `1A`, 493.3125 px
    S1_BIN, S1_DLL, STEP = 129024, 159744, 98304     # [I] first step: binary order (animation applies in update 1) / plan's DLL value ; steady 98304 (anim 3, speed 128)
    p("  INPUTS (not recomputed from the binary): x0 = %d (%.4f px, plan d2c-valeurs:28), first XY step 129024 (annex: UpdateAnimation then P in one tick) or 159744 (plan, DLL one frame late), then 98304 per update" % (x0, x0 / M16))
    p("  consistency of the two first-step values with the plan's DLL sequence 159744, 129024, 98304: the walk force goes 159744 -> 98304 in two equal steps of 30720; the binary applies the change one pass earlier.")
    res = {}
    for label, step1 in (("binary XY (129024)", S1_BIN), ("plan XY (159744)", S1_DLL)):
        p("  -- first step %s" % label)
        for order, rule, name in (("pre", "strict", "binary  : terrain BEFORE the step, strict"), ("pre", "le", "mixed   : terrain BEFORE the step, <="),
                                  ("post", "strict", "mixed   : terrain AFTER the step,  strict"), ("post", "le", "DLL-like: terrain AFTER the step,  <=")):
            rg, Kg, fpre, fpost = giles(x0, step1, STEP, order, rule)
            res[(step1, order, rule)] = (rg, Kg)
            rk = rg[Kg - 1]
            p("     %-44s ledge seen by the pre-step probe from update %s, by the post-step probe from %s ; K=%s  PosZ_dll(K)=%d  ForceZ(K)=%d FinalForceZ(K)=%d contact=%d ground=%d  -> 0x25 at s+%d" % (
                name, fpre, fpost, Kg, rk["P_dll"], rk["F"], rk["Fin"], rk["contact"], rk["ground"], Kg))
    rgb, Kb = res[(S1_BIN, "pre", "strict")]
    p("  table (binary XY, pre-step terrain, strict):")
    for r in rgb:
        if r["k"] in (1, 2, 15, 16, 17, 18, 19, 20, 21):
            p("     k=%2d PosX=%9.4f px  right edge=%9.4f px  P_dll=%8d  ForceZ=%8d FinalForceZ=%8d  contact=%d ground=%d  T_pre=%2d px T_post=%2d px" % (
                r["k"], r["x"] / M16, r["x"] / M16 + 10, r["P_dll"], r["F"], r["Fin"], r["contact"], r["ground"], r["tpre"] // M16, r["tpost"] // M16))
    p("  update 18 (no landing, any rule) : P_dll(17)=%d + F(18)=%d = %d > ledge top %d ; update 19 : P_dll(18)=%d + F(19)=%d = %d < %d -> landing (the sum is not on the boundary, so strict and <= agree)." % (
        rgb[16]["P_dll"], rgb[17]["F"], rgb[16]["P_dll"] + rgb[17]["F"], 16 * M16, rgb[17]["P_dll"], rgb[18]["Fin"], rgb[17]["P_dll"] + rgb[18]["Fin"], 16 * M16))
    kall = {k: v[1] for k, v in res.items()}
    p("  K over the 8 combinations (first step x order x rule) =", sorted(set(kall.values())))
    last_air = rgb[17]["P_dll"]
    p("  last air point (update 18) = %d = %.4f px = %.4f px above the ledge top (< 4 px: inside the DLL engine magnet, mechanism (c), not modelled here)" % (
        last_air, last_air / M16, (last_air - 16 * M16) / M16))
    verdict.append(("Giles K=19 on the 16 px ledge for strict and <= (and for either probe order), 0x25 at s+19 = F0+120 (s = F0+101)",
                    set(kall.values()) == {19}))

    # ledge visibility and sensitivity to the start x (XY still an input)
    p("  box right edge after update 16 / 17 / 18 (binary XY): %.4f / %.4f / %.4f px (ledge when >= 528 px, the real edge being 1/65536 px less)" % tuple(rgb[i]["x"] / M16 + 10 for i in (15, 16, 17)))
    p("  sweep of x0 (1/8 px, 480..520 px).  The XY walk is FREE in this model (ComputeXY and its blocking by the wall are not recomputed), so a start is called 'wall-free' only if the ledge")
    p("  first enters the box (post-step probe) at an update where the entity is already at or above the ledge top after its Z step (PosZ_dll >= 16 px); otherwise the real ComputeXY blocks the")
    p("  box at the wall (the annex output shows the right edge stuck at 528.000 px for x0 < 490.6, K=23) and this model is not comparable there.")
    lo_analytic = (528 - 10) - (S1_BIN / M16 + 17 * STEP / M16) + 1 / M16
    p("  analytic lower bound (ledge seen by the pre-step probe at update 19, i.e. x_r = (PosX + 10 px - 1 unit) >> 16 >= 528 after update 18): x0 >= 528 - 10 - %.5f - 17*1.5 + 1/65536 = %.6f px (binary first step); with the plan's first step 159744: %.6f px" % (
        S1_BIN / M16, lo_analytic, (528 - 10) - (S1_DLL / M16 + 17 * STEP / M16) + 1 / M16))
    sweep_lo = {}
    for step1 in (S1_BIN, S1_DLL):
        for order, rule in (("pre", "strict"), ("pre", "le"), ("post", "strict"), ("post", "le")):
            wf = {}
            for i in range(0, 41 * 8):
                xx = int((480 + i / 8.0) * M16)
                rg_, kk, fpre_, fpost_ = giles(xx, step1, STEP, order, rule)
                wf[xx / M16] = (kk, fpost_ is not None and giles.p_first_post >= 16 * M16)
            good = [x for x, (kk, ok) in wf.items() if ok and kk == 19]
            lo = min(good)
            hi = max(x for x, (kk, ok) in wf.items() if ok and kk == 19)
            bad_wf = [x for x, (kk, ok) in wf.items() if ok and kk != 19 and x >= lo]
            sweep_lo[(step1, order, rule)] = lo
            p("     first step %6d  probe %-4s rule %-6s : wall-free starts with K=19: from x0 = %.3f px to %.3f px ; wall-free starts with K != 19: %s ; the start just below %.3f is wall-free: %s" % (
                step1, order, rule, lo, hi, bad_wf[:3] if bad_wf else "none", lo, wf.get(lo - 0.125, (None, None))[1]))
    # walk animation 1 of the annex: steady 98304 from the first step (speed 128 before and after) -> analytic bound 491 + 1/65536, grid 491.125
    lo1 = None
    for i in range(0, 41 * 8):
        xx = int((480 + i / 8.0) * M16)
        rg_, kk, fpre_, fpost_ = giles(xx, STEP, STEP, "pre", "strict")
        if kk == 19 and fpost_ is not None and giles.p_first_post >= 16 * M16:
            lo1 = xx / M16
            break
    p("  walk-animation-1 like start (first step 98304): lowest wall-free x0 on the 1/8 grid with K=19 = %.3f px ; analytic bound 528 - 10 - 18*1.5 + 1/65536 = %.6f px" % (lo1, 528 - 10 - 27 + 1 / M16))
    verdict.append(("Giles K=19 for every wall-free start from 490.625 px (walk anim 9; analytic 490.531265) and from 491.125 px (walk anim 1; analytic 491.000015) - the lower bounds of the annex's fine sweep",
                    abs(sweep_lo[(S1_BIN, "pre", "strict")] - 490.625) < 1e-9 and abs(lo1 - 491.125) < 1e-9))

    # --------------------------------------------------------------------------------------------------------------------------------------------
    p("")
    p("== 6. ledge fall (NPC with Gravity, box 20 px wide off -10, cells 16 px high on columns 0..5 = x < 144 px, flat 0 beyond ; walks east at 65536 per tick from PosX 150 px)")
    ledge_cells = {"height": [], "slope": []}
    Wl = 52
    for r in range(60):
        for c in range(Wl):
            ledge_cells["height"].append(1 if c < 6 else 0)
            ledge_cells["slope"].append(0)
    TL = make_terrain(ledge_cells, Wl, posy=100 * M16, offx=-10 * M16, sizex=20 * M16 - 1, offy=-7 * M16, sizey=14 * M16 - 1)

    def ledge_fall(order, rule):
        e = Ent(16 * M16)
        settle(e, 16 * M16)
        x = 150 * M16
        out = []
        for t in range(1, 9):
            xn = x + 65536
            pass_P(e, TL(x) if order == "pre" else TL(xn), TL(xn), 0, rule)
            out.append((t, xn / M16, e.P - 1, e.contact, e.ground, e.Fin, TL(xn)))
            x = xn
        return out

    for order, rule, name in (("pre", "strict", "binary (terrain before the step, strict)"), ("post", "le", "DLL order (terrain after the step, <=)"),
                              ("post", "strict", "terrain after the step, strict"), ("pre", "le", "terrain before the step, <=")):
        o = ledge_fall(order, rule)
        first_air = next(t for (t, x, pz, c, g, fz, tt) in o if pz < 16 * M16)
        p("  %-42s first tick in the air = %d" % (name, first_air))
    o = ledge_fall("pre", "strict")
    for (t, x, pz, c, g, fz, tt) in o:
        p("     t=%d PosX=%.2f PosZ_dll=%8d contact=%d IsOnGround=%d FinalForceZ=%8d Terrain(after the pass)=%d" % (t, x, pz, c, g, fz, tt))
    first_bin = next(t for (t, x, pz, c, g, fz, tt) in ledge_fall("pre", "strict") if pz < 16 * M16)
    first_dll = next(t for (t, x, pz, c, g, fz, tt) in ledge_fall("post", "le") if pz < 16 * M16)
    verdict.append(("ledge fall: first tick in the air 5 (binary order) ; 4 with the terrain read after the step (DLL order, arithmetic only)", first_bin == 5 and first_dll == 4))

    # --------------------------------------------------------------------------------------------------------------------------------------------
    p("")
    p("== 7. first poll of the 0x25 after `1A [N]; 37 [N]` (handler 0x8003E3DC read above)")
    p("  call 1 at tick s : pc stored, counter 0 -> returns 0 (yield) ; call j (j>=2) : returns 2 (advance, same call goes on to the 0x25) iff counter(j-2) >= N ; counter = j-2")
    for N in (2, 3):
        calls = []
        counter = None
        for t in range(0, 8):
            if t == 0:
                counter = 0
                r = 0
            else:
                r = 2 if counter >= N else 0
                counter += 1
            calls.append(r)
            if r == 2:
                p("  37 [%d] first returns 2 at tick s+%d -> first 0x25 poll at s+%d -> for IZF 1360 (K=23) the number of 0x25 executions is %d" % (N, t, t, 23 - t + 1))
                break

    p("")
    p("== VERDICT of this script (True = my independent recurrence agrees with the annex value)")
    for text, ok in verdict:
        p("  %-6s %s" % ("AGREE" if ok else "DISAGREE", text))
    p("  anchors differing from the EXE: %s" % bad)


if __name__ == "__main__":
    main()
    txt = "\n".join(OUT) + "\n"
    sys.stdout.reconfigure(newline="\n")
    sys.stdout.write(txt)
    if len(sys.argv) > 1:
        open(sys.argv[1], "w", encoding="utf-8", newline="\n").write(txt)
