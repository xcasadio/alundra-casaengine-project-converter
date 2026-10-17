using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Audio;
using Alundra.Transitions;

namespace Alundra.Tests;

/// <summary>
/// xunit collection covering every test class that touches the SESSION-scoped
/// <see cref="AlundraMusicPlayer.Instance"/> singleton (D-C-6, docs/plan-e11c-musique.md, slice C1) -
/// today <see cref="AlundraMusicPlayerTests"/> and <see cref="AlundraWorldProxyAudioInstallationTests"/>
/// (whose <c>InstallAudioSystems</c> call now re-points that same instance). Classes sharing a
/// collection never run in parallel with each other, which is what keeps them from racing on that
/// shared mutable state (xunit runs different test CLASSES in this project in parallel by default).
/// </summary>
[CollectionDefinition(Name)]
public class AlundraMusicPlayerSingletonCollection
{
    public const string Name = "AlundraMusicPlayer singleton";
}

/// <summary>
/// T1/T1 bis/T2/T4/T5 of docs/plan-e11c-musique.md, slice C1. All in ONE class (rather than split like
/// E11.a's own tests) because every one of them drives <see cref="AlundraMusicPlayer.Instance"/> - a
/// SESSION-scoped singleton by design (D-C-6) - and xunit runs different test CLASSES in this project in
/// parallel by default (methods within one class do not run concurrently), so sharing that singleton
/// across classes would race; see <see cref="AlundraMusicPlayerSingletonCollection"/> for the other
/// class it shares this concern with. Every test calls <see cref="AlundraMusicPlayer.ResetForTests"/>
/// first (the "moyen de le réinitialiser en test" D-C-6 asks the slice to name).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public class AlundraMusicPlayerTests : IDisposable
{
    private const string WorldName = "Ship Klark (beginning)-389";
    private readonly List<string> _tempDirs = new();

    public AlundraMusicPlayerTests()
    {
        AlundraMusicPlayer.Instance.ResetForTests();
        AlundraBgmFadeDirector.Instance.ResetForTests(); // T2.1: the frame-close site drives this too now.

        // D-T-14 (docs/plan-transitions-carte.md, slice T1): this class constructs an AlundraWorldProxy,
        // so it shares the three session carriers T1 introduces - reset them here (constructor, the
        // isolation-carrying element) so no earlier test's state leaks in.
        GameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.
    }

    public void Dispose()
    {
        AlundraMusicPlayer.Instance.ResetForTests();
        AlundraBgmFadeDirector.Instance.ResetForTests();

        // D-T-14: hygiene, not covered by the acceptance (the constructor above is what carries
        // isolation) - kept for symmetry with the existing session-singleton test classes.
        GameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.

        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    /// <summary>Drives the frame-close site's OWN production code (T4.2,
    /// <see cref="AlundraWorldProxy.ConsumeMusicResetSoundFlagOnFrameClose"/> - the exact block
    /// <see cref="AlundraWorldProxy.Update"/> runs right before <c>SoundPlayer.FlushFrameSounds</c>, P3)
    /// against the session singletons directly, for tests that drive them without a real
    /// AlundraWorldProxy.Update tick.</summary>
    private static void SimulateFrameClose()
    {
        AlundraWorldProxy.ConsumeMusicResetSoundFlagOnFrameClose(AlundraMusicPlayer.Instance, AlundraBgmFadeDirector.Instance);
    }

    // ---- fixtures -------------------------------------------------------------------------------

    /// <summary>Builds a temp project with Maps/music-index.json (the given raw entries) and
    /// Musics/bgm-manifest.json (one entry per DISTINCT resolved index, asset id deterministic from the
    /// index) - and, when <paramref name="withSfx"/>, a Sounds/sfx-manifest.json with a single id-300
    /// record (T5's own sfx half), all under the SAME root so one AttachToWorld/AlundraSoundBank call
    /// covers both.</summary>
    private string BuildFixtureProject(IReadOnlyDictionary<int, int> rawByMapId, IReadOnlySet<int> playableIndices, bool withSfx = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "AlundraMusicPlayerTests_" + Guid.NewGuid());
        _tempDirs.Add(root);

        var mapsDir = Path.Combine(root, "Maps");
        Directory.CreateDirectory(mapsDir);
        var indexNode = new Dictionary<string, int>();
        foreach (var (mapId, raw) in rawByMapId)
        {
            indexNode[mapId.ToString()] = raw;
        }
        File.WriteAllText(Path.Combine(mapsDir, "music-index.json"), JsonSerializer.Serialize(indexNode));

        var musicsDir = Path.Combine(root, "Musics");
        Directory.CreateDirectory(musicsDir);
        var manifestEntries = new List<object>();
        foreach (var index in playableIndices)
        {
            manifestEntries.Add(new { sound_index = index, asset_id = MakeAssetId(index).ToString() });
        }
        File.WriteAllText(Path.Combine(musicsDir, "bgm-manifest.json"), JsonSerializer.Serialize(manifestEntries));

        if (withSfx)
        {
            var soundsDir = Path.Combine(root, "Sounds");
            Directory.CreateDirectory(soundsDir);
            var sfxJson = $$"""
            [
              { "id": 300, "vab_id": 56, "program_number": 0, "tone_number": 0, "note": 60, "seq_num": -1,
                "ref_sfx_id": 0, "max_voices": 2, "num_tones": 1, "skip_reason": null,
                "tones": [ { "tone_index": 0, "file": "sfx_0300.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 0, "repeat": false, "asset_id": "{{SfxAssetId}}" } ] }
            ]
            """;
            File.WriteAllText(Path.Combine(soundsDir, "sfx-manifest.json"), sfxJson);
        }

        return root;
    }

    private static readonly Guid SfxAssetId = Guid.Parse("00000000-0000-0000-0000-000000000300");

    private static Guid MakeAssetId(int soundIndex) => Guid.Parse($"00000000-0000-0000-0000-{soundIndex:D12}");

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Musics")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraMusicPlayerTests: no 'alundra-project/Musics' directory found above "
            + $"'{AppContext.BaseDirectory}' - T1 needs the real converter export (docs/plan-e11c-musique.md, slice C1).");
    }

    private static CasaEngineGame BuildGameWithAudio(FakeAudioBackend backend, FakeAudioClipProvider provider)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));

        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

        var audioComponent = (AudioSystemComponent)RuntimeHelpers.GetUninitializedObject(typeof(AudioSystemComponent));
        var serviceField = typeof(AudioSystemComponent).GetField("<Service>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        serviceField.SetValue(audioComponent, new AudioService(backend) { ClipProvider = provider });

        var audioComponentField = typeof(CasaEngineGame).GetField("<AudioSystemComponent>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        audioComponentField.SetValue(game, audioComponent);

        return game;
    }

    // ---- T1: at the production site, on the REAL export -----------------------------------------

    [Fact]
    public void T1_Intro_RealMusicPlayer_LoadsWithoutPlaying_ThenFirstFrameCloseStartsIndex25_Looped_FullVolume_MusicBus()
    {
        // Acceptance items (a)/(b) of docs/plan-bgm-demarrage-binaire.md, T2.1: loading a map's own BGM
        // never plays it (B6/B7) - the voice only starts at the entry's first frame-close (P3), through
        // AlundraWorldProxy.Update's own real path (not a simulated frame close, unlike T1 bis below).
        var projectRoot = FindProjectRoot();
        var manifestJson = File.ReadAllText(Path.Combine(projectRoot, "Musics", "bgm-manifest.json"));
        using var manifestDoc = JsonDocument.Parse(manifestJson);
        var assetIdText = manifestDoc.RootElement.EnumerateArray()
            .First(e => e.GetProperty("sound_index").GetInt32() == 25)
            .GetProperty("asset_id").GetString()!;
        var assetId = Guid.Parse(assetIdText);

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.RegisterStream(assetId, "bgm_025");

        var game = BuildGameWithAudio(backend, provider);
        var world = new World { Name = WorldName };
        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectRoot; // InstallAudioSystems attaches the music player off this
        try
        {
            var proxy = new AlundraWorldProxy();
            proxy.InstallAudioSystems(world);

            Assert.Empty(backend.StreamVoices); // (a): loading never plays.

            proxy.Update(0.02f); // one logic tick - the frame-close site (P3) consumes ResetSoundFlag.

            Assert.Single(backend.StreamVoices);
            Assert.True(backend.StreamVoices[0].Parameters.IsLooped);

            var service = game.AudioSystemComponent.Service;
            var voice = ((AlundraMusicPlayer)proxy.MusicPlayer!).CurrentVoiceForTests;
            Assert.Equal(AudioBusNames.Music, service.GetVoiceBus(backend.StreamVoices[0].Voice));
            Assert.Equal(AudioVoiceParameters.MaxVolume * service.Mixer.GetEffectiveGain(AudioBusNames.Music), backend.StreamVoices[0].Parameters.Volume);

            // D-C-6's own guarantee, exercised through the REAL per-world wiring (not the singleton
            // directly, unlike T1 bis): a SECOND, independently constructed AlundraWorldProxy - exactly
            // how a real map change rebuilds it - installing world "...-390" (same raw index 25, fact
            // 1.1) must NOT produce a second request, and the original voice must still be the one
            // playing. This is what would break if AlundraMusicPlayer.Instance were a per-world
            // instance instead of the session singleton D-C-6 requires.
            var world390 = new World { Name = "Some Other Map (beginning)-390" };
            HeroWorldFixture.SetProperty(world390, nameof(World.Game), game);
            var proxy390 = new AlundraWorldProxy();
            proxy390.InstallAudioSystems(world390);
            proxy390.Update(0.02f); // (b): 390's own frame close, same raw index 25 - still a total no-op.

            Assert.Single(backend.StreamVoices); // still one - the guard survived across proxies
            Assert.Equal(voice, ((AlundraMusicPlayer)proxy390.MusicPlayer!).CurrentVoiceForTests);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    // ---- T1 bis: the guard, driven on the DIRECTOR itself ----------------------------------------

    [Fact]
    public void T1bis_Director_SameRawIndex389And390_LoadOnly_ThenFrameCloseStartsOneVoice_390IsATotalNoOp()
    {
        var projectPath = BuildFixtureProject(
            new Dictionary<int, int> { [389] = 25, [390] = 25 },
            new HashSet<int> { 25 });

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.RegisterStream(MakeAssetId(25), "bgm_025_fixture");
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, null);

        AlundraMusicPlayer.Instance.PlayMapMusic(389);
        Assert.Empty(backend.StreamVoices); // B7: loading never plays.
        Assert.True(AlundraMusicPlayer.Instance.ResetSoundFlag);

        SimulateFrameClose();
        Assert.Single(backend.StreamVoices);
        Assert.False(AlundraMusicPlayer.Instance.ResetSoundFlag);
        var voiceAfter389 = AlundraMusicPlayer.Instance.CurrentVoiceForTests;

        AlundraMusicPlayer.Instance.PlayMapMusic(390); // same raw index 25 - the guard (B10)
        Assert.False(AlundraMusicPlayer.Instance.ResetSoundFlag); // total no-op: never armed
        SimulateFrameClose(); // no-op: nothing to consume

        Assert.Single(backend.StreamVoices); // no second request
        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive); // and the voice is still alive
        Assert.Equal(voiceAfter389, AlundraMusicPlayer.Instance.CurrentVoiceForTests); // not restarted
    }

    // ---- Acceptance item (c): negative-index maps are silent, a real index plays afterwards -------

    [Fact]
    public void MapEntry_NegativeIndexMaps_AreSilent_ThenARealIndexPlays()
    {
        var projectPath = BuildFixtureProject(
            new Dictionary<int, int> { [389] = 25, [183] = -1, [184] = -1, [226] = 16 },
            new HashSet<int> { 25, 1, 16 });

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.RegisterStream(MakeAssetId(25), "bgm_025");
        provider.RegisterStream(MakeAssetId(1), "bgm_001");
        provider.RegisterStream(MakeAssetId(16), "bgm_016");
        var game = BuildGameWithAudio(backend, provider);

        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectPath;
        try
        {
            var world389 = new World { Name = "Ship-389" };
            HeroWorldFixture.SetProperty(world389, nameof(World.Game), game);
            var proxy389 = new AlundraWorldProxy();
            proxy389.InstallAudioSystems(world389);
            proxy389.Update(0.02f);
            Assert.Single(backend.StreamVoices); // track 25 playing.

            // 183: raw -1 - loads track 1, but CurrentMapSoundIndex stays -1 (B7), so StopAllSound's own
            // B2 guard (P2/D6) makes the frame close a TOTAL no-op: silence, the old voice stopped above.
            var world183 = new World { Name = "Inoa night-183" };
            HeroWorldFixture.SetProperty(world183, nameof(World.Game), game);
            var proxy183 = new AlundraWorldProxy();
            proxy183.InstallAudioSystems(world183);
            Assert.False(((AlundraMusicPlayer)proxy183.MusicPlayer!).IsCurrentVoiceAlive); // stopped at load.
            proxy183.Update(0.02f);
            Assert.Single(backend.StreamVoices); // still just the one - no new Play call, ever.
            Assert.False(((AlundraMusicPlayer)proxy183.MusicPlayer!).IsCurrentVoiceAlive);

            // 184: also raw -1 - same raw index as 183 (B10's own "already current" guard), total no-op.
            var world184 = new World { Name = "Inoa night later-184" };
            HeroWorldFixture.SetProperty(world184, nameof(World.Game), game);
            var proxy184 = new AlundraWorldProxy();
            proxy184.InstallAudioSystems(world184);
            proxy184.Update(0.02f);
            Assert.Single(backend.StreamVoices);
            Assert.False(((AlundraMusicPlayer)proxy184.MusicPlayer!).IsCurrentVoiceAlive);

            // 226: a real index (16) - plays.
            var world226 = new World { Name = "Sable-226" };
            HeroWorldFixture.SetProperty(world226, nameof(World.Game), game);
            var proxy226 = new AlundraWorldProxy();
            proxy226.InstallAudioSystems(world226);
            proxy226.Update(0.02f);
            Assert.Equal(2, backend.StreamVoices.Count);
            Assert.Equal("bgm_016", provider.OpenedStreams[1]);
            Assert.True(((AlundraMusicPlayer)proxy226.MusicPlayer!).IsCurrentVoiceAlive);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    // ---- T2: no audio service --------------------------------------------------------------------

    [Fact]
    public void T2_NoGame_TriggerMapEntryMusic_NoRequestAndNoException()
    {
        var world = new World { Name = WorldName }; // Game left null - no AudioSystemComponent.
        var proxy = new AlundraWorldProxy();

        var ex = Record.Exception(() =>
        {
            proxy.InstallAudioSystems(world);
        });

        Assert.Null(ex);
        Assert.Null(proxy.MusicPlayer);
    }

    // ---- T4: the real player, on fakes -------------------------------------------------------------

    [Fact]
    public void T4_RealMusicPlayer_OnFakes_OneClip_Looped_MusicBus_VolumeAtBusGain()
    {
        var projectPath = BuildFixtureProject(
            new Dictionary<int, int> { [389] = 25 },
            new HashSet<int> { 25 });

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.RegisterStream(MakeAssetId(25), "bgm_025_fixture");
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, null);
        AlundraMusicPlayer.Instance.PlayMapMusic(389);
        SimulateFrameClose();

        Assert.Single(backend.StreamVoices);
        Assert.True(backend.StreamVoices[0].Parameters.IsLooped);
        Assert.Equal("bgm_025_fixture", provider.OpenedStreams[0]);
        Assert.Equal(AudioBusNames.Music, service.GetVoiceBus(backend.StreamVoices[0].Voice));
        Assert.Equal(
            AudioVoiceParameters.MaxVolume * service.Mixer.GetEffectiveGain(AudioBusNames.Music),
            backend.StreamVoices[0].Parameters.Volume);
    }

    // ---- T5: ownership (D-C-5) ----------------------------------------------------------------------

    [Fact]
    public void T5_StopVoicesOwnedByWorld_KillsTheSfxVoice_ButTheMusicVoiceSurvives()
    {
        var projectPath = BuildFixtureProject(
            new Dictionary<int, int> { [389] = 25 },
            new HashSet<int> { 25 },
            withSfx: true);

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(SfxAssetId, new FakeAudioClip("sfx_0300", 11025));
        provider.RegisterStream(MakeAssetId(25), "bgm_025_fixture");
        var service = new AudioService(backend) { ClipProvider = provider };

        var world = new object(); // stands in for the owning World

        // D-C-5: sfx owns the world.
        var soundPlayer = new AlundraSoundPlayer(service, new AlundraSoundBank(projectPath), world);
        soundPlayer.PlaySfx(300);

        // D-C-5/D-C-6: music owns the session (AlundraMusicPlayer.Instance itself), never the world.
        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, null);
        AlundraMusicPlayer.Instance.PlayMapMusic(389);
        SimulateFrameClose();

        Assert.Equal(2, service.ActiveVoiceCount); // one sfx voice + one music voice

        service.StopVoicesOwnedBy(world);

        Assert.Equal(1, service.ActiveVoiceCount); // only the music voice remains
        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
        Assert.Equal(AudioBusNames.Music, service.GetVoiceBus(backend.StreamVoices[0].Voice));
    }

    // ---- Acceptance items (j)/(l)/(m): frame-close interactions with a same-frame opcode ------------

    [Fact]
    public void MapEntry_ThenLoadBgmArmedInTheSameFrame_FrameCloseStaysSilent_FadeArmed()
    {
        // Acceptance item (j): 0xA6 1's own unconditional ResetSoundFlag clear (B14) means the
        // frame-close site has nothing left to consume when it runs - no voice starts, only the fade
        // machine is left armed.
        var projectPath = BuildFixtureProject(new Dictionary<int, int> { [389] = 25 }, new HashSet<int> { 25 });
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.RegisterStream(MakeAssetId(25), "bgm_025");
        var game = BuildGameWithAudio(backend, provider);
        var world = new World { Name = WorldName };
        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectPath;
        try
        {
            var proxy = new AlundraWorldProxy();
            proxy.InstallAudioSystems(world); // loads 25, arms ResetSoundFlag.
            Assert.True(((AlundraMusicPlayer)proxy.MusicPlayer!).ResetSoundFlag);

            proxy.BgmFadeDirector.LoadBgm(1); // simulated 0xA6 1, same frame, BEFORE the frame closes.
            Assert.False(((AlundraMusicPlayer)proxy.MusicPlayer!).ResetSoundFlag); // B14: cleared.

            proxy.Update(0.02f); // frame close: nothing to consume.

            Assert.Empty(backend.StreamVoices);
            Assert.True(proxy.BgmFadeDirector.IsArmed);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    [Fact]
    public void MapEntry_ThenRawIndexLoadInTheSameFrame_FrameCloseStaysSilent_ThenStopAllSoundStartsIt()
    {
        // Acceptance item (m): 0xA7 n>0's own ClearResetSoundFlag (P8) leaves the frame-close site
        // nothing to consume; the loaded track only starts on a LATER, explicit StopAllSound (0xA5).
        var projectPath = BuildFixtureProject(
            new Dictionary<int, int> { [389] = 25 },
            new HashSet<int> { 25, 5 });

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.RegisterStream(MakeAssetId(25), "bgm_025");
        provider.RegisterStream(MakeAssetId(5), "bgm_005");
        var game = BuildGameWithAudio(backend, provider);
        var world = new World { Name = WorldName };
        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectPath;
        try
        {
            var proxy = new AlundraWorldProxy();
            proxy.InstallAudioSystems(world); // loads 25, arms ResetSoundFlag.

            ((AlundraMusicPlayer)proxy.MusicPlayer!).PlayFromRawIndex(5); // simulated 0xA7 5,0, same frame.
            Assert.False(((AlundraMusicPlayer)proxy.MusicPlayer!).ResetSoundFlag); // P8: cleared.

            proxy.Update(0.02f); // frame close: nothing to consume.
            Assert.Empty(backend.StreamVoices);

            proxy.BgmFadeDirector.StopAllSound(); // simulated 0xA5.

            Assert.Single(backend.StreamVoices);
            Assert.Equal("bgm_005", provider.OpenedStreams[0]);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    [Fact]
    public void MapEntry_DifferentIndex_WhileFadeAlreadyArmed_FrameCloseDisarmsRestoresMasterAndStartsVoice()
    {
        // Acceptance item (l): B2's own "unconditional past the index guard" - a map entry's own
        // frame-close StopAllSound call disarms an in-flight fade and restores the master, exactly like
        // any other StopAllSound call, then starts the newly-loaded track.
        var projectPath = BuildFixtureProject(new Dictionary<int, int> { [226] = 16 }, new HashSet<int> { 16 });
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.RegisterStream(MakeAssetId(16), "bgm_016");
        var game = BuildGameWithAudio(backend, provider);
        var world = new World { Name = "Sable-226" };
        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectPath;
        try
        {
            var service = game.AudioSystemComponent.Service;
            service.Mixer.GetBus(AudioBusNames.Master).Volume = 0.4f; // simulates a fade in progress.
            AlundraBgmFadeDirector.Instance.LoadBgm(1); // armed by an earlier frame's own 0xA6 1.
            Assert.True(AlundraBgmFadeDirector.Instance.IsArmed);

            var proxy = new AlundraWorldProxy();
            proxy.InstallAudioSystems(world); // loads 16 (differs from CurrentMapSoundIndex 0), arms flag.

            proxy.Update(0.02f); // frame close.

            Assert.False(proxy.BgmFadeDirector.IsArmed); // disarmed.
            Assert.Equal(1f, service.Mixer.GetBus(AudioBusNames.Master).Volume, 5); // restored.
            Assert.Single(backend.StreamVoices);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    // ---- E19.L-b3 (docs/plan-e19-opcodes.md, LB3-0): the game plays its music as a stream with the loop point -----

    /// <summary>The started asset of the session player (null when nothing plays).</summary>
    internal static SoundAsset? CurrentAssetOf(AlundraMusicPlayer player) => player.CurrentAssetForTests;

    private string BuildFixtureProjectWithManifest(IReadOnlyDictionary<int, int> rawByMapId, string bgmManifestJson)
    {
        var root = BuildFixtureProject(rawByMapId, new HashSet<int>());
        File.WriteAllText(Path.Combine(root, "Musics", "bgm-manifest.json"), bgmManifestJson);
        return root;
    }

    private static string ManifestEntry(int soundIndex, string loopFields)
        => $$"""{"sound_index": {{soundIndex}}, "asset_id": "{{MakeAssetId(soundIndex)}}"{{loopFields}}}""";

    [Fact]
    public void D1_ALoopingTrack_IsAStreamLoopedAtItsLoopPoint_OnTheMusicBus_AtFullVolume_NeverAWholeClip()
    {
        var projectPath = BuildFixtureProjectWithManifest(
            new Dictionary<int, int> { [389] = 25 },
            "[" + ManifestEntry(25, """, "looping": true, "loop_start_frame": 7272, "loop_start_sample": 5344920""") + "]");

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.RegisterStream(MakeAssetId(25), "bgm_025");
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, null);
        AlundraMusicPlayer.Instance.PlayMapMusic(389);
        SimulateFrameClose();

        Assert.Empty(backend.PlayCalls); // no whole clip any more.
        var stream = Assert.Single(backend.StreamVoices);
        Assert.True(stream.Parameters.IsLooped);
        Assert.Equal(AudioBusNames.Music, service.GetVoiceBus(stream.Voice));
        Assert.Equal(AudioVoiceParameters.MaxVolume * service.Mixer.GetEffectiveGain(AudioBusNames.Music), stream.Parameters.Volume);
        Assert.Equal(new[] { "bgm_025" }, provider.OpenedStreams);

        var asset = CurrentAssetOf(AlundraMusicPlayer.Instance);
        Assert.NotNull(asset);
        Assert.True(asset!.IsStreaming);
        Assert.True(asset.IsLooped);
        Assert.Equal(5344920L, asset.LoopStartSample);
        Assert.Equal(AudioBusNames.Music, asset.BusName);
        Assert.Equal(AudioVoiceParameters.MaxVolume, asset.Volume);
    }

    [Fact]
    public void D3_AManifestWithoutTheLoopFields_LoopsTheWholeFileFromSampleZero_WithOneWarningPerAttach()
    {
        var projectPath = BuildFixtureProjectWithManifest(
            new Dictionary<int, int> { [389] = 25 },
            "[" + ManifestEntry(25, "") + ", " + ManifestEntry(16, "") + "]");

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.RegisterStream(MakeAssetId(25), "bgm_025");
        var service = new AudioService(backend) { ClipProvider = provider };

        using var capture = new WarningCapture();
        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);

        // Two tracks lack the fields, one warning: per attach, not per track, and it names the manifest.
        var warnings = capture.Warnings.Where(w => w.Contains("bgm-manifest.json", StringComparison.Ordinal)).ToList();
        Assert.True(warnings.Count == 1, $"{warnings.Count} warnings: {string.Join(" | ", warnings)}");
        Assert.Contains("looping", warnings[0], StringComparison.Ordinal);

        AlundraBgmFadeDirector.Instance.AttachToWorld(service, null);
        AlundraMusicPlayer.Instance.PlayMapMusic(389);
        SimulateFrameClose();

        var stream = Assert.Single(backend.StreamVoices);
        Assert.True(stream.Parameters.IsLooped);
        var asset = CurrentAssetOf(AlundraMusicPlayer.Instance);
        Assert.NotNull(asset);
        Assert.True(asset!.IsStreaming);
        Assert.True(asset.IsLooped);
        Assert.Equal(0L, asset.LoopStartSample);

        // A second attach (the next world) warns again, once.
        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        var afterSecond = capture.Warnings.Count(w => w.Contains("bgm-manifest.json", StringComparison.Ordinal));
        Assert.Equal(2, afterSecond);
    }

    [Fact]
    public void D4_TheRealExport_GivesTrack25ItsLoopPoint()
    {
        var projectRoot = FindProjectRoot(); // throws when the export is missing: this case is never green in silence.
        var manifestPath = Path.Combine(projectRoot, "Musics", "bgm-manifest.json");
        Assert.True(File.Exists(manifestPath), $"'{manifestPath}' is missing: export the project (L-b2) first.");

        using var manifestDoc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var entry25 = manifestDoc.RootElement.EnumerateArray().First(e => e.GetProperty("sound_index").GetInt32() == 25);
        var assetId = Guid.Parse(entry25.GetProperty("asset_id").GetString()!);
        Assert.True(entry25.GetProperty("looping").GetBoolean());
        Assert.Equal(5344920L, entry25.GetProperty("loop_start_sample").GetInt64());

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.RegisterStream(assetId, "bgm_025");
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectRoot);
        AlundraMusicPlayer.Instance.PlayFromRawIndex(25);
        AlundraMusicPlayer.Instance.PlaySequence();

        Assert.Single(backend.StreamVoices);
        var asset = CurrentAssetOf(AlundraMusicPlayer.Instance);
        Assert.NotNull(asset);
        Assert.Equal(assetId, asset!.AudioFileAssetId);
        Assert.True(asset.IsStreaming);
        Assert.True(asset.IsLooped);
        Assert.Equal(5344920L, asset.LoopStartSample);
    }

    [Fact]
    public void D5_B4_ALiveTrackIsNotRestarted_ByASecondPlaySequence()
    {
        var projectPath = BuildFixtureProjectWithManifest(
            new Dictionary<int, int>(),
            "[" + ManifestEntry(25, """, "looping": true, "loop_start_frame": 7272, "loop_start_sample": 5344920""") + "]");

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.RegisterStream(MakeAssetId(25), "bgm_025");
        var service = new AudioService(backend) { ClipProvider = provider };

        // The recorder of the starts reads both routes, a stream or a whole clip: a start counts whichever way the voice is made.
        int Starts() => backend.PlayCalls.Count + backend.StreamVoices.Count;

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraMusicPlayer.Instance.PlayFromRawIndex(25);
        Assert.Equal(0, Starts()); // loading never plays.

        AlundraMusicPlayer.Instance.PlaySequence();
        Assert.Equal(1, Starts()); // the recorder sees the start, so the next lines cannot be green by emptiness.
        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
        var track = AlundraMusicPlayer.Instance.CurrentVoiceForTests;

        AlundraMusicPlayer.Instance.PlaySequence(); // B4: a live voice is left alone.

        Assert.Equal(1, Starts());
        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
        Assert.Equal(track, AlundraMusicPlayer.Instance.CurrentVoiceForTests);
    }

    [Fact]
    public void D6_AOneShotWhoseStreamEnds_StaysAliveUntilTheNextMusicUpdate_ThenIsGone()
    {
        var projectPath = BuildFixtureProjectWithManifest(
            new Dictionary<int, int>(),
            "[" + ManifestEntry(42, """, "looping": false, "loop_start_frame": null, "loop_start_sample": null""") + "]");

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.RegisterStream(MakeAssetId(42), "bgm_042");
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraMusicPlayer.Instance.PlayFromRawIndex(42);
        AlundraMusicPlayer.Instance.PlaySequence();

        var stream = Assert.Single(backend.StreamVoices);
        Assert.False(stream.Parameters.IsLooped); // a one-shot: it ends.
        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);

        backend.DrainStream(stream.Voice); // the device played the last buffer: the stream is over.

        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive); // nothing has looked at the end yet.

        service.Update(0.016f); // the next Music.Update drops the finished track.

        Assert.False(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
    }

    private sealed class WarningCapture : ILogger, IDisposable
    {
        private readonly object _gate = new();
        private readonly List<string> _warnings = new();

        public WarningCapture()
        {
            Logs.AddLogger(this);
        }

        public List<string> Warnings
        {
            get
            {
                lock (_gate)
                {
                    return _warnings.ToList();
                }
            }
        }

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteError(string msg) { }

        public void WriteWarning(string msg)
        {
            lock (_gate)
            {
                _warnings.Add(msg);
            }
        }

        public void Dispose()
        {
            var field = typeof(Logs).GetField("_loggers", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            ((List<ILogger>)field!.GetValue(null)!).Remove(this);
        }
    }
}
