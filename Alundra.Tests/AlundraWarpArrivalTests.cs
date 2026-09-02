#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// T5 (docs/plan-transitions-carte.md §3 T5, D-T-4/D-T-7): <c>AlundraWorldProxy.AdoptPlayerPawn</c>
/// consuming <see cref="AlundraWarpDirector"/>'s pending arrival record instead of always writing the New
/// Game constants, and <c>InstallScreenFadeSystems</c> transporting the arrival's own transition effect
/// id into the log (D-T-7: reduced to effect 0 regardless).
///
/// <c>AdoptPlayerPawn</c> is PRIVATE and only reached past <see cref="AlundraWorldProxy.InitializeWithWorld"/>'s
/// two early returns (a real "tileMap" entity with loaded <see cref="TileMapData"/>) - same montage as
/// <see cref="AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World"/>, built independently here for the
/// REAL map 390 export (this chantier's own arrival map, §1.1.d), plus a hand-built hero pawn possessed by
/// a real <see cref="AlundraPlayerController"/> registered into <c>World.PlayerControllers</c> by
/// reflection (the engine's own <c>InitializePlayerControllers</c> needs a full asset-catalog pawn prefab
/// this headless montage does not have - same reflection-into-a-private-field precedent already used by
/// this project's own <c>AlundraWarpDepartureTests.GetPendingWorldToLoad</c>).
///
/// Every test resets the FOUR session singletons this class' own montages touch (D-T-14).
/// </summary>
public sealed class AlundraWarpArrivalTests : IDisposable
{
    public AlundraWarpArrivalTests()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        AlundraScreenFadeDirector.Instance.ResetForTests();
        AlundraMusicPlayer.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        AlundraScreenFadeDirector.Instance.ResetForTests();
        AlundraMusicPlayer.Instance.ResetForTests();
    }

    // -----------------------------------------------------------------------------------------------
    // Fixture plumbing - real map 390 (this chantier's own arrival map, §1.1.d), same montage shape as
    // AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World (that class' own helpers are private).
    // -----------------------------------------------------------------------------------------------

    private const string WorldName = "Ship Klark (inner)-390";
    private const string TileMapEntityName = "tileMap";
    private const int RenderLayerCount = 4;

    private static string FindProjectRoot()
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

        throw new InvalidOperationException(
            $"AlundraWarpArrivalTests: no 'alundra-project/Maps' directory found above "
            + $"'{AppContext.BaseDirectory}' - this test needs the real converter export of map 390.");
    }

    private static TileMapData LoadRealMap390TileMapData(string projectRoot)
    {
        var tileMapPath = Path.Combine(
            projectRoot, "Maps", "The Klark", "Ship Klark (inner)-390", "tilemap",
            "Ship Klark (inner)-390.tileMap");
        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));
        return tileMapData;
    }

    private static TileSetData LoadRealVisualTileSet(string projectRoot, TileMapData tileMapData)
    {
        var assetInfos = JObject.Parse(File.ReadAllText(Path.Combine(projectRoot, "AssetInfos.json")));
        var pathById = new Dictionary<Guid, string>();
        foreach (var entry in (JArray)assetInfos["asset_infos"]!)
        {
            if (Guid.TryParse((string?)entry["id"], out var id) && (string?)entry["file_name"] is { } fileName)
            {
                pathById[id] = fileName;
            }
        }

        var visualAssetId = tileMapData.TileSetDataAssetIds[0];
        Assert.True(pathById.TryGetValue(visualAssetId, out var relativePath), "AssetInfos.json missing map 390's visual tileset.");
        var fullPath = Path.Combine(projectRoot, relativePath!.Replace('\\', Path.DirectorySeparatorChar));

        var tileSetData = new TileSetData();
        tileSetData.Load(JObject.Parse(File.ReadAllText(fullPath)));
        return tileSetData;
    }

    private static List<TileMapLayer> GetLayers(TileMapComponent component)
    {
        var property = typeof(TileMapComponent).GetProperty("Layers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return (List<TileMapLayer>)property!.GetValue(component)!;
    }

    private static List<T> GetPrivateList<T>(TileMapComponent component, string fieldName)
    {
        var field = typeof(TileMapComponent).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (List<T>)field!.GetValue(component)!;
    }

    private static void InvokeBuildChunks(TileMapComponent component, TileMapLayer layer, int layerIndex)
    {
        var method = typeof(TileMapComponent).GetMethod("BuildChunks", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(component, new object[] { layer, layerIndex });
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class StubTile : Tile
    {
        public StubTile() : base(null)
        {
        }

        public override void Update(float elapsedTime)
        {
        }

        public override void Draw(float x, float y, float z, Vector2 scale)
        {
        }

        public override void Draw(float x, float y, float z, Rectangle uvOffset, Vector2 scale)
        {
        }

        public override Rectangle GetCurrentSourceRectangle() => Rectangle.Empty;
    }

    /// <summary>Same headless (no GraphicsDevice) real-map <see cref="TileMapComponent"/> montage as
    /// <see cref="AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World"/>, for map 390 instead of 389 -
    /// this chantier's own arrival map (§1.1.d).</summary>
    private static World BuildRealMap390World()
    {
        var projectRoot = FindProjectRoot();
        var tileMapData = LoadRealMap390TileMapData(projectRoot);
        var tileSetData = LoadRealVisualTileSet(projectRoot, tileMapData);

        var world = new World { Name = WorldName };
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());
        SetProperty(world, nameof(World.Game), game);

        var tileMapEntity = new Entity { Name = TileMapEntityName };
        SetProperty(tileMapEntity, nameof(Entity.World), world);

        var component = new TileMapComponent();
        tileMapEntity.RootComponent = component;
        component.TileMapData = tileMapData;
        component.TileSetData = tileSetData;

        GetPrivateList<TileSetData>(component, "_tileSets").Add(tileSetData);
        GetPrivateList<Texture2D>(component, "_tileSetTextures").Add(null!);

        var runtimeLayers = GetLayers(component);
        for (var layerIndex = 0; layerIndex < RenderLayerCount; layerIndex++)
        {
            var layerData = tileMapData.Layers[layerIndex];
            var layer = new TileMapLayer(layerData);
            var cellCount = tileMapData.MapSize.Width * tileMapData.MapSize.Height;
            for (var i = 0; i < cellCount; i++)
            {
                layer.Tiles.Add(new StubTile());
                layer.CollisionObjects.Add(null);
            }

            runtimeLayers.Add(layer);
            InvokeBuildChunks(component, layer, layerIndex);
        }

        world.Entities.Add(tileMapEntity);
        return world;
    }

    /// <summary>
    /// Hand-built hero pawn - root <see cref="TransformComponent"/> plus sibling <see cref="CollisionComponent"/>
    /// (Box 21x15x32, local_position (0.5,0.5,16), same fixture as <see cref="HeroWorldFixture.BuildHeroPawn"/>)
    /// so <see cref="AlundraEntityScriptProxy.ClampToGround"/> has a real box fixture to sample the ground
    /// under - no <see cref="CharacterControllerComponent"/> (not needed by ClampToGround itself, and
    /// AdoptPlayerPawn already tolerates a null Controller, logging instead of throwing).
    /// </summary>
    private static (Entity Entity, AlundraEntityScriptProxy Proxy) BuildHeroPawnEntity(World world)
    {
        var root = new TransformComponent();
        var collisionComponent = new CollisionComponent();
        collisionComponent.Fixtures.Add(new ColliderFixture
        {
            Shape = new Box { Size = new Vector3(21f, 15f, 32f) },
            LocalPosition = new Vector3(0.5f, 0.5f, 16f),
            LocalRotation = Quaternion.Identity,
        });
        root.AddChildComponent(collisionComponent);

        var entity = new Entity
        {
            Name = "AlundraHeroTestPawn",
            RootComponent = root,
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
        };
        entity.Initialize();
        SetProperty(entity, nameof(Entity.World), world); // ClampToGround reads Owner.World.CollisionField.

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        return (entity, proxy);
    }

    /// <summary>Possesses <paramref name="pawn"/> with a real <see cref="AlundraPlayerController"/> and
    /// registers it into <paramref name="world"/>'s own <c>World.PlayerControllers</c> by reflection on
    /// the private backing list (<c>World.InitializePlayerControllers</c> needs a full asset-catalog pawn
    /// prefab this headless montage does not build) - <c>AdoptPlayerPawn</c>'s own
    /// <c>world.PlayerControllers.OfType&lt;AlundraPlayerController&gt;().FirstOrDefault()</c> lookup then
    /// finds it exactly as it would the engine-spawned one.</summary>
    private static void RegisterPlayerController(World world, Entity pawn)
    {
        var controller = new AlundraPlayerController();
        controller.Possess(pawn);

        var field = typeof(World).GetField("_playerControllers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var list = (List<PlayerController>)field!.GetValue(world)!;
        list.Add(controller);
    }

    /// <summary>Portal 0 of map 389 (§1.1.c/§1.1.d): mono-cell (18,38), destination 390 tile (10,40),
    /// arrival direction index 1 -&gt; <c>AnimationTables.CardinalDirectionTable[1] == 0x10</c>.</summary>
    private static AlundraPortalRecord Map389Portal0(int transitionEffectId = 0) => new()
    {
        Index = 0,
        X1 = 18,
        Y1 = 38,
        X2 = 18,
        Y2 = 38,
        DestMapId = 390,
        DestTileX = 10,
        DestTileY = 40,
        ZLevel = 0,
        // Flags 0x5001 carries TransitionEffect 0 (bits 4-6, AlundraPortalRecord.TransitionEffectId) - a
        // non-zero id (domain 0..7) is injected separately by ORing it in, keeping
        // RequiredFacing/ArrivalDirection/WarpBehavior identical to the real record.
        Flags = 0x5001 | (transitionEffectId << 4),
    };

    private static AlundraEntityScriptProxy NewDeparturePlayer(int posXPixels, int posYPixels)
    {
        var entity = new Entity { Name = "player", GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        entity.Initialize();
        var player = (AlundraEntityScriptProxy)entity.GameplayProxy;
        player.IsPlayer = true;
        player.Status = EntityStatus.Normal;
        player.PosX = posXPixels << 16;
        player.PosY = posYPixels << 16;
        player.PosZ = 0;
        return player;
    }

    /// <summary>Arms the warp director's pending arrival exactly like a real departure through portal 0 of
    /// map 389 would (§1.2.c arithmetic - player standing on the source tile's own centre, so
    /// deltaX/deltaY reduce to (DestTileX*24, DestTileY*16)), without needing the full T4 departure-world
    /// montage (<see cref="AlundraWarpDepartureTests"/> already covers that arithmetic end to end).</summary>
    private static void ArmPendingArrival(int transitionEffectId = 0)
    {
        var departurePlayer = NewDeparturePlayer(posXPixels: 18 * 24 + 12, posYPixels: 38 * 16 + 8);
        AlundraWarpDirector.Instance.BeginDeparture(
            Map389Portal0(transitionEffectId), arrivalDirectionId: 0x10, departurePlayer, new AlundraGameState());
    }

    /// <summary>Captures every <see cref="Logs.WriteInfo"/> call for one test, then unregisters itself
    /// (<see cref="Logs"/> has no public removal API - same private-field reflection precedent as this
    /// class' own <see cref="RegisterPlayerController"/>) so it does not leak into later tests.</summary>
    private sealed class CapturingLogger : ILogger, IDisposable
    {
        public List<string> InfoMessages { get; } = new();

        public void Close()
        {
        }

        public void WriteTrace(string msg)
        {
        }

        public void WriteDebug(string msg)
        {
        }

        public void WriteInfo(string msg) => InfoMessages.Add(msg);

        public void WriteWarning(string msg)
        {
        }

        public void WriteError(string msg)
        {
        }

        public static CapturingLogger Install()
        {
            var logger = new CapturingLogger();
            Logs.AddLogger(logger);
            return logger;
        }

        public void Dispose()
        {
            var field = typeof(Logs).GetField("_loggers", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            ((List<ILogger>)field!.GetValue(null)!).Remove(this);
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Acceptance chiffrée (element 1, D-T-4): arrival on map 390, tile (10,40).
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void AdoptPlayerPawn_WithPendingArrival_ConsumesRecord_ClampsToGround_AndClearsHasPendingArrival()
    {
        ArmPendingArrival();
        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);

        var world = BuildRealMap390World();
        var (heroEntity, _) = BuildHeroPawnEntity(world);
        RegisterPlayerController(world, heroEntity);

        var proxy = new AlundraWorldProxy();

        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = FindProjectRoot();
        try
        {
            proxy.InitializeWithWorld(world);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }

        var hero = Assert.IsType<AlundraEntityScriptProxy>(heroEntity.GameplayProxy);

        // Position (10*24+12, 40*16+8) << 16, before ClampToGround raises PosZ.
        Assert.Equal((10 * 24 + 12) << 16, hero.PosX);
        Assert.Equal((40 * 16 + 8) << 16, hero.PosY);

        // §1.1.f/§1.4.e: ClampToGround raises PosZ from the portal's own ZLevel<<20 (0) up to the
        // destination cell's real ground height (4*16<<16 = 4194304) - the mutation "sauter ClampToGround"
        // is demonstrably non-vide, exactly as the plan's own acceptance measures it.
        Assert.Equal(4 * 16 << 16, hero.PosZ);
        Assert.Equal(4194304, hero.PosZ);

        Assert.Equal(0x36u, hero.TargetAnimationId);
        // §1.2.c's own note: index 1 gives 0x10, never the raw index 1.
        Assert.Equal(0x10u, hero.TargetDirection);
        Assert.Equal(AnimationTables.CardinalDirectionTable[1], hero.TargetDirection);
        // EntityManager.cs:85-88 - bit-complemented so the very first per-frame animation sync fires.
        Assert.Equal(~0x10u, hero.CurrentDirection);

        // [R9]: consumed, not just read - a LATER, non-warp map entry must never see this record again.
        Assert.False(AlundraWarpDirector.Instance.HasPendingArrival);
    }

    [Fact]
    public void AdoptPlayerPawn_WithPendingArrival_ArmsTheIncomingFadeFromBlackOverSixteenTicks()
    {
        ArmPendingArrival();

        var world = BuildRealMap390World();
        var (heroEntity, _) = BuildHeroPawnEntity(world);
        RegisterPlayerController(world, heroEntity);

        var proxy = new AlundraWorldProxy();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = FindProjectRoot();
        try
        {
            proxy.InitializeWithWorld(world);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }

        // §1.4.f/D-T-15: InstallScreenFadeSystems (called from InitializeWithWorld, before AdoptPlayerPawn)
        // arms the incoming fade unconditionally on every map entry, warp arrival or not - subtractive,
        // 0xff0000 -> 0, 16 ticks (AlundraScreenFadeDirector.InstallForMapEntry's own doc).
        Assert.False(AlundraScreenFadeDirector.Instance.IsSettled);
        for (var tick = 0; tick < 15; tick++)
        {
            AlundraScreenFadeDirector.Instance.Advance(1);
            Assert.False(AlundraScreenFadeDirector.Instance.IsSettled);
        }

        AlundraScreenFadeDirector.Instance.Advance(1);
        Assert.True(AlundraScreenFadeDirector.Instance.IsSettled);
    }

    // -----------------------------------------------------------------------------------------------
    // [R9]: map entry with NO prior departure falls back to the New Game constants, not a stale record.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void AdoptPlayerPawn_WithNoPendingArrival_FallsBackToNewGameConstants()
    {
        Assert.False(AlundraWarpDirector.Instance.HasPendingArrival); // fresh session, ResetForTests above.

        var world = BuildRealMap390World();
        var (heroEntity, _) = BuildHeroPawnEntity(world);
        RegisterPlayerController(world, heroEntity);

        var proxy = new AlundraWorldProxy();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = FindProjectRoot();
        try
        {
            proxy.InitializeWithWorld(world);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }

        var hero = Assert.IsType<AlundraEntityScriptProxy>(heroEntity.GameplayProxy);

        Assert.Equal((AlundraGameState.CameraTileX * 24 + 12) << 16, hero.PosX);
        Assert.Equal((AlundraGameState.CameraTileY * 16 + 8) << 16, hero.PosY);
        Assert.Equal(AlundraGameState.ResetAnimationId, hero.TargetAnimationId);
        Assert.Equal(AlundraGameState.ResetDirectionId, hero.TargetDirection);
        Assert.False(AlundraWarpDirector.Instance.HasPendingArrival);
    }

    /// <summary>Mutation "retomber sur les constantes New Game alors qu'un enregistrement existe": with a
    /// pending arrival present, the pose must NOT be the New Game tile (33,59) - exercised directly against
    /// the real acceptance montage above (portal 0 targets tile (10,40), New Game targets (33,59), the two
    /// are never equal), so no separate test is needed to falsify this mutation.</summary>
    [Fact]
    public void AdoptPlayerPawn_WithPendingArrival_DoesNotFallBackToNewGameTile()
    {
        ArmPendingArrival();

        var world = BuildRealMap390World();
        var (heroEntity, _) = BuildHeroPawnEntity(world);
        RegisterPlayerController(world, heroEntity);

        var proxy = new AlundraWorldProxy();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = FindProjectRoot();
        try
        {
            proxy.InitializeWithWorld(world);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }

        var hero = Assert.IsType<AlundraEntityScriptProxy>(heroEntity.GameplayProxy);
        var newGamePosX = (AlundraGameState.CameraTileX * 24 + 12) << 16;
        var newGamePosY = (AlundraGameState.CameraTileY * 16 + 8) << 16;

        Assert.NotEqual(newGamePosX, hero.PosX);
        Assert.NotEqual(newGamePosY, hero.PosY);
    }

    // -----------------------------------------------------------------------------------------------
    // Element 2 (D-T-7, §1.4.g): InstallScreenFadeSystems transports + logs the arrival's own effect id.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void InstallScreenFadeSystems_WithNonZeroArrivalEffectId_LogsTheTransportedIdReducedToEffectZero()
    {
        ArmPendingArrival(transitionEffectId: 5);
        Assert.Equal(5, AlundraWarpDirector.Instance.ArrivalRecordForTests.EffectId);

        var world = BuildRealMap390World();
        var proxy = new AlundraWorldProxy();

        using var logger = CapturingLogger.Install();
        proxy.InstallScreenFadeSystems(world);

        Assert.Contains(logger.InfoMessages, m => m.Contains("effect id 5") && m.Contains("reduced to effect 0"));

        // D-T-7: transported and logged, but the fade machine armed is unconditionally effect 0 regardless
        // - the same 16-tick subtractive-to-black incoming fade every map entry arms (§1.4.f), not settled
        // the instant InstallForMapEntry runs.
        Assert.False(AlundraScreenFadeDirector.Instance.IsSettled);
    }

    /// <summary>Mutation "ignorer l'id transporté": with NO pending arrival (effect id peeks as 0), nothing
    /// is logged - proving the log line above is really keyed off the transported id, not unconditional.</summary>
    [Fact]
    public void InstallScreenFadeSystems_WithNoPendingArrival_LogsNothingAboutATransitionEffect()
    {
        Assert.False(AlundraWarpDirector.Instance.HasPendingArrival);

        var world = BuildRealMap390World();
        var proxy = new AlundraWorldProxy();

        using var logger = CapturingLogger.Install();
        proxy.InstallScreenFadeSystems(world);

        Assert.DoesNotContain(logger.InfoMessages, m => m.Contains("transition effect"));
    }
}
