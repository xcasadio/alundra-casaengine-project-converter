#nullable enable
using System;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// T2 (docs/plan-transitions-carte.md §1.5/§3, "Gel global du monde") - ORACLE 1 (headless), on the
/// REAL map 389 export, driven by the same production call site and montage as
/// <see cref="AlundraDialogueOpcodesProductionTests"/> (a real sailor's own F(Interact) program opens a
/// box with <see cref="AlundraGameState.PlayerControlBits.MenuOpen"/> posed - the SailorThirteen mono-line
/// program precedent, cheap and deterministic, unlike the full 1704-frame flag chain SailorTwelve needs).
///
/// No assertion on the player, the camera or rendering anywhere in this file - <c>HeadlessIntroSimulation</c>
/// makes none of them vary (its own class doc says so), so any such assertion would be vacuous. That half
/// of T2's acceptance belongs to ORACLE 2 (<see cref="AlundraWorldProxyGlobalFreezeTests"/>), which drives
/// the real <see cref="AlundraWorldProxy.Update"/> instead.
///
/// The frozen NPC's own pose/force fields are SEEDED directly (same technique as
/// <see cref="AlundraInteractionPassTests"/>'s <c>NewCollidable</c>/<c>player.XCollisionEntity = sailor</c>)
/// rather than left to sailor 11's own real Tick-program walk timing: that walk is real (see
/// <see cref="IntroTraceHarnessTests"/>'s own frame-620-634 derivation) but tying a freeze window to it
/// would make this file depend on the WHOLE 1704-frame flag chain just to reach it. Seeding
/// <see cref="AlundraEntityScriptProxy.TargetForceX"/>/<see cref="AlundraEntityScriptProxy.ForceX"/>
/// directly exercises the exact same production code
/// (<c>AlundraScriptedMotion.TickScriptedNpc</c>'s <see cref="AlundraEntityScriptProxy.Controller"/>-null
/// branch, AlundraScriptedMotion.cs:233-241 - "production-safe... what the pre-E3 hero used", per that
/// method's own doc) with a motion that keeps going every tick until frozen, instead of stopping on its
/// own after a fixed real distance.
/// </summary>
public sealed class AlundraGlobalFreezeEntityUpdateTests : IDisposable
{
    private const string WorldName = "Ship Klark (beginning)-389";

    public AlundraGlobalFreezeEntityUpdateTests() => AlundraDialogueDirector.Instance.ResetForTests();

    public void Dispose() => AlundraDialogueDirector.Instance.ResetForTests();

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraGlobalFreezeEntityUpdateTests: no 'alundra-project/Maps' directory found above "
            + $"'{AppContext.BaseDirectory}' - these tests need the real converter export of map 389.");
    }

    /// <summary>
    /// Mutations caught here (docs/plan-transitions-carte.md §3, T2's own mutation table):
    /// "retirer la porte" and "omettre les passes de motion de AlundraEntityScriptProxy.Update du côté
    /// gelé" both surface as the SAME symptom - the NPC's pose/program-pick state changes while the box
    /// is open - so one test covers both. "mettre SyncAnimation dehors" is covered by the SAME run (a
    /// third, independent field on the SAME entity), since a mismatched TargetAnimationId/CurrentAnimationId
    /// pair only needs to sit still, never a real walk, to prove SyncAnimation didn't commute it.
    /// </summary>
    [Fact]
    public void SailorBox_Open_FreezesNpcPoseProgramPickAndAnimation_BoxStillClosesNormally()
    {
        var projectRoot = FindProjectRoot();
        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        Assert.NotNull(document);
        // Ground truth reused from AlundraDialogueOpcodesProductionTests' own SailorThirteen test: masked
        // F index 13 -> code offset 1660, the real mono-line "0x27 ; 0x0D ; 0x05 ; 0xFF" program.
        Assert.Equal(1660, document!.EventCodesFTable[13]);

        var previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = projectRoot;
        try
        {
            var sim = new HeadlessIntroSimulation(projectRoot, WorldName, document, installDialogueDirector: true);
            var director = AlundraDialogueDirector.Instance;

            AlundraEntityScriptProxy? sailorEleven = null;
            AlundraEntityScriptProxy? sailorThirteen = null;
            int posXAtFreeze = 0, posYAtFreeze = 0;
            int codeIndexAtFreeze = 0;
            var frozenHeld = true;
            var closedOnButton = false;

            sim.RunFramesForTest(60, s =>
            {
                s.GameState.LastPadState = default;

                if (s.Frame == 1)
                {
                    AlundraDialogueDirector.Instance.AttachToWorld(new DialogueService(), s.GameState);
                    AlundraDialogueDirector.Instance.InstallForMapEntry();

                    sailorEleven = s.SpawnedEntities.Single(
                        e => (e.ProgramIndexes[ScriptHelper.ProgramCTick] & 0x7f) == 11);
                    sailorThirteen = s.SpawnedEntities.Single(
                        e => (e.ProgramIndexes[ScriptHelper.ProgramFInteract] & 0x7f) == 13);
                }

                if (s.Frame == 2)
                {
                    // Real production dispatch, same site as
                    // SailorThirteen_MonoLineInteractProgram_BoxClosesOnButton_NoSoftlock: opens the box
                    // with MenuOpen posed by REAL bytes, not a synthetic Open() call.
                    s.Runner.RunScript(sailorThirteen!, ScriptHelper.ProgramFInteract);
                    Assert.True(director.IsOpen);
                    Assert.NotEqual(0u, s.GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen);

                    // Seed a deterministic, script-independent walk (AlundraScriptedMotion.cs:233-241's own
                    // Controller-null branch: PosX/PosY += FinalForceX/Y every tick once Force is already
                    // at TargetForce, no ramp) - "pose" half of the assertion.
                    sailorEleven!.Controller = null;
                    sailorEleven.Speed = 100;
                    sailorEleven.Acceleration = 0;
                    sailorEleven.TargetDirection = 0;
                    sailorEleven.CurrentDirection = 0;
                    sailorEleven.TargetForceX = 2000;
                    sailorEleven.ForceX = 2000;
                    sailorEleven.TargetForceY = 0;
                    sailorEleven.ForceY = 0;

                    // "état de programme" half of the assertion: ProgramUnknown so the HARNESS's own
                    // unrelated RunPendingEventTriggers catch-up pass (called straight from RunFrame,
                    // below - a mirror of the D3 rescan, not itself gated by T2, and not the site T2
                    // touches) skips this entity outright (its own "continue" guard,
                    // AlundraWorldProxy.cs:1539) regardless of whether the freeze holds - isolating the
                    // assertion to entity.Update()'s OWN PickEventTrigger/RunPickedEvent call. Freeze
                    // means PickEventTrigger never re-picks a slot and RunPickedEvent never dispatches
                    // real bytecode, so the REAL interpreter's own per-entity resume state
                    // (EventProgramState.CodeIndex, EventProgramState.cs:39 - Tick is one of the two
                    // slots the original ever resumes across calls off this, see AlundraEventProgramRunner's
                    // own class doc) never moves either.
                    sailorEleven.EventTrigger = ScriptHelper.ProgramUnknown;

                    // Seed an animation mismatch directly (R4/R3's own SyncAnimation correction,
                    // AlundraFrameSyncPasses.cs:241): TryResolveAnimationTarget fires unconditionally
                    // whenever CurrentAnimationId != TargetAnimationId, regardless of any
                    // AnimatedSpriteComponent - exactly what this bare-fallback proxy needs.
                    sailorEleven.CurrentAnimationId = 1;
                    sailorEleven.TargetAnimationId = 2;
                    sailorEleven.AnimationDirection = 0;

                    posXAtFreeze = sailorEleven.PosX;
                    posYAtFreeze = sailorEleven.PosY;
                    codeIndexAtFreeze = sailorEleven.EventProgramState.CodeIndex;
                }

                // Frames 3..38 mirror SailorThirteen's own "stays open, no button" window - long enough
                // to prove the freeze holds for many consecutive frozen frames, not just one.
                if (s.Frame is > 2 and <= 38)
                {
                    if (sailorEleven!.PosX != posXAtFreeze
                        || sailorEleven.PosY != posYAtFreeze
                        || sailorEleven.EventProgramState.CodeIndex != codeIndexAtFreeze
                        || sailorEleven.CurrentAnimationId != 1)
                    {
                        frozenHeld = false;
                    }
                }

                // Frames 39..47: press interact each frame while the box is open - same closing window as
                // SailorThirteen's own test.
                if (s.Frame is >= 39 and < 48 && director.IsOpen)
                {
                    s.GameState.LastPadState = new AlundraPadState { ButtonsJustPressed = AlundraPadState.Square };
                }

                if (s.Frame is >= 40 and < 50 && !closedOnButton && !director.IsOpen)
                {
                    closedOnButton =
                        (s.GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) == 0;
                }
            });

            Assert.True(frozenHeld, "the NPC's pose, program pick and animation must all stay frozen while MenuOpen is posed.");
            Assert.True(closedOnButton, "the box must still advance/close normally while frozen NPCs sit still elsewhere.");
            Assert.False(director.IsOpen);
        }
        finally
        {
            CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    /// <summary>
    /// [R4] mutation "mettre SyncTransform dedans" (docs/plan-transitions-carte.md §3, T2's own mutation
    /// table): the root position of an entity carrying a <see cref="Entity.RootComponent"/> and NO
    /// <see cref="AlundraEntityScriptProxy.Controller"/> - the only shape <see cref="AlundraFrameSyncPasses.SyncTransform"/>
    /// actually writes to (AlundraFrameSyncPasses.cs:168-183: it is a no-op whenever a controller owns the
    /// root) - must keep publishing the entity's CURRENT logical position every frame, even while
    /// <see cref="AlundraGameState.PlayerControlBits.GameplayBlockedMask"/> is posed. Deliberately headless
    /// and minimal (no <see cref="HeadlessIntroSimulation"/>, no map data needed for this specific
    /// mutation) - still <see cref="AlundraEntityScriptProxy.Update"/> at its real call site, so still
    /// squarely ORACLE 1: no player/camera/render assertion, real production code only.
    /// </summary>
    [Fact]
    public void Update_SyncTransform_KeepsPublishingRootPosition_WhileGameplayBlockedMaskIsPosed()
    {
        var host = new BareFreezeTestScriptHost();

        var root = new TransformComponent();
        var entity = new Entity
        {
            Name = "FreezeTestNpc",
            RootComponent = root,
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
        };
        entity.Initialize();

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.ScriptHost = host;
        proxy.IsPlayer = false;
        proxy.Status = EntityStatus.Normal;
        proxy.EventTrigger = ScriptHelper.ProgramUnknown;
        proxy.Controller = null;

        proxy.PosX = 100 << 16;
        proxy.PosY = 200 << 16;
        proxy.PosZ = 0;

        // Baseline, unfrozen: SyncTransform must publish the logical pose onto the root right away.
        proxy.Update(1f / 50f);
        Assert.Equal(
            AlundraEntitySpawnFactory.ResolveLogicalPosition(proxy.PosX, proxy.PosY, proxy.PosZ),
            root.LocalTransform.Position);

        // Freeze - then move the logical position again (standing in for "the last unfrozen tick just
        // wrote a new pose the instant MenuOpen got posed"). SyncTransform is "dehors" (§1.5): it must
        // still republish the CURRENT PosX/Y/Z, unconditionally of the gate, even though every OTHER pass
        // this same Update call would otherwise drive is frozen.
        host.GameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;
        proxy.PosX = 500 << 16;
        proxy.PosY = 700 << 16;
        proxy.Update(1f / 50f);

        Assert.Equal(
            AlundraEntitySpawnFactory.ResolveLogicalPosition(proxy.PosX, proxy.PosY, proxy.PosZ),
            root.LocalTransform.Position);
    }

    private sealed class NoOpEventProgramRunner : IEventProgramRunner
    {
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    private sealed class BareFreezeTestScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; } = new NoOpEventProgramRunner();
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;
        public System.Collections.Generic.IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; }
            = System.Array.Empty<AlundraEntityScriptProxy>();

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        public int LogicTicksThisFrame(float elapsedTime) => 1;
    }
}
