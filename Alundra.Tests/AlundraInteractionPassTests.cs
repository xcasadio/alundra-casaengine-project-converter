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
/// interaction chain pinned at the real call sites - the contact pass inside the real
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
    // P-a - the contact pass at the real AlundraWorldProxy.Update site (mutations №2 and №7).
    // -----------------------------------------------------------------------------------------

    private static AlundraWorldProxy BuildProxyWithPlayerAndCollidable(
        out AlundraEntityScriptProxy player, out AlundraEntityScriptProxy sailor)
    {
        var world = new World { Name = "TestWorld" };
        var camera = new Camera2dComponent();
        world.Entities.Add(new Entity { Name = "camera", RootComponent = camera });

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        player = NewCollidable(100, 100, 100);
        player.IsPlayer = true;
        sailor = NewCollidable(100, 100, 100, EntityFlags.InteractRequiresButton);

        proxy.PlayerEntity = player;

        // The ONE reflection seam of this montage: the private per-frame collidables buffer -
        // "TestWorld" spawns nothing, so RefreshUpdateProxiesAndCollidables never rebuilds (and never
        // clears) it, and injecting here feeds the real pass its real input list.
        var field = typeof(AlundraWorldProxy).GetField("_collidables", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        ((List<AlundraEntityScriptProxy>)field!.GetValue(proxy)!).Add(sailor);

        return proxy;
    }

    [Fact]
    public void Update_WritesThePlayersContact_AndDetectionFollowsAMove()
    {
        var proxy = BuildProxyWithPlayerAndCollidable(out var player, out var sailor);

        proxy.Update(1f / 50f);
        Assert.Same(sailor, player.XCollisionEntity);

        // Move the player away from its spawn position: a stale-cache port (mutation №7) or a deleted
        // call site (mutation №2) both fail here.
        player.PosX = 100000;
        proxy.Update(1f / 50f);
        Assert.Null(player.XCollisionEntity);

        player.PosX = 100;
        proxy.Update(1f / 50f);
        Assert.Same(sailor, player.XCollisionEntity);
    }

    [Fact]
    public void Update_ContactPassIsFrozen_WhileGameplayBlockedMaskIsPosed()
    {
        var proxy = BuildProxyWithPlayerAndCollidable(out var player, out _);

        // The original freezes its whole entity pipeline - physics included - behind
        // GameplayBlockedMask (EntityManager.cs:377): with a MenuOpen box up, the contact must keep
        // its pre-open value, not refresh (D-E12D-5).
        player.PosX = 100000;
        proxy.Update(1f / 50f);
        Assert.Null(player.XCollisionEntity);

        proxy.GameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;
        player.PosX = 100; // back onto the sailor - but the pass must not run.
        proxy.Update(1f / 50f);
        Assert.Null(player.XCollisionEntity);

        proxy.GameState.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.MenuOpen;
        proxy.Update(1f / 50f);
        Assert.NotNull(player.XCollisionEntity);
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
