#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1a T2 (docs/plan-e20-heros.md section 1.24.1, "Valeurs attendues"; (w1) and (w2)): the object in the hands of the hero on <see cref="CarryRig"/> - the real hero controller, the exported animation sets and chains of the carrying states, a crate of the
/// original, and a host that plays each frame tick by tick over all the entities (the hero first), at 1, 2 and 3 ticks per frame. The geometry is the oracle's: the hero at (109, 100), the crate at (130, 100), 21 px east, put in the hands by
/// <see cref="PlayerManager.PutInHands"/> INSIDE the tick L, after <c>MovePlayer</c> and before the animation clock and the physics (<see cref="CarryRig.LiftNow"/>: where the binary's <c>TryLift</c> lifts; the mode 0 of L found nothing carried, the physics of L
/// finds the crate where it was, the series of the lift starts at L + 1 and the gesture 5 at L, 36 ticks, then 12). Written BEFORE the branchings of T3: red on the stubs. Every expected value is the oracle's of the plan table.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class CarryWorldTests : IDisposable
{
    private const int Px = CarryRig.Px;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public CarryWorldTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    /// <summary>The ticks per frame of every test of this class.</summary>
    public static TheoryData<int> TicksPerFrame => new() { 1, 2, 3 };

    /// <summary>A scene with a crate 21 px east of the hero, settled for a few ticks.</summary>
    private static CarryRig Scene(int ticksPerFrame, Func<int, uint>? hold = null)
    {
        var rig = CarryRig.Build(ticksPerFrame, hold);
        rig.AddCrate(130, 100);
        rig.Run(3);
        return rig;
    }

    private static (int X, int Y, int Z) Offset(EntityScriptProxy hero, EntityScriptProxy crate)
        => ((crate.PosX - hero.PosX) / Px, (crate.PosY - hero.PosY) / Px, (crate.PosZ - hero.PosZ) / Px);

    // -----------------------------------------------------------------------------------------
    // (w1) the follow: the carried object is placed at the hero's position plus the offset at every tick (the carried branch of MoveEntity, 0x80037E34).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void W1_TheCarriedObject_IsPlacedAtTheHeroPlusTheOffset_EveryTick_WithNoForceOfItsOwn(int ticksPerFrame)
    {
        var rig = Scene(ticksPerFrame);
        var hero = rig.Hero;
        var crate = rig.Crate!;
        var failures = new List<string>();
        rig.LiftNow();
        Assert.NotNull(hero.LogicContextEntity);
        Assert.NotNull(crate.LogicContextEntity);
        Assert.Same(crate.LogicContextEntity, hero.CarriedEntity);
        Assert.Same(hero.LogicContextEntity, crate.PlatformEntity);

        rig.Run(40, tick =>
        {
            var offset = (hero.RelativeWarpOffsetX, hero.RelativeWarpOffsetY, hero.RelativeWarpOffsetZ);
            var position = (crate.PosX - hero.PosX, crate.PosY - hero.PosY, crate.PosZ - hero.PosZ);
            if (position != offset)
            {
                failures.Add($"tick {tick}: crate - hero {position}, offset {offset}");
            }

            var root = ContactWorld.Root(crate);
            var expectedRoot = EntitySpawnFactory.ResolveLogicalPosition(crate.PosX, crate.PosY, crate.PosZ);
            if (root != expectedRoot)
            {
                failures.Add($"tick {tick}: root {root}, expected {expectedRoot}");
            }

            if ((crate.ForceX, crate.ForceY, crate.ForceZ, crate.FinalForceX, crate.FinalForceY, crate.FinalForceZ) != (0, 0, 0, 0, 0, 0))
            {
                failures.Add($"tick {tick}: forces {crate.ForceX} {crate.ForceY} {crate.ForceZ} / {crate.FinalForceX} {crate.FinalForceY} {crate.FinalForceZ}");
            }

            if ((crate.ForceAdjusted, crate.CollidedWithEntityZ, crate.IsOnGround) != (1, 1, 0) || crate.XCollisionEntity != null || crate.RidingEntity != null)
            {
                failures.Add($"tick {tick}: adjusted {crate.ForceAdjusted}, collidedZ {crate.CollidedWithEntityZ}, ground {crate.IsOnGround}, xcoll {crate.XCollisionEntity}, riding {crate.RidingEntity}");
            }

            if (rig.Host.Collidables.Contains(crate))
            {
                failures.Add($"tick {tick}: the carried crate is in the collidable list");
            }
        });

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(6)));
    }

    // -----------------------------------------------------------------------------------------
    // (w2) the trace of the lift: the offset from tick to tick (the table "De L+1 à L+11" and "De L+12 à L+32") and the gesture 5 that lasts 36 ticks and chains to 12.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void W2_TheLiftTrace_RisesBy1PixelATick_ThenDrawsInOverTheHead_AndTheGestureChainsToTheStand(int ticksPerFrame)
    {
        var rig = Scene(ticksPerFrame);
        var hero = rig.Hero;
        var crate = rig.Crate!;
        var lift = rig.LiftNow();
        Assert.Equal((21, 0, 0), Offset(hero, crate));
        Assert.Equal(5u, hero.TargetAnimationId);

        var failures = new List<string>();
        rig.Run(40, tick =>
        {
            var k = tick - lift;
            var expected = k <= 11 ? (21, 0, k) : k <= 32 ? (32 - k, 0, k) : (0, 0, 32);
            var actual = Offset(hero, crate);
            if (actual != expected)
            {
                failures.Add($"L+{k}: offset {actual}, expected {expected}");
            }

            if ((hero.RelativeWarpOffsetX / Px, hero.RelativeWarpOffsetY / Px, hero.RelativeWarpOffsetZ / Px) != expected)
            {
                failures.Add($"L+{k}: the hero's offset words {hero.RelativeWarpOffsetX / Px} {hero.RelativeWarpOffsetY / Px} {hero.RelativeWarpOffsetZ / Px}, expected {expected}");
            }

            var expectedAnimation = k <= 35 ? 5u : 12u;
            if (hero.TargetAnimationId != expectedAnimation)
            {
                failures.Add($"L+{k}: the hero's animation {hero.TargetAnimationId}, expected {expectedAnimation}");
            }

            if (hero.PosX != 109 * Px || hero.PosY != 100 * Px || hero.PosZ != 0)
            {
                failures.Add($"L+{k}: the hero moved to {hero.PosX / (double)Px} {hero.PosY / (double)Px} {hero.PosZ / (double)Px}");
            }
        });

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(8)));
        Assert.Same(crate.LogicContextEntity, hero.CarriedEntity);
    }
}
