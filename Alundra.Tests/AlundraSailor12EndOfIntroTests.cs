#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.a3 T0/T2 (docs/plan-e19-opcodes.md §1.2c, ADR-0017 D-E19-12): the eight walks of sailor 12 of map 389 at the end
/// of the intro (<c>C[12]</c>, <c>@1438</c> to <c>@1494</c>), played by the real controller of bank 146 on the real
/// cells and the real navigation grid of the map, with the real header of the sailor. The walk south 48 (<c>0x1F
/// @1484</c>) ends against the wall of row 53 at the contact y = 842.0: its last step is shortened but advances, so
/// the original leaves <c>ForceAdjusted</c> at 0 (<c>0x80037d54</c> only fires on a tick with no accepted sub-step)
/// and the walk west 72 (<c>0x1F @1491</c>), which starts on the same tick, proceeds. A flag raised by a shortened step
/// ends that walk on the tick it starts and leaves the sailor 72 px east of his place (x = 540.875).
/// Expected values worked out from the cells before the code: the west walk starts at x = 540.875 on row 52, free from
/// column 17 to 23 at the sailor's height, advances 1.875 px per tick (speed 160 on X) and ends by distance on its 39th
/// tick, at x = 467.75.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraSailor12EndOfIntroTests
{
    private const string WorldName = "Ship Klark (beginning)-389";
    private const string Bank146 = "Entities/Marin-passager-mouette-146/Marin-passager-mouette-146.entity";
    private const int FirstCode = 1438;
    private const int StopCode = 1494;
    private const int EndOfStopCode = 1498;
    private const int MaxFrames = 450;
    private const float Dt = 0.02f;

    [Fact]
    public void Sailor12_AfterTheSouthWalkEndsOnTheContact_TheWestWalkProceedsToHisPlace()
    {
        var root = SaveGameDirectorTestSupport.FindProjectRoot();
        var tileMapPath = Path.Combine(root, "Maps", "The Klark", WorldName, "tilemap", WorldName + ".tileMap");
        Assert.True(File.Exists(tileMapPath), $"the real export of map '{WorldName}' is missing: '{tileMapPath}'");
        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));
        Assert.True(AlundraCellsCollisionField.TryCreate(tileMapData, WorldName, out var field));

        var eventsDirectory = Path.Combine(root, "Maps", "The Klark", WorldName, "events");
        Assert.True(Directory.Exists(eventsDirectory), $"the real events of map '{WorldName}' are missing: '{eventsDirectory}'");
        var events = JObject.Parse(File.ReadAllText(Directory.GetFiles(eventsDirectory, "*.json")[0]));
        var allCodes = ((JArray)events["Codes"]!).Select(token => (int)token).ToArray();
        var codes = allCodes.Skip(FirstCode).Take(EndOfStopCode - FirstCode).Append(0xFF).ToArray();
        Assert.Equal(0x5B, codes[StopCode - FirstCode]);

        var bank146Path = Path.Combine(root, Bank146.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(bank146Path), $"the real bank 146 is missing: '{bank146Path}'");
        var document = JObject.Parse(File.ReadAllText(bank146Path));
        var world = HeroWorldFixture.BuildWorld(field!);
        var start = new Vector3(46 * 12 + 12, 83 * 8 + 8, 10 * 8);
        var proxy = BuildSailor(world, LoadSettings(document), start);

        var prefabId = Guid.Parse((string)document["id"]!);
        Assert.True(new SpriteRecordCatalog(root).TryGet(prefabId, out var header));
        proxy.Flags = (uint)(header.MoreFlags | (header.CanPickup << 8) | (header.FlagsPortraitShadowType << 16));
        proxy.AnimSetsByAnim = header.AnimSets;
        AlundraEntitySpawnFactory.SetEntityDimensions(proxy, header.OffsetX, header.OffsetY, header.OffsetZ, header.SizeX, header.SizeY, header.SizeZ);
        proxy.Controller!.Settings.WalkabilityMask = AlundraCellsCollisionField.WalkabilityMaskFor(proxy.Flags);
        world.Update(Dt);

        Assert.True(NavigationGrid2D.TryCreateFromTileMap(tileMapData, LoadTileSets(root, tileMapData), 1f, out var grid));
        var context = new NavContext { NavigationGrid = grid };
        var runner = new AlundraEventProgramRunner(new EventProgramDocument { MapIndex = 389, EventCodesCTable = new[] { 0, 0 }, Codes = codes }, new AlundraGameState(), context);
        var lastPc = -1;
        var lastOpcode = -1;
        var reachedStop = false;
        var ended = false;
        runner.TraceSink = record =>
        {
            lastPc = record.CodeIndex + FirstCode;
            lastOpcode = record.Opcode;
            reachedStop |= lastPc == StopCode;
            ended |= record.Opcode == 0xFF;
        };
        proxy.ScriptHost = new Host(runner);
        proxy.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x81;

        var frames = 0;
        var endOfPreviousFrame = (X: proxy.PosX / 65536.0, Y: proxy.PosY / 65536.0);
        (double X, double Y)? beforeStopFrame = null;
        while (!ended && frames < MaxFrames)
        {
            world.Update(Dt);
            frames++;
            if (reachedStop && beforeStopFrame is null)
            {
                // @1494 was traced during this frame: the read point is the end of the frame just before.
                beforeStopFrame = endOfPreviousFrame;
            }

            endOfPreviousFrame = (proxy.PosX / 65536.0, proxy.PosY / 65536.0);
        }

        var x = proxy.PosX / 65536.0;
        var y = proxy.PosY / 65536.0;
        Assert.True(
            reachedStop && ended,
            $"the program of sailor 12 must reach @{StopCode} then 0xFF within {MaxFrames} frames; after {frames} frames the last instruction executed is @{lastPc} (opcode 0x{lastOpcode:X2}); sailor at ({x}, {y})");
        // Read at the end of the frame of the last westward step, before @1494 turns the sailor south at rest: the
        // movement of the tick that ends the walk still applies with the new direction (O-E19-5, y = 843.25).
        Assert.Equal((467.75, 842.0), beforeStopFrame!.Value);
    }

    private static CharacterControllerSettings LoadSettings(JObject document)
    {
        foreach (var node in (JArray)document["components"]!)
        {
            if ((string?)node["type"] == nameof(CharacterControllerComponent))
            {
                var settings = new CharacterControllerSettings();
                settings.Load((JObject)node["settings"]!);
                return settings;
            }
        }

        throw new InvalidOperationException("bank 146 carries no character controller");
    }

    private static List<TileSetData> LoadTileSets(string projectRoot, TileMapData tileMapData)
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

        var tileSets = new List<TileSetData>();
        foreach (var assetId in tileMapData.TileSetDataAssetIds)
        {
            var fullPath = Path.Combine(projectRoot, pathById[assetId].Replace('\\', Path.DirectorySeparatorChar));
            var tileSetData = new TileSetData();
            tileSetData.Load(JObject.Parse(File.ReadAllText(fullPath)));
            tileSets.Add(tileSetData);
        }

        return tileSets;
    }

    private static AlundraEntityScriptProxy BuildSailor(World world, CharacterControllerSettings settings, Vector3 startPosition)
    {
        var rootComponent = new TransformComponent();
        rootComponent.LocalTransform.Position = startPosition;
        var collisionComponent = new CollisionComponent();
        collisionComponent.Fixtures.Add(new ColliderFixture
        {
            Shape = new Box { Size = new Vector3(18f, 12f, 32f) },
            LocalPosition = new Vector3(0f, 0f, 16f),
            LocalRotation = Quaternion.Identity,
        });
        rootComponent.AddChildComponent(collisionComponent);
        var entity = new Entity { Name = "Sailor12", RootComponent = rootComponent, GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        var controllerComponent = new CharacterControllerComponent { Settings = settings };
        controllerComponent.SetControlMode(CharacterControlMode.Script);
        controllerComponent.IsVerticalOwnedExternally = true;
        entity.AddComponent(controllerComponent);
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.Controller = entity.GetComponent<CharacterControllerComponent>();
        proxy.IsPlayer = false;
        proxy.ScriptHost = new Host(new AlundraEventProgramRunner(new EventProgramDocument { MapIndex = 389, EventCodesCTable = new[] { 0, 0 }, Codes = new[] { 0xFF } }, new AlundraGameState(), null));
        proxy.Status = EntityStatus.Normal;
        proxy.PosX = (int)Math.Round((double)startPosition.X * 65536.0);
        proxy.PosY = (int)Math.Round((double)startPosition.Y * 65536.0);
        proxy.PosZ = (int)Math.Round((double)startPosition.Z * 65536.0);
        proxy.CurrentAnimationId = ~0u;
        world.AddEntity(entity);
        return proxy;
    }

    private sealed class Host : IAlundraScriptHost
    {
        private readonly AlundraLogicClock _clock = new();
        public Host(IEventProgramRunner runner) => Runner = runner;
        public IEventProgramRunner Runner { get; }
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        public int LogicTicksThisFrame(float elapsedTime)
        {
            var ticks = _clock.TicksThisFrame(elapsedTime);
            _clock.CloseFrame();
            return ticks;
        }
    }

    private sealed class NavContext : IEntityWorldContext
    {
        public NavigationGrid2D? NavigationGrid { get; set; }
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public AlundraEntityScriptProxy? PlayerEntity => null;
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;

        public void DestroyEntity(AlundraEntityScriptProxy entity)
        {
        }
    }
}
