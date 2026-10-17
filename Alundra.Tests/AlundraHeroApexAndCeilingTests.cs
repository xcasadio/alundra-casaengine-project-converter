#nullable enable
using System;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Physics;
using Alundra.Transitions;

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
        GameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    /// <summary>Terrain cells of <paramref name="heightCells"/> x 16 px from x = 144 px (the cells are 24 px wide: from the sixth), flat 0 before.</summary>
    private static AlundraCellsCollisionField LedgeField(int heightCells) => FlatCells.Create(cell: (cx, _) => (0, cx >= 6 ? heightCells : 0));

    /// <summary>The walking jump of the plan: the animation 2 carries <paramref name="izf"/> (imposed by the montage), the hero walks east at the steady force.</summary>
    private static JumpHeroRig WalkingJumpRig(int izf, int ledgeHeightCells, float x, Action<World, ContactHost>? configure = null)
    {
        var rig = JumpHeroRig.Build(LedgeField(ledgeHeightCells), configure: configure, x: x, freePad: true);
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

    /// <summary>The montage of UH-CAP: a hero without gravity at 1792 px, the script <c>0x1B</c> giving it a rise of 64 px per tick; <paramref name="roofZ"/> (pixels) lays a roof box on its way.</summary>
    private static JumpHeroRig CapRig(int roofZ)
    {
        var rig = JumpHeroRig.Build(z: 1792f, gravityFlag: false, configure: (world, host) => AddBox(world, host, "Roof", 190, 90, roofZ, 24, 16, 8));
        var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 }, Codes = new[] { 0x1B, 0x00, 0x40, 0xFF } };
        var runner = new AlundraEventProgramRunner(document, new GameState(), null);
        runner.RunOneScriptCall(rig.Hero, new EventProgramState { Codes = document.CodesAsBytes() });
        Assert.Equal(4194304, rig.Hero.ForceZ);
        return rig;
    }

    [Fact]
    public void UH_CAP_AHeroWithoutGravityRisingAt64PixelsPerTick_StopsAtTheAbsoluteCeilingOf1920Pixels()
    {
        var rig = JumpHeroRig.Build(z: 1792f, gravityFlag: false);
        var hero = rig.Hero;
        var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 }, Codes = new[] { 0x1B, 0x00, 0x40, 0xFF } };
        var runner = new AlundraEventProgramRunner(document, new GameState(), null);
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

    /// <summary>
    /// E19.m13 (M-11): the former UH-APEX-REVERT, a jump IN PLACE over a crate of 2 px. Since the gates of the snap (no XY force, no snap) the cancel rule is no longer what keeps the
    /// hero there: this is a resting guard (the hero stays on the crate, never in the ground); the cancel rule is pinned by <see cref="UH_APEX_REVERT_Walking_ACrateOf2PixelsUnderAWalkingHero_CancelsTheSnapToTheTerrain"/>.
    /// </summary>
    [Fact]
    public void UH_APEX_REST_ACrateOf2PixelsUnderAJumpInPlace_KeepsTheHeroOnItsTop()
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

    /// <summary>
    /// E19.m13 (M-11), the cancel rule of H3-R1 on a walking jump: a wide crate of 2 px under the hero; at the apex the hero is within 3 px of the terrain (0), the snap would put it IN
    /// the crate and is undone. The two gates are open here (walking: an XY force and an accepted step, asserted by the increasing PosX), so it is the cancel that holds the hero.
    /// </summary>
    [Fact]
    public void UH_APEX_REVERT_Walking_ACrateOf2PixelsUnderAWalkingHero_CancelsTheSnapToTheTerrain()
    {
        var rig = WalkingJumpRig(1280, 0, 190f, (world, host) => AddBox(world, host, "Crate", 150, 90, 0, 150, 24, 2));
        var hero = rig.Hero;
        var landedAt = 0;
        for (var tick = 1; tick <= 60 && landedAt == 0; tick++)
        {
            rig.Step(Right | Cross, tick == 1 ? Cross : 0);
            if (tick > 10 && hero.CollidedWithEntityZ == 1)
            {
                landedAt = tick;
            }
        }

        Assert.True(landedAt > 0, "the hero never landed on the crate");
        Assert.True(hero.PosZ == 131072, $"landing: posZ {hero.PosZ}");
        var previousX = hero.PosX;
        for (var extra = 1; extra <= 8; extra++)
        {
            rig.Step(Right);
            var label = $"{extra} ticks after the landing";
            Assert.True(hero.PosZ == 131072, $"{label}: posZ {hero.PosZ}");
            Assert.True(hero.IsOnGround == 1, $"{label}: onGround {hero.IsOnGround}");
            Assert.True(hero.PosX > previousX, $"{label}: posX {hero.PosX} after {previousX}");
            previousX = hero.PosX;
        }
    }

    // -----------------------------------------------------------------------------------------
    // E19.m13 (M-11, O-E19-44) - the two gates of the snap at the apex (binary 0x800377A0/0x800377A8 and 0x80037938-0x80037948)
    // -----------------------------------------------------------------------------------------

    private static void BoxAt34(World world, ContactHost host) => AddBox(world, host, "Box", 190, 90, 34, 24, 16, 32);

    [Fact]
    public void UH_GATE_XY_AJumpInPlaceUnderABoxAt34Pixels_DoesNotSnap_AndTheHeroLandsAtTick4()
    {
        var rig = JumpHeroRig.Build(configure: BoxAt34, freePad: true);
        var hero = rig.Hero;
        var expected = new[] { 131072, 98304, 32768, 0 };
        for (var tick = 1; tick <= 4; tick++)
        {
            rig.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
            var label = $"tick {tick}";
            if (tick == 1)
            {
                Assert.True(hero.FinalForceX == 0 && hero.FinalForceY == 0 && hero.LastTickDeltaX == 0 && hero.LastTickDeltaY == 0,
                    $"{label}: the jump in place has no XY force ({hero.FinalForceX}, {hero.FinalForceY}) nor displacement ({hero.LastTickDeltaX}, {hero.LastTickDeltaY})");
                Assert.True(hero.ForceZ == 0, $"{label}: forceZ {hero.ForceZ}");
                Assert.True(hero.CollidedWithEntityZ == 1, $"{label}: collided {hero.CollidedWithEntityZ}");
                Assert.True(hero.IsOnGround == 0, $"{label}: onGround {hero.IsOnGround}");
            }

            Assert.True(expected[tick - 1] == hero.PosZ, $"{label}: posZ {hero.PosZ}");
        }

        Assert.True(hero.CollidedWithEntityZ == 1, $"tick 4: collided {hero.CollidedWithEntityZ}");
        Assert.True(hero.IsOnGround == 1, $"tick 4: onGround {hero.IsOnGround}");
    }

    /// <summary>The control of the gates: a walking jump (east, then north) under the same kind of box (laid from x = 100) does snap at tick 1: the gates are open when the binary snaps.</summary>
    [Theory]
    [InlineData(East, 159744, 0, Right)]
    [InlineData(16, 0, -106496, AlundraPadState.Up)]
    public void UH_GATE_XY_Control_AWalkingJumpUnderTheSameBox_SnapsAtTick1(int direction, int forceX, int forceY, uint pad)
    {
        var rig = WalkingJumpRig(1280, 0, 120.25f, (world, host) => AddBox(world, host, "Roof", 100, 90, 34, 80, 40, 32));
        var hero = rig.Hero;
        hero.TargetDirection = (uint)direction;
        hero.ForceX = forceX;
        hero.ForceY = forceY;
        rig.Step(pad | Cross, Cross);

        if (forceX != 0)
        {
            Assert.True(hero.FinalForceX != 0 && hero.LastTickDeltaX != 0, $"precondition: finalForceX {hero.FinalForceX}, deltaX {hero.LastTickDeltaX}");
        }
        else
        {
            Assert.True(hero.FinalForceY != 0 && hero.LastTickDeltaY != 0, $"precondition: finalForceY {hero.FinalForceY}, deltaY {hero.LastTickDeltaY}");
        }

        Assert.True(hero.PosZ == 0, $"tick 1: posZ {hero.PosZ}");
        Assert.True(hero.CollidedWithEntityZ == 1, $"tick 1: collided {hero.CollidedWithEntityZ}");
        Assert.True(hero.IsOnGround == 1, $"tick 1: onGround {hero.IsOnGround}");

        rig.Step(pad | Cross);
        Assert.True(hero.CollidedWithEntityZ == 1 && hero.PosZ == 0, $"tick 2: collided {hero.CollidedWithEntityZ}, posZ {hero.PosZ}");
    }

    [Fact]
    public void UH_GATE_BLOCKED_AJumpAgainstAWallUnderTheBox_DoesNotSnap_BecauseTheStepIsFullyBlocked()
    {
        var rig = JumpHeroRig.Build(FlatCells.Create(cell: (cx, _) => (cx >= 6 ? 0x40 : 0, 0)),
            configure: (world, host) => AddBox(world, host, "Roof", 100, 90, 34, 80, 40, 32), x: 120.25f, freePad: true);
        rig.Controller.Settings.WalkabilityMask = 0x40;
        var hero = rig.Hero;
        hero.Flags |= EntityFlags.NoObstacleSlide;
        hero.TargetAnimationId = 1;
        hero.TargetDirection = East;
        hero.ForceX = 159744;
        hero.ForceY = 0;

        for (var tick = 1; tick <= 12; tick++)
        {
            rig.Step(Right);
        }

        var wallX = hero.PosX;
        rig.Step(Right);
        Assert.True(hero.PosX == wallX, $"precondition: the hero is not against the wall (posX {wallX} then {hero.PosX})");

        var expected = new[] { 131072, 98304, 32768, 0 };
        for (var tick = 1; tick <= 4; tick++)
        {
            rig.Step(Right | Cross, tick == 1 ? Cross : 0);
            var label = $"tick {tick}";
            if (tick == 1)
            {
                Assert.True(hero.FinalForceX != 0, $"{label}: precondition finalForceX {hero.FinalForceX}");
                Assert.True(hero.LastTickDeltaX == 0 && hero.LastTickDeltaY == 0, $"{label}: precondition delta ({hero.LastTickDeltaX}, {hero.LastTickDeltaY})");
                Assert.True(hero.ForceZ == 0, $"{label}: forceZ {hero.ForceZ}");
                Assert.True(hero.CollidedWithEntityZ == 1, $"{label}: collided {hero.CollidedWithEntityZ}");
                Assert.True(hero.IsOnGround == 0, $"{label}: onGround {hero.IsOnGround}");
            }

            Assert.True(hero.PosX == wallX, $"{label}: posX {hero.PosX} (wall {wallX})");
            Assert.True(expected[tick - 1] == hero.PosZ, $"{label}: posZ {hero.PosZ}");
        }

        Assert.True(hero.IsOnGround == 1, $"tick 4: onGround {hero.IsOnGround}");
    }

    // -----------------------------------------------------------------------------------------
    // E19.m13 (M-45) - the absolute ceiling no longer ends the search of the entities (binary 0x80036D94)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UH_CAP_ENTITY_ARoofBelowTheCap_LowersTheAbsoluteCeiling()
    {
        var rig = CapRig(1900);
        var hero = rig.Hero;
        for (var tick = 1; tick <= 5; tick++)
        {
            rig.Update();
            var label = $"tick {tick}";
            if (tick == 1)
            {
                Assert.True(hero.PosZ > 1792 << 16 && hero.PosZ < 1900 << 16, $"{label}: posZ {hero.PosZ}"); // under the roof, as UH-CAP's tick 1 (measured 121536512, like UH-CAP without roof).
                Assert.True(hero.CollidedWithEntityZ == 0, $"{label}: collided {hero.CollidedWithEntityZ}");
            }
            else
            {
                Assert.True(hero.PosZ == 122421248, $"{label}: posZ {hero.PosZ}");
                Assert.True(hero.CollidedWithEntityZ == 1, $"{label}: collided {hero.CollidedWithEntityZ}");
            }

            Assert.True(hero.ForceZ == 4194304, $"{label}: forceZ {hero.ForceZ}");
        }
    }

    [Fact]
    public void UH_CAP_ENTITY_ARoofAboveTheCap_DoesNotLowerTheCeiling()
    {
        var rig = CapRig(1925);
        var hero = rig.Hero;
        for (var tick = 1; tick <= 5; tick++)
        {
            rig.Update();
            var label = $"tick {tick}";
            if (tick >= 2)
            {
                Assert.True(hero.PosZ == 0x7600000 - 1, $"{label}: posZ {hero.PosZ}");
                Assert.True(hero.CollidedWithEntityZ == 1, $"{label}: collided {hero.CollidedWithEntityZ}");
            }

            Assert.True(hero.ForceZ == 4194304, $"{label}: forceZ {hero.ForceZ}");
        }
    }

    // -----------------------------------------------------------------------------------------
    // E19.m13 (M-45, tests half) - the edges of the window (3 px down snaps, 4 px does not). The pin of the Gravity gate is left out: its montage (a hero in the air state without gravity at ForceZ 0, 2 px up) is not stable (the hero is at 0 from tick 1 at HEAD too)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UH_WINDOW_DOWN_EDGE_ARoofAt35Pixels_SnapsTheHeroAtTick1()
    {
        var rig = WalkingJumpRig(1280, 0, 120.25f, (world, host) => AddBox(world, host, "Roof", 100, 90, 35, 80, 40, 32));
        rig.Step(Right | Cross, Cross);
        Assert.True(rig.Hero.PosZ == 0, $"tick 1: posZ {rig.Hero.PosZ}");
        Assert.True(rig.Hero.CollidedWithEntityZ == 1, $"tick 1: collided {rig.Hero.CollidedWithEntityZ}");
    }

    [Fact]
    public void UH_WINDOW_DOWN_EDGE_ARoofAt36Pixels_DoesNotSnapTheHero_AndItLandsAtTick5()
    {
        var rig = WalkingJumpRig(1280, 0, 120.25f, (world, host) => AddBox(world, host, "Roof", 100, 90, 36, 80, 40, 32));
        var hero = rig.Hero;
        var expected = new[] { 262144, 229376, 163840, 65536, 0 };
        for (var tick = 1; tick <= 5; tick++)
        {
            rig.Step(Right | Cross, tick == 1 ? Cross : 0);
            Assert.True(expected[tick - 1] == hero.PosZ, $"tick {tick}: posZ {hero.PosZ}");
        }

        Assert.True(hero.CollidedWithEntityZ == 1 && hero.IsOnGround == 1, $"tick 5: collided {hero.CollidedWithEntityZ}, onGround {hero.IsOnGround}");
    }
}
