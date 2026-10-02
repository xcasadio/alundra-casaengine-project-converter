#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c1 C3 (docs/plan-e19-opcodes.md §1.2h.3.1, R6 and R8): the hero in the air for the scripted jumps and for <c>0x1B</c>. The logic tick
/// owns the vertical (the binary's order: the force of the tick, then the strict landing test, then the XY step), the engine only follows the
/// root. Montage: <see cref="JumpHeroRig"/> (the real hero controller settings, the pad locked: the animation and the direction are written by the
/// test, and the interpreter is the real one for <c>0x1B</c> and <c>0x17</c>).
/// </summary>
public class AlundraHeroJumpTests
{
    /// <summary>The list of UJ-1b / UJ-3: IZF 1280, gravity 128 (positions in 16.16, no +1 of the binary).</summary>
    private static readonly int[] JumpHeights =
    {
        327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240,
        1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680,
    };

    /// <summary>UJ-3b: <c>0x1B [0,8]</c> (ForceZ 524288 before the tick, decaying from this very tick).</summary>
    private static readonly int[] FlyHeights =
    {
        491520, 950272, 1376256, 1769472, 2129920, 2457600, 2752512, 3014656, 3244032, 3440640,
        3604480, 3735552, 3833856, 3899392, 3932160, 3932160, 3899392, 3833856, 3735552, 3604480,
        3440640, 3244032, 3014656, 2752512, 2457600, 2129920, 1769472, 1376256, 950272, 491520,
    };

    /// <summary>Runs <paramref name="codes"/> (an event program of the hero, one call) on the hero of <paramref name="rig"/>.</summary>
    private static void RunProgram(JumpHeroRig rig, params int[] codes)
    {
        var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 }, Codes = codes };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), null);
        runner.RunOneScriptCall(rig.Hero, new EventProgramState { Codes = document.CodesAsBytes() });
    }

    [Fact]
    public void UJ3_AScriptedJumpOfTheHero_FliesTheListLandsOnTheStrictTest_AndGivesTheEngineBackItsValues()
    {
        var rig = JumpHeroRig.Build();
        var hero = rig.Hero;
        var (startX, startY) = (hero.PosX, hero.PosY);
        Assert.NotEqual(0, startX & 0xFFFF); // the fraction of the position is part of the test.
        Assert.Equal(1250f, rig.Controller.Settings.Gravity);

        hero.TargetAnimationId = 43;
        for (var update = 1; update <= 20; update++)
        {
            rig.Update();
            Assert.Equal(JumpHeights[update - 1], hero.PosZ);
            Assert.Equal(0, hero.IsOnGround);
            Assert.Equal(0, hero.CollidedWithEntityZ);
            Assert.True(rig.Controller.IsVerticalOwnedExternally, $"update {update}");
            Assert.Equal(0f, rig.Controller.Settings.Gravity);
            Assert.Equal(0f, rig.Controller.Settings.MaxFallSpeed);
            Assert.Equal(hero.PosZ / 65536f, rig.RootZ, 3); // the root follows the logical height.
            Assert.Equal((startX, startY), (hero.PosX, hero.PosY));
        }

        // Update 21: PosZ + F == the terrain (0), so the STRICT test of the binary does not land yet: the hero is at the ground, still in the air state.
        rig.Update();
        Assert.Equal(0, hero.PosZ);
        Assert.Equal(1, hero.IsOnGround);
        Assert.Equal(-327680, hero.ForceZ);
        Assert.Equal(0, hero.CollidedWithEntityZ);
        Assert.True(rig.Controller.IsVerticalOwnedExternally);
        Assert.Equal(0f, rig.Controller.Settings.Gravity);

        // Update 22: landed.
        rig.Update();
        Assert.Equal(0, hero.PosZ);
        Assert.Equal(0, hero.ForceZ);
        Assert.Equal(1, hero.CollidedWithEntityZ);
        Assert.Equal(1, hero.IsOnGround);
        Assert.False(rig.Controller.IsVerticalOwnedExternally);
        Assert.Equal(1250f, rig.Controller.Settings.Gravity);
        Assert.Equal(800f, rig.Controller.Settings.MaxFallSpeed);
        Assert.Equal(0f, rig.RootZ, 3);

        // Update 23: at rest with gravity the hero lands at every tick (R5 d).
        rig.Update();
        Assert.Equal(1, hero.CollidedWithEntityZ);
        Assert.Equal(0, hero.PosZ);
        Assert.Equal((startX, startY), (hero.PosX, hero.PosY));
    }

    [Fact]
    public void UJ3c_InTheAirStateTheLogicalPosZAndIsOnGroundAreTheTicksAndNotThePullOfTheEngine()
    {
        var rig = JumpHeroRig.Build();
        var hero = rig.Hero;
        hero.TargetAnimationId = 43;
        for (var update = 1; update <= 5; update++)
        {
            rig.Update();
        }

        // The root is disturbed (a lift of the engine): a frame without tick does not pull it into the logical height, and the next tick brings it back.
        var root = rig.HeroEntity.RootComponent!;
        var position = root.LocalTransform.Position;
        root.LocalTransform.Position = new Microsoft.Xna.Framework.Vector3(position.X, position.Y, position.Z + 3f);
        rig.Update(0.001f);
        Assert.Equal(JumpHeights[4], hero.PosZ);
        rig.Update();
        Assert.Equal(JumpHeights[5], hero.PosZ);
        Assert.Equal(JumpHeights[5] / 65536f, rig.RootZ, 3);

        for (var update = 7; update <= 21; update++)
        {
            rig.Update();
        }

        // Update 21: at the ground, still in the state, IsOnGround 1 (derived); a frame without tick keeps it (the engine, with the airborne latch, says 0).
        Assert.Equal(0, hero.PosZ);
        Assert.Equal(1, hero.IsOnGround);
        rig.Update(0.001f);
        Assert.Equal(1, hero.IsOnGround);
        Assert.Equal(0, hero.PosZ);
    }

    [Fact]
    public void UJ3b_AFlyOpcodeOnTheHero_DecaysFromItsOwnTick_AndLandsAtTheEnd()
    {
        var rig = JumpHeroRig.Build();
        var hero = rig.Hero;
        var (startX, startY) = (hero.PosX, hero.PosY);

        RunProgram(rig, 0x1B, 0x00, 0x08, 0xFF);
        Assert.Equal(524288, hero.ForceZ);

        for (var update = 1; update <= 30; update++)
        {
            rig.Update();
            Assert.Equal(FlyHeights[update - 1], hero.PosZ);
            Assert.Equal(0, hero.IsOnGround);
            Assert.True(rig.Controller.IsVerticalOwnedExternally, $"update {update}");
            Assert.Equal((startX, startY), (hero.PosX, hero.PosY));
        }

        rig.Update(); // 31: at the ground, the strict test does not land yet.
        Assert.Equal(0, hero.PosZ);
        Assert.Equal(1, hero.IsOnGround);
        Assert.Equal(0, hero.CollidedWithEntityZ);

        rig.Update(); // 32: landed.
        Assert.Equal(1, hero.CollidedWithEntityZ);
        Assert.Equal(0, hero.ForceZ);
        Assert.False(rig.Controller.IsVerticalOwnedExternally);
        Assert.Equal(1250f, rig.Controller.Settings.Gravity);
    }

    [Fact]
    public void UJ11_AFreezeAndAThawInTheAirOnAFrameWithoutTick_KeepTheHeroWhereItWas_AndTheFlightResumes()
    {
        // A big snap distance: a frame in which the vertical is no longer declared external would put the root back on the ground.
        var rig = JumpHeroRig.Build(groundSnapDistance: 64f);
        var hero = rig.Hero;
        hero.TargetAnimationId = 43;
        for (var update = 1; update <= 5; update++)
        {
            rig.Update();
        }

        Assert.Equal(JumpHeights[4], hero.PosZ);

        AlundraGameplayFreeze.Freeze(hero);
        AlundraGameplayFreeze.Thaw(hero);
        rig.Update(0.001f); // a frame without a logic tick.

        Assert.True(rig.Controller.IsVerticalOwnedExternally);
        Assert.Equal(JumpHeights[4], hero.PosZ);
        Assert.Equal(JumpHeights[4] / 65536f, rig.RootZ, 3);
        Assert.False(rig.Controller.IsGrounded);

        rig.Update();
        Assert.Equal(JumpHeights[5], hero.PosZ);
        Assert.Equal(JumpHeights[5] / 65536f, rig.RootZ, 3);
    }

    [Fact]
    public void UJ12_TheHeroPushingAgainstA32PixelEntity_PassesOverItExactlyWhileItsHeightIsAtLeast32Pixels()
    {
        AlundraEntityScriptProxy? wall = null;
        var rig = JumpHeroRig.Build(
            probeFactory: host => new AlundraMovementObstacleProbe(host),
            x: 100.25f,
            configure: (world, host) => wall = ContactWorld.AddEntity(world, host, "Wall", 140, 100, 0, -10, -7, 0, 20, 14, 32));
        var hero = rig.Hero;
        hero.TargetDirection = 24; // east.
        hero.TargetAnimationId = 1;

        // The hero walks up to the entity and stays flush against it.
        for (var update = 0; update < 40 && !ReferenceEquals(hero.XCollisionEntity, wall); update++)
        {
            rig.Update();
        }

        Assert.Same(wall, hero.XCollisionEntity);
        rig.Update();
        rig.Update();
        Assert.Same(wall, hero.XCollisionEntity);

        RunProgram(rig, 0x1B, 0x00, 0x08, 0xFF);
        for (var update = 1; update <= 26; update++)
        {
            var before = hero.PosX;
            rig.Update();
            if (update < 5)
            {
                Assert.True(hero.PosZ < 2097152, $"update {update}");
                Assert.Same(wall, hero.XCollisionEntity);
                Assert.True(hero.PosX - before < 4096, $"update {update}: blocked, the step is at most the contact");
            }
            else
            {
                Assert.True(hero.PosZ >= 2097152, $"update {update}");
                Assert.Null(hero.XCollisionEntity);
                Assert.True(hero.PosX - before > 150000, $"update {update}: free, the step is the force of the tick");
            }
        }
    }

    [Theory]
    [InlineData(false, 1250f)]
    [InlineData(true, 0f)]
    public void TRP_TheEngineValuesLiveAtEntryAreGivenBack_ALowGravityOpcodeBeforeTheJumpIncluded(bool lowGravityOpcodeBefore, float gravityAfterLanding)
    {
        var rig = JumpHeroRig.Build();
        var hero = rig.Hero;
        if (lowGravityOpcodeBefore)
        {
            RunProgram(rig, 0x17, 0xFF);
            Assert.Equal(0f, rig.Controller.Settings.Gravity);
            hero.Flags |= EntityFlags.Gravity; // the bit MovePlayer posts again in free play.
        }

        hero.TargetAnimationId = 43;
        rig.Update();
        Assert.True(rig.Controller.IsVerticalOwnedExternally, "the vertical is the tick's during the jump");
        for (var update = 2; update <= 22; update++)
        {
            rig.Update();
        }

        Assert.Equal(1, hero.CollidedWithEntityZ);
        Assert.False(rig.Controller.IsVerticalOwnedExternally);
        Assert.Equal(gravityAfterLanding, rig.Controller.Settings.Gravity);
        Assert.Equal(lowGravityOpcodeBefore ? 0f : 800f, rig.Controller.Settings.MaxFallSpeed);
    }

    [Fact]
    public void UJSND_TheTakeOffSoundIsAskedOnceAtTheTickOfTheImpulse_NeverForTheFlyOpcode_NorAtAnArrival()
    {
        var sounds = new RecordingSoundPlayer();
        var rig = JumpHeroRig.Build();
        rig.Host.SoundPlayer = sounds;
        rig.Hero.TargetAnimationId = 43;
        rig.Update();
        Assert.Equal(new[] { 10 }, sounds.Requests.ToArray());
        for (var update = 2; update <= 25; update++)
        {
            rig.Update();
        }

        Assert.Equal(new[] { 10 }, sounds.Requests.ToArray());

        // Under catch-up (two ticks in the frame) the impulse and its sound are still given once.
        var catchUp = new RecordingSoundPlayer();
        var rig2 = JumpHeroRig.Build();
        rig2.Host.SoundPlayer = catchUp;
        rig2.Hero.TargetAnimationId = 43;
        rig2.Update(0.04f);
        Assert.Equal(new[] { 10 }, catchUp.Requests.ToArray());
        Assert.Equal(JumpHeights[1], rig2.Hero.PosZ);

        // The fly opcode makes no sound.
        var silent = new RecordingSoundPlayer();
        var rig3 = JumpHeroRig.Build();
        rig3.Host.SoundPlayer = silent;
        RunProgram(rig3, 0x1B, 0x00, 0x08, 0xFF);
        for (var update = 1; update <= 10; update++)
        {
            rig3.Update();
        }

        Assert.NotEqual(0, rig3.Hero.PosZ);
        Assert.Empty(silent.Requests);

        // The animation of an arrival gives no impulse, hence no sound (R2): the flag the adoption of the pawn posts.
        var arrival = new RecordingSoundPlayer();
        var rig4 = JumpHeroRig.Build();
        rig4.Host.SoundPlayer = arrival;
        rig4.Hero.SpawnAnimationActive = true;
        rig4.Hero.SpawnAnimationId = 43;
        rig4.Hero.TargetAnimationId = 43;
        for (var update = 1; update <= 5; update++)
        {
            rig4.Update();
            Assert.Equal(0, rig4.Hero.PosZ);
        }

        Assert.Empty(arrival.Requests);
    }
}
