#nullable enable
using System.Collections.Generic;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E12.d's PRODUCTION-SITE stage (plan §3 étage 1, docs/plan-e12d-interaction-joueur.md): the player
/// interaction chain pinned at the real call sites - the contact of the hero's blocked step inside the real
/// <see cref="AlundraWorldProxy.Update"/> (P-a), the <c>CheckEntityInteraction</c> call inside the real
/// player branch of <see cref="AlundraEntityScriptProxy.Update"/> → <c>MovePlayer</c> (P-b), and the
/// consume-on-pick cadence at the real slot-F pick (P-c). The full-flow sailor test in
/// <see cref="AlundraDialogueOpcodesProductionTests"/> may then legitimately mirror these passes in its
/// harness - the F1 contract: a harness mirror only stands when the production site carries its own
/// test (this repo's green-and-inert family).
/// </summary>
public sealed class AlundraInteractionPassTests : System.IDisposable
{
    public AlundraInteractionPassTests()
    {
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(true);

        // D-T-14 (docs/plan-transitions-carte.md, slice T1): this class constructs an AlundraWorldProxy,
        // so it shares the three session carriers T1 introduces - reset them here (constructor, the
        // isolation-carrying element) so no earlier test's state leaks in.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.
    }

    public void Dispose()
    {
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);

        // D-T-14: hygiene, not covered by the acceptance (the constructor above is what carries
        // isolation) - kept for symmetry with the existing session-singleton test classes.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.
    }

    // -----------------------------------------------------------------------------------------
    // Shared montage bits
    // -----------------------------------------------------------------------------------------

    private static AlundraEntityScriptProxy NewCollidable(int x, int y, int z, uint extraFlags = 0)
        => new()
        {
            Flags = EntityFlags.Collidable | extraFlags,
            Status = EntityStatus.Normal,
            PosX = x,
            PosY = y,
            PosZ = z,
            Width = 16,
            Height = 16,
            Depth = 16,
        };

    private sealed class RecordingRunner : IEventProgramRunner
    {
        public readonly List<(AlundraEntityScriptProxy Entity, int Slot)> ScriptRuns = new();
        public int SpriteEventRuns;

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot) => ScriptRuns.Add((entity, programSlot));
        public void RunSpriteEvent(AlundraEntityScriptProxy entity) => SpriteEventRuns++;
    }

    private sealed class InteractScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; init; } = new RecordingRunner();
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController { get; init; }
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; init; } = System.Array.Empty<AlundraEntityScriptProxy>();
        public int TicksThisFrame { get; set; } = 1;

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        public int LogicTicksThisFrame(float elapsedTime) => TicksThisFrame;
    }

    /// <summary>A real player proxy whose real <see cref="AlundraEntityScriptProxy.Update"/> player
    /// branch runs <c>MovePlayer</c> - the exact montage of the D-E7-8 pad-seam production test.</summary>
    private static AlundraEntityScriptProxy NewDrivablePlayer(InteractScriptHost host)
    {
        var player = new AlundraEntityScriptProxy
        {
            IsPlayer = true,
            ScriptHost = host,
            Flags = EntityFlags.Collidable,
            Status = EntityStatus.Normal,
            Width = 16,
            Height = 16,
            Depth = 16,
        };
        player.Initialize(new Entity());
        return player;
    }

    private static AlundraPadState SquarePress() => new() { ButtonsJustPressed = AlundraPadState.Square };

    // -----------------------------------------------------------------------------------------
    // P-a - the contact at its production site (mutations №2 and №7). E19.d2b B4 (T-REG-E12D-2, D-E19-29): the overlap pass of the end of
    // AlundraWorldProxy.Update is gone; the contact is the entity that shortened or cancelled the hero's step, written by his controller's blocking
    // report in his own tick (AlundraEntityScriptProxy.MoveControllerAndPullPosition), through the real player branch: pad -> MovePlayer -> TickPlayer.
    // -----------------------------------------------------------------------------------------

    private sealed class HeroAndSailor
    {
        public required World World { get; init; }

        public required ContactHost Host { get; init; }

        public required AlundraEntityScriptProxy Hero { get; init; }

        public required AlundraEntityScriptProxy Sailor { get; init; }

        public required PadHold Pad { get; init; }

        public void Tick() => ContactWorld.Integrate(World, Host);
    }

    private sealed class PadHold
    {
        public uint Buttons;
    }

    /// <summary>The hero (21 x 15 x 32, a real controller, the walking animation of the export: speed 208) at (100, 100, 48) and a collidable sailor that
    /// asks for the button at (140, 100), 40 px away on x: the hero's right edge meets the sailor's left edge (130) at x = 119. The pad is held through
    /// <see cref="PadHold"/>; the probe is installed.</summary>
    private static HeroAndSailor BuildHeroAndSailor()
    {
        var pad = new PadHold();
        var host = new ContactHost(playerController: new AlundraPlayerController
        {
            PadStateProviderForTests = () => new AlundraPadState { ButtonsHold = pad.Buttons, ButtonsJustPressed = pad.Buttons },
        });
        var world = ContactWorld.BuildWorld(new FlatGroundField { GroundZ = 48f }, new AlundraMovementObstacleProbe(host));
        var hero = ContactWorld.AddEntity(world, host, "Hero", 100, 100, 48, -10, -7, 0, 21, 15, 32, isPlayer: true);
        var sailor = ContactWorld.AddEntity(world, host, "Sailor", 140, 100, 48, -10, -7, 0, 20, 14, 32);
        sailor.Flags |= EntityFlags.InteractRequiresButton;
        hero.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [0] = new AnimSetEntry { Anim = 0, Speed = 0, Acceleration = 0 },
            [1] = new AnimSetEntry { Anim = 1, Speed = 208, Acceleration = 1 },
        };
        hero.IsOnGround = 1;
        ContactWorld.Integrate(world, host);
        return new HeroAndSailor { World = world, Host = host, Hero = hero, Sailor = sailor, Pad = pad };
    }

    [Fact]
    public void Update_WritesThePlayersContact_AtTheTickOfTheStop_AndNullOnceTheMoveIsNull()
    {
        var rig = BuildHeroAndSailor();
        Assert.Null(rig.Hero.XCollisionEntity);

        // Right held: free ticks first, then the sailor from the tick the controller shortens the step.
        rig.Pad.Buttons = AlundraPadState.Right;
        var ticks = 0;
        while (rig.Hero.XCollisionEntity == null && ticks < 40)
        {
            rig.Tick();
            ticks++;
        }

        Assert.Same(rig.Sailor, rig.Hero.XCollisionEntity);
        Assert.InRange(rig.Hero.PosX, 117 << 16, 120 << 16); // flush: x = 119.

        // The pad released: the force decays to nothing, then a null Move writes no contact.
        rig.Pad.Buttons = 0;
        for (var tick = 0; tick < 12; tick++)
        {
            rig.Tick();
        }

        Assert.Null(rig.Hero.XCollisionEntity);

        // Pushing again, the contact is back (detection follows the move); stepping away clears it.
        rig.Pad.Buttons = AlundraPadState.Right;
        for (var tick = 0; tick < 12 && rig.Hero.XCollisionEntity == null; tick++)
        {
            rig.Tick();
        }

        Assert.Same(rig.Sailor, rig.Hero.XCollisionEntity);
        rig.Pad.Buttons = AlundraPadState.Left;
        for (var tick = 0; tick < 12; tick++)
        {
            rig.Tick();
        }

        Assert.Null(rig.Hero.XCollisionEntity);
    }

    [Fact]
    public void Update_TheContactIsFrozen_WhileGameplayBlockedMaskIsPosed()
    {
        var rig = BuildHeroAndSailor();

        // The original freezes its whole entity pipeline - physics included - behind GameplayBlockedMask (EntityManager.cs:377): with a MenuOpen box up,
        // the contact must keep its pre-open value, not refresh (D-E12D-5). No step is taken, so it keeps it by construction.
        rig.Pad.Buttons = AlundraPadState.Right;
        for (var tick = 0; tick < 40 && rig.Hero.XCollisionEntity == null; tick++)
        {
            rig.Tick();
        }

        Assert.Same(rig.Sailor, rig.Hero.XCollisionEntity);

        rig.Host.GameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;
        rig.Pad.Buttons = 0; // were the pipeline running, a null step would clear the contact.
        for (var tick = 0; tick < 4; tick++)
        {
            rig.Tick();
            Assert.Same(rig.Sailor, rig.Hero.XCollisionEntity);
        }

        rig.Host.GameState.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.MenuOpen;
        for (var tick = 0; tick < 12; tick++)
        {
            rig.Tick();
        }

        Assert.Null(rig.Hero.XCollisionEntity);
    }

    // -----------------------------------------------------------------------------------------
    // P-b - CheckEntityInteraction inside the REAL Update->MovePlayer chain (mutations №3 and №5).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void PlayerBranch_SquareAgainstAButtonEntity_AssignsTheActiveCollisionEntity()
    {
        var host = new InteractScriptHost
        {
            PlayerController = new AlundraPlayerController { PadStateProviderForTests = SquarePress },
        };
        var player = NewDrivablePlayer(host);
        var sailor = NewCollidable(0, 0, 0, EntityFlags.InteractRequiresButton);
        sailor.ProgramIndexes[ScriptHelper.ProgramFInteract] = 0x8d;

        player.XCollisionEntity = sailor;
        player.Update(1f / 50f); // the real production frame: player branch -> MovePlayer -> interact.

        Assert.Same(sailor, host.ActiveCollisionEntity);
        // res==2 forces Idle, the original's own `TargetAnimationId = Idle` on the button branch.
        Assert.Equal(0u, player.TargetAnimationId);
    }

    [Fact]
    public void PlayerBranch_NoPress_OrNoFProgram_OrBlockedMask_AssignsNothing()
    {
        // No press.
        var quietHost = new InteractScriptHost
        {
            PlayerController = new AlundraPlayerController { PadStateProviderForTests = () => default },
        };
        var player = NewDrivablePlayer(quietHost);
        var sailor = NewCollidable(0, 0, 0, EntityFlags.InteractRequiresButton);
        sailor.ProgramIndexes[ScriptHelper.ProgramFInteract] = 0x8d;
        player.XCollisionEntity = sailor;
        player.Update(1f / 50f);
        Assert.Null(quietHost.ActiveCollisionEntity);

        // Press, but the entity has NO F program at all.
        var host = new InteractScriptHost
        {
            PlayerController = new AlundraPlayerController { PadStateProviderForTests = SquarePress },
        };
        player = NewDrivablePlayer(host);
        player.XCollisionEntity = NewCollidable(0, 0, 0, EntityFlags.InteractRequiresButton);
        player.Update(1f / 50f);
        Assert.Null(host.ActiveCollisionEntity);

        // Press + F program, but GameplayBlockedMask posed (D-E12D-5, mutation №5): with a MenuOpen
        // box up the original never even reached MovePlayer (EntityManager.cs:377).
        host = new InteractScriptHost
        {
            PlayerController = new AlundraPlayerController { PadStateProviderForTests = SquarePress },
        };
        player = NewDrivablePlayer(host);
        player.XCollisionEntity = sailor;
        host.GameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;
        player.Update(1f / 50f);
        Assert.Null(host.ActiveCollisionEntity);
    }

    [Fact]
    public void AutoTouchEntity_InteractsWithoutAnyButton_TheOriginalsResOne()
    {
        // T3 (plan §3): no InteractRequiresButton flag -> res=1, assignment without a press.
        var host = new InteractScriptHost
        {
            PlayerController = new AlundraPlayerController { PadStateProviderForTests = () => default },
        };
        var player = NewDrivablePlayer(host);
        var touchPlate = NewCollidable(0, 0, 0);
        touchPlate.ProgramIndexes[ScriptHelper.ProgramFInteract] = 0x8d;

        player.XCollisionEntity = touchPlate;
        player.Update(1f / 50f);

        Assert.Same(touchPlate, host.ActiveCollisionEntity);
    }

    [Fact]
    public void InteractLatch_StandingStillAgainstAButtonEntity_TheLaterPressStillLands()
    {
        // T5 (plan §3): the original's g_lastValidWarp* memory. Frame 1 makes contact WITHOUT a press
        // (stores the latch); frame 2 has lost the raw contact (XCollisionEntity null, nobody moved)
        // and presses - the latch must still resolve the entity. Frame 3 repeats after the entity
        // moved one unit: the eight stored comparisons must invalidate it.
        var pressNow = false;
        var host = new InteractScriptHost
        {
            PlayerController = new AlundraPlayerController
            {
                PadStateProviderForTests = () => pressNow ? SquarePress() : default,
            },
        };
        var player = NewDrivablePlayer(host);
        var sailor = NewCollidable(0, 0, 0, EntityFlags.InteractRequiresButton);
        sailor.ProgramIndexes[ScriptHelper.ProgramFInteract] = 0x8d;

        player.XCollisionEntity = sailor;
        player.Update(1f / 50f); // contact, no press - latch stored, nothing assigned.
        Assert.Null(host.ActiveCollisionEntity);

        player.XCollisionEntity = null; // raw contact gone, positions untouched.
        pressNow = true;
        player.Update(1f / 50f);
        Assert.Same(sailor, host.ActiveCollisionEntity); // the latch carried it.

        host.ActiveCollisionEntity = null;
        sailor.PosX += 1; // any stored position mismatch invalidates the latch.
        player.Update(1f / 50f);
        Assert.Null(host.ActiveCollisionEntity);
    }

    // -----------------------------------------------------------------------------------------
    // P-c - consume-on-pick at the REAL slot-F pick site (D-E12D-4, mutation №1's cadence half).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Pick_ConsumesTheAssignmentExactlyOnce_AcrossZeroTickAndCatchUpFrames()
    {
        var runner = new RecordingRunner();
        var host = new InteractScriptHost { Runner = runner };
        var sailor = NewCollidable(0, 0, 0, EntityFlags.InteractRequiresButton);
        sailor.ScriptHost = host;
        sailor.ProgramIndexes[ScriptHelper.ProgramFInteract] = 0x8d;
        sailor.Initialize(new Entity());

        host.ActiveCollisionEntity = sailor;

        // A zero-tick frame (free time-step above 50 Hz): no pick runs, the assignment MUST survive -
        // the original's MovePlayer-head clear transposed literally would drop it here (a silently
        // ignored press).
        host.TicksThisFrame = 0;
        sailor.Update(0.001f);
        Assert.Same(sailor, host.ActiveCollisionEntity);
        Assert.DoesNotContain(runner.ScriptRuns, run => run.Slot == ScriptHelper.ProgramFInteract);

        // A catch-up frame (3 logic ticks): the FIRST pick selects F and consumes the assignment -
        // exactly ONE slot-F run, not three.
        host.TicksThisFrame = 3;
        sailor.Update(0.06f);
        Assert.Null(host.ActiveCollisionEntity);
        Assert.Equal(1, runner.ScriptRuns.FindAll(run => run.Slot == ScriptHelper.ProgramFInteract).Count);
    }
}
