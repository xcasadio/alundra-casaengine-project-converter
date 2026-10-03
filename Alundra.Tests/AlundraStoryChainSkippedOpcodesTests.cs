#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using static Alundra.Tests.AlundraStoryChainOpcodeAudit;

namespace Alundra.Tests;

/// <summary>
/// E19.e E1 (docs/plan-e19-opcodes.md §1.2i): the closed list of the opcodes the interpreter skips by their size on the 30 maps of
/// the story chain. The corpus tests walk the real export and apply one rule each (so a mutation turns exactly one red); the
/// rest are permanent tests of the walk, the oracle and the rule functions on made-up input. Rule 5 is wired by the arcs of
/// E19.e (A13, A14, A15, A17, A18) in their own tests; here its function is tested on a made-up trace.
/// A missing export FAILS the corpus tests naming it, never skips them.
/// </summary>
public class AlundraStoryChainSkippedOpcodesTests
{
    private static readonly Lazy<IReadOnlyList<ReachedSite>> Corpus = new(() =>
    {
        var root = SaveGameDirectorTestSupport.FindProjectRoot();
        return ChainMaps.SelectMany(map => WalkExportedMap(root, map)).ToList();
    });

    private static IReadOnlyList<ListedSite> TheList() => ParseList(File.ReadAllLines(FindListPath()));

    private static IReadOnlyList<ReachedSite> CorpusSkipped() => Corpus.Value.Where(r => !IsPorted(r.Opcode)).ToList();

    private static string Show(IEnumerable<ReachedSite> sites) =>
        string.Join("; ", sites.Select(s => $"{Describe(s.Site)} 0x{s.Opcode:X2}"));

    private static string Show(IEnumerable<ListedSite> sites) =>
        string.Join("; ", sites.Select(s => $"{Describe(s.Site)} 0x{s.Opcode:X2} ({s.Tier})"));

    // -----------------------------------------------------------------------------------------
    // The corpus: one test per rule
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Rule1_EverySiteReachedAndSkippedIsAListedLine()
    {
        var unlisted = Rule1UnlistedSites(CorpusSkipped(), TheList());

        Assert.True(unlisted.Count == 0, $"{unlisted.Count} skipped site(s) reached on the chain are not in the closed list: {Show(unlisted)}");
    }

    [Fact]
    public void Rule2_EveryListedLineIsASiteReachedAndSkipped()
    {
        var stale = Rule2StaleLines(TheList(), CorpusSkipped());

        Assert.True(stale.Count == 0, $"{stale.Count} line(s) of the closed list are stale (opcode now ported, or site unreachable): {Show(stale)}");
    }

    [Fact]
    public void Rule3_NoSkippedWaitNorZeroOrUnknownSizeOpcodeIsReached()
    {
        var offenders = Rule3WaitsOrUnsized(Corpus.Value, IsPorted);

        Assert.True(offenders.Count == 0, $"{offenders.Count} wait or unsized opcode(s) reached on the chain: {Show(offenders)}");
    }

    [Fact]
    public void Rule4_ScenePredicatesAreOnlyAtTheOneKnownSite()
    {
        var offenders = Rule4ScenePredicates(TheList());

        Assert.True(offenders.Count == 0, $"{offenders.Count} level-S predicate or branch line(s) beyond the one known site: {Show(offenders)}");
    }

    [Fact]
    public void TheListIsOnTheChainAndTheNineteenMapsWithoutASiteHaveNone()
    {
        var list = TheList();

        Assert.Empty(ChainMaps.Intersect(ExemptCombatMaps));
        Assert.Equal(30, ChainMaps.Distinct().Count());
        Assert.Equal(7, ExemptCombatMaps.Distinct().Count());
        Assert.Empty(list.Select(l => l.Site.Map).Distinct().Except(ChainMaps));
        Assert.Empty(MapsWithoutSkippedSite.Intersect(list.Select(l => l.Site.Map)));
        Assert.Equal(19, MapsWithoutSkippedSite.Length);
        Assert.Empty(MapsWithoutSkippedSite.Except(ChainMaps));
        Assert.Equal(list.Count, list.Select(l => l.Site).Distinct().Count()); // the key is unique.
    }

    [Fact]
    public void TheMapsWithTwoProgramsAtOnePcAreTwoLines()
    {
        var list = TheList();

        Assert.Equal(2, list.Count(l => l.Site.Map == 476 && l.Site.Pc == 112));
    }

    // -----------------------------------------------------------------------------------------
    // The oracle: the real runner
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Oracle_AnOpcodeTheRunnerHandlesIsPorted_OneItSkipsOrCannotSizeIsNot()
    {
        Assert.True(IsPorted(0x05)); // flag on
        Assert.True(IsPorted(0x00));
        Assert.True(IsPorted(0xFF));
        var absent = Enumerable.Range(0, 255).First(op => !EventOpcodeSizeTable.Entries.ContainsKey((byte)op));
        Assert.False(IsPorted(absent)); // no size: terminated
        Assert.Contains(Enumerable.Range(0, 255), op => EventOpcodeSizeTable.Entries.TryGetValue((byte)op, out var e) && e.Size > 0 && !IsPorted(op)); // skipped by size
    }

    // -----------------------------------------------------------------------------------------
    // The walk, on made-up programs (code 0 is a pad: a table entry of 0 means "no program")
    // -----------------------------------------------------------------------------------------

    private static EventProgramDocument Doc(int[] codes, int[]? b = null, int[]? c = null) => new()
    {
        MapIndex = 1,
        EventCodesATable = new int[8],
        EventCodesBTable = b ?? new[] { 0, 1 },
        EventCodesCTable = c ?? new int[8],
        EventCodesDTable = new int[8],
        EventCodesETable = new int[8],
        EventCodesFTable = new int[8],
        Codes = codes,
    };

    private static List<(char Slot, int Program, int Pc)> Walk(EventProgramDocument document, params (char, int)[] roots) =>
        WalkMap(1, document, roots).Select(r => (r.Site.Slot, r.Site.Program, r.Site.Pc)).ToList();

    [Fact]
    public void Walk_AGotoIsFollowedAndTheBytesItJumpsOverAreNotReached()
    {
        // @1 goto +5 -> @6 ; @4 and @5 are never read ; @6 end.
        var reached = Walk(Doc(new[] { 1, 0x02, 5, 0, 0x0A, 0x0A, 0xFF }), ('B', 1));

        Assert.Equal(new[] { 1, 6 }, reached.Select(r => r.Pc));
    }

    [Fact]
    public void Walk_BothBranchesOfAConditionalAreFollowed()
    {
        // @1 if true goto @6 ; @4 and @5 the fall-through ; @6 and @7 the target.
        var reached = Walk(Doc(new[] { 1, 0x03, 5, 0, 0x0A, 0xFF, 0x0A, 0xFF }), ('B', 1));

        Assert.Equal(new[] { 1, 4, 5, 6, 7 }, reached.Select(r => r.Pc).OrderBy(pc => pc));
    }

    [Fact]
    public void Walk_ASetProgramIndexOpcodeAddsItsTargetProgramAsARoot()
    {
        // B[1]: @1 set slot C program 2 ; @4 end. C[2] is at @5 (0x0A, end).
        var reached = Walk(Doc(new[] { 1, 0x40, 2, 2, 0xFF, 0x0A, 0xFF }, c: new[] { 0, 0, 5, 0, 0, 0, 0, 0 }), ('B', 1));

        Assert.Equal(new[] { ('B', 1, 1), ('B', 1, 4), ('C', 2, 5), ('C', 2, 6) }, reached.OrderBy(r => r.Slot).ThenBy(r => r.Pc));
    }

    [Fact]
    public void Walk_AReturnUserWalksTheCodeAfterTheJumpThatMarkedIt()
    {
        // @1 jump +5 -> @6 (marks @4) ; @6 return from the stored param ; @4 and @5 are reached through the mark.
        var reached = Walk(Doc(new[] { 1, 0x78, 5, 0, 0x0A, 0xFF, 0x7D }), ('B', 1));

        Assert.Equal(new[] { 1, 4, 5, 6 }, reached.Select(r => r.Pc).OrderBy(pc => pc));
    }

    [Fact]
    public void Walk_AZeroSizeOpcodeStopsTheProgramAndIsReported()
    {
        // 0x0E has size 0: the interpreter cannot step over it.
        var reached = Walk(Doc(new[] { 1, 0x0E, 0x0A, 0xFF }), ('B', 1));

        Assert.Equal(new[] { 1 }, reached.Select(r => r.Pc));
    }

    [Fact]
    public void Walk_ARestartEndsTheWalkOnCodeAlreadySeen()
    {
        var reached = Walk(Doc(new[] { 1, 0x0A, 0x49, 0x0A }), ('B', 1));

        Assert.Equal(new[] { 1, 2 }, reached.Select(r => r.Pc));
    }

    // -----------------------------------------------------------------------------------------
    // The rule functions, on made-up input
    // -----------------------------------------------------------------------------------------

    private static ListedSite Line(int map, char slot, int program, int pc, int opcode, char tier = 'O') =>
        new(new ProgramSite(map, slot, program, pc), opcode, "class", "slice", tier);

    private static ReachedSite Reached(int map, char slot, int program, int pc, int opcode) =>
        new(new ProgramSite(map, slot, program, pc), opcode);

    [Fact]
    public void ParseList_ReadsTheKeyTheOpcodeAndTheLevel()
    {
        var parsed = ParseList(new[]
        {
            "map\tslot\tprogram\tlabel\tpc\topcode\tname\tclass\tslice\ttier",
            "476\tB\t2\tmapevent1 zone(0,0,51,59)\t326\t0xA2\tCreate effect with pos\tEFFET\tE19.g\tS\tA2",
        });

        var line = Assert.Single(parsed);
        Assert.Equal(new ProgramSite(476, 'B', 2, 326), line.Site);
        Assert.Equal(0xA2, line.Opcode);
        Assert.Equal('S', line.Tier);
        Assert.Equal("E19.g", line.Slice);
    }

    [Fact]
    public void Rule1_ASkippedSiteWithNoLineOrWithALineOfAnotherOpcodeIsFlagged()
    {
        var list = new[] { Line(1, 'B', 1, 10, 0xA2) };

        Assert.Empty(Rule1UnlistedSites(new[] { Reached(1, 'B', 1, 10, 0xA2) }, list));
        Assert.Single(Rule1UnlistedSites(new[] { Reached(1, 'B', 1, 11, 0xA2) }, list)); // another pc
        Assert.Single(Rule1UnlistedSites(new[] { Reached(1, 'B', 2, 10, 0xA2) }, list)); // another program at the same pc
        Assert.Single(Rule1UnlistedSites(new[] { Reached(1, 'B', 1, 10, 0x92) }, list)); // another opcode
    }

    [Fact]
    public void Rule2_ALineForAPortedOpcodeOrAnUnreachedSiteIsFlagged()
    {
        var skipped = new[] { Reached(1, 'B', 1, 10, 0xA2) };

        Assert.Empty(Rule2StaleLines(new[] { Line(1, 'B', 1, 10, 0xA2) }, skipped));
        Assert.Single(Rule2StaleLines(new[] { Line(1, 'B', 1, 10, 0xA2), Line(1, 'B', 1, 12, 0x0B) }, skipped)); // 0x0B is ported: never a skipped site
        Assert.Single(Rule2StaleLines(new[] { Line(1, 'B', 1, 10, 0xA2), Line(1, 'C', 3, 10, 0xA2) }, skipped)); // unreachable
    }

    [Fact]
    public void Rule3_AFabricatedProgramWithAWaitOpcodeIsFlaggedWhileTheOpcodeIsSkipped()
    {
        var reached = WalkMap(1, Doc(new[] { 1, 0x20, 0, 0, 0xFF }), new[] { ('B', 1) });

        var offenders = Rule3WaitsOrUnsized(reached, _ => false);

        var offender = Assert.Single(offenders);
        Assert.Equal(0x20, offender.Opcode);
        Assert.Empty(Rule3WaitsOrUnsized(reached, op => op == 0x20)); // ported: it waits for real
    }

    [Fact]
    public void Rule3_EveryWaitOpcodeAndAZeroOrUnknownSizeAreFlaggedWhenSkipped()
    {
        foreach (var op in WaitOpcodes)
        {
            Assert.Single(Rule3WaitsOrUnsized(new[] { Reached(1, 'B', 1, 1, op) }, _ => false));
        }

        Assert.Single(Rule3WaitsOrUnsized(new[] { Reached(1, 'B', 1, 1, 0x0E) }, _ => false)); // size 0
        var absent = Enumerable.Range(0, 255).First(op => !EventOpcodeSizeTable.Entries.ContainsKey((byte)op));
        Assert.Single(Rule3WaitsOrUnsized(new[] { Reached(1, 'B', 1, 1, absent) }, _ => false)); // unknown size
        Assert.Empty(Rule3WaitsOrUnsized(new[] { Reached(1, 'B', 1, 1, 0xA2) }, _ => false)); // skipped by a known size, not a wait
    }

    [Fact]
    public void Rule4_APredicateOrBranchAtLevelSOutsideTheOneKnownSiteIsFlagged()
    {
        var known = new[]
        {
            Line(10, 'C', 75, 6418, 0x95, 'S'),
        };

        Assert.Empty(Rule4ScenePredicates(known));
        Assert.Empty(Rule4ScenePredicates(known.Append(Line(163, 'B', 2, 297, 0x82, 'O')))); // optional: allowed
        Assert.Empty(Rule4ScenePredicates(known.Append(Line(1, 'B', 1, 5, 0xA2, 'S')))); // not in the closed set
        Assert.Single(Rule4ScenePredicates(known.Append(Line(163, 'B', 2, 297, 0x82, 'S'))));
        Assert.Single(Rule4ScenePredicates(known.Append(Line(10, 'C', 76, 6418, 0x95, 'S')))); // same pc, other program
    }

    [Fact]
    public void Rule5_AnUnknownSkippedEntryOfAnArcTraceMustBeALine_TheOtherFailureKindsAreNotThisRules()
    {
        // C[2] starts at @5 ; C[3] shares that start.
        var document = Doc(new[] { 1, 0xFF, 0xFF, 0xFF, 0xFF, 0x0A, 0xFF }, c: new[] { 0, 0, 5, 5, 0, 0, 0, 0 });
        var skippedAt6 = new ArcInstruction(10, 2, 5, 6, 0xA2, EventTraceKind.UnknownSkipped);
        var list = new[] { Line(7, 'C', 2, 6, 0xA2) };

        Assert.Empty(Rule5UnlistedArcSites(7, document, new[] { skippedAt6 }, list));
        Assert.Empty(Rule5UnlistedArcSites(7, document, new[] { skippedAt6 }, new[] { Line(7, 'C', 3, 6, 0xA2) })); // a start shared by two indexes
        Assert.Single(Rule5UnlistedArcSites(7, document, new[] { skippedAt6 }, Array.Empty<ListedSite>()));
        Assert.Single(Rule5UnlistedArcSites(7, document, new[] { skippedAt6 with { Pc = 7 } }, list));
        Assert.Single(Rule5UnlistedArcSites(7, document, new[] { skippedAt6 }, new[] { Line(8, 'C', 2, 6, 0xA2) })); // another map
        Assert.Empty(Rule5UnlistedArcSites(7, document, new[]
        {
            skippedAt6 with { Kind = EventTraceKind.LoopBudgetExceeded },
            skippedAt6 with { Kind = EventTraceKind.UnknownNoSizeTerminated },
            skippedAt6 with { Kind = EventTraceKind.Implemented },
        }, Array.Empty<ListedSite>()));
    }
}
