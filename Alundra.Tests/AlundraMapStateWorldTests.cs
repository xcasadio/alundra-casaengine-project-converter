#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using Alundra.Maps;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Physics;

namespace Alundra.Tests;

/// <summary>
/// E20.e0k1 (docs/plan-e20-heros.md section 1.18.1, T3, phase B): the state that reads the map reaches an entity born by <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/>, in the real world of map 14 (<see cref="ArcRun"/>,
/// real prefabs and controllers) over a synthetic field of cells, against the oracle of <c>docs/plan-e20-annexe/e20-e0k/oracle/out</c> (the numbers are read by key, never copied). Each positive case is set next to its "no map"
/// witness in the same assertion. (k1) an arrow against a wall that ONLY the walkability mask blocks; (k2) a ball falls and lands; (k3) the same ball on the gravity of maps 159 and 160 barely falls; (k4) the fields of the entity
/// born by the interface are the ones of an entity of the same sprite created from a record.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraMapStateWorldTests
{
    private const int Arrow = 3;
    private const int FireBall = 11;
    private const int WallColumn = 15;

    private static ArcRun Arc(string name) => new(new ArcSpec("E20e0k1-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    private static TileMapData Map(int gravity, int viscosity)
    {
        var map = new TileMapData();
        map.CustomProperties["Gravity"] = gravity.ToString();
        map.CustomProperties["ZViscosity"] = viscosity.ToString();
        return map;
    }

    private static void Install(ArcRun arc, AlundraCellsCollisionField field, TileMapData? map)
    {
        typeof(AlundraWorldProxy).GetProperty(nameof(AlundraWorldProxy.CollisionField))!.SetValue(arc.Proxy, field);
        arc.RealWorld!.CollisionField = field;
        typeof(AlundraWorldProxy).GetField("_tileMapData", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(arc.Proxy, map);
    }

    /// <summary>A wall that only the mask blocks: the cells of column 15 hold the word 0x40 and the height 0 (the family C1 of the oracle: the arrow flies at 16 px above the ground).</summary>
    private static AlundraCellsCollisionField MaskWall() => FlatCells.Create(cell: (x, y) => (x == WallColumn ? 0x40 : 0, 0));

    private sealed record Flight(IReadOnlyList<int> X, IReadOnlyList<int> Z, IReadOnlyList<int> Ground, int FirstFlagTick, int FirstGroundTick);

    private static Flight Fly(int sprite, int xPixels, int zPixels, AlundraCellsCollisionField field, TileMapData? map, int ticks)
    {
        using var arc = Arc("fly");
        Install(arc, field, map);
        var proxy = ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, false, sprite, xPixels << 16, 100 << 16, zPixels << 16, 0x18)!;
        Assert.NotNull(proxy);
        var x = new List<int>();
        var z = new List<int>();
        var ground = new List<int>();
        var flag = 0;
        for (var t = 1; t <= ticks; t++)
        {
            arc.OneFrame();
            x.Add(proxy.PosX);
            z.Add(proxy.PosZ);
            ground.Add(proxy.IsOnGround);
            if (flag == 0 && proxy.ForceAdjusted != 0)
            {
                flag = t;
            }
        }

        return new Flight(x, z, ground, flag, ground.Select((g, i) => (g, i)).Where(p => p.g != 0).Select(p => p.i + 1).DefaultIfEmpty(0).First());
    }

    private static JsonElement OracleRows(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-e0k", "oracle", "out", file);
            if (File.Exists(candidate))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(candidate));
                return document.RootElement.Clone();
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-e0k/oracle/out/{file}' found above '{AppContext.BaseDirectory}'");
    }

    private static IReadOnlyList<JsonElement> OracleSlot(JsonElement root)
    {
        var slot = root.GetProperty("meta").GetProperty("slot").GetInt32().ToString();
        return root.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("slots").GetProperty(slot)).ToList();
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (k1) the arrow against a wall that only the mask blocks.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void K1_AnArrowBornWithTheMap_StopsAtTheWallOfTheMaskAtTheXOfTheOracle_AndTheSameArrowInAFreeCorridorFliesTheWholeDistance()
    {
        const int ticks = 60;
        var map = Map(128, 4096);

        // The oracle (arrows2-summary, C1): the arrow born at x = 100 stops at X = 349 exactly (22 872 064 raw), the wall column 15 being at 360. The free corridor has no wall column: same field otherwise.
        var wallX = OracleSlot(OracleRows("arrows-arrow-wall-xy.json")).Select(s => s.GetProperty("px").GetInt32()).Max();
        var withWall = Fly(Arrow, 100, 16, MaskWall(), map, ticks);
        var free = Fly(Arrow, 100, 16, FlatCells.Create(), map, ticks);

        // The free arrow: 3 px at the first tick, then 6 px per tick (the oracle's force column), so it is far past the wall at tick 60.
        var freeExpected = (100 + 3 + 6 * (ticks - 1)) << 16;
        Assert.Equal((wallX, true, freeExpected), (withWall.X.Max(), withWall.FirstFlagTick > 0, free.X.Max()));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (k2) the ball born at Z + 32 px falls and lands; the witness without a map stays in the air.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void K2_ABallBornWithTheMap_FallsAndLandsAtTheTickOfTheOracle_AndTheSameBallWithoutAMapStaysInTheAir()
    {
        const int ticks = 16;
        var oracleRoot = OracleRows("arrows-ball-fire.json");
        var oracle = OracleSlot(oracleRoot);
        var landingTick = oracle.Select((s, i) => (s, i)).First(p => p.s.GetProperty("ground_144").GetInt32() != 0).i + 1;
        var restZ = oracle[landingTick - 1].GetProperty("pz").GetInt32() - 1; // the DLL's frame is the binary's minus one raw unit (ADR-0026).

        // E20.e0k2 (D-E0K-10, derivation): the witness without a map stays in the air at the Z it was born at, and since the logical Z (HasLogicalZ) keeps the raw unit of the birth instead of the root's pixel, that Z is
        // the oracle's initial Z (the binary's Z_arg - offZ + 1) minus one raw unit (ADR-0026), not the whole pixel 32 << 16 the root used to give back.
        var bornZ = oracleRoot.GetProperty("meta").GetProperty("init").GetProperty("pz").GetInt32() - 1;

        var with = Fly(FireBall, 100, 32, FlatCells.Create(), Map(128, 4096), ticks);
        var without = Fly(FireBall, 100, 32, FlatCells.Create(), null, ticks);

        Assert.Equal((landingTick, restZ, bornZ, 0), (with.FirstGroundTick, with.Z[landingTick - 1], without.Z[^1], without.Ground.Max()));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (k3) the gravity of maps 159 and 160 (3, 256) against the one of map 14 (128, 4096).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void K3_OnMap159_TheBallBarelyFalls_WhereOnMap14_ItHasLandedAtTheSameTick()
    {
        const int ticks = 11;
        var map14 = Fly(FireBall, 100, 32, FlatCells.Create(), Map(128, 4096), ticks);
        var map159 = Fly(FireBall, 100, 32, FlatCells.Create(), Map(3, 256), ticks);

        // The force of tick t is -(t * G << 8) raw: at G = 3 the fall of eleven ticks is at most the sum of those forces (less than a pixel); at G = 128 the ball is on the ground.
        var farthest159 = Enumerable.Range(1, ticks).Sum(t => t * 3 * 256);
        var drop159 = (32 << 16) - map159.Z[^1];
        Assert.Equal((0, true, true), (map14.Z[^1], drop159 > 0 && drop159 <= farthest159 + 65536, map159.Ground.Max() == 0));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (k4) the fields posed on the entity born by the interface are the ones of an entity of the same sprite created from a record.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void K4_TheFieldsOfTheEntityBornByTheInterface_AreTheOnesOfAnEntityOfTheSameSpriteCreatedFromARecord()
    {
        var map = Map(128, 4096);
        using var arc = Arc("k4");
        Install(arc, FlatCells.Create(), map);
        var born = ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, false, Arrow, 100 << 16, 100 << 16, 16 << 16, 0x18)!;

        var catalog = new SpriteRecordCatalog(ProjectRootFinder.Find());
        Assert.True(catalog.TryGetPrefab(false, Arrow, out var prefabId));
        var record = new TileMapObjectData { Id = 0, Name = "Entity_0" };
        record.CustomProperties["Index"] = "0";
        record.CustomProperties["XPos"] = "100";
        record.CustomProperties["YPos"] = "100";
        record.CustomProperties["Height"] = "16";
        record.CustomProperties["SpriteTableIndex"] = Arrow.ToString();
        record.CustomProperties["SpriteDirection"] = "64";
        record.CustomProperties["PrefabAssetId"] = prefabId.ToString();
        var prefab = new Entity { Name = "K4Prefab", GameplayProxyClassName = typeof(AlundraEntityScriptProxy).FullName! };
        prefab.AddComponent(new CharacterControllerComponent());
        var fromRecord = (AlundraEntityScriptProxy)AlundraEntitySpawnFactory.CreateEntityFromRecord(record, _ => prefab, catalog, tileMapData: map).GameplayProxy!;

        static (int, int, uint, bool, float) Fields(AlundraEntityScriptProxy p)
            => (p.MapGravityRaw, p.MapZViscosityRaw, p.Controller!.Settings.WalkabilityMask, p.Controller.IsVerticalOwnedExternally, p.Controller.Settings.MinMoveDistance);

        Assert.Equal((128, 4096, 0x40u, true, 0f), Fields(fromRecord)); // the record path, unchanged.
        Assert.Equal(Fields(fromRecord), Fields(born));                 // the interface: the same fields.
    }
}
