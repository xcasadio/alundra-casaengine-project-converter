# E20.c1b-2 L0: what the binary does with the hero slope word (+0x18C) when it holds 1

Source: `ALUN_CD.EXE` (France), decoded with capstone (MIPS32 little endian; file offset `0x800 + addr - 0x80020000`). The decompilation was not consulted for the verdict and is not corrected. The hero record is the global at `0x80127D30`; its slope word is `lw 0x18C($s0)`, `$s0 = 0x80127D30`.

## Verdict

**The binary does not forbid the sword swing on slope 1.** For the value 1, `MovePlayer` takes exactly the path it takes for the values 0, 2, 3, 5 and 7, and `PlayerTryAttack` never reads the slope. The plan's stop condition "the binary forbids the swing on slope 1" does not hold; arcs B (map 116, levers on cells whose ground property is 2, which the DLL reads as slope 1) may proceed.

## Evidence

1. `MovePlayer` (`0x80031B50`) reads the slope word once, at the top: `0x80031B98 lw $s2, 0x18C($s0)`. A scan of `0x80031B50` onwards (1400 instructions) finds no other load of `0x18C`; `$s2` is the slope for the rest of the function.
2. The uses of `$s2` in `MovePlayer`:
   - `0x80031CF4 sltiu $v0, $s2, 8` then `0x80031CFC beqz ... 0x80032914` (a slope of 8 or more leaves the function), then the jump table at `0x80022D1C` indexed by `$s2` (`0x80031D08 sll $v1, $s2, 2` ... `0x80031D18 jr $v0`). The eight entries, read from the executable:

     | slope | target |
     |---|---|
     | 0 | `0x80031D68` |
     | **1** | **`0x80031D68`** |
     | 2 | `0x80031D68` |
     | 3 | `0x80031D68` |
     | 4 | `0x8003260C` (swimming path) |
     | 5 | `0x80031D68` |
     | 6 | `0x80031D20` (the slope-6 entry gate, `Slope_18c == 6` of the DLL) |
     | 7 | `0x80031D68` |

   - `0x80031C78`, `0x80032784`, `0x80032824`, `0x80032874`, `0x80032888`, `0x8003289C`, `0x800328D0`, `0x800328FC`: every one compares `$s2` with the constant `4` (`addiu $v0, $zero, 4` just before). None compares with 1, 2 or 3.
3. The common path `0x80031D68` (the same for slope 0 and slope 1) calls the player routines and dispatches on the current animation word (`0x80127DB8`, jump table `0x80022D3C`). The Idle handler reaches `PlayerTryAttack` at `0x80031EA8 jal 0x8002EAF4`.
4. `PlayerTryAttack` (`0x8002EAF4` to `0x8002ED64`): no `0x18C` load in its 156 instructions. Its gates are the entity word `0x180` bit `0x2000` (`0x8002EB1C`), the Square pad word (`0x80126F18 + 0x14`, bit `0x80`, `0x8002EB38`), the counter at `0x80126FFC` (`0x8002EB4C`, `slti ... 0x3c`) and the word `0x144` (`0x8002EB60`). Its three callees are `0x8003BDD8` (spawn) at `0x8002EBB0` and `0x800490FC` (twice, `0x8002EC50` and `0x8002ED30`); a scan of `0x8003BDD8` (300 instructions) and of `0x800490FC` (120 instructions) finds no load of `0x18C`.
5. The other loads of `0x18C` in the neighbourhood (`0x8002FB88`, `0x8002FBA4`, `0x8002FE44`, `0x8002FE90`, `0x8003024C`, `0x80030268`) are in the effect and animation routines of the player (`CreatePlayerAnimationEffects` `0x8002FB1C` and the code after it), not on the path that starts the swing.

## Consequence for the DLL arcs

In the DLL, the cells of the 116 lever wings (ground property 2) give `Slope_18c = 1`; the `switch` of `MovePlayer` does nothing for 1, as the binary does for the value 1. The swing is permitted on those cells in the original and in the DLL.
