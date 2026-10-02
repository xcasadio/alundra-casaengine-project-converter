"""Disassemble ALUN_CD.EXE (France): dis.py <start_hex> <count> [more ranges...]
Annotates entity field offsets that matter for the jump discovery."""
import sys
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
sys.stdout.reconfigure(encoding="utf-8")
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
data = open(EXE, "rb").read()
FIELDS = {
    0x28: "PlatformEntity", 0x6c: "Flags",
    0x88: "TargetAnimationId", 0x8c: "TargetDirection", 0x90: "CurrentAnimationId", 0x94: "CurrentDirection",
    0x98: "AnimationDirection", 0x9c: "AnimationSet", 0xa0: "FirstFrame", 0xa4: "Frame", 0xa8: "NextFrameDelay",
    0xac: "ForceResetAnimationFlag", 0xb0: "AnimCompleteCounter", 0xb4: "AnimFlags", 0xb8: "ForceZ",
    0xbc: "TargetForceX", 0xc0: "TargetForceY", 0xc4: "ForceX", 0xc8: "ForceY", 0xcc: "PrevAdjX", 0xd0: "PrevAdjY",
    0xd4: "ForceStepX", 0xd8: "ForceStepY", 0xdc: "AdjustedForceX", 0xe0: "AdjustedForceY",
    0xe4: "FinalForceX", 0xe8: "FinalForceY", 0xec: "FinalForceZ", 0xf0: "Acceleration", 0xf4: "Speed",
    0xf8: "IsZForceApplied", 0xfc: "ScreenClipX", 0x100: "ScreenClipY", 0x104: "ScreenClipZ",
    0x108: "NegModX", 0x10c: "NegModY", 0x110: "NegModZ", 0x114: "PosX", 0x118: "PosY", 0x11c: "PosZ",
    0x120: "TileX", 0x124: "TileY", 0x128: "TileZ", 0x12c: "RidingEntity", 0x130: "XCollisionEntity",
    0x134: "FloorHeight", 0x138: "TerrainHeight",
    0x13c: "ForceAdjusted", 0x140: "CollidedWithEntityZ", 0x144: "IsOnGround",
    0x158: "MapHeights0", 0x168: "PlatformUpdateFlag", 0x180: "CombinedVramFlagsOR", 0x184: "CombinedVramFlagsAND",
    0x188: "TileAttributes",
    0x1d8: "ModdedPosX", 0x1dc: "ModdedPosY", 0x1e0: "ModdedPosZ", 0x1e4: "ModX", 0x1e8: "ModY", 0x1ec: "ModZ",
    0x1f0: "Width", 0x1f4: "Height", 0x1f8: "Depth",
    0x230: "LogicEntity", 0x234: "State234", 0x238: "State238",
}
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
args = sys.argv[1:]
for i in range(0, len(args), 2):
    start = int(args[i], 16)
    n = int(args[i + 1])
    off = start - 0x80020000 + 0x800
    print("---- 0x%08x (%d)" % (start, n))
    for ins in md.disasm(data[off:off + 4 * n], start):
        note = ""
        s = ins.op_str
        if "(" in s:
            try:
                disp = s.split(",")[-1].strip()
                d = int(disp.split("(")[0], 0)
                if d in FIELDS:
                    note = "   ; +0x%x %s" % (d, FIELDS[d])
                else:
                    note = "   ; +0x%x" % d if d >= 0 else ""
            except Exception:
                pass
        print("0x%08x: %-8s %s%s" % (ins.address, ins.mnemonic, s, note))
