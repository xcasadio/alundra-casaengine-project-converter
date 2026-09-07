#nullable enable
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;

namespace Alundra.Scripts;

/// <summary>
/// Opcode-facing seam over <see cref="AlundraBgmFadeDirector"/> (B2, docs/plan-e11b-opcodes-audio.md,
/// D-B-5) - backs opcode 0xA5 in <see cref="AlundraEventProgramRunner.Dispatch"/> (0xA6 dispatches
/// through the same instance too, D-B-5's own "one piece of state, one slice"). A default interface
/// member on <see cref="IEntityWorldContext"/> (same shape as
/// <see cref="IEntityWorldContext.ScreenFadeDirector"/>/<see cref="IEntityWorldContext.MusicPlayer"/>),
/// so every EXISTING implementer keeps compiling unmodified, degrading to null (skip-by-size, once-
/// logged) exactly like those two seams.
/// </summary>
public interface IAlundraBgmFadeDirector
{
    /// <summary>
    /// Opcode 0xA5 (Script_165_0A5, EntityEventHandlers.cs:3127-3131) -&gt; <c>SoundManager.StopAllSound</c>
    /// (fact 1): when the master fade machine is currently armed (<see cref="IsArmed"/>), stops every
    /// live SFX voice (<see cref="IAlundraSoundPlayer.StopAllSfx"/>) and disarms the machine
    /// (<see cref="IsArmed"/> becomes <see langword="false"/>) - both steps are skipped when the machine
    /// was already at rest (the "fade not armed =&gt; SFX untouched" test this class's own doc calls
    /// out). Restoring the master volume to full and restarting whatever BGM is currently resolved
    /// (<see cref="IAlundraMusicPlayer.RestartIfActive"/>) happen UNCONDITIONALLY, regardless of the
    /// armed state - the original's own <c>g_currentMapSoundIndex &gt;= 0</c> outer guard is not
    /// modeled (this port's own "current index" is never negative by construction, see
    /// <see cref="AlundraMusicPlayer"/>'s own doc), so this port treats that guard as always satisfied.
    /// </summary>
    void StopAllSound();

    /// <summary>
    /// Opcode 0xA6 (Script_166_0A6, EntityEventHandlers.cs:3134-3138) -&gt; <c>SoundManager.LoadBgm</c>
    /// (fact 2): <paramref name="bgmIndex"/> == 0 restarts whatever BGM is currently resolved
    /// IMMEDIATELY, with no fade (<see cref="IAlundraMusicPlayer.RestartIfActive"/> directly - the
    /// armed state, if any, is left untouched, exactly like the original's own <c>LoadBgmCore</c>);
    /// any NON-ZERO value arms the 120-tick master fade machine (<see cref="IsArmed"/> becomes
    /// <see langword="true"/>) - the operand's value beyond zero/non-zero is unused, per the original.
    /// </summary>
    void LoadBgm(int bgmIndex);

    /// <summary>
    /// True while the master fade machine is between armed (0xA6, non-zero operand) and settled back to
    /// rest, 120 ticks later - the original's own <c>g_soundEffectState != 0</c> (fact 8). Consulted by
    /// <see cref="IAlundraSoundPlayer.PlaySfx"/> as its very first guard, BEFORE the anti-duplicate
    /// table (same order as the original's own <c>PlaySoundEffectCore</c>) - see that member's own doc.
    /// </summary>
    bool IsArmed { get; }

    /// <summary>
    /// Advances the master fade machine by <paramref name="ticks"/> LOGIC ticks (D-B-5: tick-driven,
    /// never frame-driven - the same discipline <see cref="IAlundraScreenFadeDirector"/> already
    /// follows). Called by <see cref="AlundraWorldProxy.Update"/> with <c>ticksThisFrame</c>, once per
    /// rendered frame. A no-op while <see cref="IsArmed"/> is <see langword="false"/> (the machine is at
    /// rest - the original's own <c>FUN_8004b674</c> returns immediately when its state is 0).
    /// </summary>
    void Advance(int ticks);
}

/// <summary>
/// SESSION-scoped singleton (D-B-5, same shape/reasoning as <see cref="AlundraScreenFadeDirector"/>/
/// <see cref="AlundraMusicPlayer"/> - see either class's own doc for why a per-world instance would make
/// this state vacuous by construction): the faithful transcription of <c>FUN_8004b674</c>
/// (SoundManager.cs:3763-3801), the master fade machine 0xA5/0xA6 share (fact 2, transcribed in full in
/// docs/plan-e11b-opcodes-audio.md's own B2 section):
///
/// <code>
/// if state == 0            -&gt; at rest, nothing to do
/// n = state - 1
/// if n == 3      -&gt; state = 3    ; master(0x7f, 0x7f)                       // restore
/// if n == 0x3c   -&gt; state = 0x3c ; master(0, 0) ;
///                    InitializeBgm(requestedSeqId) ; key-off all 24 voices   // the BGM SWAP
/// if n &lt; 0x3d    -&gt; state = n                                              // silent descent
/// else           -&gt; v = (state - 0x3d) * 0x7f / 0x3c ; state = n ; master(v, v)  // rampe
/// </code>
///
/// Every branch decrements <c>state</c> by exactly one (<c>state = n = state - 1</c>) - so after
/// <c>t</c> ticks from an armed <c>0x78</c>, <c>state == 0x78 - t</c>, deterministically. Milestones
/// (hand-computed, docs/plan-e11b-opcodes-audio.md's own B2 report): armed at <c>0x78</c> (120), the
/// FIRST tick computes its ramp value off the PRE-decrement state 120: <c>(120-0x3d)*0x7f/0x3c =
/// 59*127/60 = 124</c> (truncating). The ramp keeps running down to pre-tick state <c>0x3e</c> (62):
/// <c>(62-0x3d)*0x7f/0x3c = 1*127/60 = 2</c>. The NEXT tick (pre-tick state <c>0x3d</c> = 61) is the
/// swap: master mutes to 0, the current BGM restarts in place, all 24 SFX voices are key-offed. The
/// machine then counts down SILENTLY (no master call at all) until pre-tick state <c>4</c>, where it
/// restores the master to full (<c>0x7f</c>) and keeps counting down silently to <c>0</c> (rest) - 120
/// ticks total from arming to rest.
/// </summary>
/// <remarks>
/// <para><b>"Master volume" here is the engine's own <see cref="AudioBusNames.Master"/> bus</b> (D-B-5:
/// this slice is DLL-only, no engine change - <see cref="AudioBus.Volume"/> and
/// <see cref="AudioService.Update"/>'s own gain-reapplication pass already exist and already cover
/// every live voice on every bus, exactly matching the original's own single hardware SPU master
/// register affecting BGM and SFX alike). No new engine primitive is added for this slice.</para>
///
/// <para><b>What "restart the BGM in place" means in this port</b> (fact 2's own <c>InitializeBgm</c>
/// call, and fact 1's own "RELANCE le BGM"): the original's <c>InitializeBgm</c> resets a SEQUENCE
/// STATE structure (position, flags) without itself choosing a new track, and <c>PlaySeq</c>
/// (re)starts playback of whatever sequence id is already loaded - this port has no separate
/// "loaded-but-not-playing" sequence state, so both collapse onto
/// <see cref="IAlundraMusicPlayer.RestartIfActive"/>: stop the current voice, if any, and start a
/// fresh one for the SAME resolved index - see that member's own doc.</para>
///
/// <para><b>0xA7's own stop-all flag is NOT handled here</b> (D-B-6): <see cref="StopAllSound"/> is
/// still the method 0xA7's dispatch calls when its flag operand is set (fact 3's own "StopAllSound
/// (drapeau) ou PlaySeq"), but the ORCHESTRATION - load first, then conditionally stop-all - lives at
/// the opcode dispatch site (<see cref="AlundraEventProgramRunner.Dispatch"/>'s own 0xA7 case), not in
/// this class or in <see cref="AlundraMusicPlayer.PlayFromRawIndex"/>.</para>
/// </remarks>
public sealed class AlundraBgmFadeDirector : IAlundraBgmFadeDirector
{
    /// <summary>The one session-scoped instance every <see cref="AlundraWorldProxy"/> shares (D-B-5) -
    /// never <see langword="new"/>'d per world.</summary>
    public static readonly AlundraBgmFadeDirector Instance = new();

    private AlundraBgmFadeDirector()
    {
    }

    /// <summary>Fact 2: the value 0xA6's non-zero operand arms the machine to - <c>0x78</c> (120), the
    /// original's own <c>g_soundEffectState = 0x78</c>.</summary>
    private const int ArmedState = 0x78;

    /// <summary>Full-scale reference for the master volume calls (fact 1/2: both <c>0x7f</c>) - the
    /// original's own 7-bit hardware volume range, projected onto <see cref="AudioBus.Volume"/>'s own
    /// [0,1] range.</summary>
    private const float FullScaleVolume = 0x7f;

    private AudioService? _audioService;
    private IAlundraSoundPlayer? _soundPlayer;

    /// <summary>Port of <c>g_soundEffectState</c> (fact 2) - 0 at rest, else the 1..120 countdown.</summary>
    private int _state;

    /// <summary>Re-points this session-scoped instance at the current world's own
    /// <see cref="AudioService"/>/<see cref="IAlundraSoundPlayer"/> - called by
    /// <see cref="AlundraWorldProxy.InstallAudioSystems"/> on every world install, even when
    /// <paramref name="audioService"/> is null (no <c>Game</c> for this world - tolerated, same shape as
    /// <see cref="AlundraScreenFadeDirector.AttachToWorld"/>). Deliberately does NOT touch
    /// <see cref="IsArmed"/>'s own state (D-B-5, same "AttachToWorld never touches state" contract as
    /// <see cref="AlundraMusicPlayer.AttachToWorld"/>/<see cref="AlundraScreenFadeDirector.AttachToWorld"/>)
    /// - a fade already in flight survives a map change exactly like the original's global does.</summary>
    public void AttachToWorld(AudioService? audioService, IAlundraSoundPlayer? soundPlayer)
    {
        _audioService = audioService;
        _soundPlayer = soundPlayer;
    }

    /// <inheritdoc/>
    public bool IsArmed => _state != 0;

    /// <inheritdoc/>
    public void StopAllSound()
    {
        // Fact 1: the SFX stop + state reset are CONDITIONAL on the machine being armed - the test this
        // class's own doc calls "fade not armed => SFX untouched". An unconditional stop here is the
        // named mutation the plan's own tests kill.
        if (_state != 0)
        {
            _soundPlayer?.StopAllSfx();
            _state = 0;
        }

        // Fact 1: master restore + BGM restart are UNCONDITIONAL (this port's own simplification of the
        // original's "g_currentMapSoundIndex >= 0" outer guard - see this class's own remarks).
        SetMasterVolume(FullScaleVolume);
        AlundraMusicPlayer.Instance.RestartIfActive();
    }

    /// <inheritdoc/>
    public void LoadBgm(int bgmIndex)
    {
        if (bgmIndex == 0)
        {
            // Fact 2: LoadBgmCore's own "bgmIndex == 0" branch never touches g_soundEffectState - an
            // already-armed ramp (if any) keeps running untouched, independently of this restart.
            AlundraMusicPlayer.Instance.RestartIfActive();
            return;
        }

        _state = ArmedState;
    }

    /// <inheritdoc/>
    public void Advance(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            AdvanceOneTick();
        }
    }

    private void AdvanceOneTick()
    {
        if (_state == 0)
        {
            return;
        }

        var n = _state - 1;

        if (n == 3)
        {
            _state = n;
            SetMasterVolume(FullScaleVolume);
            return;
        }

        if (n == 0x3c)
        {
            _state = n;
            SetMasterVolume(0);
            AlundraMusicPlayer.Instance.RestartIfActive();
            _soundPlayer?.StopAllSfx(); // key-off all 24 voices, unconditional at this exact tick.
            return;
        }

        if (n < 0x3d)
        {
            _state = n;
            return;
        }

        // Truncating integer division - matches the original's MIPS `div` exactly (docs/plan-e11b-
        // opcodes-audio.md's own B2 report: 124 at 0x78, 2 at 0x3e). Computed on the PRE-decrement
        // state, same order as the original.
        var v = (_state - 0x3d) * 0x7f / 0x3c;
        _state = n;
        SetMasterVolume(v);
    }

    private void SetMasterVolume(float volume)
    {
        if (_audioService == null || !_audioService.Mixer.TryGetBus(AudioBusNames.Master, out var bus))
        {
            return;
        }

        bus.Volume = volume / FullScaleVolume;
    }

    /// <summary>Test-only: clears every piece of session state so tests do not leak into each other
    /// through this singleton - same seam as <see cref="AlundraMusicPlayer.ResetForTests"/>/
    /// <see cref="AlundraScreenFadeDirector.ResetForTests"/>.</summary>
    internal void ResetForTests()
    {
        _audioService = null;
        _soundPlayer = null;
        _state = 0;
    }
}
