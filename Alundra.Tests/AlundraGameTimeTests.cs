#nullable enable
using System;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E16.c T1 (docs/plan-e16-etat-partie.md, C1/C8, D-E16-23): the game-time counter
/// (<see cref="AlundraGameState.GameTime"/>, port of <c>g_gameplayTime</c>) and the death-retry counter
/// (<see cref="AlundraGameState.DeathRetryCount"/>, the original's <c>SaveSlotIndex</c>, D-E16-22). The
/// counter counts sixtieths of a second on elapsed real time, capped at <c>0x14996C4</c>; the cap is applied
/// in <see cref="double"/> before the <see cref="uint"/> conversion (SC11). The last test drives the
/// production call site, the head of <see cref="AlundraWorldProxy.Update"/>, through
/// <see cref="AlundraGameState.Instance"/> - reset around every test, like every session-singleton test class.
/// </summary>
public sealed class AlundraGameTimeTests : IDisposable
{
    public AlundraGameTimeTests()
    {
        ResetSingletons();
    }

    public void Dispose()
    {
        ResetSingletons();
    }

    private static void ResetSingletons()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    [Fact]
    public void GameTimeMax_IsTheOriginalsCap_9959_59_InSixtieths()
    {
        Assert.Equal(0x14996C4u, AlundraGameState.GameTimeMax);
        Assert.Equal(((99u * 60u + 59u) * 60u + 59u) * 60u, AlundraGameState.GameTimeMax); // 99:59:59, zero sixtieths.
        Assert.Equal(60, AlundraGameState.GameTimeUnitsPerSecond);
    }

    [Fact]
    public void AdvanceGameTime_OneSecondInOneStep_Adds60()
    {
        var state = new AlundraGameState();

        state.AdvanceGameTime(1f);

        Assert.Equal(60u, state.GameTime);
    }

    [Fact]
    public void AdvanceGameTime_OneSecondInSixtyStepsOfOneSixtieth_Adds60()
    {
        var state = new AlundraGameState();

        for (var i = 0; i < 60; i++)
        {
            state.AdvanceGameTime(1f / 60f);
        }

        Assert.Equal(60u, state.GameTime);
    }

    [Fact]
    public void AdvanceGameTime_120StepsOfOneHundredTwentieth_Add60()
    {
        var state = new AlundraGameState();

        for (var i = 0; i < 120; i++)
        {
            state.AdvanceGameTime(1f / 120f);
        }

        Assert.Equal(60u, state.GameTime);
    }

    [Fact]
    public void AdvanceGameTime_StopsAtTheCap()
    {
        var state = new AlundraGameState { GameTime = AlundraGameState.GameTimeMax - 1 };

        state.AdvanceGameTime(1f); // +60 units, only 1 left below the cap.
        Assert.Equal(AlundraGameState.GameTimeMax, state.GameTime);

        state.AdvanceGameTime(1f); // already at the cap: stays there.
        Assert.Equal(AlundraGameState.GameTimeMax, state.GameTime);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(-float.Epsilon)]
    [InlineData(float.MinValue)]
    public void AdvanceGameTime_NonFiniteZeroOrNegative_AddsNothing_AndLeavesTheFractionUsable(float elapsedSeconds)
    {
        var state = new AlundraGameState { GameTime = 100 };

        state.AdvanceGameTime(elapsedSeconds);
        Assert.Equal(100u, state.GameTime);

        // The fraction was not poisoned (a NaN folded into it would stop the counter forever).
        state.AdvanceGameTime(1f);
        Assert.Equal(160u, state.GameTime);
    }

    [Theory]
    [InlineData(71582792f, 0u)] // 4 294 967 520 units = 2^32 + 224: a uint conversion before the cap would wrap to 224.
    [InlineData(1e12f, 0u)]
    [InlineData(float.MaxValue, 0u)]
    [InlineData(1e12f, 12345u)]
    [InlineData(float.MaxValue, AlundraGameState.GameTimeMax - 1)]
    public void AdvanceGameTime_HugeFiniteDuration_ReachesTheCapWithoutWrapping(float elapsedSeconds, uint start)
    {
        var state = new AlundraGameState { GameTime = start };

        state.AdvanceGameTime(elapsedSeconds);
        Assert.Equal(AlundraGameState.GameTimeMax, state.GameTime);

        // Still capped, and still well-behaved, afterwards.
        state.AdvanceGameTime(1f);
        Assert.Equal(AlundraGameState.GameTimeMax, state.GameTime);
    }

    [Fact]
    public void InstallForMapEntry_KeepsGameTimeAndDeathRetryCount()
    {
        var state = AlundraGameState.Instance;
        state.GameTime = 123456;
        state.DeathRetryCount = 7;

        state.InstallForMapEntry();

        Assert.Equal(123456u, state.GameTime);
        Assert.Equal((byte)7, state.DeathRetryCount);
    }

    [Fact]
    public void InstallForMapEntry_KeepsTheUnsavedFraction()
    {
        var state = AlundraGameState.Instance;
        state.AdvanceGameTime(0.5f / 60f); // half a unit, still pending.

        state.InstallForMapEntry();
        state.AdvanceGameTime(0.5f / 60f); // the other half.

        Assert.Equal(1u, state.GameTime);
    }

    [Fact]
    public void ResetForTests_ZeroesGameTimeDeathRetryCountAndTheFraction()
    {
        var state = AlundraGameState.Instance;
        state.GameTime = 123456;
        state.DeathRetryCount = 7;
        state.AdvanceGameTime(0.75f / 60f); // three quarters of a unit, still pending.

        state.ResetForTests();

        Assert.Equal(0u, state.GameTime);
        Assert.Equal((byte)0, state.DeathRetryCount);

        // Had the fraction survived, 0.75 + 0.5 would pass one whole unit.
        state.AdvanceGameTime(0.5f / 60f);
        Assert.Equal(0u, state.GameTime);
    }

    [Fact]
    public void NewState_StartsAtZero()
    {
        var state = new AlundraGameState();

        Assert.Equal(0u, state.GameTime);
        Assert.Equal((byte)0, state.DeathRetryCount);
    }

    /// <summary>The production call site: the head of <see cref="AlundraWorldProxy.Update"/>. Same headless
    /// montage as <see cref="AlundraGameplayFreezeTests"/>' world-level test ("TestWorld" has no tileMap
    /// entity, so <see cref="AlundraWorldProxy.InitializeWithWorld"/> returns before the real install). The
    /// frame durations are exact binary fractions, so four quarter-second frames are exactly one second.</summary>
    [Fact]
    public void WorldProxyUpdate_AdvancesTheSessionGameTime()
    {
        var world = new CasaEngine.Framework.Scene.World.World { Name = "TestWorld" };
        var worldProxy = new AlundraWorldProxy();
        worldProxy.InitializeWithWorld(world);
        Assert.Equal(0u, worldProxy.GameState.GameTime);

        for (var i = 0; i < 4; i++)
        {
            worldProxy.Update(0.25f);
        }

        Assert.Same(AlundraGameState.Instance, worldProxy.GameState);
        Assert.Equal(60u, AlundraGameState.Instance.GameTime);
    }
}
