#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using static Alundra.Tests.Hero.CarryScenes;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1a T2 (docs/plan-e20-heros.md section 1.24.1, "Valeurs attendues"; (w7), (w8), (w11)): what the lock, the deep water and the kind of the object do to the object in the hands, at 1, 2 and 3 ticks per frame on <see cref="CarryRig"/>.
/// (w7) a lock of control draws the object in and destroys it at the 22nd call of mode 1 (the sheet with an attack), and keeps the one whose sheet has no attack; (w8) deep water lets the object go (the code of throw -1 of mode 2, then the release of C23: animation 3,
/// links cleared, flags <c>0x332374</c>) and the hero floats, the object falls and the choice of program destroys it on the water with the effect 6; (w11) a heavy object (kind 3) keeps the hero at the stand, deaf to the direction and to the Cross, next to the object of kind 1
/// that walks. Written BEFORE the branchings (W1 to W4): red on the stubs.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class CarryLockSwimAndStatesTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public CarryLockSwimAndStatesTests()
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

    public static TheoryData<int> TicksPerFrame => new() { 1, 2, 3 };

    // -----------------------------------------------------------------------------------------
    // (w7) the lock: mode 1 of UpdatePlayerCarriedEntity, five calls of wait, 2 px a call, the destruction at the 22nd call; a sheet without an attack keeps the object.
    // -----------------------------------------------------------------------------------------

    private static CarryRig LockedWithTheObject(int ticksPerFrame, int attackCount)
    {
        var rig = CarryRig.Build(ticksPerFrame);
        rig.AddCrate(109, 100, 32, attackCount: attackCount); // on the head of the hero.
        rig.Run(3);
        rig.LiftNow(fromTheHead: true);
        rig.Run(3);
        Assert.Equal((0, 0, 32), Offset(rig.Hero, rig.Crate!));
        return rig;
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void W7_ALockOfControl_DrawsTheObjectIn2PixelsACall_AndDestroysItAtThe22ndCall_WhenItsSheetHasAnAttack_AndKeepsTheOneWhoseSheetHasNone(int ticksPerFrame)
    {
        var rig = LockedWithTheObject(ticksPerFrame, attackCount: 1);
        var hero = rig.Hero;
        var crate = rig.Crate!;
        rig.Host.GameState.PlayerControlFlags |= GameState.PlayerControlBits.ControlLocked;
        var first = rig.Tick + 1;
        var failures = new List<string>();
        rig.Run(30, tick =>
        {
            var call = tick - first + 1;
            if (call == 22 && hero.CarryLockCounter != 5)
            {
                failures.Add($"call 22: the counter is {hero.CarryLockCounter}, mode 1 keeps it at 5 after the destruction");
            }

            if (call > 21)
            {
                return;
            }

            // Calls 1 to 5 count; from the 6th each axis advances by 2 px towards the hero, the object reaches him at the 21st call.
            var expectedZ = call <= 5 ? 32 : 32 - 2 * (call - 5);
            if (Offset(hero, crate) != (0, 0, expectedZ))
            {
                failures.Add($"call {call}: offset {Offset(hero, crate)}, expected (0, 0, {expectedZ})");
            }

            if (crate.Status != EntityStatus.Normal)
            {
                failures.Add($"call {call}: the crate is already destroyed");
            }
        });

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(6)));
        Assert.Equal(new[] { crate }, rig.Host.SearchDestroyed);
        Assert.Equal(new[] { first + 21 }, rig.Host.SearchDestroyedAtTick);
        Assert.Equal(EntityStatus.FlagToDestroy, crate.Status);
        Assert.Null(hero.CarriedEntity); // cleared by the destruction; the calls after it find nothing carried and reset the counter.
        Assert.Equal(0, hero.CarryLockCounter);

        // Next to it, the object whose sheet has no attack (the seeds, the herb): mode 1 is mode 0 for it, the lock keeps it over the head of the hero for as long as it lasts.
        var kept = LockedWithTheObject(ticksPerFrame, attackCount: 0);
        kept.Host.GameState.PlayerControlFlags |= GameState.PlayerControlBits.ControlLocked;
        kept.Run(60);

        Assert.Empty(kept.Host.SearchDestroyed);
        Assert.Equal(EntityStatus.Normal, kept.Crate!.Status);
        Assert.Same(kept.Crate.LogicContextEntity, kept.Hero.CarriedEntity);
        Assert.Equal((0, 0, 32), Offset(kept.Hero, kept.Crate));
    }


    // -----------------------------------------------------------------------------------------
    // (w8) deep water: mode 2, then C23.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// The hero stands at (178, 100), the crate on his head: with <paramref name="water"/> every cell from x = 144 px has the ground property 8, which reads as the slope 4 (deep water), under the hero AND under the box of the crate (24 px wide), so that a crate
    /// that falls there lands on deep water and not on the shore. The hero starts in the water, so the scene does not depend on the walk; the swim branch of the tick that follows the lift lets the object go.
    /// </summary>
    private static CarryRig SwimScene(int ticksPerFrame, bool water)
    {
        var field = FlatCells.Create(groundProperty: (cx, _) => water && cx >= 6 ? 8 : 0);
        var rig = CarryRig.Build(ticksPerFrame, field: field, heroX: 178f, heroY: 100f);
        rig.RunLiftableProgram = true;
        var crate = rig.AddCrate(178, 100, 32); // on the head of the hero.
        crate.MapGravityRaw = 128; // the crate falls once it is let go.
        crate.MapZViscosityRaw = 4096;
        crate.MapGravity = 1250f;
        crate.MapMaxFallSpeed = 800f;
        rig.Run(3);
        Assert.Equal(water ? 4 : 0, rig.Hero.Slope_18c);
        return rig;
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void W8_TheDeepWater_LetsTheObjectGo_TheHeroFloats_AndTheObjectIsDestroyedOnTheWaterWithTheEffect6(int ticksPerFrame)
    {
        var rig = SwimScene(ticksPerFrame, water: true);
        var hero = rig.Hero;
        var crate = rig.Crate!;
        var lift = rig.LiftNow(fromTheHead: true);

        // At the end of the tick of the lift the object is in the hands: the swim branch of that tick ran before it was lifted.
        Assert.Same(crate.LogicContextEntity, hero.CarriedEntity);
        Assert.Same(hero.LogicContextEntity, crate.PlatformEntity);
        Assert.Equal(CarryRig.CrateFlags, crate.Flags);

        // The next tick: mode 2 writes the code -1 and puts the object at the head of the hero, the program C23 of the same tick lets it go (animation 3, the code back to 0, the two links cleared, the flags 0x332374) and the hero floats.
        rig.Run(1);
        Assert.Equal(lift + 1, rig.Tick);
        Assert.Equal(0x1Du, hero.TargetAnimationId);
        Assert.Null(hero.CarriedEntity);
        Assert.Equal(3u, crate.TargetAnimationId);
        Assert.Null(crate.PlatformEntity);
        Assert.Null(crate.RidingEntity);
        Assert.Equal(0x332374u, crate.Flags);
        Assert.Equal(0, crate.Flags2);
        Assert.Empty(rig.Host.Destroyed); // not destroyed on the tick it is let go: the carried object has no slope of its own.

        // It falls from the head of the hero and, on the water, the choice of program destroys it with the effect 6 (f6a).
        var destroyedAt = 0;
        rig.RunUntil(() =>
        {
            if (destroyedAt == 0 && rig.Host.Destroyed.Count > 0)
            {
                destroyedAt = rig.Tick;
            }

            return destroyedAt != 0;
        }, 60);
        Assert.True(destroyedAt > lift + 1, "the object is destroyed after it fell");
        Assert.Equal(0, crate.PosZ);
        var destroyed = Assert.Single(rig.Host.Destroyed);
        Assert.Same(crate, destroyed.Entity);
        Assert.Equal(6, destroyed.EffectId);
        Assert.Equal(EntityStatus.FlagToDestroy, crate.Status);

        // The same scene on dry ground (no deep water): the object stays in the hands and nothing is destroyed, for as many ticks.
        var dry = SwimScene(ticksPerFrame, water: false);
        dry.LiftNow(fromTheHead: true);
        dry.Run(destroyedAt - lift);
        Assert.Same(dry.Crate!.LogicContextEntity, dry.Hero.CarriedEntity);
        Assert.Same(dry.Hero.LogicContextEntity, dry.Crate.PlatformEntity);
        Assert.Empty(dry.Host.Destroyed);
        Assert.Equal(CarryRig.CrateFlags, dry.Crate.Flags);
        Assert.Equal(12u, dry.Hero.TargetAnimationId);
    }

    // -----------------------------------------------------------------------------------------
    // (w11) the heavy object.
    // -----------------------------------------------------------------------------------------

    private static (double X, uint Animation, int Z) HoldRightAndJump(int ticksPerFrame, bool heavy)
    {
        var from = int.MaxValue;
        var rig = CarryRig.Build(ticksPerFrame, t => t >= from ? PadState.Right | (t == from + 4 ? PadState.Cross : 0u) : 0u);
        var crate = rig.AddCrate(109, 100, 32); // on the head of the hero.
        if (heavy)
        {
            crate.Flags |= EntityFlags.PickupKindMask; // the kind 3: 0x3323E4 | 0x600.
        }

        rig.Run(3);
        rig.LiftNow(fromTheHead: true);
        rig.Run(3);
        Assert.Equal(12u, rig.Hero.TargetAnimationId);
        from = rig.Tick + 1;
        var maxZ = 0;
        rig.Run(30, _ => maxZ = Math.Max(maxZ, rig.Hero.PosZ));
        return (rig.Hero.PosX / (double)Px, rig.Hero.TargetAnimationId, maxZ);
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void W11_AHeavyObject_KeepsTheHeroAtTheStand_DeafToTheDirectionAndTheCross_NextToTheObjectOfKind1ThatWalksAndJumps(int ticksPerFrame)
    {
        var heavy = HoldRightAndJump(ticksPerFrame, heavy: true);
        var light = HoldRightAndJump(ticksPerFrame, heavy: false);

        Assert.Equal(12u, heavy.Animation);
        Assert.Equal(109.0, heavy.X);
        Assert.Equal(0, heavy.Z);

        Assert.Equal(7u, light.Animation);
        Assert.True(light.X > 109.0 + 10.0, $"the hero with the object of kind 1 walks: {light.X}");
        Assert.True(light.Z > 0, "the hero with the object of kind 1 jumped");
    }
}
