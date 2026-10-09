# D8 and D9: the stick snap rule and the open owners

> Report returned by one agent of the E20.0 workflow of 2026-10-09 (docs/plan-e20-heros.md section 1.2), kept verbatim as evidence.
> Where it disagrees with its check or with the critic, the plan section 0.2 holds the reconciled fact.

# D8 and D9 findings (read-only; binary decoded with capstone from `ALUN_CD.EXE`)

Nothing in the repo was changed. I wrote one scratch script to the session scratchpad before noticing the no-file rule, and deleted it. The untracked `docs/plan-e20-annexe/d1-oracle-moveplayer/` in `git status` is not mine.

## D8 — stick snap for run start and resume

**Input path (FACT)**
- `AlundraPlayerController.cs:76-91` ORs the D-pad and the four `Move*Stick` actions into the same four bits. `ComputePadState` (`:248-275`) keeps only `IsKeyPressed`/`IsKeyJustPressed`.
- Each stick half-axis is an AnalogInput with `Pressed = value >= 0.5` (`PlayerSetupWriter.cs:226,249`, `InputMapping.cs:52-58`). There is no angle logic.
- The stick values already arrive as `ButtonState.Value` (`InputMapping.cs:57`, `InputMappingManager.cs:59`), the signed axis after invert. `ComputePadState` ignores them.
- MonoGame's dead-zone mode is `IndependentAxes` (`GamePad.cs:98`).
- `MovePlayer` uses `ButtonsHold >> 12` (`AlundraPlayerManager.cs:222`) to index the 8-way table (`AnimationTables.DirectionByButtons`).

**Binary (FACT)**
- The 8-way table at `0x80022C6C` is read at `0x80031CD4-0x80031CF8`. An invalid nibble keeps the current direction (`+0x8C`).
- The dash table `0x80022CAC` has only four valid entries: up (nibble 1) gives `0x10`, right (2) gives `0x18`, down (4) gives `0`, left (8) gives `8`. Everything else is −1, diagonals included.
- Start, in `0x29`: Triangle held (`0x80031FA8-0x80031FB0`), `+0xB0 != 0` (`0x80031FBC`), then the lookup (`0x80031FD0-0x80031FE8`). A −1 ends the tick. `TargetDirection` was already written at `0x80031F94` with the 8-way direction, so the hero faces the diagonal during wind-up.
- Resume, in `0x2A`: the same lookup at `0x8003219C-0x800321C0`, once the counter is above 10.
- Early window: while the counter is 10 or less, any direction bit clears the counter (`0x800321C4-0x800321D0`). Resume is then lost.
- Cone, in `0x03`, at `0x800320A0-0x800320E0`: the 8-way direction must equal one of three words in the row `0x80022CEC + facing*12`. The rows are down {1C,0,4}, up {C,10,14}, left {4,8,C}, right {14,18,1C}.
- I decoded the facing hysteresis table `0x800237F4`, read at `0x80038AF8`. Each cone row is exactly the three 45° sectors that keep that facing. So the cone already accepts the two diagonals next to the run direction, and an invalid nibble stays inside it.
- Facing index is 0 down, 1 up, 2 left, 3 right (INFERENCE, from those rows). Whether the DLL's `AnimationDirection` uses the same order is UNKNOWN.

**Consequence.** Only two sites reject a stick near a diagonal: the start lookup and the resume lookup. The cone, the exits and walking need no change.

**Proposed rule**
1. It applies only to the two dash lookups. Everything else keeps using the ORed bits.
2. Let D be the keyboard/D-pad direction bits and S the stick bits (the 0.5 gate is unchanged). Let x = `Value(MoveRightStick)` and y = `Value(MoveUpStick)`.
3. The dash nibble is the ORed nibble, unchanged, unless D is empty and S has one horizontal and one vertical bit. In that case:
   - If |x| − |y| > ε, take the horizontal bit.
   - If |y| − |x| > ε, take the vertical bit.
   - Otherwise (the 45° band), take the axis of the hero's current facing. This is the binary's own diagonal rule: keep the axis you face.
4. Proposed ε = 1/16, about ±2.5° at full push. This is a tuning choice, not a fact.
5. Carrier: add an optional override nibble to `AlundraPadState` and `AlundraTickPad`, where 0 means "use `ButtonsHold`". Existing tests that build a pad by object initializer then keep the original behaviour.

**Edge cases**

| Case | Result |
|---|---|
| Stick below 0.5 on every axis | No bits, no snap, unchanged |
| One axis at or above 0.5 | Cardinal already, unchanged |
| Exactly 45° (within ε) | Facing axis |
| D-pad and stick together | The ORed nibble is used strictly. Example: D-pad right plus stick up-right is a diagonal, so no start. The stick never makes up for the D-pad, and walking uses the same union |
| Keyboard Left+Right | Invalid nibble, no snap |
| Resume | Same rule. The early window is unchanged, so stick jitter across 0.5 in the first 10 ticks still kills the resume |

**Risks and unknowns**
- UNKNOWN: MonoGame's thumbstick scaling is not in the repo. A per-axis monotone rescale keeps the order of |x| and |y|. A square clamp would create ties at full push, which would route many diagonal pushes to the facing-axis tie-break.
- Recommended probe in E20.d: log `ThumbSticks.Left` at the eight compass points before pinning ε.
- Bit flicker around 0.5 on a run axis would stop the run. A hysteresis gate of 0.5 engage and 0.35 release is possible, but it changes walking too, so I left it out of D8.

**Alternatives**
- Always snap to the facing axis. Rejected: it ignores the stick angle, which is the point of "arrondi d'angle".
- Circular dead zone through `GamePadManager.GamePadDeadZoneMode`. This changes all movement; not recommended.

**Test:** a 0°–359° sweep at |v| = 1, 0.75 and 0.6, plus the table above.

## D9 — owners

### O-E20-1 Swimming (slope 4)

- **What it needs (FACT):** the slope-4 branch at `0x8003260C`. It clears `0x80126FFC` and `0x80127110`, calls `0x8002F884(2)` (drops the carried object), then switches through `0x80022E7C`:
  - Land states go to `0x1D` after `TryUseItem`.
  - `0x1C` and `0x31` first flip the direction.
  - `0x0F`/`0x1D`: set direction, `TryUseItem`, `CheckEntityInteraction`, `Hold & 0xD0` gives `0x28`, any direction bit gives `0x0F`, otherwise `0x1D`.
  - `0x28`/`0x3A`: `TryUseItem` only.
  - `0x3B`: `TryUseItem`, then the direction flip.
- **Hero animation data (FACT, exported):** speeds `0x0F` 144, `0x28` 176, `0x3A` 256 (invulnerable bit).
- **Who else touches it:**
  - E20.c2 (`0x3A` on a hit, `0x80031C74`).
  - E20.d (the "enter water" recipe).
  - E20.e (dust and splash tables for slopes 1, 2 and 4).
  - E20.g (carry drop).
  - E20.h (`TryUseItem` in water).
- **DLL today (FACT):** slope 4 is computed for the hero only (`AlundraEntityScriptProxy.cs:1769-1796`), and `DestroyOnSlidingSlope` already reads it. The hero currently walks on water.
- **Data:** 54 of 483 maps have at least one cell with ground-property slope 4. Of the 30 chain maps only 416 has any (399 cells); combat maps 14 and 15 also have some. Whether the hero's path crosses them is UNKNOWN.
- **Options:**
  - (a) A slice E20.i after E20.h, about five states plus `0x3A`/`0x3B`.
  - (b) A minimal stub in E20.c2/E20.d: skip the land states on slope 4 and go to `0x1D`.
  - (c) Leave it unported and document the gap.
- **Recommendation:** (a).

### O-E20-1 Mine cart

- **What MovePlayer does (FACT):** the land table sends `0x1A`, `0x1B`, `0x1E` and `0x37` to `0x80032758`, which is `jal 0x800350c0` followed by the end of the tick. The DLL already leaves such animations unchanged.
- **How it is driven (INFERENCE):** I found no immediate store of those ids into a hero animation field in the code. A byte scan of the event programs finds `0x5B` with hero selector `0x81` and animation `0x37` or `0x1B` on maps 61, 62, 63, 64, 66, 68 and 329. False positives are possible. None of these maps is on the chain.
- **Data (FACT):** all four animations carry the invulnerable bit (byte `0x0D` is 64 or 65). The hero balance record has a type-1, power-100 attack for them (`2-attack.md:148`).
- **Needs:** nothing in `MovePlayer`. The invulnerability and the ram attack come from E20.c1. The cart or rail entity itself is E14-style handler work, and which sprite it is stays UNKNOWN.
- **Options:** park it with E14 or a later slice that covers maps 61–68 and 329.
- **Recommendation:** nothing from E20.

### O-E20-3 Loot and the item entity (FACT unless marked)

- **`0x80032968` (row draw):**
  - Loop: while a byte has bit `0x80`, draw `seed = seed*0x7D2B89DD + 0xE06A02E7` (seed at `0x800A8708`) and take the top 4 bits of `seed*16` as the index into row `b & 0x7F`.
  - Rows are 16 bytes at `0x80028E0C`.
  - A byte below `0x62` is returned, otherwise 0.
- **`0x80032A40` (per-entity roll):**
  - Its single caller is `0x80039F38`, just before `SpawnWarpEntity`. That is the spawn/load path, not decoded.
  - It reads the entity record at `+0x44`: a flag id at `+0x10` and a row at `+0x12`, falling back to the sprite header's `+0x1F`.
  - A set flag gives row 0.
  - It stores the result in `+0x3C` and the flag id in `+0x40`.
- **`0x80032A00` (veto):** items `0x26` (Magic Elixir), `0x51` and `0x52` (Small and Large Crystal) are vetoed when the player-stats short at `+6` is 0. That field is MpMax by the decompiled `PlayerStats` order (INFERENCE).
- **`0x80032B90` (spawn from a dying entity):**
  - Calls `SpawnWarpEntity` (`0x80039F58`) for sprite `item + 0x1E`.
  - Sets Z force `0xA0000`, `+0x274 = 1` and a lifetime of 600 ticks or −1 per `0x800C5F7C[item*8+4]`.
  - Plays sound `0x54`.
  - Callers: `DestroyEntity(e,fx)` (`0x8003A59C`, 38 callers) twice, plus enemy handler `0x800670F8`.
- **`0x80032C7C(row)`:** one caller, the tile cut at `0x8003A988`.
- **C255 `0x8007C174`:**
  - A pickup state machine on `+0x274` (1 flying, 2 resting), `+0x278` lifetime and `+0x27C` phase.
  - It builds a message, calls `0x80033DBC` (not decoded) and sets the pickup flag via `0x80032B28`.
  - 87 exported sprite records use it as `ProgramTick`.
- **DLL today:**
  - No spawn primitive and no loot-row export. The 128 × 16 byte table at `0x80028E0C` is not exported.
  - `item-drop-properties.json` is a different 98-row table, read only for `Field3`.
  - E19 parked loot and the break effect (`AlundraEventProgramRunner.cs:309-311`, O-E19-23).
- **Who else uses it:**
  - Every `DestroyEntity(e,fx)` death.
  - Tile cut by the hero, the sword beam, the charged arrows and the bomb.
  - Map-placed pickups, through the flag.
  - Chests probably do not use it, since they run the item opcodes already ported in E19.l2 (INFERENCE).
- **Options:**
  - (a) E20.e does only draw, spawn and C255 for grass.
  - (b) A dedicated slice before E20.e that provides the shared `SpawnWarpEntity` plus the loot and item entity. It is also needed by E20.f and E14, and `SpawnWarpEntity` has 102 callers.
  - (c) E14 owns it, and E20.e cuts grass without loot as a documented gap.
- **Recommendation:** (b).

### O-E20-4 Hero projectile handlers

- **Spawn types (FACT, `2-attack.md:§3`):** arrows 3 and 4, charged arrows 7, fireball `0x0B`/`0x0C`, ice ball `0x0E`/`0x0F`, wave sword `0x1B`–`0x1D`.
- **Handler slots (FACT):**
  - Arrows are E17, `0x8007F420`, slot `0x800C5778`.
  - Bomb is E12, `0x8007F27C`, slot `0x800C5764`.
  - Sword beam is C61, `0x8007A958`.
  - Charged arrows are C94, `0x8007C0D8`.
  - Ice is C60, `0x8007A8A0`.
  - Fire is C54, address not read.
- **What the arrow handler does:** it scans targets itself using `DamageResponses[5]` and `HitCounter`, then calls `DestroyEntity(e,-1)`.
- **Needs:**
  - The spawn primitive.
  - E20.c1's boxes, `HitCounter` and `BalanceRecord`.
  - `DestroyEntity` with its effect.
  - Tile cut and loot for the sword beam, charged arrows and bomb.
- **Who else uses them (counts of exported sprite records per handler):**
  - C61 ×3, C94 ×1 and E12 ×1 match the hero's three wave swords, charged arrows and bomb.
  - E17 ×4, C60 ×6 and C54 ×1 are more than the hero alone needs, so enemies may share them. That is UNKNOWN.
  - The mapping from handler index to sprite is INFERENCE, based on the C61/C94 counts matching.
- **Options:**
  - (a) E20.f owns arrows, fire/ice and wave swords, and E20.h owns the bomb.
  - (b) The shared handlers E17, C60 and C54 go to E14 if enemies use them.
  - (c) Split: E20.f ports the spawn plus the hero-only handlers.
- **Next step:** E20.f's own discovery should read the global sprite table for these indices.
- **Recommendation:** (c).
