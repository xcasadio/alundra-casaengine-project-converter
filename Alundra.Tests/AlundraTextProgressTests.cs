using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="AlundraTextProgress"/> - the falcon/text-progress state ported from
/// <c>UpdateNumberOfFalcon</c> (0x8004e738) and <c>UpdatePlayerProgressState</c> (0x8004754c), per the
/// binary facts of docs/plan-e15-yarn.md §5.7 (T2, 2026-09-28), authoritative over the decompilation.
/// Each test cites the exact §5.7 fact it checks. Instances are built directly (never through
/// <see cref="AlundraGameState.Instance"/>), same pattern as <c>AlundraPlayerManagerTests</c>, so tests
/// never leak state into each other.
/// </summary>
public class AlundraTextProgressTests
{
    // -----------------------------------------------------------------------------------------
    // Initial values (§5.7: TextCategoryIndex and GameVariables are BSS, 0 at boot).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TextCategoryIndex_InitialValue_IsZero()
    {
        var gameState = new AlundraGameState();

        Assert.Equal(0, gameState.TextCategoryIndex);
    }

    [Fact]
    public void GameVariables_InitialValues_AreAllZero()
    {
        var gameState = new AlundraGameState();

        Assert.Equal(new[] { 0, 0, 0, 0 }, gameState.GameVariables);
    }

    // -----------------------------------------------------------------------------------------
    // Constant tables (§5.7: g_categoryThresholdTable @ 0x8009A834; category -> reward item, table
    // @ 0x8009A814 -> g_itemDropProperties names).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void CategoryThresholds_MatchTheBinaryTable()
    {
        Assert.Equal(new[] { 15, 20, 25, 30, 35, 40, 45, 50 }, AlundraTextProgress.CategoryThresholds);
    }

    [Fact]
    public void RewardItemByCategory_MatchesTheBinaryTable()
    {
        Assert.Equal(
            new[] { 0x53, 0x33, 0x53, 0x35, 0x53, 0x37, 0x53, 0x07 },
            AlundraTextProgress.RewardItemByCategory);
    }

    // -----------------------------------------------------------------------------------------
    // UpdateNumberOfFalcon (§5.7: identical to PlayerManager.cs:5187-5200).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UpdateNumberOfFalcon_SumsFromTwoPositives()
    {
        var gameState = new AlundraGameState();
        gameState.PlayerStats.Falcon = 5;
        gameState.PlayerStats.FalconTemp = 7;

        AlundraTextProgress.UpdateNumberOfFalcon(gameState);

        Assert.Equal(12, gameState.PlayerStats.Falcon);
    }

    [Fact]
    public void UpdateNumberOfFalcon_ResetsFalconTempToZero()
    {
        var gameState = new AlundraGameState();
        gameState.PlayerStats.Falcon = 5;
        gameState.PlayerStats.FalconTemp = 7;

        AlundraTextProgress.UpdateNumberOfFalcon(gameState);

        Assert.Equal(0, gameState.PlayerStats.FalconTemp);
    }

    [Fact]
    public void UpdateNumberOfFalcon_CapsAtHexThirtyTwo()
    {
        // 0x33 (=51) -> clamped to 0x32 (=50): "0x33 -> 0x32" (§5.7, plafond 0x32 si Falcon >= 0x33).
        var gameState = new AlundraGameState();
        gameState.PlayerStats.Falcon = 0x33;
        gameState.PlayerStats.FalconTemp = 0;

        AlundraTextProgress.UpdateNumberOfFalcon(gameState);

        Assert.Equal(0x32, gameState.PlayerStats.Falcon);
    }

    [Fact]
    public void UpdateNumberOfFalcon_ExactlyHexThirtyTwo_StaysUnclamped()
    {
        // "0x32 stays" (§5.7): the clamp is `> 0x32`, never touching exactly 0x32.
        var gameState = new AlundraGameState();
        gameState.PlayerStats.Falcon = 0x32;
        gameState.PlayerStats.FalconTemp = 0;

        AlundraTextProgress.UpdateNumberOfFalcon(gameState);

        Assert.Equal(0x32, gameState.PlayerStats.Falcon);
    }

    [Fact]
    public void UpdateNumberOfFalcon_WrapsOnSixteenBitOverflow()
    {
        // §5.7: "Falcon += FalconTemp sur 16 bits" - a short sum that overflows wraps like the
        // original's lhu/addu/sh, before the 0x32 clamp is even reached in the source order.
        var gameState = new AlundraGameState();
        gameState.PlayerStats.Falcon = short.MaxValue;
        gameState.PlayerStats.FalconTemp = 2;

        AlundraTextProgress.UpdateNumberOfFalcon(gameState);

        // short.MaxValue + 2 wraps to short.MinValue + 1, which the clamp (> 0x32) does not touch.
        Assert.Equal(unchecked((short)(short.MaxValue + 2)), gameState.PlayerStats.Falcon);
    }

    [Fact]
    public void UpdateNumberOfFalcon_Clears0x400BitOfFlags0x2d()
    {
        // §5.7: "GameFlags[0x2d] &= 0xfffffbff" unconditionally.
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2d] = 0xffffffff;

        AlundraTextProgress.UpdateNumberOfFalcon(gameState);

        Assert.Equal(0xfffffbffu, gameState.GameFlags[0x2d]);
    }

    // -----------------------------------------------------------------------------------------
    // UpdatePlayerProgressState - the eight category branches (§5.7: identical to
    // TextDecoder.cs:1097-1183 except the first test, which the binary makes on the sign bit).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UpdatePlayerProgressState_SignBitAlone_SelectsCategorySevenAndBit0x100()
    {
        // §5.7: "le premier test est le bit de signe de GameFlags[0x2c] (bgez @ 0x800475a8)" - the
        // decompilation's `GameFlags[0x2c] < 0` on a uint is always false and makes category 7
        // unreachable; §5.7 overrides it: the binary tests bit 31 directly.
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0x80000000;
        gameState.PlayerStats.Falcon = 0;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(7, gameState.TextCategoryIndex);
        Assert.Equal(0x100u, gameState.GameFlags[0x2d] & 0x1feu);
    }

    [Fact]
    public void UpdatePlayerProgressState_SignBitWithLowerBits_SignBitWins()
    {
        // §5.7: the sign-bit test runs first, so it wins over every lower category bit also set.
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0x80000000 | 0x40000000 | 0x2000000;
        gameState.PlayerStats.Falcon = 0;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(7, gameState.TextCategoryIndex);
    }

    [Fact]
    public void UpdatePlayerProgressState_Bit0x40000000_SelectsCategorySixAndBit0x80()
    {
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0x40000000;
        gameState.PlayerStats.Falcon = 0;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(6, gameState.TextCategoryIndex);
        Assert.Equal(0x80u, gameState.GameFlags[0x2d] & 0x1feu);
    }

    [Fact]
    public void UpdatePlayerProgressState_Bit0x20000000_SelectsCategoryFiveAndBit0x40()
    {
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0x20000000;
        gameState.PlayerStats.Falcon = 0;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(5, gameState.TextCategoryIndex);
        Assert.Equal(0x40u, gameState.GameFlags[0x2d] & 0x1feu);
    }

    [Fact]
    public void UpdatePlayerProgressState_Bit0x10000000_SelectsCategoryFourAndBit0x20()
    {
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0x10000000;
        gameState.PlayerStats.Falcon = 0;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(4, gameState.TextCategoryIndex);
        Assert.Equal(0x20u, gameState.GameFlags[0x2d] & 0x1feu);
    }

    [Fact]
    public void UpdatePlayerProgressState_Bit0x8000000_SelectsCategoryThreeAndBit0x10()
    {
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0x8000000;
        gameState.PlayerStats.Falcon = 0;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(3, gameState.TextCategoryIndex);
        Assert.Equal(0x10u, gameState.GameFlags[0x2d] & 0x1feu);
    }

    [Fact]
    public void UpdatePlayerProgressState_Bit0x4000000_SelectsCategoryTwoAndBit0x08()
    {
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0x4000000;
        gameState.PlayerStats.Falcon = 0;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(2, gameState.TextCategoryIndex);
        Assert.Equal(0x08u, gameState.GameFlags[0x2d] & 0x1feu);
    }

    [Fact]
    public void UpdatePlayerProgressState_Bit0x2000000_SelectsCategoryOneAndBit0x04()
    {
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0x2000000;
        gameState.PlayerStats.Falcon = 0;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(1, gameState.TextCategoryIndex);
        Assert.Equal(0x04u, gameState.GameFlags[0x2d] & 0x1feu);
    }

    [Fact]
    public void UpdatePlayerProgressState_NoBitSet_SelectsCategoryZeroAndBit0x02()
    {
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0;
        gameState.PlayerStats.Falcon = 0;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(0, gameState.TextCategoryIndex);
        Assert.Equal(0x02u, gameState.GameFlags[0x2d] & 0x1feu);
    }

    // -----------------------------------------------------------------------------------------
    // Mask (§5.7 / TextDecoder.cs:1104: GameFlags[0x2d] & 0xfffffe01) - bits outside 0x1FE survive,
    // the old category bit is cleared.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UpdatePlayerProgressState_MaskKeepsBitsOutside0x1FE()
    {
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0; // category 0, bit 0x2
        gameState.GameFlags[0x2d] = 0x400 | 0x1 | 0x200; // outside the 0x1fe mask
        gameState.PlayerStats.Falcon = 100; // above every threshold -> sets 0x800 too

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(0x400u, gameState.GameFlags[0x2d] & 0x400u);
        Assert.Equal(0x1u, gameState.GameFlags[0x2d] & 0x1u);
        Assert.Equal(0x200u, gameState.GameFlags[0x2d] & 0x200u);
    }

    [Fact]
    public void UpdatePlayerProgressState_ClearsThePreviousCategoryBit()
    {
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2d] = 0x100; // stale category-7 bit from a previous call
        gameState.GameFlags[0x2c] = 0; // now selects category 0 (bit 0x2)
        gameState.PlayerStats.Falcon = 0;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(0x02u, gameState.GameFlags[0x2d] & 0x1feu);
    }

    // -----------------------------------------------------------------------------------------
    // Threshold comparison (§5.7: strict, signed: falcon < threshold clears 0x800, else sets it).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UpdatePlayerProgressState_FalconEqualsThreshold_SetsBit0x800()
    {
        // Category 0's threshold is 15 (§5.7). Equal is NOT strictly below -> 0x800 set.
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0;
        gameState.PlayerStats.Falcon = 15;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(0x800u, gameState.GameFlags[0x2d] & 0x800u);
    }

    [Fact]
    public void UpdatePlayerProgressState_FalconOneBelowThreshold_Clears0x800()
    {
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0;
        gameState.GameFlags[0x2d] = 0x800; // pre-set, must be cleared
        gameState.PlayerStats.Falcon = 14;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(0u, gameState.GameFlags[0x2d] & 0x800u);
    }

    // -----------------------------------------------------------------------------------------
    // Process-lifetime state: nothing in PRODUCTION resets TextCategoryIndex or GameVariables, same
    // as the original's BSS (§5.7: "jamais remis à zéro ... hors de la sauvegarde") - see their own
    // field docs on AlundraGameState. AlundraGameState.ResetForTests IS a reset seam for both, but it
    // is a TEST-ONLY seam (it must leave Instance clean between tests, unlike any production code
    // path), so there is nothing to assert here about the binary's own behavior.
    // -----------------------------------------------------------------------------------------

    // -----------------------------------------------------------------------------------------
    // §5.7 branch order and bit checks beyond the single-bit cases above.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UpdatePlayerProgressState_Bit0x40000000WithLowerBit_HigherBitWins()
    {
        // §5.7: the branches are tested from the highest bit down, so 0x40000000 (category 6) must win
        // over 0x2000000 (category 1) also being set.
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0x40000000 | 0x2000000;
        gameState.PlayerStats.Falcon = 0;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(6, gameState.TextCategoryIndex);
        Assert.Equal(0x80u, gameState.GameFlags[0x2d] & 0x1feu);
    }

    [Fact]
    public void UpdatePlayerProgressState_AllLowerBitsPreSet_OnlyTheNewCategoryBitSurvives()
    {
        // §5.7: the mask keeps everything outside 0x1fe, but every bit INSIDE it (0x1fe) is replaced by
        // exactly the one new category bit - none of the other seven should survive even if pre-set.
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2d] = 0x1fe;
        gameState.GameFlags[0x2c] = 0x2000000; // category 1, bit 0x04
        gameState.PlayerStats.Falcon = 0;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(0x04u, gameState.GameFlags[0x2d] & 0x1feu);
    }

    [Fact]
    public void UpdatePlayerProgressState_NegativeFalcon_ComparesSignedAndClearsBit0x800()
    {
        // §5.7: the threshold comparison is signed - Falcon = -1 (short, sign-extended to int) must
        // compare below every positive threshold, not as a huge unsigned value that would set the bit.
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0; // category 0, threshold 15
        gameState.GameFlags[0x2d] = 0x800; // pre-set, must be cleared
        gameState.PlayerStats.Falcon = -1;

        AlundraTextProgress.UpdatePlayerProgressState(gameState);

        Assert.Equal(0u, gameState.GameFlags[0x2d] & 0x800u);
    }
}
