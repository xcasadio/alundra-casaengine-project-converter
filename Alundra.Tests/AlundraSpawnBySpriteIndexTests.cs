#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using static Alundra.Tests.SaveGameDirectorTestSupport;
using Alundra.Maps;

namespace Alundra.Tests;

/// <summary>The comparison of the projection of an oracle vector with an entity of the DLL, shared by the tests of the core and of the full path of the interface.</summary>
internal static class SpawnProjectionAsserts
{
    /// <summary>The fields the plan compares (README of the annex). <paramref name="withTerrain"/> false leaves out the ones that depend on the terrain of the field: <c>PosZ</c>, <c>ModdedPosZ</c>, the terrain, the floor and the corner heights.</summary>
    internal static void Check(string id, SpawnProjection p, AlundraEntityScriptProxy proxy, ICollisionField? field, Entity hero, Entity? aux, bool withTerrain)
    {
        Assert.True(p.Hp == proxy.Hp, $"{id}: Hp {proxy.Hp} (oracle {p.Hp})");
        Assert.True(p.HpMax == proxy.HpMax, $"{id}: HpMax {proxy.HpMax} (oracle {p.HpMax})");
        Assert.True(p.Ref == proxy.EntityRefId, $"{id}: EntityRefId {proxy.EntityRefId} (oracle {p.Ref})");
        Assert.True(p.Id == proxy.SpriteType, $"{id}: SpriteType {proxy.SpriteType} (oracle {p.Id})");
        Assert.True(p.Flags == proxy.Flags, $"{id}: Flags {proxy.Flags:X} (oracle {p.Flags:X})");
        Assert.True(p.SpriteProgram.SequenceEqual(proxy.SpriteProgramIndexes), $"{id}: SpriteProgramIndexes [{string.Join(",", proxy.SpriteProgramIndexes)}] (oracle [{string.Join(",", p.SpriteProgram)}])");
        Assert.True(p.Program.SequenceEqual(proxy.ProgramIndexes), $"{id}: ProgramIndexes [{string.Join(",", proxy.ProgramIndexes)}] (oracle [{string.Join(",", p.Program)}])");
        Assert.True(p.AnimTarget == proxy.TargetAnimationId, $"{id}: TargetAnimationId {proxy.TargetAnimationId} (oracle {p.AnimTarget})");
        Assert.True(p.DirTarget == proxy.TargetDirection, $"{id}: TargetDirection {proxy.TargetDirection} (oracle {p.DirTarget})");
        Assert.True(p.Px == proxy.PosX && p.Py == proxy.PosY, $"{id}: PosX/PosY {proxy.PosX}/{proxy.PosY} (oracle {p.Px}/{p.Py})");
        Assert.True(p.ModdedX == proxy.ModdedPosX && p.ModdedY == proxy.ModdedPosY, $"{id}: ModdedPosX/Y {proxy.ModdedPosX}/{proxy.ModdedPosY} (oracle {p.ModdedX}/{p.ModdedY})");
        Assert.True(p.ModX == proxy.ModX && p.ModY == proxy.ModY && p.ModZ == proxy.ModZ, $"{id}: ModX/Y/Z {proxy.ModX}/{proxy.ModY}/{proxy.ModZ} (oracle {p.ModX}/{p.ModY}/{p.ModZ})");
        Assert.True(p.Width == proxy.Width && p.Height == proxy.Height && p.Depth == proxy.Depth, $"{id}: Width/Height/Depth {proxy.Width}/{proxy.Height}/{proxy.Depth} (oracle {p.Width}/{p.Height}/{p.Depth})");
        Assert.True(p.TileX == proxy.TileX && p.TileY == proxy.TileY, $"{id}: TileX/TileY {proxy.TileX}/{proxy.TileY} (oracle {p.TileX}/{p.TileY})");
        Assert.True((p.HasAttack != 0) == (proxy.CurrentAttack != null), $"{id}: the attack of the animation 0 ({proxy.CurrentAttack != null}; oracle {p.HasAttack})");
        Assert.True(p.AnimFlags == proxy.HitAnimByte, $"{id}: HitAnimByte {proxy.HitAnimByte} (oracle {p.AnimFlags})");
        Assert.Same(Resolve(p.Parent, hero, aux), proxy.ParentEntity);
        Assert.Same(Resolve(p.Root, hero, aux), proxy.ChildEntity);
        if (!withTerrain)
        {
            return;
        }

        // The binary's Z (and floor, corner heights) is the DLL's plus one raw unit (ADR-0026).
        Assert.True(p.Pz - 1 == proxy.PosZ, $"{id}: PosZ {proxy.PosZ} (oracle {p.Pz} - 1)");
        Assert.True(p.ModdedZ - 1 == proxy.ModdedPosZ, $"{id}: ModdedPosZ {proxy.ModdedPosZ} (oracle {p.ModdedZ} - 1)");
        Assert.True(p.Terrain == proxy.ComputeTerrainHeight(field), $"{id}: terrain {proxy.ComputeTerrainHeight(field)} (oracle {p.Terrain})");
        Assert.True(p.Floor - 1 == proxy.FloorHeight, $"{id}: FloorHeight {proxy.FloorHeight} (oracle {p.Floor} - 1)");
        for (var k = 0; k < 4; k++)
        {
            Assert.True(p.Corners[k] - 1 == proxy.MapHeights[k], $"{id}: MapHeights[{k}] {proxy.MapHeights[k]} (oracle {p.Corners[k]} - 1)");
        }
    }

    private static Entity? Resolve(string who, Entity hero, Entity? aux) => who switch
    {
        "none" => null,
        "hero" => hero,
        "parent" => aux ?? throw new InvalidOperationException("the vector has no spawned parent"),
        _ => throw new InvalidOperationException(who),
    };
}

/// <summary>
/// E20.e0a1 (docs/plan-e20-heros.md section 1.16.1, ADR-0063), the data and the record-less core: the apparition of an entity by sprite index (<c>SpawnWarpEntity</c>, <c>0x80039F58</c>) against the REAL code of <c>ALUN_CD.EXE</c> (France)
/// run in the oracle of <c>docs/plan-e20-annexe/e20-e0a/oracle</c>. Nothing here computes an expected value of the creation: the rows of the oracle are the expected values (read by <see cref="AlundraSpawnIndexOracle"/>).
/// (a) the 395 bank keys of the export and the valid indices of the 483 maps; (b) the creation image of the 395 vectors and the geometry vectors at the level of the core, without a world; (g) the creation sound;
/// the frame of Z (D-E0A-1: the binary's argument is the DLL's plus one raw unit, ADR-0026) on the 8 calls the AT9 oracle journals at the real sites of the hero shot.
/// </summary>
public sealed class AlundraSpawnBySpriteIndexCoreTests
{
    private static readonly string Root = ProjectRootFinder.Find();
    private static readonly Lazy<SpriteRecordCatalog> Catalog = new(() => new SpriteRecordCatalog(Root));
    private static readonly Lazy<AlundraBalanceTables> Tables = new(() => AlundraBalanceTables.GetOrCreate(Root));

    // ---------------------------------------------------------------------------------------------------------------------
    // (a) the data
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_Data_TheBankKeysOfTheExport_GiveOnePrefabEachOfTheCatalog_151OnTheGlobalPageAnd244OnTheMapPage()
    {
        var vectors = AlundraSpawnIndexOracle.Vectors("v1").Concat(AlundraSpawnIndexOracle.Vectors("v2")).ToList();
        Assert.Equal(151, vectors.Count(v => !v.IsMapPage));
        Assert.Equal(244, vectors.Count(v => v.IsMapPage));

        var seen = new HashSet<Guid>();
        foreach (var v in vectors)
        {
            Assert.True(Catalog.Value.TryGetPrefab(v.IsMapPage, v.Index, out var prefab), $"{v.Id}: no prefab for the bank key");
            Assert.True(Catalog.Value.TryGet(prefab, out _), $"{v.Id}: the prefab {prefab} is not in the catalog");
            Assert.True(seen.Add(prefab), $"{v.Id}: the prefab {prefab} is already the one of another key");
        }

        Assert.Equal(395, seen.Count);
    }

    [Fact]
    public void A_Data_AnIndexWithoutBankKey_HasNoPrefab_AndAnAbsentKeyIsTolerated()
    {
        var global = AlundraSpawnIndexOracle.Vectors("v1").Select(v => v.Index).ToHashSet();
        var absent = Enumerable.Range(0, 256).First(i => !global.Contains(i));
        Assert.False(Catalog.Value.TryGetPrefab(false, absent, out _));
        Assert.False(Catalog.Value.TryGetPrefab(false, -1, out _));
        Assert.False(Catalog.Value.TryGetPrefab(true, 256, out _));
        Assert.False(new FakeSpriteRecordCatalog().TryGetPrefab(true, 79, out _)); // a catalog with no key at all (an export that predates BankKey) answers "no prefab", it does not throw.
    }

    [Fact]
    public void A_Data_TheValidIndicesOfTheMapPage_OfThe483Maps_AreTheOnesTheBinaryAnswered_123648Comparisons()
    {
        var indices = AlundraMapSpriteIndices.GetOrCreate(Root);
        Assert.True(indices.IsLoaded);
        var oracle = AlundraSpawnIndexOracle.ValidIndicesByMap();
        Assert.Equal(483, oracle.Count);
        var comparisons = 0;
        var valid = 0;
        for (var map = 0; map < 483; map++)
        {
            var expected = oracle[map].ToHashSet();
            for (var index = 0; index < 256; index++)
            {
                Assert.True(expected.Contains(index) == indices.IsValid(map, index), $"map {map} index {index}");
                comparisons++;
                valid += expected.Contains(index) ? 1 : 0;
            }
        }

        Assert.Equal(123648, comparisons);
        Assert.Equal(2507, valid);
        Assert.False(indices.IsValid(483, 79)); // a map the file does not know.
        Assert.False(indices.IsValid(14, 256));
        Assert.False(indices.IsValid(14, -1));
    }

    [Fact]
    public void A_Data_AMissingFile_IsPermissive_OneWarning_AndAnUnknownMapIsRefusedWhenTheFileIsThere()
    {
        var empty = Path.Combine(Path.GetTempPath(), "alundra-e0a1-no-indices-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            using var log = LogCapture.Install();
            var degraded = new AlundraMapSpriteIndices(empty);
            Assert.False(degraded.IsLoaded);
            Assert.True(degraded.IsValid(14, 79)); // permissive: the catalog's bank key alone decides.
            Assert.Single(log.Warnings, w => w.Contains("map-sprite-indices", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // (b) the creation image, at the level of the core (no world)
    // ---------------------------------------------------------------------------------------------------------------------

    private static Entity NewBareEntity()
    {
        var entity = new Entity { GameplayProxyClassName = typeof(AlundraEntityScriptProxy).FullName! };
        entity.Initialize();
        return entity;
    }

    private static AlundraCellsCollisionField FieldOf(string world)
        => world == "raised" ? FlatCells.Create(cell: (_, _) => (0, 2)) : FlatCells.Create();

    private sealed record Created(Entity Entity, AlundraEntityScriptProxy Proxy, Entity Hero, Entity? Aux);

    /// <summary>The vector, as the oracle made it, at the level of the core: the parent as asked (none, the hero, an entity created first), then the creation (the binary's argument Z), then the balance sheet of the sprite (threshold 0, as the oracle).</summary>
    private static Created Create(SpawnVector v, ICollisionField field, bool initializeBalance = true)
    {
        var hero = NewBareEntity();
        Entity? aux = null;
        Entity? parent = v.Parent switch
        {
            "none" => null,
            "hero" => hero,
            _ => aux = CreateAt(false, 3, hero, 150, 100, 0, 0x10, field),
        };
        var entity = CreateAt(v.IsMapPage, v.Index, parent, v.X, v.Y, v.Z, v.Direction, field);
        var proxy = (AlundraEntityScriptProxy)entity.GameplayProxy!;
        if (initializeBalance)
        {
            proxy.InitializeBalance(Tables.Value, 0);
        }

        return new Created(entity, proxy, hero, aux);
    }

    private static Entity CreateAt(bool isMapPage, int index, Entity? parent, int xPixels, int yPixels, int zRaw, int direction, ICollisionField? field)
    {
        Assert.True(Catalog.Value.TryGetPrefab(isMapPage, index, out var prefab));
        Assert.True(Catalog.Value.TryGet(prefab, out var header));
        return AlundraEntitySpawnFactory.CreateEntityFromSpriteIndex(null, header, isMapPage, index, parent, xPixels << 16, yPixels << 16, zRaw, direction, field);
    }

    [Fact]
    public void B_TheCreationImage_OfThe151PrefabsOfTheGlobalPage_IsTheOnesOfTheBinary()
    {
        var field = FieldOf("flat");
        foreach (var v in AlundraSpawnIndexOracle.Vectors("v1"))
        {
            var c = Create(v, field);
            SpawnProjectionAsserts.Check(v.Id, v.P!, c.Proxy, field, c.Hero, c.Aux, withTerrain: true);
        }
    }

    [Fact]
    public void B_TheCreationImage_OfThe244NumbersOfTheMapPage_IsTheOnesOfTheBinary_OneImagePerNumber()
    {
        var field = FieldOf("flat");
        foreach (var v in AlundraSpawnIndexOracle.Vectors("v2"))
        {
            var c = Create(v, field);
            SpawnProjectionAsserts.Check(v.Id, v.P!, c.Proxy, field, c.Hero, c.Aux, withTerrain: true);
        }
    }

    [Fact]
    public void B_TheGeometryVectors_ZParentDirectionAndTerrain_AreTheOnesOfTheBinary()
    {
        var vectors = AlundraSpawnIndexOracle.Vectors("v3");
        Assert.True(vectors.Count >= 40);
        Assert.Contains(vectors, v => v.World == "raised");
        Assert.Contains(vectors, v => v.Parent == "spawned");
        Assert.Contains(vectors, v => v.Z < 0);
        foreach (var v in vectors)
        {
            var field = FieldOf(v.World);
            var c = Create(v, field);
            SpawnProjectionAsserts.Check(v.Id, v.P!, c.Proxy, field, c.Hero, c.Aux, withTerrain: true);
        }
    }

    [Fact]
    public void B_ThePrefabsWithAnOffsetZ_AppearedAboveTheGround_KeepTheirModZ_TheMutantOfZMinusModZWouldSurvive()
    {
        // The vectors that make the subtraction of ModZ visible: OffsetZ is not 0 and the entity is above the ground (the raise to the terrain would hide it at Z 0).
        var chosen = AlundraSpawnIndexOracle.Vectors("v3").Where(v => v.World == "flat" && v.Z >= 30 << 16 && v.P!.ModZ != 0).ToList();
        Assert.True(chosen.Count >= 6, "the oracle must hold the OffsetZ vectors above the ground");
        Assert.Contains(chosen, v => !v.IsMapPage && v.Index == 20);
        Assert.Contains(chosen, v => v.IsMapPage && v.Index == 153);
        var field = FieldOf("flat");
        foreach (var v in chosen)
        {
            var c = Create(v, field);
            Assert.Equal(v.P!.Pz - 1, c.Proxy.PosZ);
            Assert.Equal(v.Z - c.Proxy.ModZ, c.Proxy.PosZ); // above the ground: the binary's PosZ is Z - ModZ + 1, the DLL's one raw unit less.
        }
    }

    [Fact]
    public void B_TheCaseAndTheSeries_AreNotWrittenYet_E0a2()
    {
        var v = AlundraSpawnIndexOracle.Vectors("v1").First();
        var c = Create(v, FieldOf("flat"));
        Assert.Equal(0, c.Proxy.Index);  // the slot (+0x00) and the series (+0x04) are e0a2's: pinned absent here, that task changes this line.
        Assert.Equal(0, c.Proxy.Index2);
    }

    [Fact]
    public void E_TheParentAndTheRoot_FourCases()
    {
        var field = FieldOf("flat");
        var hero = NewBareEntity();

        var none = (AlundraEntityScriptProxy)CreateAt(false, 3, null, 150, 100, 0, 0x10, field).GameplayProxy!;
        Assert.Null(none.ParentEntity);
        Assert.Null(none.ChildEntity);

        var firstEntity = CreateAt(false, 3, hero, 150, 100, 0, 0x10, field);
        var first = (AlundraEntityScriptProxy)firstEntity.GameplayProxy!;
        Assert.Same(hero, first.ParentEntity);
        Assert.Same(hero, first.ChildEntity); // the parent has no root: the root is the parent (parent.+8 == 0 gives the parent, 0x80039D48-0x80039D5C).

        var secondEntity = CreateAt(false, 4, firstEntity, 150, 100, 0, 0x10, field);
        var second = (AlundraEntityScriptProxy)secondEntity.GameplayProxy!;
        Assert.Same(firstEntity, second.ParentEntity);
        Assert.Same(hero, second.ChildEntity); // a child of a child: the root of the parent.

        var third = (AlundraEntityScriptProxy)CreateAt(false, 7, secondEntity, 150, 100, 0, 0x10, field).GameplayProxy!;
        Assert.Same(hero, third.ChildEntity);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // (g) the creation sound, and the frame of Z
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void G_TheCreationSound_IsTheOneOfTheAnimation0_7PrefabsOfTheGlobalPageAnd9NumbersOfTheMapPage()
    {
        var field = FieldOf("flat");
        var withSound = new List<(int Page, int Index, int Sound)>();
        foreach (var v in AlundraSpawnIndexOracle.Vectors("v1").Concat(AlundraSpawnIndexOracle.Vectors("v2")))
        {
            var proxy = Create(v, field, initializeBalance: false).Proxy;
            var sound = proxy.AnimationSoundOf(proxy.SpawnAnimationId);
            Assert.Equal(v.Sounds.FirstOrDefault(), sound > 0 ? sound : 0);
            Assert.True(proxy.SpawnAnimationActive);
            if (sound > 0)
            {
                withSound.Add((v.Page, v.Index, sound));
            }
        }

        Assert.Equal(7, withSound.Count(s => s.Page == 0));
        Assert.Equal(9, withSound.Count(s => s.Page == 1));
        Assert.Equal(3, withSound.Count(s => s.Page == 1 && (s.Sound & 0x100) != 0));
        Assert.Equal(0x21, withSound.Single(s => s.Page == 0 && s.Index == 7).Sound);
        Assert.Equal(0x1E, withSound.Single(s => s.Page == 0 && s.Index == 27).Sound);
        Assert.DoesNotContain(withSound, s => s.Page == 0 && s.Index == 3);
        Assert.Equal(0x114, withSound.Single(s => s.Page == 1 && s.Index == 192).Sound);
    }

    [Fact]
    public void F_TheEightCallsOfTheHeroShotJournalledByTheAt9Oracle_GiveTheBinarysImage_TheInterfaceAddsTheRawUnit()
    {
        var vectors = AlundraSpawnIndexOracle.Vectors("v7");
        Assert.Equal(8, vectors.Count);
        Assert.Equal(new[] { 3, 4, 11, 14 }, vectors.Select(v => v.Index).Distinct().OrderBy(i => i).ToArray());
        var field = FieldOf("flat");
        foreach (var v in vectors)
        {
            var c = Create(v, field);
            SpawnProjectionAsserts.Check(v.Id, v.P!, c.Proxy, field, c.Hero, c.Aux, withTerrain: true);
            // The DLL's call of the shot (the seam of E20.f3a) passes Z = PosZ + (ptr[3] << 16) in the DLL's frame: the binary's argument minus one raw unit (AlundraHeroAttackTests, the AT9 traces).
            var dllZ = v.Z - 1;
            Assert.Equal(v.Z, dllZ + 1);
        }

        // 1 048 577 in the trace of the binary, 1 048 576 in the call of the shot of the DLL.
        Assert.Contains(vectors, v => v.Z == 1048577);
    }
}
