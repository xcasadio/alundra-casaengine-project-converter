#nullable enable

namespace Alundra.Scripts;

/// <summary>
/// E13 C1 (docs/plan-e13-hud.md, D-E13-8: "le tick logique possède tout le temps du HUD. MGUI
/// dessine."): the session-scoped director that owns every piece of the permanent HUD's ANIMATED
/// state - the open/close machine, the position ordinate, the value catch-up counters, the coin-icon
/// and magic-pip animation phases. One <see cref="Tick"/> call is ONE rendered frame of the original at
/// 50 Hz PAL, exactly as the original's own <c>HudManager.Fun_8004bea4</c> (0x8004bea4, "Affichage hud")
/// ran once per its own 50 Hz main-loop frame.
///
/// <para><b>This director knows nothing about MGUI, <c>IUIScreen</c>, or any presenter</b> - unlike
/// <see cref="AlundraDialogueDirector"/> it has no view seam at all: it exposes a plain, readable state
/// (<see cref="IsDrawn"/>, <see cref="Phase"/>, <see cref="Y"/>, the rolled values, the animation
/// indices, <see cref="FrameCounter"/>) that E13's C2/C3 slices will read from a non-modal MGUI screen.
/// This split exists so the jauge keeps rolling/rattraping while a modal dialogue box is open -
/// <see cref="CasaEngine.Framework.Dialogue.Presentation.IDialoguePresenter"/>'s own screen stack freezes
/// update on every screen BELOW the modal one, and the original's own jauge never freezes for a
/// conversation (docs/plan-e13-hud.md, C1's own "Pourquoi un directeur et pas l'écran").</para>
///
/// <para>SESSION-scoped singleton, same shape as <see cref="AlundraDialogueDirector"/>/
/// <see cref="AlundraScreenFadeDirector"/>: <see cref="AttachToWorld"/> re-points this session's
/// <see cref="AlundraGameState"/> reference WITHOUT touching any animated state;
/// <see cref="InstallForMapEntry"/> is the separate map-entry call, which this class's own doc explains
/// is a deliberate NO-OP (see that method's own doc for the evidence).</para>
/// </summary>
public sealed class AlundraHudDirector
{
    public static readonly AlundraHudDirector Instance = new();

    private AlundraHudDirector()
    {
    }

    // ---- Flag identifiers (AlundraGameState.GetFlag/AddFlag/SetFlag - (word << 5) + bit, both
    // AlundraGameState.cs:194 IndexOf and AlundraGameState.cs:196 BankFor) ----
    //
    // GraphicManager.cs:1646-1673 UpdateUserInterface reads/writes three bits, all in the PERSISTENT
    // bank (id < 0x8000 = AlundraGameState's TemporaryFlagBit):
    //   word 0x38 bit 21 (mask 0x200000) -> id (0x38<<5)+21 = 1813 - script's "please appear" request.
    //   word 0x38 bit 22 (mask 0x400000) -> id (0x38<<5)+22 = 1814 - script's "please hide, now" request.
    //   word 0x33 bit 30 (mask 0x40000000) -> id (0x33<<5)+30 = 1662 - the persistent "already armed" latch.
    // 1813 and 1814 share IndexOf (56 = 0x38, same word) - only their mask differs; 1662 is a separate
    // word (51 = 0x33). All three id < 0x8000, confirmed persistent-bank by AlundraGameState.cs:196.
    private const uint ScriptOpenRequestFlag = 1813;
    private const uint ScriptOpenRequestMask = 0x200000;
    private const uint ScriptCloseRequestFlag = 1814;
    private const uint ScriptCloseRequestMask = 0x400000;
    private const uint PersistentLatchFlag = 1662;
    private const uint PersistentLatchMask = 0x40000000;

    // ---- Position tween (UIManager.UpdateUiBoxesPosition, 0x80047dd0, UIManager.cs:968-993) ----
    //
    // UIBoxHud = { X = 0, Y = 0x10, Width = 0, Height = 5 } (StaticVariables.cs:12270). Both arm
    // functions leave X/startX at 0 always (HudManager.cs:33/35,53/51) - the tween's X axis is
    // degenerate (0 -> 0) for the HUD specifically, so only Y is ported here. mode/speed are the same
    // two constants (2, 0xf) in both arm functions.
    private const int TweenSpeed = 0xf; // 15 - HudManager.cs:32,50.
    private const int OpenTargetY = 0x10; // 16 - HudManager.cs:52 (InitializeHudPositionBeforeHide's startY) and the compiled default of UIBoxHud.Y itself.
    private const int ClosedY = -41; // ~(UIBoxHud.Height << 3) = ~(5<<3) = ~40 = -41 - HudManager.cs:36,54.

    private int _tweenMode; // TextToDisplay.mode - 0 idle, 2 while armed, decremented to 0 across the last two calls.
    private int _tweenTick; // TextToDisplay.tick.
    private int _tweenYStart; // TextToDisplay.y - the tween's fixed source, set once at arm time.
    private int _tweenYTarget; // TextToDisplay.startY - the tween's fixed target, set once at arm time.

    /// <summary>Effect-named phases of <c>g_drawFrameFlags</c> (0x80176310) - the four values plan
    /// §1.2 measured, kept at their original integers (0/5/1/3) so a test can assert them directly and
    /// so <see cref="RunTriggerMachine"/>'s bit bit-tests (<c>&amp; 3</c>, <c>&amp; 6</c>, <c>|= 2</c>,
    /// <c>&amp;= ~4</c>) stay literal, verbatim ports of HudManager.cs/GraphicManager.cs - never named
    /// after <c>InitializeHudPosition</c>/<c>InitializeHudPositionBeforeHide</c> (plan §6 point 2: those
    /// two names are the INVERSE of their effect).</summary>
    public enum HudPhase
    {
        Idle = 0,
        Displayed = 1,
        Closing = 3,
        Opening = 5,
    }

    private AlundraGameState? _gameState;

    public HudPhase Phase { get; private set; } = HudPhase.Idle;

    /// <summary>True while the jauge is drawn at all (any phase but <see cref="HudPhase.Idle"/>) -
    /// mirrors <c>Fun_8004bea4</c>'s own outer guard, <c>g_drawFrameFlags != 0</c> (HudManager.cs:223).</summary>
    public bool IsDrawn => Phase != HudPhase.Idle;

    /// <summary>Current Y ordinate of the jauge's box (the tween's live output) - defaults to
    /// <see cref="OpenTargetY"/>, the compiled default of <c>UIBoxHud.Y</c> before any transition has
    /// ever run (StaticVariables.cs:12270), and therefore irrelevant while <see cref="IsDrawn"/> is
    /// false.</summary>
    public int Y { get; private set; } = OpenTargetY;

    /// <summary>Port of <c>INT_800a827c</c> (StaticVariables.cs:12276) - increments once per
    /// <see cref="Tick"/> call where the jauge <see cref="IsDrawn"/>, and drives every cadence below
    /// (HudManager.cs:319, inside the same outer guard as everything this class ports).</summary>
    public int FrameCounter { get; private set; }

    // ---- Rolled/displayed values - g_playerDataHud[0..9] (StaticVariables.cs:12278-12282) ----
    public int Hp { get; private set; } = 10; // [0] - New-Game-equivalent default, mirrors AlundraPlayerStats.Hp's own.
    public int HpMax { get; private set; } = 10; // [1]
    public int Mp { get; private set; } // [2]
    public int MpMax { get; private set; } // [3]
    public int Money { get; private set; } // [4]
    private int _hpSubStep; // [5]
    private int _hpMaxSubStep; // [6]
    private int _mpSubStep; // [7]
    private int _mpMaxSubStep; // [8]

    /// <summary>Coin icon's animation frame (0..3) - [9]. Frozen at 0 while the money roll is settled
    /// (HudManager.cs:462), the "garée" behaviour the mission cites.</summary>
    public int CoinIconFrame { get; private set; }

    /// <summary>Per-pip animation frame index (four entries, one per magic pip) - HudManager.cs:634-646
    /// (<c>DisplayMp</c>'s own animated-icon computation). Read-only view over the internal array so a
    /// caller cannot mutate this director's state through it.</summary>
    public System.Collections.Generic.IReadOnlyList<int> MagicPipFrame => _magicPipFrame;
    private readonly int[] _magicPipFrame = new int[4];

    /// <summary>
    /// Plan §6 point 4 (OPEN QUESTION, not this slice's to close): the ACTIVE line
    /// (HudManager.cs:634-646) makes every pip share the exact same frame - the reconstruction replaced
    /// two commented indirections through <c>g_inventoryWeaponIconX</c>/<c>BYTE_ARRAY_800a3238</c> with
    /// a single computation that drops the per-pip term entirely. This table is that unison, ported as
    /// the mission requires ("par défaut... l'unisson").
    ///
    /// <b>Candidate NOT ported</b> (documentation only, no code path reads it): <c>{ 0, 1, 2, 3 }</c>, a
    /// rotation deduced by reading <c>g_inventoryWeaponIconX</c>'s first 16 bytes little-endian
    /// (StaticVariables.cs:12272-12275, values <c>0x100, 0x302, 0x201, 0x3, 0x302, 0x100, 0x3, 0x201</c>
    /// -> bytes <c>00,01,02,03 / 01,02,03,00 / 02,03,00,01 / 03,00,01,02</c>) - a byte-level reading of a
    /// <c>short[]</c>, i.e. a DEDUCTION, not a read of active code. Only the author, from the raw
    /// disassembly, can decide between the two - C3 fills this table only after that answer (plan §6
    /// point 4).
    /// </summary>
    private static readonly int[] MagicPipPhaseOffset = { 0, 0, 0, 0 };

    /// <summary>Re-points this session-scoped instance at the current world's own
    /// <see cref="AlundraGameState"/> - same "re-point without touching state" contract as
    /// <see cref="AlundraDialogueDirector.AttachToWorld"/>/<see cref="AlundraScreenFadeDirector.AttachToWorld"/>.
    /// <paramref name="gameState"/> null is tolerated (no live session yet) - <see cref="Tick"/> then
    /// degrades to a no-op (<see cref="RunTriggerMachine"/>'s own null guard), same shape as every other
    /// missing-system seam in this DLL.</summary>
    public void AttachToWorld(AlundraGameState? gameState)
    {
        _gameState = gameState;
    }

    /// <summary>
    /// Map-entry hook, called from <see cref="AlundraWorldProxy"/>'s own per-map install pass for
    /// symmetry with every other session-scoped director (<see cref="AlundraDialogueDirector.InstallForMapEntry"/>,
    /// <see cref="AlundraScreenFadeDirector.InstallForMapEntry"/>) - deliberately a NO-OP here.
    ///
    /// <b>Evidence checked (mission item 5), none found</b>: <c>g_drawFrameFlags</c> (0x80176310),
    /// <c>INT_800a827c</c> (0x800A827C) and <c>g_playerDataHud</c> (0x800A8284) are plain top-level
    /// globals, never members of <c>g_saveData</c> - no map-load routine in this repository's own
    /// decompilation writes any of them (the only two writers of the animated state are
    /// <c>HudManager.InitializeHpAndMp</c>/<c>FUN_8004b770</c>, both cited on <see cref="ArmAppearance"/>'s
    /// own doc, and neither runs from a map-transition call site - only from the script-driven "please
    /// appear" trigger, branch (ii) of <see cref="RunTriggerMachine"/>). Exactly like
    /// <see cref="AlundraGameState.PlayerStats"/> (E13 C0), this animated state survives a map change.
    /// </summary>
    public void InstallForMapEntry()
    {
        // Deliberately empty - see this method's own doc.
    }

    /// <summary>
    /// Test-only: restores this session singleton to construction-equivalent state, same seam as every
    /// other session-scoped director in this DLL.
    /// </summary>
    internal void ResetForTests()
    {
        _gameState = null;
        Phase = HudPhase.Idle;
        Y = OpenTargetY;
        _tweenMode = 0;
        _tweenTick = 0;
        _tweenYStart = 0;
        _tweenYTarget = 0;
        FrameCounter = 0;
        Hp = 10;
        HpMax = 10;
        Mp = 0;
        MpMax = 0;
        Money = 0;
        _hpSubStep = 0;
        _hpMaxSubStep = 0;
        _mpSubStep = 0;
        _mpMaxSubStep = 0;
        CoinIconFrame = 0;
        System.Array.Clear(_magicPipFrame, 0, _magicPipFrame.Length);
    }

    /// <summary>
    /// One rendered frame of the original's own <c>HudManager.Fun_8004bea4</c> (0x8004bea4), fed by
    /// <c>GraphicManager.UpdateUserInterface</c>'s (0x80048054) own trigger machine which runs strictly
    /// BEFORE it every original frame too (GraphicManager.cs:1646-1689 runs its three branches, THEN the
    /// callback-table loop that invokes the render callback registered at index 1 - plan §1.1). Called
    /// once per LOGIC tick from <see cref="AlundraWorldProxy.Update"/>'s own <c>ticksThisFrame</c> loop -
    /// D-E13-8, the tick logique owns all of the HUD's time.
    /// </summary>
    public void Tick()
    {
        RunTriggerMachine();

        // HudManager.cs:223 - "if (g_drawFrameFlags != 0)" is read ONCE, here, exactly like the
        // original's own C decompiled to a single `if` test at function entry: a mutation made further
        // down THIS SAME call (the closing collapse inside AdvancePositionTween's caller, below) does
        // NOT retroactively skip the rest of this method - only branch (iii)'s INSTANT reset (which runs
        // inside RunTriggerMachine, i.e. BEFORE this read) can make wasActive false on its own tick. This
        // asymmetry is the mission's own "ne suppose aucune symétrie" note.
        var wasActive = Phase != HudPhase.Idle;
        if (!wasActive)
        {
            return;
        }

        // HudManager.cs:225 - "(g_drawFrameFlags & 6) != 0" - true only for Opening(5)/Closing(3), never
        // Displayed(1) (1 & 6 == 0): the jauge sits still, un-tweened, while merely displayed.
        if (Phase == HudPhase.Opening || Phase == HudPhase.Closing)
        {
            if (AdvancePositionTween())
            {
                // HudManager.cs:229-239 - "value == 1": Closing(3, bit1 set) collapses to Idle(0);
                // Opening(5, bit2 set) collapses to Displayed(1). Never both - the two states test
                // disjoint bits (3 & 4 == 0, 5 & 2 == 0).
                Phase = Phase == HudPhase.Closing ? HudPhase.Idle : HudPhase.Displayed;
            }
        }

        // HudManager.cs:319-324 - INT_800a827c += 1, then every Display* call. Only the state those
        // calls read/write is ported (D-E13-1: DisplayHudWeaponAndItem's own case/item bars are out of
        // scope).
        FrameCounter++;
        UpdateMagicPipPhase();
        UpdateHpAndMpCatchUp();
        UpdateMoneyRoll();
    }

    /// <summary>
    /// Port of <c>GraphicManager.UpdateUserInterface</c>'s three HUD branches (GraphicManager.cs:1656-1673,
    /// verbatim order) - the ONLY consumer this DLL was missing (plan §1.6: every opcode that WRITES
    /// these three flags already exists). Reads/writes flags through <see cref="AlundraGameState.GetFlag"/>/
    /// <see cref="AlundraGameState.AddFlag"/>/<see cref="AlundraGameState.SetFlag"/> - never the raw
    /// arrays, same discipline as every opcode dispatch in this DLL.
    /// </summary>
    private void RunTriggerMachine()
    {
        if (_gameState == null)
        {
            return; // tolerated, same degraded shape as every missing-system seam in this DLL.
        }

        // Branch (i), GraphicManager.cs:1656-1659: rearmed every tick the persistent latch is OFF.
        if ((_gameState.GetFlag(PersistentLatchFlag) & PersistentLatchMask) == 0)
        {
            ArmDisappearance();
        }

        // Branch (ii), GraphicManager.cs:1661-1666: the script's "please appear" request.
        if ((_gameState.GetFlag(ScriptOpenRequestFlag) & ScriptOpenRequestMask) != 0)
        {
            _gameState.SetFlag(ScriptOpenRequestFlag, ~ScriptOpenRequestMask);
            _gameState.AddFlag(PersistentLatchFlag, PersistentLatchMask);
            ArmAppearance();
        }

        // Branch (iii), GraphicManager.cs:1668-1673: the script's "hide instantly, no animation" request -
        // ResetDrawFrameFlags (0x8004be00) is a straight assignment to 0, not a tween arm (see this
        // block's own doc on Tick, and the mission's own "ATTENTION" note: this is NOT the animated
        // close, branch (i) is).
        if ((_gameState.GetFlag(ScriptCloseRequestFlag) & ScriptCloseRequestMask) != 0)
        {
            Phase = HudPhase.Idle;
            _gameState.SetFlag(PersistentLatchFlag, ~PersistentLatchMask);
            _gameState.SetFlag(ScriptCloseRequestFlag, ~ScriptCloseRequestMask);
        }
    }

    /// <summary>
    /// Port of <c>HudManager.InitializeHudPosition</c> (0x8004bd9c, HudManager.cs:26-38). Named by
    /// EFFECT (plan §6 point 2): despite its original name this ARMS THE DISAPPEARANCE - guard
    /// <c>(g_drawFrameFlags &amp; 3) == 1</c>, tween from the current visible ordinate
    /// (<see cref="OpenTargetY"/>) to <see cref="ClosedY"/>, then <c>g_drawFrameFlags |= 2</c>
    /// (HudManager.cs:37) - literal OR against whatever <see cref="Phase"/> already holds, ported
    /// verbatim rather than "corrected": in practice this only ever runs while <see cref="Phase"/> is
    /// <see cref="HudPhase.Displayed"/> (1 | 2 = 3, Closing) because <see cref="RunTriggerMachine"/>'s
    /// own branch (i) only calls this while the persistent latch is OFF, and the latch is exactly what
    /// <see cref="ArmAppearance"/> sets the instant it arms <see cref="HudPhase.Opening"/> - so the guard
    /// admitting <see cref="HudPhase.Opening"/> too (5 &amp; 3 == 1) is a dead path here, not a bug this
    /// port should paper over (mission: "ne suppose aucune symétrie").
    /// </summary>
    private void ArmDisappearance()
    {
        if (((int)Phase & 3) != 1)
        {
            return;
        }

        _tweenMode = 2;
        _tweenTick = 0;
        _tweenYStart = OpenTargetY;
        _tweenYTarget = ClosedY;
        Y = OpenTargetY;
        Phase = (HudPhase)((int)Phase | 2);
    }

    /// <summary>
    /// Port of <c>HudManager.InitializeHudPositionBeforeHide</c> (0x8004be0c, HudManager.cs:41-56).
    /// Named by EFFECT: despite its original name this ARMS THE APPEARANCE - guard
    /// <c>g_drawFrameFlags == 0</c> (the companion guard, <c>GameFlags[0x33] &amp; 0x40000000 != 0</c>,
    /// is guaranteed by <see cref="RunTriggerMachine"/>'s own call order: the persistent latch is set
    /// the line right before this runs, branch (ii)), tween from <see cref="ClosedY"/> to
    /// <see cref="OpenTargetY"/>, <c>g_drawFrameFlags = 0x5</c> (assignment, not OR - HudManager.cs:55).
    ///
    /// Also ports the ONE side effect of <c>FUN_8004b770</c> (0x8004b770, "init hud" - the render
    /// callback <c>SetTransitionType(1)</c> arms, HudManager.cs:66-67, plan §1.1) this director needs:
    /// refresh the displayed-max trackers to the true current max. Mission item 5's own check: this is
    /// the ONLY reset the original performs at arming - it does NOT touch the rolled current values or
    /// their sub-step counters (contrast <c>InitializeHpAndMp</c>, HudManager.cs:17-22, a FULL reset to
    /// true-max used only by New Game init, out of this director's scope - <see cref="AlundraPlayerStats"/>
    /// already starts at 10/10/0/0/0, C0).
    /// </summary>
    private void ArmAppearance()
    {
        if (Phase != HudPhase.Idle)
        {
            return;
        }

        if (_gameState != null)
        {
            HpMax = _gameState.PlayerStats.HpMax;
            MpMax = _gameState.PlayerStats.MpMax;
        }

        _tweenMode = 2;
        _tweenTick = 0;
        _tweenYStart = ClosedY;
        _tweenYTarget = OpenTargetY;
        Y = ClosedY;
        Phase = HudPhase.Opening;
    }

    /// <summary>
    /// Port of <c>UIManager.UpdateUiBoxesPosition</c> (0x80047dd0, UIManager.cs:968-993), Y axis only
    /// (see the tween fields' own doc for why X is degenerate for this box). Integer arithmetic,
    /// division truncating TOWARD ZERO - C#'s own <c>int</c> division does this natively, matching the
    /// original. Returns true exactly when <c>mode == 0</c> was ALREADY true on entry (UIManager.cs:975
    /// - the original's own <c>return 1</c> guard), i.e. two calls after the interpolation reached its
    /// target (one call sets the target and decrements mode 2 -&gt; 1, the NEXT call re-sets the same
    /// target and decrements 1 -&gt; 0, only the call AFTER THAT returns true) - the plan's own "atteinte
    /// au 16e appel et tenue deux appels de plus".
    /// </summary>
    private bool AdvancePositionTween()
    {
        if (_tweenMode == 0)
        {
            return true;
        }

        if (_tweenTick == TweenSpeed)
        {
            Y = _tweenYTarget;
            _tweenMode -= 1;
        }
        else
        {
            Y = _tweenYStart + (_tweenYTarget - _tweenYStart) * _tweenTick / TweenSpeed;
            _tweenTick += 1;
        }

        return false;
    }

    /// <summary>Port of the pip-frame computation inside <c>HudManager.DisplayMp</c> (HudManager.cs:634-646),
    /// run unconditionally every active tick (no cadence gate of its own - the cadence comes purely from
    /// the <c>/ 10</c>). <c>FrameCounter</c> is never negative, so the original's <c>if (iVar1 &lt; 0)
    /// iVar4 = iVar1 + 3</c> correction (the Ghidra signed-modulo idiom) never fires here and is not
    /// ported - see <see cref="MagicPipPhaseOffset"/>'s own doc for the per-pip open question.</summary>
    private void UpdateMagicPipPhase()
    {
        var phase = (FrameCounter / 10) % 4;
        for (var pip = 0; pip < _magicPipFrame.Length; pip++)
        {
            _magicPipFrame[pip] = (phase + MagicPipPhaseOffset[pip]) % 4;
        }
    }

    /// <summary>
    /// Port of the Hp/Mp catch-up block (HudManager.cs:325-434), gated by <c>(INT_800a827c &amp; 1) ==
    /// 0</c> (HudManager.cs:328): 1 sub-step every OTHER active tick, 4 sub-steps per point (the [5]/[7]
    /// cycle 0..3), so 1 point every 8 active ticks - the mission's own "rattrapage vie et magie 1 point
    /// / 8 images". A SECOND, independent sub-step cycle ([6]/[8]) grows the displayed max tracker
    /// itself toward the true max whenever the displayed current value has already caught up to it
    /// (HudManager.cs:367-379,421-433) - same 8-tick cadence, since it shares the same outer gate.
    /// <c>SoundEffect(8)</c>/<c>SoundEffect(9)</c> (HudManager.cs:364,413) are not ported - no audio hook
    /// is exposed by this director (D-E13-1: out of C1's scope).
    /// </summary>
    private void UpdateHpAndMpCatchUp()
    {
        if (_gameState == null || (FrameCounter & 1) != 0)
        {
            return;
        }

        var trueHpMax = _gameState.PlayerStats.HpMax;
        int hpTarget = _gameState.PlayerStats.Hp;
        if (HpMax < hpTarget)
        {
            hpTarget = HpMax;
        }

        if (hpTarget < Hp || (Hp == hpTarget && _hpSubStep != 0))
        {
            if (_hpSubStep == 0)
            {
                Hp -= 1;
            }

            _hpSubStep += 1;
            if (_hpSubStep > 3)
            {
                _hpSubStep = 0;
            }
        }
        else if (Hp < hpTarget)
        {
            _hpSubStep -= 1;
            if (_hpSubStep < 0)
            {
                _hpSubStep = 3;
            }
            else if (_hpSubStep == 0)
            {
                Hp += 1;
            }
        }
        else if (HpMax < trueHpMax)
        {
            _hpMaxSubStep -= 1;
            if (_hpMaxSubStep < 0)
            {
                _hpMaxSubStep = 3;
            }
            else if (_hpMaxSubStep == 0)
            {
                Hp = HpMax + 1;
                HpMax += 1;
            }
        }

        var trueMpMax = _gameState.PlayerStats.MpMax;
        int mpTarget = _gameState.PlayerStats.Mp;
        if (MpMax < mpTarget)
        {
            mpTarget = MpMax;
        }

        if (mpTarget < Mp || (Mp == mpTarget && _mpSubStep != 0))
        {
            if (_mpSubStep == 0)
            {
                Mp -= 1;
            }

            _mpSubStep += 1;
            if (_mpSubStep > 3)
            {
                _mpSubStep = 0;
            }
        }
        else if (Mp < mpTarget)
        {
            _mpSubStep -= 1;
            if (_mpSubStep < 0)
            {
                _mpSubStep = 3;
            }
            else if (_mpSubStep == 0)
            {
                Mp += 1;
            }
        }
        else if (MpMax < trueMpMax)
        {
            _mpMaxSubStep -= 1;
            if (_mpMaxSubStep < 0)
            {
                _mpMaxSubStep = 3;
            }
            else if (_mpMaxSubStep == 0)
            {
                Mp = MpMax + 1;
                MpMax += 1;
            }
        }
    }

    /// <summary>
    /// Port of the money-roll block (HudManager.cs:438-486), UNCONDITIONAL every active tick (no
    /// <c>&amp; 1</c> gate, unlike the Hp/Mp catch-up above): +-10 then +-1 toward the true target, and
    /// the coin icon's frame ([9]) advances 1 every 6 active ticks (<c>INT_800a827c % 6 == 0</c>,
    /// HudManager.cs:478) ONLY while the roll is not settled - the settled branch (HudManager.cs:460-464)
    /// returns immediately with the frame garaged at 0, before that gate is ever reached.
    /// </summary>
    private void UpdateMoneyRoll()
    {
        if (_gameState == null)
        {
            return;
        }

        var target = (int)_gameState.PlayerStats.Money;

        if (target < Money || (Money == target && CoinIconFrame != 0))
        {
            var minusTen = Money - 10;
            if (target < minusTen)
            {
                Money -= 10;
            }
            else if (target < Money)
            {
                Money -= 1;
            }
        }
        else
        {
            if (target <= Money)
            {
                CoinIconFrame = 0;
                return;
            }

            var plusTen = Money + 10;
            if (plusTen < target)
            {
                Money += 10;
            }
            else
            {
                Money += 1;
            }
        }

        if (FrameCounter % 6 == 0)
        {
            CoinIconFrame += 1;
            if (CoinIconFrame > 3)
            {
                CoinIconFrame = 0;
            }
        }
    }
}
