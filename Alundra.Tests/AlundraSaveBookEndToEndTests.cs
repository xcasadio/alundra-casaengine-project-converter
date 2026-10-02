#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.SaveGames;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;
using static Alundra.Tests.SaveGameDirectorTestSupport;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E16.e T5 (docs/plan-e16-etat-partie.md, L1 to L5): the save book end to end on the REAL map 17 (one of the 65
/// maps with a book, « SaveBook (Ne pas toucher !) » at tile (6, 40)): the real installation
/// (<see cref="AlundraWorldProxy.InitializeWithWorld"/>, the book spawned from the export), the real frame
/// (<see cref="AlundraWorldProxy.Update"/> after every entity's own <c>Update</c>, the hero's included, whose
/// controller gives the pad), a real dialogue presenter over a recording UI view of the world's active view. The
/// hero walks into the book (Square when its record asks for the button), the book asks, OUI, the save screen, Down
/// to <c>slot2</c>, Cross, OUI: the fake service receives <c>slot2</c> in binary. A second visit shows, recomputed
/// from what the service received (it gives back what it was given), the chapter and the summary of the game.
/// The real save folder is never reached (D-E16-31).
/// <para>
/// E19.d2b B4 (T-REG-E12D-1, D-E19-29): the hero is a real <see cref="CharacterControllerComponent"/> (the export's settings) and his contact comes from the
/// blocking report of his controller - the overlap pass of the end of the frame is gone, and he can no longer stand ON the book. He is placed 32 px south
/// of it (book record 0 at (84, 328), box y 320 to 336) and walks north, Up held: the controller stops him flush at y = 343 and names the book. A twin
/// placed at that very y without pushing writes no contact.
/// </para>
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraSaveBookEndToEndTests : IDisposable
{
    private const string Map17WorldName = "Overworld 4,0-17";
    private const int BookTileX = 6;
    private const int BookTileY = 40;

    private readonly EchoSlots _slots = new();
    private uint _hold;

    public AlundraSaveBookEndToEndTests()
    {
        ResetAll();
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(true);
    }

    public void Dispose()
    {
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);
        ResetAll();
    }

    private static void ResetAll()
    {
        ResetSingletons();
        AlundraEtcStringTable.ResetForTests();
        AlundraSaveBook.Instance.ResetForTests();
    }

    private static AlundraGameState State => AlundraGameState.Instance;

    private static AlundraSaveScreenDirector Screen => AlundraSaveScreenDirector.Instance;

    [Fact]
    public void RealMap17_TheBook_Oui_TheScreen_Slot2_Oui_WritesSlot2InBinary_ThenItsLabelShowsTheGame()
    {
        WithTheMap17Montage((uiView, world, heroEntity) => RunTheFlow(uiView, world, heroEntity));
    }

    /// <summary>T-REG-E12D-1, the twin: the hero placed flush against the book (y = 343), pushing nothing, writes no contact - and the book's flow never
    /// starts. The overlap pass that used to give him a contact from his position alone is gone.</summary>
    [Fact]
    public void RealMap17_AHeroPlacedFlushAgainstTheBookWithoutPushing_WritesNoContact()
    {
        WithTheMap17Montage((uiView, world, heroEntity) =>
        {
            var (proxy, hero, book) = PrepareTheBookScene(uiView, world, heroEntity);
            hero.PosX = 84 << 16;
            hero.PosY = FlushWithTheBookPosY;
            hero.PosZ = book.PosZ;
            hero.PushLogicalPositionToRoot();

            _hold = AlundraPadState.Square; // the button alone, no direction: nothing is pushed.
            for (var i = 0; i < 20; i++)
            {
                OneFrame(proxy);
                Assert.Null(hero.XCollisionEntity);
            }

            _hold = 0;
            Assert.False(Screen.IsBookFlowActive);
        });
    }

    /// <summary>The hero's PosY when the controller stops him flush against the book (record 0: y 328, box 320 to 336; the hero's box starts 7 px above
    /// his centre): 343 px.</summary>
    private const int FlushWithTheBookPosY = 22478848;

    private void WithTheMap17Montage(Action<AlundraSaveBookTests.RecordingUIViewRuntime, World, Entity> body)
    {
        var uiView = new AlundraSaveBookTests.RecordingUIViewRuntime();
        var world = BuildRealMap17World(uiView);
        var (heroEntity, controller) = AddHeroPawn(world);
        controller.PadStateProviderForTests = () => new AlundraPadState { ButtonsHold = _hold, ButtonsJustPressed = _hold };

        // The sprite record catalog is resolved when the proxy is built, from the project path: the export's.
        var previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = FindProjectRoot();
        try
        {
            body(uiView, world, heroEntity);
        }
        finally
        {
            CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    private void RunTheFlow(AlundraSaveBookTests.RecordingUIViewRuntime uiView, World world, Entity heroEntity)
    {
        var (proxy, hero, book) = PrepareTheBookScene(uiView, world, heroEntity);

        // E19.d2b B4 (T-REG-E12D-1): the hero is placed 32 px south of the book and walks north, Up held; the controller stops him flush against it and
        // names it (the contact of the dialogue); then Square when the book's record asks for the button.
        Assert.Equal((84 << 16, 328 << 16), (book.PosX, book.PosY));
        hero.PosX = book.PosX;
        hero.PosY = book.PosY + (32 << 16);
        hero.PosZ = book.PosZ;
        hero.PushLogicalPositionToRoot();
        for (var i = 0; i < 80 && hero.XCollisionEntity == null; i++)
        {
            _hold = AlundraPadState.Up;
            OneFrame(proxy);
        }

        Assert.Same(book, hero.XCollisionEntity);
        Assert.Equal(FlushWithTheBookPosY, hero.PosY);

        var needsButton = (book.Flags & EntityFlags.InteractRequiresButton) != 0;
        for (var i = 0; i < 10 && !Screen.IsBookFlowActive; i++)
        {
            _hold = AlundraPadState.Up | (needsButton && i % 2 == 1 ? AlundraPadState.Square : 0u);
            OneFrame(proxy);
        }

        _hold = 0;
        Assert.True(Screen.IsBookFlowActive,
            $"the hero's interaction never reached the book's slot F (contact: {hero.XCollisionEntity?.SpriteType}, "
            + $"animation {hero.TargetAnimationId}, button needed: {needsButton})");

        // The book's question, then its OUI.
        FramesUntil(proxy, () => AlundraDialogueDirector.Instance.IsAwaitingChoice, 200);
        Assert.Equal(new[] { "OUI", "NON" }, AlundraDialogueDirector.Instance.ChoicesForTests);
        Assert.True(AlundraDialogueDirector.Instance.SelectChoiceForTests(0));

        // The capture, then the screen and its picker.
        FramesUntil(proxy, () => Screen.IsPickerActive && Screen.State == AlundraSaveScreenDirector.StatePickWait, 300);
        Frames(proxy, 20); // the opening slide settles.
        Assert.Equal(new[] { "slot1", "slot2", "slot3", "slot4" }, _slots.LoadCalls);

        // Down to slot2, Cross, OUI.
        Press(proxy, AlundraPadState.Down);
        Frames(proxy, 20);
        Assert.Equal(1, Screen.Selection);
        Press(proxy, AlundraPadState.Cross);
        Assert.True(AlundraDialogueDirector.Instance.IsAwaitingChoice);
        Assert.True(AlundraDialogueDirector.Instance.SelectChoiceForTests(0));

        FramesUntil(proxy, () => Screen.State == AlundraSaveScreenDirector.StateWaitSquare, 400);
        var call = Assert.Single(_slots.SaveCalls);
        Assert.Equal("slot2", call.Slot);
        Assert.Equal(SaveGameFormat.Binary, call.Format);
        Assert.Equal(17, call.Data.InitialMapId);
        Assert.Equal((hero.TileX, hero.TileY), (call.Data.CameraTileX, call.Data.CameraTileY));

        // Square ends the screen; the book resets and the hero is released.
        Press(proxy, AlundraPadState.Square);
        FramesUntil(proxy, () => !Screen.IsActive && !Screen.IsBookFlowActive, 200);
        Assert.Equal(AlundraSaveBook.StateIdle, AlundraSaveBook.ReadState(book));
        Assert.Equal(0u, State.PlayerControlFlags);

        // The label of slot2, recomputed from what the service received.
        _slots.LoadCalls.Clear();
        Assert.True(Screen.Start(call.Data));
        FramesUntil(proxy, () => Screen.IsPickerActive, 300);
        Assert.Equal(("", ""), Screen.SlotLabel(0));
        Assert.Equal(
            ("Wendell Succombe", AlundraSaveGame.BuildSummary(call.Data.HpMax, call.Data.GameTime)),
            Screen.SlotLabel(1));
        Assert.StartsWith("  HP 10", Screen.SlotLabel(1).Line1);
    }

    /// <summary>The map 17 montage up to the point where the hero is a spawned pawn adopted with its sprite header, the book spawned from the export, the
    /// chapter reached: what both facts share.</summary>
    private (AlundraWorldProxy Proxy, AlundraEntityScriptProxy Hero, AlundraEntityScriptProxy Book) PrepareTheBookScene(
        AlundraSaveBookTests.RecordingUIViewRuntime uiView, World world, Entity heroEntity)
    {
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);
        typeof(AlundraBackdropStage).GetField("_clearColorApplied", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(proxy._backdropStage, true); // the headless montage has no view for the clear colour.

        // The headless game has no asset manager: the ETC texts come from the export on disk, the rules and the
        // service from the test (K1, D-E16-31).
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(
            DialogueTestAssets.LoadFromDisk(Path.Combine(FindProjectRoot(), "Dialogues", "Etc.dialogue")));
        AlundraSaveGameDirector.Instance.RulesFactoryForTests = RealRules;
        AlundraSaveGameDirector.Instance.SaveSlots = _slots;
        Assert.True(AlundraDialogueDirector.Instance.HasPresenter);

        // The book of map 17, spawned from the export, with the save book's native handlers (L1).
        // The book's record (index 0) has SpriteDirection bit 0x40 clear: the map-load pass skips it, and map 17
        // spawns it from its map program B[1] (opcode 0x8B at code offset 0xFA, SpawnEntityNextToEntity(record 0),
        // behind story flags). It is spawned here by that same production path, SpawnEntityByRecordId.
        Assert.DoesNotContain(Spawned(proxy), e => e.SpriteType == AlundraSaveBook.SpriteType);
        var hero = Assert.IsType<AlundraEntityScriptProxy>(heroEntity.GameplayProxy);
        Assert.NotNull(proxy.SpawnEntityByRecordId(hero, 0));

        // The hero's sprite header (flags, programs, body box): AdoptPlayerPawn resolves it through the asset
        // catalog, empty in the test process - the same block, from the export's own catalog file.
        Assert.True(proxy.SpriteRecordCatalog.TryGet(HeroPrefabId(), out var heroHeader));
        AlundraWorldProxy.ApplyHeroSpriteHeader(hero, heroHeader);

        // The map-entry animation (0x36) ends through the animation chain (anim 54 -> 0, OnAnimationFinished),
        // which the headless montage never plays: the hero is put at its end, Idle.
        Assert.Equal(AlundraGameState.ResetAnimationId, hero.TargetAnimationId);
        hero.TargetAnimationId = 0;
        var book = Assert.Single(Spawned(proxy), e => e.SpriteType == AlundraSaveBook.SpriteType);
        Assert.Equal(AlundraSaveBook.InteractCode, book.SpriteProgramIndexes[ScriptHelper.ProgramFInteract]);
        Assert.Equal(AlundraSaveBook.TickCode, book.SpriteProgramIndexes[ScriptHelper.ProgramCTick]);

        // Chapter 1 ("Wendell Succombe") reached, to read it back from the label.
        State.GameFlags[0] |= 1u << 3;
        State.PlayerStats.WeaponId = 1;

        return (proxy, hero, book);
    }

    /// <summary>The id of the hero prefab "Alundra" in the export's <c>AssetInfos.json</c>.</summary>
    private static Guid HeroPrefabId()
    {
        var assetInfos = JObject.Parse(File.ReadAllText(Path.Combine(FindProjectRoot(), "AssetInfos.json")));
        var entry = ((JArray)assetInfos["asset_infos"]!).Single(e => (string?)e["name"] == "Alundra" && (string?)e["asset_type"] == "entity");
        return Guid.Parse((string)entry["id"]!);
    }

    private void Press(AlundraWorldProxy proxy, uint button)
    {
        _hold = button;
        OneFrame(proxy);
        _hold = 0;
    }

    private void Frames(AlundraWorldProxy proxy, int count)
    {
        for (var i = 0; i < count; i++)
        {
            OneFrame(proxy);
        }
    }

    private void FramesUntil(AlundraWorldProxy proxy, Func<bool> condition, int max)
    {
        for (var i = 0; i < max && !condition(); i++)
        {
            OneFrame(proxy);
        }

        Assert.True(condition(), $"condition not reached in {max} frames (screen state 0x{Screen.State:X}, book flow {Screen.IsBookFlowActive})");
    }

    /// <summary>One frame the engine's way: every spawned entity's own <c>Update</c> - the hero's included, whose
    /// controller samples the pad - then the world's.</summary>
    private static void OneFrame(AlundraWorldProxy proxy)
    {
        foreach (var entity in Spawned(proxy).ToList())
        {
            entity.Update(0.02f);
        }

        proxy.Update(0.02f);
    }

    private static IReadOnlyList<AlundraEntityScriptProxy> Spawned(AlundraWorldProxy proxy)
        => ((IEntityWorldContext)proxy).SpawnedEntities;

    /// <summary>The hero pawn possessed by a real <see cref="AlundraPlayerController"/>, returned for its pad seam -
    /// the fixture of <see cref="SaveGameDirectorTestSupport.AddHeroPawn"/>.</summary>
    private static (Entity Entity, AlundraPlayerController Controller) AddHeroPawn(World world)
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

        // E19.d2b B4 (T-REG-E12D-1): a real controller with the export's settings; the world gets the physics world it needs.
        entity.AddComponent(new CharacterControllerComponent { Settings = HeroWorldFixture.LoadHeroControllerSettings(FindProjectRoot()) });
        HeroWorldFixture.SetProperty(world, nameof(World.PhysicsWorld), new PhysicsWorld(false, new TopDownElevationSimulationSpacePolicy()));
        entity.Initialize();
        typeof(Entity).GetProperty(nameof(Entity.World), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(entity, world);

        var controller = new AlundraPlayerController();
        controller.Possess(entity);
        var field = typeof(World).GetField("_playerControllers", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((List<PlayerController>)field.GetValue(world)!).Add(controller);
        return (entity, controller);
    }

    /// <summary>The headless real-map montage of <see cref="AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World"/>
    /// for map 17, with a game whose active render view carries <paramref name="uiView"/> (the recipe of
    /// <see cref="SaveScreenTestWorlds.WorldWithUIView"/>), so the real install wires a dialogue presenter.</summary>
    private static World BuildRealMap17World(AlundraSaveBookTests.RecordingUIViewRuntime uiView)
    {
        var projectRoot = FindProjectRoot();
        var tileMapPath = Path.Combine(projectRoot, "Maps", "Overworld", Map17WorldName, "tilemap", Map17WorldName + ".tileMap");
        Assert.True(File.Exists(tileMapPath), $"the real export of map 17 is missing: '{tileMapPath}'");
        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));
        var tileSetData = AlundraWorldProxyGlobalFreezeTests.LoadRealVisualTileSet(projectRoot, tileMapData);

        var world = SaveScreenTestWorlds.WorldWithUIView(Map17WorldName, uiView);

        var tileMapEntity = new Entity { Name = "tileMap" };
        AlundraWorldProxyGlobalFreezeTests.SetProperty(tileMapEntity, nameof(Entity.World), world);

        var component = new TileMapComponent();
        tileMapEntity.RootComponent = component;
        component.TileMapData = tileMapData;
        component.TileSetData = tileSetData;

        AlundraWorldProxyGlobalFreezeTests.GetPrivateList<TileSetData>(component, "_tileSets").Add(tileSetData);
        AlundraWorldProxyGlobalFreezeTests.GetPrivateList<Texture2D>(component, "_tileSetTextures").Add(null!);

        var runtimeLayers = AlundraWorldProxyGlobalFreezeTests.GetLayers(component);
        for (var layerIndex = 0; layerIndex < 4; layerIndex++)
        {
            var layer = new TileMapLayer(tileMapData.Layers[layerIndex]);
            var cellCount = tileMapData.MapSize.Width * tileMapData.MapSize.Height;
            for (var i = 0; i < cellCount; i++)
            {
                layer.Tiles.Add(new AlundraWorldProxyGlobalFreezeTests.StubTile());
                layer.CollisionObjects.Add(null);
            }

            runtimeLayers.Add(layer);
            AlundraWorldProxyGlobalFreezeTests.InvokeBuildChunks(component, layer, layerIndex);
        }

        world.Entities.Add(tileMapEntity);
        return world;
    }

    /// <summary>A fake service that gives back what it received for a slot (NotFound before), recording every
    /// call.</summary>
    private sealed class EchoSlots : IAlundraSaveSlots
    {
        private readonly Dictionary<string, AlundraSaveGame> _stored = new();
        public readonly List<(string Slot, AlundraSaveGame Data, SaveGameFormat Format)> SaveCalls = new();
        public readonly List<string> LoadCalls = new();

        public AlundraSaveOutcome Save(string slot, AlundraSaveGame data, SaveGameFormat format, IReadOnlyDictionary<string, string> metadata)
        {
            SaveCalls.Add((slot, data, format));
            _stored[slot] = data;
            return new AlundraSaveOutcome(SaveGameSaveStatus.Saved, string.Empty);
        }

        public AlundraLoadOutcome TryLoad(string slot, out AlundraSaveGame? data)
        {
            LoadCalls.Add(slot);
            if (_stored.TryGetValue(slot, out var stored))
            {
                data = stored;
                return new AlundraLoadOutcome(SaveGameLoadStatus.Loaded, string.Empty);
            }

            data = null;
            return new AlundraLoadOutcome(SaveGameLoadStatus.NotFound, string.Empty);
        }

        public IReadOnlyList<AlundraSlotEntry> ListSlots() => Array.Empty<AlundraSlotEntry>();
    }
}
