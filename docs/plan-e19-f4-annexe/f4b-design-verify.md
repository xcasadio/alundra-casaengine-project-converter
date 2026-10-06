# E19.f4b design report: adversarial counter-check

Read-only on the repository (HEAD 2909ae3; `git status --short` shows only the two pre-existing lines ` m CasaEngineMonogame` and ` ? alundra-datas-analyser`). Nothing was built, run, exported, edited,
staged or committed in the repository; `scratchpad\bak\` and `CasaEngine.Launcher/Program.cs` untouched. Everything I ran is in `scratchpad/f4b-design/design-verify/`.

## What I ran (my own code, not the report's)

| Script | What it checks |
|---|---|
| `v1_flight72.py` | 48x72 flight, three independent derivations: (a) closed form from the stated rule; (b) the ANNEX `F4Box`/`TextBox` run with MY OWN `Portrait` subclass and my own copy of the `scenarios.run` loop (names from the annex `names_widths.json`); (c) the 32 rows typed from the report; then (b) against `design/values-s8-72.json` (60 frames, every column) and h = 56 against the annex `values.json` S1 |
| `v5_regress_all.py` | my subclass at h = 56 against the annex `values.json`, S1..S7, every frame and column |
| `v4_s9_and_t1.py` | S9 (moving speaker, scrolling camera) re-run with my subclass against `design/values-s9-moving.json`; structural claims of the T1 contract on the annex `values.json` (null on the opcode row, S5 timeline) |
| `v2_binary.py` | own capstone loader on `ALUN_CD.EXE`: callers of the UI init `0x80044BE4`, of the camera follow `0x8002CDA0`, of the UI dispatcher `0x80048054`, of name/portrait openers and closers; disassembly of the init, of `0x8005A0C8`, `0x80057B64`, of RenderScene and of the close trigger |
| `v3_b_slot.py` | flat decode of every program of the 483 maps: any `0x40` with v1 = 1 (set the B program slot)? |
| `gen_copy/` | the report's generator re-run in a copy (byte-identical output), its 7 mutations re-run, 4 extra mutations of mine (`extra_mut.py`) |
| `annex_layout/` | the F4B-0 scripts dropped into the annex layout (`model/` + root files): they do not run |

## Verdicts

Legend: CONFIRMED / REFUTED / UNCONFIRMED. File:line are HEAD.

### Corrections au plan

1. **R4, resets at boot only: CONFIRMED.** Own disassembly: `0x80044C28` is `jal 0x8005A0C8` (writes `sh $zero,0x240(0x80180000)` = name block flags) and `0x80044C40` is `jal 0x80057B64`
   (`sh $zero,0x70(0x80180000)` = portrait state), both inside `0x80044BE4`; its only caller is `0x8002C238`, in the function starting `0x8002BFE0` (called once, from `0x800816A8`),
   before the loop head `0x8002C3F4` (which calls RenderScene `0x8002BD60` at `0x8002C3FC`). No indirect reference to `0x80044BE4` (no word, no lui/addiu pair). `0x8005A0C8` and `0x80057B64`
   have no other caller. `dll-notes.md:64`, `:116` and `dll-verify.md:22` (claim 9, "map load pipeline") are wrong on that point, and so is the plan's R4 wording.
   - The three DLL resets are DLL choices, forced because each already resets the box: `InstallForMapEntry` `:234`, `NotifyPresenterClosed` `:530`, `ResetForTests` `:612`. CONFIRMED.
   - The load needs no new site: CONFIRMED. `AlundraWorldProxy.cs:712` (`ApplyPendingLoad`) runs BEFORE `:728` (`InstallDialogueSystems` -> `AttachToWorld` + `InstallForMapEntry`); doc at `AlundraSaveGameDirector.cs:366`.
2. **Existing tests that do not move: CONFIRMED.** `IAlundraDialogueBoxHost`: one implementer (`AlundraDialogueDirector.cs:111`), 0 hit in `Alundra.Tests`, the box is built only at `:121`. `IAlundraDialogueDirector`: one implementer,
   the test fakes only expose a property of that type. Nothing is added to `IEntityWorldContext` (29 to 30 implementers, none touched). `AttachToWorld` untouched. Nits: my count of dialogue-director `AttachToWorld` test calls is 44 (not 43);
   `new AlundraInventoryPortrait()` appears 6 times (`AlundraInventoryPortraitTests.cs:31,54,81,152,172`, `AlundraInventoryPortraitWiringTests.cs:265`), not 3; the conclusion (keep the public parameterless constructor) stands.
3. **S8 and S9, F4B-0: CONFIRMED in principle**, with the amendments of the "Corrections" section below (S8 label, scripts that do not run from the annex layout).

### (A) T1 contract

| Claim | Verdict | Evidence |
|---|---|---|
| Director-level montage, binary order (move world, `Pass(true,true)`, script phase, read `Box.Phase`) | CONFIRMED | same order as annex `scenarios.run()` (`box.render`, `nb.pass_`, `pt.pass_`, then the opcode attempt); `Pass(true,true)` = `OraclePadEveryFrame` ("pad A", `AlundraTextBoxOracle.cs:961`), already used on the DLL director at `AlundraTextBoxWiringTests.cs:122,131` |
| f -> iteration f, n0 = 3, rel = f - 3; name and portrait null on the opcode row | CONFIRMED | all 7 scenarios: null on the opcode row, first draw on the next row (S1 name `[320,140]`, portrait `(160,'in')`) |
| "opened" iff `OpenSerial` grew (== `state.CodeIndex == size`); retry = `CodeIndex == 0` | CONFIRMED | `RunOneScriptCall`: `Dispatch` result 0 -> `EndCall`, else `CodeIndex += result`; `Open` does `OpenSerial++` (`AlundraDialogueDirector.cs:253`) |
| S5: a program with two `0x0D` would retry in the SAME iteration | CONFIRMED | `RunOneScriptCall` loops on after a size return (`state.CodeIndex += result`); the annex driver tries one dialogue per frame |
| `phase` compared, guard column | CONFIRMED | `AlundraDialogueBox.Phase` strings (`:172-196`) = the model's set {closed, slide-in, typing, typed, slide-out} |
| `box_y` not compared; S5 frame 43: model 168 vs DLL 240 | CONFIRMED | annex S5 frame 43 `box_y` 168; `AlundraDialogueBox.Open()` writes `Y = ClosedY` (`:281`); release sets `Y = OpenY` (168) so every other row agrees. Amendment: it differs on ONE row only, so `box_y` could be compared everywhere except S5 frame 43 (optional) |
| name: constants 140 / 148 / 34; `FrameX`, `TextX` compared | CONFIRMED | all rows of the annex file |
| clip not compared (moot) | CONFIRMED | my brute force over the 60 real names x the 16 + 16 slide positions: 0 cut, minimum margin 12 px |
| portrait `phase` in {in, rest, out, gone}; `rgb`, `x,y,w,h`; need for a "drawn this step" flag distinct from 0 x 0 | CONFIRMED | annex file: row N+1 is a drawn 0 x 0 (non-null); the DLL machine `Step()` cannot tell it from idle (`AlundraInventoryPortrait.cs:141-147`) |
| events: opcode result / sound 7 / IsOpen falling | CONFIRMED | `EvaluateClose` plays 7 (`:529`), `Released` at `:424`. Amendment: in the annex file the opcode event comes BEFORE the pass notes of the same row (S5 frame 43: `['opcode 0xd -> opened','DialogClosed']`) although the pass ran first; compare the events as a set, or build the list opcode-first |
| set-up helpers (`AdvanceProviderForTests`, `SetEtcDialogueAssetForTests`, `BuildEtc`, `OraclePages.ByteOf`, `GlyphWidth`, `ValuesPath` walk-up) exist | CONFIRMED | `AlundraDialogueDirector.cs:176`, `AlundraEtcStringTable.cs:221`, `DialogueTestAssets.cs`, `AlundraTextBoxOracle.cs:801,840`, `AlundraChoiceBoxTests.cs:21-37` |
| Risk 2 (widths: Jess 21, Septimus 51, the 60 names) | CONFIRMED resolved | the C# oracle `Widths` table is identical to the model `WIDTHS` (256 values); summing it over the 60 names reproduces `names_widths.json` `w` (0 mismatch). The PRODUCTION font3 (`alundra-project/UI/font3.fnt`, `AlundraFont3Advances`) also gives the same 60 widths (0 mismatch) |
| Risk 1 (DLL box on "AB" under `Pass(true,true)`: E N+21, T N+22, release N+40) | UNCONFIRMED (not runnable) | the annex values and f2a O-03 give it; the C# oracle test `AlundraTextBoxOracleTests.cs:109` (`O03`) pins exactly (21, 22, 40) for the oracle; DLL-vs-oracle comparison exists only for the arcs pad. Likely, but the first red-to-green run decides; the `phase` column will show it at the first transition |
| The proxy's camera is not settable to (40,20) | UNCONFIRMED (irrelevant) | not checked; the director-level choice does not depend on it |

### (B) The 48 x 72 flight

- **The table: CONFIRMED.** (a) my closed form, (b) my own subclass through the annex `TextBox`, (c) the 32 rows typed from the report, (d) `design/values-s8-72.json` (60 frames, every column): 0 mismatch between all of them.
  Rest rows N+16..N+21 are `8,100 48x72 128 rest`. Control at h = 56: my subclass reproduces the annex `values.json` S1..S7 (7 scenarios, every frame, every column, 0 mismatch); the report's `check_regression.py` also gives 0.
  Sizes (0,0) (3,4) (6,9) (9,14) (12,19) (16,24) (19,28) (22,33) (25,38) (28,43) (32,48) (35,52) (38,57) (41,62) (44,67) = the plan's list.
- **The rule: CONFIRMED as the only one consistent with D-E19-49 ("entiers, au même endroit, bas aligné", `plan-e19-opcodes.md:186`) and the binary's top-left anchor** (the alternative "keep (8,116) and draw 72" puts the bottom at 188).
  It is still an extension with no binary truth: `dll-notes.md:115` (3.1) reaches the same rule and marks it `[hyp]`, so it is not independent evidence. Record it as a session decision to confirm by the author (like D-E19-89), next to the plan's "(choix dérivé)".
  The degenerate pass position at h = 72 (8,100) is invisible (0 x 0); any other value would be as valid, so say it is a pinned convention.
- Instance members vs `public const RestX/RestY/FullWidth/FullHeight` (`AlundraInventoryPortrait.cs:35-40`): CONFIRMED collision. `AlundraInventoryViewModel.cs:182-185` reads those constants and `X/DrawnWidth`: unaffected by an additive overload.

### (C) DLL inputs

- **(a) Portrait data on the proxy: CONFIRMED.** `AlundraEntityScriptProxy` has no prefab id nor header; the header is resolved only in `ApplySpawnInitialization` (`AlundraEntitySpawnFactory.cs:559-572`, `SpriteType` `:629`) and `ApplyHeroSpriteHeader` (`AlundraWorldProxy.cs:1636-1641`); these are the only two writers of `Flags`/`SpriteType`
  (`rg FlagsPortraitShadowType`, `rg "\.SpriteType\s*="`). `Clone` is `:2334-2380`. The hero header has `FlagsPortraitShadowType = 3` (bit 0x80 clear) in `alundra-project/Data/sprite-records.json`; 25 prefabs have the bit. `SpriteRecordHeader` is a public readonly struct with `init` members: a nullable member is fine.
  Gap: F4A-R3 does not name the type, and f4a runs first and creates the DLL mirror. The type name (`DialoguePortraitRef`) and the member name must be fixed in F4A-R3, or the f4b design and f4a disagree. Flags are also writable at run time (`AlundraEventProgramRunner.cs:2205`, `:2227`: `match.Flags |= / &= `), so "flag without field" and "field without flag" are reachable by script: the report's tests 4 (flag decides) cover it.
- **(b) Camera scroll: CONFIRMED.** Property on the director, not a member of `IEntityWorldContext`, not a 4th parameter of `AttachToWorld` (re-attached at `AlundraWorldProxy.cs:1267`, 44 test calls, "re-points without touching state"). Formula `AlundraCameraMath.ToOriginalScrollSpace` (`:300`), precedent `:1510`.
  Frame order in `Update`: pad loop (`:2104-2144`) -> dialogue `Pass` loop (`:2181-2190`) -> map events -> camera block (`:2277-2313`): `Pass` and entity scripts both see the previous frame's camera. Binary order CONFIRMED by my disassembly: the camera follow `0x8002CDA0` has ONE caller, `0x8002BDB8`, inside RenderScene `0x8002BD60`, BEFORE `jal 0x80048054` (UI dispatcher) at `0x8002BE5C`;
  RenderScene has one caller (`0x8002C3FC`, the loop). Note "source null = (0,0)" is the test seam only; production with no resolved camera gives `ToOriginalScrollSpace(0) = (-160,-120)` (same fallback as the hero portrait, `:1510`).
- **(c) Close-trigger hook: CONFIRMED.** One site: `_flags |= 2` exists only at `AlundraDialogueBox.cs:528` (all `_flags` writes: `:282`, `:302`, `:417`, `:422`, `:528`), reached only through `EvaluateClose` (`:436`, `:500`). Binary order at the trigger CONFIRMED: `0x80045EF8` `jal 0x800490FC` (a0 = 7, the sound), `0x80045F00` `jal 0x80059FE0` (name close, its only caller), `0x80045F08` `jal 0x80057B84` (portrait return; 4 other callers = inventory exits).
- **Opcode side: CONFIRMED.** `Dispatch(command, logic, entity, ...)` passes the LOGIC entity (`RunOneScriptCall`: `logic = entity.LogicEntity ?? entity`); the three binary openers are the only callers of the name opener (`0x8003D640`, `0x8003F104`, `0x80041EA0`) and of the portrait open (`0x8003D634`, `0x8003F0F8`, `0x80041E84`). Position after the `HasPresenter` exit (`:1885-1894`) and before `if (director.IsOpen)` (`:1896`): CONFIRMED by the annex values themselves: S5 frames 40-42 show the 2nd dialogue's portrait and name opening on RETRY ticks (portrait 'in' at frame 41 while the box only opens at frame 43).
  Existing speaker opcode tests stay green: bare `new AlundraEntityScriptProxy()` logic entity (SpriteType -1, Flags 0), empty `SpawnedEntities`, decoy search `0x84` (function 4 over an empty list).

### (D) Advisories

1. **Ignored open keeps the last accepted speaker: CONFIRMED** (model `if self.pt.open(...): self.speaker = entity`). Mutation "speaker replaced on an ignored open" changes 33 of 40 digests; "height of an ignored open leaks" 26 of 40.
2. **Destroyed speaker: CONFIRMED.** `RecycleDestroyedEntities` (`AlundraWorldProxy.cs:2550`) only sets `Status`/`EntityRefId` and removes from the list; `OnEndPlay` of the proxy is empty; `GameplayBlockedMask = MenuOpen | Unused40 = 0x48` (`AlundraGameState.cs:90`) does not contain `MessageBox` 0x10, so recycling is held back in mode 0 only.
3. **`ResetForTests` must clear name, portrait instance, once-warned set, `ScrollSource`: CONFIRMED** (`:604-633` clears every session field today).
4. **Random sequences: CONFIRMED.** `gen_sequences.py 40` re-run in a copy: `sequences-raw-digests.json` byte-identical. SplitMix64 seed 1, first five outputs recomputed by me: equal to the stored vector (`10451216379200822465`, `13757245211066428519`, ...). The 7 mutations: 15, 30, 33, 35, 40, 40, 40 of 40. Mine: the two ignored-open slips above, "name close ignored while sliding in" 33/40, "return reads the camera of the open" 40/40; "portrait close without state test" 0/40 (a true no-op, as it should be).
5. **Corpus classes: counts CONFIRMED, one justification REFUTED.**
   - `speaker_sites.tsv`: 2856 rows = 2847 distinct (map, op, pc) + 9 extra rows (5 shared programs run by several owners). `speaker_sites_retarget.tsv` = 574 triples (238 `0x0D`: 235 + 3, 330 `0x5C`, 6 `0xC4`) matching 580 rows; the rest is **2273 distinct sites = 2276 rows**: say which the corpus test counts (rows: one expected class per owner).
   - From `corpus_classes.json`: 0 sites with a `0x42` and no `0x43` in their program. CONFIRMED.
   - 0xC4 explicit names: 31 sites, 8 ids (0x111, 0x112, 0x11D, 0x16E, 0x174, 0x19B, 0x1B0, 0x1BC): all in range and present in `names_widths.json`. CONFIRMED. All `has_portrait` rows are in range with an ETC name. CONFIRMED.
   - **Refuted justification**: "la logique entité du héros est remise à zéro à chaque événement de carte". `RunMapEventsPass` sets `player.LogicEntity = mapEvent.Entity ?? player` and writes `mapEvent.Entity = player.LogicEntity` back after each run (`AlundraWorldProxy.cs:2491`, `:2497`); it is reset to the hero only when the hero leaves the zone (`:2476`). A retarget therefore PERSISTS from tick to tick (and across programs of the same map event).
     The program-local exclusion for `unowned:B` is nevertheless sound on this corpus for another reason: the only way a map event changes program is `0x40` with v1 = 1 (`AlundraEventProgramRunner.cs:769`, `mapEvent.ProgramBMap = ...`), and my flat decode of every program of the 483 maps finds 0 such instruction (v1 is 2..5 only: A2 103, C2 285, C3 16, C5 1, E2 2, E4 20). State that as the reason.

### F4B-0 files

- Content CONFIRMED (annex `f4_model.py`, `model.py`, `scenarios.py` are byte-identical to the design's copies; the annex `values.json` equals `design/values_annex.json`).
- **REFUTED as "ready to drop in"**: `common.py` and `gen_sequences.py` read `names_widths.json` from their own folder, `check_regression.py` reads `values_annex.json` from its own folder, `flight72.py`/`scenario_s9.py` write next to themselves. Dropped into `docs/plan-e19-f4-annexe/model/`, `check_regression.py` and `gen_sequences.py` fail with FileNotFoundError (I ran it). F4B-0 has to fix the paths (`../names_widths.json`, `../values.json`) and run the scripts in place before the commit.
- S8 label: `values-s8-72.json` title says "Miming 0x17A style entity" but every name column is Jess (0x104, text x 365). The two real 72-high speakers are Miming 0x17A (w 44, bank 122) and Melzas 0x1A2 (w 39, bank 162) (`portraits_table.tsv`). Either fix the label ("S1 with h = 72") or run S8 on 0x17A so that the 72 portrait and a real name width meet.

## Anything missed

1. **Stale comment targets.** The plan (R task text) and `dll/notes.md` say to update the header of `AlundraDialogueSpeakerOpcodeTests.cs` ("ignored until E19.f"). That file has NO such comment (`rg "E19\.f|until E19|ignored"` on it: no hit). The comments that are stale are in `AlundraEventProgramRunner.cs`: `:1035-1036` (0x5C: "v[1] ... only for the deferred portrait/name box (E12.c) - ignored for display here") AND `:1043-1044` (0xC4). The plan names only `:1043-1044`; add `:1036` and drop the test-header edit.
2. **Logging level.** The arcs assert zero logged ERRORS (`AlundraArcChecks.cs:63`, `AlundraPrefabArcSupportTests.cs:81-82`, and `:133` pins `log.Errors.Count == 6463`). The new code (name box, portrait, once-warning, font3 missing, ETC missing) must log at Warning at most. The report's "expect new log lines, not new failures" is true only under that rule; put it in the plan.
3. **Order of the refusals / ETC access.** `OpenSpeaker` should test busy, then the id range, THEN resolve the ETC text. Otherwise every bare entity (SpriteType -1, hero 0, common sprites: 190 of the 2847 sites) and every retry tick calls `AlundraEtcStringTable.TryResolveText` (cache lookup, plus `LogFailureOnce` when no ETC asset is loaded, as in `AlundraDialogueSpeakerOpcodeTests` today).
4. **Production width tie.** T1 uses the oracle provider (`AdvanceProviderForTests`), so nothing ties the production `AlundraFont3Advances` to the 60 binary widths, although R2 asserts "égales aux largeurs du binaire". I checked that the real `UI/font3.fnt` gives all 60 (0 mismatch), so one corpus test (project root found like `FindProjectRoot()` in `AlundraDialogueSpeakerOpcodeTests.cs:179`) would be green and would pin it.
5. **`NotifyPresenterClosed` guard.** It returns at once when the box is not active (`AlundraDialogueDirector.cs:525`). Say where the name/portrait reset sits (inside the guard, like the box; then a name/portrait left by an abandoned retry with no box is NOT cleared by an out-of-band close, only by the map entry).
6. **F4A-R3 must fix the DLL type name** (see (C)(a)).
7. **Allocation on retry ticks.** `EntitySearchService.GetMatchingEntitiesBySearchType` allocates a `List` per call (`EntitySearchService.cs`), and `0x5C`/`0xC4` now call it on every retry tick while a box is up (up to ~40 ticks in S5). The repo states a no-per-frame-allocation rule (`AlundraEventProgramRunner.cs:500`, scratch-buffer doc). Low impact, but note it or reuse a buffer.
8. **Closed file list.** The acceptance says "liste fermée" but R1..R4 name only the runner. The production files this design touches: `AlundraEventProgramRunner.cs` (opcodes, `OpenDialog` signature), `AlundraDialogueBox.cs` (host member + one call), `AlundraDialogueDirector.cs` (`OpenSpeaker`, `ScrollSource`, `Pass`, 3 resets, `ResetForTests`, host member), `AlundraInventoryPortrait.cs` (overload, `Rgb`, `Phase`, drawn flag), new `AlundraDialogueNameBox.cs`, `AlundraEntityScriptProxy.cs` (field + `Clone`), `AlundraEntitySpawnFactory.cs` (one line), `AlundraWorldProxy.cs` (`ScrollSource` wiring), and f4a's `SpriteRecordCatalog.cs` type. Put them in the plan.
9. `design/notes.md` (scratch) still says "`AttachToWorld(..., scrollSource: ...)` new optional 4th parameter"; the final report and `dll/notes.md` choose the property. Only the scratch note is stale; do not paste it.
10. Corpus test and shared programs: rows with the same (map, op, pc) but different owners (5 sites, 9 extra rows, e.g. map 143 pc 2066 has 4 owners): iterate rows, not triples.
11. The plan's R1 "une ouverture refusée ne fait rien": consistent, but add that the openers run on retry ticks BEFORE the already-open test is what makes S5 frames 40-42 true (the S5 values are the test of it).

## Files

- `C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\f4b-design\design-verify\` : `verify.md`, `v1_flight72.py`, `v2_binary.py`, `v3_b_slot.py`, `v4_s9_and_t1.py`, `v5_regress_all.py`, `gen_copy\` (copy + `extra_mut.py`), `annex_layout\` (the path test).
