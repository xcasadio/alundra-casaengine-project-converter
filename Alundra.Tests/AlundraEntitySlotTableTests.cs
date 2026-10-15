#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E20.e0a2 (docs/plan-e20-heros.md section 1.16.2, ADR-0064): the table of the entity slots, alone (no engine, no world), against the V5 lines of the oracle of <c>docs/plan-e20-annexe/e20-e0a/oracle/out/v5.json</c>
/// (the real <c>FindFreeEntitySlot</c> <c>0x80039AD0</c> and the real slot pass <c>0x80038634</c> of the binary). Each test names the V5 line it copies; the numbers of the lines are read from the file, never recomputed.
/// </summary>
public sealed class AlundraEntitySlotTableTests
{
    private static JsonElement V5(string line)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AlundraSpawnIndexOracle.OutputFolder(), "v5.json")));
        return document.RootElement.GetProperty("table").GetProperty(line).Clone();
    }

    private static int[] Take(AlundraEntitySlotTable table, int count) => Enumerable.Range(0, count).Select(_ => table.Allocate()).ToArray();

    [Fact]
    public void V5_five_FiveApparitionsTakeSlots1To5_AndTheTableHoldsTheHeroPlusFive()
    {
        var table = new AlundraEntitySlotTable();
        Assert.Equal(V5("five").GetProperty("slots").EnumerateArray().Select(e => e.GetInt32()).ToArray(), Take(table, 5));
        Assert.Equal(V5("five").GetProperty("count_word").GetInt32() + 1, table.HeldCount); // the count word of the binary is the highest slot; the hero is the extra held one.
    }

    [Fact]
    public void V5_busy_status4_ASlotOfADestroyedEntityIsBusyUntilItIsReleased_TheNextApparitionGoesAbove()
    {
        var table = new AlundraEntitySlotTable();
        Take(table, 5); // slot 2 now holds an entity of the state 4 (FlagToDestroy): nobody released it.
        Assert.Equal(V5("busy_status4").GetProperty("slot_of_spawn_before_pass").GetInt32(), table.Allocate());
        Assert.True(table.IsHeld(2));
    }

    [Fact]
    public void V5_reuse_after_pass_AfterTheSlotIsReleased_TheNextApparitionTakesIt()
    {
        var table = new AlundraEntitySlotTable();
        Take(table, 6);
        table.Release(2);
        Assert.False(table.IsHeld(2));
        Assert.Equal(V5("reuse_after_pass").GetProperty("slot").GetInt32(), table.Allocate());
    }

    [Fact]
    public void V5_full_TheHeroPlus63ApparitionsFillTheTable_TheSixtyFourthAndSixtyFifthAreRefused()
    {
        var full = V5("full");
        var table = new AlundraEntitySlotTable();
        var slots = Take(table, full.GetProperty("accepted").GetInt32());
        Assert.Equal(full.GetProperty("slots_first_last").EnumerateArray().Select(e => e.GetInt32()).ToArray(), new[] { slots[0], slots[^1] });
        Assert.Equal(Enumerable.Range(1, 63), slots);
        Assert.Equal(full.GetProperty("live_slots_with_the_hero").GetInt32(), table.HeldCount);
        Assert.Equal(-full.GetProperty("refused_64th").GetInt32() - 1, table.Allocate()); // the binary returns the null pointer; the table returns -1.
        Assert.Equal(-full.GetProperty("refused_65th").GetInt32() - 1, table.Allocate());
        Assert.Equal(64, table.HeldCount); // a refusal takes nothing.
    }

    [Fact]
    public void V5_lowest_first_AfterReleasing40_9_And21_TheNextThreeApparitionsTakeThemLowestFirst()
    {
        var table = new AlundraEntitySlotTable();
        Take(table, 63);
        foreach (var slot in new[] { 40, 9, 21 })
        {
            table.Release(slot);
        }

        Assert.Equal(V5("lowest_first").EnumerateArray().Select(e => e.GetInt32()).ToArray(), Take(table, 3));
    }

    [Fact]
    public void V5_recycled_loop2_AnApparitionAfterTheSlotPassOfTheSameTick_TakesTheRecycledSlot_ThenTheNextOnes()
    {
        var line = V5("recycled_loop2");
        var table = new AlundraEntitySlotTable();
        var before = Take(table, 5);
        Assert.Equal(line.GetProperty("slots_before").EnumerateArray().Select(e => e.GetInt32()).ToArray(), before);
        foreach (var dead in line.GetProperty("dead_before").EnumerateArray())
        {
            table.Release(dead.GetInt32()); // the slot pass of the tick frees the entities of the state 4.
        }

        Assert.Equal(line.GetProperty("spawned_in_loop2_slot").GetInt32(), table.Allocate()); // the apparition of loop 2.
        Assert.Equal(line.GetProperty("second_after_pass").GetInt32(), table.Allocate());
        Assert.Equal(line.GetProperty("third_after_pass").GetInt32(), table.Allocate());
    }

    [Fact]
    public void TheHeroSlot0_IsHeldFromTheConstruction_EvenInAWorldWithoutHero_AndNothingAllocatesIt()
    {
        var table = new AlundraEntitySlotTable();
        Assert.True(table.IsHeld(0));
        Assert.Equal(1, table.HeldCount);
        Assert.DoesNotContain(0, Take(table, 63));
    }

    [Fact]
    public void ReleaseOfSlot0_IsNotPossible_TheHeroIsNeverFreed()
    {
        var table = new AlundraEntitySlotTable();
        table.Release(0);
        Assert.True(table.IsHeld(0));
        Assert.Equal(1, table.Allocate());
    }

    [Fact]
    public void ReleaseOfMinusOne_AnEntityWithoutASlot_HasNoEffect()
    {
        var table = new AlundraEntitySlotTable();
        Take(table, 4);
        var before = table.HeldCount;
        table.Release(-1);
        Assert.Equal(before, table.HeldCount);
        Assert.Equal(5, table.Allocate());
    }

    [Theory]
    [InlineData(64)]
    [InlineData(100)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ReleaseOutsideTheTable_HasNoEffect(int slot)
    {
        var table = new AlundraEntitySlotTable();
        Take(table, 63);
        table.Release(slot);
        Assert.Equal(64, table.HeldCount);
        Assert.Equal(-1, table.Allocate());
    }

    [Fact]
    public void ReleasingTwice_ThenAllocating_GivesTheSlotOnce()
    {
        var table = new AlundraEntitySlotTable();
        Take(table, 3);
        table.Release(2);
        table.Release(2);
        Assert.Equal(2, table.Allocate());
        Assert.Equal(4, table.Allocate()); // 2 is held again, the next free one is 4.
    }

    [Fact]
    public void AReleasedSlotLowerThanTheOthers_IsTakenBeforeAnyFreshHigherOne()
    {
        var table = new AlundraEntitySlotTable();
        Take(table, 10);
        table.Release(7);
        table.Release(3);
        Assert.Equal(new[] { 3, 7, 11 }, Take(table, 3));
    }

    [Fact]
    public void TwoTables_AreIndependent_ANewMapStartsEmpty()
    {
        var first = new AlundraEntitySlotTable();
        var second = new AlundraEntitySlotTable();
        Take(first, 63);
        Assert.Equal(1, second.Allocate());
    }

    [Fact]
    public void AFullTableRefusesWithoutDisturbing_ThenAReleaseMakesRoomForExactlyOne()
    {
        var table = new AlundraEntitySlotTable();
        Take(table, 63);
        Assert.Equal(-1, table.Allocate());
        table.Release(63);
        Assert.Equal(63, table.Allocate());
        Assert.Equal(-1, table.Allocate());
    }
}
