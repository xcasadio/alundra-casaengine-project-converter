#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// T4 (docs/plan-transitions-carte.md §3 T4) - <see cref="AlundraWarpDirector"/>: the departure
/// sequence, its own gel gate, and the map-entry disposition (D-T-15) that ends it. Every test resets
/// the FOUR session singletons this class' own montages touch, plus the three T1 introduced (D-T-14) -
/// same shape as every other class that constructs an <see cref="AlundraWorldProxy"/>. Joins
/// <see cref="AlundraMusicPlayerSingletonCollection"/> (T2.1, docs/plan-bgm-demarrage-binaire.md): the
/// n/o/p warp-departure tests arm the REAL <see cref="AlundraBgmFadeDirector.Instance"/>, which
/// <see cref="AlundraSoundPlayer.PlaySfx"/> reads directly (fact 8) - so this class must never race
/// <see cref="AlundraBgmFadeDirectorTests"/>/<see cref="AlundraMusicPlayerTests"/> on that shared state
/// either, same reasoning as <see cref="AlundraWorldProxyAudioInstallationTests"/>.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
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
        AlundraBgmFadeDirector.Instance.ResetForTests(); // T2.1 (n/o/p): the warp-departure music tests.
    }

    public void Dispose()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        AlundraScreenFadeDirector.Instance.ResetForTests();
        AlundraMusicPlayer.Instance.ResetForTests();
        AlundraBgmFadeDirector.Instance.ResetForTests();
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

    /// <summary>Same shape as <see cref="BuildGameWithGameManager"/>, plus a real
    /// <see cref="AudioSystemComponent"/> (same recipe as <c>AlundraMusicPlayerTests.BuildGameWithAudio</c>)
    /// - T2.1's own warp-departure tests (n/o/p) need BOTH <see cref="AlundraWorldProxy.InstallAudioSystems"/>
    /// (the music) and <see cref="AlundraWorldProxy.InstallWarpSystems"/> (the departure, off the SAME
    /// <see cref="GameManager"/>) on one world.</summary>
    private static CasaEngineGame BuildGameWithAudioAndGameManager(FakeAudioBackend backend, FakeAudioClipProvider provider, out GameManager gameManager)
    {
        var game = BuildGameWithGameManager(out gameManager);

        var audioComponent = (AudioSystemComponent)RuntimeHelpers.GetUninitializedObject(typeof(AudioSystemComponent));
        var serviceField = typeof(AudioSystemComponent).GetField("<Service>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        serviceField.SetValue(audioComponent, new AudioService(backend) { ClipProvider = provider });

        var audioComponentField = typeof(CasaEngineGame).GetField("<AudioSystemComponent>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        audioComponentField.SetValue(game, audioComponent);

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
        // A NON-degraded montage on purpose: with no GameManager and no world index the director's own
        // abort guard would (rightly) lift the gate the moment the fade settles, and this test would then
        // be measuring the abort path instead of the freeze. Attaching both makes Advance take the real
        // emission branch, which keeps the gate posted through to InstallForMapEntry - exactly what
        // happens in game.
        var projectRoot = FindProjectRoot();
        AlundraWarpDirector.Instance.AttachToWorld(new GameManager(null), soundPlayer: null, projectRoot);

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

    [Fact]
    public void InstallForMapEntry_ClearsAPendingWorldPathThatIsActuallyThere()
    {
        // The closing verifier found this clause pinned by a VACUOUS assertion: the montage had no world
        // index, so the pending path was null before InstallForMapEntry ever ran. Resolve a real one
        // first, and assert it is non-null, so the clearing has something to clear.
        var projectRoot = FindProjectRoot();
        AlundraWarpDirector.Instance.AttachToWorld(new GameManager(null), soundPlayer: null, projectRoot);

        var player = NewPlayer(posXPixels: 0, posYPixels: 0);
        AlundraWarpDirector.Instance.BeginDeparture(Map389Portal0(), 0x10, player, new AlundraGameState());

        Assert.NotNull(AlundraWarpDirector.Instance.PendingWorldPathForTests);

        AlundraWarpDirector.Instance.InstallForMapEntry();

        Assert.Null(AlundraWarpDirector.Instance.PendingWorldPathForTests);
    }

    [Fact]
    public void TheOutgoingFadeKeepsPushingBlackAfterItSettles_SoItSurvivesTheWorldSwitch()
    {
        // D-T-5's persistence latch. Without it the fade director's draw guard would call Clear() the
        // tick after the machine settles, and the screen would flash back to the departure map for the
        // frame the switch happens on.
        var service = new ScreenEffectService();
        AlundraScreenFadeDirector.Instance.AttachToWorld(service);

        var player = NewPlayer(posXPixels: 0, posYPixels: 0);
        AlundraWarpDirector.Instance.BeginDeparture(Map389Portal0(), 0x10, player, new AlundraGameState());

        for (var tick = 0; tick < 24; tick++)
        {
            AlundraScreenFadeDirector.Instance.Advance(1);
            AlundraScreenFadeDirector.Instance.PushToAttachedService();
        }

        Assert.True(AlundraScreenFadeDirector.Instance.IsSettled);
        Assert.True(service.Active, "the settled black must keep being submitted - that is what the persistence latch is for.");
        Assert.Equal(0xff, service.R);
    }

    [Fact]
    public void ADepartureThatCanNeverReachTheEngine_LiftsTheGateAndGivesTheHeroItsGravityBack()
    {
        // Abort guard. With no GameManager attached the world change can never be requested; doing
        // nothing would leave the gate posted for the rest of the session - player and NPCs frozen, in
        // silence. Unreachable on the shipped export, but a mute permanent lock is the worst possible
        // failure, so it fails loudly and recoverably instead.
        AlundraWarpDirector.Instance.AttachToWorld(gameManager: null, soundPlayer: null, FindProjectRoot());

        var player = NewPlayerWithController(gravity: 42f, maxFallSpeed: 7f);
        AlundraWarpDirector.Instance.BeginDeparture(Map389Portal0(), 0x10, player, new AlundraGameState());

        Assert.True(AlundraWarpDirector.Instance.IsTransitionInProgress);
        Assert.Equal(0f, player.Controller!.Settings.Gravity);

        for (var tick = 0; tick < 24; tick++)
        {
            AlundraScreenFadeDirector.Instance.Advance(1);
            AlundraWarpDirector.Instance.Advance(1);
        }

        Assert.False(AlundraWarpDirector.Instance.IsTransitionInProgress);
        Assert.Equal(42f, player.Controller!.Settings.Gravity);
        Assert.Equal(7f, player.Controller!.Settings.MaxFallSpeed);
        Assert.False(player.Controller!.IsVerticalOwnedExternally);
    }

    // -----------------------------------------------------------------------------------------------
    // T2.1 acceptance (docs/plan-bgm-demarrage-binaire.md, D8/P7): the BGM half of the departure -
    // (n)/(o)/(p). Real project data (music-index.json/sfx-manifest.json), real AlundraMusicPlayer/
    // AlundraBgmFadeDirector singletons, on the fake audio backend - same precedent as
    // AlundraMusicPlayerTests' own T1.
    // -----------------------------------------------------------------------------------------------

    /// <summary>Portal from map 389 to <paramref name="destMapId"/>, WarpBehaviorId
    /// <paramref name="warpBehaviorId"/> (Flags' own low nibble) - otherwise identical to
    /// <see cref="Map389Portal0"/>.</summary>
    private static AlundraPortalRecord Map389PortalTo(int destMapId, int warpBehaviorId) => new()
    {
        Index = 0,
        X1 = 18,
        Y1 = 38,
        X2 = 18,
        Y2 = 38,
        DestMapId = destMapId,
        DestTileX = 10,
        DestTileY = 40,
        ZLevel = 0,
        Flags = 0x5000 | warpBehaviorId,
    };

    [Fact]
    public void WarpDeparture_SilentWarpSound_ArmsFadeOnly_NeverLoadsDestination_ThenArrivalPlaysIt()
    {
        // Acceptance item (n): warp behaviour 1 -> sound 69 (seq_num -1, max_voices 0: "none", real
        // manifest). Destination map 45 -> raw index 30 (real music-index.json, != map 389's own 25).
        var projectRoot = FindProjectRoot();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectRoot;
        try
        {
            var backend = new FakeAudioBackend();
            var provider = new FakeAudioClipProvider();
            provider.Register(Track25AssetId(projectRoot), new FakeAudioClip("bgm_025", 44100));
            provider.Register(Guid.Parse("84951c69-5db3-52ed-beb0-265839a9bcbb"), new FakeAudioClip("bgm_030", 44100));
            var game = BuildGameWithAudioAndGameManager(backend, provider, out _);

            var world389 = new World { Name = "Ship-389" };
            HeroWorldFixture.SetProperty(world389, nameof(World.Game), game);
            var proxy389 = new AlundraWorldProxy();
            proxy389.InstallAudioSystems(world389);
            proxy389.InstallWarpSystems(world389);
            proxy389.Update(0.02f); // frame close: track 25 starts.
            Assert.Single(backend.PlayCalls);

            var player = NewPlayer(posXPixels: 18 * 24 + 12, posYPixels: 38 * 16 + 8);
            proxy389.PlayerEntity = player;

            var portal = Map389PortalTo(45, warpBehaviorId: 1);
            ((IAlundraScriptHost)proxy389).OnPortalTriggerDetected(portal, 0x10);

            // F2: the departure only RECORDS the request - nothing armed yet, same frame it was requested.
            Assert.Single(backend.PlayCalls);
            Assert.False(AlundraBgmFadeDirector.Instance.IsArmed);

            proxy389.Update(0.02f); // the departing world's own next frame close: evaluates the pending
                                     // departure (F2), AFTER the reset-flag consumption (B9).
            // No destination track loaded, only the fade armed.
            Assert.Single(backend.PlayCalls); // still just track 25's own play.
            Assert.True(AlundraBgmFadeDirector.Instance.IsArmed);

            var world45 = new World { Name = "Dest-45" };
            HeroWorldFixture.SetProperty(world45, nameof(World.Game), game);
            var proxy45 = new AlundraWorldProxy();
            proxy45.InstallAudioSystems(world45); // loads 30 (differs from 25) - stops the old voice.
            proxy45.Update(0.02f); // arrival's own first frame close.

            Assert.False(AlundraBgmFadeDirector.Instance.IsArmed); // disarmed by StopAllSound.
            var service = game.AudioSystemComponent.Service;
            Assert.Equal(1f, service.Mixer.GetBus(AudioBusNames.Master).Volume, 5);
            Assert.Equal(2, backend.PlayCalls.Count);
            Assert.Equal("bgm_030", ((FakeAudioClip)backend.PlayCalls[1].Clip).Name);
            Assert.True(((AlundraMusicPlayer)proxy45.MusicPlayer!).IsCurrentVoiceAlive);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    [Fact]
    public void WarpDeparture_SameFrameAsMapEntry_TrackStartsFirst_ThenFadeArmsAfter()
    {
        // F2's own entry-frame case: map entry (B7) loads track 25 and arms ResetSoundFlag, but starts
        // NO voice yet; a behaviour-1 (silent) departure to 45 is then triggered in that SAME frame,
        // before the first Update ever runs. One Update call must run BOTH in the binary's own order
        // (B9 before B17): the reset flag's own StopAllSound starts track 25's voice FIRST, and only
        // THEN does F2's pending-departure evaluation arm the fade - never the other way around.
        var projectRoot = FindProjectRoot();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectRoot;
        try
        {
            var backend = new FakeAudioBackend();
            var provider = new FakeAudioClipProvider();
            provider.Register(Track25AssetId(projectRoot), new FakeAudioClip("bgm_025", 44100));
            var game = BuildGameWithAudioAndGameManager(backend, provider, out _);

            var world389 = new World { Name = "Ship-389" };
            HeroWorldFixture.SetProperty(world389, nameof(World.Game), game);
            var proxy389 = new AlundraWorldProxy();
            proxy389.InstallAudioSystems(world389); // loads track 25, arms ResetSoundFlag - no voice yet.
            proxy389.InstallWarpSystems(world389);

            var player = NewPlayer(posXPixels: 18 * 24 + 12, posYPixels: 38 * 16 + 8);
            proxy389.PlayerEntity = player;

            var portal = Map389PortalTo(45, warpBehaviorId: 1);
            ((IAlundraScriptHost)proxy389).OnPortalTriggerDetected(portal, 0x10); // same frame, before Update.

            // Nothing started yet at all - map entry only loads and arms the flag (B7); the departure
            // only records its own request (F2).
            Assert.Empty(backend.PlayCalls);
            Assert.False(AlundraBgmFadeDirector.Instance.IsArmed);

            proxy389.Update(0.02f); // the ONE frame close both consume.

            Assert.Single(backend.PlayCalls); // track 25's own voice, started by the flag's StopAllSound.
            Assert.Equal("bgm_025", ((FakeAudioClip)backend.PlayCalls[0].Clip).Name);
            Assert.True(((AlundraMusicPlayer)proxy389.MusicPlayer!).IsCurrentVoiceAlive);
            Assert.True(AlundraBgmFadeDirector.Instance.IsArmed); // the departure ran AFTER it (B9 then B17).
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    [Fact]
    public void WarpDeparture_AudibleWarpSound_StopsTheDepartingBgm_NeverArmsTheFade_ThenArrivalPlaysDestination()
    {
        // Acceptance item (o): warp behaviour 3 -> sound 55 (seq_num -1, max_voices 4: NOT none).
        var projectRoot = FindProjectRoot();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectRoot;
        try
        {
            var backend = new FakeAudioBackend();
            var provider = new FakeAudioClipProvider();
            provider.Register(Track25AssetId(projectRoot), new FakeAudioClip("bgm_025", 44100));
            provider.Register(Guid.Parse("84951c69-5db3-52ed-beb0-265839a9bcbb"), new FakeAudioClip("bgm_030", 44100));
            var game = BuildGameWithAudioAndGameManager(backend, provider, out _);

            var world389 = new World { Name = "Ship-389" };
            HeroWorldFixture.SetProperty(world389, nameof(World.Game), game);
            var proxy389 = new AlundraWorldProxy();
            proxy389.InstallAudioSystems(world389);
            proxy389.InstallWarpSystems(world389);
            proxy389.Update(0.02f);
            Assert.Single(backend.PlayCalls);

            var player = NewPlayer(posXPixels: 18 * 24 + 12, posYPixels: 38 * 16 + 8);
            proxy389.PlayerEntity = player;

            var portal = Map389PortalTo(45, warpBehaviorId: 3);
            ((IAlundraScriptHost)proxy389).OnPortalTriggerDetected(portal, 0x10);

            // F2: the departure only RECORDS the request - the voice is still playing right after it.
            Assert.True(((AlundraMusicPlayer)proxy389.MusicPlayer!).IsCurrentVoiceAlive);
            Assert.Single(backend.PlayCalls);

            proxy389.Update(0.02f); // frame close: evaluates the pending departure (F2), AFTER the
                                     // reset-flag consumption (B9).
            // The music voice is stopped outright (LoadBgm(0)), the fade never arms.
            Assert.False(((AlundraMusicPlayer)proxy389.MusicPlayer!).IsCurrentVoiceAlive);
            Assert.False(AlundraBgmFadeDirector.Instance.IsArmed);
            Assert.Single(backend.PlayCalls); // no new play at departure either.

            var world45 = new World { Name = "Dest-45" };
            HeroWorldFixture.SetProperty(world45, nameof(World.Game), game);
            var proxy45 = new AlundraWorldProxy();
            proxy45.InstallAudioSystems(world45);
            proxy45.Update(0.02f);

            Assert.Equal(2, backend.PlayCalls.Count);
            Assert.Equal("bgm_030", ((FakeAudioClip)backend.PlayCalls[1].Clip).Name);
            Assert.True(((AlundraMusicPlayer)proxy45.MusicPlayer!).IsCurrentVoiceAlive);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    [Fact]
    public void WarpDeparture_WarpSoundWithNoTonesButARealSeqNum_IsNotSilent_StopsTheDepartingBgm()
    {
        // F3 (docs/plan-bgm-demarrage-binaire.md): warp behaviour 5 -> sound 74 (real manifest: seq_num
        // 3, max_voices 0, ZERO tones). B17's own test is SeqNum == -1 AND MaxVoices == 0 - sound 74
        // fails it on SeqNum alone (3 != -1), so it is NOT silent, even though it has no tones at all.
        // This discriminates the "silent decided by tone count" mutation (which would call it silent -
        // zero tones - and arm the fade instead of stopping outright).
        var projectRoot = FindProjectRoot();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectRoot;
        try
        {
            var backend = new FakeAudioBackend();
            var provider = new FakeAudioClipProvider();
            provider.Register(Track25AssetId(projectRoot), new FakeAudioClip("bgm_025", 44100));
            var game = BuildGameWithAudioAndGameManager(backend, provider, out _);

            var world389 = new World { Name = "Ship-389" };
            HeroWorldFixture.SetProperty(world389, nameof(World.Game), game);
            var proxy389 = new AlundraWorldProxy();
            proxy389.InstallAudioSystems(world389);
            proxy389.InstallWarpSystems(world389);
            proxy389.Update(0.02f);
            Assert.Single(backend.PlayCalls);

            var player = NewPlayer(posXPixels: 18 * 24 + 12, posYPixels: 38 * 16 + 8);
            proxy389.PlayerEntity = player;

            var portal = Map389PortalTo(45, warpBehaviorId: 5);
            ((IAlundraScriptHost)proxy389).OnPortalTriggerDetected(portal, 0x10);

            proxy389.Update(0.02f); // frame close: evaluates the pending departure (F2).

            // Stopped outright (LoadBgm(0)), never armed - same shape as (o), NOT the silent (n) shape.
            Assert.False(((AlundraMusicPlayer)proxy389.MusicPlayer!).IsCurrentVoiceAlive);
            Assert.False(AlundraBgmFadeDirector.Instance.IsArmed);
            Assert.Single(backend.PlayCalls);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    [Fact]
    public void WarpDeparture_SameRawMusicIndexAsDestination_TouchesNeitherFadeNorVoice()
    {
        // Acceptance item (p): 389 -> 390, both raw index 25 - B17's own "index égal -> rien".
        var projectRoot = FindProjectRoot();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectRoot;
        try
        {
            var backend = new FakeAudioBackend();
            var provider = new FakeAudioClipProvider();
            provider.Register(Track25AssetId(projectRoot), new FakeAudioClip("bgm_025", 44100));
            var game = BuildGameWithAudioAndGameManager(backend, provider, out _);

            var world389 = new World { Name = "Ship-389" };
            HeroWorldFixture.SetProperty(world389, nameof(World.Game), game);
            var proxy389 = new AlundraWorldProxy();
            proxy389.InstallAudioSystems(world389);
            proxy389.InstallWarpSystems(world389);
            proxy389.Update(0.02f);
            Assert.Single(backend.PlayCalls);
            var voiceBeforeDeparture = ((AlundraMusicPlayer)proxy389.MusicPlayer!).CurrentVoiceForTests;

            var player = NewPlayer(posXPixels: 18 * 24 + 12, posYPixels: 38 * 16 + 8);
            proxy389.PlayerEntity = player;

            ((IAlundraScriptHost)proxy389).OnPortalTriggerDetected(Map389Portal0(), 0x10); // DestMapId 390.

            proxy389.Update(0.02f); // frame close: evaluates the pending departure (F2) - index égal, rien.

            Assert.Single(backend.PlayCalls); // no new play, no stop: still the SAME voice.
            Assert.False(AlundraBgmFadeDirector.Instance.IsArmed);
            Assert.True(((AlundraMusicPlayer)proxy389.MusicPlayer!).IsCurrentVoiceAlive);
            Assert.Equal(voiceBeforeDeparture, ((AlundraMusicPlayer)proxy389.MusicPlayer!).CurrentVoiceForTests);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    // -----------------------------------------------------------------------------------------------
    // T4.3 (docs/plan-bgm-demarrage-binaire.md, S3): the SAME music half as (n)/(o) above, but through
    // the 0x53 path (BeginDepartureFromChangeMapOpcode, AlundraWarpDirector.cs:387) instead of the
    // portal path (:466, PlayDepartureSound). The 0x53 path has no WarpBehaviorTable - sfxId is the raw
    // v[7] operand, so 69/55 are passed directly (silent/audible, same real manifest fixtures as (n)/(o)).
    // A mutation that swaps :387's own HandleWarpDeparture call for a direct PlayMapMusic must fail these
    // two tests only.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void Warp0x53Departure_SilentWarpSound_ArmsFadeOnly_NeverLoadsDestination_ThenArrivalPlaysIt()
    {
        // Sound 69 (seq_num -1, max_voices 0: "none", same real manifest fixture as acceptance item (n)).
        // Destination map index 45 -> raw music index 30 (real music-index.json, != map 389's own 25).
        var projectRoot = FindProjectRoot();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectRoot;
        try
        {
            var backend = new FakeAudioBackend();
            var provider = new FakeAudioClipProvider();
            provider.Register(Track25AssetId(projectRoot), new FakeAudioClip("bgm_025", 44100));
            provider.Register(Guid.Parse("84951c69-5db3-52ed-beb0-265839a9bcbb"), new FakeAudioClip("bgm_030", 44100));
            var game = BuildGameWithAudioAndGameManager(backend, provider, out _);

            var world389 = new World { Name = "Ship-389" };
            HeroWorldFixture.SetProperty(world389, nameof(World.Game), game);
            var proxy389 = new AlundraWorldProxy();
            proxy389.InstallAudioSystems(world389);
            proxy389.InstallWarpSystems(world389);
            proxy389.Update(0.02f); // frame close: track 25 starts.
            Assert.Single(backend.PlayCalls);

            var player = NewPlayer(posXPixels: 18 * 24 + 12, posYPixels: 38 * 16 + 8);
            proxy389.PlayerEntity = player;

            // The 0x53 path itself, not the portal path: v[7]'s raw sfx id (69), no WarpBehaviorTable.
            AlundraWarpDirector.Instance.BeginDepartureFromChangeMapOpcode(
                desiredMapIndex: 45, posX: 10 * 24, posY: 40 * 16, posZ: 0, effectId: 0, sfxId: 69,
                player, AlundraGameState.Instance);

            // F2: the departure only RECORDS the request - nothing armed yet, same frame it was requested.
            Assert.Single(backend.PlayCalls);
            Assert.False(AlundraBgmFadeDirector.Instance.IsArmed);

            proxy389.Update(0.02f); // the departing world's own next frame close: evaluates the pending
                                     // departure (F2), AFTER the reset-flag consumption (B9).
            Assert.Single(backend.PlayCalls); // still just track 25's own play.
            Assert.True(AlundraBgmFadeDirector.Instance.IsArmed);
            Assert.True(((AlundraMusicPlayer)proxy389.MusicPlayer!).IsCurrentVoiceAlive); // track 25 still alive.

            var world45 = new World { Name = "Dest-45" };
            HeroWorldFixture.SetProperty(world45, nameof(World.Game), game);
            var proxy45 = new AlundraWorldProxy();
            proxy45.InstallAudioSystems(world45); // loads 30 (differs from 25) - stops the old voice.
            proxy45.Update(0.02f); // arrival's own first frame close.

            Assert.False(AlundraBgmFadeDirector.Instance.IsArmed); // disarmed by StopAllSound.
            Assert.Equal(2, backend.PlayCalls.Count);
            Assert.Equal("bgm_030", ((FakeAudioClip)backend.PlayCalls[1].Clip).Name);
            Assert.True(((AlundraMusicPlayer)proxy45.MusicPlayer!).IsCurrentVoiceAlive);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    [Fact]
    public void Warp0x53Departure_AudibleWarpSound_StopsTheDepartingBgm_NeverArmsTheFade_ThenArrivalPlaysDestination()
    {
        // Sound 55 (seq_num -1, max_voices 4: NOT none, same real manifest fixture as acceptance item (o)).
        var projectRoot = FindProjectRoot();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectRoot;
        try
        {
            var backend = new FakeAudioBackend();
            var provider = new FakeAudioClipProvider();
            provider.Register(Track25AssetId(projectRoot), new FakeAudioClip("bgm_025", 44100));
            provider.Register(Guid.Parse("84951c69-5db3-52ed-beb0-265839a9bcbb"), new FakeAudioClip("bgm_030", 44100));
            var game = BuildGameWithAudioAndGameManager(backend, provider, out _);

            var world389 = new World { Name = "Ship-389" };
            HeroWorldFixture.SetProperty(world389, nameof(World.Game), game);
            var proxy389 = new AlundraWorldProxy();
            proxy389.InstallAudioSystems(world389);
            proxy389.InstallWarpSystems(world389);
            proxy389.Update(0.02f);
            Assert.Single(backend.PlayCalls);

            var player = NewPlayer(posXPixels: 18 * 24 + 12, posYPixels: 38 * 16 + 8);
            proxy389.PlayerEntity = player;

            AlundraWarpDirector.Instance.BeginDepartureFromChangeMapOpcode(
                desiredMapIndex: 45, posX: 10 * 24, posY: 40 * 16, posZ: 0, effectId: 0, sfxId: 55,
                player, AlundraGameState.Instance);

            // F2: the departure only RECORDS the request - the voice is still playing right after it.
            Assert.True(((AlundraMusicPlayer)proxy389.MusicPlayer!).IsCurrentVoiceAlive);
            Assert.Single(backend.PlayCalls);

            proxy389.Update(0.02f); // frame close: evaluates the pending departure (F2), AFTER the
                                     // reset-flag consumption (B9).
            Assert.False(((AlundraMusicPlayer)proxy389.MusicPlayer!).IsCurrentVoiceAlive);
            Assert.False(AlundraBgmFadeDirector.Instance.IsArmed);
            Assert.Single(backend.PlayCalls); // no new play at departure either.

            var world45 = new World { Name = "Dest-45" };
            HeroWorldFixture.SetProperty(world45, nameof(World.Game), game);
            var proxy45 = new AlundraWorldProxy();
            proxy45.InstallAudioSystems(world45);
            proxy45.Update(0.02f);

            Assert.Equal(2, backend.PlayCalls.Count);
            Assert.Equal("bgm_030", ((FakeAudioClip)backend.PlayCalls[1].Clip).Name);
            Assert.True(((AlundraMusicPlayer)proxy45.MusicPlayer!).IsCurrentVoiceAlive);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    /// <summary>Records every <see cref="IAlundraSoundPlayer.PlaySfx"/> request - same shape as
    /// <c>AlundraEventProgramRunnerTests.FakeWarpSoundPlayer</c>, local to this class since T4.4's own
    /// test needs it here too.</summary>
    private sealed class FakeWarpSoundPlayer : IAlundraSoundPlayer
    {
        public readonly List<int> Requests = new();
        public void PlaySfx(int sfxId) => Requests.Add(sfxId);
        public void RemixVoice(int sfxId, int left, int right) { }
        public void FlushFrameSounds() { }
        public void StopAllSfx() { }
    }

    [Fact]
    public void Warp0x53Departure_SilentWarpSound379_NeverStartsItsVoice_AudibleWarpSoundStillDoes()
    {
        // T4.4 (docs/plan-bgm-demarrage-binaire.md, S1): sound 379 (real manifest: seq_num -1, max_voices
        // 0, ONE playable tone - B17's own silent test is seq_num/max_voices, not tone count) must never
        // reach PlaySfx, exactly like the executable's own 0x80049f78 zeroing. Sound 55 (seq_num -1,
        // max_voices 4: audible, same fixture as acceptance item (o)) still does.
        var projectRoot = FindProjectRoot();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectRoot;
        try
        {
            var soundPlayer = new FakeWarpSoundPlayer();
            AlundraWarpDirector.Instance.AttachToWorld(gameManager: null, soundPlayer, projectRoot);

            var player = NewPlayer(posXPixels: 18 * 24 + 12, posYPixels: 38 * 16 + 8);

            AlundraWarpDirector.Instance.BeginDepartureFromChangeMapOpcode(
                desiredMapIndex: 45, posX: 10 * 24, posY: 40 * 16, posZ: 0, effectId: 0, sfxId: 379,
                player, AlundraGameState.Instance);

            Assert.DoesNotContain(379, soundPlayer.Requests); // silent - never played.

            AlundraWarpDirector.Instance.BeginDepartureFromChangeMapOpcode(
                desiredMapIndex: 45, posX: 10 * 24, posY: 40 * 16, posZ: 0, effectId: 0, sfxId: 55,
                player, AlundraGameState.Instance);

            Assert.Contains(55, soundPlayer.Requests); // audible - still played.
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    /// <summary>The real <c>Musics/bgm-manifest.json</c>'s own asset id for sound index 25 (389/390's
    /// own track) - same lookup T1 (<c>AlundraMusicPlayerTests</c>) performs.</summary>
    private static Guid Track25AssetId(string projectRoot)
    {
        var manifestJson = System.IO.File.ReadAllText(System.IO.Path.Combine(projectRoot, "Musics", "bgm-manifest.json"));
        using var manifestDoc = System.Text.Json.JsonDocument.Parse(manifestJson);
        var assetIdText = manifestDoc.RootElement.EnumerateArray()
            .First(e => e.GetProperty("sound_index").GetInt32() == 25)
            .GetProperty("asset_id").GetString()!;
        return Guid.Parse(assetIdText);
    }

    private static AlundraEntityScriptProxy NewPlayerWithController(float gravity, float maxFallSpeed)
    {
        var player = NewPlayer(posXPixels: 0, posYPixels: 0);
        var controller = new CharacterControllerComponent();
        controller.Settings.Gravity = gravity;
        controller.Settings.MaxFallSpeed = maxFallSpeed;
        controller.IsVerticalOwnedExternally = false;
        player.Controller = controller;
        return player;
    }

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
