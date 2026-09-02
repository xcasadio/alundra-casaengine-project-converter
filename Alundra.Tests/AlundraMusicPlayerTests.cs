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
    public void T1_Intro_RealMusicPlayer_ProducesExactlyOneRequest_Index25_Looped_FullVolume_MusicBus()
    {
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
    public void T1bis_Director_SameRawIndex389And390_OneRequestOnly_VoiceSurvivesNotRestarted()
    {
        var projectPath = BuildFixtureProject(
            new Dictionary<int, int> { [389] = 25, [390] = 25 },
            new HashSet<int> { 25 });

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(MakeAssetId(25), new FakeAudioClip("bgm_025_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);

        AlundraMusicPlayer.Instance.PlayMapMusic(389);
        Assert.Single(backend.PlayCalls);
        var voiceAfter389 = AlundraMusicPlayer.Instance.CurrentVoiceForTests;

        AlundraMusicPlayer.Instance.PlayMapMusic(390); // same raw index 25 - the guard

        Assert.Single(backend.PlayCalls); // no second request
        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive); // and the voice is still alive
        Assert.Equal(voiceAfter389, AlundraMusicPlayer.Instance.CurrentVoiceForTests); // not restarted
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
        AlundraMusicPlayer.Instance.PlayMapMusic(389);

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
        AlundraMusicPlayer.Instance.PlayMapMusic(389);

        Assert.Equal(2, service.ActiveVoiceCount); // one sfx voice + one music voice

        service.StopVoicesOwnedBy(world);

        Assert.Equal(1, service.ActiveVoiceCount); // only the music voice remains
        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
        Assert.Equal(AudioBusNames.Music, service.GetVoiceBus(AlundraMusicPlayer.Instance.CurrentVoiceForTests));
    }
}
