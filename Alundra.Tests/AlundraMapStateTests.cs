#nullable enable
using System;
using System.Linq;
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
/// E20.e0k1 (docs/plan-e20-heros.md section 1.18.1, D-E0K-2): the state that reads the map (the two raw words of gravity and viscosity, the walkability mask, the vertical ownership and the minimum move distance of the
/// controller) is posed by ONE helper (<see cref="EntitySpawnFactory.ApplyMapDependentState"/>), called by the record path and by the creation by sprite index when a map is given.
/// (a) the helper on the creation by sprite index, at the level of the core (a prefab with a controller, the real headers of the catalog): with a map (128, 4096) the arrow (flags <c>0x012074</c>, mask <c>0x40</c>), the wave
/// (ClassB, <c>0x41</c>) and a ClassA entity (<c>0x1040</c>) get the state; the SAME creation without a map leaves the defaults of the controller, in the same assertion. (b) the guard: an entity created from a record has
/// the same values as before the extraction (green by construction). (c) the pin P3 of E20.e0a3b (D-E0A-8): the marked entity whose first animation is validated keys its kinematics on its CURRENT animation, never on a
/// target that changed after the validation.
/// </summary>
public sealed class AlundraMapStateTests
{
    private const int MapGravity = 128;
    private const int MapViscosity = 4096;

    private static readonly string Root = ProjectRootFinder.Find();
    private static readonly Lazy<SpriteRecordCatalog> Catalog = new(() => new SpriteRecordCatalog(Root));

    private static TileMapData Map(int gravity, int viscosity)
    {
        var map = new TileMapData();
        map.CustomProperties["Gravity"] = gravity.ToString();
        map.CustomProperties["ZViscosity"] = viscosity.ToString();
        return map;
    }

    /// <summary>A prefab with a controller (default settings: mask 0, not owned, minimum move distance 0.001), the only thing the state needs besides the header.</summary>
    private static Entity PrefabWithController()
    {
        var prefab = new Entity { Name = "MapStatePrefab", GameplayProxyClassName = typeof(AlundraEntityScriptProxy).FullName! };
        prefab.AddComponent(new CharacterControllerComponent());
        return prefab;
    }

    /// <summary>What the state reads back: the two raw words, the mask, the ownership and the minimum move distance.</summary>
    private sealed record State(int GravityRaw, int ViscosityRaw, uint Mask, bool Owned, float MinMove);

    private static State Read(AlundraEntityScriptProxy proxy)
    {
        Assert.NotNull(proxy.Controller);
        return new State(proxy.MapGravityRaw, proxy.MapZViscosityRaw, proxy.Controller!.Settings.WalkabilityMask, proxy.Controller.IsVerticalOwnedExternally, proxy.Controller.Settings.MinMoveDistance);
    }

    private static AlundraEntityScriptProxy Born(bool isMapPage, int index, TileMapData? map)
    {
        Assert.True(Catalog.Value.TryGetPrefab(isMapPage, index, out var prefabId));
        Assert.True(Catalog.Value.TryGet(prefabId, out var header));
        var entity = EntitySpawnFactory.CreateEntityFromSpriteIndex(PrefabWithController(), header, isMapPage, index, null, 150 << 16, 100 << 16, 0, 0x18, null, map);
        return Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
    }

    /// <summary>The defaults of a controller no map has touched: the state a creation without a map must leave.</summary>
    private static State Untouched()
    {
        var bare = new CharacterControllerComponent();
        return new State(0, 0, bare.Settings.WalkabilityMask, bare.IsVerticalOwnedExternally, bare.Settings.MinMoveDistance);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (a) the creation by sprite index.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(false, 3, 0x40u)]        // the arrow: neither ClassA nor ClassB.
    [InlineData(false, 27, 0x41u)]       // the sword wave: ClassB.
    [InlineData(true, 79, 0x1040u)]      // a Beannoid: ClassA.
    public void A_TheMapState_IsPosedOnTheEntityBornWithAMap_AndTheSameCreationWithoutAMapChangesNothing(bool isMapPage, int index, uint expectedMask)
    {
        var with = Read(Born(isMapPage, index, Map(MapGravity, MapViscosity)));
        var without = Read(Born(isMapPage, index, null));

        // One assertion for the pair: the case with the map carries the state, the case without leaves the defaults (so a state posed whatever the argument fails, and so does one never posed).
        Assert.Equal(
            (new State(MapGravity, MapViscosity, expectedMask, Owned: true, MinMove: 0f), Untouched()),
            (with, without));
    }

    [Fact]
    public void A_TheTwoWordsAreTheOnesOfTheMapGiven_NotConstants()
    {
        var gravity128 = Read(Born(false, 3, Map(MapGravity, MapViscosity)));
        var gravity3 = Read(Born(false, 3, Map(3, 256)));
        Assert.Equal(((MapGravity, MapViscosity), (3, 256)), ((gravity128.GravityRaw, gravity128.ViscosityRaw), (gravity3.GravityRaw, gravity3.ViscosityRaw)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b) the guard: the record path has the values it had.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(false, 3, 0x40u)]
    [InlineData(false, 27, 0x41u)]
    [InlineData(true, 79, 0x1040u)]
    public void B_AnEntityCreatedFromARecord_HasTheSameStateAsBeforeTheExtraction(bool isMapPage, int index, uint expectedMask)
    {
        Assert.True(Catalog.Value.TryGetPrefab(isMapPage, index, out var prefabId));
        var record = new TileMapObjectData { Id = 0, Name = "Entity_0" };
        record.CustomProperties["Index"] = "0";
        record.CustomProperties["XPos"] = "150";
        record.CustomProperties["YPos"] = "100";
        record.CustomProperties["Height"] = "0";
        record.CustomProperties["SpriteTableIndex"] = index.ToString();
        record.CustomProperties["SpriteDirection"] = isMapPage ? "192" : "64"; // 0x80 selects the map page; the low bits are the facing.
        record.CustomProperties["PrefabAssetId"] = prefabId.ToString();

        var withMap = EntitySpawnFactory.CreateEntityFromRecord(record, _ => PrefabWithController(), Catalog.Value, tileMapData: Map(MapGravity, MapViscosity));
        var withoutMap = EntitySpawnFactory.CreateEntityFromRecord(record, _ => PrefabWithController(), Catalog.Value);

        Assert.Equal(
            (new State(MapGravity, MapViscosity, expectedMask, Owned: true, MinMove: 0f), Untouched()),
            (Read(Assert.IsType<AlundraEntityScriptProxy>(withMap.GameplayProxy)), Read(Assert.IsType<AlundraEntityScriptProxy>(withoutMap.GameplayProxy))));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c) P3 of E20.e0a3b, green by construction since E20.e0a3b: the guard of D-E0A-8 has TWO conditions.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Collection(AlundraMusicPlayerSingletonCollection.Name)]
    public sealed class MarkedEntityPins
    {
        private static ArcRun Arc(string name) => new(new ArcSpec("E20e0k1-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

        /// <summary>The horizontal displacement of one scripted tick of a marked arrow, given the animation the entity says is its current one and the one it asks for.</summary>
        private static int OneTick(ArcRun arc, uint current, uint target)
        {
            var proxy = ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, false, 3, 150 << 16, 100 << 16, 0, 0x18)!;
            Assert.True(proxy.JoinsBirthTick);
            arc.OneFrame(); // the first frame validates the pending switch: the current animation is 0 from here on.
            Assert.Equal(0u, proxy.CurrentAnimationId);
            proxy.CurrentAnimationId = current;
            proxy.TargetAnimationId = target;
            var before = proxy.PosX;
            ScriptedMotion.TickScriptedNpc(proxy);
            return proxy.PosX - before;
        }

        [Fact]
        public void P3_AMarkedEntityWhoseFirstAnimationIsValidated_KeysItsKinematicsOnItsCurrentAnimation_NotOnAnAskedOne()
        {
            using var arc = Arc("p3");
            var sets = ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, false, 3, 150 << 16, 100 << 16, 0, 0x18)!.AnimSetsByAnim!;
            var otherAnimation = sets.Where(kv => kv.Key != 0 && kv.Value.Speed != sets[0].Speed).Select(kv => (uint)kv.Key).First();

            var kept = OneTick(arc, current: 0, target: 0);            // control: current and target agree.
            var asked = OneTick(arc, current: 0, target: otherAnimation); // the target changed after the validation: the speed of the CURRENT animation still applies.
            var witness = OneTick(arc, current: otherAnimation, target: otherAnimation); // the other animation really moves at another speed.

            Assert.NotEqual(kept, witness); // the pin discriminates (otherwise it would pass whatever the guard).
            Assert.Equal(kept, asked);
        }
    }
}
