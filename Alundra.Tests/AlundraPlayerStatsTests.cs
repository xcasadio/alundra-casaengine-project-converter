using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E13 C0 acceptance (docs/plan-e13-hud.md, tranche "C0 - DLL : l'état joueur"): each bound of §1.5, on
/// both sides, transcribed from <c>PlayerManager.cs</c>'s own <c>SetPlayerHpMax</c>/<c>SetPlayerHp</c>/
/// <c>SetPlayerMpMax</c>/<c>SetPlayerMp</c>/<c>SetMoney</c> - see <see cref="AlundraPlayerManager"/>'s
/// own doc for the exact line ranges each setter ports. Uses fresh <c>new AlundraGameState()</c>
/// instances throughout (same convention as <see cref="AlundraPlayerManagerTests"/>), not the shared
/// <see cref="AlundraGameState.Instance"/> singleton, so no reset-for-tests seam is needed here.
/// </summary>
public class AlundraPlayerStatsTests
{
    // -----------------------------------------------------------------------------------------
    // HpMax - ceiling 50 (any value >= 51 becomes 50), floor 0 (PlayerManager.cs:1705-1723).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(-1, 0)]     // floor: negative clamps to 0
    [InlineData(0, 0)]      // floor boundary
    [InlineData(50, 50)]    // ceiling boundary: 0x32 passes through unclamped
    [InlineData(51, 50)]    // ceiling: 0x33 and above becomes 0x32
    [InlineData(9999, 50)]  // well above the ceiling
    public void SetPlayerHpMax_ClampsToZeroFifty(int hpMax, short expected)
    {
        var state = new AlundraGameState();

        var result = AlundraPlayerManager.SetPlayerHpMax(state, hpMax);

        Assert.Equal(expected, result);
        Assert.Equal(expected, state.PlayerStats.HpMax);
    }

    // -----------------------------------------------------------------------------------------
    // Hp - clamped to [0, HpMax] (PlayerManager.cs:1734-1761), HpMax being whatever it currently is,
    // not a fixed constant.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SetPlayerHp_NegativeAmount_ClampsToZero()
    {
        var state = new AlundraGameState();
        AlundraPlayerManager.SetPlayerHpMax(state, 20);

        var result = AlundraPlayerManager.SetPlayerHp(state, -1);

        Assert.Equal(0, result);
        Assert.Equal(0, state.PlayerStats.Hp);
    }

    [Fact]
    public void SetPlayerHp_AboveCurrentHpMax_ClampsToHpMax()
    {
        var state = new AlundraGameState();
        AlundraPlayerManager.SetPlayerHpMax(state, 20);

        var result = AlundraPlayerManager.SetPlayerHp(state, 21);

        Assert.Equal(20, result);
        Assert.Equal(20, state.PlayerStats.Hp);
    }

    [Fact]
    public void SetPlayerHp_WithinBounds_PassesThroughUnclamped()
    {
        var state = new AlundraGameState();
        AlundraPlayerManager.SetPlayerHpMax(state, 20);

        var result = AlundraPlayerManager.SetPlayerHp(state, 15);

        Assert.Equal(15, result);
        Assert.Equal(15, state.PlayerStats.Hp);
    }

    // -----------------------------------------------------------------------------------------
    // MpMax - ceiling 4 (any value >= 5 becomes 4), floor 0, clamped both sides (PlayerManager.cs:1791-1811).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(-1, 0)]  // floor: negative clamps to 0
    [InlineData(0, 0)]   // floor boundary
    [InlineData(4, 4)]   // ceiling boundary: passes through unclamped
    [InlineData(5, 4)]   // ceiling: 5 and above becomes 4
    [InlineData(999, 4)] // well above the ceiling
    public void SetPlayerMpMax_ClampsToZeroFour(short mpMax, short expected)
    {
        var state = new AlundraGameState();

        var result = AlundraPlayerManager.SetPlayerMpMax(state, mpMax);

        Assert.Equal(expected, result);
        Assert.Equal(expected, state.PlayerStats.MpMax);
    }

    // -----------------------------------------------------------------------------------------
    // Mp - clamped to [0, MpMax] (PlayerManager.cs:1813-1829), same shape as Hp/HpMax.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SetPlayerMp_NegativeAmount_ClampsToZero()
    {
        var state = new AlundraGameState();
        AlundraPlayerManager.SetPlayerMpMax(state, 3);

        var result = AlundraPlayerManager.SetPlayerMp(state, -1);

        Assert.Equal(0, result);
        Assert.Equal(0, state.PlayerStats.Mp);
    }

    [Fact]
    public void SetPlayerMp_AboveCurrentMpMax_ClampsToMpMax()
    {
        var state = new AlundraGameState();
        AlundraPlayerManager.SetPlayerMpMax(state, 3);

        var result = AlundraPlayerManager.SetPlayerMp(state, 4);

        Assert.Equal(3, result);
        Assert.Equal(3, state.PlayerStats.Mp);
    }

    [Fact]
    public void SetPlayerMp_WithinBounds_PassesThroughUnclamped()
    {
        var state = new AlundraGameState();
        AlundraPlayerManager.SetPlayerMpMax(state, 3);

        var result = AlundraPlayerManager.SetPlayerMp(state, 2);

        Assert.Equal(2, result);
        Assert.Equal(2, state.PlayerStats.Mp);
    }

    // -----------------------------------------------------------------------------------------
    // Money - clamped to [0, 9999] (PlayerManager.cs:1671-1690).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(-1, 0)]        // floor: negative clamps to 0
    [InlineData(0, 0)]         // floor boundary
    [InlineData(9999, 9999)]   // ceiling boundary: passes through unclamped
    [InlineData(10000, 9999)]  // ceiling: >= 10000 becomes 9999
    [InlineData(32000, 9999)]  // well above the ceiling, still within short range
    public void SetMoney_ClampsToZeroNineNineNineNine(int amount, short expected)
    {
        var state = new AlundraGameState();

        var result = AlundraPlayerManager.SetMoney(state, (short)amount);

        Assert.Equal(expected, result);
        Assert.Equal(expected, state.PlayerStats.Money);
    }

    // -----------------------------------------------------------------------------------------
    // §1.5 bis - one shared instance, not a save/current pair (GameInitializer.cs:444-445's own alias).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void PlayerStats_IsTheSameInstanceAcrossReads_NoSeparateSaveAndCurrentObject()
    {
        var state = new AlundraGameState();

        var firstRead = state.PlayerStats;
        AlundraPlayerManager.SetPlayerHp(state, 7);
        var secondRead = state.PlayerStats;

        // Same reference both times: there is no second "save" object that could drift from what a
        // setter just wrote to "the current" one - exactly the aliasing docs/plan-e13-hud.md §1.5 bis
        // measured in the original (g_playerStats == g_saveData.PlayerStats).
        Assert.Same(firstRead, secondRead);
        Assert.Equal(7, secondRead.Hp);
    }

    // -----------------------------------------------------------------------------------------
    // New Game initialization - 10 / 10 / 0 / 0 / 0 (GameInitializer.cs:372-376).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Construction_MatchesNewGameDefaults()
    {
        var state = new AlundraGameState();

        Assert.Equal(10, state.PlayerStats.Hp);
        Assert.Equal(10, state.PlayerStats.HpMax);
        Assert.Equal(0, state.PlayerStats.Mp);
        Assert.Equal(0, state.PlayerStats.MpMax);
        Assert.Equal(0, state.PlayerStats.Money);
    }

    [Fact]
    public void InitializeNewGameStats_ResetsAnAlreadyMutatedInstanceToNewGameDefaults()
    {
        var state = new AlundraGameState();
        AlundraPlayerManager.SetPlayerHpMax(state, 45);
        AlundraPlayerManager.SetPlayerHp(state, 38);
        AlundraPlayerManager.SetPlayerMpMax(state, 3);
        AlundraPlayerManager.SetPlayerMp(state, 2);
        AlundraPlayerManager.SetMoney(state, 2163);

        AlundraPlayerManager.InitializeNewGameStats(state);

        Assert.Equal(10, state.PlayerStats.Hp);
        Assert.Equal(10, state.PlayerStats.HpMax);
        Assert.Equal(0, state.PlayerStats.Mp);
        Assert.Equal(0, state.PlayerStats.MpMax);
        Assert.Equal(0, state.PlayerStats.Money);
    }

    // -----------------------------------------------------------------------------------------
    // Debug value set - 38/45 HP, 2/3 MP, 2163 money (GameInitializer.cs:378-392's own "unused, only
    // for debugging" branch).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void LoadDebugStats_SetsTheGameInitializerDebugValueSet()
    {
        var state = new AlundraGameState();

        AlundraPlayerManager.LoadDebugStats(state);

        Assert.Equal(38, state.PlayerStats.Hp);
        Assert.Equal(45, state.PlayerStats.HpMax);
        Assert.Equal(2, state.PlayerStats.Mp);
        Assert.Equal(3, state.PlayerStats.MpMax);
        Assert.Equal(2163, state.PlayerStats.Money);
    }
}
