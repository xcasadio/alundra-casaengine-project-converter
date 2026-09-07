using System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// The end-to-end recette point of docs/plan-e11-audio.md, slice E11.a's own acceptance (§4): on the
/// REAL install path (<see cref="AlundraWorldProxy.InstallAudioSystems"/>) for world
/// "Ship Klark (beginning)-389", the four intro sound ids (300/301/302/61) resolve to their tone files -
/// and the same wiring fails (no <see cref="AlundraWorldProxy.SoundPlayer"/> at all) when there is no
/// <c>Game</c>/<c>AudioSystemComponent</c> to install against, i.e. the bank is never reachable through
/// this seam without installation.
///
/// A live <see cref="AudioSystemComponent"/> needs a real MonoGame <c>Game</c>/<c>GraphicsDevice</c> to
/// construct normally - unavailable in this headless test process - so, like
/// <see cref="HeroWorldFixture.BuildWorld"/> already does for <c>World.Game</c> itself, both the
/// <c>Game</c> and its <see cref="AudioSystemComponent"/> are built via
/// <see cref="RuntimeHelpers.GetUninitializedObject"/> plus direct backing-field writes: this is still
/// the REAL <see cref="AlundraWorldProxy.InstallAudioSystems"/>/<see cref="AudioService"/>/
/// <see cref="AlundraSoundPlayer"/> production code, only the otherwise-unconstructible MonoGame shell
/// around it is faked.
/// </summary>
/// docs/plan-e11c-musique.md, slice C1: <see cref="InstallAudioSystems"/> now also re-points the
/// SESSION-scoped <see cref="AlundraMusicPlayer.Instance"/> singleton (D-C-6) - so this class shares
/// the <see cref="AlundraMusicPlayerSingletonCollection"/> xunit collection with
/// <see cref="AlundraMusicPlayerTests"/>, the only other class touching that same shared instance,
/// keeping them from racing (xunit runs different test CLASSES in parallel by default).
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public class AlundraWorldProxyAudioInstallationTests : IDisposable
{
    private const string WorldName = "Ship Klark (beginning)-389";

    public AlundraWorldProxyAudioInstallationTests()
    {
        // D-T-14 (docs/plan-transitions-carte.md, slice T1): this class constructs an AlundraWorldProxy,
        // so it shares the three session carriers T1 introduces - reset them here (constructor, the
        // isolation-carrying element) so no earlier test's state leaks in.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraSoundGroupIndexTable.ResetForTests(); // B3 (D-B-7): joins the session carriers this class resets.
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.
    }

    public void Dispose()
    {
        // D-T-14: hygiene, not covered by the acceptance (the constructor above is what carries
        // isolation) - kept for symmetry with the existing session-singleton test classes.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraSoundGroupIndexTable.ResetForTests(); // B3 (D-B-7): joins the session carriers this class resets.
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Sounds")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraWorldProxyAudioInstallationTests: no 'alundra-project/Sounds' directory found above "
            + $"'{AppContext.BaseDirectory}' (docs/plan-e11-audio.md, slice E11.a).");
    }

    private static CasaEngineGame BuildGameWithAudio(FakeAudioBackend backend, FakeAudioClipProvider provider)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));

        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

        // AudioSystemComponent's real constructor needs a live AssetContentManager (for its
        // AssetContentManagerAudioClipProvider) that this headless game never has - so the component
        // itself is built uninitialized too, with its own real AudioService wired directly onto its
        // backing field. Everything downstream of THAT (AlundraWorldProxy.InstallAudioSystems,
        // AlundraSoundPlayer, AlundraSoundBank) runs unmodified production code.
        var audioComponent = (AudioSystemComponent)RuntimeHelpers.GetUninitializedObject(typeof(AudioSystemComponent));
        var serviceField = typeof(AudioSystemComponent).GetField("<Service>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        serviceField.SetValue(audioComponent, new AudioService(backend) { ClipProvider = provider });

        var audioComponentField = typeof(CasaEngineGame).GetField("<AudioSystemComponent>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        audioComponentField.SetValue(game, audioComponent);

        return game;
    }

    [Fact]
    public void InstallAudioSystems_RealGame_WiresARealSoundPlayer_ResolvingAllFourIntroSoundsToToneFiles()
    {
        var projectRoot = FindProjectRoot();
        var world = new World();
        var game = BuildGameWithAudio(new FakeAudioBackend(), new FakeAudioClipProvider());
        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

        var proxy = new AlundraWorldProxy { SoundBank = new AlundraSoundBank(projectRoot) };
        proxy.InstallAudioSystems(world);

        Assert.NotNull(proxy.SoundPlayer);
        Assert.IsType<AlundraSoundPlayer>(proxy.SoundPlayer);

        // The exact seam AlundraSoundPlayer itself resolves through - all four intro ids (§1.1) must be
        // playable on the real install path.
        foreach (var (sfxId, expectedFirstToneFile) in new[]
                 {
                     (300, "sfx_0300.wav"),
                     (301, "sfx_0301.wav"),
                     (302, "sfx_0302_0.wav"),
                     (61, "sfx_0061.wav"),
                 })
        {
            var resolved = proxy.SoundBank.TryResolve(sfxId, soundGroup: null, out var resolution);
            Assert.True(resolved, $"expected sfx id {sfxId} to resolve on the real install path.");
            Assert.Equal(expectedFirstToneFile, resolution.Tones[0].File);
        }
    }

    /// <summary>
    /// Pins the OWNER that the real install path hands to <see cref="AlundraSoundPlayer"/> — the
    /// production decision itself, not just the constructor plumbing.
    ///
    /// <para>Found in main-session verification of slice C1: the ownership test in
    /// <c>AlundraSoundPlayerTests</c> builds the player DIRECTLY with an explicit owner, so it proves
    /// the parameter is honoured but is blind to what <see cref="AlundraWorldProxy.InstallAudioSystems"/>
    /// actually passes. Changing that one call site to any non-world object left the whole suite green
    /// — the "no slice without a test traversing the production call site" rule, unsatisfied.</para>
    ///
    /// <para>What it guards (fact 1.7 of docs/plan-e11c-musique.md): <c>World.Clear</c> stops voices by
    /// <c>ReferenceEquals(entry.Owner, world)</c>, and a fresh <see cref="AlundraSoundPlayer"/> is built
    /// per world — so any owner other than the world leaves sound effects running past their world.</para>
    /// </summary>
    [Fact]
    public void InstallAudioSystems_PlaysSfxOwnedByTheWorldItself_SoClearingTheWorldStopsThem()
    {
        var projectRoot = FindProjectRoot();
        var world = new World { Name = WorldName };
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        var game = BuildGameWithAudio(backend, provider);
        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

        var proxy = new AlundraWorldProxy { SoundBank = new AlundraSoundBank(projectRoot) };
        proxy.InstallAudioSystems(world);
        Assert.NotNull(proxy.SoundPlayer);

        var service = game.AudioSystemComponent.Service;

        // Register a clip for every tone of sfx 300 so the real player can actually start a voice.
        Assert.True(proxy.SoundBank.TryResolve(300, soundGroup: null, out var resolution));
        foreach (var tone in resolution.Tones)
        {
            provider.Register(tone.AssetId, new FakeAudioClip());
        }

        proxy.SoundPlayer!.PlaySfx(300);
        Assert.True(service.ActiveVoiceCount > 0, "the sfx should have started a voice on the real install path.");

        // The production decision under test: these voices must belong to the WORLD, which is what
        // World.Clear passes when it tears the world down.
        service.StopVoicesOwnedBy(world);

        Assert.Equal(0, service.ActiveVoiceCount);
    }

    [Fact]
    public void InstallAudioSystems_NoGame_LeavesSoundPlayerNull_TheSoundsAreUnreachableWithoutInstallation()
    {
        var world = new World(); // World.Game left null - no Game/AudioSystemComponent to install against.
        var proxy = new AlundraWorldProxy { SoundBank = new AlundraSoundBank(FindProjectRoot()) };

        proxy.InstallAudioSystems(world);

        // The bank itself would still resolve the ids (it's a plain file read) - but PROVING the
        // installation matters: without it, the interpreter's own seam (IEntityWorldContext.SoundPlayer)
        // is null, so 0xBD/0xBE/0x12/0x75 can never reach AlundraSoundPlayer.PlaySfx at all, regardless
        // of what the bank could resolve.
        Assert.Null(proxy.SoundPlayer);
    }

    // -----------------------------------------------------------------------------------------------
    // B3 (docs/plan-e11b-opcodes-audio.md, D-B-7): the sound group reaches AlundraSoundBank.TryResolve
    // through the REAL InstallAudioSystems install site (not the direct-constructor tests in
    // AlundraSoundPlayerTests) - a synthetic project fixture, isolated from the real "alundra-project"
    // one every other test in this class reads, since Maps/sound-group-index.json is not re-exported by
    // this slice.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void InstallAudioSystems_LoadsTheMapsOwnSoundGroup_SoPlaySfxRedirectsThroughRefSfxIdChain()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "AlundraWorldProxyAudioInstallationTests_Group_" + Guid.NewGuid());
        var soundsDir = Path.Combine(projectPath, "Sounds");
        var mapsDir = Path.Combine(projectPath, "Maps");
        Directory.CreateDirectory(soundsDir);
        Directory.CreateDirectory(mapsDir);

        // Id 700's own vab_id (99) is foreign to map 389's own group (56, below) - InstallAudioSystems
        // must resolve that group off Maps/sound-group-index.json and hand it to the real
        // AlundraSoundPlayer it installs, so PlaySfx(700) follows RefSfxId to id 701 (vab_id 56).
        var id701Tone0 = Guid.Parse("00000000-0000-0000-0000-000000000701");
        File.WriteAllText(Path.Combine(soundsDir, "sfx-manifest.json"), $$"""
        [
          {
            "id": 700, "vab_id": 99, "program_number": 0, "tone_number": 0, "note": 60,
            "seq_num": -1, "ref_sfx_id": 701, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "tones": [
              { "tone_index": 0, "file": "sfx_0700.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 0, "repeat": false, "asset_id": "{{Guid.Empty}}" }
            ]
          },
          {
            "id": 701, "vab_id": 56, "program_number": 0, "tone_number": 0, "note": 60,
            "seq_num": -1, "ref_sfx_id": 0, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "tones": [
              { "tone_index": 0, "file": "sfx_0701.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 0, "repeat": false, "asset_id": "{{id701Tone0}}" }
            ]
          }
        ]
        """);
        File.WriteAllText(Path.Combine(mapsDir, "sound-group-index.json"), """{"389":56}""");

        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectPath; // InstallAudioSystems reads the group table off this.

        try
        {
            var world = new World { Name = WorldName }; // "Ship Klark (beginning)-389" -> map id 389.
            var backend = new FakeAudioBackend();
            var provider = new FakeAudioClipProvider();
            provider.Register(id701Tone0, new FakeAudioClip("sfx_0701", 11025));
            var game = BuildGameWithAudio(backend, provider);
            HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

            var proxy = new AlundraWorldProxy { SoundBank = new AlundraSoundBank(projectPath) };
            proxy.InstallAudioSystems(world);
            Assert.NotNull(proxy.SoundPlayer);

            proxy.SoundPlayer!.PlaySfx(700);

            // The mutation this test kills: the group never passed (null) at the install site - id 700
            // would then resolve to its OWN record (vab_id 99, no group to redirect against) and play
            // sfx_0700's clip (unregistered here) instead of following the chain to 701.
            Assert.Single(backend.PlayCalls);
            Assert.Equal("sfx_0701", ((FakeAudioClip)backend.PlayCalls[0].Clip).Name);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(projectPath, recursive: true);
        }
    }

    // -----------------------------------------------------------------------------------------------
    // B1 (docs/plan-e11b-opcodes-audio.md, D-B-4): the anti-duplicate table's own frame ownership,
    // exercised through the REAL AlundraWorldProxy.Update path (not AlundraSoundPlayer directly) - the
    // observable is the fake backend's own voices. Every setup below installs a REAL AlundraSoundPlayer
    // over a REAL AudioService(FakeAudioBackend) and drives dispatch through a REAL
    // AlundraEventProgramRunner, exactly like production.
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// One slot-B (map-event) program that plays <paramref name="sfxId"/> via 0x12 (PlaySound1) then
    /// ends - wired as <paramref name="proxy"/>'s own <see cref="AlundraWorldProxy.EventProgramRunner"/>,
    /// with <see cref="AlundraWorldProxy.PlayerEntity"/> seeded inside the map event's own zone (same
    /// montage as <c>AlundraWorldProxyUpdateCharacterizationTests.MapEventTeleport_IsVisibleToCameraTarget_SameFrame</c>).
    /// </summary>
    private static AlundraEntityScriptProxy WireMapEventSoundTrigger(AlundraWorldProxy proxy, int sfxId)
    {
        var player = new AlundraEntityScriptProxy
        {
            Status = EntityStatus.Normal,
            TileX = 0,
            TileY = 0,
        };
        proxy.PlayerEntity = player;

        var record = new TileMapObjectData();
        record.CustomProperties["EventCodesBIndex"] = "129"; // masked 0x7f = 1, non-dud (see the
                                                               // characterization test's own comment).
        record.CustomProperties["Index"] = "1";
        record.CustomProperties["X1"] = "0";
        record.CustomProperties["Y1"] = "0";
        record.CustomProperties["X2"] = "100";
        record.CustomProperties["Y2"] = "100";
        var mapEventsLayer = new TileMapObjectLayerData();
        mapEventsLayer.Objects.Add(record);
        proxy.BuildMapEvents(mapEventsLayer);

        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesBTable = new[] { 0, 0 },
            // PlaySound2(sfxId); End - 0xBD, not 0x12: 0x12's own id operand is a single byte (0-255),
            // too narrow for sfxId 300 (0x12C) below (EventProgramDocument.Codes is byte-range data,
            // see its own doc - a value over 255 there would just get truncated).
            Codes = new[] { 0xBD, sfxId & 0xFF, (sfxId >> 8) & 0xFF, 0xFF },
        };
        proxy.EventProgramRunner = new AlundraEventProgramRunner(document, proxy.GameState, proxy);

        return player;
    }

    /// <summary>
    /// A SECOND entity, driven through <see cref="AlundraWorldProxy.RunPendingEventTriggers"/> (the D3
    /// catch-up "rattrapage" pass, NOT <see cref="AlundraWorldProxy.RunMapEventsPass"/>'s own map-events
    /// pass) - its own slot-C (Tick) program plays the SAME <paramref name="sfxId"/> via 0x12, so a
    /// single tick dispatches it from a DIFFERENT pass than <see cref="WireMapEventSoundTrigger"/>'s own
    /// map event. Added to <paramref name="proxy"/>'s own private <c>_spawnedEntities</c> by reflection
    /// (no internal seam exists for it, unlike <see cref="AlundraWorldProxy.PlayerEntity"/>/<see
    /// cref="AlundraWorldProxy.BuildMapEvents"/>) so <c>RefreshUpdateProxiesAndCollidables</c> picks it
    /// up into <c>_updateProxies</c> on the next <see cref="AlundraWorldProxy.Update"/> call. The SAME
    /// document/table shape as the map event (Table[1]=0, same 3-byte program) - slot C reads
    /// <c>EventCodesCTable</c> instead of B, off the SAME underlying <c>Codes</c> array.
    /// </summary>
    private static void WireRescanSoundTrigger(AlundraWorldProxy proxy, int sfxId)
    {
        var document = (EventProgramDocument)typeof(AlundraEventProgramRunner)
            .GetField("_document", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(proxy.EventProgramRunner)!;
        document.EventCodesCTable = new[] { 0, 0 };

        var entity = new Entity { GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        entity.Initialize();
        var rescanProxy = (AlundraEntityScriptProxy)entity.GameplayProxy!;
        rescanProxy.IsPlayer = false;
        rescanProxy.ProgramIndexes[ScriptHelper.ProgramCTick] = 129; // masked 0x7f = 1, same Table[1]=0.
        rescanProxy.EventTrigger = ScriptHelper.ProgramCTick;

        var spawnedEntitiesField = typeof(AlundraWorldProxy).GetField("_spawnedEntities", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var spawnedEntities = (List<Entity>)spawnedEntitiesField.GetValue(proxy)!;
        spawnedEntities.Add(entity);
    }

    /// <summary>Resolves <paramref name="sfxId"/> against <paramref name="proxy"/>'s own real
    /// <see cref="AlundraSoundBank"/> and registers a fake clip for every one of its tones, so
    /// <see cref="AlundraSoundPlayer.PlaySfx"/> can actually start a backend voice.</summary>
    private static void RegisterClipsFor(AlundraWorldProxy proxy, FakeAudioClipProvider provider, int sfxId)
    {
        Assert.True(proxy.SoundBank.TryResolve(sfxId, soundGroup: null, out var resolution));
        foreach (var tone in resolution.Tones)
        {
            provider.Register(tone.AssetId, new FakeAudioClip());
        }
    }

    [Fact]
    public void Update_TwoTicksInOneRenderedFrame_SameId_ProducesOnlyOneBackendVoice()
    {
        // D-B-4 cadence (a): the deviation ASSUMED (a same-tick, i.e. per-Update, anti-duplicate window
        // instead of the original's own per-50Hz-frame one) and PROVEN here - RunMapEventsPass runs
        // ONCE PER TICK (AlundraWorldProxy.Update's own ticksThisFrame loop), so a rendered frame
        // carrying 2 logic ticks dispatches the SAME map event's 0x12 twice. The anti-duplicate table
        // must still refuse the second dispatch: the mutation "flush at the head of RunMapEventsPass"
        // (called once per TICK, not once per Update) fails this test by producing 2 voices.
        var projectRoot = FindProjectRoot();
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        var world = new World();
        var game = BuildGameWithAudio(backend, provider);
        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

        var proxy = new AlundraWorldProxy { SoundBank = new AlundraSoundBank(projectRoot) };
        proxy.InstallAudioSystems(world);
        // sfxId 300: real manifest MaxVoices=2 (single tone) - high enough that the anti-duplicate
        // table, not MaxVoices, is what has to be the thing refusing the second tick's request.
        RegisterClipsFor(proxy, provider, sfxId: 300);
        WireMapEventSoundTrigger(proxy, sfxId: 300);

        // First Update() call ever on this proxy -> the sticky first-frame tick floor would force >=1
        // tick regardless, but 0.04s/0.02s = 2 ticks already clears that floor on its own.
        proxy.Update(0.04f);

        Assert.Equal(1, backend.ActiveVoiceCount);
    }

    [Fact]
    public void Update_SameTick_TwoDifferentDispatchPasses_SameId_ProducesOnlyOneVoice_AndReplaysNextFrame()
    {
        // D-B-4 cadence (b): within the SAME tick, RunMapEventsPass (map events) and
        // RunPendingEventTriggers (the D3 "rattrapage" catch-up re-scan) BOTH dispatch a program that
        // requests the same sfx id. The flush living at Update's own frame-close site (next to
        // _logicClock.CloseFrame(), AFTER both passes) means the SECOND pass's request is refused - the
        // mutation "flush at the head of a dispatch pass" (either one) would let it through instead.
        var projectRoot = FindProjectRoot();
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        var world = new World();
        var game = BuildGameWithAudio(backend, provider);
        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

        var proxy = new AlundraWorldProxy { SoundBank = new AlundraSoundBank(projectRoot) };
        proxy.InstallAudioSystems(world);
        // sfxId 300: real manifest MaxVoices=2 - room for both the frame's own successful voice AND
        // the "replays next frame" voice below, so MaxVoices never masks what the anti-duplicate table
        // alone is responsible for.
        RegisterClipsFor(proxy, provider, sfxId: 300);
        WireMapEventSoundTrigger(proxy, sfxId: 300);
        WireRescanSoundTrigger(proxy, sfxId: 300);

        proxy.Update(0.02f); // exactly one tick: RunMapEventsPass, then RunPendingEventTriggers.

        Assert.Equal(1, backend.ActiveVoiceCount); // the rescan's own duplicate request was refused.

        // "Replays at the next rendered frame": Update's own frame-close flush already ran (right after
        // both passes, same call above) - the SAME id must be playable again right now, through the
        // SAME real SoundPlayer this Update call used. The mutation "flush never called" fails this
        // assertion instead (the table would still hold id 300 from the frame above).
        proxy.SoundPlayer!.PlaySfx(300);
        Assert.Equal(2, backend.ActiveVoiceCount);
    }

    // Note: the "production site" acceptance (two requests for the same id within one rendered frame,
    // driven through the REAL Update path, producing exactly one backend voice) is already the exact
    // shape of Update_TwoTicksInOneRenderedFrame_SameId_ProducesOnlyOneBackendVoice above (the mutation
    // "the proxy's own FlushFrameSounds call site removed" fails IT too - both ticks' requests would
    // keep succeeding forever, never just once per frame) - not duplicated as a separate test.
}
