#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.h1b1 (docs/plan-e19-opcodes.md §1.2n.1b, D-E19-59, ADR-0026): the absolute writes of Z (the spawn, 0x8A, 0x64) follow the DLL
/// convention (binary - 1), the spawn floors <c>PosZ</c> at the terrain height <c>T</c> when it is given a collision field, and the
/// spawn support without reach is gone from production. Every expected value is written by hand before the code: a value the
/// measure contradicts is a stop, never a re-pin.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraAbsoluteZWritesTests
{
    private const int C = ScriptHelper.ProgramCTick;

    // ---------------------------------------------------------------------------------------------------------------------------
    // The platform of maps 83 and 410 (record 22): 0x64 @1235, 0x1B [0,1] (+1 px per tick), 0x21 [32,0] @1246.
    // ---------------------------------------------------------------------------------------------------------------------------

    /// <summary>The call of the program (1 = the call that runs 0x64) in which <c>0x1B @1249</c>, the instruction after the wait 0x21 @1246,
    /// first runs, and the platform's <c>PosZ</c> right after it.</summary>
    private static (int Call, int PosZ) FirstClimbEnd(string zone, string world, int record)
    {
        using var arc = new ArcRun(new ArcSpec(
            "H1b1-" + world, zone, world, new[] { 764 }, 18, 11, 1, 200, RealController: true, Prefabs: true));
        var call = 0;
        var lastFrame = -1;
        var found = (Call: -1, PosZ: -1);
        arc.OnInstruction = t =>
        {
            if (t.Slot != C || t.ProgramStart != 1232)
            {
                return;
            }

            if (t.Frame != lastFrame)
            {
                call++;
                lastFrame = t.Frame;
            }

            if (t.Pc == 1249 && found.Call < 0)
            {
                found = (call, arc.EntityByRecord(record)!.PosZ);
            }
        };

        arc.RunUntil(() => found.Call >= 0, "the floating platform ends its first climb (0x21 @1246 passes)");
        return found;
    }

    [Fact]
    public void Map83_TheFloatingPlatform_EndsItsFirstClimbAtThe33rdCall_At48Pixels()
    {
        // Binary: 0x64 sets PosZ to 16 px + 1, +1 px per tick, the wait of 32 px passes at the 33rd call (the call of 0x64 is the 1st), at 48 px + 1.
        // In the DLL convention (binary - 1) that is PosZ = 48 px = 3145728. Before H1b1: the 34th call, 3211264 (49 px).
        var (call, posZ) = FirstClimbEnd("Lizardman's Lair", "Lizardman's Lair-83", 22);

        Assert.Equal(33, call);
        Assert.Equal(3145728, posZ);
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // The piles of crates: guards, green before and after (a crate spawned on another stands on it, whatever the convention).
    // ---------------------------------------------------------------------------------------------------------------------------

    private static void AssertTheCratesStayStacked(
        string zone, string world, int tileX, int tileY, int lowRecord, int highRecord, int lowPosZ, int highPosZ)
    {
        using var arc = new ArcRun(new ArcSpec(
            "H1b1-" + world, zone, world, Array.Empty<int>(), tileX, tileY, 1, 200, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((tileX * 24 + 12) << 16, (tileY * 16 + 8) << 16, 1 << 20, AlundraGameState.ResetAnimationId, 0)));
        // The map-load spawn zone is tested against the hero's tile AT the load: the arrival puts him in the zone of the crates.
        for (var i = 0; i < 60; i++)
        {
            arc.OneFrame();
        }

        var low = arc.EntityByRecord(lowRecord);
        var high = arc.EntityByRecord(highRecord);
        Assert.NotNull(low);
        Assert.NotNull(high);
        Assert.Equal((lowPosZ, highPosZ), (low!.PosZ, high!.PosZ));
        Assert.Equal(low.PosZ + low.ModZ + low.Depth + 1, high.PosZ + high.ModZ);
    }

    [Fact]
    public void Map390_TheCrateOfRecord6_StaysOnTheCrateOfRecord4()
        => AssertTheCratesStayStacked("The Klark", "Ship Klark (inner)-390", 10, 30, 4, 6, 4194304, 5242880);

    [Fact]
    public void Map163_TheCrateOfRecord15_StaysOnTheCrateOfRecord14()
        => AssertTheCratesStayStacked("Inoa", "Inoa (inner)-163", 30, 40, 14, 15, 2097152, 3145728);

    [Fact]
    public void Map179_TheCrateOfRecord12_StaysOnTheCrateOfRecord11()
        => AssertTheCratesStayStacked("Inoa", "Inoa (inner)-179", 40, 10, 11, 12, 2097152, 3145728);

    // ---------------------------------------------------------------------------------------------------------------------------
    // The terrain floor of the spawn, on the production path (SpawnEntityByRecordId with a real field).
    // ---------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void SpawnEntityByRecordId_AnEntityAuthoredUnderTheTerrain_IsRaisedToTheTerrainHeight()
    {
        // A flat terrain of 16 px (height 1 = 16 px), a record at z 0 with ModZ 0 and a box of 24 x 16 inside the first cell: the binary floors the
        // spawn at T + 1, the DLL at T = 1048576. Before H1b1 no floor was applied: 0 - 0 + 1.
        var prefabId = Guid.Parse("fd375feb-2f77-447e-aedb-c3fa44c64edd");
        var record = new TileMapObjectData { Id = 0, Name = "Entity_0" };
        record.CustomProperties["Index"] = "0";
        record.CustomProperties["XPos"] = "0";
        record.CustomProperties["YPos"] = "0";
        record.CustomProperties["Height"] = "0";
        record.CustomProperties["SpriteDirection"] = "64";
        record.CustomProperties["PrefabAssetId"] = prefabId.ToString();

        var tileMapData = new TileMapData();
        tileMapData.MapSize = new CasaEngine.Core.Math.Size(2, 2);
        tileMapData.CustomProperties["AlundraCells"] =
            "{\"map_index\":1,\"cell_count\":4,\"walkability\":[0,0,0,0],\"ground_property\":[0,0,0,0],"
            + "\"slope\":[0,0,0,0],\"height\":[1,1,1,1]}";
        Assert.True(AlundraCellsCollisionField.TryCreate(tileMapData, "map_1", out var field));

        var header = new SpriteRecordHeader
        {
            MoreFlags = 128, OffsetX = -12, OffsetY = -8, OffsetZ = 0, SizeX = 24, SizeY = 16, SizeZ = 32,
        };
        var proxy = new AlundraWorldProxy { SpriteRecordCatalog = new FakeSpriteRecordCatalog().Add(prefabId, header) };
        SetPrivate(proxy, "_world", new World());
        ((Dictionary<int, TileMapObjectData>)GetPrivate(proxy, "_entityRecordsByIndex"))[0] = record;
        typeof(AlundraWorldProxy).GetProperty(nameof(AlundraWorldProxy.CollisionField))!.SetValue(proxy, field);

        var spawned = proxy.SpawnEntityByRecordId(new AlundraEntityScriptProxy(), 0);

        Assert.NotNull(spawned);
        Assert.Equal(0, spawned!.ModZ);
        Assert.Equal(1048576, spawned.PosZ);
    }

    private static object GetPrivate(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void SetPrivate(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
