#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// T4 (docs/plan-transitions-carte.md §3 T4) - <see cref="AlundraWarpDirector"/>: the departure
/// sequence, its own gel gate, and the map-entry disposition (D-T-15) that ends it. Every test resets
/// the FOUR session singletons this class' own montages touch, plus the three T1 introduced (D-T-14) -
/// same shape as every other class that constructs an <see cref="AlundraWorldProxy"/>.
/// </summary>
public sealed class AlundraWarpDepartureTests : IDisposable
{
    public AlundraWarpDepartureTests()
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

    // ---- fixtures ---------------------------------------------------------------------------------

    /// <summary>Portal 0 of map 389 (§1.1.c/§1.1.d): mono-cell (18,38), destination 390 tile (10,40),
    /// Flags 0x5001 (RequiredFacing 1, ArrivalDirection 1, TransitionEffect 0, WarpBehavior 1).</summary>
    private static AlundraPortalRecord Map389Portal0() => new()
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
        Flags = 0x5001,
    };

    private static CasaEngineGame BuildGameWithGameManager(out GameManager gameManager)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));

        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

        gameManager = new GameManager(game);
        var gameManagerField = typeof(CasaEngineGame).GetField("<GameManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        gameManagerField.SetValue(game, gameManager);

        return game;
    }

    private static string? GetPendingWorldToLoad(GameManager gameManager)
    {
        var field = typeof(GameManager).GetField("_worldToLoad", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string?)field.GetValue(gameManager);
    }

    /// <summary>Wires <c>entity.GameplayProxy</c> to the returned proxy through the real
    /// <c>GameplayProxyClassName</c>/<see cref="Entity.Initialize()"/> factory path (NOT
    /// <c>proxy.Initialize(new Entity())</c>, which only sets <c>Owner</c> and leaves the entity's own
    /// <c>GameplayProxy</c> null - a gap <see cref="AlundraFrameSyncPasses.SyncAnimation"/> silently
    /// no-ops on, since it keys off <c>entity.GameplayProxy</c>, not <c>proxy.Owner</c>) - same pattern
    /// as <see cref="NewNpcEntityWithEventTrigger"/>/<see cref="AlundraWorldProxyEntityManipulationTests.NewEntityWithProxy"/>.</summary>
    private static AlundraEntityScriptProxy NewPlayer(int posXPixels, int posYPixels)
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

    private static Entity NewNpcEntityWithEventTrigger()
    {
        var entity = new Entity { Name = "npc", GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        entity.Initialize();
        var proxy = (AlundraEntityScriptProxy)entity.GameplayProxy;
        proxy.Status = EntityStatus.Normal;
        proxy.IsPlayer = false;
        proxy.EventTrigger = ScriptHelper.ProgramCTick; // ProgramIndexes[2] defaults to 0 -> RunSpriteEvent.
        return entity;
    }

    private static void AddSpawnedEntity(AlundraWorldProxy proxy, Entity entity)
    {
        var field = typeof(AlundraWorldProxy).GetField("_spawnedEntities", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((List<Entity>)field.GetValue(proxy)!).Add(entity);
    }

    // -----------------------------------------------------------------------------------------------
    // Acceptance bullet 1: full sequence from the production trigger to the world-change request, with
    // the EXACT arrival position for portal 0 of the 389 - §1.2.c arithmetic, PlayerManager.cs:3497-3509.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void OnPortalTriggerDetected_Map389Portal0_ArmsExactArrivalRecord_AndRequestsWorldOnlyAfterFadeSettles()
    {
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = FindProjectRoot();
        try
        {
            var game = BuildGameWithGameManager(out var gameManager);
            var world = new World { Name = "TestWorld" };
            HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

            // InstallWarpSystems directly (same precedent as AlundraScreenFadeDirectorTests' own
            // InstallScreenFadeSystems calls): this world has no "tileMap" entity, so the full
            // InitializeWithWorld would exit through its early return (AlundraWorldProxy.cs:515-520)
            // before ever reaching the install block this test needs.
            var proxy = new AlundraWorldProxy();
            proxy.InstallWarpSystems(world);

            // Player standing exactly on the source tile's own centre (18,38) - deltaX/deltaY reduce to
            // (DestTileX*24, DestTileY*16), §1.1.d/§1.2.c's own arithmetic.
            var player = NewPlayer(posXPixels: 18 * 24 + 12, posYPixels: 38 * 16 + 8);
            player.Controller = new CharacterControllerComponent
            {
                Settings = new CharacterControllerSettings { Gravity = 500f, MaxFallSpeed = 800f },
            };
            proxy.PlayerEntity = player;

            var portal = Map389Portal0();
            var arrivalDirectionId = AnimationTables.CardinalDirectionTable[portal.ArrivalDirectionIndex];
            Assert.Equal(0x10u, arrivalDirectionId); // §1.2.c's own note: index 1 gives 0x10, never 1.

            ((IAlundraScriptHost)proxy).OnPortalTriggerDetected(portal, arrivalDirectionId);

            // The gel starts THIS SAME call (D-T-6).
            Assert.True(AlundraWarpDirector.Instance.IsTransitionInProgress);

            // Exact arrival record (§1.2.c/T4's own acceptance bullet):
            // target (10*24+12, 40*16+8) << 16, Z = 0 before clamp; internal map index 390 (identity
            // table); animation 0x36; direction 0x10.
            var record = AlundraWarpDirector.Instance.ArrivalRecordForTests;
            Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
            Assert.Equal(390u, record.MapIndex);
            Assert.Equal((10 * 24 + 12) << 16, record.PosX);
            Assert.Equal((40 * 16 + 8) << 16, record.PosY);
            Assert.Equal(0, record.PosZ);
            Assert.Equal(0x36u, record.AnimationId);
            Assert.Equal(0x10u, record.DirectionId);
            Assert.Equal(0, record.EffectId); // TransitionEffectId bits of 0x5001 are 0.

            // D-T-5: outgoing fade armed with the persistence latch, not yet settled.
            Assert.False(AlundraScreenFadeDirector.Instance.IsSettled);

            // [R6] reserve #1: the hero's engine-driven gravity is suspended for the departure - see
            // AlundraPlayerManager.SuspendGravityForWarpDeparture's own doc.
            Assert.Equal(0f, player.Controller.Settings.Gravity);
            Assert.Equal(0f, player.Controller.Settings.MaxFallSpeed);
            Assert.True(player.Controller.IsVerticalOwnedExternally);

            // §1.1.e: the world path is ALREADY resolved (transported), but must not be requested before
            // the fade has settled - mutation "émettre la demande de monde avant stabilisation" fails here.
            var expectedPath = "Maps\\The Klark\\Ship Klark (inner)-390\\Ship Klark (inner)-390.world";
            Assert.Null(GetPendingWorldToLoad(gameManager));

            for (var i = 0; i < 20; i++)
            {
                proxy.Update(1f / 50f);
                var pending = GetPendingWorldToLoad(gameManager);
                Assert.True(pending == null || AlundraScreenFadeDirector.Instance.IsSettled);
            }

            Assert.Equal(expectedPath, GetPendingWorldToLoad(gameManager));
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Gel: 16 ticks of the outgoing fade, at least one NPC AND one entity event program (the [R6]
    // reserve #2 gap - RunPendingEventTriggers, no production-site test existed before this) AND the
    // player itself do not advance a tick.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void Update_FreezesNpcEventProgram_AndPlayer_WhileTransitionInProgress_ThenResumesOnceLifted()
    {
        var world = new World { Name = "TestWorld" };
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world); // no tileMap entity - early return, harmless for this montage.

        var npcEntity = NewNpcEntityWithEventTrigger();
        AddSpawnedEntity(proxy, npcEntity);
        var npc = (AlundraEntityScriptProxy)npcEntity.GameplayProxy;
        npc.ScriptHost = proxy;
        npc.TargetAnimationId = 5;
        npc.CurrentAnimationId = 9; // != Target, so SyncAnimation would commute if it ran (§1.5).

        var player = NewPlayer(posXPixels: 0, posYPixels: 0);
        player.ScriptHost = proxy;
        player.TargetAnimationId = 5;
        player.CurrentAnimationId = 9;
        proxy.PlayerEntity = player;

        // Arm the gel directly (this test is about the FREEZE, not the trigger arithmetic - covered by
        // the previous test) - same director state OnPortalTriggerDetected would have armed.
        AlundraWarpDirector.Instance.BeginDeparture(Map389Portal0(), 0x10, player, proxy.GameState);
        Assert.True(AlundraWarpDirector.Instance.IsTransitionInProgress);

        var runner = (AlundraEventProgramRunner)proxy.EventProgramRunner;
        var spriteRunsBeforeAnyTick = runner.SpriteEventRunCount;

        for (var tick = 0; tick < 16; tick++)
        {
            proxy.Update(1f / 50f);

            // "un programme d'événement d'entité n'avance pas d'un tick" - RunPendingEventTriggers is
            // gated the SAME way as RunMapEventsPass (D-T-6) - the [R6] reserve #2 this test closes.
            Assert.Equal(spriteRunsBeforeAnyTick, runner.SpriteEventRunCount);
            Assert.Equal(ScriptHelper.ProgramCTick, npc.EventTrigger); // never consumed (RunPickedEvent sets -1).

            // "un PNJ ... n'avance pas d'un tick" (own entity-side Update, §1.5's SyncAnimation row).
            npc.Update(1f / 50f);
            Assert.Equal(9u, npc.CurrentAnimationId);

            // "le joueur non plus".
            player.Update(1f / 50f);
            Assert.Equal(9u, player.CurrentAnimationId);
        }

        // Lift the gate the same way T5's own map entry will (D-T-15) - a second InstallForMapEntry.
        AlundraWarpDirector.Instance.InstallForMapEntry();
        Assert.False(AlundraWarpDirector.Instance.IsTransitionInProgress);

        proxy.Update(1f / 50f);
        Assert.Equal(spriteRunsBeforeAnyTick + 1, runner.SpriteEventRunCount);
        Assert.Equal(ScriptHelper.ProgramUnknown, npc.EventTrigger);

        npc.Update(1f / 50f);
        Assert.Equal(5u, npc.CurrentAnimationId);

        player.Update(1f / 50f);
        Assert.Equal(5u, player.CurrentAnimationId);
    }

    // -----------------------------------------------------------------------------------------------
    // Dégel, reformulated on what a two-world montage can actually refute (R4) - style
    // AlundraScreenFadeDirectorTests' own T7 (:370-412).
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void InstallForMapEntry_OnArrivalWorld_LiftsGate_ClearsSequenceAndPendingWorldPath_KeepsArrivalRecordReadable()
    {
        var world1 = new World { Name = "DepartureWorld" };
        var proxy1 = new AlundraWorldProxy();
        proxy1.InitializeWithWorld(world1);

        var player = NewPlayer(posXPixels: 18 * 24 + 12, posYPixels: 38 * 16 + 8);
        proxy1.PlayerEntity = player;

        var portal = Map389Portal0();
        ((IAlundraScriptHost)proxy1).OnPortalTriggerDetected(portal, 0x10);

        Assert.True(AlundraWarpDirector.Instance.IsTransitionInProgress);
        Assert.True(AlundraWarpDirector.Instance.IsDepartureArmedForTests);
        var recordBeforeArrival = AlundraWarpDirector.Instance.ArrivalRecordForTests;

        // Same singleton, second world - the montage T7 already proves is the real cross-world shape
        // (AlundraScreenFadeDirectorTests:370-412). InstallWarpSystems directly (this world has no
        // "tileMap" entity either - see the previous test's own comment on InitializeWithWorld's early
        // return).
        var world2 = new World { Name = "ArrivalWorld" };
        var proxy2 = new AlundraWorldProxy();
        proxy2.InstallWarpSystems(world2); // runs AlundraWarpDirector.InstallForMapEntry (D-T-15).

        Assert.False(AlundraWarpDirector.Instance.IsTransitionInProgress);
        Assert.False(AlundraWarpDirector.Instance.IsDepartureArmedForTests);
        Assert.Null(AlundraWarpDirector.Instance.PendingWorldPathForTests);

        // CONSERVED - T5's own AdoptPlayerPawn/InstallScreenFadeSystems have not run in this DLL slice,
        // so nothing has consumed it yet; it must still read back exactly as armed.
        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
        Assert.Equal(recordBeforeArrival, AlundraWarpDirector.Instance.ArrivalRecordForTests);
    }

    [Fact]
    public void Update_ProductionSite_FrozenPassesResume_OnceGateIsLifted()
    {
        var world = new World { Name = "TestWorld" };
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        var npcEntity = NewNpcEntityWithEventTrigger();
        AddSpawnedEntity(proxy, npcEntity);
        var npc = (AlundraEntityScriptProxy)npcEntity.GameplayProxy;
        npc.ScriptHost = proxy;

        var player = NewPlayer(0, 0);
        player.ScriptHost = proxy;
        proxy.PlayerEntity = player;

        AlundraWarpDirector.Instance.BeginDeparture(Map389Portal0(), 0x10, player, proxy.GameState);
        var runner = (AlundraEventProgramRunner)proxy.EventProgramRunner;
        var before = runner.SpriteEventRunCount;

        proxy.Update(1f / 50f);
        Assert.Equal(before, runner.SpriteEventRunCount); // frozen.

        AlundraWarpDirector.Instance.InstallForMapEntry();
        proxy.Update(1f / 50f);
        Assert.Equal(before + 1, runner.SpriteEventRunCount); // resumed - the site of production test.
    }

    // ---- shared project-root lookup (same precedent as AlundraDialogueOpcodesProductionTests) --------

    private static string FindProjectRoot()
    {
        var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = System.IO.Path.Combine(directory.FullName, "alundra-project");
            if (System.IO.Directory.Exists(System.IO.Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraWarpDepartureTests: no 'alundra-project/Maps' directory found above "
            + $"'{AppContext.BaseDirectory}' - this test needs the real converter export.");
    }
}
