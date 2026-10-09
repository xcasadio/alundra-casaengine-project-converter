> Report returned by one agent of the read-only discovery workflow for E20.c2 and E14.a of 2026-10-09 (slice E20.c2), kept verbatim as evidence. The plan holds the reconciled facts.

# E20.c2 binary decode: the hurt hero (label c2-binary)

Legend: **[F]** is a fact decoded at a binary address, or measured in an emulation of the real code. **[I]** is an inference, with its basis. **[U]** is unknown, with what would decide it. **[D]** is a reading of the DLL. T is the tick of the MovePlayer call that enters the hurt state. The hero is slot 0, at `0x80127D30`, stride `0x294`. Entity field names follow the DLL's `AlundraEntityScriptProxy`.

Verification, all in my scratch folder (`scratchpad/disc-c2-e14a/c2-binary/`) and never in the repo:
- `d.py` is a capstone disassembler.
- `emu/test_a_combine.py` runs the real step-6 code against a Python model on 44,000 byte cases, with 0 mismatches.
- `emu/test_bc.py` runs the real regeneration block and the real `0x80031A68` against models, 3,000 cases each, with 0 mismatches.
- `c1tools/hurt_run.py` and its siblings run the real tick passes in a copy of the e20-c1 oracle world: hero, one inert Beannoid, new-game kit, balance level D=0. The timelines quoted below come from it.

## 0. Things that change the plan

1. **The END site is common to all paths; the DLL's early returns skip it.**
   - [F] `0x80032914` runs `UpdateItemEffectState`, then `SetPlayerHpMax(hero+0x18)`, then `SetPlayerHp(hero+0x14)`. Every exit reaches it:
     - the frozen exit (`+0x20 != 0`, `0x80031BBC`);
     - the locked branch (`0x80031C0C/20/28`);
     - the hurt path (`0x80031C98`);
     - the death branch and every table case.
   - [D] `AlundraPlayerManager.cs:208-213` (BlockedByEntity) and `:220-223` (InputBlockedMask) `return` before the END call at `:463`.
   - With c2, both must reach the END. Otherwise the Hp mirror and the hero sheet freeze during dialogues and locked sequences. The regen has its own gates, so the END is harmless there.

2. **Hero HP is entity-borne. The DLL still writes `PlayerStats` directly in two places.**
   - [F] Hero `+0x14` (Hp) and `+0x18` (HpMax) are authoritative. PlayerStats is a one-way mirror at every END.
   - [F] The copy back from PlayerStats happens only at hero spawn (`0x80031A00-0x80031A08`: HpMax then Hp, via getters `0x8004DC50` and `0x8004DD18`). The spawn is called from the map-load routine at `0x8003B2D4`.
   - [D] `InitializeBalance` sets `Hp = HpMax = record.MaxHp` (10) in `AlundraEntityScriptProxy.HitPass.cs:70-71`. After a loaded save with other HP that is wrong. c2 must overwrite both from PlayerStats right after it.
   - [D] `IncreaseHpMaxAndFill` (`AlundraPlayerManager.cs:1004`, the comment calls it a "provisional seam") writes stats directly. The mirror would undo it.
   - [F] The binary version (`0x800333AC`) does `hero.HpMax += 1; hero.Hp = hero.HpMax`, with no cap there. The cap of 50 is only in `SetPlayerHpMax`. Every later heal must write the entity too. The heal at `0x80032E50` adds `max(5, HpMax/4)` to `hero+0x14`.

3. **The driver `0x8003634C` and `UpdatePlayerCarriedEntity(2)` cannot be ported "for real" in c2.**
   - [F] `[0x80127164]` is written non-zero only at `0x80034A90`, in the item-use start `0x8003499C`, reached only through `TryUseItem` (`0x8002ED90`). Hero `+0x2C` is non-zero only after lifting.
   - [F] Until E20.h and E20.g, `0x8003634C` returns 1 and writes nothing. `0x8002F884` is `beqz hero+0x2C -> return` (`0x8002F89C`).
   - Recommendation: keep both as named stubs, call them at the exact sites of §2 and §3, and move the "for real" of D-E20-14 to E20.h and E20.g. The call order matters only because the driver runs before the new animation is stored.

4. **Death cut (D-E20-5).** The narrowest faithful cut is to skip the single test at `0x80031CA8` (`beqz hero.Hp -> 0x800327A4`) and continue at `0x80031CB0`. Details in §2.5.

5. **The blink is draw-list membership, not a flag.** Details in §4. The hero is already hidden on the hurt tick itself, and 30 of the 120 ticks are hidden.

6. **Hurt in water (`0x3A`) needs a decision.** [D] The DLL has no slope-4 handler. Details in §3.3.

7. **`controlFlags == 0x20`.**
   - [F] Bit `0x20` is set by `ori ...,0x20` only at `0x80062740` and `0x80075264`, both native sprite handlers (E14). It is cleared at `0x800621DC`, `0x80062330` and `0x80074DA8`.
   - [U] A register-passed mask elsewhere cannot be excluded by an immediate scan.
   - So the path is unreachable from the DLL today. Port it as six lines plus a unit test that sets exactly `0x20`.

8. **Pins that will move.** `AlundraHitPassTests.cs:328` pins the hero touched at ticks 1, 26, 51 and 76. With c2, `+0x1CC = 0x78` gives touches at ticks 1 and 121 (emulated, §6). Any arc where the hero ends in contact with an enemy changes trajectory. This includes the c1b-2 lever arcs that start near Beannoïdes (115, 116, 117 and 44).

9. **Decompilation naming traps, with the binary winning.**
   - `FindWarpFacingDirection` is `0x80031A68` (the damage entry).
   - `AnimateWarpEffect` is `0x800350C0`, the cancel of the item in use. The DLL header calls it a "pure visual-effect call", which is wrong.
   - `MaybeStartWarpAnimation` is `0x8003634C`, the item-use driver.
   - `g_playerWarpTimer` is `[0x80126FFC]`, the charge counter.
   - `g_playerEffectTransitionCooldown` is `[0x80127110]`, the pending weapon pointer.
   - `CheckAndExecuteWarp` is `0x8002F120`.
   - `TileAttributes & 0x80` is `hero+0x188 & 0x80`, the warp-tile branch at `0x80032768`, which is E7 and outside c2.
   - Decompilation loss: in the death table, case `0x3B` with slope 4 and `+0xAC == 0` makes the decompilation write `0x39`. The binary leaves the state unchanged (`0x800328CC`).

10. **Equipment immunity and `0xC0` do not stop the hurt reaction.** [F] The hit pass sets `TouchingEntity` unless `(r & 0xC0) == 0x80` on the hero's own `+0x1C4` sheet (`0x800395D4-0x800395EC`). That sheet is record 0, whose responses are all 0, so the hero is never immune there. `0x80031A68` then returns a direction whatever `ComputeNewHp` did. A combined response of `0x80` or `0xC0` costs no HP but still gives the hurt animation, `+0x1CC = 0x78` and the knockback.

11. **Armour-less hero.** [F] If no armour is owned, the sheet is copied from `hero.+0x1C4` (record 0, all responses 0). Every hit then costs exactly 1 HP, the minimum.

## 1. MovePlayer around the damage entry (`0x80031B50`)

[F] Single caller: the events pass at `0x800386EC`, first call of the pass, before every other entity. It runs once per tick when `[0x800DC4B8] & 0x48 == 0`. `0x80031A68` has exactly two callers, both in MovePlayer: `0x80031C14` and `0x80031C44`.

```
0x80031B70  [0x80127108]=0 ; [0x8012710C]=weapon sheet index      (E20.b, unrelated)
0x80031B98  s2 = hero.+0x18C            // slope, read BEFORE 0x8002F120 and used for the 0x3A choice
0x80031B9C  0x8002F120()                // CheckAndExecuteWarp (E7)
A: if hero.+0x20 != 0:  [0x80126FFC]=0 ; [0x80127110]=0 ; goto END
B: if [0x800DC4B8] & 0x34:                                   // locked
       0x8002FB14(1) ; [0x80126FFC]=0 ; 0x8002F884(1) ; [0x80127110]=0 ; 0x800350C0()
       if [0x800DC4B8] != 0x20 goto END                      // whole-word compare: bit 0x80 present -> no damage entry
       d = 0x80031A68() ; if d != -1: hero.+0x1CC = 0x78    // no animation, no direction, no death test
       goto END
C: hero.Flags(+0x6C) |= 0x100 ; 0x8002FB14(0)                // effects run BEFORE the entry, on the old animation
   d = 0x80031A68()
   if d != -1:                                               // 0x80031C58
       [0x80126FFC]=0 ; [0x80127110]=0 ; 0x8002F884(2) ; 0x8003634C()
       hero.+0x88 = (s2 == 4) ? 0x3A : 0x31 ; hero.+0x8C = d ; hero.+0x1CC = 0x78 ; goto END
   if hero.+0x14 == 0 goto 0x800327A4                        // 0x80031CA8, death branch (E18)
   if hero.+0x188 & 0x80 goto 0x80032768                     // warp tile (E7)
   0x8003634C()                                              // 0x80031CC4, result ignored
   ... pad -> direction, slope dispatch (0x80022D1C), prologue calls, state dispatch ...
END 0x80032914: 0x800307E8() ; SetPlayerHpMax(hero.+0x18) ; SetPlayerHp(hero.+0x14)
```

Notes:
- [F] The hurt path precedes every state. An attack swing, a jump, a ladder or anything else is replaced.
- [F] The death test runs only on a tick without damage, so death starts one tick after the killing blow.
- [F] `0x8002F120` and the `+0x188` test are not part of c2.

## 2. The damage entry `0x80031A68`

### 2.1 Rule (emulated against the real code: 3,000 random cases, 0 mismatches)

```
int DamageEntry(hero):                  // -1 = nothing, else a direction 0..31 (a value of 0 is a valid direction)
  t = hero.TouchingEntity (+0x224)
  if t != null:
      ComputeNewHp(hero)                // 0x8003A374 ; return value ignored
      return GetDirection(hero.PosX - t.PosX, hero.PosY - t.PosY)    // 0x8002E814, raw 16.16 differences of +0x114/+0x118
  if hero.+0x1CC != 0         return -1     // 0x80031ABC
  if [0x80127000] >= 3        return -1     // boots level 3 (0x80031AD0)
  n = (hero.+0x180 & 0x180) >> 7 ; if n == 0 return -1
  cost = halfword[0x80028B8C + 2n]          // table 00 00 | 01 00 | 02 00 | 03 00 -> {0,1,2,3}
  hero.Hp = (cost < hero.Hp) ? hero.Hp - cost : 0     // armour is bypassed
  return (hero.TargetDirection(+0x8C) + 0x10) & 0x1F
```

- [F] With a toucher, neither `+0x1CC` nor the boots are tested. Invulnerability then relies only on the hit pass skipping a target whose `+0x1CC != 0`. See §4 for the other skips in that pass.
- [F] `GetDirection` is a 16x16 arctangent table (`0x80022488`) with quadrant folding. 0 is down (+Y), `8` is left, `0x10` is up and `0x18` is right.
- [D] The DLL has `ScriptHelper.GetDirectionToTarget`, used by `AlundraEventProgramRunner.cs:3376`. It is not oracle-checked against `0x8002E814` in what I read. The `(dx,dy)` grid check is an oracle task for c2.
- [F] The toucher's `CurrentAttack` (`toucher+0x1C8`) is read at call time. MovePlayer is the first thing in tick N+1, so it still holds the attack the hit pass tested at tick N.
- [F] `TouchingEntity` is cleared by the counters pass of the same tick, after MovePlayer. A hit that arrives on a tick where MovePlayer exits early (frozen, or locked and not exactly `0x20`) is lost.

### 2.2 `ComputeNewHp` and `ResolveBalanceTarget` for the hero (already in `AlundraDamage`; re-read)

- [F] Sheet is `0x80127008`. `r = sheet[(attr & 0xF) + 2]`.
- [F] `attr == 0`, a null attack or a null sheet leaves the HP.
- [F] `r & 0xC0 == 0x40` sets HP to 0.
- [F] `r & 0xC0` equal to `0x80` or `0xC0` leaves the HP.
- [F] Otherwise `dmg = max(1, (power * r) >> 4)`, and `hp = (dmg < hp) ? hp - dmg : 0`.
- [F] The equipment term applies only for `attr & 0x80`. No hurting attack has that bit (D3).
- Worked values:
  - Cloth armour (r = 16): Beannoïd power 1 gives 1; Orc hammer power 4 gives 4.
  - Leather (r = 12): Orc 3; Beannoïd `(1*12)>>4 = 0` so the minimum 1.
  - Ancient armour (r = 8): Orc 2.
  - Silver armour (r = 4): Orc 1.

### 2.3 Sound, effects, knockback

- [F] `0x80031A68` itself makes none of these.
- [F] Sound 23 is the `Sfx` byte of animations `0x31` and `0x3A`. It plays at animation load (`0x80038BC8`, `jal 0x800490FC`), which is the generic change-of-animation sound.
- [F] The 3 sparks (effect 9) are in the hit pass (E20.c1a). The effects 4 and 5 are E20.f.
- [F] There is no explicit velocity. The knockback is the animation's `Speed` (256) along `TargetDirection`, then physics decay. Emulated at rest, per-tick dx in raw 16.16:
  - T..T+3: -196608 (= 256*768).
  - T+4 to T+14: -184320, then decreasing by 12288 each tick to -61440.
  - T+15: -30720, then 0.
  - Total -2,138,112 raw (-32.625 px).
- [I] This is the D1 physics regime. The physics pass was not re-decoded here.

### 2.4 Hurt hero outputs (the full write set)

`hero.Hp`, `+0x88`, `+0x8C`, `+0x1CC`, `[0x80126FFC]=0`, `[0x80127110]=0`. Through the stubs: carried-entity drop (no-op while `+0x2C == 0`) and the driver (no-op while `[0x80127164] == 0`). Through the END: the sheet, boots, regen and the stats mirror.

### 2.5 HP 0 and the E18 boundary

- [F] The entry is `0x80031CA8 -> 0x800327A4`. It runs `[0x80126FFC]=0 ; [0x80127110]=0 ; 0x8002F884(2) ; 0x800350C0()`. It then selects by `hero.+0x88` through the table `0x80022FBC` (index = animation - 0x1C):

  | Current animation | Handler | Effect |
  |---|---|---|
  | `0x1C` | `0x80032898` | Slope 4 gives `0x3C`. Otherwise it waits for `+0xAC != 0`, then `0x39`. |
  | `0x31`, `0x39` | `0x80032870` | Slope 4 gives `0x3C`. Otherwise it does nothing. |
  | `0x3A`, `0x3C` | `0x80032884` | Off slope 4 gives `0x39`. |
  | `0x3B` | `0x800328CC` | Off slope 4 gives `0x39`. On slope 4 it waits for `+0xAC`, then `0x3C`. |
  | `0x4E`, `0x4F` | `0x800327F0` | See below. |
  | Anything else | `0x800328F8` | Slope 4 gives `0x3C`, otherwise `0x39`. |

  The `0x4E/0x4F` handler waits for `+0xAC`. It then checks item `0x27` through `GetNumberOfItem`. If the hero owns it, it uses it through `0x800347D4` and goes to Idle (`0x1D` on slope 4). Otherwise it writes the request words `[0x800DC4C4]=1`, `[0x800DC4C8]=8`, `[0x800DC4CC]=0x1DD`, `[0x800DC4E8]=0`, `[0x800DC4D0]=0` and sets `controlFlags |= 4` (`0x80032838-0x8003286C`).
- Emulated killing blow (Hp 1, contact):
  - T=2, Hp 0, the hurt plays in full (`0x31` then `0x1C`).
  - Tick 17 (T+15) goes to `0x39`, tick 67 to `0x4E`, and the game-over request appears at tick 76.
  - With Hp 0 at the start: `0x39` at tick 1, `0x4E` at tick 51, request at tick 60.
- **E18 owns:**
  - the test and branch at `0x80031CA8`/`0x800327A4`;
  - table `0x80022FBC`;
  - animations `0x39` (50 ticks), `0x3C` and `0x4E`;
  - handler `0x800327F0`;
  - the potion `0x27` and the `0x1DD` request;
  - Start+Select (`0x8002BBE4-0x8002BC34`: it sets Hp 0 and writes `+0x1CC = 0x78`);
  - opcode `0xBB`.
- **Cut.** Never take `0x80031CA8`. With Hp 0, ComputeNewHp keeps flooring at 0, and the hero keeps control.
  - After a killing blow he returns to Idle at T+15 with Hp 0.
  - Every later hit still gives the hurt, at most once per 120 ticks.
  - Regen is off, because its gate is `Hp != 0`. Heals work, because `0x80032E50` has no such gate.
  - [D] `AlundraSaveGame` accepts `hp 0..hpMax`, so a save at Hp 0 validates (`AlundraSaveGame.cs:410`).
  - [U] Whether the author expects to see 0 hearts or a floor of 1. D-E20-5 reads both ways. Decide it with the author. A floor of 1 would be a deviation applied at the writer, not a cut.

## 3. The hurt states inside MovePlayer

### 3.1 Animation data (from `map_alundra.json`, sprite 0; delays are the low 7 bits of the first byte of the 5-byte frame record)

| Anim | Frames (ticks) | End | Speed | AnimFlags `+0xB4` | Sfx |
|---|---|---|---|---|---|
| `0x31` | 4 x 1 = 4 | chain `0x1C` | 256 | `0x40` | 23 |
| `0x1C` | 1,2,2,2,2,1 = 10 | hold, sets `+0xAC = 1` | 0 | `0x44` | none |
| `0x3A` | 4 x 1 = 4 | chain `0x3B` | 256 | `0x40` | 23 |
| `0x3B` | 4 x 2 = 8 | hold, `+0xAC = 1` | 0 | `0x44` | none |

- [F] `UpdateAnimation` (`0x80038AB4`):
  - A change of animation or direction reloads the sheet, zeroes `+0xA8` and `+0xAC`, loads `+0xB4` from byte `0xD`, and plays the sound.
  - A terminator whose byte 0 is 0 chains to byte 1; if bit `0x80` is set, it holds with `+0xA8 = 0x7FFFFFFF` and `+0xAC = 1`. Byte 0 equal to 1 loops.
  - The chain reloads in the same tick.
- [F] `+0xAC` is the hold flag, set when a hold terminator is reached and cleared on every animation reload. `+0xB0` is the separate chain/loop counter ("AnimCompleteCounter").

### 3.2 General slope handler (slopes 0 to 3, 5 and 7; 6 after its climb prelude `0x80031D20`)

Tables `0x80022D1C` (slope) and `0x80022D3C` (state). All of this was observed in the emulation, with T=2:

- State `0x31` (`0x80032680`): `TryUseItem()` (result ignored), then END. It does nothing else and does not read the D-pad. `+0x8C` is untouched, so the direction stays the knockback direction.
- State `0x1C` (`0x80032558`), shared with `0x3E` (E20.d, port once):
  - `if TryUseItem() == 0: hero.+0x8C = (hero.+0x8C + 0x10) & 0x1F; END` (`0x80032560 -> 0x8003270C`).
  - Otherwise `if +0xAC != 1: END`.
  - Otherwise `+0x8C = (+0x8C + 0x10) & 0x1F`, and `+0x88 = (+0x144 != 0) ? 0 : 1`.
  - The value 1 in the air is the confirmed quirk. Emulated in the air, tick 16 shows state 1 and tick 17 shows `0x2D`.
- States `0x3A` and `0x3B` when the slope is not 4 (`0x80031DB4`): flip `+0x8C` by `0x10`, then `+0x88 = ground ? 0 : 0x2D`, then `TryUseItem`, then END. The hurt then lasts one tick.
- Timeline (T = hurt tick, `+0x1CC` after the counters pass):
  - T: `0x31` loaded, hero hidden, `+0x1CC` reads 0x77.
  - T+1 to T+4: MovePlayer sees `0x31`.
  - T+4: the animation pass chains to `0x1C`.
  - T+5 to T+15: MovePlayer sees `0x1C`.
  - T+14: the animation pass sets `+0xAC = 1`.
  - T+15: MovePlayer exits to Idle with the direction flipped.

### 3.3 Slope-4 handler (`0x8003260C`, table `0x80022E7C`)

- Every tick it runs `[0x80126FFC]=0 ; [0x80127110]=0 ; 0x8002F884(2)` first (`0x8003260C-0x80032620`).
- State `0x31` and state `0x1C` (`0x80032658`): flip `+0x8C`, then `+0x88 = 0x1D`.
- State `0x3A`: `TryUseItem()` only.
- State `0x3B` (`0x800326FC`): if `TryUseItem() == 0`, flip `+0x8C` and END. Otherwise wait for `+0xAC == 1`, then `+0x8C = (+0x8C + 0x10) & 0x1F` and `+0x88 = 0x1D`.
- Emulated water (every cell slope 4, Beannoïd contact): `0x3A` at T, `0x3B` from T+4, `+0xAC = 1` at T+12, `0x1D` with the flipped direction at T+13. [F] The hero did not translate at all during `0x3A` (x constant), although `0x3A` has Speed 256. [U] The cause is in the physics pass `0x800364C8` for slope-4 cells (E20.i and physics). Decide it by decoding that pass.
- **DLL decision.** The DLL has no slope-4 branch and walks on water. [D] Check that `Slope_18c` reads 4 on deep-water cells; I did not run the DLL probe.
  - Faithful entry, which I recommend:
    - port the selection `slope == 4 ? 0x3A : 0x31`;
    - port the `0x3A/0x3B` fix-up above (otherwise the hero stays in `0x3B` forever, held with `+0xAC = 1`);
    - accept a one-tick hurt on water until E20.i adds the slope-4 cases.
  - Alternative: use `0x31` in water until E20.i, with a documented deviation. That gives a 15-tick hurt but then differs from the binary.

### 3.4 Input lock

- [F] While hurt, the pad does not choose a direction.
- [F] The Circle edge still reaches `TryUseItem` (`0x8002ED64`: it calls `0x8003499C` if `controlFlags == 0`). The item-use start (table `0x8002339C`) is not checked here.
- [U] Whether starting an item is refused while hurt is decided in the per-item start function. For beans (`0x80034224`), it is not refused by the animation: it checks only `+0x2C == 0`, slope != 4 and `[0x80127164] == 0`. E20.h decides the rest.

## 4. Invulnerability counter `hero+0x1CC` and the blink

### 4.1 Writers and the countdown

- [F] Writers for the hero: `0x80031C9C` (hurt, 0x78), `0x80031C2C` (0x20 path, 0x78), `0x8002BC2C` (Start+Select, 0x78), and opcode `0x86` at `0x8004002C`, which sets (does not add). Native enemy handlers also write it for themselves.
- [F] The counters pass `0x80038998` runs for slots 0..`[0x80134360]` inclusive, with no status filter, after the events pass in the same tick. In order, per slot: `+0x1C++`; `if +0x1CC != 0: +0x1CC--`; `if +0x1D0 != 0: +0x1D0--`; and it clears `+0x224`, `+0x220` and `+0x12C`.
- [F] The whole pass is skipped when `[0x800DC4B8] & 0x48`. Only `0x800384F4` still runs, so the counter freezes and the draw list is recomputed with the frozen value.

### 4.2 Readers

| Reader | What it does |
|---|---|
| `0x80031ABC` | Hazard branch guard (`+0x1CC != 0` gives -1). |
| `0x8003942C` | Hit pass skips a target with `+0x1CC != 0`. The same gate also skips `+0x1D0 != 0` (checked first), `AnimFlags & 0x40` (`0x8003943C`) and `Flags & mask == 0`. |
| `0x800385F0` | Lists pass `0x800384F4`: draw-list drop. |
| `0x80038EEC/F4` | Shadow pass `0x80038E84`: shadow release. |
| `0x80062690` | A native handler reads the hero's `+0x1CC` and `AnimFlags & 0x40` before acting on the hero (E14). |
| `0x800622C8`, `0x80070400` | Native handlers reading their own entity's `+0x1CC`. |

### 4.3 Blink

- [F] The lists pass builds three lists, per slot 0..`[0x80134360]`:
  - the active list `0x80127B28` (count `0x80127D28`): status 2 or 3 and `+0x20 == 0`;
  - the collidable list `0x80127C28`: `Flags & 0x80`, `AnimFlags & 0x80 == 0`, `+0x28 == 0`;
  - the draw list `0x80134250` (count `0x8013435C`): status 2 or 3 **and** `(+0x1CC & 3) != 3`.
- [F] The draw list is consumed by `0x800399B8`/`0x800397AC` and `0x8003B470`. The shadow pass releases the entity's shadow effect (`+0x1B8`) when the same bit test holds (`0x80038EEC-0x80038F14`).
- [F] This pass runs after the counters pass in the same tick. On the hurt tick `+0x1CC` has already dropped from 0x78 to 0x77, and `0x77 & 3 == 3`, so the hero is hidden on tick T itself.
- Emulated hidden ticks (T=2): 2, 6, 10, ..., 118. That is T+4j for j = 0..29, 30 ticks out of 120, with `+0x1CC` values 119, 115, ..., 3.
- Rule: set the hero's `entity.IsVisible` (the DLL already uses it, `AlundraFrameSyncPasses.cs:104`) once per logic tick, in the lists step after the counters pass: `visible = status in {2,3} && (DamagedTickCounter & 3) != 3`. Apply the same condition to the shadow.
- When the next hit is possible:
  - The hit pass of tick T+119 sees `+0x1CC = 0` and may set `TouchingEntity`; the hurt follows at T+120.
  - Emulated: touches at ticks 1 and 121, second hurt at 122.
  - Hazard hurts repeat at T+120, T+240 (ticks 2, 122, 242).

## 5. `UpdateItemEffectState` (`0x800307E8`) and the item-use driver

### 5.1 Step 1: boots level

[F] `0x800307EC-0x80030890`: `[0x80127000] = 3` if `GetNumberOfItem(0x1C) != 0`, else 2 if `0x1B` is owned, else 1 if `0x1A` is owned, else 0. Item `0x19` does not count. Init zeroes it at `0x80031730`.

| Reader | Test |
|---|---|
| `0x80031AD0` | `< 3` allows the hazard hurt. |
| `0x800368BC` | `> 0` (physics, cell flag `0x10`). |
| `0x800369E0` | `> 0` (physics, cell flag `0x08`). |
| `0x800374FC` | `< 2`. |

- [D] The DLL's `AlundraScriptedMotion.BootsLevel` recomputes it on demand. The binary caches it at the END of the previous tick, a one-tick lag that is immaterial.
- Emulated: item `0x1C` means no hazard hurt; items `0x1B` and `0x1A` do not prevent it.

### 5.2 Steps 2 to 5: equipment records (done in c1b-1)

Records at `0x80146E00 + 8i` (pointer, item+0x1E).

### 5.3 Step 6: copy and response combination (fuzzed against the real code)

1. Copy 255 bytes (15 chunks of 16 plus 15) to `0x80127008`. The source is record 0, or `[0x80127EF4]` (= `hero+0x1C4`) when record 0 is null. Only sheet bytes 2 to 13 are ever read afterwards, by `ComputeNewHp` and by this rewrite.
2. For `i = 1..11`, `b = sheet[2+i]` (just copied), `A = weapon-record[2+i]` and `B = item-record[2+i]`. `A` and `B` default to `0x10` when the record is null.

```
fb=b&0xC0, fA=A&0xC0, fB=B&0xC0
if fb==0x80 or fA==0x80 or fB==0x80 -> 0x80
v = b
if fb != 0:
    if fA != 0 and fB != 0 -> 0x40 (stored as is)
    v = 0x10
if fA == 0: v += A - 0x10
if fB == 0: v += B - 0x10
result = v>=0x20 ? 0x1F : v<0 ? 0 : v
```

New-game kit: cloth `0x10` x 11, dagger `0x40` x 11, no item gives `0x10` x 11.

### 5.4 Step 7: regeneration (emulated, period 200 gives +1 at ticks 201 and 402)

Gates: `[0x800DC4B8] & 0x7C == 0`, `hero.+0x20 == 0`, `lhu [0x80126F28] == 0` (no button held, including the D-pad), `Hp != 0` and `Hp < HpMax`. If any gate fails, the three counters `[0x80126FE8/EC/F0]` are zeroed and Hp is not touched.

```
a = Hp
for i in 0..2:
    if rec[i] != null and rec[i].byte[2] != 0:
        cnt[i]++ ; if rec[i].byte[2] < cnt[i]: cnt[i] = 0 ; a++        // period = byte[2] + 1 ticks
    else: cnt[i] = 0
Hp = min(HpMax, a)
```

- [F] Byte [2] of an equipment record is the regen period (0 for cloth, dagger and swords). Rings and tonic have 200.
- [F] The code after `0x80030D40` is debug printing only.

### 5.5 The driver `0x8003634C`

- [F] If `[0x80127164] == 0` it returns 1 and writes nothing.
- [F] Otherwise it dispatches by `[0x80127164] - 0x1F` (20 entries, table `0x800235F4`) to the per-item handler, then does `[0x8012715C]++` (saturating at `0x7FFFFFFF`), zeroes `[0x80127164]` if the handler finished, and returns the finished flag.
- [F] Call sites: `0x80031C6C` (hurt path, before the new animation is stored) and `0x80031CC4` (every normal tick, after the HP-0 and warp-tile tests, result ignored). There are no tail calls to it.
- [F] The cancel `0x800350C0` is separate. Its callers are `0x80031BFC` (locked branch), `0x80032758` (default case), `0x80032770` (warp-tile branch), `0x800327B4` (death) and four handlers. The hurt path does not call it.
- [F] The only non-zero setter of `[0x80127164]` is `0x80034A90`; zero setters are `0x80032964` (spawn), `0x800351A4` and `0x800364A8`.

## 6. Where HP lives and how the HUD reads it

- [F] Entity: `hero+0x14` (Hp) and `+0x18` (HpMax), 32-bit.
- [F] PlayerStats is at `*[0x80176318]`: Hp (int16) at +0, HpMax (int16) at +2.
- [F] `SetPlayerHpMax(a)` (`0x8004DC68`): `a >= 0x33` gives `0x32`; `a < 0` gives 0.
- [F] `SetPlayerHp(a)` (`0x8004DD30`): `a > stats.HpMax` gives HpMax; `a < 0` gives 0.
- [F] The END calls them in that order with `hero.HpMax` then `hero.Hp`.
- [F] The HUD reads stats through the getters `0x8004DD18` (Hp) and `0x8004DC50` (HpMax) at `0x8004C210/18/34`. It animates a displayed counter that catches up (`[0x800B8284...]`) and plays sound 9 on increments and sound 8 on others.
- [D] The DLL HUD already ports that catch-up from `_gameState.PlayerStats.Hp` (`AlundraHudDirector.cs:544-586`). c2 needs only the mirror.
- [F] Save-slot summary builder `0x80030FC8-0x8003132C` reads `hero.HpMax` (`0x80031198`). The DLL has only stats (F7).
- [D] `AlundraPlayerManager.SetPlayerHp/SetPlayerHpMax` match the binary clamps.

## 7. Globals read or written by the hurt slice

| Address | Meaning | Read | Written |
|---|---|---|---|
| `0x800DC4B8` | Control flags (`&0x34` locked, `&0x48` pause, `&0x7C` regen gate) | MovePlayer, END, tick | game-over request sets bit 4 |
| `0x80126F28` | Pad hold word | regen gate | pad routine |
| `0x80126FE8/EC/F0` | Regen counters | step 7 | step 7 |
| `0x80126FFC` | Charge counter ("warp timer" in the decompilation) | | zeroed on frozen, locked, hurt, death, slope 4 |
| `0x80127000` | Boots level | hazard branch, physics | step 1, spawn |
| `0x80127008` (255 B) | Hero defence sheet | `ComputeNewHp` | step 6 |
| `0x80127110` | Pending weapon pointer | | zeroed on frozen, locked, hurt, death, slope 4 |
| `0x80127164`, `0x8012715C` | Item in use, driver tick counter | driver | driver, cancel |
| `0x80146DF8` | Latched hero animation | `ResolveBalanceTarget` (only for `attr & 0x80`) | hit pass |
| `0x80146E00-14` | Equipment records | step 6, 7 | steps 2 to 5 |
| `0x80176318 -> stats` | Hp/HpMax mirror | getters, HUD | END |
| `0x800C5F7C` | Item property table (category = `byte[6] & 0x7F`) | steps 2 to 5 | |
| `0x80028B8C` | Hazard cost table | | |
| `0x800DC058` | Debug flag | | |
| Hero fields | `+0x14`, `+0x18`, `+0x20`, `+0x2C`, `+0x6C`, `+0x88`, `+0x8C`, `+0xAC`, `+0x114/118`, `+0x144`, `+0x180`, `+0x18C`, `+0x1C4`, `+0x1CC`, `+0x224` | | `+0x14`, `+0x88`, `+0x8C`, `+0x1CC` |
| Toucher fields | `+0x114/118` and `+0x1C8` (attr and power bytes) | | |

[F] The hazard word `+0x180` is the OR, over the four footprint corners, of the tile word at the hero's level. A corner contributes only if its height is exactly at the hero's feet (`0x80038120-0x80038190`). It is written in the physics pass `0x80038364`, via `0x80038064`, at the end of the tick. The next tick's MovePlayer reads it, so at tick 1 the hazard value is still 0 and the first hazard hurt comes at tick 2.

## 8. Oracle pins for the port (all emulated on the real binary)

- Contact: hero at rest 12 px west of a Beannoïd. `TouchingEntity` set at the end of tick 1. T=2: Hp 10 to 9, state `0x31`, direction 8, `+0x1CC` 119, hidden.
  - Ticks 2 to 5 are `0x31` and ticks 6 to 16 are `0x1C`.
  - `+0xAC = 1` at tick 16.
  - Tick 17 is Idle with direction 24.
- Dx per tick as in §2.3.
- Second touch at tick 121, second hurt at 122, Hp 9 to 8.
- Hazard everywhere on the map: n=1/2/3 gives Hp 9, 8, 7 on the first hurt. Repeats at 122 and 242: n=1 gives 8, 7; n=2 gives 6, 4; n=3 gives 4, 1. Direction is `(24 + 0x10) & 0x1F = 8`.
- Water: `0x3A` at tick 2, `0x3B` at 6, `+0xAC` at 14, `0x1D` at 15, direction flipped, no x translation.
- In the air: `0x31` at tick 1, `0x1C` from 5, `+0xAC` at 15, state 1 at 16, `0x2D` at 17.
- Forced `0x20`: Hp 10 to 9 at tick 2, `+0x1CC` 119, animation and direction unchanged, blink on.

## 9. Unknowns to decide

- **U1** What the author expects at Hp 0: 0 hearts or a floor of 1 (D-E20-5). The author decides.
- **U2** `Slope_18c == 4` on deep-water cells in the DLL probe. Decide by reading a water map cell through the DLL.
- **U3** Why the emulated hero does not translate during `0x3A`. Decode `0x800364C8` for slope 4 (E20.i).
- **U4** Whether item start is refused while hurt (E20.h).
- **U5** Which sprites own `0x80062740` and `0x80075264`, the setters of control bit `0x20`. Map the handler addresses to sprite indices through the tables at `0x800A8F4C`.
- **U6** The value of the sheet `0x80127008` on the very first tick after boot. It is global BSS, so zeros. It affects only a hit on that tick.
- **U7** HpMax above 50 on the entity: `0x800333AC` is uncapped while the mirror clamps to 50. An edge case that stays an edge case.
