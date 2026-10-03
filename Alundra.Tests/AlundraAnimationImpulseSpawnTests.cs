#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using Alundra.Scripts;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c1 R2 (docs/plan-e19-opcodes.md §1.2h.3.1), tests UJ-6 and UJ-6b: the animation of an appearance never gives its Z impulse (the binary makes
/// that first switch itself in <c>InitializeEntity</c>, and its next <c>UpdateAnimation</c> clears the impulse before the physics), but another animation
/// written by a script before the first validation does. The entities are the export's REAL prefabs, spawned by the production factory
/// (<see cref="AlundraEntitySpawnFactory.CreateEntityFromRecord"/>, the call of the map load, <c>0x2D</c>, <c>0x8A</c> and <c>0x8B</c>) with a record written by hand.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraAnimationImpulseSpawnTests : IDisposable
{
    private const string ArrowLevel1 = "eab8d775";
    private const string ChargedFireball = "67b30f8f";
    private const string LargeIronGrid = "396c008e";

    private readonly string _previousProjectPath = EngineEnvironment.ProjectPath;

    public void Dispose() => EngineEnvironment.ProjectPath = _previousProjectPath;

    private static Guid PrefabId(string root, string prefix)
    {
        var infos = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(root, "AssetInfos.json")));
        foreach (var node in (Newtonsoft.Json.Linq.JArray)infos["asset_infos"]!)
        {
            var id = (string?)node["id"];
            if (id != null && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Guid.Parse(id);
            }
        }

        throw new InvalidOperationException($"no asset of the export starts with {prefix}");
    }

    /// <summary>Spawns the real prefab <paramref name="prefix"/> by the production factory at (xPos 16, yPos 8) and <paramref name="height"/> (units of 8 px), into a world of flat ground.</summary>
    private static (JumpNpcRig Rig, AlundraEntityScriptProxy Spawned) Spawn(string prefix, int height)
    {
        var root = ProjectRootFinder.Find();
        EngineEnvironment.ProjectPath = root;
        var world = ContactWorld.BuildWorld(new FlatGroundField { GroundZ = 0 }, null);
        ArcPrefabAssets.Install(world, root);
        var host = new ContactHost();

        var record = new TileMapObjectData { Name = "Entity_0" };
        record.CustomProperties["PrefabAssetId"] = PrefabId(root, prefix).ToString();
        record.CustomProperties["Index"] = "0";
        record.CustomProperties["SpriteTableIndex"] = "0";
        record.CustomProperties["SpriteDirection"] = "0";
        record.CustomProperties["XPos"] = "16";
        record.CustomProperties["YPos"] = "8";
        record.CustomProperties["Height"] = height.ToString();

        var entity = AlundraEntitySpawnFactory.CreateEntityFromRecord(
            record, id => world.Game!.AssetContentManager.LoadCopy<Entity>(id), SpriteRecordCatalog.GetOrCreate(root));
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.ScriptHost = host;
        host.All.Add(proxy);
        world.AddEntity(entity);
        return (new JumpNpcRig { World = world, Host = host, Npc = proxy }, proxy);
    }

    [Fact]
    public void UJ6_AnArrowOfImpulseWithoutGravity_DoesNotTakeOffAtItsAppearance_NorDoesTheForceMove()
    {
        var (rig, arrow) = Spawn(ArrowLevel1, height: 2);
        Assert.Equal(256, arrow.AnimSetsByAnim![0].IsZForceApplied); // the animation 0 of the arrow carries an impulse of 256 ...
        Assert.Equal(0, (int)(arrow.Flags & EntityFlags.Gravity)); // ... and it has no gravity.
        Assert.True(arrow.SpawnAnimationActive);

        var spawnHeight = arrow.PosZ;
        rig.Update();
        var rest = arrow.PosZ;
        Assert.InRange(spawnHeight - rest, 0, 1); // the unit of the header (+1 of the spawn) is swallowed by the first round trip of the root.
        for (var tick = 1; tick <= 10; tick++)
        {
            if (tick > 1)
            {
                rig.Update();
            }

            Assert.Equal(rest, arrow.PosZ); // a naive hook would give h + 65536 * n.
            Assert.Equal(0, arrow.ForceZ);
            Assert.Equal(0, arrow.IsZForceApplied);
        }

        Assert.False(arrow.SpawnAnimationActive); // fell at the first validation.
    }

    [Fact]
    public void UJ6_ACharedFireballOfImpulseWithGravity_DoesNotTakeOffAtItsAppearance()
    {
        var (rig, fireball) = Spawn(ChargedFireball, height: 0);
        Assert.Equal(512, fireball.AnimSetsByAnim![0].IsZForceApplied);
        Assert.NotEqual(0, (int)(fireball.Flags & EntityFlags.Gravity));

        var spawnHeight = fireball.PosZ;
        rig.Update();
        var rest = fireball.PosZ;
        Assert.InRange(spawnHeight - rest, 0, 1); // at the ground: the unit of the header of the spawn.
        for (var tick = 1; tick <= 10; tick++)
        {
            if (tick > 1)
            {
                rig.Update();
            }

            Assert.Equal(rest, fireball.PosZ); // a naive hook would give h + 131072 at the tick 1 and then the decay.
            Assert.Equal(0, fireball.IsZForceApplied);
        }
    }

    [Fact]
    public void UJ6_TheIronGridOfTheStopMarker_KeepsAForceZOfZeroAtEveryTick()
    {
        var (rig, grid) = Spawn(LargeIronGrid, height: 2);
        Assert.Equal(-32768, grid.AnimSetsByAnim![0].IsZForceApplied);
        Assert.Equal(0, (int)(grid.Flags & EntityFlags.Gravity));

        rig.Update();
        var rest = grid.PosZ;
        for (var tick = 1; tick <= 10; tick++)
        {
            if (tick > 1)
            {
                rig.Update();
            }

            Assert.Equal(0, grid.ForceZ); // a naive hook would give -8388608 (the 0x8000 marker holds the grid, and the appearance gives no impulse anyway).
            Assert.Equal(rest, grid.PosZ);
        }
    }

    private sealed class WriteTargetOnceRunner : IEventProgramRunner
    {
        public bool Done { get; private set; }

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
            if (!Done)
            {
                Done = true;
                entity.TargetAnimationId = JumpNpcRig.ImpulseAnimation; // the program's own `1A [3]` at its first tick.
            }
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    [Fact]
    public void UJ6b_AnAppearedEntityWhoseProgramWritesAnotherAnimationAtItsFirstTick_TakesOffByTheImpulseOfThatAnimation()
    {
        // The montage of UJ-1 (NPC with gravity at rest), made an appeared entity (the flag the factory raises with the animation 0 of the appearance), whose
        // program writes `1A [3]` (impulse 1360) at its first tick: an ordinary switch of the binary, which gives the impulse.
        var runner = new WriteTargetOnceRunner();
        var world = ContactWorld.BuildWorld(new FlatGroundField { GroundZ = 0 }, null);
        var host = new ContactHost(runner);
        var npc = ContactWorld.AddEntity(world, host, "Appeared", 200, 100, 0, -10, -7, 0, 20, 14, 32);
        npc.Flags |= EntityFlags.Gravity;
        npc.MapGravityRaw = 128;
        npc.MapZViscosityRaw = 4096;
        npc.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [0] = new AnimSetEntry { Anim = 0, Speed = 0 },
            [JumpNpcRig.ImpulseAnimation] = new AnimSetEntry { Anim = JumpNpcRig.ImpulseAnimation, Speed = 0, IsZForceApplied = 1360 },
        };
        npc.ProgramIndexes[ScriptHelper.ProgramCTick] = 1;
        npc.CurrentAnimationId = ~0u; // as the appearance writes it ...
        npc.TargetAnimationId = 0;
        npc.SpawnAnimationActive = true; // ... with its flag.
        npc.SpawnAnimationId = 0;
        npc.ResyncControllerFromFlags();
        var rig = new JumpNpcRig { World = world, Host = host, Npc = npc };

        rig.Update(); // the first tick: the program writes the animation 3 before the clock steps.
        Assert.True(runner.Done);
        Assert.Equal(348160, npc.PosZ);
        Assert.Equal(1360, npc.IsZForceApplied);
    }

    private sealed class HeroWorldContext : IEntityWorldContext
    {
        public HeroWorldContext(AlundraEntityScriptProxy hero) => PlayerEntity = hero;

        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = new List<AlundraEntityScriptProxy>();

        public AlundraEntityScriptProxy? PlayerEntity { get; }

        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }

        public void SetForcedCameraLookAt(int x, int y, int z)
        {
        }

        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;

        public void DestroyEntity(AlundraEntityScriptProxy entity) => entity.Status = EntityStatus.FlagToDestroy;

        public CasaEngine.Framework.AI.Navigation.NavigationGrid2D? NavigationGrid => null;
    }

    [Fact]
    public void UJ7_TheHeroArrivingByThe0x53LaunchedDuringTheAnimation2_TakesNoImpulse_AndStaysAtItsHeightFor5Ticks()
    {
        // The arrival of the preset day3-after-dream (map 179, tile (17, 7), z 1), on the real map, the real prefab of the hero and the real controller.
        var spec = new ArcSpec("UJ-7", "Inoa", "Inoa (inner)-179", new[] { 203, 1651, 1660 }, 0, 0, 0, 100, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((17 * 24 + 12) << 16, (7 * 16 + 8) << 16, 1 << 20, AlundraGameState.ResetAnimationId, 0));
        using var arc = new ArcRun(spec);
        Assert.True(arc.Hero.SpawnAnimationActive, "AdoptPlayerPawn raises the flag of the appearance with the animation of the arrival");
        Assert.Equal(AlundraGameState.ResetAnimationId, arc.Hero.SpawnAnimationId);
        arc.OneFrame();
        arc.OneFrame();
        var hero = arc.Hero;
        Assert.Equal(1280, hero.AnimSetsByAnim![2].IsZForceApplied); // the jump of the hero (the animation 2) carries an impulse in the export.

        // The real opcode 0x53 of a program, launched while the hero plays the animation 2: it copies the animation into the arrival record
        // (AlundraEventProgramRunner case 0x53 -> BeginDepartureFromChangeMapOpcode, PlayerManager's own constants are not used).
        hero.TargetAnimationId = 2;
        var document = new EventProgramDocument { MapIndex = 179, EventCodesATable = new[] { 0, 0 }, Codes = new[] { 0x53, 179, 0, 17, 7, 1, 0, 0, 0xFF } };
        var runner = new AlundraEventProgramRunner(document, ArcRun.State, new HeroWorldContext(hero));
        runner.RunOneScriptCall(new AlundraEntityScriptProxy { EntityRefId = 1, Status = EntityStatus.Normal }, new EventProgramState { Codes = document.CodesAsBytes() });
        var record = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
        Assert.Equal(2u, record.AnimationId);

        // The destination map's entry: the record is consumed by AdoptPlayerPawn of the world of the arrival (here the same world, entered again: the
        // transition itself - fade, world change - is not played by the headless montage, so the record is posed again after the reset of the director).
        AlundraWarpDirector.Instance.ResetForTests();
        AlundraWarpDirector.Instance.SetPendingArrivalForTests(record.MapIndex, record.PosX, record.PosY, record.PosZ, record.AnimationId, record.DirectionId);
        var tileMapData = (CasaEngine.Framework.Assets.TileMap.TileMapData)typeof(AlundraWorldProxy)
            .GetField("_tileMapData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(arc.Proxy)!;
        typeof(AlundraWorldProxy).GetMethod("AdoptPlayerPawn", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(arc.Proxy, new object[] { arc.RealWorld!, tileMapData });
        AlundraWarpDirector.Instance.ResetForTests();
        Assert.Equal(2u, hero.TargetAnimationId); // the arrival animation is the 2 the 0x53 copied.
        Assert.True(hero.SpawnAnimationActive);
        Assert.Equal(2u, hero.SpawnAnimationId);

        // E19.d2c2 (S1): the hero is control-locked from here on, as a scripted arrival is; otherwise the tail of MovePlayer rewrites the animation 2 (a jump
        // state) to Idle at the first frame on the ground and the test would no longer discriminate the exemption of R2.
        ArcRun.State.PlayerControlFlags |= AlundraGameState.PlayerControlBits.ControlLocked;
        var rest = hero.PosZ;
        for (var tick = 1; tick <= 5; tick++)
        {
            arc.OneFrame();
            Assert.Equal(0, hero.IsZForceApplied); // no impulse: the animation 2 of the arrival is the first switch of InitializeEntity (from the very first tick).
            Assert.Equal(rest, hero.PosZ);
        }
    }
}
