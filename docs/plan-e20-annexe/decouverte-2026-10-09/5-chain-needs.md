# Survey 5: what the game progression needs

> Read-only discovery of 2026-10-09 for E20 (docs/plan-e20-heros.md, section 0.2). Raw output of one workflow
> agent, kept as evidence. Where this file and its binary check disagree, the check and the plan section 0.2 win.

# First progression needs for attack and sprint (surface: game progression)

## 1. Answer in brief

- **FACT:** Nothing from 389 to 164 (day 1) requires attack or sprint. No script tests them. The terrain legs I checked need only jumps.
- **FACT:** The first mandatory attack is Tarn's Manor 115, the entrance hall, on day 2. Four Muruta must die before the exits open. This is the first thing the author hits in a normal playthrough.
- **FACT:** No chain script or terrain leg I checked requires sprint (see §6 for what was not checked).
- **INFERENCE:** The first mandatory lift or throw is the bomb in map 362 (day 4). It comes first on the F9 test-save route, which skips 115–117 and 44 (§3).
- **INFERENCE:** The first obstacle that needs a lift or a climb in the original is a crate barricade in map 392, the ship at night. The port currently bypasses it (§4).

Conventions:
- `@n` is the index in the `Codes` array of `alundra-project/Maps/<zone>/<map>-<id>/events/*.events.json`.
- I read programs with `research/census/waits-0x24/m.py`, read-only.
- Tile = record `XPos/2`, `YPos/2`.
- Binary addresses refer to `ALUN_CD.EXE` (France).

## 2. What the hero owns (FACT)

- The new-game loop (`0x80031868`–`0x80031890`) calls `AddOneItemIfUnlocked` for each of 0x62 table entries whose byte 6 has bit `0x80`. It is followed by `SetPlayerWeaponId(1)` at `0x80031894`.
- In `Data/item-drop-properties.json` that bit is set only on items 1, 17 and 25: Épée de base, Veste en tissu, Bottes normales.
- HP is 10/10. The F9 presets are "new game + flags" (`docs/test-saves.md:47-50`), so they carry the same kit.
- Chain scripts and chests give no weapon. I found no `0xC0`. Chest contents are 36, 41, 61, 70, 72, 79, 80, 83 and 85, plus the Livre d'Elna (88) and the shop items.
- Everything below is therefore met with the base sword only: no hammer, bow, magic or bomb item.

## 3. Ordered first needs

| # | Map | Event and condition | Evidence | Belongs to |
|---|---|---|---|---|
| 0a | 389–164 | Optional but visible: crates and jars (`Flags&0x600`, native C23 `0x8006B848`, E2 `0x8007ED30`). They are currently traversable by decision. | plan:123, :178, :9547; `Data/sprite-records.json` | lift/throw, E14 |
| 0b | 162, 169, 478 | Animals Poulet C91 `0x800637D8`, Corbeau C92 `0x80063CB4`, Toutou C93 `0x80063DB4`. Their sprite D (hit) slot is 0, so a sword hit does nothing. | sprite-records, binary table `0x800C4F34` | none |
| 0c | 392, 389, 478, 135 | The hero's own Sprint animation (id 3) is scripted: `0x5B [129,3,67]` @64. Pickup/Hold/Throw appear only as scripted animations (389 @1544, 478 @329/345, 135 @2129/2242). None is player-controlled. | event programs; `PlayerAnimation.cs` | none |
| 1 | 392 | Crate barricade. The only passage between the two halves is x=27–28, rows 18–20. Crates rec4–11 stand at x=26, rows 17–20, two high, directly on it. The hero starts east at tile (30,13). The exit portal is west at (22,23). | cell grid and records of 392; plan:9547-9551 ("3 px" window) | lift/throw or climb: **unassigned** |
| 2 | 14 | Optional hostile field. Beannoïde ×7 (C1 `0x80065ED4`, D1 `0x8007D9A4`), Guêpe ×3 (C18 `0x8006A564`, D11 `0x8007DEE8`), Poisson ×2 (C19 `0x8006A974`, D6 `0x8007DCD8`). B[5] `0x2C [7],[8],[9]` @252-262 only drives an ambient sound loop. Secrets are bomb-only (see §4). | map 14 @252-262, @494, @544 | E14, hero damage, E18 |
| 3 | 15 | The route to portal 0 climbs 16-px ledges: (38,21)→(38,22)→(37,22)→(35,22). A plain jump suffices (apex 27.5 px, plan UH-1). B[1] @72 is a scripted Murruta scene (rec0–2, `0x2C [2]` @109). The Muruta destroy themselves (@712/@777/@890). Orc (masse) ×3 (C16 `0x80069F44`, D12 `0x8007DF4C`) are optional at tiles (7,30), (7,36), (21,36). | map 15; plan:9538-9546; author reached it (recette R5) | already working |
| **4** | **115 hall** | **First mandatory attack.** B[2] @172 fires when the hero reaches (10,55), at arrival from 15. `0x55` ×4 @344-359 clear GroundProperty bit `0x80` on (10,44), (11,44), (5,42), (16,42), which are the tiles of portals 4, 2, 6. `0x2D` activates rec24–27 (4 Muruta) @403-407. `0x2C` 24..27 waits for all four to be destroyed @414-431. G569 is set @434, and `0x54` ×3 restores the portals @493-503. | map 115; binary `0x2C` at `0x8003DC84` | hero attack and hit core; Muruta C10 `0x80068154`, D9 `0x8007DE04`, E1 `0x8007ED10` = E14; hero death = E18 |
| 5 | 115 NW room | Lever rec2 at tile (2,7). D[1] @1476 (touched) sets T2. C[1] @1200 sets T1. B[1] @128-166 waits T1 @135, then animates `0x85` door frames onto (4,6), the tiles of portal 12 (4..5,6) to 116. | map 115 | hero attack only; the lever is script-only, no native |
| 6 | 116 | (a) Grate rec6 (tile (22,21)). A[1] @128 and C[1] @312-325 open it only when Beannoïde rec0 and rec1 (tiles (16,18) and (20,18)) no longer exist. (b) B[1] @152-293 extends a bridge only while T1 and T2 are both on. Levers rec39/40 toggle them (D[5] @1076, D[6] @1084, `0x32`). Plates rec7/8 hold them 60 ticks by riding. A walk from arrival (3,20) reaches neither portal 0 nor 1. | map 116 | hero attack (levers), E14 Beannoïde; riding already exists |
| 7 | 117 | Bascule rec0 at (21,11): D[1] @1184 rotates T1→T4. Platforms are rewritten by `0x85` @444-703. Iron balls are ridden (T240..T270). Key chest rec19 holds item 61 (door in 115 B[6] @982). Livre rec20: F[2] @1252 sets T1000, then C[20] @1032-1074 sets G108/G203 and `[162]=176` @1068. A walk from (13,34) reaches none of the key chest, portal 8 or portal 0. | map 117 | hero attack (hit the bascule), E14 (Beannoïde ×5, iron-ball native E18 `0x8007F658`) |
| 8 | 44 (day 3 dream) | B[3] @328: `0x2D` on rec22–29 (8 Beannoïde), then `0x2C` 22..29 @465-502 waits for all of them. Six levers rec7–12 share D[8] @2320. B[5] @744-985 is the boss: `0x8B` spawns rec35 Slime géant @866 (C102 `0x80065B0C`, D46 `0x8007EC9C`). `0x2C` 35/36/37 @895-907 waits for the slimes and the Beannoïde, then G1635 and ChangeMap 179 @985. | map 44. **Skipped by preset `day3-after-dream`** (map 179, tile (17,7)) | hero attack and E14; boss = E14 |
| 9 | 362 (day 4 dream) | B[4] @168 waits T6. T6 is set only by lid rec0, D[1] @728: `0x87 [128,3]` = touched by an attack of nibble table[3] = 4 (**Bomb**). Olen's script spawns the bomb rec5 (C62 `0x8007A978`, D30 `0x8007E79C`, E12 `0x8007F27C`) @569. Then 4 Muruta spawn (`0x8A` @182-212) and T7 is set @228. B[2] @64-140 ends the dream 750 ticks later (5×120+150); it does not wait for kills. B[7] @312-341 only spawns chest rec10 (item 79). | map 362; preset `day4-meeting` lands at 185 | `0x87` port; how the bomb reaches the lid is UNKNOWN (lift/throw suspected); Muruta = E14 |

## 4. Mechanisms the gates rely on (verified in the binary)

- **Kill-wait.**
  - `0x2C` (`0x8003DC84`): calls the search `0x8003C954` and stores `Result = (count == 0)`.
  - Raw ids are record ids. The gate opens only when the entity is destroyed.
  - The Muruta/Beannoïde E slot is native 1 (`0x8007ED10`, destroy). The plan says the DLL already ports E0/E1 (plan:126).
- **Hit pipeline.**
  - In `UpdateBalanceRecords` (`0x80039300`), `sw $s1,0x224($s0)` at `0x80039668` writes the victim's TouchingEntity. It reads the attacker's FrameCollision (`+0x1D4`) and CurrentAttack (`+0x1C8`).
  - `0x8003966C` sets the cooldown `+0x1D0 = 0x19`. `0x80039674`–`0x80039680` does attacker `+0x220` (HitCounter) `+= 1`.
  - Event selection (`0x80038840`–`0x8003884C`): TouchingEntity ≠ 0 selects program type 3 (D, touch). So a lever's D script runs only after a hit.
  - `ComputeNewHp` (`0x8003A374`) reads `+0x224` and `+0x1C8`. The hero's hit reaction at `0x80031A68` reads the player's `+0x224`.
- **Attack-type opcodes `0x87` and `0x95`.**
  - Both index the byte table at `0x80098FA4` = [1,2,3,4,5,5,6,7]. `0x87` uses operand v[2] (`0x80040060`). `0x95` uses v[9] (`0x80040824`).
  - Nibble names at `0x80098F64`: 1 Sword, 2 Hammer, 3 Arrow, 4 Bomb, 5 Tackle, 6 Fire, 7 Ice.
  - Chain sites are map 10: `0x95` @6418 (idx 3, Bomb), `0x87` @6688 and @6704 (idx 1, Hammer). Map 14: @494 (Bomb) and @544 (Bomb). Map 15: @902 (Bomb). Map 362: @728 (Bomb).
  - No chain site is sword-specific. The sword unlocks only levers and kill-waits. Only 362 @728 is mandatory.
- **DLL state.**
  - The consumer exists: `AlundraEntityScriptProxy.cs:1517-1545` selects E on HitCounter and D on TouchingEntity.
  - The producer does not. `CurrentAttack` and `FrameCollision` are commented out (`:400`, `:403`).
  - HitCounter "stays 0 until E14 writes it" (`AlundraEventProgramRunner.cs:747`).
  - `PlayerTryAttack`, `PlayerTryAction`, `TryUseItem` and the Triangle run are no-ops (`AlundraPlayerManager.cs:423`).
  - `0x87` and `0x95` are still skipped by size (absent from the dispatch list). The plan leaves them to E14 (plan:6628).
  - **INFERENCE:** the data is exported. The hero `.anim2d` files carry `collision_keyframes` (anim17: 4 keyframes, anim25: 51). `Data/balance.json` holds the hero's attacks. I did not confirm this maps onto the original frame-collision records.

## 5. Decompilation vs binary

- **Checked, no divergence found:**
  - the new-game loop and `SetPlayerWeaponId(1)`;
  - nibble table `0x80098FA4`;
  - `0x87` at `0x80040048`;
  - `0x95` table indexing at `0x800407C0`;
  - the TouchingEntity/HitCounter write at `0x80039668`;
  - event-type selection;
  - `0x2C`;
  - handler pointers C 1/10/16/18/19/23/62/91/92/93/102, D 1/6/9/11/12/30/46, E 1/2/12/14/18, all equal to the decompilation registry.
- **Name vs effect (not a binary divergence):** the opcode table calls `0x54` "Set walkable" and `0x55` "Set unwalkable". The binary shows `0x54` ORs the operand bits (`0x8003ED4C`) and `0x55` ANDs their complement (`0x8003EDE8`/`0x8003EDEC`).
- **Not checked in the binary:**
  - handler bodies (HitCommon, C10, C1);
  - the lever balance responses;
  - the claim that cleared bit `0x80` disables portals. That claim is **INFERENCE** from portal-tile coincidence and dialogs 133/134 (B[5] @905/@891, read as `M115_S005/S006`, "fermé à double tour").

## 6. UNKNOWN

- Whether the original passes the 392 barricade by lifting, climbing the 32-px stack, or both.
- The route geometry inside 116, 117, 44 and 362. I analysed only the 10→14→15 legs; the rest was read from scripts.
- Whether sprint timing matters on the 117 iron balls and platforms.
- How the bomb reaches the lid in 362.
- Whether a sword hit breaks an unlifted crate. C23 sets DeactivateOnHit only after a throw.

## 7. Open questions for the author

1. Is lift/throw/grab (`PlayerTryAction`, `0x8002EDBC`) part of the new hero task? The decision text names only attack and sprint, and plan:3297 had given grab to E14. 362 needs it (suspected) and 392 may need it.
2. Should the first Muruta and Beannoïde AI (C10/D9/E1, C1/D1) belong to the attack task so the 115 hall is testable, or to the first E14 slice?
3. Should the hero take damage before E18 exists? It has 10 HP and four Muruta in the hall, and no chain script handles death. `0xBB` (retry or title) appears only in maps 477 and 347.
4. Do you want a test-save preset after the 115 hall (flags G262, G569, G1650) so the lever and the 116 gates can be tried without Muruta AI?

## 8. Suggested slice boundaries

1. **Hit core plus hero attack.**
   - Port `UpdateBalanceRecords` and `ComputeNewHp`, `PlayerTryAttack` (`0x8002EAF4`), the hero hit reaction, and frame-collision and current-attack loading from the existing export.
   - Port `0x87` and `0x95`.
   - **Acceptance:** the 115 NW lever, the 116 levers, the 117 bascule, and the 44 levers work. All are script-only, so no E14 is needed.
2. **First E14 enemies, in need order.**
   - Muruta and Beannoïde (115, 116, 117, 44, 362).
   - Slime géant for the 44 boss.
   - Guêpe, Poisson and Orc, which are optional and sit in 14 and 15.
3. **Lift/throw (`PlayerTryAction`) and the bomb.** Needed for 362, and for the 392 barricade (possibly). Unassigned.
4. **Sprint** (Hold&0xE0 → state 4 at `0x800320FC`; PrepareSprint 0x29, SprintDash 0x04, StopSprint 0x2A). Nothing on the chain requires it. Schedule it after slice 1.
5. **E18** (death, retry). First lethal contact is the 115 hall, and potentially maps 14 and 15 earlier.
