#nullable enable
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using Alundra.Maps;
using Alundra.Events;
using Alundra.Audio;

namespace Alundra.Transitions;

/// <summary>
/// Opcode-facing seam over <see cref="ScreenFadeDirector"/> (docs/plan-e10-fondu.md, slice
/// E10.b, D-E10-4: "the moteur is MECHANISM, the DLL is POLICY" - the engine's own
/// <see cref="ScreenEffectService"/> knows nothing about 16.16 fixed point, the channel swap, the
/// persistence latch, or any other PSX fidelity rule; all of that lives here). Backs opcodes
/// 0xAF/0xB0/0xB1 in <see cref="EventProgramRunner.Dispatch"/> via
/// <see cref="IEntityWorldContext.ScreenFadeDirector"/> - a default interface member (same shape as
/// <see cref="IEntityWorldContext.CellMutator"/>/<see cref="IEntityWorldContext.SoundPlayer"/>), so every
/// EXISTING implementer (<see cref="NoOpEntityWorldContext"/>, every test fake) keeps compiling
/// unmodified, degrading to null (skip-by-size, once-logged) exactly like those two seams.
/// </summary>
public interface IScreenFadeDirector
{
    /// <summary>
    /// Opcode 0xAF (Script_175_0AF, EntityEventHandlers.cs:3290-3300) - machine B, the ONLY machine that
    /// draws. <paramref name="r"/>/<paramref name="g"/>/<paramref name="b"/> are already in DISPLAY
    /// order (see <see cref="ScreenFadeDirector"/>'s own class doc on the channel swap - there is
    /// nothing to un-swap here, by construction). <paramref name="tpage"/> selects the blend mode
    /// (§1.3: 1 = Additive, 2 = Subtractive, else Opaque); <paramref name="duration"/> is the ramp length
    /// in TICKS; <paramref name="persistLock"/> is the persistence latch (§1.1 - never decremented,
    /// cleared only by the next map-entry install, see <see cref="ScreenFadeDirector.InstallForMapEntry"/>).
    /// </summary>
    void BeginFadeEffect(int r, int g, int b, int tpage, int duration, int persistLock);

    /// <summary>
    /// Opcode 0xB0 (Script_176_0B0, EntityEventHandlers.cs:3302-3312) - machine A, the "warp" timer.
    /// Its colours are DEAD in this port (§1.1: the only consumer of their output,
    /// <c>g_displayEnvColor*</c>, has zero readers) - only its flag/duration matter, consumed by
    /// <see cref="IsSettled"/> (0xB1).
    /// </summary>
    void SetWarpFadeDuration(int r, int g, int b, int duration);

    /// <summary>
    /// Opcode 0xB1's own predicate (Script_177_0B1, EntityEventHandlers.cs:3314-3327):
    /// <c>fadeStepFlags == 0 &amp;&amp; warpFlags == 0</c>. The caller (<see cref="EventProgramRunner.Dispatch"/>)
    /// writes <c>state.Result</c> from this BOTH ways - see that call site's own doc on why 0xB1 must
    /// never be routed through <c>UnknownOpcode</c>'s no-touch fallback (a skipped 0xB1 would leave
    /// Result stale).
    /// </summary>
    bool IsSettled { get; }
}
