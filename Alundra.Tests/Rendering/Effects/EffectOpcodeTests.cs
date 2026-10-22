#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Rendering.Effects;
using Alundra.Transitions;
using Alundra.Hero;
using Alundra.Tests.Support;
using Alundra.Tests.Maps;
using Alundra.Tests.Rendering.Backdrops;

namespace Alundra.Tests.Rendering.Effects;

/// <summary>
/// E19.g G3b (docs/plan-e19-opcodes.md, section 1.2o.6, G3B-R1 to R3): the nine effect opcodes (<c>0x90</c> to <c>0x94</c>, <c>0xA0</c> to <c>0xA3</c>) through the real interpreter, on the
/// real operands of the real event programs of the export (read at their pcs), against <c>g3b-golden.tsv</c> (the binary's model, cross-checked with the real code). Each handler queues its
/// operation on the effect stage of the host; the stage applies it at the tick that emitted it. Written red on today's code, where the nine opcodes are skipped by their size.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class EffectOpcodeTests : IDisposable
{
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();
    private static readonly Dictionary<int, int[]> CodesOfMap = new();

    public EffectOpcodeTests()
    {
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    public void Dispose()
    {
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Montage
    // ---------------------------------------------------------------------------------------------------------------------

    private sealed class Host : IEntityWorldContext
    {
        public EffectStage? Stage { get; set; }
        public List<EntityScriptProxy> Spawned { get; } = new();
        public IReadOnlyList<EntityScriptProxy> SpawnedEntities => Spawned;
        public EntityScriptProxy? PlayerEntity { get; set; }
        public EntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public EntityScriptProxy? SpawnEntityByRecordId(EntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(EntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
        public EffectStage? EffectStage => Stage;
    }

    internal static int[] Codes(int map)
    {
        if (!CodesOfMap.TryGetValue(map, out var codes))
        {
            codes = MapEventProgramLoader.Load(Root, EffectGolden.WorldNameOf(Root, map)!)!.Codes;
            CodesOfMap[map] = codes;
        }

        return codes;
    }

    /// <summary>The instruction at <paramref name="pc"/> of the real program of the map: the opcode and its operands, as the export holds them.</summary>
    private static int[] Real(int map, int pc)
    {
        var codes = Codes(map);
        return codes.Skip(pc).Take(EventOpcodeSizeTable.Entries[(byte)codes[pc]].Size).ToArray();
    }

    private static EffectStage StageOf(int map, int heroTileX = 0, int heroTileY = 0)
    {
        var stage = new EffectStage();
        stage.Load(EffectGolden.BankOf(Root, map));
        stage.CaptureHeroTile(heroTileX, heroTileY);
        stage.SpawnLoadEffects();
        return stage;
    }

    /// <summary>Runs the instructions as one program on the host and proves the program reached its end (the <c>0x1A 9</c> after them ran in the same call: none of them suspends).</summary>
    private static List<EventTraceRecord> Exec(IEntityWorldContext host, EntityScriptProxy? entity, params int[][] instructions)
    {
        var codes = instructions.SelectMany(i => i).Concat(new[] { 0x1A, 9, 0xFF }).ToArray();
        var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = new int[6], Codes = codes };
        var runner = new EventProgramRunner(document, new GameState(), host);
        var trace = new List<EventTraceRecord>();
        runner.TraceSink = trace.Add;
        entity ??= new EntityScriptProxy { Status = EntityStatus.Normal, LogicContextEntity = new Entity { Name = "logic" } };
        entity.TargetAnimationId = 0;
        runner.RunOneScriptCall(entity, new EventProgramState { Codes = document.CodesAsBytes() });
        Assert.Equal(9u, entity.TargetAnimationId);
        return trace;
    }

    private static List<EventTraceRecord> Exec(IEntityWorldContext host, params int[][] instructions) => Exec(host, null, instructions);

    private static void Flush(EffectStage stage, int ticks = 1) => stage.RunFrame(ticks, new bool[ticks]);

    private static (int Index, int Record, int X, int Y, int Z)[] Snapshot(EffectStage stage) =>
        stage.Reservoir.ActiveSlots.Select(s => (s.Index, s.RecordIndex, s.X, s.Y, s.Z)).ToArray();

    private static (int Index, int Record, int X, int Y, int Z) Row(string[] r) =>
        ((int)long.Parse(r[1]), (int)long.Parse(r[2]), (int)long.Parse(r[3]), (int)long.Parse(r[4]), (int)long.Parse(r[5]));

    // ---------------------------------------------------------------------------------------------------------------------
    // The golden table and the real programs
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheGoldenSites_AreTheBytesOfTheRealPrograms_Ninety_Nine_OfThem()
    {
        var sites = OpcodeGolden.Of("OP").ToList();
        Assert.Equal(99, sites.Count);
        foreach (var site in sites)
        {
            var real = Real(int.Parse(site[1]), int.Parse(site[4]));
            var expected = new[] { Convert.ToInt32(site[5], 16) }.Concat(site[6].Split(',').Select(int.Parse)).ToArray();
            Assert.Equal(expected, real);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Map 476: the aura
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Map476_A2PlacesTheAuraRaw_AndTheSlotKeepsTheAnimationOfItsRecord_Then0x93LeavesItThere()
    {
        var stage = StageOf(476);
        Assert.Equal(0, stage.Reservoir.ActiveCount); // the record is not a load-time effect
        var host = new Host { Stage = stage };

        Exec(host, Real(476, 326));
        Flush(stage);

        var create = OpcodeGolden.One("S476", "create");
        var slot = Assert.Single(stage.Reservoir.ActiveSlots);
        Assert.Equal((0, 62914560, 10485760, 3145729, 1), (slot.RecordIndex, slot.X, slot.Y, slot.Z, slot.TargetAnimation));
        Assert.Equal(OpcodeGolden.Numbers(create, 2), new long[] { slot.RecordIndex, slot.X, slot.Y, slot.Z, slot.TargetAnimation });

        Exec(host, Real(476, 334));
        Flush(stage);
        var pos93 = OpcodeGolden.Numbers(OpcodeGolden.One("S476", "pos93"), 2);
        Assert.Equal(pos93, new long[] { slot.X, slot.Y, slot.Z });
        Assert.Equal(1, slot.Z & 0xFFFF); // the + 1 of 0x93 and 0xA2
    }

    [Fact]
    public void Map476_TheAura_IsDrawnAtTicks0To222_AndFreedAtTick223_AfterTheAnimation0()
    {
        var stage = StageOf(476);
        var host = new Host { Stage = stage };
        Exec(host, Real(476, 326), Real(476, 342)); // 0xA2 [0, 960, 160, 48] then 0x92 [0, 0]
        var last = int.Parse(OpcodeGolden.One("S476", "last_drawn")[2]);
        var freed = int.Parse(OpcodeGolden.One("S476", "freed")[2]);
        Assert.Equal((222, 223), (last, freed));

        var slot = (EffectSlot?)null;
        var lastActive = -1;
        var freedAt = -1;
        for (var tick = 0; tick < 300 && freedAt < 0; tick++)
        {
            Flush(stage);
            slot ??= stage.Reservoir.Slots[0];
            if (slot.Active)
            {
                lastActive = tick;
            }
            else
            {
                freedAt = tick;
            }
        }

        Assert.Equal((last, freed), (lastActive, freedAt));
        Assert.Equal(0, slot!.TargetAnimation);
    }

    [Theory]
    [InlineData(219)]
    [InlineData(220)]
    [InlineData(221)]
    [InlineData(222)]
    [InlineData(223)]
    [InlineData(224)]
    public void Map476_Animation1Asked_AfterNUpdates_RevivesTheAuraUpTo222_AndNotFrom223(int updates)
    {
        var expected = OpcodeGolden.Of("S476", "revive").Single(r => r[2] == updates.ToString())[3] == "2";
        var stage = StageOf(476);
        var host = new Host { Stage = stage };
        Exec(host, Real(476, 326), Real(476, 342));
        for (var i = 0; i < updates; i++)
        {
            Flush(stage);
        }

        Assert.Equal(0, stage.Reservoir.Slots[0].RecordIndex); // the aura was created in the slot 0, and a freed slot keeps its record index
        Assert.Equal(updates <= 223, stage.Reservoir.Slots[0].Active); // the aura is there until its destruction tick (222) has passed, and freed at the tick 223
        Exec(host, new[] { 0x92, 0, 1 });
        for (var i = 0; i < 60; i++)
        {
            Flush(stage);
        }

        Assert.Equal(expected, stage.Reservoir.Slots[0].Active);
        if (expected)
        {
            Assert.Equal(1, stage.Reservoir.Slots[0].CurrentAnimation);
            Assert.False(stage.Reservoir.Slots[0].Destroying);
        }
    }

    [Fact]
    public void Map476_TheTwelve0x92OfTheProgram2_AimTheAnimationsOfTheArcInOrder()
    {
        var stage = StageOf(476);
        var host = new Host { Stage = stage };
        Exec(host, Real(476, 326));
        Flush(stage);
        var expected = OpcodeGolden.One("S476", "anims")[2].Split(',').Select(int.Parse).ToArray();
        Assert.Equal(new[] { 0, 1, 3, 1, 3, 3, 1, 3, 3, 3, 1, 3 }, expected);

        var seen = new List<int>();
        foreach (var site in OpcodeGolden.Of("OP").Where(r => r[1] == "476" && r[3] == "2" && r[5] == "0x92").OrderBy(r => int.Parse(r[4])))
        {
            Exec(host, Real(476, int.Parse(site[4])));
            Flush(stage);
            seen.Add(stage.Reservoir.Slots[0].TargetAnimation);
        }

        Assert.Equal(expected, seen.ToArray());
    }

    [Fact]
    public void ASlotInItsDestroyTick_StillObeys0x93_AndIs0x91Freed()
    {
        var stage = StageOf(476);
        var host = new Host { Stage = stage };
        Exec(host, Real(476, 326), Real(476, 342));
        for (var i = 0; i < 223; i++)
        {
            Flush(stage); // the ticks 0 to 222: the destruction flag is set at 222, the slot is still there
        }

        var slot = stage.Reservoir.Slots[0];
        Assert.True(slot.Active && slot.Destroying);

        Exec(host, new[] { 0x93, 0, 1, 0, 2, 0, 3, 0 });
        stage.RunFrame(0, Array.Empty<bool>()); // the queue is applied without an update
        Assert.Equal((1 << 16, 2 << 16, (3 << 16) + 1), (slot.X, slot.Y, slot.Z));

        Exec(host, new[] { 0x91, 0 });
        stage.RunFrame(0, Array.Empty<bool>());
        Assert.False(slot.Active);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Map 391: the load slots, the five 0xA2 and the five 0x94
    // ---------------------------------------------------------------------------------------------------------------------

    private static readonly int[] SitesA2Of391 = { 228, 236, 244, 252, 408 };
    private static readonly int[] SitesF94Of391 = { 417, 425, 433, 441, 503 };

    [Fact]
    public void Map391_FirstInstant_TenSlots_TheLoadCopiesStayWhereTheyAre_TheFiveA2PlaceOnlyTheirOwn()
    {
        var stage = StageOf(391);
        var host = new Host { Stage = stage };
        foreach (var pc in SitesA2Of391)
        {
            Exec(host, Real(391, pc));
        }

        Flush(stage, 3);

        var expected = OpcodeGolden.Of("S391a").Select(Row).ToArray();
        Assert.Equal(10, expected.Length);
        Assert.Equal(expected, Snapshot(stage));
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 3, 4, 0, 1, 2 }, stage.Reservoir.ActiveSlots.Select(s => s.RecordIndex).ToArray());
        // the load copies are at (996, 504 + 96 k, 0) and (1020, 792, 0) px, as at map entry
        Assert.Equal(new[] { 65273856, 65273856, 65273856, 65273856, 66846720 }, stage.Reservoir.ActiveSlots.Take(5).Select(s => s.X).ToArray());
    }

    [Fact]
    public void Map391_SecondInstant_EachSlotMovesByTheForceOfItsRecordTimesN_TheLoadCopyIncluded()
    {
        var stage = StageOf(391);
        var host = new Host { Stage = stage };
        foreach (var pc in SitesA2Of391)
        {
            Exec(host, Real(391, pc));
        }

        foreach (var pc in SitesF94Of391)
        {
            Exec(host, Real(391, pc));
        }

        stage.RunFrame(0, Array.Empty<bool>()); // the operations are applied, no update
        var forces = OpcodeGolden.Of("S391f").Select(r => (Record: int.Parse(r[1]), F: OpcodeGolden.Numbers(r, 2))).ToList();
        Assert.Equal(5, forces.Count);
        foreach (var (record, f) in forces)
        {
            foreach (var slot in stage.Reservoir.ActiveSlots.Where(s => s.RecordIndex == record))
            {
                Assert.Equal(f, new long[] { slot.ForceX, slot.ForceY, slot.ForceZ });
            }
        }

        Assert.Equal(new long[] { 12800, 9216, 25600, 12800, 12800 }, forces.Select(f => f.F[1]).ToArray());

        var before = stage.Reservoir.ActiveSlots.ToDictionary(s => s.Index, s => (s.X, s.Y, s.Z));
        var n = int.Parse(OpcodeGolden.One("S391n", "100")[1]);
        Flush(stage, n); // N non-frozen ticks

        foreach (var row in OpcodeGolden.Of("S391b"))
        {
            var slot = stage.Reservoir.Slots[int.Parse(row[1])];
            Assert.Equal(int.Parse(row[2]), slot.RecordIndex);
            var b = before[slot.Index];
            Assert.Equal(new long[] { long.Parse(row[3]), long.Parse(row[4]), long.Parse(row[5]) }, new long[] { slot.X - b.X, slot.Y - b.Y, slot.Z - b.Z });
        }

        // the plan's own example, at N = 100: the slots 0 and 7 of the record 0 move by 1 280 000 in y, those of the record whose force is 100 by 2 560 000
        var move = (int i) => stage.Reservoir.Slots[i].Y - before[i].Y;
        Assert.Equal((1280000, 1280000, 921600, 2560000), (move(0), move(7), move(1), move(2)));
        foreach (var row in OpcodeGolden.Of("S391c"))
        {
            Assert.Equal(int.Parse(row[2]), stage.Reservoir.ActiveSlots.Count(s => s.RecordIndex == int.Parse(row[1])));
        }
    }

    [Fact]
    public void Map391_TheFourFirst0x94_GiveTheOracleOfTheBinary_OnTheLoadSlots()
    {
        var stage = StageOf(391);
        var host = new Host { Stage = stage };
        foreach (var pc in SitesF94Of391.Take(4))
        {
            Exec(host, Real(391, pc));
        }

        stage.RunFrame(0, Array.Empty<bool>());
        var before = stage.Reservoir.ActiveSlots.ToDictionary(s => s.Index, s => s.Y);
        Flush(stage, 100);

        var oracle = OpcodeGolden.One("S391x", "S1")[2].Split(',').Select(p => p.Split(':')).ToDictionary(p => int.Parse(p[0]), p => int.Parse(p[1]));
        Assert.Equal(oracle, stage.Reservoir.ActiveSlots.ToDictionary(s => s.Index, s => s.Y - before[s.Index]).Where(kv => oracle.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // 0x90, 0x91, the full reservoir
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Op0x90_SpawnsTheRecordAtItsPosition_AndARecordOutsideTheTableCreatesNothing()
    {
        var stage = StageOf(391);
        var host = new Host { Stage = stage };

        Exec(host, new[] { 0x90, 0 });
        Flush(stage);
        var record0 = OpcodeGolden.One("S90", "record0");
        var slot = stage.Reservoir.Slots[5];
        Assert.Equal(OpcodeGolden.Numbers(record0, 2), new long[] { slot.Index, slot.RecordIndex, slot.X, slot.Y, slot.Z, slot.TargetAnimation, slot.Mode });

        Exec(host, new[] { 0x90, 9 });
        Flush(stage);
        Assert.Equal(int.Parse(OpcodeGolden.One("S90", "outside")[2]), stage.Reservoir.ActiveCount);
    }

    [Fact]
    public void Op0x91_FreesBothSlotsOfARecordAtOnce_NotDrawn_AndARecordOutsideTheTableIsANoOp()
    {
        var stage = StageOf(391);
        var host = new Host { Stage = stage };
        foreach (var pc in SitesA2Of391)
        {
            Exec(host, Real(391, pc));
        }

        Flush(stage);
        Assert.Equal(10, stage.Reservoir.ActiveCount);

        Exec(host, new[] { 0x91, 3 });
        stage.RunFrame(0, Array.Empty<bool>()); // freed by the queue alone, before any update
        var freed = OpcodeGolden.One("S91", "freed");
        Assert.Equal(freed[2], string.Join(",", stage.Reservoir.Slots.Where(s => !s.Active).Where(s => s.Index is < 10).Select(s => s.Index)));
        Assert.Equal(int.Parse(freed[3]), stage.Reservoir.ActiveCount);

        Exec(host, new[] { 0x91, 7 });
        Flush(stage);
        Assert.Equal(8, stage.Reservoir.ActiveCount);
    }

    [Fact]
    public void TheFullReservoir_Takes128_AMoreCreationDoesNothing_And0x91FreesTheSlotThatTheNextOneTakes()
    {
        var stage = StageOf(391);
        var host = new Host { Stage = stage };
        Assert.Equal(5, stage.Reservoir.ActiveCount);
        for (var i = 0; i < 123; i++)
        {
            Exec(host, new[] { 0x90, 3 });
        }

        stage.RunFrame(0, Array.Empty<bool>());
        Assert.Equal(128, stage.Reservoir.ActiveCount);
        Exec(host, new[] { 0x90, 3 });
        stage.RunFrame(0, Array.Empty<bool>());
        Assert.Equal(128, stage.Reservoir.ActiveCount);

        Exec(host, new[] { 0x91, 4 }); // the record 4 has one slot: the load slot 4
        stage.RunFrame(0, Array.Empty<bool>());
        Assert.Equal(127, stage.Reservoir.ActiveCount);
        Assert.False(stage.Reservoir.Slots[4].Active);

        Exec(host, new[] { 0x90, 3 });
        stage.RunFrame(0, Array.Empty<bool>());
        Assert.True(stage.Reservoir.Slots[4].Active);
        Assert.Equal(3, stage.Reservoir.Slots[4].RecordIndex);
        var row = OpcodeGolden.One("S5", "next_slot");
        Assert.Equal((row[2], row[3]), (stage.Reservoir.Slots[4].Index.ToString(), stage.Reservoir.Slots[4].RecordIndex.ToString()));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Map 162, 135, 10
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Map162_SevenLoadSlotsPlusTheElevenA2_EighteenSlots_TheRecord5Holds12_EachAtItsPosition()
    {
        var stage = StageOf(162);
        Assert.Equal(7, stage.Reservoir.ActiveCount);
        var host = new Host { Stage = stage };
        var sites = OpcodeGolden.Of("OP").Where(r => r[1] == "162").OrderBy(r => int.Parse(r[4])).ToList();
        Assert.Equal(11, sites.Count);
        foreach (var site in sites)
        {
            Exec(host, Real(162, int.Parse(site[4])));
        }

        Flush(stage);
        Assert.Equal(18, stage.Reservoir.ActiveCount);
        Assert.Equal(OpcodeGolden.Of("S162").Select(Row).ToArray(), Snapshot(stage));
        foreach (var row in OpcodeGolden.Of("S162n"))
        {
            Assert.Equal(int.Parse(row[2]), stage.Reservoir.ActiveSlots.Count(s => s.RecordIndex == int.Parse(row[1])));
        }

        Assert.Equal(12, stage.Reservoir.ActiveSlots.Count(s => s.RecordIndex == 5));
    }

    [Fact]
    public void Map135_TheTwoA0_MoveTheAltarFrom468_376_To396_384_PixelsByTwosComplement_NoPlusOne()
    {
        var stage = StageOf(135);
        var host = new Host { Stage = stage };
        var altar = Assert.Single(stage.Reservoir.ActiveSlots);
        var load = OpcodeGolden.Numbers(OpcodeGolden.One("S135", "load"), 2);
        Assert.Equal(load, new long[] { altar.X, altar.Y, altar.Z });
        Assert.Equal((468, 376, 64), (altar.X >> 16, altar.Y >> 16, altar.Z >> 16));

        Exec(host, Real(135, 536)); // 0xA0 [0, 0, 8, 0]: y + 8
        stage.RunFrame(0, Array.Empty<bool>());
        Assert.Equal(OpcodeGolden.Numbers(OpcodeGolden.One("S135", "a0_1"), 2), new long[] { altar.X, altar.Y, altar.Z });
        Assert.Equal((468, 384), (altar.X >> 16, altar.Y >> 16));

        Exec(host, Real(135, 549)); // 0xA0 [0, 0xFFB8, 0, 0]: x - 72
        stage.RunFrame(0, Array.Empty<bool>());
        Assert.Equal(OpcodeGolden.Numbers(OpcodeGolden.One("S135", "a0_2"), 2), new long[] { altar.X, altar.Y, altar.Z });
        Assert.Equal((396, 384, 64), (altar.X >> 16, altar.Y >> 16, altar.Z >> 16));
        Assert.Equal(0, altar.Z & 0xFFFF); // no + 1
    }

    [Fact]
    public void Map135_TheA3_SpawnsAtTheEntityWhenOneIsFound_AndNothingWhenNone()
    {
        var found = OpcodeGolden.One("S135a3", "found");
        var stage = StageOf(135);
        var host = new Host { Stage = stage };
        var entity = new EntityScriptProxy
        {
            Status = EntityStatus.Normal, LogicContextEntity = new Entity { Name = "logic" },
            PosX = (int)long.Parse(found[4]), PosY = (int)long.Parse(found[5]), PosZ = (int)long.Parse(found[6]),
        };

        Exec(host, entity, Real(135, 2490)); // 0xA3 [1, 0x80, 0, 0, 0, 0, 0, 0]: the entity itself
        Flush(stage);

        var created = stage.Reservoir.Slots[int.Parse(found[7])];
        Assert.True(created.Active);
        Assert.Equal((int.Parse(found[2]), long.Parse(found[8]), long.Parse(found[9]), long.Parse(found[10])), (created.RecordIndex, (long)created.X, (long)created.Y, (long)created.Z));
        Assert.Equal((found[11] == "1", int.Parse(found[12]), int.Parse(found[13])), (created.TargetMapTable, created.TargetEffect, created.TargetAnimation)); // the global effect 13

        var none = OpcodeGolden.One("S135a3", "none");
        var stage2 = StageOf(135);
        var host2 = new Host { Stage = stage2 };
        Exec(host2, new[] { 0xA3, 1, 0x81, 0, 0, 0, 0, 0, 0 }); // the hero is not there: nothing is searched, nothing is created
        Flush(stage2);
        Assert.Equal((int.Parse(none[2]), int.Parse(none[3])), (1, stage2.Reservoir.ActiveCount));
    }

    [Fact]
    public void Map10_TheRollingRock_0x90Then0x92_IsDrawnAtTicks0To154_AndFreedAtTick155_TheEntrySlotStays()
    {
        var stage = StageOf(10);
        var host = new Host { Stage = stage };
        var entry = OpcodeGolden.Numbers(OpcodeGolden.One("S10", "entry"), 2);
        var entrySlot = Assert.Single(stage.Reservoir.ActiveSlots);
        Assert.Equal(entry, new long[] { entrySlot.Index, entrySlot.RecordIndex, entrySlot.X, entrySlot.Y, entrySlot.Z });
        Assert.Equal(4, entrySlot.RecordIndex);

        Exec(host, Real(10, 2828), Real(10, 2864)); // 0x90 [0] then 0x92 [0, 1]
        var rock = (EffectSlot?)null;
        var lastActive = -1;
        var freedAt = -1;
        for (var tick = 0; tick < 400 && freedAt < 0; tick++)
        {
            Flush(stage);
            if (tick == 0)
            {
                rock = stage.Reservoir.Slots[1];
                var row = OpcodeGolden.Numbers(OpcodeGolden.One("S10", "rock"), 2);
                Assert.Equal(row.Take(2).Concat(row.Skip(3)).ToArray(), new long[] { rock.Index, rock.RecordIndex, rock.X, rock.Y, rock.Z });
                Assert.Equal(int.Parse(OpcodeGolden.One("S10", "rock92")[2]), rock.TargetAnimation);
            }

            if (rock!.Active)
            {
                lastActive = tick;
            }
            else
            {
                freedAt = tick;
            }
        }

        Assert.Equal((int.Parse(OpcodeGolden.One("S10", "last_drawn")[2]), int.Parse(OpcodeGolden.One("S10", "freed")[2])), (lastActive, freedAt));
        Assert.True(entrySlot.Active);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // 0xA1, 0x92 defensive rule, no host
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void OpA1_MovesEverySlotOfTheRecordToTheEntityPlusTheOffsets_NoPlusOne_AndDoesNothingWithoutEntity()
    {
        var none = OpcodeGolden.One("SA1", "none");
        var withEntity = OpcodeGolden.One("SA1", "entity");
        var stage = StageOf(391);
        var host = new Host { Stage = stage };
        Exec(host, Real(391, 228), Real(391, 244)); // the records 3 then 0: the record 0 now has two slots
        Flush(stage);
        var op = new[] { 0xA1, 0, 0x81, 0xFD, 0xFF, 2, 0, 1, 0 }; // record 0, the hero, d = (-3, 2, 1) px

        Exec(host, op); // no hero: nothing found
        stage.RunFrame(0, Array.Empty<bool>());
        Assert.Equal(none[2], string.Join(",", stage.Reservoir.ActiveSlots.Where(s => s.RecordIndex == 0).SelectMany(s => new long[] { s.X, s.Y, s.Z })));

        host.PlayerEntity = new EntityScriptProxy { Status = EntityStatus.Normal, PosX = (int)long.Parse(withEntity[2]), PosY = (int)long.Parse(withEntity[3]), PosZ = (int)long.Parse(withEntity[4]) };
        Exec(host, op);
        stage.RunFrame(0, Array.Empty<bool>());
        Assert.Equal(withEntity[5], string.Join(",", stage.Reservoir.ActiveSlots.Where(s => s.RecordIndex == 0).SelectMany(s => new long[] { s.Index, s.X, s.Y, s.Z })));
        Assert.All(stage.Reservoir.ActiveSlots.Where(s => s.RecordIndex != 0), s => Assert.NotEqual(stage.Reservoir.Slots[0].X, s.X));
    }

    [Fact]
    public void Op0x92_WithAnAnimationOutsideTheTable_WarnsOnce_AndIgnoresTheOperation()
    {
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();
        var stage = StageOf(391);
        var host = new Host { Stage = stage };

        Exec(host, new[] { 0x92, 0, 200 });
        Exec(host, new[] { 0x92, 0, 201 });
        Flush(stage);

        Assert.Equal(0, stage.Reservoir.Slots[0].TargetAnimation);
        Assert.True(stage.Reservoir.Slots[0].Active);
        Assert.Equal(1, log.Warnings.Count(w => w.Contains("EffectReservoir")));
    }

    [Theory]
    [InlineData(0x90)]
    [InlineData(0x91)]
    [InlineData(0x92)]
    [InlineData(0x93)]
    [InlineData(0x94)]
    [InlineData(0xA0)]
    [InlineData(0xA1)]
    [InlineData(0xA2)]
    [InlineData(0xA3)]
    public void WithoutAnEffectStage_EachOpcodeDegrades_AndTheProgramGoesOnBySize(int opcode)
    {
        var size = EventOpcodeSizeTable.Entries[(byte)opcode].Size;
        var instruction = new int[size];
        instruction[0] = opcode;

        var trace = Exec(new Host(), instruction);

        Assert.Contains(trace, t => t.Kind == EventTraceKind.Degraded && t.CodeIndex == 0);
        Assert.DoesNotContain(trace, t => t.Kind == EventTraceKind.UnknownSkipped);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The queue (G3B-R3)
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnOperationMarkedTick1_OfAFrameOfTwoTicks_GivesTheStateOfTwoFramesOfOneTick()
    {
        var two = StageOf(391);
        var one = StageOf(391);
        var hostTwo = new Host { Stage = two };
        var hostOne = new Host { Stage = one };

        Flush(one); // the tick 0
        two.BeginSource(1, 1); // an entity's operation at the tick 1 of a frame of two ticks
        Exec(hostTwo, Real(391, 228), Real(391, 417));
        two.EndSource();
        Exec(hostOne, Real(391, 228), Real(391, 417)); // the same operations, in the second frame of one tick
        Flush(one);
        two.RunFrame(2, new[] { false, false });

        Assert.Equal(Snapshot(one), Snapshot(two));
        Assert.Equal(6, two.Reservoir.ActiveCount);
        Assert.Equal(one.Reservoir.ActiveSlots.Select(s => s.CurrentImageSetIndex).ToArray(), two.Reservoir.ActiveSlots.Select(s => s.CurrentImageSetIndex).ToArray());
    }

    [Fact]
    public void TheOperationsOfTheMapEventsComeBeforeThoseOfTheEntities_AtTheSameTick_WhateverTheOrderOfEmission()
    {
        var stage = StageOf(391);
        var host = new Host { Stage = stage };

        stage.BeginSource(0, 1);
        Exec(host, new[] { 0x90, 1 }); // an entity: emitted first
        stage.EndSource();
        stage.BeginSource(0, 0);
        Exec(host, new[] { 0x90, 2 }); // the map events: emitted second, applied first
        stage.EndSource();
        Flush(stage);

        Assert.Equal(new[] { 2, 1 }, stage.Reservoir.ActiveSlots.Skip(5).Select(s => s.RecordIndex).ToArray());
    }

    [Fact]
    public void A0xA2_AndABoxThatPosts0x40AtTheSameTick_TheSlotExistsFrozenAndReadable_NotAnimated()
    {
        var stage = StageOf(391);
        var host = new Host { Stage = stage };
        Exec(host, Real(391, 228));

        stage.RunFrame(1, new[] { true }); // the gate read after the events of the tick: 0x40 posted by a box

        var created = stage.Reservoir.Slots[5];
        Assert.True(created.Active);
        Assert.Equal((22020096, 45613056, 5242881), (created.X, created.Y, created.Z));
        Assert.Equal(-1, created.CurrentImageSetIndex); // the animation did not advance
        Assert.Equal(3, created.RecordIndex);
    }

    [Fact]
    public void A0x90_AtTheTickThatStartsAWarpDeparture_TheSlotExistsDuringTheDeparture_AndTheQueueIsEmptyAfterEachUpdate()
    {
        var (world, _) = WorldProxyGlobalFreezeTests.BuildRealMap389World();
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);
        BackdropWarpDepartureSupport.SetPrivateField(proxy._backdropStage, "_clearColorApplied", true); // the headless montage has no view for the clear colour.
        var stage = proxy._effectStage;
        stage.Load(EffectGolden.Synthetic(new[] { EffectGolden.Anim("Loop", new[] { 1, 0 }, new[] { 1, 1 }) }, 2, EffectGolden.Record(0x80, 1, 1, 0, 0)));
        Assert.Same(stage, ((IEntityWorldContext)proxy).EffectStage);
        proxy.Update(0.02f); // priming frame

        var player = BackdropWarpDepartureSupport.BeginDeparture(proxy);
        Exec(new HostOf(proxy), new[] { 0x90, 0 });
        for (var frame = 0; frame < 5; frame++)
        {
            proxy.Update(0.02f);
            Assert.True(WarpDirector.Instance.IsTransitionInProgress, $"F{frame}");
            var slot = Assert.Single(stage.Reservoir.ActiveSlots); // created once, never again: the queue is empty after the Update
            Assert.Equal(-1, slot.CurrentImageSetIndex); // no tick during the departure
        }

        GC.KeepAlive(player);
    }

    /// <summary>A script host that is also the effect host: its runner emits one operation per script call (the calls are the ticks of the entity's own loop).</summary>
    private sealed class EntityLoopHost : IScriptHost, IEntityWorldContext, IEventProgramRunner
    {
        public int Ticks;
        public EffectStage? Stage { get; set; }
        public int Calls;
        public IEventProgramRunner Runner => this;
        public EntityScriptProxy? ActiveCollisionEntity { get; set; }
        public GameState GameState { get; } = new();
        public HeroController? PlayerController => null;
        public IReadOnlyList<EntityScriptProxy> Collidables { get; } = Array.Empty<EntityScriptProxy>();
        public void DestroyEntity(EntityScriptProxy entity, int effectId) { }
        public int LogicTicksThisFrame(float elapsedTime) => Ticks;
        public IReadOnlyList<EntityScriptProxy> SpawnedEntities { get; } = Array.Empty<EntityScriptProxy>();
        public EntityScriptProxy? PlayerEntity => null;
        public EntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public EntityScriptProxy? SpawnEntityByRecordId(EntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(EntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
        public EffectStage? EffectStage => Stage;

        public void RunScript(EntityScriptProxy entity, int programSlot)
        {
            if (programSlot == ScriptHelper.ProgramCTick)
            {
                var x = ++Calls;
                Stage!.Emit(reservoir => reservoir.Create(true, 0, 0, x << 16, 0, 0));
            }
        }

        public void RunSpriteEvent(EntityScriptProxy entity)
        {
        }
    }

    [Fact]
    public void TheEntityLoop_MarksTheEffectOperationsWithTheTickOfItsScript_AFrameOfTwoTicksAppliesTheSecondAfterTheFirstUpdate()
    {
        var stage = new EffectStage();
        stage.Load(EffectGolden.Synthetic(new[] { EffectGolden.Anim("Loop", new[] { 1, 0 }, new[] { 1, 1 }) }, 2)); // image sets 0 then 1, one tick each, forever
        var host = new EntityLoopHost { Stage = stage, Ticks = 2 };
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;
        typeof(Microsoft.Xna.Framework.Game).GetField("_components", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());
        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);
        var owner = new Entity { Name = "e", GameplayProxyClassName = typeof(EntityScriptProxy).FullName!, RootComponent = new AnimatedSpriteComponent() };
        HeroWorldFixture.SetProperty(owner, nameof(Entity.World), world);
        owner.Initialize();
        var entity = Assert.IsType<EntityScriptProxy>(owner.GameplayProxy);
        entity.ScriptHost = host;
        entity.Status = EntityStatus.Normal;
        entity.ProgramIndexes[ScriptHelper.ProgramCTick] = 1;

        owner.Update(0.04f); // the script runs at the tick 0 and at the tick 1 of the frame: two operations, marked 0 and 1
        stage.RunFrame(2, new[] { false, false });

        Assert.Equal(2, host.Calls);
        var created = stage.Reservoir.ActiveSlots.ToList();
        Assert.Equal(new[] { 1 << 16, 2 << 16 }, created.Select(s => s.X).ToArray());
        Assert.Equal(new[] { 1, 0 }, created.Select(s => s.CurrentImageSetIndex).ToArray()); // two updates for the first, one for the second (its tick is 1)

        stage.RunFrame(1, new[] { false }); // nothing is left to apply
        Assert.Equal(2, stage.Reservoir.ActiveCount);
    }

    /// <summary>The world proxy itself as the host of the interpreter: the runner reads its <see cref="IEntityWorldContext.EffectStage"/>.</summary>
    private sealed class HostOf : IEntityWorldContext
    {
        private readonly WorldProxy _proxy;

        public HostOf(WorldProxy proxy) => _proxy = proxy;

        public IReadOnlyList<EntityScriptProxy> SpawnedEntities => ((IEntityWorldContext)_proxy).SpawnedEntities;
        public EntityScriptProxy? PlayerEntity => ((IEntityWorldContext)_proxy).PlayerEntity;
        public EntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public EntityScriptProxy? SpawnEntityByRecordId(EntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(EntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
        public EffectStage? EffectStage => ((IEntityWorldContext)_proxy).EffectStage;
    }
}
