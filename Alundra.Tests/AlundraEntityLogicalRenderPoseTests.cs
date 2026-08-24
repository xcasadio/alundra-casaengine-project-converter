using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// Covers E3.a (docs/plan-e3-collisions.md): the logical/render pose split introduced on
/// <see cref="AlundraWorldProxy"/>'s three transform write sites
/// (<see cref="AlundraWorldProxy.CreateEntityFromPrefab"/>, <see cref="AlundraWorldProxy.AdoptPlayerPawn"/>,
/// <see cref="AlundraWorldProxy.SyncTransform"/>).
///  - Formula: <see cref="AlundraWorldProxy.ResolveLogicalPosition"/> followed by
///    <see cref="TopDownElevationSimulationSpacePolicy.DeriveRenderPosition"/> must reproduce, bit for
///    bit, the OLD single-step formula this slice replaced (kept below as <see cref="OldResolveWorldPosition"/>),
///    for every one of map 389's own 19 "Entities" layer records (XPos/YPos/Height read from the real
///    converter export, see <see cref="AlundraWorldProxySpawnInitializationTests.ShouldSpawnRecord_Map389EntitiesLayer_Keeps14Of19"/>'s
///    own doc for the sibling SpriteDirection list) and the hero's own New Game spawn
///    (<see cref="AlundraWorldProxy.AdoptPlayerPawn"/>).
///  - Ordering: a real <see cref="Entity"/> (TransformComponent -&gt; RenderProjectionComponent -&gt;
///    AnimatedSpriteComponent) in a <see cref="World"/> under <see cref="TopDownElevationSimulationSpacePolicy"/>
///    (same headless montage as <c>WallPlacementOverlayTests</c>'s own world/game fixture, plus the
///    PhysicsWorld wiring CasaEngineMonogame's own <c>TransformComponentTests</c> uses for E3.0) - a Load
///    program mutates the proxy's logical pose from inside its own <c>Update</c> (the fake
///    <see cref="IEventProgramRunner"/> below), and after one <see cref="World.Update(float)"/> the
///    <see cref="AnimatedSpriteComponent"/>'s world position already reflects the NEW pose, not the one
///    the natural (pre-mutation) component <c>Update</c> projected earlier in the very same frame.
///  - Bounds/index: same frame, <see cref="Entity.GetBoundingBox"/> and the world's spatial index follow
///    the same corrected render pose.
/// </summary>
public class AlundraEntityLogicalRenderPoseTests
{
    // AdoptPlayerPawn's own tile-to-pixel formula (AlundraWorldProxy.cs ~1006-1007), duplicated here
    // rather than made public there - see EntityRecordMapper's own class doc for the same convention.
    private const int TileWidth = 24;
    private const int TileHeight = 16;

    // -----------------------------------------------------------------------------------------
    // Formula: DeriveRenderPosition(ResolveLogicalPosition(x, y, z)) == OldResolveWorldPosition(x, y, z)
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// The formula this slice replaced (<c>AlundraWorldProxy.ResolveWorldPosition</c> before E3.a),
    /// kept here purely as the reference the new two-step (logical write + policy projection) path must
    /// reproduce exactly.
    /// </summary>
    private static Vector3 OldResolveWorldPosition(int posX, int posY, int posZ)
    {
        var pixelX = posX >> 16;
        var pixelY = posY >> 16;
        var elevationPixels = posZ >> 16;

        return new Vector3(pixelX, -pixelY + elevationPixels, 0f);
    }

    private static Vector3 DeriveRenderPositionFromLogical(int posX, int posY, int posZ)
    {
        var policy = new TopDownElevationSimulationSpacePolicy();
        return policy.DeriveRenderPosition(AlundraWorldProxy.ResolveLogicalPosition(posX, posY, posZ));
    }

    /// <summary>
    /// Map 389's own "Entities" layer, all 19 enabled records (XPos, YPos, Height), read from the real
    /// converter export - the same corpus <see cref="AlundraWorldProxySpawnInitializationTests.ShouldSpawnRecord_Map389EntitiesLayer_Keeps14Of19"/>
    /// covers for spawn gating.
    /// </summary>
    public static IEnumerable<object[]> Map389RecordPositions()
    {
        int[,] xyz =
        {
            { 34, 72, 46 }, { 36, 72, 58 }, { 38, 72, 46 }, { 34, 74, 46 }, { 36, 74, 46 },
            { 38, 74, 46 }, { 66, 71, 0 }, { 16, 71, 0 }, { 56, 95, 8 }, { 16, 97, 8 },
            { 70, 70, 0 }, { 38, 72, 50 }, { 46, 83, 10 }, { 28, 42, 20 }, { 26, 90, 10 },
            { 32, 76, 16 }, { 26, 96, 16 }, { 34, 108, 16 }, { 36, 106, 20 },
        };

        for (var i = 0; i < xyz.GetLength(0); i++)
        {
            yield return new object[] { xyz[i, 0], xyz[i, 1], xyz[i, 2] };
        }
    }

    [Theory]
    [MemberData(nameof(Map389RecordPositions))]
    public void RenderPoseFormula_Map389EntitiesLayerRecord_MatchesTheOldFormula(int xPos, int yPos, int height)
    {
        var record = new TileMapObjectData { Id = 0, Name = "Entity_Formula" };
        record.CustomProperties["XPos"] = xPos.ToString();
        record.CustomProperties["YPos"] = yPos.ToString();
        record.CustomProperties["Height"] = height.ToString();

        var proxy = new AlundraEntityScriptProxy();
        EntityRecordMapper.Map(record, proxy);

        var expected = OldResolveWorldPosition(proxy.PosX, proxy.PosY, proxy.PosZ);
        var actual = DeriveRenderPositionFromLogical(proxy.PosX, proxy.PosY, proxy.PosZ);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RenderPoseFormula_HeroNewGameSpawn_MatchesTheOldFormula()
    {
        // AdoptPlayerPawn's own New Game spawn (AlundraWorldProxy.cs ~1006-1008).
        var posX = (AlundraGameState.CameraTileX * TileWidth + TileWidth / 2) << 16;
        var posY = (AlundraGameState.CameraTileY * TileHeight + TileHeight / 2) << 16;
        const int posZ = 0;

        var expected = OldResolveWorldPosition(posX, posY, posZ);
        var actual = DeriveRenderPositionFromLogical(posX, posY, posZ);

        Assert.Equal(expected, actual);
        // Same anchor WorldWriter.ResolveTileCentreSpawn documents for the New Game PlayerStart.
        Assert.Equal(new Vector3(804f, -952f, 0f), expected);
    }

    // -----------------------------------------------------------------------------------------
    // Ordering + bounds/index
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// A fake <see cref="IEventProgramRunner"/> whose <c>RunScript</c> mutates the entity's OWN logical
    /// pose fields, standing in for a Load program that repositions the entity - exactly the kind of
    /// same-frame, mid-<c>Update</c> pose change <see cref="AlundraWorldProxy.SyncTransform"/>'s
    /// re-projection call exists for.
    /// </summary>
    private sealed class PoseMovingRunner : IEventProgramRunner
    {
        private readonly int _newPosX, _newPosY, _newPosZ;
        public int RunScriptCallCount { get; private set; }

        public PoseMovingRunner(int newPosX, int newPosY, int newPosZ)
        {
            _newPosX = newPosX;
            _newPosY = newPosY;
            _newPosZ = newPosZ;
        }

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
            RunScriptCallCount++;
            entity.PosX = _newPosX;
            entity.PosY = _newPosY;
            entity.PosZ = _newPosZ;
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    private sealed class FakeScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; }
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = new List<AlundraEntityScriptProxy>();

        public FakeScriptHost(IEventProgramRunner runner)
        {
            Runner = runner;
        }

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }
    }

    [Fact]
    public void SyncTransform_SameFrameReprojection_SpriteFollowsThePoseChangedDuringUpdate()
    {
        var (world, entity, sprite, poseA, poseB, runner) = BuildScene();

        world.AddEntity(entity);
        world.Update(1f / 50f);

        Assert.Equal(1, runner.RunScriptCallCount);

        var policy = new TopDownElevationSimulationSpacePolicy();
        var renderA = policy.DeriveRenderPosition(poseA);
        var renderB = policy.DeriveRenderPosition(poseB);

        // The natural, non-forced component Update ran BEFORE the Load program moved the proxy (root
        // component update precedes GameplayProxy.Update in Entity.Update), so without SyncTransform's
        // own explicit re-projection the sprite would still show renderA here.
        Assert.NotEqual(renderA, renderB);
        Assert.Equal(renderB, sprite.WorldMatrixNoScale.Translation);
    }

    [Fact]
    public void SyncTransform_SameFrameReprojection_BoundsAndIndexFollowTheNewPose()
    {
        var (world, entity, _, poseA, poseB, _) = BuildScene();

        world.AddEntity(entity);
        world.Update(1f / 50f);

        var policy = new TopDownElevationSimulationSpacePolicy();
        var renderA = policy.DeriveRenderPosition(poseA);
        var renderB = policy.DeriveRenderPosition(poseB);

        var boundingBox = entity.GetBoundingBox();
        Assert.NotEqual(ContainmentType.Disjoint, boundingBox.Contains(renderB));
        Assert.Equal(ContainmentType.Disjoint, boundingBox.Contains(renderA));

        var aroundNewPose = new List<Entity>();
        world.SpatialServices.WorldIndex.Query(CreateFrustumAroundPoint(renderB, 5f), aroundNewPose);
        Assert.Contains(entity, aroundNewPose);

        var aroundOldPose = new List<Entity>();
        world.SpatialServices.WorldIndex.Query(CreateFrustumAroundPoint(renderA, 5f), aroundOldPose);
        Assert.DoesNotContain(entity, aroundOldPose);
    }

    /// <summary>
    /// Builds a headless world (same montage as <c>WallPlacementOverlayTests</c>'s own world/game fixture,
    /// plus the <see cref="PhysicsWorld"/>/<see cref="GameManager"/> wiring CasaEngineMonogame's own
    /// <c>CasaEngine.Tests.Physics.TransformComponentTests</c> uses for E3.0) and one entity: root
    /// <see cref="TransformComponent"/> at logical pose A, with a <see cref="RenderProjectionComponent"/>
    /// child carrying an <see cref="AnimatedSpriteComponent"/>, and an <see cref="AlundraEntityScriptProxy"/>
    /// - resolved/cached exactly like <see cref="AlundraWorldProxy.CreateEntityFromPrefab"/> does - whose
    /// Load program (<see cref="PoseMovingRunner"/>) moves it to logical pose B.
    /// </summary>
    private static (World World, Entity Entity, AnimatedSpriteComponent Sprite, Vector3 PoseA, Vector3 PoseB, PoseMovingRunner Runner)
        BuildScene()
    {
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;

        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

        var gameManager = (GameManager)RuntimeHelpers.GetUninitializedObject(typeof(GameManager));
        var viewManagerField = typeof(GameManager).GetField("<ViewManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        viewManagerField.SetValue(gameManager, new CasaEngine.Framework.Rendering.ViewManager());
        var gameManagerField = typeof(CasaEngineGame).GetField("<GameManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        gameManagerField.SetValue(game, gameManager);

        SetProperty(world, nameof(World.Game), game);
        SetProperty(world, nameof(World.PhysicsWorld), new PhysicsWorld(false, new TopDownElevationSimulationSpacePolicy()));

        // Pose A: map 389 / Entity_0 anchor (XPos=34, YPos=72, Height=46 -> PosX=420<<16, PosY=584<<16,
        // PosZ=46<<19 - see EntityRecordMapperTests). Pose B: an arbitrary different logical position the
        // fake Load program moves the entity to mid-Update.
        var record = new TileMapObjectData { Id = 0, Name = "Entity_0" };
        record.CustomProperties["XPos"] = "34";
        record.CustomProperties["YPos"] = "72";
        record.CustomProperties["Height"] = "46";
        record.CustomProperties["EventCodesA_LoadIndex"] = "1"; // nonzero => RunPickedEvent calls RunScript, not RunSpriteEvent.

        var proxy = new AlundraEntityScriptProxy();
        AlundraWorldProxy.ApplyRecord(record, proxy); // PosX/Y/Z = pose A; Status = Loaded; ProgramIndexes[ALoad] = 1.

        var poseA = AlundraWorldProxy.ResolveLogicalPosition(proxy.PosX, proxy.PosY, proxy.PosZ);

        const int newPosX = 900 * 0x10000;
        const int newPosY = 300 * 0x10000;
        const int newPosZ = 64 << 16;
        var poseB = new Vector3(900f, 300f, 64f);

        var runner = new PoseMovingRunner(newPosX, newPosY, newPosZ);
        proxy.ScriptHost = new FakeScriptHost(runner);
        proxy.IsPlayer = false;

        var entity = new Entity { Name = "PoseTestEntity", GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        SetProperty(entity, nameof(Entity.GameplayProxy), (IGameplayProxy)proxy);

        var root = new TransformComponent();
        entity.RootComponent = root;
        root.LocalTransform.Position = poseA;

        var projection = new RenderProjectionComponent();
        root.AddChildComponent(projection);

        var sprite = new AnimatedSpriteComponent();
        projection.AddChildComponent(sprite);

        // Resolve/cache once, exactly like AlundraWorldProxy.CreateEntityFromPrefab does at spawn time -
        // a no-op here since the entity is not in a world yet (Owner.World is still null).
        proxy.RenderProjection = entity.GetComponent<RenderProjectionComponent>();
        proxy.RenderProjection?.UpdateProjection();

        return (world, entity, sprite, poseA, poseB, runner);
    }

    private static BoundingFrustum CreateFrustumAroundPoint(Vector3 point, float halfExtent)
    {
        var eye = point + new Vector3(0f, 0f, halfExtent * 4f);
        var view = Matrix.CreateLookAt(eye, point, Vector3.Up);
        var projection = Matrix.CreateOrthographic(halfExtent * 2f, halfExtent * 2f, 0.1f, halfExtent * 8f);
        return new BoundingFrustum(view * projection);
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }
}
