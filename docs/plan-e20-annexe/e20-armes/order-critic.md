> Report returned by one agent of the read-only discovery workflow for E20.e0, E20.f and the dependencies of E20.h and E20.g (night of 2026-10-09), kept verbatim as evidence. The plan holds the reconciled facts.

# order-critic: dependency graph and execution order for tonight (weapons, then item use, then collisions)

I wrote nothing in the repository and built nothing. My scratch folder is `.../scratchpad/disc-weapons/order-critic/`, and it is empty: no probe was needed.

**Labels**
- **FACT (me)**: I re-read it myself, with file:line.
- **FACT [report §]**: taken from one of the five reports and not re-run by me. Where several reports agree independently, I say so.
- **INFERENCE** gives its basis. **UNKNOWN** gives what would decide it.

**Slice names used below**
- Existing: E20.e0, E20.e, E20.f, E20.g, E20.h, E20.c2, E14.a.
- Sub-slices I propose: X0, S1, E0a, E0r1, E0r2, R0, K0, Fa to Fg, C2-0, C2a, H0 to H4, G1 to G4, A362.

## Things that change the plan

1. **D-E20-18's sharing premise is false, so the weapons need nothing from enemy-owned handlers.**
   - FACT, four reports agree (f-weapons §0.1, f-projectiles item 1, e0-spawn §11, test-means §0.6):
     - E17, C60, E9, C61, E10 and C94 appear only on the hero's common-bank sprites (types 3-15 and 0x1B-0x1D).
     - "Fire = C54" is wrong: C54 (`0x80078E34`) is the boss on map 326.
     - The only non-hero spawner of a hero projectile is the Torche handler E20 (`0x8007F6C8`). It spawns type 0x0C and zeroes `+0x70..+0x84` (f-projectiles item 1), so C60/E9 never run on that copy.
   - What the weapons need from the "shared" side:
     - from E14.a0, only the per-type registry of D-E14-1;
     - from D-E14-2, only the break-effect byte, and only for the charged arrow (type 7, BreakEffect 8; f-projectiles §3).
   - E3, which really is shared (15+ banks), is needed only by item use: earth magic 0x10 and fire projectile 0x17 (e0-spawn §5). It is not needed by the weapons.
   - Caveat (FACT [e0-spawn §11], not re-checked): 3 map records on map 309 place common sprite 0x0C. Registering C60/E9 for type 12 brings them alive. That is faithful, but it is a regression watch-point.
2. **The registry is a prerequisite of E20.f, E20.g and E20.h, and today it sits inside E14.a.**
   - FACT (me): ADR-0055's context says `RunSpriteEvent` (`AlundraEventProgramRunner.cs:293-330`) runs only the save book and E0/E1.
   - FACT (me): `docs/plan-e14-annexe/e14a/check.md:165-170` defines E14.a0 as the registry, empty, with a switch that turns the natives off.
   - D-E20-24's "prerequisites first" rule lets the registry part of E14.a0 (call it R0) be pulled forward. The enemy helpers stay with E14.a.
3. **A slice that is not in the plan is on the critical path: the logic-tick vertical and flight step for non-hero entities (K0).**
   - FACT (me): `AlundraScriptedMotion.cs:194` runs `airborne = entity.IsPlayer && RunHeroVerticalTick(entity)`, and the gravity landing at `:198` is hero-only.
   - What the binary needs, FACT [f-projectiles §5, §8]:
     - arrows fly at constant Z (no gravity bit) with `NoObstacleSlide` (bit 13);
     - balls drop under gravity from Z+32 px;
     - an arrow that bounces gains `Gravity|0x100`.
   - K0 also serves thrown crates (G2), the bomb and beans (H3), spells (H4) and E14 (enemy ground heights, `check.md:171-173`).
4. **A fresh entity takes part in its spawn tick in the binary; the DLL gives it its first tick one frame later.**
   - FACT [e0-spawn §8, f-projectiles item 2]: confirmed on the real code by both reports.
   - FACT (me): the DLL defers it on purpose. `AlundraWorldProxy.cs:2617-2620` says the engine integrates a spawned entity "at the head of the NEXT frame". `World.cs:168-173` `AddEntity` only queues into `_baseObjectsToAdd`.
   - This answers O-E20-11. Whether a same-tick join is possible without an engine change is UNKNOWN; that is spike S1.
   - It changes a pinned E20.a structure test ("entités apparues en cours d'image").
5. **E20.c2 splits naturally, and only its HP part is needed early.**
   - The HP model (C2a) is a hard prerequisite of the heals (H1), water magic (H4) and hearts pickup (E0c). It is not "the hurt".
   - FACT (me):
     - D-E20-24 says "E14.a and the hurt (la blessure) of E20.c2 follow".
     - D-E14-4 orders E14.a before the hurt because E20.c2 re-measures E14.a's arcs.
     - `check.md` §c (e20-c2, lines 155-203) separates c2a (HP model, END, `UpdateItemEffectState`) from c2b (the hurt).
   - The split itself (Q6 of that check) was **not** answered by the author. Plan line 123 says the author answered questions 1-5. So the split is a main-session planning choice, and confirming it is non-blocking.
6. **Item use cannot finish before carrying.**
   - FACT [h-g-deps §0.2]: the bean start `0x80034224` and the bomb start `0x8003453C` spawn the object and make it the carried entity (`hero+0x2C`).
   - So H3 (beans and bomb) needs G1 (lift) and G2 (throw).
   - The author's order holds only if E20.h is split: H0 and H1 before E20.g, H3 and H4 after G1 and G2.
7. **The envelope edge "E20.f needs E20.e" is too strong.**
   - FACT [f-weapons §0.5, f-projectiles §4]: E20.e owns two things the weapons touch.
     - The grass cut `CheckAndTriggerTileEffect` (`0x8003A7B0`), called by C61 and C94.
     - The flail bounce, which is a self-contained block of `CreatePlayerAnimationEffects(0)`.
   - FACT [e0-spawn §9]: the grass cut drops loot through `0x80032C7C`, which drags in the drop (E0b), pickup C255 (E0c), C2a and the blink rule.
   - Recommendation:
     - the tile cut stays a named stub in C61/C94 until E20.e;
     - the flail block is ported alone at its exact site (`0x80031C3C`; the DLL marks it "not ported" at `AlundraPlayerManager.cs:225-226`, FACT me).
8. **No entity loses HP before E14.a.**
   - FACT [test-means §0.1]: `ComputeNewHp` has no production caller.
   - So E20.f and E20.h acceptance rests on contact, sparks, the hero `HitCounter` and `+0x1D0`, scripted `0x87` targets, and oracle traces. Kills are not available.
   - The arenas are listed in test-means §6: A1 map 14, A3 map 16, A4 map 303, A5 map 443, A6 map 396.
9. **The in-game recipes need F9 saves that only the author can generate.**
   - FACT (me): `docs/test-saves.md:9-10` says to run the tool outside the Claude app, because writes under `%LOCALAPPDATA%` are virtualized.
   - FACT [test-means §0.2]: `Preset` cannot set the weapon, MP, HP or Circle item. `AlundraTestSavesTests.cs:70-72` pins exactly two preset names.
   - Fg can add the presets; creating the save files is a morning action for the author.
10. **The 362 lid probably does not need the hero's bomb.**
    - FACT [h-g-deps §0.1, test-means §6 A10]: Olen's script spawns the bomb with `0x8B` and explodes it with `0x1A [1]`; the lid's `D[1]` is `0x87 [128,3]`.
    - The 362 has three owners in the plans: D-E20-10 and the c1b-2 row put it in E20.h, D-E14-7 in E14.a. A test-only arc (A362) can decide it now.
    - FACT [e0-spawn §11]: when H3 registers type 2's handlers C62/D30/E12, Olen's placed bomb changes behaviour, so A362 must be re-run after H3.
11. **Almost every slice edits the same two files, so tonight's run is serial.**
    - FACT (me): `AlundraPlayerManager.cs:208-230` holds the BlockedByEntity branch, the locked branch and the driver site. Fa (timer zeroing at `0x80031BB4` and `0x80031BEC`), C2a (END on every exit), H0 (cancel `0x800350C0` in the locked branch) and G1 (`UpdatePlayerCarriedEntity(1)`) all edit them.
    - E0a, E0r1, R0 and K0 all touch `AlundraWorldProxy.cs` or `AlundraEntityScriptProxy.cs`.
    - Only X0 (converter), C2-0 (an oracle under `docs/`) and the Fg preset are separable, and X0 must not run while tests read `alundra-project/`.
12. **The spawn Z convention trap.** FACT: three numbers describe the same spawn, and the DLL uses yet another convention.
    - The oracle journals the spawn argument: z 1048577 [test-means §4].
    - The stored PosZ is 1048578, because `InitializeEntity` adds 1 [f-projectiles §5].
    - The DLL convention drops the +1 (D-E19-59, `plan-e19-opcodes.md:206`).
    - Expected values copied from traces must be translated once, in one helper.

## 1. Starting state

- **Done.** E20.a, E20.b, E20.c1a, E20.c1b-1 and E20.c1b-2 are done and CONFIRMED.
  - FACT (me): plan §1.3-§1.7 headings; `git log` from `1c2de24` to `1f0055c`.
- **Not started.** E20.c2, E20.d, E20.e0, E20.e, E20.f, E20.g, E20.h, E20.i and E14.a.
  - FACT (me): discovery only, commit `542aa8c`.
- **Stubs in place.** FACT (me):
  - `TryUseItem` returns 1 (`AlundraPlayerManager.cs:468`).
  - `PlayerTryAction` returns 0 (`:471`).
  - `UpdatePlayerWeaponEffect` (`:533`), `SpawnWeaponProjectile` (`:573`), `UpdatePlayerCarriedEntity` (`:579`) and `UpdateItemUseDriver` (`:646`) are stubs.
  - The locked branch returns early (`:220-223`).

## 2. Nodes

| Node | Content | Source |
|---|---|---|
| **X0** | Converter: `Data/global-sprites.json` (common index → prefab id; 151 valid, 105 invalid, entry 256 a sentinel) plus the BreakEffect byte (`+0x1E`). One full in-place export, manifest proof, format ADR(s). | e0-spawn §2, Q3; D-E14-2 |
| **S1** | Timeboxed spike, two questions: (a) can a proxy spawned during `MovePlayer` join `_tickEntities` and move by its logical position before the engine integrates it; (b) can a non-hero entity's controller be bypassed (carried or flying)? | e0-spawn Q2; f-projectiles item 2; h-g-deps §3.5 |
| **E0a** | `SpawnEntityBySpriteIndex` (`0x80039F58` → `0x80039B28` / `0x80039AD0` / `0x80039D04`): record-less factory refactor (`AlundraEntitySpawnFactory.cs:564,636,693`), `EntityRefId = -1`, parent `+0x0C` and root `+0x08`, same-tick join per S1, `SpawnWeaponProjectile` wired, A0 and A254 | e0-spawn §15 |
| **E0s** | The 64-slot model: first-free reuse, cap 63 | e0-spawn §2 (deferrable, see §5) |
| **E0r1** | RNG advance once per tick, after the effect stage (`0x8002BD0C-0x8002BD34`) | e0-spawn §7 |
| **E0r2** | Loot roll at creation (`0x80032A40`, for every spawn path) | e0-spawn §0.7 |
| **E0b / E0c** | Drop with veto (`0x80032B90`) and the tile-cut loot; C255 pickup (`0x80033F00`) | e0-spawn §15 |
| **R0** | Registry: (slot, code) entries gated by sprite type, natives-off switch, empty | check.md e14a:165-170 |
| **K0** | Non-hero logic-tick vertical step and flight (gravity bit, impulse, landing, constant-Z flight, `NoObstacleSlide`, `ForceAdjusted` on wall, edge and terrain height, ground `+0x138` from `0x800370C4`) | f-projectiles §8 items 3-4 |
| **Fa** | Charge timer `0x80126FFC`, release path, zeroing sites, pad in the signature, tap bump | f-weapons §2-3 |
| **Fb** | Flail bounce block of `CreatePlayerAnimationEffects(0)` | f-weapons §4 |
| **Fd** | Handlers E17, C60, E9, C94, C61 (tile cut stubbed), E10, and `DestroyEntity(e, fx)` with effect | f-projectiles §4 |
| **Fe** | Effects 3, 0x10, 0x11, 0x12, 0x19; hit effects 4 and 5 in the hit pass; host effect-stage accessor | f-weapons §0.6 |
| **Ff** | Opcodes `0xC0` / `0xC1` | f-weapons §10 |
| **Fg** | `Preset` fields (Weapon, Item, Hp, HpMax, Mp, MpMax, Money), `ArcRun` give and equip helpers, map smoke tests, arenas | test-means §2, §6-8 |
| **C2-0 / C2a** | c2 oracle; HP model, END on every exit, rest of `UpdateItemEffectState`, vessel seam | check.md e20-c2:155-175 |
| **H0 / H1** | Item-use frame (`0x8002ED64`, `0x8003499C`, `0x8003634C`, `0x800350C0`); the five instant items | h-g-deps §2.5 |
| **H2 / H3 / H4** | Sand cape; beans and bomb; tomes by element | h-g-deps §2.5 |
| **G1 / G2 / G3 / G4** | Lift and carry; throw and break; liftables solid again; the 392 barricade | h-g-deps §3.6 |
| **A362** | Test-only arc of map 362 with Olen's bomb | h-g-deps §0.1 |

## 3. Prerequisite edges, with evidence

Kinds: **H** = hard (the slice cannot meet its acceptance without it). **S** = soft (a named stub or partial port is possible). **F** = file or structure coupling (an ordering choice, not a semantic need).

| # | Edge | Kind | Evidence |
|---|---|---|---|
| 1 | E0a → X0 | H | FACT [e0-spawn §2, f-projectiles item 6]: no index → prefab data is exported; the prefab id comes from `EntityNames.csv`, which is not shipped (`SpriteWriter.cs:394,494`) |
| 2 | E0a (same tick) → S1 | H for fidelity | FACT (me) `AlundraWorldProxy.cs:2617-2620`, `World.cs:168-173`; FACT [E] the binary ticks it at once |
| 3 | Fc (shot) → E0a | H | FACT (me): the `SpawnWeaponProjectile` stub at `AlundraPlayerManager.cs:573` |
| 4 | Fd → R0 | H | FACT (me): ADR-0055 context (`RunSpriteEvent` runs only the book and E0/E1) |
| 5 | Fd → K0 | H | FACT (me) `AlundraScriptedMotion.cs:194,198`; FACT [f-projectiles §5] constant-Z arrows, falling balls |
| 6 | Fd (type 7, E1) → X0 (BreakEffect) | S | FACT [h-g-deps §0.4]: `DestroyEntity` reads `lbu 0x1e` at `0x8003A5E8`; D-E19-29 already ports E0/E1 "without break effect" |
| 7 | Fd (C61, C94 grass) → E20.e | S | FACT [f-weapons §0.5]: `CheckAndTriggerTileEffect` belongs to E20.e. Its loot needs E0b and E0c (edge 21) |
| 8 | Fd / Fe (E9 particles, hold spark) → E0r1 | S (H for oracle columns) | FACT [f-projectiles item 7]: E9 draws 10 at impact, then 2 every 8 ticks; FACT [e0-spawn §6] the DLL has no advance (`AlundraWorldProxy.cs:2766-2772`) |
| 9 | Fe → effect-stage accessor | H | FACT [f-weapons §0.6]: `IAlundraScriptHost` exposes none |
| 10 | Fb → c1a `HitCounter`, c1b-1 `HitBox` (done); Fb → E20.e | done; S | FACT [f-weapons §4]; site "not ported" at `AlundraPlayerManager.cs:225` (me) |
| 11 | Fa ↔ C2a | F | FACT (me) `:208-223`; Fa zeroes at `0x80031BB4` and `0x80031BEC` [f-weapons §2], and C2a restructures those exits |
| 12 | C2a → C2-0 | H (acceptance) | FACT (me) check.md e20-c2:168-169 ("oracle groups E, F and G") |
| 13 | H1, H4 (water), E0c (hearts) → C2a | H | FACT [h-g-deps §0.8; c2-binary.md:25-29]: heals write hero `+0x14`; the mirror at END `0x80032914`. In the DLL the entity Hp is set from the sheet (`AlundraEntityScriptProxy.HitPass.cs:70-71`, me) and the stats are separate |
| 14 | H0 → C2a | F | FACT [h-g-deps §0.7]: the locked branch calls `0x800350C0`; C2a adds END on every exit (check.md:157-160) |
| 15 | H1 → H0 | H | FACT [h-g-deps §1]: the start switch `0x8003499C` dispatches the instant items |
| 16 | H3 → G1, G2 | H | FACT [h-g-deps §0.2]: starts write `hero+0x2C` |
| 17 | H3, H4 → E0a, R0, K0 | H | FACT [e0-spawn §6]: 11 item-use spawn sites; FACT [h-g-deps §2.2]: handlers C73/E21, C62/D30/E12, C63/C65/C75/E3/E13/E28 |
| 18 | H4 → E3 registered for types 0x10 and 0x17 only | H | D-E14-1 type gating keeps the enemy E3 users inert |
| 19 | G1 → R0, S1/K0, C2a | H, H, F | FACT [h-g-deps §3.1, §3.3]: C23 carried-state switch; `MoveEntity` `0x80037E34` places a carried entity, and the DLL has no platform-follow branch; `UpdatePlayerCarriedEntity(1)` sits in the locked branch |
| 20 | G2 → G1, K0, X0 (BreakEffect), E0b | H, H, H, S | FACT [h-g-deps §3.1]: E2 → `DestroyEntity(-1)` → effect plus loot; precedent for "nothing drops" until E0b: D-E14-3 |
| 21 | E20.e → E0a, E0b, E0c | H | FACT [e0-spawn §9]: the tile cut calls `0x80032C7C`, which spawns item + 0x1E |
| 22 | E0b → E0a, E0r2 | H | FACT [e0-spawn §9]: the drop reads `+0x3C`, written by the roll |
| 23 | E0c → E0b, R0, C2a, blink rule | H, H, H, S | FACT [e0-spawn §0.11-12]: blink owned by c2b (D-E20-21), or implemented generically |
| 24 | c2b → E14.a | H (author) | D-E14-4, D-E20-24 |
| 25 | c2b hosts the hurt-path sites of Fa, H0 and G | F | FACT (me) check.md:179-181: c2b zeroes the timer and pointer and calls `UpdatePlayerCarriedEntity(2)` and the driver stub; D-E20-19 |
| 26 | E14.a → R0, K0 (ground heights) | H (reuse) | FACT (me) check.md e14a:166-173 |
| 27 | A362 → none new; re-run after H3 | — / S | FACT [e0-spawn §11]: 1 bomb record on 362 |
| 28 | G3 → G1, G2, G4, author | H | FACT [h-g-deps §0.6, §3.4]: 12 chain maps with liftables; A5/A5r on 392; crate 5 leaves a 3-px window |
| 29 | Fg recipe → author-generated F9 saves | H (recipe) | FACT (me) `docs/test-saves.md:9-10` |

## 4. Graph

```
done: E20.a  E20.b  E20.c1a  E20.c1b-1  E20.c1b-2
                    |
   +----------------+------------------+---------------+
   Fa   Fb(S:E20.e)  Ff               S1(spike)        X0(converter)
   |                                   |  \             |
   |                                   |   +-> K0 <-----+ (none)
   |                                   v      |
   |                       E0a <----- X0      |
   |                        | \               |
   |                 E0r1   |  Fc ------------+--> Fd <-- R0
   |                   \    |                      |  (S: X0 BreakEffect, E20.e tile cut)
   |                    +---+------------------> Fe
   |
   +--(F)--> C2-0 -> C2a --(F)--> H0 -> H1          [weapons done above]
                      |
          R0,K0,S1 -> G1 -> G2 -> H3 -> A362 re-run
                                 \-> H4a-d (E3 per type; C2a for water)
          G1,G2 + G4(evidence) + AUTHOR -> G3
   later: E0r2 -> E0b -> E0c -> E20.e ; E14.a0-helpers -> E14.a1 -> c2b ; E20.d ; E20.i
```

## 5. Recommended order for tonight

All steps are serial (point 11 above). Each slice follows the plan's own lifecycle: plan-verifier, executor, fresh verifier (plan line 21). X0 is a data or serialization change, so its independent reviews are mandatory, and it uses the double-export determinism proof (memory: "export déterministe au bit près").

**Phase W, weapons (the author's first item).**
1. **Envelope revision.** It records points 1-7, 10 and 11 above. One plan-verifier pass. No author needed (section 6).
2. **S1 spike.** Timeboxed, in the scratch area or a throwaway worktree. It comes first because it is the only node that can raise an engine question (QD). Its answer also settles G1's open UNKNOWN.
3. **Fa, then Fb, then Ff.** They have no new prerequisites, so they give early visible value.
   - Fa: aura and spark calls go to a named effect seam until Fe.
   - Ff: tiny; the bit only shows once H0 exists.
4. **X0.** Run it while no executor is running tests. One full in-place export, manifest diff, then a second export to prove the bytes are identical.
   - Ship the BreakEffect byte now, not in E14.a. G2 needs it before E14.a, and the author's order puts G2 before E14.a.
5. **E0a.** Same-tick join per S1; justify the E20.a pin move with O-E20-11's answer.
   - Keep append order. O-E20-13 already allows "à mesurer avec E20.e0 **ou E14**" (plan:1459, FACT me), so E0s, the 64-slot model, can be its own later slice.
6. **E0r1.** Re-enable the c1 spark-speed comparisons (e0-spawn §7) and justify every moved pin.
7. **R0.** Empty registry plus switch. Keep E0/E1 and the book outside it (AD-2 "keep"), so the pinned dispatch tests stay (`dll-natives.md:281`).
8. **K0.** Accepted against the f-projectiles traces: `hunter_wall`, `fire_rod`, `ice_rod`, `arrow_vs_tile_heights`, `fire_on_deep_water`. Translate the +1 once (point 12).
9. **Fd**, registered for hero types only, then **Fe**.
10. **Fg.** Presets, arena smoke tests per map (16, 69, 77, 136, 137, 304, 396, 443 are UNKNOWN to build), and arcs A1-A6 (test-means §6).

**Phase I, item use (the author's second item).**
11. **C2-0, then C2a.** Stop rule from check.md:175: any story-arc or trace pin moving while the stats are 10/10.
12. **H0, then H1.** The heals work on the chain chests (herb 36, S Potion 41; h-g-deps §2.5).
13. **A362**, whenever convenient. It is cheap and independent.

**Phase C, carrying (the prerequisite of the rest of item use) and the collisions.**
14. **G1, then G2.** With no E0b, nothing drops; record it as the analogue of D-E14-3, to confirm.
15. **H3**, then re-run **A362**, then **H4a-d** (one element per sub-slice; each starts by reading its handlers).
16. **G4.** An evidence arc on 392 (lift record 5 versus climb crate 5) and the 3-px geometry search.
17. **G3 only after QA** (section 6). It reverses D-E19-28 and D-E19-46 and rewrites pinned chain arcs.

**After tonight:** E0s, E0r2, E0b, E0c, E20.e, E14.a (a0 helpers, then a1), c2b, E20.d, E20.i.

**Expected reach.** INFERENCE from the size of past slices: one night realistically covers steps 1-10, possibly 11. Phases I and C will mostly fall to later runs; ordering them correctly still avoids rework.

**Pause points.**
- S1 shows an engine change is needed for the same-tick join. Keep E20.a's approved rule and ask QD. This is not a new deviation: E20.a's approved rule is "garde la règle d'aujourd'hui" (plan:1460), so this does not block.
- K0 cannot match the trace positions.
- X0's export is not deterministic.
- A story-arc pin moves without a binary justification.
- Two fix and re-verify failures on one slice.

## 6. Decisions

**No author decision needed.** Each item rests on a standing rule or an existing decision.

| Slice or choice | Basis |
|---|---|
| Fa, Fb, Ff, Fd, Fe, Fg | Approved envelope row E20.f; binary is ground truth |
| Pulling R0 (E14.a0 registry) and K0 forward | D-E20-24 "prerequisites first"; D-E14-1 fixes the registry's shape |
| Porting E17, C60, E9 in E20.f | D-E20-24: they are prerequisites of working bows and rods (without E17, an arrow stuck at a wall picks a no-op E slot forever: INFERENCE from `PickEventTrigger`, f-projectiles §4). Confirm ownership later: Q-B |
| X0 index → prefab file | Additive data file with its own ADR; D-E20-17 put the spawn in E20.e0 |
| Same-tick join (if DLL-only), RNG advance, loot roll | Binary fidelity; O-E20-11 measured; D-E20-17's envelope row names the advance |
| Keeping append order (E0s later) | O-E20-13's own text allows E14 |
| C2-0 and C2a before item use | Not "the hurt"; D-E14-4's purpose (re-measuring enemy arcs) is unaffected. INFERENCE; confirm: Q-E |
| Power Glove 0x3B rule for kind-3 lifts | Binary `0x8002EDF8` [h-g-deps §0.5]. The decompilation (`PlayerManager.cs:1105`) and `EntityFlags.cs:282` read it inverted; do not copy them |
| Named stub for the tile cut in C61/C94 until E20.e | Same pattern as D-E20-19; record it in the plan |
| G1, G2, H0, H1, H3, H4 | Envelope rows; H3 needs G1 and G2 as prerequisites |

**Questions for the author.** Q-A is the only blocking one, and it blocks G3 only. The rest are non-blocking or conditional.
- **Q-A (blocks G3, possibly G4).** Does "collisions" mean E20.g, with crates and jars solid again (ending D-E19-28 and D-E19-46, and rewriting the A5/A5r 392 corridor arcs)? Or one of the alternatives in section 7, especially O-E19-22 (deep water blocks the hero without Merman boots, unowned)?
- **Q-B.** D-E20-18 listed E17, C60 and "C54" for E14 on the premise "enemies maybe share them". The census shows they are hero-only, and C54 is a boss. Confirm that E20.f owns E17, C60 and E9, and correct the plan text.
- **Q-C.** Normal arrows fly at hero Z+16 and miss bodies with `SizeZ` ≤ 16: the Beannoïde, and 88 of 217 class-A sprites (f-projectiles item 3). Keep this as faithful, or treat it as a defect of the original? The default is faithful.
- **Q-D (only if S1 says an engine change is needed).** Accept an engine change for the same-tick join, or keep E20.a's rule (a 1-tick, 3-px lag on every shot)?
- **Q-E.** Confirm the c2 split (c2-0, c2a, c2b) and that c2a goes before item use. Q6 of `e20-c2/check.md` was never answered.
- **Q-F.** The BreakEffect export moves from E14.a (D-E14-2) to X0. Confirm.
- **Q-G.** The sand cape (states `0x20-0x24`, item `0x20`) is in no slice. Is it needed, and who owns it? The default is to defer it.
- **Q-H.** The 362 premise of D-E20-10 ("a bomb placed on a lid") looks wrong. Confirm once A362 has run.
- **Q-I (only if G4's evidence is inconclusive).** O-E20-5: does the original pass the 392 barricade by lifting or by climbing?
- **Morning action, not a decision.** Generate the weapon and item F9 saves outside the app, once Fg lands.

## 7. Is "collisions" E20.g?

**Evidence for E20.g**
- FACT (me): the main session's reading is already on record as "to confirm" (D-E20-24, ADR-0054).
- FACT (me): E20.g is the only envelope row with obstacle semantics: "caisses et jarres redeviennent des obstacles (fin de l'écart D-E19-28/D-E19-46)" (plan:488).
- FACT (me): the author grouped "porter, lancer" with "les objets du Rond" in D-E20-10 (plan:97). The three words of D-E20-24 then cover f, h and the obstacle half of g. Swimming already has its own name and slice (E20.i, D-E20-16).
- FACT [h-g-deps §3.2]: the gap is the most widespread visible movement defect. There are 2,251 liftable placements, crates on 12 chain maps, and the hero walks through them.
- FACT (me): D-E19-46 shows the author knowingly deferred it "jusqu'à E14" on 2026-10-03.

**Evidence against, by alternative (h-g-deps §6)**

| Alternative | Assessment |
|---|---|
| (2) Native-destruction obstacles | Already solid (D-E19-34). Their "collision" is destruction, which is E14 |
| (3) O-E19-22 deep-water block | FACT (me) `plan-e19-opcodes.md:12326`: "à placer", unowned; 10,644 cells on 54 maps; the DLL hero walks on deep water (E20.d recipe text, plan:484). The strongest alternative: an unowned hero collision rule. It sits naturally with E20.i |
| (4) O-E19-42 exact contacts | The author kept the sliding with D-E19-58 on 2026-10-03, "pour l'instant". No sign he changed his mind |
| (5) O-E19-40 snap, O-E19-44 doors | O-E19-40 needs an engine plan (FACT me, :12344); O-E19-44 is done by E19.m13 |
| (6) Hit collisions | Covered by f, h and E14 anyway |
| (7) Engine refactor | Memory says phases A-F are delivered; the rest is editor UI |
| (8) Per-tick riding | D-E20-13 is a deviation the author accepted |

**Verdict.** INFERENCE, medium-high confidence: E20.g is the best-supported reading, with O-E19-22 the only serious rival.
- The ambiguity costs nothing tonight. G1 and G2 are needed anyway as prerequisites of H3, and the night will not reach G3.
- Gate G3 on Q-A, and offer "E20.g plus O-E19-22" as the joint candidate.

## 8. Inconsistencies between the five reports

These are resolved here; none changes the order.

| Topic | What the reports say | Resolution |
|---|---|---|
| Who ordered E20.f before E20.e0 | f-weapons §0.5 says "the author ordered f before e0 and e" | Wrong. D-E20-24 puts prerequisites first and names e0 explicitly (FACT me, plan:135-138) |
| Arrow spawn Z | test-means: z 1048577; f-projectiles: 1048578 | Argument versus stored value: `InitializeEntity` adds +1 (point 12). Consistent |
| E1 and E3 user counts | e0-spawn: E1 on 54 banks, E3 on 15 banks plus 0x96-0x99; f-projectiles: E1 on 178 headers; f-weapons: E3 has 21 users | Different counting units. Not decision-carrying |
| Chests | test-means §0.9: native, E14 (INFERENCE); e0-spawn §10: the DLL ports chests through opcodes (E19.l2), handler F255 | e0-spawn's FACT[D] wins; herb and S Potion chests on the chain work today |
| Same-tick spawn | f-projectiles first saw S+1 | Harness artefact (zeroed slot indices), since fixed; it now agrees with e0-spawn FACT[E] |
| e0-spawn Q1 (slot cap) | Framed as an author question | I judge it not one: the faithful port needs no author, and deferral is already allowed by O-E20-13 |
| `UpdateItemEffectState` owner | h-g-deps lists it among E20.h's needs | Owned by c2 per D-E20-19; it lands in C2a |

## 9. UNKNOWN, and what decides each

| Unknown | Decider |
|---|---|
| Same-tick join and controller bypass for a non-integrated or carried entity | S1, after reading `AlundraEntitySpawnFactory.CreateEntityFromPrefab` (`:427`), the proxy's initialization, and `PushLogicalPositionToRoot` |
| Whether map 309's 3 placed type-0x0C records keep C60/E9 or are overridden by map programs | Read their records' program fields and `0x8004201C` |
| Whether arenas 16, 69, 77, 136, 137, 304, 396, 443 build in `ArcRun` | Smoke tests per map (test-means §8) |
| Whether the 362 lid reacts to Olen's bomb today | A362 |
| Spell handler bodies (C63, C65, C75, E3, E13, E28); wind-lower's warp or fade; CD voice `0x8005A9E0` | Read the binary at the start of each H4 sub-slice |
| E9 slope value 1 | A census of `UpdateGroundSlope` ground properties |
| Template `+0x228 = 0` | A RAM dump of `0x80134590` (e0-spawn §2) |
| The night's actual reach | Slice sizes; the INFERENCE in section 5 |

**Evidence.**
- Files I re-read: `docs/plan-e20-heros.md` (1-152, 470-507, 1445-1461), `docs/plan-conversion-totale.md:741-766`, ADR-0054, ADR-0055, `docs/plan-e14-annexe/e14a/check.md:150-229`, `docs/plan-e20-annexe/e20-c2/check.md:140-212`, `docs/plan-e19-opcodes.md:123-205,12326-12348`, `docs/test-saves.md:9-10`.
- Code I re-read: `Alundra/Scripts/AlundraWorldProxy.cs:2606-2685`, `AlundraEntityScriptProxy.cs:1170-1267`, `AlundraPlayerManager.cs:200-244`, `AlundraScriptedMotion.cs` (grep), `CasaEngineMonogame/CasaEngine/Framework/Scene/World/World.cs:150-185`.
