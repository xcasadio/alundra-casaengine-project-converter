#nullable enable
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Alundra.Maps;
using Alundra.Events;
using Alundra.Scripts;

namespace Alundra.Audio;

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
    /// (B2, docs/plan-bgm-demarrage-binaire.md): when <see cref="AlundraMusicPlayer.CurrentMapSoundIndex"/>
    /// is negative, an IMMEDIATE, TOTAL no-op (B2's own <c>bltz</c> guard - nothing below runs at all,
    /// not even the SFX stop or the master restore). Otherwise: when the master fade machine is currently
    /// armed (<see cref="IsArmed"/>), stops every live SFX voice (<see cref="IAlundraSoundPlayer.StopAllSfx"/>)
    /// and disarms the machine - both skipped when already at rest (the "fade not armed =&gt; SFX
    /// untouched" test this class's own doc calls out). Restoring the master volume to full and calling
    /// <see cref="IAlundraMusicPlayer.PlaySequence"/> (B4: plays only if a track is loaded and silent -
    /// NEVER restarts a track already playing) happen UNCONDITIONALLY past the index guard, regardless of
    /// the armed state.
    /// </summary>
    void StopAllSound();

    /// <summary>
    /// Opcode 0xA6 (Script_166_0A6, EntityEventHandlers.cs:3134-3138) -&gt; <c>SoundManager.LoadBgm</c>
    /// (B14, docs/plan-bgm-demarrage-binaire.md): <see cref="IAlundraMusicPlayer.ClearResetSoundFlag"/>
    /// runs UNCONDITIONALLY first, exactly like the original's own <c>g_resetSoundFlag = 0</c>. Then:
    /// <paramref name="bgmIndex"/> == 0 STOPS whatever BGM is currently loaded, immediately
    /// (<see cref="IAlundraMusicPlayer.StopSequence"/> - an arrêt, never a relaunch; the armed state, if
    /// any, is left untouched, exactly like the original's own <c>LoadBgmCore</c>); any NON-ZERO value
    /// arms the 120-tick master fade machine (<see cref="IsArmed"/> becomes <see langword="true"/>) - the
    /// operand's value beyond zero/non-zero is unused, per the original.
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
