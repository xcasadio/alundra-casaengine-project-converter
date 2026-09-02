#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// T2 (docs/plan-transitions-carte.md §1.5/§3, "Gel global du monde") - ORACLE 2 (production site): drives
/// the real <see cref="AlundraWorldProxy.Update"/> itself (same headless montage as
/// <see cref="AlundraWorldProxyUpdateCharacterizationTests"/>/<see cref="AlundraDialogueFramePassTests"/>),
/// proving the gate this slice adds is really exercised at its call site AND that the "dehors" passes keep
/// running while it is closed.
///
/// Two of the plan's own T2 mutations are already covered by EXISTING tests, not repeated here (plan §3's
/// own mutation table names both precedents explicitly):
/// <list type="bullet">
/// <item><description>"élargir la porte à l'avance de boîte" -&gt;
/// <see cref="AlundraDialogueFramePassTests.Update_RunsTheDialoguePass_ButtonClosesABoxNoScriptIsWatching"/>
/// already opens a box with <c>controlMode: 0</c> (MenuOpen) and proves the SAME <c>proxy.Update</c> call
/// still advances/closes it - widening the gate to cover the dialogue tick would fail that test.</description></item>
/// <item><description>the contact probe's own freeze -&gt;
/// <see cref="AlundraInteractionPassTests.Update_ContactPassIsFrozen_WhileGameplayBlockedMaskIsPosed"/>.</description></item>
/// </list>
/// This file adds the one T2 mutation with no existing coverage - "mettre le suivi caméra dedans" - plus
/// the [R5] debt test.
/// </summary>
public sealed class AlundraWorldProxyGlobalFreezeTests : IDisposable
{
    public AlundraWorldProxyGlobalFreezeTests()
    {
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(true);

        // D-T-14 (docs/plan-transitions-carte.md, slice T1): this class constructs an AlundraWorldProxy,
        // so it shares the three session carriers T1 introduces - reset them here (constructor, the
        // isolation-carrying element) so no earlier test's state leaks in.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.
    }

    public void Dispose()
    {
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);

        // D-T-14: hygiene, not covered by the acceptance (the constructor above is what carries
        // isolation) - kept for symmetry with the existing session-singleton test classes.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.
    }

    // -----------------------------------------------------------------------------------------
    // [R4]/table row: "mettre le suivi caméra dedans" -> oracle 2, "la caméra continue".
    // -----------------------------------------------------------------------------------------

    private static World BuildHeadlessWorld() => new() { Name = "TestWorld" };

    private static Camera2dComponent AddCameraEntity(World world)
    {
        var camera = new Camera2dComponent();
        var cameraEntity = new Entity { Name = "camera", RootComponent = camera };
        world.Entities.Add(cameraEntity);
        Assert.Contains(cameraEntity, world.Entities);
        return camera;
    }

    private static AlundraEntityScriptProxy BuildFollowedTarget(int posXPixels, int posYPixels)
        => new()
        {
            Status = EntityStatus.Normal,
            PosX = posXPixels << 16,
            PosY = posYPixels << 16,
            PosZ = 0,
        };

    /// <summary>
    /// The camera follow (<see cref="AlundraCameraDirector.UpdateCameraFollow"/>) is "dehors" (§1.5's own
    /// table: "the original's own look-at update is the LAST thing UpdateEntities does, AFTER the `else`")
    /// - it must keep tracking <see cref="AlundraWorldProxy.EntityFollowedByCamera"/> even while
    /// <see cref="AlundraGameState.PlayerControlBits.GameplayBlockedMask"/> is posed (a MenuOpen dialogue
    /// box must not also freeze the camera).
    /// </summary>
    [Fact]
    public void Update_CameraFollow_KeepsTrackingFollowedEntity_WhileGameplayBlockedMaskIsPosed()
    {
        var world = BuildHeadlessWorld();
        var camera = AddCameraEntity(world);

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        var followed = BuildFollowedTarget(posXPixels: 100, posYPixels: 200);
        proxy.EntityFollowedByCamera = followed;

        proxy.Update(0.02f); // frame 1: snaps straight to the look-at (ArmFirstFrameSnap).
        var targetAfterFirstFrame = camera.Target;

        proxy.GameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;

        // Move the followed target while frozen - the original's own look-at update is not behind the
        // GameplayBlockedMask gate at all (§1.5), so the camera must still react to it.
        followed.PosX = 900 << 16;
        followed.PosY = 500 << 16;
        proxy.Update(0.02f);

        Assert.NotEqual(targetAfterFirstFrame, camera.Target);

        proxy.GameState.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.MenuOpen;
    }

    // -----------------------------------------------------------------------------------------
    // [R5] debt inherited from T1: no test today crosses InitializeWithWorld's real installation block
    // (every existing caller uses a world with no "tileMap" entity and exits through the early return,
    // AlundraWorldProxy.cs:506-518 - see AlundraGameStateSessionTests/AlundraWorldProxySessionStateTests'
    // own class docs, which say so explicitly). GameState.InstallForMapEntry() and the ActiveCollisionEntity
    // reset (both posed by T1, AlundraWorldProxy.cs:527/535) are green but unpinned. This montage reuses
    // AlundraCellVisualSyncTests' own real-map-389 headless TileMapComponent fixture (built directly, not
    // shared, since that class' own helpers are private) but drives the FULL
    // <see cref="AlundraWorldProxy.InitializeWithWorld"/>, not just <c>InstallCellAndOverlaySystems</c>.
    // -----------------------------------------------------------------------------------------

    private const string WorldName = "Ship Klark (beginning)-389";
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
            $"AlundraWorldProxyGlobalFreezeTests: no 'alundra-project/Maps' directory found above "
            + $"'{AppContext.BaseDirectory}' - the [R5] test needs the real converter export of map 389.");
    }

    private static TileMapData LoadRealTileMapData(string projectRoot)
    {
        var tileMapPath = Path.Combine(
            projectRoot, "Maps", "The Klark", "Ship Klark (beginning)-389", "tilemap",
            "Ship Klark (beginning)-389.tileMap");
        if (!File.Exists(tileMapPath))
        {
            throw new InvalidOperationException($"AlundraWorldProxyGlobalFreezeTests: '{tileMapPath}' not found.");
        }

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
        Assert.True(pathById.TryGetValue(visualAssetId, out var relativePath), "AssetInfos.json missing map 389's visual tileset.");
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

    /// <summary>Same headless (no GraphicsDevice) real-map-389 <see cref="TileMapComponent"/> montage as
    /// <see cref="AlundraCellVisualSyncTests.BuildHeadlessProxy"/> - built independently here (that
    /// class' own helpers are private) because THIS test needs the raw <see cref="World"/>/<see cref="Entity"/>
    /// BEFORE any proxy touches them, to call <see cref="AlundraWorldProxy.InitializeWithWorld"/> directly
    /// instead of <c>InstallCellAndOverlaySystems</c>.</summary>
    private static (World World, Entity TileMapEntity) BuildRealMap389World()
    {
        var projectRoot = FindProjectRoot();
        var tileMapData = LoadRealTileMapData(projectRoot);
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
        return (world, tileMapEntity);
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

    /// <summary>
    /// [R5]: crosses <see cref="AlundraWorldProxy.InitializeWithWorld"/>'s two early returns (a real
    /// "tileMap" entity with loaded <see cref="TileMapData"/>, exactly the montage
    /// <see cref="AlundraCellVisualSyncTests"/> already proves is reachable headless) and pins the two
    /// T1 instructions that sit right after them (AlundraWorldProxy.cs:527/535):
    /// <see cref="AlundraGameState.InstallForMapEntry"/> really runs (a stale TemporaryFlags entry and a
    /// non-null InteractLatchEntity, both seeded BEFORE the call, are gone after it), and
    /// <see cref="AlundraWorldProxy.ActiveCollisionEntity"/> really gets reset to null. The call is made
    /// with NO try/catch: if a later step inside the SAME method throws, that is itself new information
    /// this test surfaces rather than hides.
    /// </summary>
    [Fact]
    public void InitializeWithWorld_WithRealTileMapEntity_InstallsSessionMapEntryDisposition()
    {
        var (world, _) = BuildRealMap389World();

        // Seed a stale value for each of the two instructions T2 must pin, so InitializeWithWorld running
        // is the ONLY thing that can make the post-call assertions below true.
        const int seededTemporaryFlagIndex = 3;
        AlundraGameState.Instance.TemporaryFlags[seededTemporaryFlagIndex] = 0xdead_beef;
        AlundraGameState.Instance.InteractLatchEntity = new AlundraEntityScriptProxy();

        var proxy = new AlundraWorldProxy();
        proxy.ActiveCollisionEntity = new AlundraEntityScriptProxy();

        proxy.InitializeWithWorld(world);

        Assert.Equal(0u, AlundraGameState.Instance.TemporaryFlags[seededTemporaryFlagIndex]);
        Assert.Null(AlundraGameState.Instance.InteractLatchEntity);
        Assert.Null(proxy.ActiveCollisionEntity);
    }
}
