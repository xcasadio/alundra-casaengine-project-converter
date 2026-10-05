#nullable enable
using System;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c2 D1 (docs/plan-e19-opcodes.md §1.2h.3.2, S1): the jump states of <c>MovePlayer</c> - the binary's <c>0x80031E38</c>/<c>0x80031E84</c> and the common
/// tail <c>0x80031EA8</c>. The tests of <c>MovePlayer</c> alone (SJ-1 to SJ-4) work on a bare proxy (no controller: the air predicate reads
/// <see cref="AlundraEntityScriptProxy.HeroAirborne"/>), the tests of the whole flow (SJ-8, UH-4, UH-5, the return to Idle/Moving) on <see cref="JumpHeroRig"/>
/// with a free pad.
/// </summary>
public sealed class AlundraHeroJumpStatesTests
{
    private const uint Cross = AlundraPadState.Cross;
    private const uint Square = AlundraPadState.Square;
    private const uint Right = AlundraPadState.Right;
    private const uint Left = AlundraPadState.Left;

    private const uint Idle = 0;
    private const uint Moving = 1;
    private const uint JumpWalking = 2;
    private const uint JumpStanding = 0x2B;
    private const uint AirMoving = 0x2C;
    private const uint AirStill = 0x2D;

    /// <summary>The list of UJ-3 / UH-1: IZF 1280, gravity 128 (positions in 16.16, no +1 of the binary).</summary>
    private static readonly int[] JumpHeights =
    {
        327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240,
        1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680,
    };

    private static AlundraEntityScriptProxy Hero(uint animation, int onGround = 1, uint direction = 0)
        => new() { TargetAnimationId = animation, TargetDirection = direction, IsOnGround = onGround };

    private static AlundraPadState Pad(uint hold = 0, uint pressed = 0) => new() { ButtonsHold = hold, ButtonsJustPressed = pressed };

    private static void Move(AlundraEntityScriptProxy hero, AlundraPadState pad, AlundraGameState? state = null, IAlundraScriptHost? host = null)
        => AlundraPlayerManager.MovePlayer(hero, in pad, state ?? new AlundraGameState(), host);

    // -----------------------------------------------------------------------------------------
    // SJ-1 - MovePlayer alone, on the ground, without controller.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SJ1a_IdleWithTheCrossEdge_StartsTheStandingJump_AndKeepsItsDirection()
    {
        var hero = Hero(Idle, direction: 0x08);
        Move(hero, Pad(Cross, Cross));
        Assert.Equal(JumpStanding, hero.TargetAnimationId);
        Assert.Equal(0x08u, hero.TargetDirection);
    }

    [Fact]
    public void SJ1b_MovingWithARightHeldAndTheCrossEdge_StartsTheWalkingJump_FacingRight()
    {
        var hero = Hero(Moving, direction: 0x18);
        Move(hero, Pad(Right | Cross, Cross));
        Assert.Equal(JumpWalking, hero.TargetAnimationId);
        Assert.Equal(0x18u, hero.TargetDirection);

        var fromIdle = Hero(Idle);
        Move(fromIdle, Pad(Right | Cross, Cross));
        Assert.Equal(JumpWalking, fromIdle.TargetAnimationId);
        Assert.Equal(0x18u, fromIdle.TargetDirection);
    }

    [Fact]
    public void SJ1c_TheCrossHeldWithoutANewEdge_DoesNotJump()
    {
        var hero = Hero(Idle);
        Move(hero, Pad(Cross));
        Assert.Equal(Idle, hero.TargetAnimationId);

        var walking = Hero(Moving, direction: 0x18);
        Move(walking, Pad(Right | Cross));
        Assert.Equal(Moving, walking.TargetAnimationId);
    }

    [Fact]
    public void SJ1d_TheCellsThatForbidTheJump_EndTheCaseWithoutChangingAnything_NotEvenTheMoving()
    {
        var idle = Hero(Idle);
        idle.CombinedVramFlagsOR = 0x4000;
        Move(idle, Pad(Cross, Cross));
        Assert.Equal(Idle, idle.TargetAnimationId);

        // The pad is clear: the case ends and the animation stays the walking one (the tail does not reach its Moving/Idle write).
        var moving = Hero(Moving, direction: 0x18);
        moving.CombinedVramFlagsOR = 0x4000;
        Move(moving, Pad(Cross, Cross));
        Assert.Equal(Moving, moving.TargetAnimationId);

        // A direction held: the hero does not even become Moving (UH-5, M9 of the annex).
        var still = Hero(Idle);
        still.CombinedVramFlagsOR = 0x4000;
        Move(still, Pad(Right | Cross, Cross));
        Assert.Equal(Idle, still.TargetAnimationId);

        // Another bit of the flags does not forbid it.
        var water = Hero(Idle);
        water.CombinedVramFlagsOR = 0x1800;
        Move(water, Pad(Cross, Cross));
        Assert.Equal(JumpStanding, water.TargetAnimationId);
    }

    [Fact]
    public void SJ1e_TheInteractionWinsOverTheJump_TheButtonOneAndTheContactOne()
    {
        // A NPC that asks for the button: Square and Cross on the same frame -> Idle, the NPC is the active collision, no jump (UH-4 d).
        var host = new ContactHost();
        var npc = new AlundraEntityScriptProxy { Flags = EntityFlags.InteractRequiresButton | EntityFlags.Collidable, Status = EntityStatus.Normal };
        npc.ProgramIndexes[ScriptHelper.ProgramFInteract] = 1;
        var hero = Hero(Moving, direction: 0x18);
        hero.XCollisionEntity = npc;
        Move(hero, Pad(Square | Cross, Square | Cross), host.GameState, host);
        Assert.Equal(Idle, hero.TargetAnimationId);
        Assert.Same(npc, host.ActiveCollisionEntity);

        // A NPC of automatic contact: the case ends with the animation untouched (the hero stays Moving although the pad is clear), no jump.
        var contactHost = new ContactHost();
        var contactNpc = new AlundraEntityScriptProxy { Flags = EntityFlags.Collidable, Status = EntityStatus.Normal };
        contactNpc.ProgramIndexes[ScriptHelper.ProgramFInteract] = 1;
        var walker = Hero(Moving, direction: 0x18);
        walker.XCollisionEntity = contactNpc;
        Move(walker, Pad(Cross, Cross), contactHost.GameState, contactHost);
        Assert.Equal(Moving, walker.TargetAnimationId);
        Assert.Same(contactNpc, contactHost.ActiveCollisionEntity);
    }

    [Fact]
    public void SJ1f_AControlLockedHero_DoesNotJump_UnlessTheDebugSwitchIsOn()
    {
        var locked = new AlundraGameState { PlayerControlFlags = AlundraGameState.PlayerControlBits.ControlLocked };
        var hero = Hero(Idle);
        Move(hero, Pad(Cross, Cross), locked);
        Assert.Equal(Idle, hero.TargetAnimationId);

        AlundraPlayerManager.SetDebugIgnoreControlLockOverrideForTests(true);
        try
        {
            var debug = Hero(Idle);
            Move(debug, Pad(Cross, Cross), locked);
            Assert.Equal(JumpStanding, debug.TargetAnimationId);
        }
        finally
        {
            AlundraPlayerManager.SetDebugIgnoreControlLockOverrideForTests(null);
        }
    }

    // -----------------------------------------------------------------------------------------
    // SJ-2 / SJ-3 - the air and the landing.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(JumpWalking)]
    [InlineData(JumpStanding)]
    [InlineData(AirMoving)]
    [InlineData(AirStill)]
    [InlineData(Moving)]
    [InlineData(Idle)]
    public void SJ2_InTheAir_TheTargetIsTheMovingOrTheStillAirAnimation_AndTheDirectionFollowsTheStick(uint start)
    {
        var right = Hero(start, onGround: 0);
        right.HeroAirborne = true;
        Move(right, Pad(Right));
        Assert.Equal(AirMoving, right.TargetAnimationId);
        Assert.Equal(0x18u, right.TargetDirection);

        var nothing = Hero(start, onGround: 0, direction: 0x18);
        nothing.HeroAirborne = true;
        Move(nothing, Pad());
        Assert.Equal(AirStill, nothing.TargetAnimationId);
        Assert.Equal(0x18u, nothing.TargetDirection);

        // The direction changed in the air takes effect the very frame (Left = 0x08).
        var reversed = Hero(start, onGround: 0, direction: 0x18);
        reversed.HeroAirborne = true;
        Move(reversed, Pad(Left));
        Assert.Equal(AirMoving, reversed.TargetAnimationId);
        Assert.Equal(0x08u, reversed.TargetDirection);

        // The edge of the Cross in the air changes nothing.
        var crossed = Hero(start, onGround: 0);
        crossed.HeroAirborne = true;
        Move(crossed, Pad(Cross, Cross));
        Assert.Equal(AirStill, crossed.TargetAnimationId);
    }

    [Theory]
    [InlineData(AirMoving)]
    [InlineData(AirStill)]
    public void SJ3_OnTheGround_TheAirAnimationsGoBackToTheIdleMovingOrJumpAgain(uint start)
    {
        var right = Hero(start);
        Move(right, Pad(Right));
        Assert.Equal(Moving, right.TargetAnimationId);

        var nothing = Hero(start);
        Move(nothing, Pad());
        Assert.Equal(Idle, nothing.TargetAnimationId);

        var jump = Hero(start);
        Move(jump, Pad(Cross, Cross));
        Assert.Equal(JumpStanding, jump.TargetAnimationId);

        var walkingJump = Hero(start);
        Move(walkingJump, Pad(Right | Cross, Cross));
        Assert.Equal(JumpWalking, walkingJump.TargetAnimationId);
        Assert.Equal(0x18u, walkingJump.TargetDirection);

        // The run of the Triangle is a no-op of the port (S1): Triangle alone gives the Idle, like today.
        var triangle = Hero(start);
        Move(triangle, Pad(AlundraPadState.Triangle));
        Assert.Equal(Idle, triangle.TargetAnimationId);
    }

    [Fact]
    public void SJ3b_TheLandingAnimationsAreTheJumpStatesToo_ForTheTakeOffOnTheGround()
    {
        // 2 and 0x2B on the ground (the frame after a take-off with a tick run): the case rewrites them like the others.
        foreach (var start in new[] { JumpWalking, JumpStanding })
        {
            var hero = Hero(start);
            hero.JumpStartTickStamp = -1;
            Move(hero, Pad(Right));
            Assert.Equal(Moving, hero.TargetAnimationId);
        }
    }

    // -----------------------------------------------------------------------------------------
    // SJ-4 - the stamp of the tick.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SJ4_TheTakeOffTargetIsKeptUntilATickRan_ThenTheTailRewritesIt()
    {
        var hero = Hero(JumpWalking);
        hero.JumpStartTickStamp = hero.MotionTickCount;
        Move(hero, Pad());
        Assert.Equal(JumpWalking, hero.TargetAnimationId); // no tick since the take-off: not rewritten.

        AlundraPlayerManager.Tick(hero, 1);
        Move(hero, Pad());
        Assert.Equal(Idle, hero.TargetAnimationId); // a tick ran: the ground tail.

        // The stamp is the one the tail takes when it writes the take-off (2 or 0x2B).
        var standing = Hero(Idle);
        Move(standing, Pad(Cross, Cross));
        Assert.Equal(JumpStanding, standing.TargetAnimationId);
        Assert.Equal(standing.MotionTickCount, standing.JumpStartTickStamp);
        Move(standing, Pad());
        Assert.Equal(JumpStanding, standing.TargetAnimationId); // a second frame without tick: still the take-off.
        AlundraPlayerManager.Tick(standing, 1);
        Move(standing, Pad());
        Assert.Equal(Idle, standing.TargetAnimationId);
    }

    [Fact]
    public void SJ4b_ATakeOffWithoutAStampOfThisTick_IsRewrittenByTheTail_EvenOnAFrameWithoutTick()
    {
        // The guard of the stamp: the standing take-off (JumpStanding) on the ground is kept only when its stamp equals the tick count; a stamp of an earlier tick, or none
        // (MotionTickCount - 1, i.e. -1, the default value without a stamp), lets the tail rewrite it to Idle.
        var hero = Hero(JumpStanding);
        hero.JumpStartTickStamp = hero.MotionTickCount - 1;
        Move(hero, Pad());
        Assert.Equal(Idle, hero.TargetAnimationId);
    }

    // -----------------------------------------------------------------------------------------
    // Whole flow on the world rig (free pad).
    // -----------------------------------------------------------------------------------------

    private static JumpHeroRig FreeRig(Func<int, int, int>? groundProperty = null)
        => JumpHeroRig.Build(
            groundProperty == null ? null : FlatCells.Create(groundProperty: groundProperty),
            freePad: true);

    [Fact]
    public void SJ8a_TheCrossWhileAMenuIsOpen_DoesNotJump_AndTheEdgeIsNotKept()
    {
        var rig = FreeRig();
        rig.Host.GameState.PlayerControlFlags = AlundraGameState.PlayerControlBits.MenuOpen;
        rig.Step(Cross, Cross);
        Assert.Equal(Idle, rig.Hero.CurrentAnimationId);
        Assert.Equal(0, rig.Hero.PosZ);

        // The menu closes, the Cross stays held without a new edge: ten frames without a jump, and no impulse is owed.
        rig.Host.GameState.PlayerControlFlags = 0;
        for (var frame = 0; frame < 10; frame++)
        {
            rig.Step(Cross, 0);
            Assert.Equal(Idle, rig.Hero.CurrentAnimationId);
            Assert.Equal(0, rig.Hero.PosZ);
        }

        Assert.Equal(0, rig.Hero.IsZForceApplied);
        Assert.Equal(-1, rig.Hero.JumpStartTickStamp);
    }

    [Fact]
    public void SJ8b_TheCrossDuringAMessageBox_DoesNotJump_TheCrossAfterItDoes()
    {
        var rig = FreeRig();
        rig.Host.GameState.PlayerControlFlags = AlundraGameState.PlayerControlBits.MessageBox;
        rig.Step(Cross, Cross);
        Assert.Equal(Idle, rig.Hero.CurrentAnimationId);
        Assert.Equal(0, rig.Hero.PosZ);

        rig.Host.GameState.PlayerControlFlags = 0;
        rig.Step(Cross, 0);
        Assert.Equal(Idle, rig.Hero.CurrentAnimationId);

        rig.Step(Cross, Cross);
        Assert.Equal(JumpStanding, rig.Hero.CurrentAnimationId);
        Assert.Equal(JumpHeights[0], rig.Hero.PosZ);
    }

    [Fact]
    public void UH4a_TheCrossInTheAir_ChangesNothing()
    {
        var rig = FreeRig();
        for (var frame = 1; frame <= 20; frame++)
        {
            var pressed = frame is 1 or 5;
            rig.Step(pressed ? Cross : 0, pressed ? Cross : 0);
            Assert.Equal(JumpHeights[frame - 1], rig.Hero.PosZ);
            Assert.Equal(frame == 1 ? JumpStanding : AirStill, rig.Hero.CurrentAnimationId);
        }
    }

    [Fact]
    public void UH4b_TheCrossAtTheTickAfterTheLastAirTick_JumpsAgain_FromTheStateThatHasNotLandedYet()
    {
        var rig = FreeRig();
        for (var frame = 1; frame <= 21; frame++)
        {
            rig.Step(frame == 1 ? Cross : 0, frame == 1 ? Cross : 0);
        }

        // Tick 21: the foot is at the ground and the strict test has not landed yet.
        Assert.Equal(0, rig.Hero.PosZ);
        Assert.Equal(1, rig.Hero.IsOnGround);
        Assert.Equal(0, rig.Hero.CollidedWithEntityZ);

        // Tick 22: the edge again - the tail of this frame is the ground one, the take-off starts again from the state still held by the tick.
        rig.Step(Cross, Cross);
        Assert.Equal(JumpStanding, rig.Hero.CurrentAnimationId);
        Assert.Equal(327680, rig.Hero.ForceZ);
        Assert.Equal(327680, rig.Hero.PosZ);
        Assert.Equal(0, rig.Hero.CollidedWithEntityZ);
    }

    [Fact]
    public void UH4c_TheCrossHeldAfterTheLanding_DoesNotJumpAgain()
    {
        var rig = FreeRig();
        for (var frame = 1; frame <= 40; frame++)
        {
            rig.Step(Cross, frame == 1 ? Cross : 0);
        }

        Assert.Equal(Idle, rig.Hero.CurrentAnimationId);
        Assert.Equal(0, rig.Hero.PosZ);
        Assert.Equal(1, rig.Hero.IsOnGround);
        Assert.False(rig.Hero.HeroAirborne);
    }

    [Fact]
    public void UH5_OnACellThatForbidsTheJump_TheCrossDoesNothing_NotEvenTheMovingAnimation()
    {
        var rig = FreeRig(groundProperty: (_, _) => 0x40);
        Assert.NotEqual(0u, rig.Hero.CombinedVramFlagsOR & 0x4000u);
        for (var frame = 1; frame <= 4; frame++)
        {
            rig.Step(Cross, frame == 1 ? Cross : 0);
            Assert.Equal(Idle, rig.Hero.CurrentAnimationId);
            Assert.Equal(0, rig.Hero.PosZ);
            Assert.Equal(0, rig.Hero.ForceZ);
            Assert.Equal(1, rig.Hero.IsOnGround);
        }

        // With a direction held: no walking jump, and the hero does not even become Moving on the frame of the edge.
        var walking = FreeRig(groundProperty: (_, _) => 0x40);
        walking.Step(Right | Cross, Cross);
        Assert.Equal(Idle, walking.Hero.CurrentAnimationId);
        Assert.Equal(0, walking.Hero.ForceX);
        Assert.Equal(0, walking.Hero.PosZ);
        walking.Step(Right, 0); // the next frame, without the edge, the hero walks.
        Assert.Equal(Moving, walking.Hero.CurrentAnimationId);
    }

    [Theory]
    [InlineData(44u)]
    [InlineData(45u)]
    public void AfterAScriptedJump_AReleasedHeroOnTheGroundGoesBackToIdleOrMoving(uint animation)
    {
        var idle = FreeRig();
        idle.Hero.TargetAnimationId = animation;
        idle.Step();
        Assert.Equal(Idle, idle.Hero.CurrentAnimationId);

        var moving = FreeRig();
        moving.Hero.TargetAnimationId = animation;
        moving.Step(Right);
        Assert.Equal(Moving, moving.Hero.CurrentAnimationId);
        Assert.Equal(0x18u, moving.Hero.TargetDirection);
    }

    [Fact]
    public void AScriptedJumpReleasedInTheAir_ComesBackToTheGroundAnimation_OnceTheHeroTouchesTheGround()
    {
        var rig = FreeRig();
        rig.Host.GameState.PlayerControlFlags = AlundraGameState.PlayerControlBits.ControlLocked;
        rig.Hero.TargetAnimationId = 43;
        for (var frame = 1; frame <= 5; frame++)
        {
            rig.Step();
            Assert.Equal(JumpHeights[frame - 1], rig.Hero.PosZ);
        }

        // The script releases the hero in the air with a direction held: the air animation of the stick, then the landing.
        rig.Host.GameState.PlayerControlFlags = 0;
        rig.Step(Right);
        Assert.Equal(AirMoving, rig.Hero.CurrentAnimationId);
        var frames = 0;
        while (rig.Hero.IsOnGround == 0 && frames++ < 40)
        {
            rig.Step(Right);
            Assert.Equal(AirMoving, rig.Hero.CurrentAnimationId);
        }

        rig.Step(Right);
        Assert.Equal(1, rig.Hero.IsOnGround);
        Assert.Equal(Moving, rig.Hero.CurrentAnimationId);
        Assert.Equal(0, rig.Hero.PosZ);
    }
}
