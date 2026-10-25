import sys
sys.path.insert(0, r'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-h4/work')
import exelib as E
START = "00 01 02 04 05 06 07 08 09 0a 0b 0c 0e 0f 10 11 12 13 14 15 16 17 18 19 1c 1d 1f 20 21 22 23 24 25 26 28 29 2a 2b 2c 2d 2e 2f 30 31 32 33 34 35 39 3a 3b 3c 3e 3f 40 41 42 44 46 49 4b 4e 4f".split()
START = [int(x, 16) for x in START]
SKIP = {0x0D, 0x1A, 0x1B, 0x1E, 0x27, 0x37, 0x38, 0x3D, 0x43, 0x45, 0x47, 0x48, 0x4A, 0x4C, 0x4D}
GROUND = {0x04, 0x08, 0x09, 0x10, 0x11, 0x12, 0x13, 0x18, 0x19, 0x25, 0x3F, 0x40, 0x44, 0x49}
AIR = {0x0A, 0x14, 0x15, 0x16, 0x17, 0x1F, 0x26, 0x41, 0x42, 0x46, 0x4B}
def site(s):
    if s in (0, 1): return 'DLL `TryUseItem` in the Idle/Moving case (PlayerManager.cs:464)'
    if s in (2, 0x2B, 0x2C, 0x2D): return 'DLL jump states case (:483)'
    if s in (0x0E, 0x35): return 'DLL climb case (:502)'
    if s in (0x0F, 0x1D, 0x28, 0x3A, 0x3B): return 'DLL swim-exit case (:610), land side; in deep water SwimTick (:751-808)'
    if s == 0x29: return 'DLL PrepareSprintTick (:838)'
    if s == 0x2A: return 'DLL StopSprintTick (:943)'
    if s in (0x3E, 0x1C): return 'DLL BounceTick (:997)'
    if s == 0x31: return 'DLL hurt case (:635, c2b)'
    if s in GROUND: return 'DLL ground attack group (:649)'
    if s in AIR: return 'DLL air attack group (:659)'
    if s in (5, 0x0B): return 'DLL LiftGestureTick (:1498, g1a/g1b)'
    if s in (6, 7, 0x0C, 0x2E, 0x2F, 0x30): return 'DLL CarryGroupTick (:1518, g1a)'
    if s in (0x20, 0x21, 0x22, 0x23, 0x24): return 'NOT in DLL; no slice (cape, h2 / Q-H2)'
    if s in (0x32, 0x33, 0x34): return 'NOT in DLL; slice h4a (tomes)'
    if s in (0x39, 0x3C, 0x4E, 0x4F): return 'NOT in DLL; slice E18 (death states)'
    return '???'
print('| state | binary handler (0x80022D3C) | site / slice |')
print('|---|---|---|')
for s in START:
    print('| 0x%02X | 0x%08X | %s |' % (s, E.u32(0x80022D3C + 4 * s), site(s)))
bad = [s for s in START if site(s) == '???']
print('UNMAPPED', bad, len(START))
nores = [s for s in range(0x50) if s not in START]
print('NOREACT', ['%02X' % s for s in nores], [hex(E.u32(0x80022D3C + 4 * s)) for s in nores])
print('SKIP handlers', sorted(set(hex(E.u32(0x80022D3C + 4 * s)) for s in SKIP)))
