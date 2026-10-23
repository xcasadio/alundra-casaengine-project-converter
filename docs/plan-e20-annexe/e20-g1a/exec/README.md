# E20.g1a (carrying) - execution record

Plan: `docs/plan-e20-heros.md` section 1.24.1 and the block "Revision au HEAD (2026-10-18)" (which prevails over the text of 1.24.1 where they differ). Branch of the parent
`chantier/theme1-porter-plateforme`. Start HEAD of the execution: `b62726ff` (E19.d2c2 D5b merged: the passengers follow their carrier, ADR-0088).

## T1 (phase A) - state at the start, read at the start HEAD before any code

### The commun T0 is not on the branch

`docs/plan-e20-annexe/e20-g/` (the commun T0 of 1.24.0: oracle copied with its checksums, the fourth scenario `c4_climb_lift`, the 16 mutants of one word, the DLL probes P-G-1 to P-G-5, the
recount of the predicted list, the census of the maps of the recipe) does not exist, and no commit mentions it. T1 does not need it (its values are the vectors of the plan table "Valeurs attendues" and
of the models of `docs/plan-e20-annexe/e20-g1a/work/g1a_models.py`, which the independent critic replayed with 0 deviation on 3 000 vectors per mode, `check.md`); the T0 stays owed to the slice
(acceptance "Oracle: manifeste identique ; 16 mutants"). It is flagged in the report, not done here.

### Prerequisites re-read at the start HEAD

- The marker: `EntityScriptProxy.HasLogicalZ` (`Alundra/Entities/EntityScriptProxy.cs:1500`, `internal bool`), posed today by `WorldProxy` at `Alundra/Maps/WorldProxy.cs:3615` (the interface of the sprite
  index) and by nothing else; never cloned. `PutInHands` poses it (D-G-13); never removed.
- W1 to W4 of e0k2 exist as the plan says: W1 `ScriptedMotion.RunOneKinematicTick` (`Alundra/Physics/ScriptedMotion.cs:218-219`, `RunLogicalZPreXyStep`, Z before XY, magnet and `IsOnGround` after), W2 the support block
  of `EntityScriptProxy.TickPhysics` (`EntityScriptProxy.cs:793-801`, `:851-853`), W3 the pull of the root (`:1596-1602`), W4 the thaw (`GameplayFreeze.cs:141-142`) and the teleport (`EntityScriptProxy.cs:2566`); the
  speed key: `ScriptedMotion.cs:182` already reads `TargetAnimationId` for `HasLogicalZ` (g2a's extension is already there for the marker, to be re-read by g2a).
- D5b (E19.d2c2, 2026-10-24) is merged at the start HEAD: (w5) will be pinned in phase 0 with the value of UH-14 of this HEAD (T2's work).
- Stubs and places to wire (phase B, not touched by T1): `PlayerManager.PlayerTryAction` (`Alundra/Hero/PlayerManager.cs:940`, returns 0), `PlayerManager.UpdatePlayerCarriedEntity` (`:1285`, empty; called with 0 at
  `:408` and with 2 at `:659`), the locked branch (`:322-332`, no call of mode 1), `WorldProxy.DestroyEntity` (`Alundra/Maps/WorldProxy.cs:3342`, `:3371`, neither clears the carrier).
- Type set of C23 read in the export (`alundra-project/Data/sprite-records.json`, `ProgramTick == 23`): bank keys 35, 36, 38, 41, 48, 182 of the map page (sprite types `0x100 +` = 291, 292, 294, 297, 304,
  438) and `alundra_234` of the global page (type 234): 7 types, all with `ProgramDeactivate == 2` (E2). Confirms the plan ("`0x100 +` l'indice, [a confirmer par le T0]").
- The balance sheet: `EntityScriptProxy.BalanceRecord` (`.Attacks.Count` is the binary's `AttackCount`, byte `0xE`); a proxy without a record (a bare test montage) is read as `AttackCount` 0.

### Phases and what T1 adds (no behaviour change)

New: `Alundra/Hero/CarryRules.cs` (pure rules and the three modes of `UpdatePlayerCarriedEntity` as a function of a hero and its object), `Alundra/Ai/Objects/LiftableAi.cs` (C23, not registered),
`PlayerManager.PutInHands` (no production caller), the counter field of the hero. The stubs `PlayerTryAction` and `UpdatePlayerCarriedEntity` of `PlayerManager` stay stubs; nothing of `MovePlayer`,
`TickMotion`, `DestroyEntity` or the registry changes. Therefore **no existing test may move in T1**.

### Predicted list of the existing tests that move in the SLICE (recount at the start HEAD, by symbol)

None in T1 (phase A). For phases B and C of the slice (methods that reach `PlayerTryAction`, `UpdatePlayerCarriedEntity`, `CarriedEntity`, `PlatformEntity`, a type of the future wired set, `Flags & 0x600`, the registry
or the list of presets):

| Test (file : method) | Phase | Why / expected |
|---|---|---|
| `Ai/NativeSpriteRegistryTests : ProductionRunner_RegistryHoldsTheHandlersOf...` (`runner.NativeSprites.Count`) | C | red until its sum gains `LiftableAi.Keys.Count`; rewritten by derivation |
| `Ai/Projectiles/WaveSwordLifeTests : V3_TheProductionRegistryHoldsTheKeysOfItsClasses...` | C | same, the second pin of the registry |
| `Ai/Objects/PillarAiRegistrationTests`, `Ai/Projectiles/ArrowAiTests : E5_...`, `ChargedArrowAiTests : C2_...` | C | read the registry keys of their own class (own-class counts: expected green; to confirm by the measure) |
| `Ai/Enemies/BeannoideOracleTests : RunE1Call`, `MurutaOracleTests : RunE1Call` | B | (W5): a carrier without proxy in `DestroyEntity` (expected green, accolated) |
| `Hero/LadderClimbTests : CaseSixGate_CarryingEntity_DoesNotEnterClimbing` (+ `BuildLadderHero`, `MakeQualifyingIdlePlayer`) | B | a bare `Entity` in `CarriedEntity`: (W1) must not throw (expected green) |
| `Entities/EntityDestructionTests : TR2_...`, `Items/LootRollWorldTests.Rolls : M3_...` | B/C | a liftable in the list / a crate born by `SpawnEntityByRecordId` (C23 inscribed in phase C, inert when nothing is carried) |
| `Entities/EntitySearchServiceTests : FunctionId4_..., FunctionId11_...`, `NpcCharacterControllerMoverTests : EntitySearchService_Searches5And6_...` | B | read `PlatformEntity`; an object in hands now changes the searches 4 and 11 (positive neighbour to add) |
| `Physics/EntityCollisionTests : SubjectGate_...OrCarried...`, `Physics/MovementObstacleProbeTests : TR1_..., TRLift_...` | B | `PlatformEntity` / `PickupKindMask` readers (expected green) |
| `Entities/TerrainHeightTests` (witness, `:33-36` comment), `Entities/LogicalZStepTests :116`, `Maps/MapEdgeWorldTests :226` | B | witnesses of the marker; comments made false by g1a, corrected |
| `Arcs/*` on maps with crates (C23 inert), `Arcs/EntityContactArcTests : TH4_...`, `Arcs/VisionAndCoastArcTests : A3_...` | C | read counts of contacts; expected green |
| `SaveGames/TestSavesTests` (list of presets) | T7 of the slice | the preset `porter-m<N>` moves the pin of the list, by derivation |

Baseline of the suite at the start HEAD (Release, `--list-tests`): 5 662 tests (equal to the count closed by D5b T8).
