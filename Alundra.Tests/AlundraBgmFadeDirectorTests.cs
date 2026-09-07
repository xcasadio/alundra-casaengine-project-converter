#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// B2 of docs/plan-e11b-opcodes-audio.md: the master BGM fade machine (<see cref="AlundraBgmFadeDirector"/>,
/// opcodes 0xA5/0xA6/0xA7). Joins <see cref="AlundraMusicPlayerSingletonCollection"/> (rather than a new
/// collection) because every test here also touches <see cref="AlundraMusicPlayer.Instance"/> - the
/// fade machine's own <c>RestartIfActive</c> calls straight through to it (D-B-5) - so the two session
/// singletons must never run in parallel with each other either; classes sharing a collection never do
/// (xunit runs different test CLASSES in this project in parallel by default; see that collection's own
/// doc, and note the project's own <c>xunit.runner.json</c> already disables cross-collection
/// parallelism entirely, kept here purely for hygiene/documentation).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public class AlundraBgmFadeDirectorTests : IDisposable
{
    public AlundraBgmFadeDirectorTests()
    {
        AlundraBgmFadeDirector.Instance.ResetForTests();
        AlundraMusicPlayer.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraBgmFadeDirector.Instance.ResetForTests();
        AlundraMusicPlayer.Instance.ResetForTests();

        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // ---- fixtures -------------------------------------------------------------------------------

    /// <summary>Records every <see cref="StopAllSfx"/> call - the SFX half of the machine's own swap-tick
    /// key-off and of 0xA5's own conditional stop (fact 1/fact 2).</summary>
    private sealed class FakeSoundPlayerForFade : IAlundraSoundPlayer
    {
        public int StopAllSfxCallCount;
        public void PlaySfx(int sfxId) { }
        public void RemixVoice(int sfxId, int left, int right) { }
        public void FlushFrameSounds() { }
        public void StopAllSfx() => StopAllSfxCallCount++;
    }

    private static float MasterVolume(AudioService service)
        => service.Mixer.GetBus(AudioBusNames.Master).Volume;

    // ---- the 120-step ramp, hand-computed milestones (docs/plan-e11b-opcodes-audio.md's own B2
    // report/fact 2) ---------------------------------------------------------------------------------

    [Fact]
    public void Advance_FromArmed0x78_HandComputedMilestones_124_2_Swap_Restore_Rest()
    {
        var backend = new FakeAudioBackend();
        var service = new AudioService(backend);
        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);

        AlundraBgmFadeDirector.Instance.LoadBgm(5); // arms: state = 0x78 (120).
        Assert.True(AlundraBgmFadeDirector.Instance.IsArmed);

        // Tick 1 (pre-tick state 0x78=120): v = (120-0x3d)*0x7f/0x3c = 59*127/60 = 124 (truncating).
        AlundraBgmFadeDirector.Instance.Advance(1);
        Assert.Equal(124f / 127f, MasterVolume(service), 5);
        Assert.Equal(0, sfx.StopAllSfxCallCount); // no key-off yet - still ramping down.

        // 58 more ticks (total 59, pre-tick state now 0x3e=62): v = (62-0x3d)*0x7f/0x3c = 1*127/60 = 2.
        AlundraBgmFadeDirector.Instance.Advance(58);
        Assert.Equal(2f / 127f, MasterVolume(service), 5);
        Assert.Equal(0, sfx.StopAllSfxCallCount);
        Assert.True(AlundraBgmFadeDirector.Instance.IsArmed);

        // Tick 60 (pre-tick state 0x3d=61): the SWAP - master mutes to 0, all SFX key-off.
        AlundraBgmFadeDirector.Instance.Advance(1);
        Assert.Equal(0f, MasterVolume(service), 5);
        Assert.Equal(1, sfx.StopAllSfxCallCount);
        Assert.True(AlundraBgmFadeDirector.Instance.IsArmed); // still counting down silently.

        // 56 more silent ticks (total 116, pre-tick state now 5): no master call - volume unchanged.
        AlundraBgmFadeDirector.Instance.Advance(56);
        Assert.Equal(0f, MasterVolume(service), 5);
        Assert.Equal(1, sfx.StopAllSfxCallCount); // no second key-off during the silent descent.

        // Tick 117 (pre-tick state 4): the RESTORE - master back to full (0x7f).
        AlundraBgmFadeDirector.Instance.Advance(1);
        Assert.Equal(1f, MasterVolume(service), 5);

        // 3 more silent ticks (total 120): machine settles to rest.
        AlundraBgmFadeDirector.Instance.Advance(3);
        Assert.False(AlundraBgmFadeDirector.Instance.IsArmed);
        Assert.Equal(1f, MasterVolume(service), 5); // held at full - no further master call past rest.

        // Fully at rest: further ticks are a no-op (the original's own "state==0 -> return").
        AlundraBgmFadeDirector.Instance.Advance(5);
        Assert.False(AlundraBgmFadeDirector.Instance.IsArmed);
    }

    [Fact]
    public void Advance_FullyPastState0x3d_TheSwapTick_RestartsWhateverBgmIsCurrentlyResolved()
    {
        var projectPath = BuildFixtureProjectWithOneTrack(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraMusicPlayer.Instance.PlayFromRawIndex(7); // one voice playing - the "currently resolved" track.
        Assert.Single(backend.PlayCalls);
        var firstVoice = AlundraMusicPlayer.Instance.CurrentVoiceForTests;

        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);
        AlundraBgmFadeDirector.Instance.LoadBgm(9); // arm

        AlundraBgmFadeDirector.Instance.Advance(60); // reach the swap tick (pre-tick state 0x3d).

        // RestartIfActive stopped the old voice and started a fresh one for the SAME track.
        Assert.Equal(2, backend.PlayCalls.Count);
        Assert.NotEqual(firstVoice, AlundraMusicPlayer.Instance.CurrentVoiceForTests);
        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
    }

    // ---- 0xA6 (LoadBgm): 0 vs non-zero ------------------------------------------------------------

    [Fact]
    public void LoadBgm_ZeroOperand_RestartsImmediately_NeverArms_NoMasterCall()
    {
        var projectPath = BuildFixtureProjectWithOneTrack(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };
        service.Mixer.GetBus(AudioBusNames.Master).Volume = 0.3f; // a non-default value the machine must not touch.

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraMusicPlayer.Instance.PlayFromRawIndex(7);
        Assert.Single(backend.PlayCalls);

        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);

        AlundraBgmFadeDirector.Instance.LoadBgm(0);

        Assert.False(AlundraBgmFadeDirector.Instance.IsArmed); // fact 2: state untouched by the 0 branch.
        Assert.Equal(2, backend.PlayCalls.Count); // RestartIfActive ran immediately.
        Assert.Equal(0.3f, MasterVolume(service), 5); // the machine's own master call never ran.
        Assert.Equal(0, sfx.StopAllSfxCallCount); // no SFX key-off on this path.
    }

    [Fact]
    public void LoadBgm_NonZeroOperand_ArmsTheMachine_NoImmediateRestart()
    {
        var projectPath = BuildFixtureProjectWithOneTrack(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraMusicPlayer.Instance.PlayFromRawIndex(7);
        Assert.Single(backend.PlayCalls);

        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);

        AlundraBgmFadeDirector.Instance.LoadBgm(42); // any non-zero value.

        Assert.True(AlundraBgmFadeDirector.Instance.IsArmed);
        Assert.Single(backend.PlayCalls); // no restart until the swap tick, 59 ticks later.
    }

    // ---- 0xA5 (StopAllSound): conditional on the armed state --------------------------------------

    [Fact]
    public void StopAllSound_NotArmed_LeavesSfxUntouched_ButRestoresMasterAndRestartsBgm()
    {
        var projectPath = BuildFixtureProjectWithOneTrack(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };
        service.Mixer.GetBus(AudioBusNames.Master).Volume = 0.4f; // must end up restored to full.

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraMusicPlayer.Instance.PlayFromRawIndex(7);

        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);
        Assert.False(AlundraBgmFadeDirector.Instance.IsArmed); // fresh, never armed.

        AlundraBgmFadeDirector.Instance.StopAllSound();

        // The mutation this kills (D-B-1's own "0xA5 inconditionnel" - making the SFX stop run
        // regardless of the armed state): a not-armed machine must leave SFX completely untouched.
        Assert.Equal(0, sfx.StopAllSfxCallCount);
        Assert.Equal(1f, MasterVolume(service), 5); // restored unconditionally.
        Assert.Equal(2, backend.PlayCalls.Count); // BGM relaunched unconditionally (RestartIfActive).
    }

    [Fact]
    public void StopAllSound_Armed_StopsSfxAndDisarms_ThenRestoresMasterAndRestartsBgm()
    {
        var projectPath = BuildFixtureProjectWithOneTrack(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraMusicPlayer.Instance.PlayFromRawIndex(7);

        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);
        AlundraBgmFadeDirector.Instance.LoadBgm(5); // arm.
        Assert.True(AlundraBgmFadeDirector.Instance.IsArmed);

        AlundraBgmFadeDirector.Instance.StopAllSound();

        Assert.Equal(1, sfx.StopAllSfxCallCount); // armed -> the SFX stop DOES run.
        Assert.False(AlundraBgmFadeDirector.Instance.IsArmed); // disarmed.
        Assert.Equal(1f, MasterVolume(service), 5);
        Assert.Equal(2, backend.PlayCalls.Count); // BGM relaunched.
    }

    [Fact]
    public void PlaySfx_WhileArmed_IsBlocked_ThenReplaysAfterStopAllSound()
    {
        // fact 8/D-B-5's own dependency: the fade machine's armed state gates EVERY PlaySfx call.
        var sfxProjectPath = BuildSfxFixtureProject(out var sfxAssetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(sfxAssetId, new FakeAudioClip("sfx_fixture", 11025));
        var service = new AudioService(backend) { ClipProvider = provider };

        var soundPlayer = new AlundraSoundPlayer(service, new AlundraSoundBank(sfxProjectPath), new object());
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, soundPlayer);

        AlundraBgmFadeDirector.Instance.LoadBgm(5); // arm.
        Assert.True(AlundraBgmFadeDirector.Instance.IsArmed);

        soundPlayer.PlaySfx(300);
        Assert.Empty(backend.PlayCalls); // blocked - the mutation "guard removed" would let this through.

        AlundraBgmFadeDirector.Instance.StopAllSound(); // 0xA5 - reopens SFX.
        Assert.False(AlundraBgmFadeDirector.Instance.IsArmed);

        soundPlayer.PlaySfx(300);
        Assert.Single(backend.PlayCalls); // plays now.
    }

    // ---- production site: a synthetic event program, dispatched by the REAL runner ----------------

    [Fact]
    public void ProductionSite_SyntheticProgram_DispatchesAllThreeOpcodesThroughTheRealRunner()
    {
        var projectPath = BuildFixtureProjectWithOneTrack(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);

        var context = new ProductionFakeContext(AlundraMusicPlayer.Instance, AlundraBgmFadeDirector.Instance);

        // A program dispatching, in order: 0xA7 (play raw index 7, no stop-all), 0xA6 (arm), 0xA5 (stop
        // all sound - conditional on the just-armed state, then unconditional restore/restart), End.
        var codes = new[] { 0xA7, 7, 0, 0xA6, 1, 0xA5, 0xFF };
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = codes,
        };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), context);
        var entity = new AlundraEntityScriptProxy();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(6, state.CodeIndex); // 0xA7(3)+0xA6(2)+0xA5(1) = 6, right before the terminal 0xFF.
        Assert.Equal(2, backend.PlayCalls.Count); // 0xA7's own load + 0xA5's own unconditional restart.
        Assert.Equal(1, sfx.StopAllSfxCallCount); // 0xA6 armed it, so 0xA5 DID stop the (zero) live SFX.
        Assert.False(AlundraBgmFadeDirector.Instance.IsArmed); // 0xA5 disarmed it.
        Assert.Equal(1f, MasterVolume(service), 5);
    }

    private sealed class ProductionFakeContext : IEntityWorldContext
    {
        public ProductionFakeContext(IAlundraMusicPlayer musicPlayer, IAlundraBgmFadeDirector bgmFadeDirector)
        {
            MusicPlayer = musicPlayer;
            BgmFadeDirector = bgmFadeDirector;
        }

        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public AlundraEntityScriptProxy? PlayerEntity => null;
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) { }
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity) { }
        public CasaEngine.Framework.AI.Navigation.NavigationGrid2D? NavigationGrid => null;
        public IAlundraMusicPlayer? MusicPlayer { get; }
        public IAlundraBgmFadeDirector? BgmFadeDirector { get; }
    }

    // ---- fixture builders -------------------------------------------------------------------------

    private readonly List<string> _tempDirs = new();

    private string BuildFixtureProjectWithOneTrack(out Guid assetId)
    {
        var root = Path.Combine(Path.GetTempPath(), "AlundraBgmFadeDirectorTests_" + Guid.NewGuid());
        _tempDirs.Add(root);

        var musicsDir = Path.Combine(root, "Musics");
        Directory.CreateDirectory(musicsDir);
        assetId = Guid.Parse("00000000-0000-0000-0000-000000000007");
        var manifest = $$"""[{"sound_index": 7, "asset_id": "{{assetId}}"}]""";
        File.WriteAllText(Path.Combine(musicsDir, "bgm-manifest.json"), manifest);

        // Empty per-map table - these tests drive PlayFromRawIndex directly, never PlayMapMusic.
        var mapsDir = Path.Combine(root, "Maps");
        Directory.CreateDirectory(mapsDir);
        File.WriteAllText(Path.Combine(mapsDir, "music-index.json"), "{}");

        return root;
    }

    private string BuildSfxFixtureProject(out Guid assetId)
    {
        var root = Path.Combine(Path.GetTempPath(), "AlundraBgmFadeDirectorTests_Sfx_" + Guid.NewGuid());
        _tempDirs.Add(root);

        var soundsDir = Path.Combine(root, "Sounds");
        Directory.CreateDirectory(soundsDir);
        assetId = Guid.Parse("00000000-0000-0000-0000-000000000300");
        var json = $$"""
        [
          { "id": 300, "vab_id": 56, "program_number": 0, "tone_number": 0, "note": 60, "seq_num": -1,
            "ref_sfx_id": 0, "max_voices": 2, "num_tones": 1, "skip_reason": null,
            "tones": [ { "tone_index": 0, "file": "sfx_0300.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 0, "repeat": false, "asset_id": "{{assetId}}" } ] }
        ]
        """;
        File.WriteAllText(Path.Combine(soundsDir, "sfx-manifest.json"), json);

        return root;
    }
}
