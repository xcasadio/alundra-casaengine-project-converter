#nullable enable
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E16.c T2 (docs/plan-e16-etat-partie.md, C7, F6 to F8): the chapter walk
/// (<see cref="AlundraChapterFlags.GetFirstEnabledFlagIndex"/>, port of 0x800813B0) and the slot summary
/// (<see cref="AlundraSaveGame.BuildSummary"/>, port of <c>UpdateMenuStatusText</c>'s text). The expected
/// summary is computed here, independently, from F6's formulas.
/// </summary>
public sealed class AlundraChapterFlagsTests
{
    /// <summary>The 41 ids read in <c>g_flagIdList</c> (F8), copied here so an edit of the production table
    /// is caught.</summary>
    private static readonly ushort[] ExpectedFlagIds =
    [
        0x0003, 0x0008, 0x006C, 0x0676, 0x006D, 0x00EF, 0x0127, 0x00F7,
        0x00E9, 0x00E8, 0x015C, 0x012B, 0x028B, 0x014B, 0x0578, 0x0101,
        0x0579, 0x057A, 0x0372, 0x057B, 0x057C, 0x057D, 0x0385, 0x057E,
        0x0510, 0x0023, 0x001F, 0x0046, 0x0048, 0x004F, 0x037A, 0x01D3,
        0x0233, 0x06A2, 0x037B, 0x00E7, 0x044F, 0x03D0, 0x0664, 0x049F,
        0x04A0,
    ];

    private static void Set(AlundraGameState state, ushort flagId) => state.AddFlag(flagId, 1u << (flagId & 0x1F));

    [Fact]
    public void FlagIds_AreThe41IdsOfTheBinary_AllInsideThe64SavedWords()
    {
        Assert.Equal(ExpectedFlagIds, AlundraChapterFlags.FlagIds.ToArray());
        Assert.Equal(41, AlundraChapterFlags.LastChapterIndex);
        Assert.All(AlundraChapterFlags.FlagIds, id => Assert.True(id >> 5 < 64));
    }

    [Fact]
    public void NoFlagSet_Returns0()
    {
        var state = new AlundraGameState();

        Assert.Equal(0, AlundraChapterFlags.GetFirstEnabledFlagIndex(state.GameFlags));
    }

    [Fact]
    public void FirstFlagOnly_Returns1()
    {
        var state = new AlundraGameState();
        Set(state, ExpectedFlagIds[0]);

        Assert.Equal(1, AlundraChapterFlags.GetFirstEnabledFlagIndex(state.GameFlags));
    }

    [Fact]
    public void AllFlagsSet_Returns41()
    {
        var state = new AlundraGameState();
        foreach (var id in ExpectedFlagIds)
        {
            Set(state, id);
        }

        Assert.Equal(41, AlundraChapterFlags.GetFirstEnabledFlagIndex(state.GameFlags));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(33)] // 0x06A2, the highest id (word 53).
    [InlineData(40)]
    public void HoleInTheMiddle_ReturnsTheHolesIndex(int hole)
    {
        var state = new AlundraGameState();
        for (var i = 0; i < ExpectedFlagIds.Length; i++)
        {
            if (i != hole)
            {
                Set(state, ExpectedFlagIds[i]);
            }
        }

        Assert.Equal(hole, AlundraChapterFlags.GetFirstEnabledFlagIndex(state.GameFlags));
    }

    [Fact]
    public void ChapterIdSetInTemporaryFlags_IsNotSeen()
    {
        var state = new AlundraGameState();
        var id = ExpectedFlagIds[0];
        state.AddFlag(0x8000u | id, 1u << (id & 0x1F)); // the session-only bank, same word and bit.
        Assert.NotEqual(0u, state.TemporaryFlags[id >> 5]);

        Assert.Equal(0, AlundraChapterFlags.GetFirstEnabledFlagIndex(state.GameFlags));
    }

    [Fact]
    public void The64SavedWords_GiveTheSameChapterAsThe1024SessionWords()
    {
        var state = new AlundraGameState();
        for (var i = 0; i < 25; i++)
        {
            Set(state, ExpectedFlagIds[i]);
        }

        var saved = state.GameFlags.Take(64).ToArray();

        Assert.Equal(25, AlundraChapterFlags.GetFirstEnabledFlagIndex(state.GameFlags));
        Assert.Equal(25, AlundraChapterFlags.GetFirstEnabledFlagIndex(saved));
    }

    [Fact]
    public void ShorterArray_ReadsMissingWordsAsClear_WithoutThrowing()
    {
        Assert.Equal(0, AlundraChapterFlags.GetFirstEnabledFlagIndex(new uint[0]));
    }

    [Theory]
    [InlineData(0, "0000")]
    [InlineData(1, "0001")]
    [InlineData(41, "0041")]
    public void FormatChapter_WritesFourDigits(int index, string expected)
    {
        Assert.Equal(expected, AlundraChapterFlags.FormatChapter(index));
    }

    // ---- Summary text (F6) ----------------------------------------------------------------------

    /// <summary>F6, written independently of the production code: the 30-character template of
    /// 0x80022C38, HP in 5-6, hours <c>t / 216000 % 100</c> in 19-20, minutes
    /// <c>t / 3600 - 60 * (t / 216000)</c> in 22-23, seconds <c>t / 60 - 60 * (t / 3600)</c> in 25-26.</summary>
    private static string ExpectedSummary(int hp, uint t)
    {
        var hours = t / 216000 % 100;
        var minutes = t / 3600 - 60 * (t / 216000);
        var seconds = t / 60 - 60 * (t / 3600);

        return "  HP " + (char)('0' + hp / 10) + (char)('0' + hp % 10)
            + "       TIME "
            + (char)('0' + hours / 10) + (char)('0' + hours % 10) + ":"
            + (char)('0' + minutes / 10) + (char)('0' + minutes % 10) + ":"
            + (char)('0' + seconds / 10) + (char)('0' + seconds % 10)
            + "   ";
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(59u)]
    [InlineData(60u)]
    [InlineData(3599u)]
    [InlineData(3600u)]
    [InlineData(215999u)]
    [InlineData(216000u)]
    [InlineData(AlundraGameState.GameTimeMax)]
    public void BuildSummary_Time_MatchesF6(uint gameTime)
    {
        var summary = AlundraSaveGame.BuildSummary(10, gameTime);

        Assert.Equal(30, summary.Length);
        Assert.Equal(ExpectedSummary(10, gameTime), summary);
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)10)]
    [InlineData((short)50)]
    public void BuildSummary_Hp_MatchesF6(short hpMax)
    {
        var summary = AlundraSaveGame.BuildSummary(hpMax, 0);

        Assert.Equal(ExpectedSummary(hpMax, 0), summary);
    }

    [Theory]
    [InlineData(0u, "  HP 10       TIME 00:00:00   ")]
    [InlineData(59u, "  HP 10       TIME 00:00:00   ")]
    [InlineData(60u, "  HP 10       TIME 00:00:01   ")]
    [InlineData(3599u, "  HP 10       TIME 00:00:59   ")]
    [InlineData(3600u, "  HP 10       TIME 00:01:00   ")]
    [InlineData(215999u, "  HP 10       TIME 00:59:59   ")]
    [InlineData(216000u, "  HP 10       TIME 01:00:00   ")]
    [InlineData(AlundraGameState.GameTimeMax, "  HP 10       TIME 99:59:59   ")]
    public void BuildSummary_Time_ReadsAsSixtiethsOfASecond(uint gameTime, string expected)
    {
        Assert.Equal(expected, AlundraSaveGame.BuildSummary(10, gameTime));
    }

    [Theory]
    [InlineData((short)0, "  HP 00       TIME 00:00:00   ")]
    [InlineData((short)50, "  HP 50       TIME 00:00:00   ")]
    public void BuildSummary_Hp_Literal(short hpMax, string expected)
    {
        Assert.Equal(expected, AlundraSaveGame.BuildSummary(hpMax, 0));
    }

    [Theory]
    [InlineData(short.MinValue)]
    [InlineData((short)-1)]
    [InlineData((short)100)]
    [InlineData(short.MaxValue)]
    public void BuildSummary_HpOutsideTwoDigits_DoesNotThrow_AndStaysWellFormed(short hpMax)
    {
        var summary = AlundraSaveGame.BuildSummary(hpMax, uint.MaxValue);

        Assert.Equal(30, summary.Length);
        Assert.DoesNotContain(summary, char.IsSurrogate);
    }
}
