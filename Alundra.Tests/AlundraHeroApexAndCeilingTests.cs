#nullable enable
using System;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.h3 (docs/plan-e19-opcodes.md §1.2n.3, H3-1): the snap at the top of a jump (binary <c>0x80037848</c>) and the ceilings (binary <c>0x80036D94</c>) of the
/// hero in the air state, on <see cref="JumpHeroRig"/> with a free pad (real hero controller settings, synthetic cells) and on the montage of SJ-12 (<c>AddObject</c>).
/// The boxes are laid by <see cref="ContactWorld.AddEntity"/> (the DLL's convention: the floor is the height, without the binary's +1). Positions in 16.16.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraHeroApexAndCeilingTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    private const uint Cross = AlundraPadState.Cross;
    private const uint Right = AlundraPadState.Right;
    private const int East = 24;

    public AlundraHeroApexAndCeilingTests()
    {
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    /// <summary>Terrain cells of <paramref name="heightCells"/> x 16 px from x = 144 px (the cells are 24 px wide: from the sixth), flat 0 before.</summary>
    private static AlundraCellsCollisionField LedgeField(int heightCells) => FlatCells.Create(cell: (cx, _) => (0, cx >= 6 ? heightCells : 0));

    /// <summary>The walking jump of the plan: the animation 2 carries <paramref name="izf"/> (imposed by the montage), the hero walks east at the steady force.</summary>
    private static JumpHeroRig WalkingJumpRig(int izf, int ledgeHeightCells, float x)
    {
        var rig = JumpHeroRig.Build(LedgeField(ledgeHeightCells), x: x, freePad: true);
        rig.SetAnimSet(new AnimSetEntry { Anim = 2, Speed = 208, Acceleration = 1, IsZForceApplied = izf, Sfx = 10 });
        rig.Hero.TargetAnimationId = 1;
        rig.Hero.TargetDirection = East;
        rig.Hero.ForceX = 159744;
        rig.Hero.ForceY = 0;
        return rig;
    }

    private static AlundraEntityScriptProxy AddBox(World world, ContactHost host, string name, int x, int y, int z, int sizeX, int sizeY, int sizeZ)
    {
        var box = ContactWorld.AddEntity(world, host, name, x, y, z, 0, 0, 0, sizeX, sizeY, sizeZ);
        box.LogicContextEntity = box.OwnerEntity!;
        return box;
    }

    [Fact]
    public void UH_APEX_DOWN_ALedgeOf32PixelsJustBelowTheApex_SnapsTheHeroOnItsTopAtTick12()
    {
        var rig = WalkingJumpRig(1408, 2, 108f);
        var hero = rig.Hero;
        var expected = new[] { 360448, 688128, 983040, 1245184, 1474560, 1671168, 1835008, 1966080, 2064384, 2129920, 2162688 };
        for (var tick = 1; tick <= 13; tick++)
        {
            rig.Step(Right | Cross, tick == 1 ? Cross : 0);
            var label = $"tick {tick}";
            if (tick <= 11)
            {
                Assert.True(expected[tick - 1] == hero.PosZ, $"{label}: posZ {hero.PosZ}");
            }
            else if (tick == 12)
            {
                Assert.True(hero.ForceZ == 0, $"{label}: forceZ {hero.ForceZ}");
                Assert.True(hero.PosZ == 2097152, $"{label}: posZ {hero.PosZ}");
                Assert.True(hero.IsOnGround == 1, $"{label}: onGround {hero.IsOnGround}");
                Assert.True(hero.CollidedWithEntityZ == 0, $"{label}: collided {hero.CollidedWithEntityZ}");
            }
            else
            {
                Assert.True(hero.CollidedWithEntityZ == 1, $"{label}: collided {hero.CollidedWithEntityZ}");
                Assert.True(hero.CurrentAnimationId == 1u, $"{label}: animation {hero.CurrentAnimationId}");
            }
        }
    }

    [Fact]
    public void UH_APEX_UP_ALedgeOf48PixelsJustAboveTheApex_SnapsTheHeroOnItsTopAtTick14()
    {
        var rig = WalkingJumpRig(1664, 3, 120f);
        var hero = rig.Hero;
        for (var tick = 1; tick <= 14; tick++)
        {
            rig.Step(Right | Cross, tick == 1 ? Cross : 0);
        }

        Assert.True(hero.PosZ == 3145728, $"posZ {hero.PosZ}");
        Assert.True(hero.PosX == 8866816, $"posX {hero.PosX}");
        Assert.True(hero.IsOnGround == 1, $"onGround {hero.IsOnGround}");
    }

    [Fact]
    public void SJ_14_ABoxOf32PixelsLaidAt48Pixels_StopsTheHeadOfTheHeroAtTick4_AndTheHeroFallsFromRest()
    {
        var rig = JumpHeroRig.Build(configure: (world, host) => AddBox(world, host, "Box", 190, 90, 48, 24, 16, 32), freePad: true);
        var hero = rig.Hero;
        var expected = new[] { 327680, 622592, 884736, 1048576, 1015808, 950272, 851968, 720896, 557056, 360448, 131072, 0 };
        for (var tick = 1; tick <= 12; tick++)
        {
            rig.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
            var label = $"tick {tick}";
            Assert.True(expected[tick - 1] == hero.PosZ, $"{label}: posZ {hero.PosZ}");
            if (tick == 4)
            {
                Assert.True(hero.ForceZ == 0, $"{label}: forceZ {hero.ForceZ}");
                Assert.True(hero.CollidedWithEntityZ == 1, $"{label}: collided {hero.CollidedWithEntityZ}");
            }
            else if (tick < 4)
            {
                Assert.True(hero.CollidedWithEntityZ == 0, $"{label}: collided {hero.CollidedWithEntityZ}");
            }
        }

        Assert.True(hero.IsOnGround == 1, $"onGround {hero.IsOnGround}");
    }

    [Fact]
    public void UH_12_ASlabLaidAt40Pixels_StopsTheHeadOfTheHeroAtTick2_AndTheHeroLandsAtTick8()
    {
        var rig = JumpHeroRig.Build(configure: (world, host) => AddBox(world, host, "Slab", 190, 90, 40, 24, 16, 16), freePad: true);
        var hero = rig.Hero;
        var expected = new[] { 327680, 524288, 491520, 425984, 327680, 196608, 32768, 0 };
        for (var tick = 1; tick <= 8; tick++)
        {
            rig.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
            var label = $"tick {tick}";
            Assert.True(expected[tick - 1] == hero.PosZ, $"{label}: posZ {hero.PosZ}");
            if (tick == 2)
            {
                Assert.True(hero.ForceZ == 0, $"{label}: forceZ {hero.ForceZ}");
                Assert.True(hero.CollidedWithEntityZ == 1, $"{label}: collided {hero.CollidedWithEntityZ}");
            }
            else if (tick == 1)
            {
                Assert.True(hero.CollidedWithEntityZ == 0, $"{label}: collided {hero.CollidedWithEntityZ}");
            }
        }
    }

    [Fact]
    public void UH_CAP_AHeroWithoutGravityRisingAt64PixelsPerTick_StopsAtTheAbsoluteCeilingOf1920Pixels()
    {
        var rig = JumpHeroRig.Build(z: 1792f, gravityFlag: false);
        var hero = rig.Hero;
        var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 }, Codes = new[] { 0x1B, 0x00, 0x40, 0xFF } };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), null);
        runner.RunOneScriptCall(hero, new EventProgramState { Codes = document.CodesAsBytes() });
        Assert.Equal(4194304, hero.ForceZ);

        for (var tick = 1; tick <= 5; tick++)
        {
            rig.Update();
            var label = $"tick {tick}";
            if (tick == 1)
            {
                Assert.True(hero.PosZ > 1792 << 16 && hero.PosZ < 0x7600000 - 1, $"{label}: posZ {hero.PosZ}"); // still under the ceiling.
                Assert.True(hero.CollidedWithEntityZ == 0, $"{label}: collided {hero.CollidedWithEntityZ}");
            }
            else
            {
                Assert.True(hero.PosZ == 0x7600000 - 1, $"{label}: posZ {hero.PosZ}");
                Assert.True(hero.CollidedWithEntityZ == 1, $"{label}: collided {hero.CollidedWithEntityZ}");
            }

            Assert.True(hero.ForceZ == 4194304, $"{label}: forceZ {hero.ForceZ}");
        }
    }

    [Fact]
    public void UH_APEX_REVERT_ACrateOf2PixelsUnderTheHero_CancelsTheSnapToTheTerrain()
    {
        var rig = JumpHeroRig.Build(configure: (world, host) => AddBox(world, host, "Crate", 190, 90, 0, 30, 24, 2), freePad: true);
        var hero = rig.Hero;
        var landedAt = 0;
        for (var tick = 1; tick <= 60 && landedAt == 0; tick++)
        {
            rig.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
            if (tick > 10 && hero.CollidedWithEntityZ == 1)
            {
                landedAt = tick;
            }
        }

        Assert.True(landedAt > 0, "the hero never landed on the crate");
        Assert.True(hero.PosZ == 131072, $"landing: posZ {hero.PosZ}");
        for (var extra = 1; extra <= 10; extra++)
        {
            rig.Step();
            var label = $"{extra} ticks after the landing";
            Assert.True(hero.PosZ == 131072, $"{label}: posZ {hero.PosZ}");
            Assert.True(hero.IsOnGround == 1, $"{label}: onGround {hero.IsOnGround}");
        }
    }
}
