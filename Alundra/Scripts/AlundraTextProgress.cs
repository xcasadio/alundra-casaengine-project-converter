#nullable enable

namespace Alundra.Scripts;

/// <summary>
/// E15.c T3 (docs/plan-e15-yarn.md §5.7, T2 - binary facts, authoritative over the decompilation):
/// port of the falcon/text-progress state the original's <c>\X0</c>-<c>\X5</c> text codes read and
/// write - <c>UpdateNumberOfFalcon</c> (0x8004e738) and <c>UpdatePlayerProgressState</c> (0x8004754c).
/// Both are called only from the four <c>\X</c> sites of the text decoder (§5.7), which E15.c's
/// <c>falcon_update</c> Yarn command drives (docs/plan-e15-yarn.md §1, ADR-0007) - this class holds
/// just the two ported procedures and the constant tables they use, operating on
/// <see cref="AlundraGameState"/> fields (<see cref="AlundraGameState.TextCategoryIndex"/>,
/// <see cref="AlundraGameState.GameFlags"/>, <see cref="AlundraPlayerStats.Falcon"/>/
/// <see cref="AlundraPlayerStats.FalconTemp"/>).
/// </summary>
public static class AlundraTextProgress
{
    /// <summary>
    /// <c>g_categoryThresholdTable</c> @ 0x8009A834 (docs/plan-e15-yarn.md §5.7): initialized data,
    /// never written, read by <c>\X3</c>, <c>\X5</c> and <see cref="UpdatePlayerProgressState"/>.
    /// Index by <see cref="AlundraGameState.TextCategoryIndex"/>.
    /// </summary>
    public static readonly int[] CategoryThresholds = { 15, 20, 25, 30, 35, 40, 45, 50 };

    /// <summary>
    /// Category -&gt; reward item, ETC entry <c>0x200 + RewardItemByCategory[category]</c>
    /// (docs/plan-e15-yarn.md §5.7): the 8-pointer table @ 0x8009A814, read only at 0x8004674c, into
    /// <c>g_itemDropProperties</c> (0x800C5F7C) records whose name field is filled at boot from
    /// ETC + IndexTable[0x200 + i]. This is the section-5.7 fact that corrects the decompilation's
    /// <c>TextDecoder.cs:536</c> <c>GetItemName(category)</c>, which wrongly takes the category for an
    /// item id.
    /// </summary>
    public static readonly int[] RewardItemByCategory = { 0x53, 0x33, 0x53, 0x35, 0x53, 0x37, 0x53, 0x07 };

    /// <summary>Bit 0x2d flag set when the category threshold is reached/passed (docs/plan-e15-yarn.md §5.7).</summary>
    public const uint ThresholdReachedBit = 0x800;

    /// <summary>Bit 0x2d flag <see cref="UpdateNumberOfFalcon"/> always clears.</summary>
    public const uint FalconUpdatedClearBit = 0x400;

    /// <summary>
    /// Port of <c>PlayerManager.UpdateNumberOfFalcon</c> @ 0x8004e738 (identical to
    /// <c>Gameplay/PlayerManager.cs:5187-5200</c>, docs/plan-e15-yarn.md §5.7): folds the temporary
    /// falcon count into the real one with 16-bit wraparound (the original's <c>lhu</c>/<c>addu</c>/
    /// <c>sh</c> on a <c>short</c> field - C#'s <c>short += short</c> already wraps the same way),
    /// resets the temporary count, clamps the total at 0x32 (signed), then clears
    /// <see cref="FalconUpdatedClearBit"/> of <c>GameFlags[0x2d]</c>.
    /// </summary>
    public static void UpdateNumberOfFalcon(AlundraGameState gameState)
    {
        var stats = gameState.PlayerStats;
        stats.Falcon = unchecked((short)(stats.Falcon + stats.FalconTemp));
        stats.FalconTemp = 0;

        if (stats.Falcon > 0x32)
        {
            stats.Falcon = 0x32;
        }

        gameState.GameFlags[0x2d] &= ~FalconUpdatedClearBit;
    }

    /// <summary>
    /// Port of <c>UpdatePlayerProgressState</c> @ 0x8004754c (docs/plan-e15-yarn.md §5.7): identical to
    /// <c>Text/TextDecoder.cs:1097-1183</c> EXCEPT the first test, which the binary makes on the SIGN
    /// BIT (bit 31) of <c>GameFlags[0x2c]</c> (<c>bgez</c> @ 0x800475a8) - the decompilation instead
    /// writes <c>GameFlags[0x2c] &lt; 0</c> on a <c>uint</c>, which is always false and makes category
    /// 7 unreachable; §5.7 is authoritative and this port tests bit 31 directly.
    ///
    /// Masks <c>GameFlags[0x2d]</c> to <c>0xfffffe01</c> (keeping every bit outside 0x1FE), then ORs in
    /// exactly one of the eight category bits (0x100 down to 0x2) and sets
    /// <see cref="AlundraGameState.TextCategoryIndex"/> to the matching index (7 down to 0), by testing
    /// <c>GameFlags[0x2c]</c>'s bits from the sign bit down to 0x2000000. Finally compares
    /// <see cref="AlundraPlayerStats.Falcon"/> (already updated by <see cref="UpdateNumberOfFalcon"/>)
    /// against <see cref="CategoryThresholds"/>[<see cref="AlundraGameState.TextCategoryIndex"/>] -
    /// strictly, signed: below it clears <see cref="ThresholdReachedBit"/>, at or above it sets it.
    /// </summary>
    public static void UpdatePlayerProgressState(AlundraGameState gameState)
    {
        var gameFlags = gameState.GameFlags;
        var progressFlags = gameFlags[0x2d] & 0xfffffe01;
        var testedFlags = gameFlags[0x2c];

        if ((testedFlags & 0x80000000U) != 0)
        {
            gameFlags[0x2d] = progressFlags | 0x100;
            gameState.TextCategoryIndex = 7;
        }
        else if ((testedFlags & 0x40000000U) != 0)
        {
            gameFlags[0x2d] = progressFlags | 0x80;
            gameState.TextCategoryIndex = 6;
        }
        else if ((testedFlags & 0x20000000U) != 0)
        {
            gameFlags[0x2d] = progressFlags | 0x40;
            gameState.TextCategoryIndex = 5;
        }
        else if ((testedFlags & 0x10000000U) != 0)
        {
            gameFlags[0x2d] = progressFlags | 0x20;
            gameState.TextCategoryIndex = 4;
        }
        else if ((testedFlags & 0x8000000U) != 0)
        {
            gameFlags[0x2d] = progressFlags | 0x10;
            gameState.TextCategoryIndex = 3;
        }
        else if ((testedFlags & 0x4000000U) != 0)
        {
            gameFlags[0x2d] = progressFlags | 8;
            gameState.TextCategoryIndex = 2;
        }
        else if ((testedFlags & 0x2000000U) != 0)
        {
            gameFlags[0x2d] = progressFlags | 4;
            gameState.TextCategoryIndex = 1;
        }
        else
        {
            gameFlags[0x2d] = progressFlags | 2;
            gameState.TextCategoryIndex = 0;
        }

        var threshold = CategoryThresholds[gameState.TextCategoryIndex];
        var falcon = gameState.PlayerStats.Falcon;

        if (falcon < threshold)
        {
            gameFlags[0x2d] &= ~ThresholdReachedBit;
        }
        else
        {
            gameFlags[0x2d] |= ThresholdReachedBit;
        }
    }
}
