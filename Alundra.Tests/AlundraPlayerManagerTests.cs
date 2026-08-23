using System.Collections.Generic;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="AlundraPlayerManager"/>'s ported subset of <c>PlayerManager.MovePlayer</c> and its
/// own kinematic tick - see that class' own doc for the exact ported/not-ported line ranges.
/// </summary>
public class AlundraPlayerManagerTests
{
    private static AlundraGameState NewUnlockedState() => new(); // PlayerControlFlags = 0 (New Game default)

    // -----------------------------------------------------------------------------------------
    // Direction table (StaticVariables.g_directionByButtons, PlayerManager.cs:199-205) - all 16
    // ButtonsHold>>0xc combinations.
    // -----------------------------------------------------------------------------------------

    public static IEnumerable<object[]> AllDirectionCombinations()
    {
        for (var i = 0; i < 16; i++)
        {
            yield return new object[] { (uint)i };
        }
    }

    [Theory]
    [MemberData(nameof(AllDirectionCombinations))]
    public void DirectionByButtons_AllSixteenCombinations_MatchStaticVariablesTable(uint buttonsHoldNibble)
    {
        // Expected values transcribed directly from
        // alundra-datas-analyser/AlundraTools/AlundraEngine/StaticVariables.cs:99-103 (g_directionByButtons).
        var expected = new[]
        {
            0xFFFFFFFFu, 0x10u, 0x18u, 0x14u, 0x0u, 0xFFFFFFFFu, 0x1Cu, 0xFFFFFFFFu,
            0x8u, 0xCu, 0xFFFFFFFFu, 0xFFFFFFFFu, 0x4u, 0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu,
        };

        Assert.Equal(expected[buttonsHoldNibble], AnimationTables.DirectionByButtons[buttonsHoldNibble]);
    }

    [Fact]
    public void MovePlayer_InvalidDirectionCombination_FallsBackToCurrentTargetDirection()
    {
        var player = new AlundraEntityScriptProxy { TargetAnimationId = 0, TargetDirection = 0x18 };
        // ButtonsHold nibble 0 (no direction bits, other bits irrelevant to the >>0xc read) - actually we
        // need an INVALID combination: nibble 5 = Up+Down (0x1000|0x4000 -> nibble 0b0101 = 5) -> 0xFFFFFFFF.
        var pad = new AlundraPadState { ButtonsHold = AlundraPadState.Up | AlundraPadState.Down };

        AlundraPlayerManager.MovePlayer(player, in pad, NewUnlockedState());

        // buttonsHold (nibble) != 0 here (Up+Down both held), so TargetAnimationId becomes Moving, but the
        // invalid combination means TargetDirection falls back to whatever it already was (0x18).
        Assert.Equal(0x18u, player.TargetDirection);
    }

    // -----------------------------------------------------------------------------------------
    // Idle/Moving selection (PlayerManager.cs:361-383, simplified per E2's own scope).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void MovePlayer_IdleNoPadHeld_StaysIdle()
    {
        var player = new AlundraEntityScriptProxy { TargetAnimationId = 0, TargetDirection = 0 };

        AlundraPlayerManager.MovePlayer(player, in NoInput, NewUnlockedState());

        Assert.Equal(0u, player.TargetAnimationId);
    }

    [Fact]
    public void MovePlayer_IdleWithRightHeld_BecomesMovingFacingRight()
    {
        var player = new AlundraEntityScriptProxy { TargetAnimationId = 0, TargetDirection = 0 };
        var pad = new AlundraPadState { ButtonsHold = AlundraPadState.Right };

        AlundraPlayerManager.MovePlayer(player, in pad, NewUnlockedState());

        Assert.Equal(1u, player.TargetAnimationId); // Moving
        Assert.Equal(0x18u, player.TargetDirection); // g_directionByButtons[Right>>0xc = 2] = 0x18
    }

    [Fact]
    public void MovePlayer_MovingPadReleased_BecomesIdle()
    {
        var player = new AlundraEntityScriptProxy { TargetAnimationId = 1, TargetDirection = 0x18 };

        AlundraPlayerManager.MovePlayer(player, in NoInput, NewUnlockedState());

        Assert.Equal(0u, player.TargetAnimationId);
        Assert.Equal(0x18u, player.TargetDirection); // direction held (dir falls back to current)
    }

    [Fact]
    public void MovePlayer_OtherAnimationId_LeftUnchanged_NotPortedCase()
    {
        var player = new AlundraEntityScriptProxy { TargetAnimationId = 0x2D /* Jump */, TargetDirection = 0 };
        var pad = new AlundraPadState { ButtonsHold = AlundraPadState.Right };

        AlundraPlayerManager.MovePlayer(player, in pad, NewUnlockedState());

        Assert.Equal(0x2Du, player.TargetAnimationId); // untouched - Jump is not one of the ported cases.
    }

    // -----------------------------------------------------------------------------------------
    // Locked / blocked branches.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void MovePlayer_InputBlocked_DoesNotChangeAnimationOrDirection()
    {
        var player = new AlundraEntityScriptProxy { TargetAnimationId = 0, TargetDirection = 0 };
        var pad = new AlundraPadState { ButtonsHold = AlundraPadState.Right };
        var state = new AlundraGameState { PlayerControlFlags = AlundraGameState.PlayerControlBits.ControlLocked };

        AlundraPlayerManager.MovePlayer(player, in pad, state);

        Assert.Equal(0u, player.TargetAnimationId);
        Assert.Equal(0u, player.TargetDirection);
    }

    [Fact]
    public void MovePlayer_BlockedByEntity_DoesNotChangeAnimationOrDirection()
    {
        var player = new AlundraEntityScriptProxy
        {
            TargetAnimationId = 0,
            TargetDirection = 0,
            BlockedByEntity = new CasaEngine.Framework.Scene.Entities.Entity(),
        };
        var pad = new AlundraPadState { ButtonsHold = AlundraPadState.Right };

        AlundraPlayerManager.MovePlayer(player, in pad, NewUnlockedState());

        Assert.Equal(0u, player.TargetAnimationId);
        Assert.Equal(0u, player.TargetDirection);
    }

    [Fact]
    public void MovePlayer_UnlockedFreeBranch_SetsGravityFlag()
    {
        var player = new AlundraEntityScriptProxy { TargetAnimationId = 0, TargetDirection = 0, Flags = 0 };

        AlundraPlayerManager.MovePlayer(player, in NoInput, NewUnlockedState());

        Assert.Equal(EntityFlags.Gravity, player.Flags & EntityFlags.Gravity);
    }

    // -----------------------------------------------------------------------------------------
    // LoadingMap(0x36) -> Idle bridge (PlayerManager.cs:914-922, documented V1 deviation).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void MovePlayer_LoadingMapNoPadHeld_BridgesToIdle()
    {
        var player = new AlundraEntityScriptProxy { TargetAnimationId = 0x36, TargetDirection = 0 };

        AlundraPlayerManager.MovePlayer(player, in NoInput, NewUnlockedState());

        Assert.Equal(0u, player.TargetAnimationId); // Idle
    }

    [Fact]
    public void MovePlayer_LoadingMapPadHeld_BridgesStraightToMoving_SameTick()
    {
        var player = new AlundraEntityScriptProxy { TargetAnimationId = 0x36, TargetDirection = 0 };
        var pad = new AlundraPadState { ButtonsHold = AlundraPadState.Up };

        AlundraPlayerManager.MovePlayer(player, in pad, NewUnlockedState());

        Assert.Equal(1u, player.TargetAnimationId); // Moving - the bridge and the Idle/Moving switch both
                                                      // apply within the SAME MovePlayer call.
        Assert.Equal(0x10u, player.TargetDirection);
    }

    [Fact]
    public void MovePlayer_LoadingMapWhileInputBlocked_StaysLoadingMap()
    {
        var player = new AlundraEntityScriptProxy { TargetAnimationId = 0x36, TargetDirection = 0 };
        var state = new AlundraGameState { PlayerControlFlags = AlundraGameState.PlayerControlBits.ControlLocked };

        AlundraPlayerManager.MovePlayer(player, in NoInput, state);

        Assert.Equal(0x36u, player.TargetAnimationId); // locked branch returns before the bridge ever runs.
    }

    // -----------------------------------------------------------------------------------------
    // Kinematic tick - hand-computed scenario (Speed 208, Acceleration 1, direction 0x18 (east)).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Tick_SpeedByAcceleration1DirectionEast_ProducesHandComputedForceProgression()
    {
        var player = new AlundraEntityScriptProxy
        {
            TargetAnimationId = 1, // Moving
            TargetDirection = 0x18, // east
            AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
            {
                [1] = new AnimSetEntry { Anim = 1, Speed = 208, Acceleration = 1 },
            },
        };

        // Tick 1: TargetForceX = g_offsetXList[0x18] * 208 = 768 * 208 = 159744;
        // ForceStepX = |159744-0| >> 1 = 79872; ForceX = IncrementForce(0, 159744, 79872) = 79872.
        AlundraPlayerManager.Tick(player, 1f / 50f);
        Assert.Equal(79872, player.ForceX);
        Assert.Equal(0, player.ForceY); // g_offsetYList[0x18] = 0 (due east has no Y component)
        Assert.Equal(79872, player.PosX);

        // Tick 2: Speed/Direction/Acceleration unchanged -> no recompute; ForceX = IncrementForce(79872,
        // 159744, 79872) = 159744.
        AlundraPlayerManager.Tick(player, 1f / 50f);
        Assert.Equal(159744, player.ForceX);
        Assert.Equal(79872 + 159744, player.PosX);

        // Tick 3: force already equals target -> steady state, ForceX unchanged at 159744.
        AlundraPlayerManager.Tick(player, 1f / 50f);
        Assert.Equal(159744, player.ForceX);
        Assert.Equal(79872 + 159744 + 159744, player.PosX);
    }

    [Fact]
    public void Tick_AccumulatesSubStepElapsedTime_RunsWholeTicksOnly()
    {
        var player = new AlundraEntityScriptProxy
        {
            TargetAnimationId = 1,
            TargetDirection = 0x18,
            AnimSetsByAnim = new Dictionary<int, AnimSetEntry> { [1] = new AnimSetEntry { Anim = 1, Speed = 208, Acceleration = 1 } },
        };

        // A small slice of elapsed time - not enough to run any 50 Hz (0.02s) step yet.
        AlundraPlayerManager.Tick(player, 0.005f);
        Assert.Equal(0, player.PosX);

        // Enough more arrives that the accumulator now clearly exceeds one 50 Hz step (0.005 + 0.02 =
        // 0.025s, comfortably past the 0.02s threshold regardless of float rounding) - runs exactly once.
        AlundraPlayerManager.Tick(player, 0.02f);
        Assert.Equal(79872, player.PosX);
    }

    [Fact]
    public void Tick_LongStall_CapsCatchUpAtFourTicksPerFrame()
    {
        var player = new AlundraEntityScriptProxy
        {
            TargetAnimationId = 1,
            TargetDirection = 0x18,
            AnimSetsByAnim = new Dictionary<int, AnimSetEntry> { [1] = new AnimSetEntry { Anim = 1, Speed = 208, Acceleration = 1 } },
        };

        // 1 full second (50 ticks worth) in one go - capped at 4 ticks for this single frame.
        AlundraPlayerManager.Tick(player, 1f);

        // Ticks: 79872, +159744=239616, +159744(steady)=399360... wait force reaches target after tick 2
        // and steady-states at 159744/tick from tick 2 onward: tick1=79872, tick2=+159744, tick3=+159744,
        // tick4=+159744.
        Assert.Equal(79872 + 159744 * 3, player.PosX);
    }

    [Fact]
    public void Tick_NoAnimSetForCurrentAnimation_TreatsSpeedAsZero_NoMovement()
    {
        var player = new AlundraEntityScriptProxy { TargetAnimationId = 0, TargetDirection = 0x18, AnimSetsByAnim = null };

        AlundraPlayerManager.Tick(player, 1f / 50f);

        Assert.Equal(0, player.PosX);
        Assert.Equal(0, player.ForceX);
    }

    private static readonly AlundraPadState NoInput = default;
}
