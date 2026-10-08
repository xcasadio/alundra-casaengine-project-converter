# notes.md - E19.h2 D1: findings, corrections, order of the tick, decision note

> **Errata (independent check, `recompute/corrections.md`):** no number of this annex changes; C1-C9 retag labels and provenance ([R], [input], [I], [D]); **C3** corrects the A10J X pin (below). Read the corrections before using a value.

Read-only study of the vertical state of the NPCs in the air, on the real code of `ALUN_CD.EXE` (France). No dotnet, no build, no test run, nothing staged or committed, no tracked file touched (only this folder was created). Branch `chantier/e19-suite3`, `git rev-parse HEAD` = `f06e08736079f0d14c8c1428ff2d9a691c311724` (see README.md).

Tags: **[M]** measured on the real code by two engines that agree, **[R]** read in the disassembly (`python -B docs/plan-e19-backlog-1006-annexe/mipsdis.py <addr> <count>`), **[I]** inferred, **[D]** read in the DLL source or in a test.

## 1. What D1 produced

| item | where |
|---|---|
| archived tools, imports fixed, edits listed | `tools/`, README.md |
| replay of IZF 1360 / 1280 / 768 / 1024 and TR-V, per update: PosZ, ForceZ, FinalForceZ, contact, IsOnGround, Terrain, tick of `0x25` | `out/replay_*.txt`, `out/replay_all.json`, `values.md` |
| second engine and models agree on every update | `out/replay_summary.txt` (16 cases, 0 disagreement), `out/rules_vs_emu.out` (21000 ticks), `out/validate.out`, `out/zmodel_dll.out` |
| Giles on the real cells of map 10, the ledge fall | `out/giles_ledge.txt` |
| DLL against binary per pin, mechanism (a)-(d) | `dll-delta.md` |

Key values **[M]** (all counted from the tick `s` of the `1A` / `1B`; update k = pass of the tick s+k-1; `0x25` returns at s+K, K = first update with `IsOnGround` or contact):

| family | K = `0x25` returns at | last air point | ForceZ at K | contact first at |
|---|---|---|---|---|
| IZF 1360 | s+23 | 1.375 px | 0 (FinalForceZ -372736) | 23 |
| IZF 1280 | s+21 | 5.0 px | -327680 | 22 |
| IZF 768 | s+13 | 3.0 px | -196608 | 14 |
| IZF 1024 | s+17 | 4.0 px | -262144 | 18 |
| TR-V (no gravity, 8 px, -32768/tick) | t+16 | 0.5 px | -32768 | 17 only without the `1B [0,0]` tail, never with it |
| Giles onto the 16 px ledge (IZF 1360) | s+19 = F0+120 | 19.125 px (3.125 px above the ledge) | 0 | 19 |
| ledge fall | first tick in the air = 5 | | | |

## 2. Findings that refine or correct `h2.json`

1. **All numeric claims of the facts 11, 12, 13 and 25 are reproduced** (1360: update 23 with `FinalForceZ` -372736; 1280: `ForceZ` -327680 at update 21, contact at 22; TR-V at t+16; ledge tick 5). Each value now has a saved output and two engines (the discovery had kept no output file).
2. **TR-V contact**: "contact at update 17" holds only if the force is kept. The real program runs `1B [0,0]` in the call in which `0x25` returns (t+16), before update 17, so `ForceZ` is 0 and the strict landing test (`T >= ModdedPosZ + 0` is false at rest = T+1) never raises the contact. Both variants are saved (`out/replay_S-TRV.txt`, `out/replay_S-TRV-keep.txt`).
3. **`TerrainHeight` read after a pass is the probe after the XY step.** ComputeZ probes at 0x80037690 before the step, but the value in `+0x138` at the end of the pass is the post-step terrain (ledge, tick 4: the landing test passed on the pre-step terrain 16 px, `+0x138` reads 0 after the pass, `IsOnGround` is already 0 with PosZ still at 16 px). A reader of `+0x138` or of `IsOnGround` must not assume the terrain used by ComputeZ.
4. **Giles: the order Z-before-XY does not matter for his landing.** On the real cells (map 10, ledge from column 22 = x >= 528 px) the box edge passes x = 528 after update 17; the binary's ComputeZ sees the ledge from update 18, the DLL's order from update 17, the landing is update 19 in both. K = 19 for every start x from 490.6 px (the plan's 493.3125 px has about 2.7 px of margin). Only the (c) magnet moves his `0x25` (F0+119 in the DLL, F0+120 in the binary).
5. **Giles' XY**: with the binary order of `UpdateAnimation` then `P` in one tick, the first jump step is 129024 (animation 3 applies in update 1), not 159744 as the plan's DLL-derived values (D-E19-13, the DLL reads the animation a frame late). The binary's X lags the plan's by 61440 units from update 2 on (0.94 px). Nothing relational +-1 px depends on it.
6. **`validate.out`**: the earlier claim "460 scenarios" is `validate.py 400` (400 random + 60 directed); the default run is 360. Both give 0 mismatch (`out/validate.out` is the 400 run).
7. **`m.py`**: see README.md section 3: the repo copy `research/census/walks-0x0b/m.py` can replace the scratchpad one.
8. The slice also lists the `1B` rise/fall of maps 174 and 181 (`1B [0,6]`, `1B [0,250]`) and the fall of 135 `C[9]`: **not replayed** (outside the list of this task); the programs are listed in `out/programs_listing.txt` for the next tranche.

## 3. Order of the tick: B (map events) and C (entity programs) **[R]**

| step | binary address | note |
|---|---|---|
| `UpdateWorld` | 0x8002E0F0-0x8002E114 | the world calls, in this order: `RunMapEvents` 0x8003C67C (`jal` at 0x8002E100), `UpdateEntities` 0x8003B388 (0x8002E108), the effects 0x8003C410 (0x8002E110) |
| `UpdateEntities` head | 0x8003B388-0x8003B398 | `0x800DC4B8 & 0x48` != 0 -> only the lists 0x800384F4 run (no script, animation or physics: menu, inventory, freeze) |
| then | 0x8003B3A0.. | 0x80038634 (destroyed entities), **0x800386D0 (entity scripts = the programs `C`)**, 0x80038998, **0x800384F4 (lists)**, **0x80038E18 (UpdateAnimation of every active entity)**, **0x80038364 (physics pass P)**, 0x80038E84, 0x80039300 |
| inside P | 0x800383B0-0x800383B8 | `+0x168`, `+0x140` (contact) and `+0x13C` cleared at the start of every pass: the contact lives only in the pass that raised it |
| | 0x8003847C / 0x800384BC | `MoveEntity` per entity (skipped when `+0x168 != 0`), then the end pass 0x80038064 (`IsOnGround` 0x800380F8-0x8003810C) |

So within one tick: **map programs (B) -> entity programs (C) -> lists -> UpdateAnimation -> physics**. A `1A` or `1B` written by either kind of program is applied by the pass of the **same** tick (update 1), and a `0x25` of either kind reads the pass of the **previous** tick. In the DLL the B pass is documented as running after the entity updates of the image (E19.h discovery notes section 6.2, O-E19-28 "ordre de mise a jour par entite au lieu de par creneau"); the call site is `AlundraWorldProxy.cs:2282` inside `Update`, and the real order against the entity proxies comes from the engine's update order: **not re-verified tonight**.

## 4. Decision note: is an extension of the emulator to the scene loop needed?

**Question** (`h2.json`, slice step 6 and author question 2): for the pins that remain after the unit-level values, is the emulation of the whole scene loop of the binary needed, or does the project method of D-E19-77 (E19.f2a: absolute arc pins written in advance by a simulation of the DLL in an out-of-repo copy, `docs/plan-e19-f3-annexe/sim-notes.md` section 0) suffice?

**Recommendation: no extension of the emulator to the scene loop. The unit-level values are produced; the absolute arc pins are DLL values that the binary's scene loop cannot give.**

Reasons:

1. **The values h2 needs at unit level are now measured without any scene loop.** UJ1b-binary, UJ-7w, TR-V exact, the ledge, Giles relational, and the A3 / bomb guards (section 1, `values.md`) come from the real physics pass, the real list builder, the real handlers 0x1A, 0x1B, 0x25, 0x37, 0x16, 0x63 and the real terrain probe on real cells, with the stand-in of `UpdateAnimation` that is exact for a jump with one animation change. The scene loop was never part of this chain of reasoning.
2. **The absolute frames of A10 / A12 / A10J are properties of the DLL arcs, not of the binary.** They combine the binary rule (which this annex gives as `s+23`, "update 1 at the tick of the program", "the second `1A` at the tick the first returns") with the DLL's own scheduling: the order of B and C, the pacing of the boxes and the polling phases of Wendell and Meade (the T102 620/621 incident of E19.d2c1 is exactly this). A MIPS emulation of the binary's scene loop would produce the *binary's* frames, which the DLL does not and is not meant to reproduce (accepted gaps O-E19-28 d-f). It is the wrong oracle for these pins; D-E19-77's simulation of the DLL (or a pin written relationally, then measured after the code) is the right one.
3. **What a scene-loop emulation could add is small and can be had separately.** The one binary fact that this annex could not establish is whether the second `1A [2]` of Bergus @839 (animation already requested once, chain `2 -> 4`) is given its impulse: it depends on `UpdateAnimation` (0x80038AB4, switch block entered by target, direction row or end of chain) run over the **real** sprite bytes of Bergus. A bounded harness for the real routine over synthetic sprite-info bytes already exists (`docs/plan-e19-o9-annexe/run_update_animation.py`); feeding it the real animset bytes is a small task (D1b), nowhere near the full loop (real UpdateAnimation + B/C scheduler + record appearance + dialogs, size L in `h2.json`).
4. **Cost and constraint.** The scene-loop extension is size L and the D-E19-77 method needs `dotnet`, hence "after the test campaign"; the relational route needs neither.

**Proposed use of D1 for the plan of h2** (to be decided by the author, question 2 below):
- unit pins written now from this annex: UJ1b-binary (`ForceZ` -327680 at update 21), UJ-7w (`0x25` at s+23, 20 executions for `37 [3]`), TR-V `== t+16`, ledge tick 5, Giles `F0+120` inside the existing +-1;
- A10: relational wording ("each jump: `0x25` executes from s+3 and returns at s+23; the second `1A` is at the return tick of the first") and measure the absolute frames after the code, or use D-E19-77 if the author wants them before; do not hand-derive them;
- optional D1b: Bergus' second impulse on real sprite bytes (not the scene loop).

**Limits of this decision**: it rests on the replays above being right for one animation change per jump; it does not cover the escalators/stairs guards (DLL side), the entities carried (`+0x28 != 0`, D5b territory) and the support by other entities (not loaded in the replays: one entity per world).

## 5. Measured / inferred / not done

- **Measured [M]**: everything in `values.md`; the ledge first tick; K = 19 for Giles given the assumed start; engines A and B agree on all of it; the Z-only cases also agree with three rules models.
- **Read [R]**: the order of the tick, the landing test, the end pass, the handlers 0x25/0x1A/0x1B (section 3 and `dll-delta.md` section 0).
- **Inferred [I]**: A10 second jump (hypothesis H), the DLL values of the 768 / 1024 `0x25`, A12 after h2, the later A10J pins; every one is marked where it appears.
- **Not done**: the DLL side of any value (no dotnet); the replays of the `1B` rise/fall of 174 / 181 and of 135 `C[9]`; `UpdateAnimation` on real sprite bytes; the scene loop; the XY walks before the jumps (injected); the support by other entities, the carried entities (`+0x28`), the slide tiles (`+0x188`).

## 6. Author questions of `h2.json`, unchanged

(copied verbatim from `docs/plan-e19-m-annexe/night-discovery-2026-10-09/h2.json`, `author_questions`, in the original French; numbering 1 to 4 = indexes 0 to 3)

1. Couplage avec O-E19-29 (troncature au pixel quand un PNJ atterrit) : le tri du 2026-10-08 vous pose deja la question 1 (A maintenant / B chapitre 18 / C jamais, recommandation du tri : A). Pour h2 : (1) h2 corrige-t-elle O-E19-29 au passage, ce qui bascule UJ1c et les epingles de Giles vers F0+257/268/269 ; (2) ou garde-t-elle exactement la troncature actuelle pour isoler la tranche ; (3) ou attend-elle votre reponse a la question 1 du tri ? Recommandation : (3), puis (1) si la reponse du tri est A ; h2 ne demarre pas avant.

2. Methode d'epinglage : D-E19-77 (E19.f2a) a fait ecrire d'avance par simulation hors depot les epingles d'arcs qui bougeaient. Pour h2, acceptez-vous (a) des epingles RELATIONNELLES (retour de 0x25 compte depuis l'instruction qui lance le saut, valeurs de zsim par saut) plus des valeurs absolues d'arcs seulement mesurees apres le code, (b) la methode D-E19-77 (port dans une copie hors depot de la DLL, suite rejouee, valeurs ecrites d'avance ; exige dotnet), ou (c) l'extension de l'emulateur MIPS a la boucle de scene (L) ? Recommandation : (b) pour A10, (a) pour le reste, et ne decider l'extension (c) qu'a la fin de D1.

3. Perimetre de l'etat en l'air : h2a (sauts scriptes : impulsion d'animation, 0x1B/0x5E, qui touchent la chaine) d'abord, puis h2b (chutes de rebord et d'escalier, visibles hors chaine) ? Et h2b est-elle distincte d'O-E19-40 (aimantation de descente de 4 px du moteur, 'plus tard (moteur)') ou l'absorbe-t-elle ? Recommandation : h2a puis h2b, h2b decidee apres la mesure du rebord.

4. Propriete des fichiers : confirmez-vous que h2 peut prendre AlundraScriptedMotion.cs, EntitySupport.cs et AlundraEntityScriptProxy.cs tant que D5b reste en pause et qu'O-E19-17 est differe, et qu'il passe apres la fin de la campagne de tests ?
