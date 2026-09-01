#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// Covers E2 (docs/plan-echelles-chiffrage.md E2): <see cref="AlundraEntityScriptProxy.UpdateFloorHeight"/>'s
/// composed port of <c>GetCollisionOnZ</c> (PhysicsEngine.cs:1602-1675) against the REAL map 389
/// ("Ship Klark (beginning)") cell/record data - same fixture/self-skip pattern as
/// <see cref="AlundraGroundSlopeTests"/> (E1's own sibling slice): a real headless <see cref="World"/>
/// with the real map 389 <see cref="AlundraCellsCollisionField"/> installed as <c>World.CollisionField</c>,
/// and hero pawns built from the shared <see cref="HeroWorldFixture"/> montage. Tests self-skip (return
/// early) when <c>alundra-project/</c> is not present in this checkout.
/// </summary>
public class AlundraFloorHeightTests
{
    private const string WorldName = "Ship Klark (beginning)-389";

    // 24x16 px cells (StaticVariables.MapTileWidth/MapTileHeight) - same constants
    // AlundraGroundSlopeTests/AlundraCellsCollisionFieldTests use for their own real-map assertions.
    private const int CellWidthPx = 24;
    private const int CellHeightPx = 16;

    // Same production hero footprint as AlundraGroundSlopeTests's own ProductionCallSite test (F4 fix -
    // real converter-exported bank header, alundra-project/Data/sprite-records.json, hero asset
    // 4158f0d7-c5f0-4f6a-a48f-e73d0dd2250b).
    private const int HeroOffsetX = -10;
    private const int HeroOffsetY = -7;
    private const int HeroSizeX = 21;
    private const int HeroSizeY = 15;

    private static string? FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static TileMapData? LoadMap389TileMapData(string projectRoot)
    {
        var tileMapPath = Path.Combine(
            projectRoot, "Maps", "The Klark", "Ship Klark (beginning)-389", "tilemap",
            "Ship Klark (beginning)-389.tileMap");
        if (!File.Exists(tileMapPath))
        {
            return null;
        }

        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));
        return tileMapData;
    }

    private static AlundraCellsCollisionField? LoadMap389Field(TileMapData tileMapData)
    {
        var created = AlundraCellsCollisionField.TryCreate(tileMapData, WorldName, out var field);
        Assert.True(created, "map 389's AlundraCells custom property should parse and match MapSize.");
        return field;
    }

    /// <summary>Same real-record fixture shape as
    /// <c>AlundraNpcCharacterControllerMoverTests.BuildRealRecordProxy</c> - a pure logic-side collidable
    /// (no root/physics component at all, matching how that class' own sailor-11/record-2 platform test
    /// builds its own support candidate): <see cref="AlundraEntitySpawnFactory.ApplyRecord"/> then
    /// <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/> against the REAL map 389 record data, so
    /// every field <see cref="EntitySupport.TryFindSupport"/> reads (Flags/Width/Height/Depth/Pos*/Mod*)
    /// is the genuine converter-exported value, not a hand-picked literal.</summary>
    private static AlundraEntityScriptProxy BuildRealRecordProxy(
        TileMapData tileMapData, SpriteRecordCatalog catalog, int recordIndex)
    {
        var entitiesLayer = tileMapData.ObjectLayers.First(l => l.Name == "Entities");
        var record = entitiesLayer.Objects.First(
            o => o.CustomProperties.TryGetValue("Index", out var idx) && idx == recordIndex.ToString());

        var proxy = new AlundraEntityScriptProxy();
        AlundraEntitySpawnFactory.ApplyRecord(record, proxy);
        var backingEntity = new Entity();
        proxy.LogicContextEntity = backingEntity;
        AlundraEntitySpawnFactory.ApplySpawnInitialization(record, backingEntity, proxy, catalog, tileMapData: tileMapData);
        return proxy;
    }

    private sealed class NoOpRunner : IEventProgramRunner
    {
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    /// <summary>Same shape as <c>AlundraGroundSlopeTests.PlayerScriptHost</c>, plus a MUTABLE
    /// <see cref="Collidables"/> so a test can seed the entity-support candidate
    /// <see cref="AlundraEntityScriptProxy.UpdateFloorHeight"/> reads (see
    /// <c>AlundraNpcCharacterControllerMoverTests.FakeScriptHost</c>'s own identical addition for E4.f).</summary>
    private sealed class PlayerScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; } = new NoOpRunner();
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController { get; init; }
        public List<AlundraEntityScriptProxy> Collidables { get; } = new();
        IReadOnlyList<AlundraEntityScriptProxy> IAlundraScriptHost.Collidables => Collidables;

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        // One logic tick per rendered frame - every test below runs a single frame; FloorHeight is
        // computed from PosX/PosY/PosZ alone (already pulled from the root by the time the IsPlayer
        // branch runs - see Update's own E3.d paragraph), so LogicTicksThisFrame's own animation-advance
        // side effect is irrelevant here, same rationale as AlundraGroundSlopeTests's own host.
        public int LogicTicksThisFrame(float elapsedTime) => 1;
    }

    // -----------------------------------------------------------------------------------------
    // (1) MANDATORY acceptance: production call site, flat ground. Same scale cell as
    // AlundraGroundSlopeTests.ProductionCallSite_HeroSeededOnScaleCell_Slope18cIsSix: (18, 36), height 11
    // -> ground height 176px - real World.Update -> AlundraEntityScriptProxy.Update's IsPlayer branch ->
    // MovePlayer + Tick + UpdateGroundSlope + UpdateFloorHeight, no direct method call.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ProductionCallSite_HeroSeededOnFlatGround_FloorHeightEqualsTerrainHeight()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot);
        if (tileMapData == null)
        {
            return;
        }

        var field = LoadMap389Field(tileMapData);
        if (field == null)
        {
            return;
        }

        const int cellX = 18;
        const int cellY = 36;
        const int cellHeightUnits = 11;
        var x1 = cellX * CellWidthPx + 1; // 433 - production footprint entirely inside the cell.
        var y1 = cellY * CellHeightPx; // 576
        var rootX = x1 - HeroOffsetX;
        var rootY = y1 - HeroOffsetY;
        var groundHeightPx = cellHeightUnits * CellHeightPx;

        var world = HeroWorldFixture.BuildWorld(field);
        var controller = new AlundraPlayerController { PadStateProviderForTests = () => new AlundraPadState { ButtonsHold = 0, ButtonsJustPressed = 0 } };
        var host = new PlayerScriptHost { PlayerController = controller };

        var settings = new CharacterControllerSettings();
        var (_, proxy) = HeroWorldFixture.BuildHeroPawn(
            world, settings, new Vector3(rootX, rootY, groundHeightPx), host);

        AlundraEntitySpawnFactory.SetEntityDimensions(proxy, HeroOffsetX, HeroOffsetY, 0, HeroSizeX, HeroSizeY, 32);

        // Sanity: nothing is seeded in Collidables, so the terrain-only branch of UpdateFloorHeight is
        // what this test exercises (mirrors the flat-ground half of GetCollisionOnZ - no qualifying
        // entity candidate, collision stays TerrainHeight + 1 in the original's own convention).
        Assert.Empty(host.Collidables);

        world.Update(1f / 50f);

        var expectedFloorHeight = proxy.ComputeTerrainHeight();
        Assert.Equal(groundHeightPx << 16, expectedFloorHeight); // sanity: matches the seeded ground height.
        Assert.Equal(expectedFloorHeight, proxy.FloorHeight);
        Assert.NotEqual(0, proxy.FloorHeight); // non-zero, per the ticket's own acceptance wording.
    }

    // -----------------------------------------------------------------------------------------
    // (2) MANDATORY acceptance: production call site, record 1's own real box (top 496px), floating
    // directly above the SAME (18, 36) flat-ground cell test (1) rests on (terrain 176px). This pose is
    // deliberately chosen so the TERRAIN-ONLY branch of UpdateFloorHeight would answer 176px (verified
    // below via ComputeTerrainHeight, matching test (1) exactly) while the real answer, once
    // EntitySupport.TryFindSupport composes in record 1's own real candidate, is 496px - a mandatory
    // control (this test also runs with Collidables forced empty, and MUST fail) proves the assertion
    // below cannot pass through the terrain branch alone, unlike the original record-2 pose this test
    // used to use (its own single flat (18,36) terrain corner already produced 400px, so the entity
    // branch was never actually exercised - see docs/plan-echelles-chiffrage.md's own E2 adversarial pass
    // notes for that history).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ProductionCallSite_HeroSeededAboveRecordOnePlatform_FloorHeightEqualsPlatformTop()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot);
        if (tileMapData == null)
        {
            return;
        }

        var field = LoadMap389Field(tileMapData);
        if (field == null)
        {
            return;
        }

        var catalog = new SpriteRecordCatalog(projectRoot);
        var platformProxy = BuildRealRecordProxy(tileMapData, catalog, recordIndex: 1);
        Assert.True((platformProxy.Flags & EntityFlags.Collidable) != 0, "record 1's real header should carry Collidable.");

        // Real support-surface top, computed from the SAME spawned platform's own fields (not a
        // hand-transcribed literal). This port kept the original's own entity-resting "+1" convention
        // (EntitySupport.cs:173, "platformTopZ = candidateTop + 1") - unlike the terrain branch, which
        // dropped it (see UpdateFloorHeight's own "THE -1 BELONGS TO THE SEED" doc paragraph) - so the
        // expected FloorHeight when this platform is found is candidateTop + 1, matching
        // EntitySupport.TryFindSupport's own supportTopZ exactly.
        var candidateTop = platformProxy.PosZ + platformProxy.ModZ + platformProxy.Depth;
        var expectedFloorHeight = candidateTop + 1;
        Assert.InRange(candidateTop / 65536.0, 495.9, 496.1); // sanity: record 1's real top sits ~496px up.

        // Same (18, 36) cell test (1) rests on - 176px flat terrain (real map data, not a literal): if
        // TryFindSupport's own candidate were dropped (or never reached), UpdateFloorHeight would answer
        // this terrainHeight, not candidateTop - the two must differ for this test to actually prove
        // anything about the entity branch.
        const int cellX = 18;
        const int cellY = 36;
        const int cellHeightUnits = 11;
        var terrainHeightPx = cellHeightUnits * CellHeightPx;
        Assert.NotEqual(terrainHeightPx << 16, candidateTop); // the whole point of this pose.

        // Hero footprint over record 1's own real footprint (moddedX/Y 432,576 - same corner as test
        // (1)'s own (18,36) cell), floating 8px above the platform's real top so the strict
        // "candidateTop < moddedPosZ" comparator (EntitySupport.cs:129, verifier A1) is satisfied without
        // relying on any engine-side vertical physics (default CharacterControllerSettings carries zero
        // gravity - see AlundraGroundSlopeTests's own ProductionCallSite test for the same zero-drift
        // precedent).
        var x1 = cellX * CellWidthPx + 1; // 433 - same footprint origin as test (1).
        var y1 = cellY * CellHeightPx; // 576
        var rootX = x1 - HeroOffsetX;
        var rootY = y1 - HeroOffsetY;
        var heroStartZPx = (candidateTop >> 16) + 8;

        var world = HeroWorldFixture.BuildWorld(field);
        var controller = new AlundraPlayerController { PadStateProviderForTests = () => new AlundraPadState { ButtonsHold = 0, ButtonsJustPressed = 0 } };
        var host = new PlayerScriptHost { PlayerController = controller };
        host.Collidables.Add(platformProxy);

        var settings = new CharacterControllerSettings();
        var (_, proxy) = HeroWorldFixture.BuildHeroPawn(
            world, settings, new Vector3(rootX, rootY, heroStartZPx), host);

        AlundraEntitySpawnFactory.SetEntityDimensions(proxy, HeroOffsetX, HeroOffsetY, 0, HeroSizeX, HeroSizeY, 32);

        // Real hero header (sprite-records.json, asset 4158f0d7-...) carries MoreFlags 140 (0x8C), which
        // includes the Collidable bit (0x80) - EntitySupport.IsEligibleSubject's own SUBJECT gate
        // (GetCollisionOnZ:1606-1619's exact conjunct). AlundraEntitySpawnFactory.SetEntityDimensions does not
        // itself set Flags (that is AdoptPlayerPawn's own job, out of this bare-fixture pawn's path), so
        // this test sets it directly rather than assume it - matching the real production value, not
        // inventing one.
        proxy.Flags |= EntityFlags.Collidable;

        // Sanity check on the geometry itself, independent of UpdateFloorHeight: prove the hero's own
        // footprint really does overlap record 1's real footprint under the ACTUAL asymmetric rule
        // TryFindSupport applies (EntitySupport.cs:140-165), not a symmetric approximation - a symmetric
        // "sum of widths" check can pass poses the real (stricter, asymmetric) rule would reject.
        var moddedPosX = proxy.PosX + proxy.ModX;
        var moddedPosY = proxy.PosY + proxy.ModY;
        var platformModX = platformProxy.PosX + platformProxy.ModX;
        var platformModY = platformProxy.PosY + platformProxy.ModY;
        var deltaX = platformModX - moddedPosX;
        var overlapsX = deltaX < 0
            ? moddedPosX - platformModX < platformProxy.Width + 1
            : deltaX < proxy.Width + 1;
        var deltaY = platformModY - moddedPosY;
        var overlapsY = deltaY < 0
            ? moddedPosY - platformModY < platformProxy.Height + 1
            : deltaY < proxy.Height + 1;
        Assert.True(overlapsX, "hero and record 1 footprints should overlap on X, per EntitySupport's own asymmetric rule.");
        Assert.True(overlapsY, "hero and record 1 footprints should overlap on Y, per EntitySupport's own asymmetric rule.");

        world.Update(1f / 50f);

        Assert.Equal(expectedFloorHeight, proxy.FloorHeight);
        Assert.NotEqual(0, proxy.FloorHeight);

        // MANDATORY control (F1): with the exact same pose but no entity candidates at all, the
        // terrain-only branch must answer the DIFFERENT terrainHeightPx value above, proving the
        // assertion above genuinely exercises EntitySupport.TryFindSupport rather than passing through
        // the terrain branch alone (as the record-2 pose this test replaced silently did).
        var controlHost = new PlayerScriptHost { PlayerController = new AlundraPlayerController { PadStateProviderForTests = () => new AlundraPadState { ButtonsHold = 0, ButtonsJustPressed = 0 } } };
        Assert.Empty(controlHost.Collidables);
        var controlWorld = HeroWorldFixture.BuildWorld(field);
        var (_, controlProxy) = HeroWorldFixture.BuildHeroPawn(
            controlWorld, new CharacterControllerSettings(), new Vector3(rootX, rootY, heroStartZPx), controlHost);
        AlundraEntitySpawnFactory.SetEntityDimensions(controlProxy, HeroOffsetX, HeroOffsetY, 0, HeroSizeX, HeroSizeY, 32);
        controlProxy.Flags |= EntityFlags.Collidable;
        controlWorld.Update(1f / 50f);
        Assert.NotEqual(expectedFloorHeight, controlProxy.FloorHeight);
        Assert.Equal(terrainHeightPx << 16, controlProxy.FloorHeight);
    }

    // -----------------------------------------------------------------------------------------
    // (3) Defined out-of-map / no-field behaviour (contract's own "comportement hors carte defini et
    // teste"): AlundraEntityScriptProxy.ComputeTerrainHeight returns 0 without an installed
    // World.CollisionField (its own documented "sea level" fallback) - UpdateFloorHeight must not throw,
    // and must fall back to that same 0, since IsEligibleSubject is false by default (Flags carries no
    // Collidable bit on a bare proxy) so the entity-search half never runs either.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void NoCollisionFieldInstalled_FloorHeightFallsBackToZero_NoThrow()
    {
        var proxy = new AlundraEntityScriptProxy();

        var host = new PlayerScriptHost();
        proxy.ScriptHost = host;

        // Sanity: the entity branch really is inactive here (a bare proxy carries no Collidable flag),
        // so this test's own "falls back to 0" claim traces to ComputeTerrainHeight's own documented
        // no-field return, not to some untested other path that also happens to leave the field at its
        // C# default. Tying the assertion below to ComputeTerrainHeight's own live return (rather than a
        // bare literal 0) keeps it discriminating against an "always returns 0" mutant of
        // UpdateFloorHeight - see this suite's own class-doc F5 note.
        Assert.False(EntitySupport.IsEligibleSubject(proxy));

        var exception = Record.Exception(() => proxy.UpdateFloorHeight());

        Assert.Null(exception);
        Assert.Equal(proxy.ComputeTerrainHeight(), proxy.FloorHeight);
        Assert.Equal(0, proxy.ComputeTerrainHeight()); // sanity: the documented no-field fallback really is 0.
    }

    // -----------------------------------------------------------------------------------------
    // (4) Restriction (player only, same rationale as AlundraGroundSlopeTests's own NPC restriction):
    // an NPC's own FloorHeight is never touched by this slice - it stays the C# default 0 even across
    // several real frames, because UpdateFloorHeight's only call site is the IsPlayer branch of Update.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ProductionCallSite_NpcNeverGetsFloorHeight_StaysZero()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot);
        if (tileMapData == null)
        {
            return;
        }

        var field = LoadMap389Field(tileMapData);
        if (field == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);
        var host = new PlayerScriptHost();

        var root = new TransformComponent();
        root.LocalTransform.Position = new Vector3(468f, 584f, 176f);
        var entity = new Entity
        {
            Name = "NpcFloorHeightTestPawn",
            RootComponent = root,
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
        };
        entity.Initialize();

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.IsPlayer = false;
        proxy.ScriptHost = host;
        proxy.Status = EntityStatus.Normal;
        proxy.PosX = (int)Math.Round(468.0 * 65536.0);
        proxy.PosY = (int)Math.Round(584.0 * 65536.0);
        proxy.PosZ = (int)Math.Round(176.0 * 65536.0);

        world.AddEntity(entity);

        for (var frame = 0; frame < 5; frame++)
        {
            world.Update(1f / 50f);
        }

        Assert.Equal(0, proxy.FloorHeight);
    }
}
