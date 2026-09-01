#nullable enable
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;

namespace Alundra.Scripts;

/// <summary>
/// Opcode-facing seam over <see cref="AlundraScreenFadeDirector"/> (docs/plan-e10-fondu.md, slice
/// E10.b, D-E10-4: "the moteur is MECHANISM, the DLL is POLICY" - the engine's own
/// <see cref="ScreenEffectService"/> knows nothing about 16.16 fixed point, the channel swap, the
/// persistence latch, or any other PSX fidelity rule; all of that lives here). Backs opcodes
/// 0xAF/0xB0/0xB1 in <see cref="AlundraEventProgramRunner.Dispatch"/> via
/// <see cref="IEntityWorldContext.ScreenFadeDirector"/> - a default interface member (same shape as
/// <see cref="IEntityWorldContext.CellMutator"/>/<see cref="IEntityWorldContext.SoundPlayer"/>), so every
/// EXISTING implementer (<see cref="NoOpEntityWorldContext"/>, every test fake) keeps compiling
/// unmodified, degrading to null (skip-by-size, once-logged) exactly like those two seams.
/// </summary>
public interface IAlundraScreenFadeDirector
{
    /// <summary>
    /// Opcode 0xAF (Script_175_0AF, EntityEventHandlers.cs:3290-3300) - machine B, the ONLY machine that
    /// draws. <paramref name="r"/>/<paramref name="g"/>/<paramref name="b"/> are already in DISPLAY
    /// order (see <see cref="AlundraScreenFadeDirector"/>'s own class doc on the channel swap - there is
    /// nothing to un-swap here, by construction). <paramref name="tpage"/> selects the blend mode
    /// (§1.3: 1 = Additive, 2 = Subtractive, else Opaque); <paramref name="duration"/> is the ramp length
    /// in TICKS; <paramref name="persistLock"/> is the persistence latch (§1.1 - never decremented,
    /// cleared only by the next map-entry install, see <see cref="AlundraScreenFadeDirector.InstallForMapEntry"/>).
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
    /// <c>fadeStepFlags == 0 &amp;&amp; warpFlags == 0</c>. The caller (<see cref="AlundraEventProgramRunner.Dispatch"/>)
    /// writes <c>state.Result</c> from this BOTH ways - see that call site's own doc on why 0xB1 must
    /// never be routed through <c>UnknownOpcode</c>'s no-touch fallback (a skipped 0xB1 would leave
    /// Result stale).
    /// </summary>
    bool IsSettled { get; }
}

/// <summary>
/// SESSION-scoped singleton (D-E10-6, same shape as <see cref="AlundraMusicPlayer"/> - see that class's
/// own doc for the full reasoning on why a per-world instance would make this state vacuous by
/// construction): the two 16.16 fixed-point colour machines of <c>RenderTransitionEffects</c>
/// (GraphicManager.cs:1552-1643), plus the persistence latch and the "arm effect 0 at map entry" preamble
/// (GameEngine.cs:886-905, D-E10-7).
/// </summary>
/// <remarks>
/// <para><b>The channel swap (§1.2), and why there is no swap in this code</b>: the decompilation writes
/// opcode 0xAF's FIRST colour operand into <c>g_targetFadeColorB</c> and its THIRD into
/// <c>g_targetFadeColorR</c> (EntityEventHandlers.cs:3292-3294), and <c>RenderTransitionEffects</c> then
/// reads <c>g_currentFadeColorB</c> into the DISPLAYED red channel (<c>tile.r0</c>) and
/// <c>g_currentFadeColorR</c> into the displayed blue channel (<c>tile.b0</c>) - GraphicManager.cs:
/// 1594-1596. So the decomp's own "R"/"B" variable NAMES are inverted relative to what they draw, but
/// the OPCODE's own operand order already matches DISPLAY order (R, G, B) end to end. This port stores
/// every channel in DISPLAY order from the very first write (<see cref="BeginFadeEffect"/>'s own
/// <paramref name="r"/>/<paramref name="g"/>/<paramref name="b"/> parameters ARE what reaches the
/// screen) - there is nothing to "fix", only to document: never rename these fields to chase the
/// decomp's own inverted B/R names, and never route an operand through a second swap "for symmetry"
/// with the decompilation - that would silently reintroduce the very bug this note exists to prevent.</para>
///
/// <para><b>Machine A's colours are dead</b> (§1.1: <c>g_displayEnvColor*</c>, the only output of
/// <c>g_warpFadeColor*</c>, has zero readers in the decompilation) - this class still runs machine A's
/// full 16.16 ramp (so its FLAG timing is faithful, including the truncating-division edges of §1.4),
/// but never exposes its colour, matching the original's own dead output.</para>
///
/// <para><b>The map-entry preamble (§1.1, D-E10-7)</b>: <see cref="InstallForMapEntry"/> ports
/// GameEngine.cs:886-888 (<c>g_fadeFrameCounter = 0; g_fadeStepFlags = 0; g_warpFlags = 0;</c>) followed
/// IMMEDIATELY by :895-905's own re-arm of effect 0 (subtractive, 16 ticks, 0xff0000 -&gt; 0) - the two
/// steps are kept as textually separate statements (never merged into one assignment) so a mutation that
/// drops ONLY the reset - while keeping the re-arm - is something a test can actually apply (T7).</para>
/// </remarks>
public sealed class AlundraScreenFadeDirector : IAlundraScreenFadeDirector
{
    /// <summary>The one session-scoped instance every <see cref="AlundraWorldProxy"/> shares (D-E10-6) -
    /// never <see langword="new"/>'d per world.</summary>
    public static readonly AlundraScreenFadeDirector Instance = new();

    private AlundraScreenFadeDirector()
    {
    }

    private ScreenEffectService? _service;

    // ---- Machine B: the fade rectangle (the only one that draws) - g_currentFadeColor*/
    // g_targetFadeColor*/g_fadeColorStep* (GraphicManager.cs), plus g_fadeStepFlags/g_fadeFrameCounter/
    // g_fadeTPagePrim1. Every channel is stored in DISPLAY order (R, G, B) - see this class' own doc.
    private bool _fadeActive;
    private int _persistLock;
    private int _currentR;
    private int _currentG;
    private int _currentB;
    private int _targetR;
    private int _targetG;
    private int _targetB;
    private int _stepR;
    private int _stepG;
    private int _stepB;
    private int _tpage;

    /// <summary>
    /// The draw-guard's own <c>fadeStepFlags</c> value as observed ENTERING the LAST tick
    /// <see cref="Advance"/> processed this frame (or the untouched current value, on a frame with no
    /// tick at all) - GraphicManager.cs:1572-1578 checks this flag BEFORE that tick's own advance/arrival
    /// check runs, so a tick that ARRIVES this very call (§1.5: current becomes exactly target, flags
    /// clears to <see langword="false"/> inside <see cref="AdvanceOneTick"/>) must still be drawn - only
    /// the FOLLOWING tick, which now finds the flag already clear on entry, is skipped. Since this port
    /// pushes once per rendered frame regardless of tick count (§1.6), this field is what
    /// <see cref="PushToAttachedService"/> gates on, NOT <see cref="_fadeActive"/>'s post-advance value.
    /// </summary>
    private bool _fadeGateActiveEnteringPush;

    // ---- Machine A: the warp timer - g_warpFadeColor*/g_fadeColor*_Target/g_warpFadeColor*_Step, plus
    // g_warpFlags. Colours are DEAD (see this class' own doc) - kept only for faithful flag/duration
    // timing.
    private bool _warpActive;
    private int _warpCurrentR;
    private int _warpCurrentG;
    private int _warpCurrentB;
    private int _warpTargetR;
    private int _warpTargetG;
    private int _warpTargetB;
    private int _warpStepR;
    private int _warpStepG;
    private int _warpStepB;

    /// <summary>Re-points this session-scoped instance at the current world's own
    /// <see cref="ScreenEffectService"/> (<c>world.Game?.ScreenEffectComponent?.Service</c>) - called by
    /// <see cref="AlundraWorldProxy.InstallScreenFadeSystems"/> on every world install. Deliberately does
    /// NOT touch any of the fade/warp state (D-E10-6, same shape as <see cref="AlundraMusicPlayer.AttachToWorld"/>)
    /// - only <see cref="InstallForMapEntry"/> does that. Null is a valid, tolerated value (T2 - a world
    /// with no <c>Game</c>): <see cref="PushToAttachedService"/> then bypasses with no exception.</summary>
    public void AttachToWorld(ScreenEffectService? service) => _service = service;

    /// <summary>
    /// Port of the map-entry preamble (GameEngine.cs:886-888) immediately followed by the effect-0 re-arm
    /// (GameEngine.cs:895-905, §1.5) - called by <see cref="AlundraWorldProxy.InstallScreenFadeSystems"/>,
    /// itself called from <see cref="AlundraWorldProxy.InitializeWithWorld"/> (D-E10-7: armed INSIDE the
    /// installation method, no second, independently deletable call site).
    ///
    /// <b>Pushes NOTHING to the attached service</b> (the boxed rule ahead of this plan's own tests):
    /// pushing the just-armed state here would submit 255 (a full black frame the original never draws,
    /// §1.5) - the first push happens from <see cref="AlundraWorldProxy.Update"/>, AFTER
    /// <see cref="Advance"/> has already consumed at least one tick (the first-frame tick floor,
    /// <c>AlundraWorldProxy._firstFrameStillOpen</c>), so the first value ever pushed is 239.
    /// </summary>
    public void InstallForMapEntry()
    {
        // GameEngine.cs:886-888 - the reset. Kept as its own, separately mutable step (see this class'
        // own class doc) - T7's own mutation deletes exactly these three lines and nothing else.
        _persistLock = 0;
        _fadeActive = false;
        _warpActive = false;

        // GameEngine.cs:895-905 (WarpPlayer's own "case 0"): subtractive, 16 ticks, 0xff0000 -> 0. Only
        // effect 0 is wired (D-E10-7) - the table for the other transition ids is documented (§1.5) for
        // a later chantier, not implemented here.
        _currentR = _currentG = _currentB = 0xff0000;
        _targetR = _targetG = _targetB = 0;
        _fadeActive = true; // GameEngine.cs:904's own "g_fadeStepFlags = 1", written BEFORE
                             // ApplyScreenFade (:972) calls BeginFadeEffect - so the original's own
                             // BeginFadeEffect ALWAYS observes the flag already 1 at this call site.
        _tpage = 2; // subtractive (GameEngine.cs:896's own drawPage for case 0).
        ApplyDurationEdgeForFadeMachine(activeAtEntry: _fadeActive, duration: 16);
    }

    /// <inheritdoc/>
    public void BeginFadeEffect(int r, int g, int b, int tpage, int duration, int persistLock)
    {
        // EntityEventHandlers.cs:3292-3297: target written, THEN frameCounter, THEN flags = 1, THEN
        // BeginFadeEffect - so this call always observes flags ALREADY 1 (see this method's own doc on
        // why the branch below is kept anyway).
        _targetR = r << 16;
        _targetG = g << 16;
        _targetB = b << 16;
        _persistLock = persistLock;
        _fadeActive = true; // written before the duration edge runs, same order as the opcode handler.
        _tpage = tpage;
        ApplyDurationEdgeForFadeMachine(activeAtEntry: _fadeActive, duration);
    }

    /// <inheritdoc/>
    public void SetWarpFadeDuration(int r, int g, int b, int duration)
    {
        // EntityEventHandlers.cs:3305-3309: targets written, THEN warpFlags = 1, THEN SetFadeDuration -
        // same write order as BeginFadeEffect above.
        _warpTargetR = r << 16;
        _warpTargetG = g << 16;
        _warpTargetB = b << 16;
        _warpActive = true; // written before the duration edge runs, same order as the opcode handler.
        ApplyDurationEdgeForWarpMachine(activeAtEntry: _warpActive, duration);
    }

    /// <summary>
    /// Port of <c>GameEngine.BeginFadeEffect</c>'s own duration branch (GameEngine.cs:986-1029): when the
    /// machine was NOT already active at entry, it SNAPS (<c>current = target</c>, step = 0, no
    /// animation) instead of dividing - kept faithfully even though, in THIS port's only two call sites
    /// (<see cref="InstallForMapEntry"/>/<see cref="BeginFadeEffect"/>), the caller always sets the flag
    /// to <see langword="true"/> immediately before this runs, so <paramref name="activeAtEntry"/> is
    /// always <see langword="true"/> in production - the branch is not collapsed away, so a caller that
    /// arms this machine WITHOUT presetting the flag still gets the original's own snap behaviour.
    ///
    /// <b>D-E10-8 necessity deviation</b>: the original divides unconditionally in its "active" branch
    /// and traps on <paramref name="duration"/> == 0 (a debug-only breakpoint, GameEngine.cs:1001-1004);
    /// this port instead folds <c>duration == 0</c> into the SAME snap path used for "was not active" -
    /// both observably mean "settle immediately, nothing to ramp" - rather than dividing by zero.
    /// </summary>
    private void ApplyDurationEdgeForFadeMachine(bool activeAtEntry, int duration)
    {
        if (!activeAtEntry || duration == 0)
        {
            _stepR = _stepG = _stepB = 0;
            _currentR = _targetR;
            _currentG = _targetG;
            _currentB = _targetB;
            return;
        }

        // Truncating C# integer division - matches the original's MIPS `div` (GameEngine.cs:1000/1010/
        // 1020) exactly, including the "duration+1 ticks when the delta does not divide evenly" and
        // "delta smaller in magnitude than duration truncates the step to 0" edges (§1.4) - both fall
        // out of this one line with no special-casing, see MoveTowards's own doc for the rest.
        _stepR = (_targetR - _currentR) / duration;
        _stepG = (_targetG - _currentG) / duration;
        _stepB = (_targetB - _currentB) / duration;
    }

    /// <summary>Port of <c>GameEngine.SetFadeDuration</c>'s own duration branch (GameEngine.cs:1032-
    /// 1064) - identical shape to <see cref="ApplyDurationEdgeForFadeMachine"/>, applied to machine A's
    /// (dead) colours instead, purely so its FLAG timing (consumed by <see cref="IsSettled"/>) is
    /// faithful to the same truncating-division edges.</summary>
    private void ApplyDurationEdgeForWarpMachine(bool activeAtEntry, int duration)
    {
        if (!activeAtEntry || duration == 0)
        {
            _warpStepR = _warpStepG = _warpStepB = 0;
            _warpCurrentR = _warpTargetR;
            _warpCurrentG = _warpTargetG;
            _warpCurrentB = _warpTargetB;
            return;
        }

        _warpStepR = (_warpTargetR - _warpCurrentR) / duration;
        _warpStepG = (_warpTargetG - _warpCurrentG) / duration;
        _warpStepB = (_warpTargetB - _warpCurrentB) / duration;
    }

    /// <summary>
    /// Advances both machines by <paramref name="ticks"/> LOGIC ticks (§1.6, D-E10-8: tick-driven, never
    /// frame-driven - the class of bug E5.c/the first-frame camera already found). Called by
    /// <see cref="AlundraWorldProxy.Update"/> with <c>ticksThisFrame</c>, BEFORE <see cref="PushToAttachedService"/>
    /// runs for that same frame - "advance, then draw" (§1.5), never the reverse.
    /// </summary>
    public void Advance(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            // Captured BEFORE this tick's own advance - see _fadeGateActiveEnteringPush's own doc: the
            // original checks the flag before advancing, so an arrival happening INSIDE this very tick
            // must still be drawn once (the flag only reads false entering the NEXT tick).
            _fadeGateActiveEnteringPush = _fadeActive;
            AdvanceOneTick();
        }

        if (ticks == 0)
        {
            // No tick ran this frame - re-submit whatever is already held (§1.6: "a frame with no tick
            // re-submits the held value"), gated on the CURRENT (untouched) state.
            _fadeGateActiveEnteringPush = _fadeActive;
        }
    }

    /// <summary>One tick of <c>RenderTransitionEffects</c>' own state advance (GraphicManager.cs:1552-
    /// 1591), minus the draw call itself (that lives in the engine's own <c>ScreenEffectComponent</c>,
    /// D-E10-4).</summary>
    private void AdvanceOneTick()
    {
        // Machine A (warp) - unconditional advance/arrival check while active, GraphicManager.cs:1554-
        // 1565. Not gated by machine B's own draw guard - the two machines are independent.
        if (_warpActive)
        {
            _warpCurrentR = MoveTowards(_warpCurrentR, _warpTargetR, _warpStepR);
            _warpCurrentG = MoveTowards(_warpCurrentG, _warpTargetG, _warpStepG);
            _warpCurrentB = MoveTowards(_warpCurrentB, _warpTargetB, _warpStepB);

            if (_warpCurrentR == _warpTargetR && _warpCurrentG == _warpTargetG && _warpCurrentB == _warpTargetB)
            {
                _warpActive = false;
            }
        }

        // Machine B (fade rectangle) - GraphicManager.cs:1579-1591. No-op while inactive; the DRAW guard
        // itself (flags == 0 && persistLock == 0 -> nothing submitted) is applied in
        // PushToAttachedService, not here - an inactive-but-persisted machine (persistLock != 0, T5)
        // still has nothing left to ADVANCE, only to keep pushing at its settled colour.
        if (!_fadeActive)
        {
            return;
        }

        _currentR = MoveTowards(_currentR, _targetR, _stepR);
        _currentG = MoveTowards(_currentG, _targetG, _stepG);
        _currentB = MoveTowards(_currentB, _targetB, _stepB);

        if (_currentR == _targetR && _currentG == _targetG && _currentB == _targetB)
        {
            _fadeActive = false;
        }
    }

    /// <summary>
    /// Faithful port of <c>GraphicManager.MoveTowards</c> (GraphicManager.cs:1622-1643): pure addition
    /// plus a STRICT overshoot clamp, no rounding. The branch is chosen by the SIGN OF <paramref name="step"/>
    /// itself, not by the sign of <c>target - value</c> - the "pinned sign" the truncating division can
    /// produce (§1.4/T4): a DESCENDING target (<c>target &lt; value</c>) whose magnitude is smaller than
    /// the duration truncates to <c>step == 0</c>, which is still <c>&gt;= 0</c> - so it takes the
    /// non-negative branch, which finds <c>target &lt; value + 0</c> already true and snaps to
    /// <paramref name="target"/> in exactly ONE tick, instead of never moving. An ASCENDING target with
    /// the same truncated-to-zero step takes the SAME branch but is never "already past" it, so it never
    /// arrives at all - both edges fall out of this one pair of comparisons with no special-casing.
    /// </summary>
    private static int MoveTowards(int value, int target, int step)
    {
        var result = value + step;
        var overshot = step < 0 ? result < target : target < result;
        return overshot ? target : result;
    }

    /// <inheritdoc/>
    public bool IsSettled => !_fadeActive && !_warpActive;

    /// <summary>
    /// Pushes this frame's state to the attached <see cref="ScreenEffectService"/> - a no-op when none is
    /// attached (T2: no <c>Game</c>, no exception). The draw guard itself (GraphicManager.cs:1572-1578):
    /// while machine B is inactive AND the persistence latch is at zero, NOTHING is submitted
    /// (<see cref="ScreenEffectService.Clear"/>) - a persisted, settled tint (<see cref="_persistLock"/>
    /// != 0, T5) keeps submitting its settled colour indefinitely instead.
    /// </summary>
    public void PushToAttachedService()
    {
        if (_service == null)
        {
            return;
        }

        if (!_fadeGateActiveEnteringPush && _persistLock == 0)
        {
            _service.Clear();
            return;
        }

        var r = (byte)(_currentR >> 16);
        var g = (byte)(_currentG >> 16);
        var b = (byte)(_currentB >> 16);
        var blend = _tpage switch
        {
            1 => SpriteBlendMode.Additive,
            2 => SpriteBlendMode.Subtractive,
            _ => SpriteBlendMode.Opaque,
        };

        _service.SetOverlay(r, g, b, blend);
    }

    /// <summary>Test-only: clears every piece of session state so tests do not leak into each other
    /// through this singleton - same seam as <see cref="AlundraMusicPlayer.ResetForTests"/>.</summary>
    internal void ResetForTests()
    {
        _service = null;

        _fadeActive = false;
        _fadeGateActiveEnteringPush = false;
        _persistLock = 0;
        _currentR = _currentG = _currentB = 0;
        _targetR = _targetG = _targetB = 0;
        _stepR = _stepG = _stepB = 0;
        _tpage = 0;

        _warpActive = false;
        _warpCurrentR = _warpCurrentG = _warpCurrentB = 0;
        _warpTargetR = _warpTargetG = _warpTargetB = 0;
        _warpStepR = _warpStepG = _warpStepB = 0;
    }
}
