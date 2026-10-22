"""R5 discovery: the manual decisions, applied by build.py over the rule of the author's answer to Q-R-3 (D-R-13, D-R-19; ADR-0080).

The rule, in the order build.py applies it to a test file (the first that decides wins):
  1. STRUCTURAL (this file): the 26 end-to-end scenarios and trace harnesses go to `Arcs` (D-R-13 point 3, D-R-19), the tests of the test tooling
     and the module initializer to `Support`, the two export guards without a DLL type to `ExportGuards` (D-R-19). These do not depend on a name.
  2. NAME-TYPE: a file goes to the folder of the DLL type it is named after (the longest DLL type name that is a CamelCase prefix of the file
     stem, `Alundra` prefix and `Tests`/`Support`... suffix removed). D-R-13 point 1: "by the type of its name", not by the domain it exercises.
  3. NAME-WORD: the first CamelCase word(s) of the stem name a DLL folder (`Hero...` -> Hero, `Dialogue...` -> Dialogues).
  4. SUBJECT (this file): the name names neither a type nor a folder; the discoverer's judgement of the subject, each with its reason. Applied
     only when 2 and 3 decide nothing, and listed with the basis `subject` in r5-files.tsv so that the author can contest it.
  5. REFS: the folder with the most distinctive references (IL and lexical, idf-weighted), basis `refs`; also listed as not decided by a name.
Files without any test (helpers) go to the folder of their users (build.py), whatever their name.

STRUCTURAL / SUBJECT: key = path under Alundra.Tests/, value = (folder, reason, alternative or '').
"""

ARC = 'Arcs'
R_ARC = 'end-to-end scenario on real exported map(s) with the export prefabs and the real hero, or golden-trace harness (class doc "arc")'

STRUCTURAL = {}
SUBJECT = {}


def st(files, folder, reason, alt=''):
    for f in files.split():
        STRUCTURAL[f] = (folder, reason, alt)


def sj(files, folder, reason, alt=''):
    for f in files.split():
        SUBJECT[f] = (folder, reason, alt)


# ------------------------------------------------------------------------------------------------------------------ Arcs: 23 scenarios + 3 harnesses
st('''AlundraBeannoideGrateArcTests.cs AlundraBergusJumpArcTests.cs AlundraCabinWalkTests.cs AlundraDay3SceneArcTests.cs
AlundraEntityContactArcTests.cs AlundraFlailBounceArcTests.cs AlundraHeroJumpArcTests.cs AlundraInoaAwakeningArcTests.cs
AlundraInoaDayOneArcTests.cs AlundraLeverArcTests.cs AlundraLeverBasculeArcTests.cs AlundraLeverBridgeArcTests.cs AlundraLeverDoorsArcTests.cs
AlundraLeverPillarArcTests.cs AlundraLeverSmokeTests.cs AlundraMurutaHallArcTests.cs AlundraSailor12EndOfIntroTests.cs AlundraShipArcTests.cs
AlundraShipBlockArcTests.cs AlundraShipCorridorArcTests.cs AlundraTarnMurggArcTests.cs AlundraVisionAndCoastArcTests.cs AlundraVisionArcTests.cs
HeroTraceHarnessTests.cs IntroTraceHarnessTests.cs Map389LoadProgramsTests.cs''', ARC, R_ARC)

# ------------------------------------------------------------------------------------------------------------------ Support: tests of the tooling itself
st('AlundraArcGuardAndSoundHostTests.cs', 'Support', 'permanent test of the guard T-REG-0 of ArcSupport and of the sound host: it tests test tooling, not a scenario', 'Arcs')
st('AlundraArcInventoryHelpersTests.cs', 'Support', 'tests the inventory helpers (Give, Equip, SetMp, SetItem) of ArcSupport: test tooling, not a scenario', 'Arcs')
st('AlundraPrefabArcSupportTests.cs', 'Support', 'self-test of the "Prefabs" arc mode of ArcSupport (guard against a silent fallback): test tooling, not a scenario', 'Arcs')
st('AlundraScriptAssemblyRegistration.cs', 'Support', 'module initializer of the test assembly (no user, runs by itself): not a test, not tied to a domain', '')

# ------------------------------------------------------------------------------------------------------------------ ExportGuards (D-R-19)
st('AlundraCornerTrackExportGuardTests.cs', 'ExportGuards', 'guard on the exported anim2d of a chest; references no DLL type (D-R-13 point 7, D-R-19)', '')
st('AlundraZeroDurationLoopExportGuardTests.cs', 'ExportGuards', 'guard on the exported zero-duration loop animations; references no DLL type (D-R-13 point 7, D-R-19)', '')

# ------------------------------------------------------------------------------------------------------------------ SUBJECT: the name decides nothing
sj('AlundraClassAndBranchOpcodesTests.cs', 'Events', 'class/branch opcodes through the real interpreter (Entities 50 % / Events 46 %: tie, opcode name decides)', 'Entities')
sj('AlundraTurnOrderTests.cs', 'Events', 'opcodes 0x5A/0x5B walk order through the interpreter (Rng 36 / Entities 29 / Events 29: three-way tie, opcode decides)', 'Rng or Entities')
sj('AlundraMissingAnimationWaitCorpusTests.cs', 'Events', 'static guard walking every event program of the export for animation waits (Entities 53 / Events 46: tie)', 'Entities')
sj('AlundraHeightTargetSitesTests.cs', 'Events', 'opcodes 0x22/0x23 on the real sites through ArcRun; GameState only sets flags; twin of HeightWaitOpcodes (Events); the critic refuted Session', 'Session')
sj('AlundraDestroyedEntityRecyclingTests.cs', 'Maps', 'subject WorldProxy.RecycleDestroyedEntities (Maps 38 / Entities 37: tie; the critic found the subject in WorldProxy)', 'Entities')
sj('Scripts/AlundraAssetHandleReleaseTests.cs', 'Maps', 'its 3 tests call WorldProxy (TryBuildNavigationGrid, OnEndPlay); the critic refuted UI/Hud', 'UI/Hud')
sj('AlundraNewGameEntryTests.cs', 'Maps', 'subject WorldProxy.AdoptPlayerPawn (Session 27 / Hud 25 / Audio 22: three-way tie; the critic found the subject in WorldProxy)', 'Session')
sj('AlundraEnemyGroundTests.cs', 'Entities', 'floor/corner heights of entities other than the hero (EntityScriptProxy 87 %); the Enemy word is the E14 plan name', 'Ai/Enemies')
sj('AlundraWaveSwordDamageTests.cs', 'Combat', 'damage of the wave by the pure Damage function (Combat 99 %)', 'Ai/Projectiles')
sj('AlundraWaveSwordLifeTests.cs', 'Ai/Projectiles', 'life of the sword wave (WaveSwordAi)', 'Entities')
sj('AlundraWaveSwordSupport.cs', 'Ai/Projectiles', 'oracle + run of the sword wave, used by the WaveSword tests', 'Entities')
sj('AlundraWaveSwordWiringTests.cs', 'Ai/Projectiles', 'sword wave born by the production seam (WaveSwordAi); Hero 32 / Entities 26 / Items 17 by refs', 'Hero')
sj('AlundraBeannoideOracleTests.cs', 'Ai/Enemies', 'native A1/C1/D1/E1 of the Beannoid (BeannoideAi) against the oracle', '')
sj('AlundraMurutaOracleTests.cs', 'Ai/Enemies', 'native A0/C10/D9/E1 of the Muruta (MurutaAi) against the oracle', '')
sj('AlundraInteractionPassTests.cs', 'Hero', 'player interaction chain at its production sites (HeroController 25, PlayerManager); Entities 43 / Hero 30: tie', 'Entities')
sj('AlundraFlailBounceWallProbeTests.cs', 'Hero', 'wall probes of the flail bounce: same subject as FlailBounceTests (PlayerAnimationEffects), ICellMutator only incidental', 'Cells')
sj('AlundraLoadingMapAirArrivalTests.cs', 'Hero', 'LoadingMap case of MovePlayer with two real portal arrivals (Entities 41 / Hero 35: tie)', 'Arcs or Entities')
sj('AlundraHitEffectWorldTests.cs', 'Combat', 'effects 4 and 5 of the HIT PASS on the real world (E20.f3b); sibling of HitPassEffectTests', 'Rendering/Effects')
sj('AlundraCharacterControllerAdoptionTests.cs', 'Physics', 'hand-built pawn adopting the controller + collision field (E3.d) (Entities 49 / Physics 21)', 'Entities')
sj('AlundraMapEdgeClampTests.cs', 'Physics', 'pure clamp of a step to the grid (ScriptedMotion 5, Physics 44 %)', 'Entities or Maps')
sj('AlundraTextBoxOracle.cs', 'Dialogues', 'C# port of the dialogue text-box model (DialogueBox); no DLL type referenced', 'UI/Messages')
sj('AlundraTextBoxOracleTests.cs', 'Dialogues', 'the oracle\'s own tests: same folder as the oracle', 'UI/Messages')
sj('AlundraFont3GlyphTests.cs', 'UI/Speech', 'dialogue box text path DialogueScreen + DialoguePresenter wiring (sibling of DialoguePresenterFontTests); no DLL type referenced', 'Dialogues')
sj('UI/AlundraDialogueScreenAssetTests.cs', 'UI/Speech', 'DialogueScreen assets (DialoguePresenter 14 refs, UI/Speech 91 %)', 'Dialogues')
sj('UI/AlundraCursorAnimationTimingTests.cs', 'UI/Inventory', 'the inventory cursor animation (class doc); no DLL type referenced', 'UI/Shared')
sj('UI/AlundraScreenBindingReleaseTests.cs', 'UI/Shared', 'HUD + inventory (+ text box) screens: cross-screen release of MGUI bindings (Hud 58 / Inventory 30 / Messages 11)', 'UI/Hud')
sj('UI/AlundraScreensFollowTheWindowTests.cs', 'UI/Shared', 'the four full-screen screens (HUD, inventory, sub-inventory, save): cross-screen', 'UI/Hud')
