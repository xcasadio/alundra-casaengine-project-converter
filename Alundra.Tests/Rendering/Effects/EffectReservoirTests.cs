#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using Alundra.Rendering.Effects;
using Alundra.Tests.Support;

namespace Alundra.Tests.Rendering.Effects;

/// <summary>
/// E19.g G3a (docs/plan-e19-opcodes.md, section 1.2o.6, G3A-R2 to R4 and R7): the effect reservoir against the oracles of the binary. The values come from
/// <c>g3a-golden.tsv</c> (generated, committed before the code) and were measured red on empty types first.
/// </summary>
public sealed class EffectReservoirTests
{
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();

    private static EffectReservoir Reservoir(EffectBank bank, List<string>? warnings = null) => new(bank, warnings is null ? null : warnings.Add);

    [Fact]
    public void EveryAnimationOfTheBinary_IsDrawnFromTick0ToItsLastTick_ThenFreedOrLooped()
    {
        var banks = new Dictionary<int, EffectBank>();
        var rows = EffectGolden.Animations;
        Assert.Equal(363, rows.Count);
        Assert.Equal(130, rows.Count(r => r.Destroy));

        foreach (var row in rows)
        {
            var bank = row.MapTable
                ? banks.TryGetValue(row.MapId, out var cached) ? cached : banks[row.MapId] = EffectGolden.BankOf(Root, row.MapId)
                : banks.TryGetValue(-1, out var global) ? global : banks[-1] = EffectGolden.BankOf(Root, 7);
            var reservoir = Reservoir(bank);
            var slot = reservoir.Create(row.MapTable, row.Effect, row.Animation, 0, 0, 0);
            Assert.NotNull(slot);
            Assert.True(bank.TryGetTable(row.MapTable, row.Effect, out var table), row.Key);

            var ticks = row.Destroy ? row.LastDrawn + 3 : 2 * row.Life + 1;
            for (var tick = 0; tick < ticks; tick++)
            {
                reservoir.Tick(frozen: false);
                if (row.Destroy && tick > row.FreeTick - 1)
                {
                    Assert.False(slot!.Active, $"{row.Key} tick {tick}: freed at {row.FreeTick}");
                    continue;
                }

                Assert.True(slot!.Active, $"{row.Key} tick {tick}");
                Assert.Equal(row.Destroy && tick == row.LastDrawn, slot.Destroying);

                // the frame shown at this tick: the cumulative delays, the last frame held on the last drawn tick of a Destroy, wrapped on a Loop
                var at = row.Destroy ? Math.Min(tick, row.Life - 1) : tick % row.Life;
                var set = row.Frames[0][1];
                var elapsed = 0;
                foreach (var frame in row.Frames)
                {
                    if (at < elapsed + frame[0])
                    {
                        set = frame[1];
                        break;
                    }

                    elapsed += frame[0];
                    set = frame[1];
                }

                Assert.Equal(set, slot.CurrentImageSetIndex);
                Assert.Same(table.ImageSets[set], slot.CurrentImageSet);
                if (tick == 0)
                {
                    Assert.Equal(row.FirstIdsv, slot.CurrentImageSet!.Idsv);
                    Assert.Equal(row.FirstImages, slot.CurrentImageSet.Images.Count);
                }
            }

            Assert.Equal(row.Destroy ? 0 : 1, reservoir.ActiveCount);
        }
    }

    [Fact]
    public void LoadSpawn_OnTheTile00_GivesTheSlotsOfTheBinary_OnAllTheMapsOfTheExport()
    {
        var expected = EffectGolden.Loads.GroupBy(l => l.Map).ToDictionary(g => g.Key, g => g.OrderBy(l => l.Slot).ToList());
        var total = 0;
        foreach (var map in EffectGolden.AllMapIds(Root))
        {
            var bank = EffectGolden.BankOf(Root, map);
            var reservoir = Reservoir(bank);
            reservoir.SpawnAtLoad(0, 0);

            var rows = expected.GetValueOrDefault(map) ?? new();
            var slots = reservoir.ActiveSlots.ToList();
            Assert.Equal(rows.Count, slots.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var slot = slots[i];
                Assert.Equal(rows[i].Slot, slot.Index);
                Assert.Equal(rows[i].Record, slot.RecordIndex);
                Assert.Equal((rows[i].XPx << 16, rows[i].YPx << 16, rows[i].ZPx << 16), (slot.X, slot.Y, slot.Z));
                Assert.Equal(0, slot.Mode);
                Assert.Equal(bank.Records[slot.RecordIndex].Flags & 0x80, slot.TargetMapTable ? 0x80 : 0);
                Assert.Equal(bank.Records[slot.RecordIndex].Anim, slot.TargetAnimation);
                total++;
            }
        }

        Assert.Equal(245, total);
        Assert.Equal(79, expected.Count(e => e.Value.Count > 0));
    }

    [Fact]
    public void LoadSpawn_TheSixRestrictedRecords_FollowTheInclusiveZoneOfTheHeroTile()
    {
        Assert.Equal(6, EffectGolden.Zones.Count);
        foreach (var zone in EffectGolden.Zones)
        {
            var bank = EffectGolden.BankOf(Root, zone.Map);
            foreach (var (tileX, tileY, inside) in new[]
            {
                (zone.X1, zone.Y1, true), (zone.X2, zone.Y2, true), (zone.X1 - 1, zone.Y1, false), (zone.X2 + 1, zone.Y2, false), (zone.X1, zone.Y1 - 1, false),
                (zone.X2, zone.Y2 + 1, false),
            })
            {
                var reservoir = Reservoir(bank);
                reservoir.SpawnAtLoad(tileX, tileY);
                Assert.Equal(inside, reservoir.ActiveSlots.Any(s => s.RecordIndex == zone.Record));
            }
        }
    }

    [Fact]
    public void LoadSpawn_LeavesNoSlotForARecordWithoutTheBit0x40_AndNothingForAMapWithoutCompanion()
    {
        var bank = EffectGolden.Synthetic(new[] { EffectGolden.Anim("Loop", new[] { 5, 0 }) }, 1,
            EffectGolden.Record(0x80, 1, 1, 0, 0), EffectGolden.Record(0xC0, 2, 2, 0, 0), EffectGolden.Record(0x40, 3, 3, 1, 0));
        var reservoir = Reservoir(bank);
        reservoir.SpawnAtLoad(10, 10);

        Assert.Equal(new[] { 1, 2 }, reservoir.ActiveSlots.Select(s => s.RecordIndex));
        Assert.Equal(new[] { 0, 1 }, reservoir.ActiveSlots.Select(s => s.Index));
        Assert.Equal(((2 * 12 + 12) << 16, (2 * 8 + 8) << 16, 0), (reservoir.Slots[0].X, reservoir.Slots[0].Y, reservoir.Slots[0].Z));
        Assert.Equal(((3 * 12 + 12) << 16, (3 * 8 + 8) << 16, 1 << 19), (reservoir.Slots[1].X, reservoir.Slots[1].Y, reservoir.Slots[1].Z));
        Assert.True(reservoir.Slots[0].TargetMapTable);
        Assert.False(reservoir.Slots[1].TargetMapTable);

        var empty = Reservoir(EffectBank.Empty);
        empty.SpawnAtLoad(0, 0);
        Assert.Equal(0, empty.ActiveCount);
    }

    [Fact]
    public void ThePool_Has128Slots_The129thCreationDoesNothing_AndTheSmallestFreeSlotWins()
    {
        var bank = EffectGolden.Synthetic(new[] { EffectGolden.Anim("Loop", new[] { 5, 0 }) }, 1, EffectGolden.Record(0x80, 4, 4, 0, 0));
        var warnings = new List<string>();
        var reservoir = Reservoir(bank, warnings);
        Assert.Equal(128, EffectReservoir.SlotCount);
        Assert.Equal(128, reservoir.Slots.Count);

        for (var i = 0; i < 128; i++)
        {
            Assert.Equal(i, reservoir.Spawn(0)!.Index);
        }

        Assert.Equal(128, reservoir.ActiveCount);
        Assert.Null(reservoir.Spawn(0));
        Assert.Null(reservoir.Create(true, 0, 0, 1, 2, 3));
        Assert.Equal(128, reservoir.ActiveCount);
        Assert.Empty(warnings);

        reservoir.Free(reservoir.Slots[5]);
        Assert.Equal(127, reservoir.ActiveCount);
        Assert.Equal(5, reservoir.Spawn(0)!.Index);
        Assert.Equal(128, reservoir.ActiveCount);
    }

    [Fact]
    public void SpawnOfARecord_NeedsNeitherTheZoneNorTheBit0x40_AndAnUnknownRecordDoesNothing()
    {
        var bank = EffectGolden.Synthetic(new[] { EffectGolden.Anim("Loop", new[] { 5, 0 }) }, 1,
            EffectGolden.Record(0x80, 4, 4, 0, 0, x1: 40, y1: 40, x2: 41, y2: 41));
        var reservoir = Reservoir(bank);

        Assert.NotNull(reservoir.Spawn(0));
        Assert.Null(reservoir.Spawn(1));
        Assert.Null(reservoir.Spawn(-1));
        Assert.Equal(1, reservoir.ActiveCount);
    }

    [Fact]
    public void ABlockedTick_MovesNothing_KeepsTheSlotAndIts_DestroyedStateIsNotFreed()
    {
        var bank = EffectGolden.Synthetic(new[] { EffectGolden.Anim("Destroy", new[] { 1, 0 }, new[] { 1, 1 }) }, 2);
        var reservoir = Reservoir(bank);
        var slot = reservoir.Create(true, 0, 0, 0, 0, 0)!;
        slot.ForceX = 100;

        reservoir.Tick(frozen: true);
        Assert.True(slot.Active);
        Assert.Equal((0, -1), (slot.X, slot.CurrentImageSetIndex));

        reservoir.Tick(frozen: false);
        Assert.Equal((100, 0), (slot.X, slot.CurrentImageSetIndex));
        reservoir.Tick(frozen: true);
        reservoir.Tick(frozen: true);
        Assert.Equal((100, 0), (slot.X, slot.CurrentImageSetIndex));

        reservoir.Tick(frozen: false); // frame 1
        Assert.Equal((200, 1, false), (slot.X, slot.CurrentImageSetIndex, slot.Destroying));
        reservoir.Tick(frozen: false); // S = 2: the flag is set, still drawn
        Assert.Equal((300, 1, true), (slot.X, slot.CurrentImageSetIndex, slot.Destroying));
        Assert.True(slot.Active);

        reservoir.Tick(frozen: true); // a blocked tick never frees it
        reservoir.Tick(frozen: true);
        Assert.True(slot.Active);
        Assert.Equal(300, slot.X);

        reservoir.Tick(frozen: false); // S + 1: freed, not moved
        Assert.False(slot.Active);
        Assert.Equal(300, slot.X);
        Assert.Equal(0, reservoir.ActiveCount);
    }

    [Fact]
    public void ModeZero_AddsTheForces_ThenTheDepthKeyIsYHigh_PlusZHigh_PlusIdsvShifted()
    {
        var bank = EffectGolden.Synthetic(new[] { EffectGolden.Anim("Loop", new[] { 3, 1 }) }, 2);
        var reservoir = Reservoir(bank);
        var slot = reservoir.Create(true, 0, 0, 0x00010000, 0x00230001, 0x00050000 + 0x1234)!;
        slot.ForceX = 0x100;
        slot.ForceY = -0x40;
        slot.ForceZ = 0x10;

        reservoir.Tick(frozen: false);

        Assert.Equal((0x00010100, 0x0022FFC1, 0x00051244), (slot.X, slot.Y, slot.Z));
        // (y & 0xFFFF0000) + (z >> 16) + (IDSV << 16): y = 0x0022FFC1 -> 0x00220000, z >> 16 = 5, IDSV 11 -> 0x000B0000
        Assert.Equal(0x00220000u + 5u + (11u << 16), slot.DepthKey);
        Assert.Equal(11, slot.Idsv);
    }

    [Fact]
    public void AnAnimationTheTableDoesNotHold_AWarningOnce_ThenTheSlotIsDestroyed()
    {
        var bank = EffectGolden.Synthetic(new[] { EffectGolden.Anim("Loop", new[] { 3, 0 }) }, 1);
        var warnings = new List<string>();
        var reservoir = Reservoir(bank, warnings);
        var missingAnimation = reservoir.Create(true, 0, 9, 0, 0, 0)!;
        var missingEffect = reservoir.Create(true, 5, 0, 0, 0, 0)!;
        var globalWithoutBank = reservoir.Create(false, 0, 0, 0, 0, 0)!;

        reservoir.Tick(frozen: false);
        Assert.Equal(1, warnings.Count);
        Assert.All(warnings, w => Assert.Contains("Effect", w));
        Assert.True(missingAnimation.Active && missingEffect.Active && globalWithoutBank.Active); // destroyed this tick, freed at the next
        Assert.All(new[] { missingAnimation, missingEffect, globalWithoutBank }, s => Assert.True(s.Destroying));

        reservoir.Tick(frozen: false);
        Assert.Equal(0, reservoir.ActiveCount);
        Assert.Equal(1, warnings.Count);
    }

    [Fact]
    public void AnEndThatIsNeitherDestroyNorLoop_IsAWarningOnce_AndADestroy()
    {
        var bank = EffectGolden.Synthetic(new[] { EffectGolden.Anim("Bounce", new[] { 1, 0 }) }, 1);
        var warnings = new List<string>();
        var reservoir = Reservoir(bank, warnings);
        var slot = reservoir.Create(true, 0, 0, 0, 0, 0)!;

        reservoir.Tick(frozen: false);
        Assert.False(slot.Destroying);
        reservoir.Tick(frozen: false);
        Assert.True(slot.Destroying);
        reservoir.Tick(frozen: false);
        Assert.False(slot.Active);
        Assert.Equal(1, warnings.Count);
    }

    [Fact]
    public void TheAuraOfMap476_IsDrawnFromTick0ToTheLastTickOfTheBinary_AndFreedAfterIt()
    {
        var row = EffectGolden.Scenario("S476").Single(r => r[2] == "dead");
        var dead = int.Parse(row[3]);
        var lastDrawn = int.Parse(row[5]);
        var freed = int.Parse(row[7]);
        var depth0 = uint.Parse(row[9]);

        var reservoir = Reservoir(EffectGolden.BankOf(Root, 476));
        var slot = reservoir.Spawn(0)!; // record 0: flags 0x80 only, not spawned at load; animation 1 (the loop)
        slot.TargetAnimation = 0; // what 0x92 [0, 0] does (G3b)
        slot.X = 62914560;
        slot.Y = 10485760;
        slot.Z = 3145729;

        for (var tick = 0; tick <= freed; tick++)
        {
            reservoir.Tick(frozen: false);
            if (tick == 0)
            {
                Assert.Equal(depth0, slot.DepthKey);
                Assert.Equal(0, slot.CurrentAnimation);
            }

            Assert.Equal(tick < freed, slot.Active);
            if (slot.Active)
            {
                Assert.Equal(tick == dead, slot.Destroying);
            }
        }

        Assert.Equal(lastDrawn, dead);
        Assert.Equal(freed, lastDrawn + 1);
    }

    [Fact]
    public void ASwitchOfTheTargetAnimation_BeforeTheFreeingTick_RevivesTheSlot_AtTheFreeingTickItIsGone()
    {
        var revives = EffectGolden.Scenario("S476-revive").ToList();
        Assert.Equal(5, revives.Count);
        foreach (var row in revives)
        {
            var applied = int.Parse(row[2]);
            var status = int.Parse(row[3]);
            var reservoir = Reservoir(EffectGolden.BankOf(Root, 476));
            var slot = reservoir.Spawn(0)!;
            slot.TargetAnimation = 0;
            for (var tick = 0; tick < applied; tick++)
            {
                reservoir.Tick(frozen: false);
            }

            slot.TargetAnimation = 1; // 0x92 [0, 1]
            for (var tick = 0; tick < 60; tick++)
            {
                reservoir.Tick(frozen: false);
            }

            Assert.Equal(status == 2, slot.Active);
            if (slot.Active)
            {
                Assert.Equal(1, slot.CurrentAnimation);
                Assert.False(slot.Destroying);
            }
        }
    }

    [Fact]
    public void AChangeOfTargetAnimation_LoadsImageZeroTheSameTick_AndClearsTheDestroyFlag()
    {
        var bank = EffectGolden.Synthetic(new[]
        {
            EffectGolden.Anim("Loop", new[] { 5, 0 }),
            EffectGolden.Anim("Destroy", new[] { 2, 1 }, new[] { 2, 2 }),
        }, 3);
        var reservoir = Reservoir(bank);
        var slot = reservoir.Create(true, 0, 1, 0, 0, 0)!;

        reservoir.Tick(frozen: false);
        reservoir.Tick(frozen: false);
        reservoir.Tick(frozen: false);
        Assert.Equal(2, slot.CurrentImageSetIndex);
        reservoir.Tick(frozen: false);
        reservoir.Tick(frozen: false); // S = 4: the flag is set
        Assert.True(slot.Destroying);

        slot.TargetAnimation = 0;
        reservoir.Tick(frozen: false); // would have freed it; the flag is set, so it is freed first
        Assert.False(slot.Active);

        var second = reservoir.Create(true, 0, 1, 0, 0, 0)!;
        for (var tick = 0; tick < 3; tick++)
        {
            reservoir.Tick(frozen: false);
        }

        second.TargetAnimation = 0;
        reservoir.Tick(frozen: false);
        Assert.Equal((0, 0, false), (second.CurrentAnimation, second.CurrentImageSetIndex, second.Destroying));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // E19.g G3b (G3B-R2): the operations by record
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>Two records of one effect (a loop animation 0, a destroy animation 1 of two ticks), neither flagged for the load spawn.</summary>
    private static EffectReservoir TwoRecords(List<string>? warnings = null) => Reservoir(EffectGolden.Synthetic(new[]
    {
        EffectGolden.Anim("Loop", new[] { 5, 0 }),
        EffectGolden.Anim("Destroy", new[] { 1, 1 }, new[] { 1, 2 }),
    }, 3, EffectGolden.Record(0x80, 3, 4, 1, 0), EffectGolden.Record(0x80, 5, 6, 2, 0)), warnings);

    [Fact]
    public void FreeRecord_FreesEverySlotOfTheRecord_TheOneInItsDestroyTickToo_AndNoOtherRecord()
    {
        var reservoir = TwoRecords();
        var a = reservoir.Spawn(0)!;
        var b = reservoir.Spawn(1)!;
        var c = reservoir.Spawn(0)!;
        var dying = reservoir.Spawn(0)!;
        dying.TargetAnimation = 1;
        reservoir.Tick(frozen: false);
        reservoir.Tick(frozen: false);
        reservoir.Tick(frozen: false); // S = 2: the flag is set at the third update
        Assert.True(dying.Active && dying.Destroying);

        reservoir.FreeRecord(0);

        Assert.Equal((false, true, false, false), (a.Active, b.Active, c.Active, dying.Active));
        reservoir.FreeRecord(9); // outside the table: nothing
        Assert.Equal(1, reservoir.ActiveCount);
    }

    [Fact]
    public void SetAnimation_AimsEverySlotOfTheRecord_TheSwitchHappensAtTheNextUpdate()
    {
        var reservoir = TwoRecords();
        var a = reservoir.Spawn(0)!;
        var other = reservoir.Spawn(1)!;
        reservoir.Tick(frozen: false);

        reservoir.SetAnimation(0, 1);

        Assert.Equal((1, 0, 0), (a.TargetAnimation, a.CurrentAnimation, other.TargetAnimation));
        reservoir.Tick(frozen: false);
        Assert.Equal((1, 1), (a.CurrentAnimation, a.CurrentImageSetIndex));
        Assert.Equal(0, other.CurrentAnimation);
    }

    [Fact]
    public void SetAnimation_OutsideTheTable_WarnsOnce_AndLeavesTheSlot()
    {
        var warnings = new List<string>();
        var reservoir = TwoRecords(warnings);
        var a = reservoir.Spawn(0)!;

        reservoir.SetAnimation(0, 2); // the table holds two animations
        reservoir.SetAnimation(0, 7);

        Assert.Equal(0, a.TargetAnimation);
        Assert.Single(warnings);
        reservoir.Tick(frozen: false);
        Assert.True(a.Active);
        Assert.False(a.Destroying);
    }

    [Fact]
    public void PlaceRecord_PutsTheOperandsInTheHighWord_AndAddsOneToZ_OnEverySlotOfTheRecordOnly()
    {
        var reservoir = TwoRecords();
        var a = reservoir.Spawn(0)!;
        var b = reservoir.Spawn(0)!;
        var other = reservoir.Spawn(1)!;
        var before = (other.X, other.Y, other.Z);

        reservoir.PlaceRecord(0, 960, 160, 48);

        Assert.Equal((62914560, 10485760, 3145729), (a.X, a.Y, a.Z));
        Assert.Equal((a.X, a.Y, a.Z), (b.X, b.Y, b.Z));
        Assert.Equal(before, (other.X, other.Y, other.Z));
        reservoir.PlaceRecord(0, 0xFFFF, 0x8000, 0xFFFF); // the unsigned 16-bit operands wrap to 32 bits like the binary's shift
        Assert.Equal((-65536, int.MinValue, -65535), (a.X, a.Y, a.Z));
    }

    [Fact]
    public void AddPosition_AddsTheOperandsInTheHighWord_TwosComplement_NoPlusOne()
    {
        var reservoir = TwoRecords();
        var a = reservoir.Spawn(0)!;
        a.X = 468 << 16;
        a.Y = 376 << 16;
        a.Z = 64 << 16;

        reservoir.AddPosition(0, 0xFFB8, 8, 0); // -72, +8, 0

        Assert.Equal((396 << 16, 384 << 16, 64 << 16), (a.X, a.Y, a.Z));
    }

    [Fact]
    public void SetForces_TakesTheSignedOperandsShiftedBy8_AndTheSlotMovesByThemEachTick()
    {
        var reservoir = TwoRecords();
        var a = reservoir.Spawn(0)!;
        var b = reservoir.Spawn(1)!;
        reservoir.Tick(frozen: false);
        var origin = (a.X, a.Y, a.Z);

        reservoir.SetForces(0, 0xFFFF, 50, 0xFF00); // -1, 50, -256

        Assert.Equal((-256, 12800, -65536), (a.ForceX, a.ForceY, a.ForceZ));
        Assert.Equal((0, 0, 0), (b.ForceX, b.ForceY, b.ForceZ));
        reservoir.Tick(frozen: false);
        reservoir.Tick(frozen: false);
        Assert.Equal((origin.X - 512, origin.Y + 25600, origin.Z - 131072), (a.X, a.Y, a.Z));
    }

    [Fact]
    public void MoveRecordToEntity_PutsEverySlotAtTheEntityPlusTheOffsets_NoPlusOne()
    {
        var reservoir = TwoRecords();
        var a = reservoir.Spawn(0)!;
        var b = reservoir.Spawn(0)!;
        var other = reservoir.Spawn(1)!;
        var before = (other.X, other.Y, other.Z);

        reservoir.MoveRecordToEntity(0, (100 << 16) + 77, (60 << 16) + 5, (8 << 16) + 1, 0xFFFD, 2, 1); // d = (-3, 2, 1)

        Assert.Equal((97 << 16) + 77, a.X);
        Assert.Equal((62 << 16) + 5, a.Y);
        Assert.Equal((9 << 16) + 1, a.Z);
        Assert.Equal((a.X, a.Y, a.Z), (b.X, b.Y, b.Z));
        Assert.Equal(before, (other.X, other.Y, other.Z));
    }

    [Fact]
    public void SpawnPlaced_PlacesOnlyTheSlotItCreated_TheOthersOfTheRecordStay()
    {
        var reservoir = TwoRecords();
        var old = reservoir.Spawn(0)!;
        var before = (old.X, old.Y, old.Z);

        var created = reservoir.SpawnPlaced(0, 960, 160, 48);

        Assert.NotNull(created);
        Assert.NotSame(old, created);
        Assert.Equal((62914560, 10485760, 3145729), (created!.X, created.Y, created.Z));
        Assert.Equal(before, (old.X, old.Y, old.Z));
        Assert.Equal(0, created.TargetAnimation); // the animation stays the record's
        Assert.Null(reservoir.SpawnPlaced(9, 1, 2, 3)); // outside the table: nothing
        Assert.Equal(2, reservoir.ActiveCount);
    }

    [Fact]
    public void SpawnPlaced_OnAFullReservoir_CreatesNothing_AndMovesNothing()
    {
        var reservoir = TwoRecords();
        for (var i = 0; i < EffectReservoir.SlotCount; i++)
        {
            Assert.NotNull(reservoir.Spawn(0));
        }

        var before = reservoir.ActiveSlots.Select(s => (s.X, s.Y, s.Z)).ToArray();
        Assert.Null(reservoir.SpawnPlaced(0, 960, 160, 48));
        Assert.Null(reservoir.SpawnAtEntity(0, 1, 2, 3, 4, 5, 6));
        Assert.Equal(before, reservoir.ActiveSlots.Select(s => (s.X, s.Y, s.Z)).ToArray());
    }

    [Fact]
    public void SpawnAtEntity_PlacesOnlyTheNewSlot_AtTheEntityPlusTheOffsets_NoPlusOne()
    {
        var reservoir = TwoRecords();
        var old = reservoir.Spawn(1)!;
        var before = (old.X, old.Y, old.Z);

        var created = reservoir.SpawnAtEntity(1, 300 << 16, 200 << 16, 16 << 16, 0xFFFF, 0, 2);

        Assert.Equal((299 << 16, 200 << 16, 18 << 16), (created!.X, created.Y, created.Z));
        Assert.Equal(before, (old.X, old.Y, old.Z));
    }
}
