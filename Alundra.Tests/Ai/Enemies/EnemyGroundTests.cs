#nullable enable
using System;
using System.IO;
using System.Linq;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;
using Xunit;
using Alundra.Physics;
using Alundra.Entities;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Enemies;

/// <summary>
/// E14.a0b T2 (docs/plan-e14-ia-native.md section 1.2): the floor (<c>FloorHeight</c>) and the four corner heights (<c>MapHeights</c>) of the entities other than the hero
/// (<see cref="EntityScriptProxy.UpdateEnemyGroundHeights(CasaEngine.Engine.Physics.ICollisionField?)"/>), against the real code of <c>ALUN_CD.EXE</c> (France): <c>docs/plan-e14-annexe/e14a/oracle/out/h2/map_&lt;N&gt;.txt.gz</c>,
/// 1,711 rows on the REAL cells of maps 14, 115 and 116 (here read from the converter's export of the same maps), bodies and hero, flat and raised ground, creation, physics step and displaced bodies.
/// The single formula under test: every height of the DLL is the binary's minus 1 (ADR-0026), so <c>FloorHeight(DLL) = floor(binary) - 1</c> and <c>MapHeights[k](DLL) = corner(binary)[k] - 1</c>; hence
/// <c>FloorHeight - MapHeights[k]</c> and the difference of the floors of a body and of the hero are the binary's. The expected values are the oracle's, never computed by the code under test.
/// </summary>
public sealed class EnemyGroundTests
{
    private static string MapFolder(int map) => map switch
    {
        14 => Path.Combine("Overworld", "Overworld 3,1-14"),
        115 => Path.Combine("Tarn's Manor", "Tarn's Manor-115"),
        116 => Path.Combine("Tarn's Manor", "Tarn's Manor-116"),
        _ => throw new ArgumentOutOfRangeException(nameof(map)),
    };

    internal static CellsCollisionField LoadField(int map)
    {
        var folder = MapFolder(map);
        var name = Path.GetFileName(folder);
        var path = Path.Combine(SaveGameDirectorTestSupport.FindProjectRoot(), "Maps", folder, "tilemap", name + ".tileMap");
        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(path)));
        Assert.True(CellsCollisionField.TryCreate(tileMapData, name, out var field), $"the AlundraCells property of map {map} should parse");
        return field!;
    }

    /// <summary>A proxy with the box of one block of the oracle (the binary's fields, which the DLL names Pos/Mod/Width/Height/Depth).</summary>
    internal static EntityScriptProxy Body(int[] block) => new()
    {
        SpriteType = 0x100 + 79,
        PosX = block[EnemyGroundRow.PosX],
        PosY = block[EnemyGroundRow.PosY],
        PosZ = block[EnemyGroundRow.PosZ],
        ModX = block[EnemyGroundRow.ModX],
        ModY = block[EnemyGroundRow.ModY],
        ModZ = block[EnemyGroundRow.ModZ],
        Width = block[EnemyGroundRow.Width],
        Height = block[EnemyGroundRow.Height],
        Depth = block[EnemyGroundRow.Depth],
    };

    [Theory]
    [InlineData(14)]
    [InlineData(115)]
    [InlineData(116)]
    public void Body_FloorAndCornerHeights_AreTheBinarysMinusOne(int map)
    {
        var field = LoadField(map);
        var rows = EnemyOracle.Ground(map);
        Assert.NotEmpty(rows);
        var flat = 0;
        var raised = 0;
        foreach (var row in rows)
        {
            var e = row.E;
            var body = Body(e);
            body.UpdateEnemyGroundHeights(field);
            var where = $"map {map} record {row.Record} variant {row.Variant} hero offset ({row.HeroDx},{row.HeroDy}) sweep ({row.SweepDx},{row.SweepDy})";

            Assert.True(e[EnemyGroundRow.Floor - 0] - 1 == body.FloorHeight, $"{where}: floor binary {e[EnemyGroundRow.Floor]} DLL {body.FloorHeight}");
            for (var k = 0; k < 4; k++)
            {
                Assert.True(e[EnemyGroundRow.Corner0 + k] - 1 == body.MapHeights[k], $"{where}: corner {k} binary {e[EnemyGroundRow.Corner0 + k]} DLL {body.MapHeights[k]}");

                // the differences the helpers read
                Assert.True(e[EnemyGroundRow.Floor] - e[EnemyGroundRow.Corner0 + k] == body.FloorHeight - body.MapHeights[k], $"{where}: floor - corner {k}");
            }

            Assert.Equal(e[EnemyGroundRow.Terrain], body.ComputeTerrainHeight(field));
            if (e[EnemyGroundRow.Terrain] == 0)
            {
                flat++;
            }
            else
            {
                raised++;
            }
        }

        Assert.True(flat > 0 || map == 14, $"map {map}: some rows on flat ground");
        Assert.True(raised > 0, $"map {map}: some rows on raised ground");
    }

    [Theory]
    [InlineData(14)]
    [InlineData(115)]
    [InlineData(116)]
    public void Hero_FloorHeight_IsTheBinarysMinusOne_AndTheDifferenceOfFloorsIsTheBinarys(int map)
    {
        var field = LoadField(map);
        var rows = EnemyOracle.Ground(map);
        foreach (var row in rows)
        {
            var body = Body(row.E);
            body.UpdateEnemyGroundHeights(field);
            var hero = Body(row.H);

            // the hero's floor as UpdateFloorHeight computes it (no entity under his feet in the oracle: the collidable list is empty)
            var heroFloor = hero.ComputeFloorHeight(hero.ComputeTerrainHeight(field));
            var where = $"map {map} record {row.Record} variant {row.Variant} hero offset ({row.HeroDx},{row.HeroDy}) sweep ({row.SweepDx},{row.SweepDy})";
            Assert.True(row.H[EnemyGroundRow.Floor] - 1 == heroFloor, $"{where}: hero floor binary {row.H[EnemyGroundRow.Floor]} DLL {heroFloor}");

            // |dz| between the hero and the body, the third word of the relative position of the real 0x8007FE20
            Assert.True(row.R[2] == Math.Abs(body.FloorHeight - heroFloor), $"{where}: |dz| binary {row.R[2]} DLL {Math.Abs(body.FloorHeight - heroFloor)}");
            Assert.True(row.R[5] == body.FloorHeight - heroFloor, $"{where}: dz binary {row.R[5]} DLL {body.FloorHeight - heroFloor}");
        }
    }

    [Fact]
    public void UpdateEnemyGroundHeights_WithoutAField_OrForABareProxy_LeavesTheEntityAlone()
    {
        var proxy = new EntityScriptProxy { FloorHeight = 7, SpriteType = 0x100 + 79 };
        proxy.MapHeights[2] = 9;

        proxy.UpdateEnemyGroundHeights(null);

        Assert.Equal(7, proxy.FloorHeight);
        Assert.Equal(9, proxy.MapHeights[2]);

        // a proxy that was never created from a sprite record (SpriteType -1): the harness pawns, whatever the field
        var bare = new EntityScriptProxy { FloorHeight = 7 };
        bare.UpdateEnemyGroundHeights(LoadField(14));
        Assert.Equal(7, bare.FloorHeight);
        Assert.Equal(0, bare.MapHeights[0]);
    }

    /// <summary>The production paths (the spawn of the world and the steps of the physics) write the fields: the seven Beannoids of map 14, created by the real prefabs of the arena, hold the floor and the corner heights the real spawner of the binary left (variant 0 rows).</summary>
    [Fact]
    public void ProductionSpawn_OfTheMap14Beannoids_HoldsTheBinarysCreationValuesMinusOne()
    {
        using var arena = new HitArena("E14a0b-ground", 212, 96, null, 24, 0, naturalTargetCount: 7);
        var creation = EnemyOracle.Ground(14).Where(r => r.Variant == 0 && r.HeroDx == 0 && r.HeroDy == 0).ToDictionary(r => r.Record);
        Assert.Equal(7, arena.Targets.Count);
        for (var record = 0; record < 7; record++)
        {
            var expected = creation[record].E;
            var body = arena.Targets[record];
            Assert.True(expected[EnemyGroundRow.PosX] == body.PosX && expected[EnemyGroundRow.PosY] == body.PosY, $"record {record}: position binary ({expected[EnemyGroundRow.PosX]}, {expected[EnemyGroundRow.PosY]}) DLL ({body.PosX}, {body.PosY})");
            Assert.True(expected[EnemyGroundRow.Floor] - 1 == body.FloorHeight, $"record {record}: floor binary {expected[EnemyGroundRow.Floor]} DLL {body.FloorHeight}");
            for (var k = 0; k < 4; k++)
            {
                Assert.True(expected[EnemyGroundRow.Corner0 + k] - 1 == body.MapHeights[k], $"record {record}: corner {k} binary {expected[EnemyGroundRow.Corner0 + k]} DLL {body.MapHeights[k]}");
            }
        }
    }
}
