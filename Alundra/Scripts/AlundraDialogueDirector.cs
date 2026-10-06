#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.Yarn;

namespace Alundra.Scripts;

/// <summary>
/// Opcode-facing seam over <see cref="AlundraDialogueDirector"/> (docs/plan-e12-dialogues.md, slice
/// E12.a, D-E12-5: "the moteur is MECHANISM, the DLL is POLICY" - the engine's own
/// <see cref="IDialoguePresenter"/> knows nothing about the close-mode mask, numeric control-code flags,
/// paging on <c>\A</c>, or <see cref="AlundraGameState.PlayerControlFlags"/>; all of that lives here).
/// Backs opcodes 0x0D/0x39/0x44/0x4C-0x51/0x5C in <see cref="AlundraEventProgramRunner.Dispatch"/> via
/// <see cref="IEntityWorldContext.DialogueDirector"/> - a default interface member (same shape as
/// <see cref="IEntityWorldContext.ScreenFadeDirector"/>), so every EXISTING implementer keeps compiling
/// unmodified, degrading to null (skip-by-size, once-logged) exactly like that seam.
/// </summary>
public interface IAlundraDialogueDirector
{
    /// <summary>True once a real <see cref="IDialoguePresenter"/> has been attached
    /// (<see cref="AlundraDialogueDirector.AttachToWorld"/>) - the actual real/degraded switch every
    /// opcode case in <see cref="AlundraEventProgramRunner.Dispatch"/> tests (NOT whether this interface
    /// member itself is null - see that interface member's own doc).</summary>
    bool HasPresenter { get; }

    /// <summary>Port of the original's own "is a dialog box currently open" state (<c>g_dialog_flags &amp; 4</c>) - true from a
    /// successful <see cref="Open"/> until the box is RELEASED, 18 passes after its close was triggered (E19.f2a, F2-R2): the
    /// slide of the exit is part of the open box, and opcode 0x39 waits for the release.</summary>
    bool IsOpen { get; }

    /// <summary>True while a choice list (opcode 0x44) is open and unresolved.</summary>
    bool IsAwaitingChoice { get; }

    /// <summary>
    /// Opcode 0x0D/0x5C/0xC4's own "open" half (Dispatch itself owns the reentrancy guard - see that method's
    /// own doc on why 0x0D checks <see cref="IsOpen"/> BEFORE calling this, T2): resets the box to the state of
    /// <c>InitializeDialogMessage</c> (text flags 3, close mode 3, scroll mode 3, the latches off), applies
    /// <paramref name="controlMode"/>'s <see cref="AlundraGameState.PlayerControlBits.MessageBox"/> (1) or
    /// <see cref="AlundraGameState.PlayerControlBits.MenuOpen"/> (anything else) bit, plays sound 6, and starts
    /// <paramref name="node"/> of <paramref name="asset"/> on this director's own Yarn runner (docs/plan-e15-yarn.md, E15.c
    /// contract item 2), whose first page is cut into the steps of the box (E19.f2a, F2-R3): the box types it one step at a time
    /// from its own passes. The page's commands/functions run when the runner delivers it. Either
    /// <paramref name="asset"/> null, <paramref name="node"/> null, or a node absent from the asset opens
    /// an empty box instead (D-E15-10), exactly as an out-of-range/never-loaded local string did before.
    /// </summary>
    void Open(DialogueAsset? asset, string? node, int controlMode);

    /// <summary>Opcode 0x4C - sets the text flags (1 the held button gates a step, 2 the delay does, 4 a 0x4D does, 8 waits for the cursor's press) and
    /// clears a 0x4D in waiting (D-E19-62). Gives the hand back to nobody.</summary>
    void SetTextFlags(int flags);

    /// <summary>Opcode 0x4D - latches one step of the typing, when the text flags say a latch gates (bit 4).</summary>
    void LatchTextStep();

    /// <summary>Opcode 0x4E - sets the scroll mode (1 waits ten passes, 2 a press, 4 a 0x4F, 8 starts at once).</summary>
    void SetScrollMode(int mode);

    /// <summary>Opcode 0x4F - latches the start of a scroll, when the scroll mode says a latch starts it (bit 4).</summary>
    void LatchScrollStart();

    /// <summary>Opcode 0x50 - sets the close-mode mask (bit0 auto-timer/bit1 button/bit2 script, §1.2).</summary>
    void SetCloseMask(int mask);

    /// <summary>Opcode 0x51 - latches the close of the box, when the close mode's bit2 (script-close) is set (§1.2); the box takes the latch once its
    /// typing is done, so the close triggers at the first pass after the end of the typing that sees it. Returns whether the latch was set, though
    /// the opcode itself writes no <c>Result</c> either way (see that opcode's own dispatch doc).</summary>
    bool RequestScriptClose();

    /// <summary>One pass of the box for a host that has no world proxy to run it (the unit tests): the pass takes the square button of the PREVIOUS
    /// call - the binary's box reads the pad sampled during the previous frame - then this call records the square of
    /// <see cref="AlundraGameState.LastPadState"/> (<c>ButtonsHold</c>, the rising edge being taken against the previous call). The world proxy
    /// does not call this: it calls <see cref="AlundraDialogueDirector.Pass"/> itself, on the square of its own pad pass (F2-R1).</summary>
    void Tick();

    /// <summary>Opcode 0x44's own first-entry half: opens a generic choice list (labels already resolved
    /// by the caller - <see cref="AlundraEtcStringTable"/> for the OUI/NON pair, D-E12-6) through the
    /// attached presenter and starts waiting for <see cref="TakeChoiceResult"/> to report a selection.</summary>
    void OpenChoice(IReadOnlyList<string> labels);

    /// <summary>Opcode 0x44's own polling half: <see langword="null"/> while no selection has been made
    /// yet (still <see cref="IsAwaitingChoice"/>) - the caller returns 0 (suspend) in that case, exactly
    /// like <see cref="IsOpen"/>'s own gate. Once a selection lands, returns 1 iff it was the FIRST option
    /// (§1.3's own <c>Result = 1 ssi la PREMIÈRE option</c>) and clears the awaiting state so a later call
    /// does not re-report the same selection.</summary>
    int? TakeChoiceResult();
}

/// <summary>
/// SESSION-scoped singleton (same shape as <see cref="AlundraMusicPlayer"/>/<see cref="AlundraScreenFadeDirector"/>
/// - see either class's own doc for the full "vacuous by construction" reasoning, D-C-6/D-E10-6): the
/// original's own <c>g_dialog_flags</c>/close-mode mask/choice result are GLOBALS that survive a map
/// change, and a per-world instance rebuilt in <see cref="AlundraWorldProxy.InitializeWithWorld"/> would
/// make that survival vacuous by construction. <see cref="AttachToWorld"/> re-points this session's
/// presenter/game-state references WITHOUT touching open/mask/page state (same contract as
/// <see cref="AlundraMusicPlayer.AttachToWorld"/>); <see cref="InstallForMapEntry"/> is the SEPARATE call
/// that actually resets that state, from <see cref="AlundraWorldProxy.InstallDialogueSystems"/>'s own
/// install preamble (docs/plan-e12-dialogues.md, "AttachToWorld re-points without touching state,
/// map-entry reset in the install preamble").
/// <para/>
/// E19.f2a (docs/plan-e19-opcodes.md section 1.2j.3, F2-R1 to F2-R6, ADR-0029): the box is the binary's, to the tick. The director owns an
/// <see cref="AlundraDialogueBox"/> (the machine: slides, typing steps, lines and scroll, cursor, voices, close and release) and feeds it the
/// pages the Yarn runner delivers, cut into steps from their text and markers; the world proxy runs <see cref="Pass"/> once per logic tick
/// BEFORE the scripts of that tick. The box is drawn by the text box screen (E19.f2b1c, <see cref="AlundraTextBoxPresenter"/> reads the box after each pass); the
/// engine's presenter is only the choices' window (until E19.f3). The text flags of a page are set at their glyph, not at the display of the page.
/// </summary>
public sealed class AlundraDialogueDirector : IAlundraDialogueDirector, IAlundraDialogueBoxHost
{
    /// <summary>The one session-scoped instance every <see cref="AlundraWorldProxy"/> shares.</summary>
    public static readonly AlundraDialogueDirector Instance = new();

    private const int DefaultCloseMask = 3; // §1.2 - bit0 (auto-timer) | bit1 (button), the original's default g_etcAnimationMode.
    private const uint SquareBit = AlundraPadState.Square; // §1.2/D-E12-4: bit 0x80.

    private AlundraDialogueDirector()
    {
        _box = new AlundraDialogueBox(this);
    }

    private readonly AlundraDialogueBox _box;
    private IDialoguePresenter? _presenter;
    private AlundraGameState? _gameState;
    private IAlundraSoundPlayer? _soundPlayer;

    // E15.c T5 (docs/plan-e15-yarn.md, contract item 2): this director's own Yarn runner, built on a
    // capture presenter (AlundraDialogueCapturePresenter) - NEVER built directly on _presenter, which is
    // reattached on every AttachToWorld while these two persist. The capture presenter hands each page the runner delivers to
    // OnPageShown (E19.f2a); _presenter only gets the choices. Rebuilt only when the game state
    // actually changes (a new world/map - InstallForMapEntry resets open/page state right after anyway);
    // a same-game-state re-point (TryWireDialoguePresenterOnce's "presenter appears later this frame")
    // just re-points the capture presenter's own WorldPresenter, keeping any dialogue already running.
    private AlundraDialogueCapturePresenter? _capturePresenter;
    private YarnDialogueRunner? _runner;
    private AlundraGameState? _boundGameState;

    private int _pageCount;
    private int _pagesDelivered;
    private int _pageIndex;
    private DialogueLine _pageLine = DialogueLine.Empty;

    // The square button of the previous call of Tick (the seam of the hosts without a world proxy).
    private bool _tickSquareHeld;
    private bool _tickSquarePressed;

    private bool _awaitingChoice;
    private int? _pendingChoiceResult;
    private EventHandler<DialogueChoiceSelectedEventArgs>? _choiceHandler;

    public bool HasPresenter => _presenter != null;
    public bool IsOpen => _box.IsActive;
    public bool IsAwaitingChoice => _awaitingChoice;

    /// <summary>
    /// E16.e L4 (docs/plan-e16-etat-partie.md, SE1): how many times <see cref="Open"/> ran this session - a caller
    /// that opened a box remembers the value right after its own <see cref="Open"/>, and knows the box is still its
    /// own while the value has not changed (the save book closes only the box it opened). Never reset, not even by
    /// <see cref="InstallForMapEntry"/>, so a remembered value can never match a later box by accident.
    /// </summary>
    public int OpenSerial { get; private set; }

    /// <summary>The box the world draws: its state is read by the view (E19.f2b) and by the tests; only this director writes it.</summary>
    internal AlundraDialogueBox Box => _box;

    /// <summary>Test-only seam (F2B1B-R1): the advance of a glyph, in place of the production reader of <c>UI/font3.fnt</c>; set and put back by the test (<c>try</c>/<c>finally</c>).</summary>
    internal Func<char, int>? AdvanceProviderForTests { get; set; }

    /// <summary>Re-points this session-scoped instance at the current world's own presenter/game state/sound player -
    /// called by <see cref="AlundraWorldProxy.InstallDialogueSystems"/> on every world install. Deliberately
    /// does NOT touch the box, the page state or the choice state (same
    /// contract as <see cref="AlundraMusicPlayer.AttachToWorld"/>/<see cref="AlundraScreenFadeDirector.AttachToWorld"/>)
    /// - only <see cref="InstallForMapEntry"/> does that: the box goes on running, its page already known, even when the
    /// presenter is gone (E19.f2a, F2-R2). <paramref name="presenter"/> null is a valid,
    /// tolerated value (no UI view available for this world's active render view) - <see cref="HasPresenter"/>
    /// then drives every opcode's own degraded fallback. <paramref name="soundPlayer"/> plays the sounds of the box (6 at the opening, 7 at the close,
    /// 79 to 82 for the voices, F2-R5).</summary>
    public void AttachToWorld(IDialoguePresenter? presenter, AlundraGameState? gameState, IAlundraSoundPlayer? soundPlayer = null)
    {
        _presenter = presenter;
        _gameState = gameState;
        _soundPlayer = soundPlayer;

        if (presenter == null || gameState == null)
        {
            _capturePresenter = null;
            _runner = null;
            _boundGameState = null;
            return;
        }

        if (_capturePresenter != null && ReferenceEquals(_boundGameState, gameState))
        {
            // Same world's game state, a presenter appearing/changing this frame (production's own
            // TryWireDialoguePresenterOnce retry): re-point the capture presenter's target only, so any
            // dialogue already running on _runner keeps its Yarn state (docs/plan-e15-yarn.md, contract
            // item 2 - "présentateur du monde rebranché à chaque rattachement", never the runner itself).
            _capturePresenter.WorldPresenter = presenter;
            return;
        }

        // First attach, or a genuinely different game state (a new world/map) - InstallForMapEntry runs
        // right after this from the same install call and resets open/page state anyway, so there is no
        // Yarn state worth preserving across this rebuild.
        _capturePresenter = new AlundraDialogueCapturePresenter(presenter) { LineSink = OnPageShown };
        _runner = new YarnDialogueRunner(_capturePresenter) { VariableStorage = new AlundraYarnVariableStorage(gameState) };
        new AlundraYarnBindings(gameState).Register(_runner);
        _boundGameState = gameState;
    }

    /// <summary>Map-entry reset (mirrors <see cref="AlundraScreenFadeDirector.InstallForMapEntry"/>'s own
    /// contract): closes out any dialogue this session still thought was open (clearing
    /// <see cref="AlundraGameState.PlayerControlFlags"/>'s MessageBox/MenuOpen bits so a stale lock never
    /// survives a map transition) and resets every piece of open/mask/page/choice state to New-Game-
    /// equivalent. Called from <see cref="AlundraWorldProxy.InstallDialogueSystems"/>, right after
    /// <see cref="AttachToWorld"/> - the ONLY call site (same M16 lesson as every other install method in
    /// this DLL: no separate, independently deletable call site).</summary>
    public void InstallForMapEntry()
    {
        if (_box.IsActive)
        {
            ClearControlFlags();
        }

        UnsubscribeChoiceHandler();

        _box.Reset();
        _pageCount = 0;
        _pagesDelivered = 0;
        _pageIndex = 0;
        _pageLine = DialogueLine.Empty;
        _awaitingChoice = false;
        _pendingChoiceResult = null;
        _tickSquareHeld = false;
        _tickSquarePressed = false;
        _runner?.Stop();
        _presenter?.Close(); // the runner's own close is swallowed by the capture presenter: the presenter of the world is closed here
    }

    /// <inheritdoc/>
    public void Open(DialogueAsset? asset, string? node, int controlMode)
    {
        OpenSerial++; // E16.e L4: see the property's own doc.
        _pageIndex = 0;
        _pagesDelivered = 0;
        _pageLine = DialogueLine.Empty;
        _awaitingChoice = false;
        _pendingChoiceResult = null;

        _box.Open(); // sound 6 and the reset of the text state: the box is open from this tick, its first pass is the next one
        ApplyControlMode(controlMode);

        var pageCount = asset != null && node != null ? CountPages(asset, node) : 0;
        if (pageCount > 0 && _runner != null)
        {
            _pageCount = pageCount;
            _runner.Start(asset!, node!); // delivers page 0 - its own commands/functions run right now.
        }
        else
        {
            // D-E15-10: absent node/asset (or no runner attached at all) opens an empty box, same as an
            // out-of-range/never-loaded local string did before E15.c.
            _pageCount = 1;
        }
    }

    /// <summary>
    /// docs/plan-e15-yarn.md, E15.c contract item 2: "le nombre de pages est celui des identifiants
    /// <c>line:{nœud}_p{k}</c> de l'asset" - counts the contiguous <c>line:{node}_p0</c>,
    /// <c>line:{node}_p1</c>... keys E15.b's emitter guarantees are deterministic and gap-free, without
    /// scanning every key of the asset.
    /// </summary>
    private static int CountPages(DialogueAsset asset, string node)
    {
        var count = 0;
        while (asset.LineTexts.ContainsKey($"line:{node}_p{count}"))
        {
            count++;
        }

        return count;
    }

    private void ApplyControlMode(int controlMode)
    {
        if (_gameState == null)
        {
            return;
        }

        // The binary: MessageBox (0x10, player frozen, the scripts keep ticking) for 1, MenuOpen (0x08, map events and world updates pause too) otherwise.
        _gameState.PlayerControlFlags |= controlMode == 1
            ? AlundraGameState.PlayerControlBits.MessageBox
            : AlundraGameState.PlayerControlBits.MenuOpen;
    }

    private void ClearControlFlags()
    {
        if (_gameState == null)
        {
            return;
        }

        // §1.2: "close clears both" - unconditionally, regardless of which one open actually set.
        _gameState.PlayerControlFlags &= ~(AlundraGameState.PlayerControlBits.MessageBox | AlundraGameState.PlayerControlBits.MenuOpen);
    }

    // ---- the page: the runner delivers it, the box types it

    /// <summary>Called by the capture presenter each time the runner delivers a page (the first at <see cref="Open"/>, the next at the release of a
    /// cursor): keeps the page as the director's current line and appends its steps to the box (F2-R3).</summary>
    private void OnPageShown(DialogueLine line)
    {
        _pageLine = new DialogueLine(AlundraDialogueCapturePresenter.ToFont3Text(line));
        _pagesDelivered++;
        _box.AppendPage(Tokenize(line), hasFollowingPage: _pagesDelivered < _pageCount);
    }

    /// <summary>F2-R3: cuts a page into steps from its text and its zero-length markers: <c>br</c> a new line, <c>glyph</c> a glyph, <c>voice</c>,
    /// <c>center</c>, <c>slow</c>, <c>flag</c> set at its position, <c>yield</c> a step in which nothing is drawn, <c>empty</c> nothing. Any other
    /// marker is ignored. A character of the text is one step; a line feed (0x0A) is free.</summary>
    internal static List<DialogueToken> Tokenize(DialogueLine line)
    {
        var tokens = new List<DialogueToken>();
        var markers = line.Attributes.Where(a => a.Length == 0).OrderBy(a => a.Position).ToList(); // a stable sort: the markers of one position keep their order
        var next = 0;
        for (var i = 0; i <= line.Text.Length; i++)
        {
            while (next < markers.Count && markers[next].Position <= i)
            {
                AppendMarker(tokens, markers[next++]);
            }

            if (i < line.Text.Length && line.Text[i] != '\n')
            {
                tokens.Add(new DialogueToken(DialogueTokenKind.Character, 0, line.Text[i]));
            }
        }

        return tokens;
    }

    private static void AppendMarker(List<DialogueToken> tokens, DialogueMarkupAttribute marker)
    {
        switch (marker.Name)
        {
            case "br":
                tokens.Add(new DialogueToken(DialogueTokenKind.NewLine));
                break;
            case "glyph" when TryReadId(marker, out var glyphId):
                tokens.Add(new DialogueToken(DialogueTokenKind.MarkedGlyph, glyphId, (char)glyphId));
                break;
            case "voice" when TryReadId(marker, out var voice):
                tokens.Add(new DialogueToken(DialogueTokenKind.Voice, voice));
                break;
            case "center":
                tokens.Add(new DialogueToken(DialogueTokenKind.Center));
                break;
            case "slow":
                tokens.Add(new DialogueToken(DialogueTokenKind.Slow));
                break;
            case "flag" when TryReadId(marker, out var flag) && flag >= 0:
                tokens.Add(new DialogueToken(DialogueTokenKind.Flag, flag));
                break;
            case "yield":
                tokens.Add(new DialogueToken(DialogueTokenKind.Yield));
                break;
        }
    }

    private static bool TryReadId(DialogueMarkupAttribute marker, out int id)
    {
        id = 0;
        if (!marker.Properties.TryGetValue("id", out var value) || value is null)
        {
            return false;
        }

        try
        {
            id = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    // ---- the box's own opcodes

    /// <inheritdoc/>
    public void SetTextFlags(int flags) => _box.SetTextFlags(flags);

    /// <inheritdoc/>
    public void LatchTextStep() => _box.LatchStep();

    /// <inheritdoc/>
    public void SetScrollMode(int mode) => _box.SetScrollMode(mode);

    /// <inheritdoc/>
    public void LatchScrollStart() => _box.LatchScroll();

    /// <inheritdoc/>
    public void SetCloseMask(int mask) => _box.SetCloseMode(mask);

    /// <inheritdoc/>
    public bool RequestScriptClose() => _box.LatchClose();

    // ---- the pass

    /// <summary>
    /// One pass of the box (F2-R1, step 1 of a logic tick): the world proxy calls it at the start of every tick, before the gate is re-read and
    /// before the map events of the tick, with the square button of the tick BEFORE. Runs the machine; what it drew is read by the text box screen's presenter
    /// (E19.f2b1c), the engine's presenter only ever gets the choices.
    /// </summary>
    public void Pass(bool squareHeld, bool squarePressed)
    {
        PassCountForTests++;
        _box.Pass(squareHeld, squarePressed);
    }

    /// <inheritdoc/>
    public void Tick()
    {
        Pass(_tickSquareHeld, _tickSquarePressed);

        var held = _gameState != null && (_gameState.LastPadState.ButtonsHold & SquareBit) != 0;
        _tickSquarePressed = held && !_tickSquareHeld;
        _tickSquareHeld = held;
    }

    // ---- IAlundraDialogueBoxHost

    void IAlundraDialogueBoxHost.FlagReached(int flag)
    {
        if (_gameState != null)
        {
            AlundraYarnBindings.SetTextFlag(_gameState, (uint)flag);
        }
    }

    void IAlundraDialogueBoxHost.PlaySound(int sfxId) => _soundPlayer?.PlaySfx(sfxId);

    int IAlundraDialogueBoxHost.Advance(char display)
    {
        var provider = AdvanceProviderForTests;
        return provider != null
            ? provider(display)
            : AlundraFont3Advances.GetOrCreate(CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath).Advance(display);
    }

    void IAlundraDialogueBoxHost.PageTurned()
    {
        _pageIndex++;

        // The next page is asked of the Yarn runner now, so that its own commands/functions run exactly at its turn - never earlier.
        _runner?.Continue();
    }

    void IAlundraDialogueBoxHost.Released()
    {
        _pageCount = 0;
        _pagesDelivered = 0;
        _pageIndex = 0;
        ClearControlFlags();

        // Closing the box stops any Yarn dialogue still active (the capture presenter swallows the runner's own close) and closes the presenter.
        _runner?.Stop();
        _presenter?.Close();
    }

    /// <summary>
    /// The UI closed the box out of band - the window's own close control, not Alundra's interact
    /// button. Brings the LOGICAL box down with it, at once (like <see cref="InstallForMapEntry"/>: no slide of the exit) so the control flags it
    /// posted are cleared and the world resumes: without this the window vanished while this director still held the box open,
    /// leaving MenuOpen posted with no visible box left to dismiss it, which froze NPCs and the player
    /// until the interact button was pressed as well (reported in play, 2026-09-02).
    ///
    /// No-op when no box is open, which is also the re-entry guard: the presenter that reports its own close is not asked to close again.
    /// </summary>
    internal void NotifyPresenterClosed()
    {
        if (!_box.IsActive)
        {
            return;
        }

        _box.Reset();
        _pageCount = 0;
        _pagesDelivered = 0;
        _pageIndex = 0;
        ClearControlFlags();
        _runner?.Stop();
        _presenter?.Close(); // the box is already down: a presenter that answers by calling back into here finds no box open
    }

    /// <inheritdoc/>
    public void OpenChoice(IReadOnlyList<string> labels)
    {
        _awaitingChoice = true;
        _pendingChoiceResult = null;

        if (_presenter == null)
        {
            return;
        }

        _choiceHandler ??= OnPresenterChoiceSelected;
        _presenter.ChoiceSelected -= _choiceHandler; // guard against a stale double-subscription.
        _presenter.ChoiceSelected += _choiceHandler;
        _presenter.ShowChoices(labels);
    }

    private void OnPresenterChoiceSelected(object? sender, DialogueChoiceSelectedEventArgs e)
    {
        // §1.3: Result = 1 iff the FIRST option was picked, else 0.
        _pendingChoiceResult = e.SelectedIndex == 0 ? 1 : 0;
    }

    /// <inheritdoc/>
    public int? TakeChoiceResult()
    {
        if (_pendingChoiceResult == null)
        {
            return null;
        }

        var result = _pendingChoiceResult.Value;
        _pendingChoiceResult = null;
        _awaitingChoice = false;
        UnsubscribeChoiceHandler();

        // E19.f2b1c F2B1C-R5: the engine's window holds only the buttons now, and after an answer its service stays open: it goes with the answer when a box is active
        // (the box has its own screen). A lone choice, asked without a box, is closed by CloseStandaloneChoice.
        if (_box.IsActive)
        {
            _presenter?.Close();
        }

        return result;
    }

    /// <summary>
    /// E16.e L4 (docs/plan-e16-etat-partie.md, SE1): ends a choice list without a selection - clears the awaiting
    /// state and any result not yet taken, and stops listening to the presenter. It neither closes a box nor touches
    /// <see cref="AlundraGameState.PlayerControlFlags"/>: the save book calls it on its abandon and its reset, after
    /// closing the box it owns, so no choice of its own is ever left waiting. A no-op when no choice is pending.
    /// </summary>
    internal void CancelChoice()
    {
        _awaitingChoice = false;
        _pendingChoiceResult = null;
        UnsubscribeChoiceHandler();
    }

    /// <summary>
    /// E16.e L5 (docs/plan-e16-etat-partie.md, the closing review of 2026-09-29): closes a choice list asked WITHOUT
    /// a box (<see cref="OpenChoice"/> with no <see cref="Open"/> before it) - the save screen's OUI/NON. After an
    /// answer the engine's service stays open (<c>DialogueService.SelectChoice</c>) and nothing else removes the
    /// dialogue screen, while the release of a box would clear <c>MessageBox</c>/<c>MenuOpen</c>, which the
    /// save screen keeps until its state <c>0x63</c>. So this clears the choice in waiting and any result not yet
    /// taken, stops listening to the presenter, and closes the presenter (<c>_presenter?.Close()</c>, which removes
    /// the screen). It never touches <see cref="AlundraGameState.PlayerControlFlags"/>.
    /// Returns false, doing nothing, while a box is open (<see cref="IsOpen"/>): that choice is not a lone one.
    /// </summary>
    internal bool CloseStandaloneChoice()
    {
        if (_box.IsActive)
        {
            return false;
        }

        _awaitingChoice = false;
        _pendingChoiceResult = null;
        UnsubscribeChoiceHandler();
        _presenter?.Close();
        return true;
    }

    private void UnsubscribeChoiceHandler()
    {
        if (_presenter != null && _choiceHandler != null)
        {
            _presenter.ChoiceSelected -= _choiceHandler;
        }
    }

    /// <summary>Test-only: clears every piece of session state (same seam as
    /// <see cref="AlundraMusicPlayer.ResetForTests"/>/<see cref="AlundraScreenFadeDirector.ResetForTests"/>)
    /// so tests do not leak into each other through this singleton.</summary>
    internal void ResetForTests()
    {
        UnsubscribeChoiceHandler();
        _presenter = null;
        _gameState = null;
        _soundPlayer = null;
        _capturePresenter = null;
        _runner = null;
        _boundGameState = null;
        _box.Reset();
        _pageCount = 0;
        _pagesDelivered = 0;
        _pageIndex = 0;
        _pageLine = DialogueLine.Empty;
        _tickSquareHeld = false;
        _tickSquarePressed = false;
        _awaitingChoice = false;
        _pendingChoiceResult = null;
        PassCountForTests = 0;
    }

    /// <summary>Test-only accessor: how many passes of the box ran since the last <see cref="ResetForTests"/> (one per logic tick, whether a box is open or not).</summary>
    internal int PassCountForTests { get; private set; }

    /// <summary>Test-only accessor (T3): the close-mode mask currently in effect.</summary>
    internal int CloseMaskForTests => _box.CloseMode;

    /// <summary>Test-only accessor: how many pages the currently open dialogue was split into (0 when
    /// closed).</summary>
    internal int PageCountForTests => _pageCount;

    /// <summary>Test-only accessor: the zero-based index of the page currently shown.</summary>
    internal int PageIndexForTests => _pageIndex;

    /// <summary>Test-only accessor: the WHOLE current page as the director holds it (the text of the line the runner delivered, in font3), or null when no
    /// presenter is attached - the presenter itself never sees the text (E19.f2b1c: the box is drawn by the text box screen).</summary>
    internal DialogueLine? CurrentLineForTests => _presenter == null ? null : _pageLine;

    /// <summary>Test-only accessor: the attached presenter's own currently displayed choice labels (empty
    /// when none is awaiting selection).</summary>
    internal IReadOnlyList<string> ChoicesForTests => _presenter?.Choices ?? Array.Empty<string>();

    /// <summary>Test-only: drives the attached presenter's own <c>SelectChoice</c> directly - simulates
    /// the player picking an option without needing a live UI.</summary>
    internal bool SelectChoiceForTests(int index) => _presenter?.SelectChoice(index) ?? false;
}
