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
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

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
        AlundraGameState.Instance.ResetForTests();
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
        AlundraGameState.Instance.ResetForTests();
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

    /// <summary>Mirrors the frame-close site (P3, AlundraWorldProxy.Update, right before
    /// SoundPlayer.FlushFrameSounds): consumes MusicPlayer's own ResetSoundFlag, if set, by calling
    /// StopAllSound then clearing it - for tests that drive the singletons directly instead of a real
    /// AlundraWorldProxy.Update tick.</summary>
    private static void SimulateFrameClose()
    {
        if (AlundraMusicPlayer.Instance.ResetSoundFlag)
        {
            AlundraBgmFadeDirector.Instance.StopAllSound();
            AlundraMusicPlayer.Instance.ClearResetSoundFlag();
        }
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
        provider.Register(assetId, new FakeAudioClip("bgm_025", 44100));

        var game = BuildGameWithAudio(backend, provider);
        var world = new World { Name = WorldName };
        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = projectRoot; // InstallAudioSystems attaches the music player off this
        try
        {
            var proxy = new AlundraWorldProxy();
            proxy.InstallAudioSystems(world);

            Assert.Empty(backend.PlayCalls); // (a): loading never plays.

            proxy.Update(0.02f); // one logic tick - the frame-close site (P3) consumes ResetSoundFlag.

            Assert.Single(backend.PlayCalls);
            Assert.True(backend.PlayCalls[0].Parameters.IsLooped);

            var service = game.AudioSystemComponent.Service;
            var voice = ((AlundraMusicPlayer)proxy.MusicPlayer!).CurrentVoiceForTests;
            Assert.Equal(AudioBusNames.Music, service.GetVoiceBus(voice));
            Assert.Equal(AudioVoiceParameters.MaxVolume * service.Mixer.GetEffectiveGain(AudioBusNames.Music), backend.PlayCalls[0].Parameters.Volume);

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

            Assert.Single(backend.PlayCalls); // still one - the guard survived across proxies
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
        provider.Register(MakeAssetId(25), new FakeAudioClip("bgm_025_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, null);

        AlundraMusicPlayer.Instance.PlayMapMusic(389);
        Assert.Empty(backend.PlayCalls); // B7: loading never plays.
        Assert.True(AlundraMusicPlayer.Instance.ResetSoundFlag);

        SimulateFrameClose();
        Assert.Single(backend.PlayCalls);
        Assert.False(AlundraMusicPlayer.Instance.ResetSoundFlag);
        var voiceAfter389 = AlundraMusicPlayer.Instance.CurrentVoiceForTests;

        AlundraMusicPlayer.Instance.PlayMapMusic(390); // same raw index 25 - the guard (B10)
        Assert.False(AlundraMusicPlayer.Instance.ResetSoundFlag); // total no-op: never armed
        SimulateFrameClose(); // no-op: nothing to consume

        Assert.Single(backend.PlayCalls); // no second request
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
        provider.Register(MakeAssetId(25), new FakeAudioClip("bgm_025", 44100));
        provider.Register(MakeAssetId(1), new FakeAudioClip("bgm_001", 44100));
        provider.Register(MakeAssetId(16), new FakeAudioClip("bgm_016", 44100));
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
            Assert.Single(backend.PlayCalls); // track 25 playing.

            // 183: raw -1 - loads track 1, but CurrentMapSoundIndex stays -1 (B7), so StopAllSound's own
            // B2 guard (P2/D6) makes the frame close a TOTAL no-op: silence, the old voice stopped above.
            var world183 = new World { Name = "Inoa night-183" };
            HeroWorldFixture.SetProperty(world183, nameof(World.Game), game);
            var proxy183 = new AlundraWorldProxy();
            proxy183.InstallAudioSystems(world183);
            Assert.False(((AlundraMusicPlayer)proxy183.MusicPlayer!).IsCurrentVoiceAlive); // stopped at load.
            proxy183.Update(0.02f);
            Assert.Single(backend.PlayCalls); // still just the one - no new Play call, ever.
            Assert.False(((AlundraMusicPlayer)proxy183.MusicPlayer!).IsCurrentVoiceAlive);

            // 184: also raw -1 - same raw index as 183 (B10's own "already current" guard), total no-op.
            var world184 = new World { Name = "Inoa night later-184" };
            HeroWorldFixture.SetProperty(world184, nameof(World.Game), game);
            var proxy184 = new AlundraWorldProxy();
            proxy184.InstallAudioSystems(world184);
            proxy184.Update(0.02f);
            Assert.Single(backend.PlayCalls);
            Assert.False(((AlundraMusicPlayer)proxy184.MusicPlayer!).IsCurrentVoiceAlive);

            // 226: a real index (16) - plays.
            var world226 = new World { Name = "Sable-226" };
            HeroWorldFixture.SetProperty(world226, nameof(World.Game), game);
            var proxy226 = new AlundraWorldProxy();
            proxy226.InstallAudioSystems(world226);
            proxy226.Update(0.02f);
            Assert.Equal(2, backend.PlayCalls.Count);
            Assert.Equal("bgm_016", ((FakeAudioClip)backend.PlayCalls[1].Clip).Name);
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
        provider.Register(MakeAssetId(25), new FakeAudioClip("bgm_025_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, null);
        AlundraMusicPlayer.Instance.PlayMapMusic(389);
        SimulateFrameClose();

        Assert.Single(backend.PlayCalls);
        Assert.True(backend.PlayCalls[0].Parameters.IsLooped);
        Assert.Equal("bgm_025_fixture", ((FakeAudioClip)backend.PlayCalls[0].Clip).Name);
        Assert.Equal(AudioBusNames.Music, service.GetVoiceBus(AlundraMusicPlayer.Instance.CurrentVoiceForTests));
        Assert.Equal(
            AudioVoiceParameters.MaxVolume * service.Mixer.GetEffectiveGain(AudioBusNames.Music),
            backend.PlayCalls[0].Parameters.Volume);
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
        provider.Register(MakeAssetId(25), new FakeAudioClip("bgm_025_fixture", 44100));
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
        Assert.Equal(AudioBusNames.Music, service.GetVoiceBus(AlundraMusicPlayer.Instance.CurrentVoiceForTests));
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
        provider.Register(MakeAssetId(25), new FakeAudioClip("bgm_025", 44100));
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

            Assert.Empty(backend.PlayCalls);
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
        provider.Register(MakeAssetId(25), new FakeAudioClip("bgm_025", 44100));
        provider.Register(MakeAssetId(5), new FakeAudioClip("bgm_005", 44100));
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
            Assert.Empty(backend.PlayCalls);

            proxy.BgmFadeDirector.StopAllSound(); // simulated 0xA5.

            Assert.Single(backend.PlayCalls);
            Assert.Equal("bgm_005", ((FakeAudioClip)backend.PlayCalls[0].Clip).Name);
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
        provider.Register(MakeAssetId(16), new FakeAudioClip("bgm_016", 44100));
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
            Assert.Single(backend.PlayCalls);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }
}
