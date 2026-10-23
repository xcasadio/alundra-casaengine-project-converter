#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Transitions;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Hero;

/// <summary>
/// E19.d2c2 D5b T1 (docs/plan-e19-opcodes.md §1.2h.3.2b, D-E19-117): the witnesses of the hero carried by a platform that moves in Z, written BEFORE the code, with the values of the
/// binary replayed (<c>docs/plan-e19-d5b-annexe/exec-2026-10-22/vec_d5b_out.txt</c>, <c>docs/plan-e19-d5b-annexe/check/t_land_on_rising.py</c>, <c>work/out/t5_dock_real.txt</c>).
/// Every value is relative: the hero's foot minus the top of its carrier (<c>rel</c>), which is the same in the DLL's convention as in the binary's. In the binary the hero
/// moves in the very tick of its carrier (rel 0 at every tick: phase 0), where the DLL reads the force of the carrier with a tick of delay (M1) and does not move the target first (M2).
/// The witnesses are red at the head of D5b and are made green by the tasks named in the plan: UH-16 (rising), E22, the dock rising (the entry rule) and the freeze by T3, UH-16 (descending), DESC, UH-19 and the dock descending by T4.
/// The montage is the one of UH-14 (<see cref="JumpHeroRig"/> with a free pad, the platform added after the hero); the object boxes are offset from their roots so that their physical
/// boxes never meet the hero's body.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class HeroPlatformRideTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public HeroPlatformRideTests()
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

    private static EntityScriptProxy AddObject(World world, ContactHost host, string name, int x, int y, int sizeX, int sizeY, int sizeZ, int offsetX = 0, int offsetY = 0, int z = 0)
    {
        var obj = ContactWorld.AddEntity(world, host, name, x, y, z, offsetX, offsetY, 0, sizeX, sizeY, sizeZ);
        obj.LogicContextEntity = obj.OwnerEntity!;
        return obj;
    }

    /// <summary>The top of the box of <paramref name="p"/> in the DLL's convention (the height the foot of a rider is at when it rests on it).</summary>
    private static int Top(EntityScriptProxy p) => p.PosZ + p.ModZ + p.Depth + 1;

    /// <summary>The hero's foot minus the top of <paramref name="carrier"/>, in 16.16 units (the <c>rel</c> of the binary vectors).</summary>
    private static int Rel(EntityScriptProxy hero, EntityScriptProxy carrier) => hero.PosZ + hero.ModZ - Top(carrier);

    /// <summary>The montage of UH-14: the hero rests on a platform with no gravity whose top is <c>platformZ + sizeZ</c> px; four settling frames.</summary>
    private static (JumpHeroRig Rig, EntityScriptProxy Platform) Montage(int platformZ, int sizeX = 24, int sizeY = 16, int sizeZ = 16, int offsetX = 44, int offsetY = 0)
    {
        var rig = JumpHeroRig.Build(x: 156.25f, freePad: true);
        var platform = AddObject(rig.World, rig.Host, "Platform", 100, 92, sizeX, sizeY, sizeZ, offsetX: offsetX, offsetY: offsetY, z: platformZ);
        rig.Hero.PosZ = (platformZ + sizeZ) << 16;
        rig.Hero.PushLogicalPositionToRoot();
        for (var settle = 0; settle < 4; settle++)
        {
            rig.Step();
        }

        Assert.Equal((platformZ + sizeZ) << 16, rig.Hero.PosZ);
        Assert.Same(platform.LogicContextEntity, rig.Hero.RidingEntity);
        return (rig, platform);
    }

    /// <summary>The montage of the lift of the map 22 (E22, DESC): the big platform 48 x 32 x 8 whose logical box (x 132..180, y 84..116) is under the hero.</summary>
    private static (JumpHeroRig Rig, EntityScriptProxy Platform) LiftMontage() => Montage(platformZ: 40, sizeX: 48, sizeY: 32, sizeZ: 8, offsetX: 32, offsetY: -8);

    /// <summary>The state of a rider carried by a platform, at the end of a tick: the binary's relations (rel 0, carried, no contact with the Z force of the carrier's, on the ground).</summary>
    private static void AssertCarried(string label, JumpHeroRig rig, EntityScriptProxy platform)
    {
        var hero = rig.Hero;
        Assert.True(Rel(hero, platform) == 0, $"{label}: foot minus top {Rel(hero, platform)}");
        Assert.True(ReferenceEquals(platform.LogicContextEntity, hero.RidingEntity), $"{label}: riding");
        Assert.True(hero.CollidedWithEntityZ == 0, $"{label}: collided {hero.CollidedWithEntityZ}");
        Assert.True(hero.IsOnGround == 1, $"{label}: onGround {hero.IsOnGround}");
    }

    // -----------------------------------------------------------------------------------------
    // UH-16 - the hero rides a platform driven in Z by ForceZ (no gravity), in phase 0.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UH16_APlatformRisingByOnePixelPerTick_CarriesTheHeroInPhase0_ForFiftyTicks_AndAfterItStops()
    {
        var (rig, platform) = Montage(platformZ: 0);
        var hero = rig.Hero;
        var start = platform.PosZ;
        platform.ForceZ = 65536;
        for (var tick = 1; tick <= 55; tick++)
        {
            if (tick == 51)
            {
                platform.ForceZ = 0;
            }

            rig.Step();
            var label = $"tick {tick}";
            Assert.True(platform.PosZ - start == Math.Min(tick, 50) * 65536, $"{label}: platform {platform.PosZ - start}");
            AssertCarried(label, rig, platform);
            Assert.True(hero.FinalForceZ == (tick <= 50 ? 65536 : 0), $"{label}: tick force {hero.FinalForceZ}");
        }
    }

    [Fact]
    public void UH16_APlatformDescendingByOnePixelPerTick_CarriesTheHeroInPhase0_AndNoFrameIsInTheAir()
    {
        var (rig, platform) = Montage(platformZ: 40);
        var hero = rig.Hero;
        var start = platform.PosZ;
        platform.ForceZ = -65536;
        for (var tick = 1; tick <= 30; tick++)
        {
            if (tick == 25)
            {
                platform.ForceZ = 0;
            }

            rig.Step();
            var label = $"tick {tick}";
            Assert.True(platform.PosZ - start == -Math.Min(tick, 24) * 65536, $"{label}: platform {platform.PosZ - start}");
            AssertCarried(label, rig, platform);
            Assert.NotEqual(44u, hero.CurrentAnimationId); // no air frame (the fall animation of the sets of the montage).
            Assert.NotEqual(45u, hero.CurrentAnimationId);
        }
    }

    // -----------------------------------------------------------------------------------------
    // E22 / DESC - the lift of the map 22 (platform 48 x 32 x 8), the binary's tick schedule of vec_d5b.py.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void E22_TheLiftRisingByOnePixelPerTickFromTick3For64Ticks_CarriesTheHero_RelZero_ForceOfTheTickCopied()
    {
        var (rig, platform) = LiftMontage();
        var hero = rig.Hero;
        var start = platform.PosZ;
        for (var tick = 0; tick < 72; tick++)
        {
            if (tick == 3)
            {
                platform.ForceZ = 65536;
            }

            if (tick == 67)
            {
                platform.ForceZ = 0;
            }

            rig.Step();
            var label = $"tick {tick}";
            var moved = tick < 3 ? 0 : Math.Min(tick - 2, 64);
            Assert.True(platform.PosZ - start == moved * 65536, $"{label}: platform {platform.PosZ - start}");
            AssertCarried(label, rig, platform);

            // vec_d5b_out.txt: ffz 65536 on the ticks 3 to 66, 0 before and after.
            Assert.True(hero.FinalForceZ == (tick >= 3 && tick <= 66 ? 65536 : 0), $"{label}: tick force {hero.FinalForceZ}");
        }
    }

    [Fact]
    public void DESC_TheLiftDescendingByOnePixelPerTickFromTick3For24Ticks_CarriesTheHero_RelZero_NeverFloating()
    {
        var (rig, platform) = LiftMontage();
        var start = platform.PosZ;
        for (var tick = 0; tick < 32; tick++)
        {
            if (tick == 3)
            {
                platform.ForceZ = -65536;
            }

            if (tick == 27)
            {
                platform.ForceZ = 0;
            }

            rig.Step();
            var label = $"tick {tick}";
            var moved = tick < 3 ? 0 : Math.Min(tick - 2, 24);
            Assert.True(platform.PosZ - start == -moved * 65536, $"{label}: platform {platform.PosZ - start}");
            AssertCarried(label, rig, platform);
        }
    }

    // -----------------------------------------------------------------------------------------
    // UH-19 - the hero falls onto a platform that rises (t_land_on_rising.py, the real pass of the binary).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(65536, 196608, 32768, 0)] // 1 px above, the platform rises 3 px per tick: it rises 0.5 px (the hero's fall), then 3 px per tick.
    [InlineData(16384, 65536, 16384, 1)] // 0.25 px above, the platform rises 1 px per tick: the hero lands (contact 1) on the platform that has risen under it by 0.25 px, then 1 px per tick.
    public void UH19_TheHeroFallingOntoARisingPlatform_StopsItBelowTheFoot_LandsOnIt_AndRidesItFromTheNextTick(int gap, int rise, int firstRise, int firstContact)
    {
        var (rig, platform) = Montage(platformZ: 40);
        var hero = rig.Hero;

        // The hero is lifted by `gap` above its rest on the platform (in the held state it entered at rest), and the platform starts to rise.
        hero.PosZ += gap;
        hero.PushLogicalPositionToRoot();
        platform.ForceZ = rise;
        var previous = platform.PosZ;
        for (var tick = 0; tick < 6; tick++)
        {
            rig.Step();
            var label = $"tick {tick}";
            var delta = platform.PosZ - previous;
            previous = platform.PosZ;
            Assert.True(delta == (tick == 0 ? firstRise : rise), $"{label}: platform rose {delta}");
            Assert.True(Rel(hero, platform) == 0, $"{label}: foot minus top {Rel(hero, platform)}");
            Assert.True(hero.IsOnGround == 1, $"{label}: onGround {hero.IsOnGround}");
            Assert.True(hero.CollidedWithEntityZ == (tick == 0 ? firstContact : 0), $"{label}: collided {hero.CollidedWithEntityZ}");
            Assert.True(ReferenceEquals(platform.LogicContextEntity, hero.RidingEntity) == (tick > 0), $"{label}: riding");
        }
    }

    // -----------------------------------------------------------------------------------------
    // The dock (t5_dock_real.txt): the hero straddles the 16 px floor and an object flush with it in a hollow; the object moves, the hero goes with it.
    // -----------------------------------------------------------------------------------------

    private static (JumpHeroRig Rig, EntityScriptProxy Block) DockMontage(int blockZ)
    {
        // The montage of UH-17: cells of 16 px (height 1) with one hollow cell at 0 (cx 8, x 192..216, row 6); the block (24 x 16 x 16) is in the hollow, its top flush with the floor when blockZ is 0.
        var field = FlatCells.Create(cell: (cx, cy) => (0, cx == 8 && cy == 6 ? 0 : 1));
        // The block is added AFTER the hero, as in the world (the hero is the first of the physics list, WorldProxy._tickEntities): a montage that ticks the carrier before the hero would
        // have the hero find its carrier already moved at the head of its tick (the binary's prepass, 0x800364C8, runs before every move; the montage has none).
        var rig = JumpHeroRig.Build(
            field,
            x: 193f,
            y: 103.5f,
            z: 16f + blockZ,
            freePad: true);
        var block = AddObject(rig.World, rig.Host, "Switch", 148, 96, 24, 16, 16, offsetX: 44, z: blockZ);
        rig.Hero.PosZ = (16 + blockZ) << 16; // the block joins the world with the first update: the hero is put on it afterwards, as in UH-14.
        rig.Hero.PushLogicalPositionToRoot();
        for (var i = 0; i < 4; i++)
        {
            rig.Step();
        }

        Assert.True(ReferenceEquals(block.LogicContextEntity, rig.Hero.RidingEntity), $"settled: hero z {rig.Hero.PosZ / 65536.0} airborne {rig.Hero.HeroAirborne} block z {block.PosZ / 65536.0}");
        return (rig, block);
    }

    [Fact]
    public void Dock_ABlockRisingByOnePixelPerTick_CarriesTheHeroStraddlingTheFloor_RelZero_AndItNeverStandsStill()
    {
        var (rig, block) = DockMontage(blockZ: 0);
        var hero = rig.Hero;
        var start = block.PosZ;
        block.ForceZ = 65536;
        var still = 0;
        var previous = block.PosZ;
        for (var tick = 1; tick <= 8; tick++)
        {
            if (tick == 5)
            {
                block.ForceZ = 0;
            }

            rig.Step();
            var label = $"tick {tick}";
            still = block.PosZ == previous && tick < 5 ? still + 1 : 0;
            previous = block.PosZ;
            Assert.True(still <= 2, $"{label}: the block stood still");
            Assert.True(block.PosZ - start == Math.Min(tick, 4) * 65536, $"{label}: block {block.PosZ - start}");
            Assert.True(Rel(hero, block) == 0, $"{label}: foot minus top {Rel(hero, block)}");
            Assert.True(ReferenceEquals(block.LogicContextEntity, hero.RidingEntity), $"{label}: riding");
            Assert.True(hero.IsOnGround == 1, $"{label}: onGround {hero.IsOnGround}");
            Assert.True(hero.CollidedWithEntityZ == 0, $"{label}: collided {hero.CollidedWithEntityZ}");
        }
    }

    [Fact]
    public void Dock_ABlockDescendingByOnePixelPerTickToTheDock_CarriesTheHeroDown_RelZero_AndTheContactIsRaisedAtTheArrival()
    {
        var (rig, block) = DockMontage(blockZ: 8);
        var hero = rig.Hero;
        block.ForceZ = -65536;
        for (var tick = 1; tick <= 12; tick++)
        {
            rig.Step();
            var label = $"tick {tick}";
            Assert.True(block.PosZ == Math.Max(8 - tick, 0) * 65536, $"{label}: block {block.PosZ / 65536.0}");
            Assert.True(Rel(hero, block) == 0, $"{label}: foot minus top {Rel(hero, block)}");
            Assert.True(ReferenceEquals(block.LogicContextEntity, hero.RidingEntity), $"{label}: riding");
            Assert.True(hero.IsOnGround == 1, $"{label}: onGround {hero.IsOnGround}");
        }

        // The block rests on the hollow's floor: the hero lands on it at every tick (the force of the carrier is still -1 px), as t10 to t13 of the binary.
        Assert.Equal(1048576, hero.PosZ);
        Assert.Equal(1, hero.CollidedWithEntityZ);
    }

    // -----------------------------------------------------------------------------------------
    // The freeze - a platform whose force is not zero and that has a rider does not stand still (the ceiling of the carriers taken alone blocks it).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Freeze_APlatformWithANonZeroForceAndARider_NeverStandsStillForMoreThanTwoTicks_AndTheRiderGoesWithIt()
    {
        var (rig, platform) = Montage(platformZ: 0);
        var hero = rig.Hero;
        var start = platform.PosZ;
        platform.ForceZ = 65536;
        var still = 0;
        var previous = platform.PosZ;
        for (var tick = 1; tick <= 20; tick++)
        {
            rig.Step();
            still = platform.PosZ == previous ? still + 1 : 0;
            previous = platform.PosZ;
            Assert.True(still <= 2, $"tick {tick}: the platform stood still for {still} ticks");
        }

        Assert.Equal(start + 20 * 65536, platform.PosZ);
        Assert.Equal(0, Rel(hero, platform));
        Assert.Same(platform.LogicContextEntity, hero.RidingEntity);
    }
}
