using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// T4 (docs/plan-e12-dialogues.md, slice E12.a): <see cref="AlundraDialogueTextParser.SplitIntoPages"/>
/// splits on <c>\A</c>, resolves <c>\N</c> to a real newline, and strips+counts every unhandled control
/// code exactly once per code letter (logged) while counting every occurrence.
/// </summary>
public class AlundraDialogueTextParserTests
{
    [Fact]
    public void SplitIntoPages_SplitsOnBackslashA_AndResolvesBackslashN_ToNewline()
    {
        var pages = AlundraDialogueTextParser.SplitIntoPages("Line one\\NLine two\\APage two");

        Assert.Equal(2, pages.Count);
        Assert.Equal("Line one\nLine two", pages[0].DisplayText);
        Assert.Equal("Page two", pages[1].DisplayText);
    }

    [Fact]
    public void SplitIntoPages_MutationGuard_NotSplittingOnBackslashA_WouldFailThisAssertion()
    {
        // Mutation target (plan §4, T4): "ne pas découper sur \A -> T4 tombe" - asserting exactly 1 page
        // here would still pass under that mutation, so assert the SPLIT COUNT itself, which only a
        // correct \A split produces.
        var pages = AlundraDialogueTextParser.SplitIntoPages("A\\AB\\AC");
        Assert.Equal(3, pages.Count);
        Assert.Equal("A", pages[0].DisplayText);
        Assert.Equal("B", pages[1].DisplayText);
        Assert.Equal("C", pages[2].DisplayText);
    }

    [Fact]
    public void SplitIntoPages_CollectsNumericCodes_RawNotOred()
    {
        var pages = AlundraDialogueTextParser.SplitIntoPages("Hello\\999 world");

        Assert.Single(pages);
        Assert.Equal(new[] { 999 }, pages[0].NumericCodes);
        // The numeric code contributes nothing to the DISPLAYED text.
        Assert.DoesNotContain("999", pages[0].DisplayText);
        Assert.Equal("Hello world", pages[0].DisplayText);
    }

    [Fact]
    public void SplitIntoPages_MultiDigitNumericCode_ParsedAsOneCode()
    {
        var pages = AlundraDialogueTextParser.SplitIntoPages("\\13end");

        Assert.Single(pages);
        Assert.Equal(new[] { 13 }, pages[0].NumericCodes);
        Assert.Equal("end", pages[0].DisplayText);
    }

    [Fact]
    public void SplitIntoPages_UnknownCode_StrippedFromTextAndCounted()
    {
        AlundraDialogueTextParser.ResetCountersForTests();

        var pages = AlundraDialogueTextParser.SplitIntoPages("Before\\YAfter\\Yagain");

        Assert.Single(pages);
        Assert.Equal("BeforeAfteragain", pages[0].DisplayText);
        Assert.Equal(2, AlundraDialogueTextParser.UnknownCodeCounts['Y']);
    }

    [Fact]
    public void SplitIntoPages_RealMap389String_MatchesKnownShape()
    {
        // Real map 389 strings.json index 1 (docs/plan-e12-dialogues.md §1.4/T1): "\CQu'est-ce que tu
        // veux, petit ? As-tu\Nencore oublié o}i se trouve\Nta cabine ?\999\Y" - \C and \Y stripped, \N
        // becomes newlines, \999 collected as a numeric code, one single page (no \A anywhere).
        AlundraDialogueTextParser.ResetCountersForTests();

        const string raw = "\\CQu'est-ce que tu veux, petit ? As-tu\\Nencore oublié o}i se trouve\\Nta cabine ?\\999\\Y";
        var pages = AlundraDialogueTextParser.SplitIntoPages(raw);

        Assert.Single(pages);
        Assert.Equal(new[] { 999 }, pages[0].NumericCodes);
        Assert.Equal(
            "Qu'est-ce que tu veux, petit ? As-tu\nencore oublié o}i se trouve\nta cabine ?",
            pages[0].DisplayText);
    }
}
