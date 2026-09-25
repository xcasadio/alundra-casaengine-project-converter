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
/// B2 of docs/plan-e11b-opcodes-audio.md, model rewritten by T2.1 of docs/plan-bgm-demarrage-binaire.md:
/// the master BGM fade machine (<see cref="AlundraBgmFadeDirector"/>, opcodes 0xA5/0xA6/0xA7). Joins
/// <see cref="AlundraMusicPlayerSingletonCollection"/> (rather than a new collection) because every test
/// here also touches <see cref="AlundraMusicPlayer.Instance"/> - the fade machine's own
/// <c>StopSequence</c>/<c>PlaySequence</c> calls go straight through to it (D-B-5) - so the two session
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
    public void Advance_FullyPastState0x3d_TheSwapTick_StopsTheCurrentBgm_NeverRestartsIt()
    {
        // B15 (docs/plan-bgm-demarrage-binaire.md): the swap tick's own InitializeBgm call is an ARRÊT,
        // never a relaunch - the named mutation this test kills is the swap tick calling PlaySequence
        // (or a restart) instead of StopSequence.
        var projectPath = BuildFixtureProjectWithOneTrack(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraMusicPlayer.Instance.PlayFromRawIndex(7); // loads track 7, no voice yet (B13).

        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);
        AlundraBgmFadeDirector.Instance.StopAllSound(); // simulate 0xA5 starting it, as production would.
        Assert.Single(backend.PlayCalls);

        AlundraBgmFadeDirector.Instance.LoadBgm(9); // arm

        AlundraBgmFadeDirector.Instance.Advance(60); // reach the swap tick (pre-tick state 0x3d).

        // B15: StopSequence stopped the voice - no second Play call, and the voice is no longer alive.
        Assert.Single(backend.PlayCalls);
        Assert.False(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
    }

    // ---- 0xA6 (LoadBgm): 0 vs non-zero ------------------------------------------------------------

    [Fact]
    public void LoadBgm_ZeroOperand_StopsImmediately_NeverArms_NoMasterCall_ClearsResetFlag()
    {
        // B14 (docs/plan-bgm-demarrage-binaire.md): bgmIndex == 0 is InitializeBgm - an arrêt, never a
        // restart - and g_resetSoundFlag = 0 runs unconditionally, even on this branch.
        var projectPath = BuildFixtureProjectWithOneMapEntry(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraMusicPlayer.Instance.PlayMapMusic(389); // loads track 7 (fixture map->raw), arms ResetSoundFlag.
        Assert.True(AlundraMusicPlayer.Instance.ResetSoundFlag);

        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);
        AlundraBgmFadeDirector.Instance.StopAllSound(); // simulate the frame-close start, as production would.
        Assert.Single(backend.PlayCalls);

        service.Mixer.GetBus(AudioBusNames.Master).Volume = 0.3f; // a non-default value the machine must not touch.

        AlundraBgmFadeDirector.Instance.LoadBgm(0);

        Assert.False(AlundraBgmFadeDirector.Instance.IsArmed); // state untouched by the 0 branch.
        Assert.Single(backend.PlayCalls); // StopSequence ran, not a restart - no second Play call.
        Assert.False(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
        Assert.Equal(0.3f, MasterVolume(service), 5); // the machine's own master call never ran.
        Assert.Equal(0, sfx.StopAllSfxCallCount); // no SFX key-off on this path.
        Assert.False(AlundraMusicPlayer.Instance.ResetSoundFlag); // B14: cleared unconditionally.
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
        AlundraMusicPlayer.Instance.PlayFromRawIndex(7); // loads only, no voice (B13).

        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);
        AlundraBgmFadeDirector.Instance.StopAllSound(); // starts it, as production's frame-close would.
        Assert.Single(backend.PlayCalls);

        AlundraBgmFadeDirector.Instance.LoadBgm(42); // any non-zero value.

        Assert.True(AlundraBgmFadeDirector.Instance.IsArmed);
        Assert.Single(backend.PlayCalls); // no restart until the swap tick, 59 ticks later.
    }

    // ---- 0xA5 (StopAllSound): conditional on the armed state, and on whether a voice is already alive --

    [Fact]
    public void StopAllSound_NotArmed_TrackLoadedButSilent_LeavesSfxUntouched_RestoresMasterAndStartsTheVoice()
    {
        var projectPath = BuildFixtureProjectWithOneTrack(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };
        service.Mixer.GetBus(AudioBusNames.Master).Volume = 0.4f; // must end up restored to full.

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraMusicPlayer.Instance.PlayFromRawIndex(7); // loads only - no voice yet (B13).
        Assert.Empty(backend.PlayCalls);

        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);
        Assert.False(AlundraBgmFadeDirector.Instance.IsArmed); // fresh, never armed.

        AlundraBgmFadeDirector.Instance.StopAllSound();

        // The mutation this kills (0xA5 inconditionnel - making the SFX stop run regardless of the armed
        // state): a not-armed machine must leave SFX completely untouched.
        Assert.Equal(0, sfx.StopAllSfxCallCount);
        Assert.Equal(1f, MasterVolume(service), 5); // restored unconditionally.
        Assert.Single(backend.PlayCalls); // B1/B2: StopAllSound is the executable's ONLY BGM-playing site.
    }

    [Fact]
    public void StopAllSound_TrackAlreadyPlaying_NeverRestartsIt()
    {
        // Acceptance item (g): 0xA5 while a track already plays must leave the SAME voice alone.
        var projectPath = BuildFixtureProjectWithOneTrack(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraMusicPlayer.Instance.PlayFromRawIndex(7);

        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);
        AlundraBgmFadeDirector.Instance.StopAllSound(); // starts the voice.
        Assert.Single(backend.PlayCalls);
        var voice = AlundraMusicPlayer.Instance.CurrentVoiceForTests;

        AlundraBgmFadeDirector.Instance.StopAllSound(); // a second 0xA5 while it already plays.

        Assert.Single(backend.PlayCalls); // B4: PlaySeq never touches a voice already alive - no restart.
        Assert.Equal(voice, AlundraMusicPlayer.Instance.CurrentVoiceForTests);
    }

    [Fact]
    public void StopAllSound_Armed_StopsSfxAndDisarms_ThenRestoresMasterAndStartsTheVoice()
    {
        var projectPath = BuildFixtureProjectWithOneTrack(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        AlundraMusicPlayer.Instance.PlayFromRawIndex(7); // loads only - no voice yet.

        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);
        AlundraBgmFadeDirector.Instance.LoadBgm(5); // arm.
        Assert.True(AlundraBgmFadeDirector.Instance.IsArmed);

        AlundraBgmFadeDirector.Instance.StopAllSound();

        Assert.Equal(1, sfx.StopAllSfxCallCount); // armed -> the SFX stop DOES run.
        Assert.False(AlundraBgmFadeDirector.Instance.IsArmed); // disarmed.
        Assert.Equal(1f, MasterVolume(service), 5);
        Assert.Single(backend.PlayCalls); // B1/B2: this IS the track's first (and only) play.
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
        Assert.Single(backend.PlayCalls); // 0xA7 only LOADS (B13) - 0xA5's own PlaySequence is the only play.
        Assert.Equal(1, sfx.StopAllSfxCallCount); // 0xA6 armed it, so 0xA5 DID stop the (zero) live SFX.
        Assert.False(AlundraBgmFadeDirector.Instance.IsArmed); // 0xA5 disarmed it.
        Assert.Equal(1f, MasterVolume(service), 5);
    }

    // -----------------------------------------------------------------------------------------------
    // T2.1 acceptance (docs/plan-bgm-demarrage-binaire.md): opcode 0xA7's own load-only semantics
    // (d/e/f), driven through the REAL runner, exactly like ProductionSite_SyntheticProgram above.
    // -----------------------------------------------------------------------------------------------

    private static AlundraEventProgramRunner NewRunner(int[] codes, out AlundraEntityScriptProxy entity, out EventProgramState state)
    {
        var context = new ProductionFakeContext(AlundraMusicPlayer.Instance, AlundraBgmFadeDirector.Instance);
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = codes,
        };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), context);
        entity = new AlundraEntityScriptProxy();
        state = new EventProgramState { Codes = document.CodesAsBytes() };
        return runner;
    }

    [Fact]
    public void PlayMusic_0xA7_StopAllFlagZero_OnATrackAlreadyPlaying_LoadsSilently_ThenA5StartsTheNewTrack()
    {
        // Acceptance item (d).
        var projectPath = BuildFixtureProjectWithTwoTracks(out var asset7, out var asset5);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(asset7, new FakeAudioClip("bgm_007", 44100));
        provider.Register(asset5, new FakeAudioClip("bgm_005", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);

        // Track 7 already playing - "sur une carte qui joue".
        AlundraMusicPlayer.Instance.PlayFromRawIndex(7);
        AlundraBgmFadeDirector.Instance.StopAllSound();
        Assert.Single(backend.PlayCalls);

        var loadRunner = NewRunner(new[] { 0xA7, 5, 0, 0xFF }, out var loadEntity, out var loadState);
        loadRunner.RunOneScriptCall(loadEntity, loadState); // 0xA7(5,0): stops+closes 7, loads 5.

        Assert.False(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
        Assert.Single(backend.PlayCalls); // still just the original track-7 play.

        var stopAllRunner = NewRunner(new[] { 0xA5, 0xFF }, out var stopAllEntity, out var stopAllState);
        stopAllRunner.RunOneScriptCall(stopAllEntity, stopAllState); // 0xA5 starts track 5.

        Assert.Equal(2, backend.PlayCalls.Count);
        Assert.Equal("bgm_005", ((FakeAudioClip)backend.PlayCalls[1].Clip).Name);
    }

    [Fact]
    public void PlayMusic_0xA7_StopAllFlagSet_StartsTheVoiceRightFromTheOpcode()
    {
        // Acceptance item (e): v[2] != 0 makes the runner's own dispatch call StopAllSound right after
        // the load, in the SAME opcode - a voice must be alive as soon as RunOneScriptCall returns.
        var projectPath = BuildFixtureProjectWithTwoTracks(out _, out var asset5);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(asset5, new FakeAudioClip("bgm_005", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);

        var runner = NewRunner(new[] { 0xA7, 5, 1, 0xFF }, out var entity, out var state);
        runner.RunOneScriptCall(entity, state);

        Assert.Single(backend.PlayCalls);
        Assert.Equal("bgm_005", ((FakeAudioClip)backend.PlayCalls[0].Clip).Name);
        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
    }

    [Fact]
    public void PlayMusic_0xA7_ZeroIndex_ThenA5_StaysSilent()
    {
        // Acceptance item (f). F4: a track must actually be LOADED AND PLAYING first, or "PlayCalls
        // stays empty" is trivially true regardless of what 0xA7(0,0)/0xA5 do - the previous version of
        // this test never loaded anything and never even ran the 0xA5 half (RunOneScriptCall executes
        // ONE opcode; the original single call only ever reached the 0xA7 instruction).
        var projectPath = BuildFixtureProjectWithOneTrack(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);

        // Track 7 loaded and started first - "sur une carte qui joue" (fact d's own precondition).
        AlundraMusicPlayer.Instance.PlayFromRawIndex(7);
        AlundraBgmFadeDirector.Instance.StopAllSound();
        Assert.Single(backend.PlayCalls);
        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);

        var loadRunner = NewRunner(new[] { 0xA7, 0, 0, 0xFF }, out var loadEntity, out var loadState);
        loadRunner.RunOneScriptCall(loadEntity, loadState); // 0xA7(0,0): stops+closes, flag untouched.

        Assert.False(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
        Assert.Single(backend.PlayCalls); // no new play from the load itself.

        var stopAllRunner = NewRunner(new[] { 0xA5, 0xFF }, out var stopAllEntity, out var stopAllState);
        stopAllRunner.RunOneScriptCall(stopAllEntity, stopAllState); // 0xA5: PlaySequence - nothing loaded now.

        Assert.Single(backend.PlayCalls); // still exactly the one original play - stays silent.
        Assert.False(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
        Assert.Equal(0, AlundraMusicPlayer.Instance.CurrentMapSoundIndex);
    }

    // ---- 0xA6 (LoadBgm): stop and swap-tick behaviour on a REALLY playing voice (h/i) ---------------

    [Fact]
    public void LoadBgm_ZeroOperand_StopsTheVoice_ThenA5StartsAFreshOneForTheSameTrack()
    {
        // Acceptance item (h).
        var projectPath = BuildFixtureProjectWithOneTrack(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);

        AlundraMusicPlayer.Instance.PlayFromRawIndex(7);
        AlundraBgmFadeDirector.Instance.StopAllSound(); // starts it.
        Assert.Single(backend.PlayCalls);
        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);

        AlundraBgmFadeDirector.Instance.LoadBgm(0); // 0xA6 0.

        Assert.False(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
        Assert.Single(backend.PlayCalls); // still just the one, so far.

        AlundraBgmFadeDirector.Instance.StopAllSound(); // 0xA5.

        Assert.Equal(2, backend.PlayCalls.Count); // a NEW voice for the SAME track.
        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
    }

    [Fact]
    public void LoadBgm_NonZeroOperand_Advance_SwapTickStopsTheVoice_RestoreTickStaysSilent_ThenA5Starts()
    {
        // Acceptance item (i).
        var projectPath = BuildFixtureProjectWithOneTrack(out var assetId);
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(assetId, new FakeAudioClip("bgm_fixture", 44100));
        var service = new AudioService(backend) { ClipProvider = provider };

        AlundraMusicPlayer.Instance.AttachToWorld(service, projectPath);
        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);

        AlundraMusicPlayer.Instance.PlayFromRawIndex(7);
        AlundraBgmFadeDirector.Instance.StopAllSound(); // starts it.
        Assert.Single(backend.PlayCalls);

        AlundraBgmFadeDirector.Instance.LoadBgm(1); // 0xA6 1 - arm.
        AlundraBgmFadeDirector.Instance.Advance(60); // swap tick (pre-tick state 0x3d).

        Assert.False(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive); // stopped, not restarted.

        AlundraBgmFadeDirector.Instance.Advance(57); // restore tick (pre-tick state 4), 117 total.

        Assert.Equal(1f, MasterVolume(service), 5);
        Assert.False(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive); // still silent - restore != play.
        Assert.Single(backend.PlayCalls); // no extra Play call anywhere in this whole sequence.

        AlundraBgmFadeDirector.Instance.StopAllSound(); // 0xA5.

        Assert.Equal(2, backend.PlayCalls.Count);
        Assert.True(AlundraMusicPlayer.Instance.IsCurrentVoiceAlive);
    }

    // ---- master fade armed on a -1 map: master untouched by 0xA5 (k) --------------------------------

    [Fact]
    public void OnANegativeIndexMap_LoadBgmArmed_ThenA5_LeavesMasterAlone_StaysArmed()
    {
        // Acceptance item (k): B2's own index guard - CurrentMapSoundIndex < 0 makes StopAllSound a
        // TOTAL no-op, so an armed fade survives it untouched (master volume included).
        var backend = new FakeAudioBackend();
        var service = new AudioService(backend);
        service.Mixer.GetBus(AudioBusNames.Master).Volume = 0.55f;
        var sfx = new FakeSoundPlayerForFade();
        AlundraBgmFadeDirector.Instance.AttachToWorld(service, sfx);

        // B7: a -1 map's own raw table entry loads track 1 but leaves CurrentMapSoundIndex at -1 (never
        // remapped) - the fixture's own map 183 -> raw -1 entry, same shape as the real table (B16).
        var minusOneProjectPath = BuildFixtureProjectWithNegativeMapEntry();
        AlundraMusicPlayer.Instance.AttachToWorld(service, minusOneProjectPath);
        AlundraMusicPlayer.Instance.PlayMapMusic(183);
        Assert.Equal(-1, AlundraMusicPlayer.Instance.CurrentMapSoundIndex);

        AlundraBgmFadeDirector.Instance.LoadBgm(1); // 0xA6 1 - arm.
        Assert.True(AlundraBgmFadeDirector.Instance.IsArmed);

        AlundraBgmFadeDirector.Instance.StopAllSound(); // 0xA5.

        Assert.True(AlundraBgmFadeDirector.Instance.IsArmed); // B2: untouched - total no-op.
        Assert.Equal(0.55f, MasterVolume(service), 5); // master untouched too.
        Assert.Empty(backend.PlayCalls); // and PlaySequence never ran either.
    }

    private string BuildFixtureProjectWithTwoTracks(out Guid asset7, out Guid asset5)
    {
        var root = Path.Combine(Path.GetTempPath(), "AlundraBgmFadeDirectorTests_Two_" + Guid.NewGuid());
        _tempDirs.Add(root);

        var musicsDir = Path.Combine(root, "Musics");
        Directory.CreateDirectory(musicsDir);
        asset7 = Guid.Parse("00000000-0000-0000-0000-000000000007");
        asset5 = Guid.Parse("00000000-0000-0000-0000-000000000005");
        var manifest = $$"""[{"sound_index": 7, "asset_id": "{{asset7}}"}, {"sound_index": 5, "asset_id": "{{asset5}}"}]""";
        File.WriteAllText(Path.Combine(musicsDir, "bgm-manifest.json"), manifest);

        var mapsDir = Path.Combine(root, "Maps");
        Directory.CreateDirectory(mapsDir);
        File.WriteAllText(Path.Combine(mapsDir, "music-index.json"), "{}");

        return root;
    }

    private string BuildFixtureProjectWithNegativeMapEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), "AlundraBgmFadeDirectorTests_Neg_" + Guid.NewGuid());
        _tempDirs.Add(root);

        var musicsDir = Path.Combine(root, "Musics");
        Directory.CreateDirectory(musicsDir);
        var asset1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        File.WriteAllText(Path.Combine(musicsDir, "bgm-manifest.json"), $$"""[{"sound_index": 1, "asset_id": "{{asset1}}"}]""");

        var mapsDir = Path.Combine(root, "Maps");
        Directory.CreateDirectory(mapsDir);
        File.WriteAllText(Path.Combine(mapsDir, "music-index.json"), """{"183": -1}""");

        return root;
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

    /// <summary>Same fixture as <see cref="BuildFixtureProjectWithOneTrack"/>, but with a map 389 -&gt;
    /// raw index 7 entry - so a test can arm <see cref="AlundraMusicPlayer.ResetSoundFlag"/> through the
    /// real <see cref="AlundraMusicPlayer.PlayMapMusic"/> path (B7) instead of <see cref="AlundraMusicPlayer.PlayFromRawIndex"/>
    /// (which clears the flag itself, P8, and so cannot be used to arm it).</summary>
    private string BuildFixtureProjectWithOneMapEntry(out Guid assetId)
    {
        var root = Path.Combine(Path.GetTempPath(), "AlundraBgmFadeDirectorTests_Map_" + Guid.NewGuid());
        _tempDirs.Add(root);

        var musicsDir = Path.Combine(root, "Musics");
        Directory.CreateDirectory(musicsDir);
        assetId = Guid.Parse("00000000-0000-0000-0000-000000000007");
        var manifest = $$"""[{"sound_index": 7, "asset_id": "{{assetId}}"}]""";
        File.WriteAllText(Path.Combine(musicsDir, "bgm-manifest.json"), manifest);

        var mapsDir = Path.Combine(root, "Maps");
        Directory.CreateDirectory(mapsDir);
        File.WriteAllText(Path.Combine(mapsDir, "music-index.json"), """{"389": 7}""");

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
