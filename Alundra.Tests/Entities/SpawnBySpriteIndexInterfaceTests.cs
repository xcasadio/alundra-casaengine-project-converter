#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Maps;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Rng;
using Alundra.Audio;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Entities;

/// <summary>
/// E20.e0a1 (docs/plan-e20-heros.md section 1.16.1, ADR-0063), the interface <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/> on the REAL world of map 14 (<c>Overworld 3,1-14</c>, built by the production path of
/// <see cref="ArcRun"/> with the export's prefabs), against the oracle of <c>docs/plan-e20-annexe/e20-e0a/oracle</c>. The full path (catalog, validity of the page, the prefab read fresh, the core, the balance sheet, the addition to the
/// world, the sound, the root push) gives the projection of the oracle HOW THE CORE DOES - hence 151 numbers of the global page (valid on every map) and the numbers of the map page that are valid on map 14 - except the fields that depend on
/// the terrain (<c>PosZ</c>, <c>ModdedPosZ</c>, the terrain, the floor and the corner heights): the oracle ran on a flat field and map 14 has its own, those are proved by the core tests on the field of the oracle.
/// No production path calls the interface (the shot of the hero is E20.f3's).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class SpawnBySpriteIndexInterfaceTests
{
    private const int HeroX = 150;
    private const int HeroY = 100;

    private static ArcRun Arc(string name) => new(new ArcSpec("E20e0a1-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    private static IEntityWorldContext Context(ArcRun arc) => arc.Proxy;

    private static EntityScriptProxy Spawn(ArcRun arc, EntityScriptProxy? parent, bool isMapPage, int index, int xPixels = HeroX, int yPixels = HeroY, int dllZ = -1, int direction = 0x10)
        => Context(arc).SpawnEntityBySpriteIndex(parent, isMapPage, index, xPixels << 16, yPixels << 16, dllZ, direction)!;

    private static int Lines(ArcRun arc) => arc.Log!.Warnings.Count(w => w.Contains("SpawnEntityBySpriteIndex", StringComparison.Ordinal));

    [Fact]
    public void B2_V6_TheMapPageNumbersValidOnMap14_AreFiveOfTheOracle()
    {
        Assert.Equal(new[] { 79, 136, 144, 153, 199 }, SpawnIndexOracle.ValidIndicesByMap()[14].ToArray());
    }

    [Fact]
    public void B2_TheFullPath_On151NumbersOfTheGlobalPage_AndTheNumbersOfTheMapPageValidOnMap14_GivesTheProjectionOfTheOracle_AndAppendsToTheWorld()
    {
        using var arc = Arc("full");
        var onMap14 = SpawnIndexOracle.ValidIndicesByMap()[14].ToHashSet();
        var vectors = SpawnIndexOracle.Vectors("v1").Concat(SpawnIndexOracle.Vectors("v2").Where(v => onMap14.Contains(v.Index))).ToList();
        Assert.Equal(151 + 5, vectors.Count);

        foreach (var v in vectors)
        {
            var before = arc.Entities.Count;
            GameRandom.RandomSeed = 0x12345678; // the seed the oracle writes before each call (run_e0a.py, SEED).
            var spawned = Context(arc).SpawnEntityBySpriteIndex(arc.Hero, v.IsMapPage, v.Index, v.X << 16, v.Y << 16, v.Z - 1, v.Direction);
            var wordAfter = (uint)GameRandom.RandomSeed;
            Assert.NotNull(spawned);
            Assert.Equal(before + 1, arc.Entities.Count);
            Assert.Same(spawned, arc.Entities[^1]); // last of the list: the list follows the order of the slots (E20.e0a2) and this vector holds the highest slot, the previous ones having been recycled.
            SpawnProjectionAsserts.Check(v.Id, v.P!, spawned!, arc.Proxy.CollisionField, arc.HeroEntity, aux: null, withTerrain: false);

            // E20.e0b2 (D-E0A-3): the loot roll of the creation, compared here and ONLY here (never in SpawnProjectionAsserts.Check, shared with the tests of the core of e0a1 that do not go through the helper of the world):
            // +0x3C and +0x40 of the oracle and the word after the call, from the seed the oracle writes before each call.
            Assert.True(
                (unchecked((int)spawned!.ContentsItemId), spawned.ContentsGameFlag, wordAfter) == (v.P!.Loot[0], v.P.Loot[1], v.RngOut),
                $"{v.Id}: loot (+0x3C {spawned.ContentsItemId}, +0x40 {spawned.ContentsGameFlag}, word {wordAfter:X8}), oracle ({v.P.Loot[0]}, {v.P.Loot[1]}, {v.RngOut:X8})");

            // E20.e0a2 (ADR-0064): 156 apparitions in ONE world exceed the table of 63 slots (the shadow run of phase A counted 112 overflows here), so each vector is destroyed and recycled once checked, as the binary frees
            // the slot of a destroyed entity at the next slot pass; the next vector takes the same lowest free slot, and is still the last of the list.
            Assert.Equal(before, spawned!.Slot);
            spawned.Status = EntityStatus.FlagToDestroy;
            arc.Proxy.RecycleDestroyedEntities();
            Assert.Equal(before, arc.Entities.Count);
        }
    }

    [Fact]
    public void F_TheRawUnitOfZ_IsAddedByTheInterface_AHeroZMakesAnEntityOneRawUnitHigher()
    {
        using var arc = Arc("z");
        var field = arc.Proxy.CollisionField;
        // A Z well above the ground of map 14 at this point: no raise to the terrain hides the rule.
        var high = 40 << 16;
        var spawned = Spawn(arc, arc.Hero, false, 3, dllZ: high);
        Assert.True(spawned.ComputeTerrainHeight(field) < high);
        Assert.Equal(high + 1 - spawned.ModZ, spawned.PosZ); // D-E0A-1: PosZ = (z + 1) - ModZ, before the raise.
        Assert.Equal(high + 1 - spawned.ModZ + spawned.ModZ, spawned.ModdedPosZ);

        // The prefab with an OffsetZ of -18 px at 50 px: the subtraction is visible.
        var offset = Spawn(arc, arc.Hero, false, 20, dllZ: 50 << 16);
        Assert.Equal((50 << 16) + 1 + (18 << 16), offset.PosZ);
    }

    [Fact]
    public void F_TheEightCallsOfTheAt9Traces_AppearAtTheZOfTheBinaryMinusOneRawUnit_UnlessTheTerrainRaisesThem()
    {
        using var arc = Arc("at9");
        var field = arc.Proxy.CollisionField;
        foreach (var v in SpawnIndexOracle.Vectors("v7"))
        {
            // The DLL's call of the shot (the seam of E20.f3a) passes 1 048 576 where the binary's trace has 1 048 577.
            var spawned = Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, v.Index, v.X << 16, v.Y << 16, v.Z - 1, v.Direction)!;
            Assert.NotNull(spawned);
            var terrain = spawned.ComputeTerrainHeight(field);
            var expected = v.P!.Pz - 1 <= terrain ? terrain : v.P.Pz - 1;
            Assert.True(expected == spawned.PosZ, $"{v.Id}: PosZ {spawned.PosZ}, expected {expected} (oracle {v.P.Pz} - 1, terrain {terrain})");
            Assert.Equal(v.P.Px, spawned.PosX);
            Assert.Equal(v.P.Py, spawned.PosY);
        }
    }

    [Fact]
    public void C_TheNullContract_AnInvalidIndexAnAbsentPrefabOrAnUnknownMap_AddsNothing_AndLogsOneLine()
    {
        using var arc = Arc("null");
        var global = SpawnIndexOracle.Vectors("v1").Select(v => v.Index).ToHashSet();
        var absent = Enumerable.Range(0, 256).First(i => !global.Contains(i));
        var invalid = new (bool IsMapPage, int Index, string Why)[]
        {
            (false, -1, "negative"), (false, 256, "256"), (false, absent, "no bank key on the global page"), (true, -1, "negative on the map page"), (true, 256, "256 on the map page"),
            (true, 213, "a number of the map page that is not valid on map 14"),
        };

        foreach (var (isMapPage, index, why) in invalid)
        {
            var count = arc.Entities.Count;
            var lines = Lines(arc);
            Assert.True(Context(arc).SpawnEntityBySpriteIndex(arc.Hero, isMapPage, index, HeroX << 16, HeroY << 16, 0, 0x10) is null, why);
            Assert.Equal(count, arc.Entities.Count);
            Assert.Equal(lines + 1, Lines(arc));
        }

        // A map the file does not know.
        var previousIndices = arc.Proxy.MapSpriteIndices;
        arc.Proxy.MapSpriteIndices = new MapSpriteIndices(new Dictionary<int, IReadOnlyList<int>> { [15] = new[] { 79 } });
        var countBefore = arc.Entities.Count;
        var linesBefore = Lines(arc);
        Assert.Null(Context(arc).SpawnEntityBySpriteIndex(arc.Hero, true, 79, HeroX << 16, HeroY << 16, 0, 0x10));
        Assert.Equal(countBefore, arc.Entities.Count);
        Assert.Equal(linesBefore + 1, Lines(arc));
        arc.Proxy.MapSpriteIndices = previousIndices;

        // A prefab the content manager cannot read.
        var previousCatalog = arc.Proxy.SpriteRecordCatalog;
        arc.Proxy.SpriteRecordCatalog = new UnreadablePrefabCatalog(previousCatalog);
        linesBefore = Lines(arc);
        Assert.Null(Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, 3, HeroX << 16, HeroY << 16, 0, 0x10));
        Assert.Equal(countBefore, arc.Entities.Count);
        Assert.Equal(linesBefore + 1, Lines(arc));
        arc.Proxy.SpriteRecordCatalog = previousCatalog;

        // A null parent is accepted (the binary accepts it too).
        var orphan = Context(arc).SpawnEntityBySpriteIndex(null, false, 3, HeroX << 16, HeroY << 16, 0, 0x10);
        Assert.NotNull(orphan);
        Assert.Equal(countBefore + 1, arc.Entities.Count);
        Assert.Null(orphan!.ParentEntity);
        Assert.Null(orphan.ChildEntity);
    }

    /// <summary>A catalog that gives a prefab for every key but whose prefab no content manager can read (a prefab the export does not hold).</summary>
    private sealed class UnreadablePrefabCatalog : ISpriteRecordCatalog
    {
        private readonly ISpriteRecordCatalog _real;

        public UnreadablePrefabCatalog(ISpriteRecordCatalog real) => _real = real;

        public bool TryGet(Guid prefabAssetId, out SpriteRecordHeader header)
        {
            header = default;
            return true;
        }

        public bool TryGetPrefab(bool isMapPage, int index, out Guid prefabAssetId)
        {
            prefabAssetId = Guid.Parse("00000000-0000-0000-0000-00000000e0a1");
            return true;
        }
    }

    [Fact]
    public void D_TheNewEntity_IsNotFoundBy0x2COr0x2EOf0_AndItsTriggerIsMinusOne()
    {
        using var arc = Arc("search");
        var spawned = Spawn(arc, arc.Hero, false, 3);
        Assert.Equal(-1, spawned.EntityRefId); // the binary writes -1 at 0x80039D70-0x80039D88; the field's default 0 would answer to 0x2C [0] and 0x2E [0].
        Assert.Equal(ScriptHelper.ProgramUnknown, spawned.EventTrigger);
        Assert.Equal(EntityStatus.Loaded, spawned.Status);

        // 0x2C [0] and 0x2E [0] are this search with the record 0 as the key.
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(arc.Hero, 0, arc.Entities, arc.Hero);
        Assert.DoesNotContain(spawned, matches);
        Assert.All(matches, m => Assert.Equal(0, m.EntityRefId));
    }

    [Fact]
    public void E_TheParentAndTheRoot_ThroughTheInterface_FourCases()
    {
        using var arc = Arc("parent");
        var hero = arc.HeroEntity;

        var first = Spawn(arc, arc.Hero, false, 3);
        Assert.Same(hero, first.ParentEntity);
        Assert.Same(hero, first.ChildEntity);

        var second = Spawn(arc, first, false, 4);
        Assert.Same(ArcRun.EntityOf(first), second.ParentEntity);
        Assert.Same(hero, second.ChildEntity); // a child of a child: the root of its parent.

        var third = Spawn(arc, second, false, 7);
        Assert.Same(hero, third.ChildEntity);

        var orphan = Context(arc).SpawnEntityBySpriteIndex(null, false, 3, HeroX << 16, HeroY << 16, 0, 0x10)!;
        Assert.Null(orphan.ParentEntity);
        Assert.Null(orphan.ChildEntity);
    }

    [Fact]
    public void H_TheNumber79OfTheMapPage_ByTheInterface_AndTheRecord0OfMap14_ByTheProductionPath_DifferOnlyByTheClosedList()
    {
        using var arc = Arc("record");
        var record = arc.EntityByRecord(0);
        Assert.NotNull(record);
        Assert.Equal(0x100 + 79, record!.SpriteType);
        Assert.NotNull(record.RecordHeight);

        // The same call as the record's: its own X and Y, its own Z (Height << 19 is the binary's argument; the interface wants the DLL's, one raw unit less), its direction.
        var byIndex = Context(arc).SpawnEntityBySpriteIndex(arc.Hero, true, 79, record.PosX, record.PosY, (record.RecordHeight!.Value << 19) - 1, (int)record.TargetDirection)!;
        Assert.NotNull(byIndex);

        Assert.Equal(record.SpriteType, byIndex.SpriteType);
        Assert.Equal(record.SpriteTableIndex, byIndex.SpriteTableIndex);
        Assert.Equal(record.Hp, byIndex.Hp);
        Assert.Equal(record.HpMax, byIndex.HpMax);
        Assert.Equal(record.Flags, byIndex.Flags);
        Assert.Equal(record.SpriteProgramIndexes, byIndex.SpriteProgramIndexes);
        Assert.Equal(record.ProgramIndexes, byIndex.ProgramIndexes);
        Assert.Equal(record.TargetAnimationId, byIndex.TargetAnimationId);
        Assert.Equal(record.TargetDirection, byIndex.TargetDirection);
        Assert.Equal(record.CurrentAnimationId, byIndex.CurrentAnimationId);
        Assert.Equal(record.CurrentDirection, byIndex.CurrentDirection);
        Assert.Equal((record.PosX, record.PosY, record.PosZ), (byIndex.PosX, byIndex.PosY, byIndex.PosZ));
        Assert.Equal((record.ModdedPosX, record.ModdedPosY, record.ModdedPosZ), (byIndex.ModdedPosX, byIndex.ModdedPosY, byIndex.ModdedPosZ));
        Assert.Equal((record.ModX, record.ModY, record.ModZ), (byIndex.ModX, byIndex.ModY, byIndex.ModZ));
        Assert.Equal((record.Width, record.Height, record.Depth), (byIndex.Width, byIndex.Height, byIndex.Depth));
        Assert.Equal((record.TileX, record.TileY, record.TileZ), (byIndex.TileX, byIndex.TileY, byIndex.TileZ));
        Assert.Equal(record.FloorHeight, byIndex.FloorHeight);
        Assert.Equal(record.MapHeights, byIndex.MapHeights);
        Assert.Equal(record.Status, byIndex.Status);
        Assert.Equal(record.EventTrigger, byIndex.EventTrigger);
        Assert.Equal(record.BreakEffect, byIndex.BreakEffect);
        Assert.Equal(record.SpawnAnimationActive, byIndex.SpawnAnimationActive);
        Assert.Equal(record.AnimationSoundOf(0), byIndex.AnimationSoundOf(0));

        // The closed list: the case and the series (e0a2), the root and the parent, the record's own identity (EntityRefId, the record's height), and the self pointer.
        Assert.Equal(-1, byIndex.EntityRefId);
        Assert.Equal(0, record.EntityRefId);
        Assert.Same(arc.HeroEntity, byIndex.ParentEntity);
        Assert.Null(record.ParentEntity);
        Assert.Null(byIndex.RecordHeight);
        Assert.Same(ArcRun.EntityOf(byIndex), byIndex.LogicContextEntity);
    }

    /// <summary>A recorder of the sounds asked (the world proxy's sound player seam).</summary>
    private sealed class RecordingSoundPlayer : ISoundPlayer
    {
        public List<int> Ids { get; } = new();

        public void PlaySfx(int sfxId) => Ids.Add(sfxId);

        public void RemixVoice(int sfxId, int left, int right)
        {
        }

        public void FlushFrameSounds()
        {
        }

        public void StopAllSfx()
        {
        }
    }

    [Fact]
    public void G_TheSoundOfTheAnimation0_IsRequestedOnceAtTheTickOfTheCall_AndOnlyWhereTheBinaryPlaysOne()
    {
        using var arc = Arc("sound");
        var player = new RecordingSoundPlayer();
        typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(arc.Proxy, player);

        Spawn(arc, arc.Hero, false, 3);
        Assert.Empty(player.Ids); // the arrows have no sound at their creation.
        Spawn(arc, arc.Hero, false, 7);
        Assert.Equal(new[] { 0x21 }, player.Ids);
        Spawn(arc, arc.Hero, false, 27);
        Assert.Equal(new[] { 0x21, 0x1E }, player.Ids);
    }

    [Fact]
    public void L_TheIndicesOfTheMapPage_AreReadAtTheFirstCallOfTheMapPage_NeverAtTheLoadOfAWorld()
    {
        using var arc = Arc("lazy");
        Assert.Null(arc.Proxy.MapSpriteIndices); // a world loaded: the file has not been read (no existing world sees a warning).
        Spawn(arc, arc.Hero, false, 3);
        Assert.Null(arc.Proxy.MapSpriteIndices); // the global page needs no file.
        Spawn(arc, arc.Hero, true, 79);
        Assert.NotNull(arc.Proxy.MapSpriteIndices);
        Assert.True(arc.Proxy.MapSpriteIndices!.IsLoaded);
    }

    [Fact]
    public void NoOpContext_AndEveryExistingImplementer_KeepCompiling_TheDefaultOfTheInterfaceIsNull()
    {
        IEntityWorldContext context = NoOpEntityWorldContext.Instance;
        Assert.Null(context.SpawnEntityBySpriteIndex(null, false, 3, 0, 0, 0, 0x10));
    }
}
