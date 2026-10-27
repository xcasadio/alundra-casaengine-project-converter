#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Combat;
using Alundra.Entities;

namespace Alundra.Tests.Combat;

/// <summary>
/// E20.h4b1 (docs/plan-e20-heros.md section 1.31.2): the target query of the spells (<c>0x8003AF70</c> and its sort <c>0x8003AE04</c>). (q1) the binary mode (exclusive bound, sort from 3 results) against the 130 vectors of the real code
/// (the 120 of <c>e20-h4b/bin/vec</c> and the 10 complementary ones, the sentinel among them); (q2) the corrected mode (inclusive bound, sort from 2) against the vectors of the oracle of T0; (q3) every filter next to its opposite;
/// (q4) the sort; (q5) the key modulo 2^32 and the 32-bit difference; (q6) the production values of the settings. The expected values of (q1) and (q2) are the real code's and the oracle's, not computed by the code under test.
/// </summary>
public sealed class TargetQueryTests
{
    private const uint Mask = 1;
    private const int Type = 1;

    // -----------------------------------------------------------------------------------------
    // (q1) and (q2): the vectors.
    // -----------------------------------------------------------------------------------------

    private static List<int> SlotsOf(IEnumerable<EntityScriptProxy> found) => found.Select(p => p.Slot).ToList();

    [Fact]
    public void Q1_BinaryMode_ReplaysTheVectorsOfTheRealCode()
    {
        var vectors = TargetQueryVectorAdapter.LoadAll();
        Assert.Equal(130, vectors.Count);
        var failures = new List<string>();
        foreach (var v in vectors)
        {
            var found = SlotsOf(TargetQuery.Find(v.Entities, v.Count, v.Caller, v.Mask, v.Type, inclusiveBound: false, sortThreshold: 3));
            if (!found.SequenceEqual(v.RealSlots))
            {
                failures.Add($"{v.File} #{v.Index}: expected [{string.Join(", ", v.RealSlots)}], got [{string.Join(", ", found)}]");
            }
        }

        Assert.Empty(failures);
        // The files must have results (the empty lists alone could not tell the query from nothing) and the sentinel: the live eligible slot 4 = count is in the memory of the real code but never returned.
        Assert.True(vectors.Count(v => v.RealSlots.Count > 0) >= 70);
        var sentinel = vectors.Single(v => v.Case != null && v.Case.StartsWith("SENTINEL", StringComparison.Ordinal));
        Assert.Equal(new[] { 3, 2, 1 }, sentinel.RealSlots);
        Assert.Equal(4, sentinel.Count);
        Assert.Contains(sentinel.Entities, e => e.Slot == 4 && e.Status.IsActive());
    }

    [Fact]
    public void Q2_CorrectedMode_ReplaysTheVectorsOfTheOracle()
    {
        var vectors = TargetQueryVectorAdapter.LoadAll();
        using var document = JsonDocument.Parse(File.ReadAllText(TargetQueryVectorAdapter.FindUp(Path.Combine("docs", "plan-e20-annexe", "e20-h4b1", "vec_query_corrected.json"))));
        var corrected = document.RootElement.GetProperty("vectors").EnumerateArray().ToList();
        Assert.Equal(vectors.Count, corrected.Count);
        var failures = new List<string>();
        var differFromBinary = 0;
        for (var i = 0; i < vectors.Count; i++)
        {
            var v = vectors[i];
            var expected = corrected[i].GetProperty("corrected").EnumerateArray().Select(e => e.GetInt32()).ToList();
            Assert.Equal(v.File, corrected[i].GetProperty("file").GetString());
            Assert.Equal(v.CallerSlot, corrected[i].GetProperty("caller_slot").GetInt32());
            Assert.Equal(v.RealSlots, corrected[i].GetProperty("binary").EnumerateArray().Select(e => e.GetInt32()).ToList());
            if (!expected.SequenceEqual(v.RealSlots))
            {
                differFromBinary++;
            }

            var found = SlotsOf(TargetQuery.Find(v.Entities, v.Count, v.Caller, v.Mask, v.Type, inclusiveBound: true, sortThreshold: 2));
            if (!found.SequenceEqual(expected))
            {
                failures.Add($"{v.File} #{v.Index}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", found)}]");
            }
        }

        Assert.Empty(failures);
        // The README of T0: the corrected lists differ from the binary lists in 8 of the 130 vectors (1 for the bound, 7 for the threshold).
        Assert.Equal(8, differFromBinary);
    }

    // -----------------------------------------------------------------------------------------
    // Hand-made worlds: slot s at x = s * 10 pixels unless said, the caller at the origin.
    // -----------------------------------------------------------------------------------------

    private static EntityScriptProxy Hero(uint flags = 1) => Make(0, x: 0, flags: flags);

    private static BalanceRecord Sheet(int type, byte response)
    {
        var responses = new byte[11];
        var maxHp = 0;
        if (type + 2 == 2)
        {
            maxHp = response;
        }
        else
        {
            responses[type + 2 - 3] = response;
        }

        return new BalanceRecord(0, 15, maxHp, responses, Array.Empty<BalanceAttack>(), null);
    }

    private const int DefaultX = int.MinValue;

    private static EntityScriptProxy Make(
        int slot, int x = DefaultX, int y = 0, EntityStatus status = EntityStatus.Normal, uint flags = 1, bool blocked = false, int damaged = 0, int frameCollision = 0, int hitAnim = 0, bool sheet = true, byte response = 0,
        int sheetType = Type)
    {
        var proxy = new EntityScriptProxy
        {
            Slot = slot,
            Status = status,
            Flags = flags,
            PosX = (x == DefaultX ? slot * 10 : x) << 16,
            PosY = y << 16,
            DamagedTickCounter = damaged,
            FrameCollisionTickCounter = frameCollision,
            HitAnimByte = hitAnim,
            BalanceRecord = sheet ? Sheet(sheetType, response) : null,
        };
        if (blocked)
        {
            proxy.BlockedByEntity = new Entity { Name = "blocker" };
        }

        return proxy;
    }

    private static List<int> Run(IReadOnlyList<EntityScriptProxy> entities, int count, EntityScriptProxy caller, bool corrected = false, uint mask = Mask, int type = Type)
        => SlotsOf(TargetQuery.Find(entities, count, caller, mask, type, inclusiveBound: corrected, sortThreshold: corrected ? 2 : 3));

    /// <summary>The caller (the hero, slot 0, at the origin) then the given entities, count = the highest slot + 1.</summary>
    private static List<int> RunWith(params EntityScriptProxy[] others)
    {
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero };
        list.AddRange(others);
        return Run(list, others.Max(o => o.Slot) + 1, hero);
    }

    // -----------------------------------------------------------------------------------------
    // (q3) every filter next to its opposite.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Q3_Status_OnlyTwoAndThreeAreCandidates()
    {
        var found = RunWith(
            Make(1, status: EntityStatus.Loaded), Make(2, status: EntityStatus.Normal), Make(3, status: EntityStatus.Deactivated), Make(4, status: EntityStatus.Destroyed), Make(5, status: (EntityStatus)5));
        Assert.Equal(new[] { 2, 3 }, found);
    }

    [Fact]
    public void Q3_StatusFour_IsNotACandidateAndStatusOneNeither()
    {
        Assert.Equal(new[] { 2 }, RunWith(Make(1, status: EntityStatus.Loaded), Make(2), Make(3, status: (EntityStatus)4)));
    }

    [Fact]
    public void Q3_BlockedByEntity_ExcludesTheBlockedOne()
    {
        Assert.Equal(new[] { 2 }, RunWith(Make(1, blocked: true), Make(2)));
    }

    [Fact]
    public void Q3_Mask_KeepsTheEntitiesWithOneBitOfTheMask()
    {
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero, Make(1, flags: 2), Make(2, flags: 3), Make(3, flags: 4) };
        Assert.Equal(new[] { 2 }, Run(list, 4, hero, mask: 1));
        Assert.Equal(new[] { 1, 2 }, Run(list, 4, hero, mask: 2));
        Assert.Equal(new[] { 1, 2, 3 }, Run(list, 4, hero, mask: 7));
        Assert.Equal(new[] { 3 }, Run(list, 4, hero, mask: 4));
    }

    [Fact]
    public void Q3_TheTwoCounters_ExcludeAnEntityWhenNonZero()
    {
        var found = RunWith(Make(1, damaged: 1), Make(2, frameCollision: 1), Make(3, damaged: 0, frameCollision: 0), Make(4, damaged: 5, frameCollision: 5));
        Assert.Equal(new[] { 3 }, found);
    }

    [Fact]
    public void Q3_HitAnimByte_BitForty_Excludes()
    {
        // 0x40 and 0xC0 hold the bit; 0x3F and 0x80 do not (the byte of the hit pass, not the flags of the animation).
        var found = RunWith(Make(1, hitAnim: 0x40), Make(2, hitAnim: 0x3F), Make(3, hitAnim: 0xC0), Make(4, hitAnim: 0x80), Make(5, hitAnim: 0));
        Assert.Equal(new[] { 2, 4, 5 }, found);
    }

    [Fact]
    public void Q3_NoSheet_Excludes()
    {
        Assert.Equal(new[] { 2 }, RunWith(Make(1, sheet: false), Make(2)));
    }

    [Fact]
    public void Q3_Response_EightyIsDiscarded_FortyC0And00AreKept()
    {
        var found = RunWith(Make(1, response: 0x80), Make(2, response: 0xC0), Make(3, response: 0x40), Make(4, response: 0x00), Make(5, response: 0xBF), Make(6, response: 0x81));
        Assert.Equal(new[] { 2, 3, 4 }, found);
    }

    [Fact]
    public void Q3_Response_IsTheByteOfTheAskedType()
    {
        // The sheet answers 0x80 for the type 2 only: asked for the type 1 it is kept, asked for the type 2 it is discarded.
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero, Make(1, response: 0x80, sheetType: 2), Make(2) };
        Assert.Equal(new[] { 1, 2 }, Run(list, 3, hero, type: 1));
        Assert.Equal(new[] { 2 }, Run(list, 3, hero, type: 2));
    }

    [Fact]
    public void Q3_TheCaller_IsNeverACandidate()
    {
        var hero = Hero();
        var caller = Make(2);
        var list = new List<EntityScriptProxy> { hero, Make(1), caller, Make(3) };
        Assert.Equal(new[] { 1, 3 }, Run(list, 4, caller));
    }

    [Fact]
    public void Q3_TheHeroAtSlotZero_EvenFullyEligible_IsNeverACandidate()
    {
        // Slot 0 is status 2, has the bit of the mask, a sheet and no exclusion; another entity is the caller.
        var hero = Make(0, x: 0);
        var caller = Make(1);
        var list = new List<EntityScriptProxy> { hero, caller, Make(2) };
        Assert.Equal(new[] { 2 }, Run(list, 3, caller));
        Assert.Equal(new[] { 2 }, Run(list, 3, caller, corrected: true));
    }

    [Fact]
    public void Q3_AFreeCellBetweenTwoCandidates_TheScanGoesBySlotNotByListIndex()
    {
        // The list is compact: the cell 2 is free (absent), 3 is the second candidate, 5 is beyond the count.
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero, Make(1), Make(3), Make(5) };
        Assert.Equal(new[] { 1, 3 }, Run(list, 4, hero));
        Assert.Equal(new[] { 1, 3 }, Run(list, 4, hero, corrected: true));
        Assert.Equal(new[] { 1, 3 }, Run(list, 5, hero));
        Assert.Equal(new[] { 1, 3, 5 }, Run(list, 5, hero, corrected: true));
        Assert.Equal(new[] { 1, 3, 5 }, Run(list, 6, hero));
    }

    [Fact]
    public void Q3_TheBound_ExclusiveInTheBinaryInclusiveWhenCorrected()
    {
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero, Make(1), Make(2), Make(3), Make(4) };
        Assert.Equal(new[] { 1, 2, 3 }, Run(list, 4, hero));
        Assert.Equal(new[] { 1, 2, 3, 4 }, Run(list, 4, hero, corrected: true));
        Assert.Empty(Run(list, 1, hero));
        Assert.Equal(new[] { 1 }, Run(list, 1, hero, corrected: true));
        Assert.Empty(Run(list, 0, hero));
    }

    [Fact]
    public void Q3_AProxyOutsideTheTable_IsNeverACandidate()
    {
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero, Make(-1), Make(1) };
        Assert.Equal(new[] { 1 }, Run(list, 3, hero, corrected: true));
    }

    // -----------------------------------------------------------------------------------------
    // (q4) the sort.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Q4_TwoResultsInTheWrongOrder_AreNotSortedInTheBinaryAndSortedWhenCorrected()
    {
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero, Make(1, x: 100), Make(2, x: 10) };
        Assert.Equal(new[] { 1, 2 }, Run(list, 3, hero));
        Assert.Equal(new[] { 2, 1 }, Run(list, 3, hero, corrected: true));
    }

    [Fact]
    public void Q4_ThreeResultsWithEqualKeys_TakeTheOrderOfHoare()
    {
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero, Make(1, x: 5), Make(2, x: 5), Make(3, x: 5) };
        Assert.Equal(new[] { 3, 2, 1 }, Run(list, 4, hero));
        Assert.Equal(new[] { 3, 2, 1 }, Run(list, 4, hero, corrected: true));
    }

    [Fact]
    public void Q4_FourResultsWithTies_TakeTheOrderOfHoare()
    {
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero, Make(1, x: 2), Make(2, x: 1), Make(3, x: 1), Make(4, x: 2) };
        Assert.Equal(new[] { 2, 3, 4, 1 }, Run(list, 5, hero, corrected: true));
        Assert.Equal(new[] { 2, 3, 4, 1 }, Run(list, 5, hero));
    }

    [Fact]
    public void Q4_DistinctKeys_AreSortedAscending()
    {
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero, Make(1, x: 5), Make(2, x: 3), Make(3, x: 9), Make(4, x: 1), Make(5, x: 7) };
        Assert.Equal(new[] { 4, 2, 1, 5, 3 }, Run(list, 6, hero));
        Assert.Equal(new[] { 4, 2, 1, 5, 3 }, Run(list, 6, hero, corrected: true));
    }

    [Fact]
    public void Q4_TheKeyIsTheSquaredDistanceOnBothAxes()
    {
        // Slot 1 is at (6, 0): key 36; slot 2 at (3, 4): key 25; slot 3 at (0, -4): key 16.
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero, Make(1, x: 6, y: 0), Make(2, x: 3, y: 4), Make(3, x: 0, y: -4) };
        Assert.Equal(new[] { 3, 2, 1 }, Run(list, 4, hero, corrected: true));
        var farOnY = new List<EntityScriptProxy> { hero, Make(1, x: 0, y: 10), Make(2, x: 9, y: 0) };
        Assert.Equal(new[] { 2, 1 }, Run(farOnY, 3, hero, corrected: true));
    }

    [Fact]
    public void Q4_TheDistanceIsMeasuredFromTheCaller()
    {
        // The caller is the slot 2 at (50, 0): slot 1 at (10, 0) is 40 away, slot 3 at (45, 0) is 5 away.
        var hero = Hero();
        var caller = Make(2, x: 50);
        var list = new List<EntityScriptProxy> { hero, Make(1, x: 10), caller, Make(3, x: 45) };
        Assert.Equal(new[] { 3, 1 }, Run(list, 4, caller, corrected: true));
        Assert.Equal(new[] { 1, 3 }, Run(list, 4, caller));
    }

    // -----------------------------------------------------------------------------------------
    // (q5) the key modulo 2^32 and the 32-bit difference.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Q5_TheKeyIsUnsigned_ASquaredDistanceOfTwoToThe31IsTheFarthest()
    {
        // Slot 1 at (-32768, -32768) from the origin: key 0x80000000 (it does not fit in a signed 32-bit key); slot 2 near.
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero, Make(1, x: -32768, y: -32768), Make(2, x: 10) };
        Assert.Equal(new[] { 2, 1 }, Run(list, 3, hero, corrected: true));
    }

    [Fact]
    public void Q5_TheDifferenceIsComputedOn32Bits_ThenShifted()
    {
        // The caller is at 16.16 0x7FFF0000, the slot 1 at -0x7FFF0000: the 32-bit difference is 0x00020000, so dx = 2 (key 4); in 64 bits it would be -65534. The slot 2 is 3 away (key 9).
        var caller = new EntityScriptProxy { Slot = 0, Status = EntityStatus.Normal, Flags = 1, PosX = 0x7FFF0000, PosY = 0 };
        var wrapped = Make(1);
        wrapped.PosX = -0x7FFF0000;
        wrapped.PosY = 0;
        var near = Make(2);
        near.PosX = unchecked(0x7FFF0000 + 3 * 0x10000);
        near.PosY = 0;
        var list = new List<EntityScriptProxy> { caller, wrapped, near };
        Assert.Equal(new[] { 1, 2 }, Run(list, 3, caller, corrected: true));
        near.PosX = unchecked(0x7FFF0000 + 1 * 0x10000);
        Assert.Equal(new[] { 2, 1 }, Run(list, 3, caller, corrected: true));
    }

    [Fact]
    public void Q5_TheDifferenceIsShiftedArithmetically_ANegativeOffsetIsItsAbsoluteDistance()
    {
        // dx = -3 (key 9) against dx = +4 (key 16): the nearer is the negative one.
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero, Make(1, x: 4), Make(2, x: -3) };
        Assert.Equal(new[] { 2, 1 }, Run(list, 3, hero, corrected: true));
    }

    // -----------------------------------------------------------------------------------------
    // (q6) the production values of the settings.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Q6_TheProductionSettings_AreTheCorrectedDefaultsOfQH4B2AndQH4B3()
    {
        Assert.True(TargetQuery.InclusiveBound);
        Assert.Equal(2, TargetQuery.SortThreshold);
    }

    [Fact]
    public void Q6_TheDefaultCall_UsesTheProductionSettings()
    {
        var hero = Hero();
        var list = new List<EntityScriptProxy> { hero, Make(1, x: 100), Make(2, x: 10), Make(3, x: 20) };
        // The slot 3 = the count: listed because the bound is inclusive; two or more results are sorted.
        Assert.Equal(new[] { 2, 3, 1 }, SlotsOf(TargetQuery.Find(list, 3, hero, Mask, Type)));
    }
}
