#nullable enable
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.d2b B4 (docs/plan-e19-opcodes.md §1.2h.2, D-E19-29, D-E19-30, D-E19-35): the contact of the dialogue comes from the blocking report of the
/// controller, and a walk blocked by an entity does not detour. T-R7: the hero of a real controller walking into an entity that asks for the button
/// (<c>InteractRequiresButton</c>): <see cref="AlundraEntityScriptProxy.XCollisionEntity"/> is that entity at the tick the controller shortens or cancels the
/// step (the binary's <c>+0x130</c>, <c>0x80037F08</c>: the obstacle of the tick, 0 when nothing blocked), null when the hero steps away or asks for no
/// displacement, kept while the world is frozen. T-R8: the navigation detour of <c>0x0B</c> and <c>0x1E</c> engages on a contact of the cells, not on an
/// entity.
/// </summary>
public sealed class AlundraEntityContactReportTests
{
    // ---- T-R7 -----------------------------------------------------------------------------------------------------

    private sealed class HeroRig
    {
        public required World World { get; init; }

        public required ContactHost Host { get; init; }

        public required AlundraEntityScriptProxy Hero { get; init; }

        public required AlundraEntityScriptProxy Npc { get; init; }

        public void Tick() => ContactWorld.Integrate(World, Host);
    }

    /// <summary>The hero (21 x 15 x 32, a cutscene lock so that the pad does not override his walk) at (100, 100, 48), an entity that asks for the button
    /// at (160, 100), a probe installed. The hero walks east by his <c>TargetDirection</c> and the walk animation (speed 208), north on 16.</summary>
    private static HeroRig NewHeroRig()
    {
        var host = new ContactHost(playerControlFlags: AlundraGameState.PlayerControlBits.ControlLocked, playerController: new AlundraPlayerController());
        var world = ContactWorld.BuildWorld(new FlatGroundField { GroundZ = 48f }, new AlundraMovementObstacleProbe(host));
        var hero = ContactWorld.AddEntity(world, host, "Hero", 100, 100, 48, -10, -7, 0, 21, 15, 32, isPlayer: true);
        var npc = ContactWorld.AddEntity(world, host, "Npc", 160, 100, 48, -10, -7, 0, 20, 14, 32);
        npc.Flags |= EntityFlags.InteractRequiresButton;
        hero.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [0] = new AnimSetEntry { Anim = 0, Speed = 0, Acceleration = 0 },
            [1] = new AnimSetEntry { Anim = 1, Speed = 208, Acceleration = 0 },
        };
        hero.CurrentAnimationId = 0;
        hero.TargetAnimationId = 0;
        ContactWorld.Integrate(world, host);
        return new HeroRig { World = world, Host = host, Hero = hero, Npc = npc };
    }

    private static void Walk(HeroRig rig, uint direction)
    {
        rig.Hero.TargetDirection = direction;
        rig.Hero.TargetAnimationId = 1;
    }

    private static void Stop(HeroRig rig) => rig.Hero.TargetAnimationId = 0;

    [Fact]
    public void TR7_TheHeroWalkingIntoAnEntity_ContactIsTheEntityAtTheTickOfTheStop_NullWhenHeStepsAwayOrAsksNoDisplacement()
    {
        var rig = NewHeroRig();
        Assert.Null(rig.Hero.XCollisionEntity);

        // East, into the entity: null on the free ticks, the entity from the tick the controller shortens the step.
        Walk(rig, 24);
        var ticks = 0;
        while (rig.Hero.XCollisionEntity == null && ticks < 40)
        {
            rig.Tick();
            ticks++;
        }

        Assert.Same(rig.Npc, rig.Hero.XCollisionEntity);
        Assert.InRange(ticks, 10, 30);
        Assert.Equal(rig.Hero.EntityBlockCount > 0, true);
        var x = rig.Hero.PosX;
        Assert.InRange(x, 138 << 16, 139 << 16); // flush: the right edge (x + 11) meets the left edge of the entity (150).

        // Still pushing: the contact is written on every tick of the push.
        for (var tick = 0; tick < 3; tick++)
        {
            rig.Tick();
            Assert.Same(rig.Npc, rig.Hero.XCollisionEntity);
            Assert.InRange(rig.Hero.PosX, x - 8, x + 8);
        }

        // North, away from the entity: nothing blocks.
        Walk(rig, 16);
        rig.Tick();
        Assert.Null(rig.Hero.XCollisionEntity);

        // Push again, then ask for no displacement (the idle animation): the contact goes with the displacement.
        Walk(rig, 24);
        for (var tick = 0; tick < 6 && rig.Hero.XCollisionEntity == null; tick++)
        {
            rig.Tick();
        }

        Assert.Same(rig.Npc, rig.Hero.XCollisionEntity);
        Stop(rig);
        rig.Tick();
        Assert.Null(rig.Hero.XCollisionEntity);
    }

    [Fact]
    public void TR7_TheContactIsKeptWhileTheWorldIsFrozen()
    {
        var rig = NewHeroRig();
        Walk(rig, 24);
        for (var tick = 0; tick < 40 && rig.Hero.XCollisionEntity == null; tick++)
        {
            rig.Tick();
        }

        Assert.Same(rig.Npc, rig.Hero.XCollisionEntity);

        rig.Host.GameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;
        Stop(rig); // were the pipeline running, an idle tick would clear the contact.
        for (var tick = 0; tick < 3; tick++)
        {
            rig.Tick();
            Assert.Same(rig.Npc, rig.Hero.XCollisionEntity);
        }

        rig.Host.GameState.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.MenuOpen;
        rig.Tick();
        Assert.Null(rig.Hero.XCollisionEntity);
    }

    [Fact]
    public void TR7_ATwinPlacedFlushAgainstTheEntity_WithoutPushing_WritesNoContact()
    {
        var rig = NewHeroRig();
        rig.Hero.PosX = 139 << 16; // the overlap probe of the end of the frame is gone: standing flush is not a contact.
        rig.Hero.PushLogicalPositionToRoot();

        for (var tick = 0; tick < 5; tick++)
        {
            rig.Tick();
            Assert.Null(rig.Hero.XCollisionEntity);
        }
    }

    // ---- T-R8 -----------------------------------------------------------------------------------------------------

    private static EventProgramDocument NewDocument(params int[] codes) => new()
    {
        MapIndex = 1,
        EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
        Codes = codes,
    };

    private sealed class GridWorld : IEntityWorldContext
    {
        public GridWorld(NavigationGrid2D grid) => NavigationGrid = grid;

        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities => System.Array.Empty<AlundraEntityScriptProxy>();

        public AlundraEntityScriptProxy? PlayerEntity => null;

        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }

        public void SetForcedCameraLookAt(int x, int y, int z)
        {
        }

        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;

        public void DestroyEntity(AlundraEntityScriptProxy entity)
        {
        }

        public NavigationGrid2D? NavigationGrid { get; }
    }

    private static NavigationGrid2D SyntheticGrid()
    {
        var grid = new NavigationGrid2D(10, 10, 1f);
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                grid.SetCell(x, y, (x, y) == (5, 5)
                    ? NavigationGridCell.Blocked
                    : new NavigationGridCell(true, 1f, NavigationLayerMask.All));
            }
        }

        return grid;
    }

    private static (AlundraEventProgramRunner Runner, AlundraEntityScriptProxy Entity, EventProgramState State) WalkMontage(params int[] codes)
    {
        var document = NewDocument(codes);
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), new GridWorld(SyntheticGrid()));
        var entity = new AlundraEntityScriptProxy { PosX = 108 << 16, PosY = 88 << 16, TargetDirection = 24 };
        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        return (runner, entity, state);
    }

    [Fact]
    public void TR8_Walk0x1E_ForceAdjustedWithAnEntityInContact_DoesNotDetour_AndDoesNotLatchAnAttempt()
    {
        var (runner, entity, state) = WalkMontage(0x1E, 24, 0, 0xFF);
        runner.RunOneScriptCall(entity, state);

        entity.ForceAdjusted = 1;
        entity.XCollisionEntity = new AlundraEntityScriptProxy();
        runner.RunOneScriptCall(entity, state);

        Assert.Null(entity.WalkDetourPath);
        Assert.False(entity.WalkDetourAttempted);
        Assert.Equal(24u, entity.TargetDirection);
        Assert.Equal(0, state.CodeIndex); // still waiting for its distance, as in the original.
    }

    [Fact]
    public void TR8_Walk0x1E_ForceAdjustedOnACellContact_StillDetours()
    {
        var (runner, entity, state) = WalkMontage(0x1E, 24, 0, 0xFF);
        runner.RunOneScriptCall(entity, state);

        entity.ForceAdjusted = 1;
        entity.XCollisionEntity = null;
        runner.RunOneScriptCall(entity, state);

        Assert.NotNull(entity.WalkDetourPath);
        Assert.True(entity.WalkDetourAttempted);
        Assert.NotEqual(24u, entity.TargetDirection);
    }

    [Fact]
    public void TR8_WalkUntilBeyondRadius0x0B_ForceAdjustedWithAnEntityInContact_DoesNotDetour_AndDoesNotLatchAnAttempt()
    {
        var (runner, entity, state) = WalkMontage(0x01, 0x0B, 7, 24, 0, 0xFF);
        runner.RunOneScriptCall(entity, state);

        entity.ForceAdjusted = 1;
        entity.XCollisionEntity = new AlundraEntityScriptProxy();
        runner.RunOneScriptCall(entity, state);
        runner.RunOneScriptCall(entity, state);

        Assert.Null(entity.WalkDetourPath);
        Assert.False(entity.WalkDetourAttempted);
        Assert.Equal(24u, entity.TargetDirection);
        Assert.Equal(1, state.CodeIndex);
    }

    [Fact]
    public void TR8_WalkUntilBeyondRadius0x0B_ForceAdjustedOnACellContact_StillDetours()
    {
        var (runner, entity, state) = WalkMontage(0x01, 0x0B, 7, 24, 0, 0xFF);
        runner.RunOneScriptCall(entity, state);

        entity.ForceAdjusted = 1;
        entity.XCollisionEntity = null;
        runner.RunOneScriptCall(entity, state);

        Assert.NotNull(entity.WalkDetourPath);
        Assert.NotEqual(24u, entity.TargetDirection);
    }
}
