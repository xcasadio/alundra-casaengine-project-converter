#nullable enable
using System;
using System.Collections.Generic;
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
/// Backs opcodes 0x0D/0x39/0x44/0x50/0x51/0x5C in <see cref="AlundraEventProgramRunner.Dispatch"/> via
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

    /// <summary>Port of the original's own "is a dialog box currently open" state - true from a
    /// successful <see cref="Open"/> until <see cref="AlundraDialogueDirector.Close"/> runs (button,
    /// script, or auto-timer).</summary>
    bool IsOpen { get; }

    /// <summary>True while a choice list (opcode 0x44) is open and unresolved.</summary>
    bool IsAwaitingChoice { get; }

    /// <summary>
    /// Opcode 0x0D/0x5C's own "open" half (Dispatch itself owns the reentrancy guard - see that method's
    /// own doc on why 0x0D checks <see cref="IsOpen"/> BEFORE calling this, T2): resets the close-mode
    /// mask to 3 (§1.2/T3), applies <paramref name="controlMode"/>'s
    /// <see cref="AlundraGameState.PlayerControlBits.MessageBox"/>/
    /// <see cref="AlundraGameState.PlayerControlBits.MenuOpen"/> bit, and starts <paramref name="node"/>
    /// of <paramref name="asset"/> on this director's own Yarn runner (docs/plan-e15-yarn.md, E15.c
    /// contract item 2), which delivers its first page immediately - the page's own commands/functions
    /// run at that moment, as the numeric control-code flags did before E15.c (D-E12-4). Either
    /// <paramref name="asset"/> null, <paramref name="node"/> null, or a node absent from the asset opens
    /// an empty box instead (D-E15-10), exactly as an out-of-range/never-loaded local string did before.
    /// </summary>
    void Open(DialogueAsset? asset, string? node, int controlMode);

    /// <summary>Opcode 0x50 - sets the close-mode mask (bit0 auto-timer/bit1 button/bit2 script, §1.2).</summary>
    void SetCloseMask(int mask);

    /// <summary>Opcode 0x51 - honoured only while <see cref="IsOpen"/> and the mask's bit2 (script-close)
    /// is set (§1.2); returns whether it actually closed anything, though the opcode itself writes no
    /// <c>Result</c> either way (see that opcode's own dispatch doc).</summary>
    bool RequestScriptClose();

    /// <summary>Polled once per dispatch of opcode 0x39 (the only "re-checked every frame while blocking"
    /// site on the ordinary, non-choice path - see this method's own class doc): while
    /// <see cref="IsOpen"/> and NOT <see cref="IsAwaitingChoice"/>, advances to the next page on a
    /// freshly-pressed interact button (unconditional - the close-mode mask only ever gates the FINAL
    /// close, never an intermediate page turn) or closes once the last page is showing and either the
    /// button-close bit (mask bit1) or the auto-timer (mask bit0, 360 ticks) allows it. A no-op while
    /// closed or while a choice is being asked (the choice UI owns input then).</summary>
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
/// </summary>
public sealed class AlundraDialogueDirector : IAlundraDialogueDirector
{
    /// <summary>The one session-scoped instance every <see cref="AlundraWorldProxy"/> shares.</summary>
    public static readonly AlundraDialogueDirector Instance = new();

    private const uint AutoCloseTicks = 360; // §1.2 - mask bit0.
    private const int DefaultCloseMask = 3; // §1.2 - bit0 (auto-timer) | bit1 (button), the original's default g_etcAnimationMode.
    private const int CloseMaskAutoTimerBit = 0x1;
    private const int CloseMaskButtonBit = 0x2;
    private const int CloseMaskScriptBit = 0x4;
    private const uint InteractButtonBit = AlundraPadState.Square; // §1.2/D-E12-4: bit 0x80, just-pressed.

    private AlundraDialogueDirector()
    {
    }

    private IDialoguePresenter? _presenter;
    private AlundraGameState? _gameState;

    // E15.c T5 (docs/plan-e15-yarn.md, contract item 2): this director's own Yarn runner, built on a
    // capture presenter (AlundraDialogueCapturePresenter) that forwards every transformed line to
    // _presenter (the world's own presenter, above) - NEVER built directly on _presenter, which is
    // reattached on every AttachToWorld while these two persist. Rebuilt only when the game state
    // actually changes (a new world/map - InstallForMapEntry resets open/page state right after anyway);
    // a same-game-state re-point (TryWireDialoguePresenterOnce's "presenter appears later this frame")
    // just re-points the capture presenter's own WorldPresenter, keeping any dialogue already running.
    private AlundraDialogueCapturePresenter? _capturePresenter;
    private YarnDialogueRunner? _runner;
    private AlundraGameState? _boundGameState;

    private bool _isOpen;
    private int _closeMask = DefaultCloseMask;
    private int _pageCount;
    private int _pageIndex;
    private uint _ticksSinceOpenOrPage;

    private bool _awaitingChoice;
    private int? _pendingChoiceResult;
    private EventHandler<DialogueChoiceSelectedEventArgs>? _choiceHandler;

    public bool HasPresenter => _presenter != null;
    public bool IsOpen => _isOpen;
    public bool IsAwaitingChoice => _awaitingChoice;

    /// <summary>
    /// E16.e L4 (docs/plan-e16-etat-partie.md, SE1): how many times <see cref="Open"/> ran this session - a caller
    /// that opened a box remembers the value right after its own <see cref="Open"/>, and knows the box is still its
    /// own while the value has not changed (the save book closes only the box it opened). Never reset, not even by
    /// <see cref="InstallForMapEntry"/>, so a remembered value can never match a later box by accident.
    /// </summary>
    public int OpenSerial { get; private set; }

    /// <summary>Re-points this session-scoped instance at the current world's own presenter/game state -
    /// called by <see cref="AlundraWorldProxy.InstallDialogueSystems"/> on every world install. Deliberately
    /// does NOT touch <see cref="_isOpen"/>/<see cref="_closeMask"/>/<see cref="_pageCount"/>/choice state (same
    /// contract as <see cref="AlundraMusicPlayer.AttachToWorld"/>/<see cref="AlundraScreenFadeDirector.AttachToWorld"/>)
    /// - only <see cref="InstallForMapEntry"/> does that. <paramref name="presenter"/> null is a valid,
    /// tolerated value (no UI view available for this world's active render view) - <see cref="HasPresenter"/>
    /// then drives every opcode's own degraded fallback.</summary>
    public void AttachToWorld(IDialoguePresenter? presenter, AlundraGameState? gameState)
    {
        _presenter = presenter;
        _gameState = gameState;

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
        _capturePresenter = new AlundraDialogueCapturePresenter(presenter, gameState);
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
        if (_isOpen)
        {
            ClearControlFlags();
        }

        UnsubscribeChoiceHandler();

        _isOpen = false;
        _closeMask = DefaultCloseMask;
        _pageCount = 0;
        _pageIndex = 0;
        _ticksSinceOpenOrPage = 0;
        _awaitingChoice = false;
        _pendingChoiceResult = null;
        _swallowOpeningButtonPress = false;
        _runner?.Stop();
    }

    /// <summary>E12.d (D-E12D-6): true when the interact button was ALREADY just-pressed in the pad
    /// snapshot at the moment <see cref="Open"/> ran - i.e. the very press that triggered the
    /// interaction that opened this box. The first <see cref="Tick"/> then ignores the button (the
    /// auto-timer still counts), so the opening press cannot advance or close what it just opened.
    /// The original is protected upstream instead: its advance pass is suppressed during the box's
    /// opening animation and text decoding (UIManager.Fun_80046ef0:104-119, g_dialog_flags &amp; 3 /
    /// g_textPrimitives) - a strictly LONGER suppression window than this one-snapshot swallow.</summary>
    private bool _swallowOpeningButtonPress;

    /// <inheritdoc/>
    public void Open(DialogueAsset? asset, string? node, int controlMode)
    {
        OpenSerial++; // E16.e L4: see the property's own doc.
        _closeMask = DefaultCloseMask; // §1.2/T3: every open resets the close-mode mask to 3.
        _swallowOpeningButtonPress =
            _gameState != null && (_gameState.LastPadState.ButtonsJustPressed & InteractButtonBit) != 0;
        _pageIndex = 0;
        _ticksSinceOpenOrPage = 0;
        _isOpen = true;
        _awaitingChoice = false;
        _pendingChoiceResult = null;

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
            _capturePresenter?.ShowLine(DialogueLine.Empty);
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

        switch (controlMode)
        {
            case 1: // §1.2: MessageBox (0x10) - player frozen, world/scripts keep ticking.
                _gameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MessageBox;
                break;
            case 0: // §1.2: MenuOpen (0x08) - map events/world updates pause too.
                _gameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;
                break;
        }
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

    /// <inheritdoc/>
    public void SetCloseMask(int mask) => _closeMask = mask;

    /// <inheritdoc/>
    public bool RequestScriptClose()
    {
        if (!_isOpen || (_closeMask & CloseMaskScriptBit) == 0)
        {
            return false;
        }

        Close();
        return true;
    }

    /// <inheritdoc/>
    public void Tick()
    {
        if (!_isOpen || _awaitingChoice)
        {
            return;
        }

        _ticksSinceOpenOrPage++;

        var buttonPressed = _gameState != null && (_gameState.LastPadState.ButtonsJustPressed & InteractButtonBit) != 0;

        // D-E12D-6: the press that opened this box does not also advance it (see the field's own doc).
        if (_swallowOpeningButtonPress)
        {
            _swallowOpeningButtonPress = false;
            buttonPressed = false;
        }
        var autoTimerElapsed = (_closeMask & CloseMaskAutoTimerBit) != 0 && _ticksSinceOpenOrPage >= AutoCloseTicks;

        if (!buttonPressed && !autoTimerElapsed)
        {
            return;
        }

        if (HasMorePages())
        {
            // §1.2: the auto-timer only ever CLOSES the box - it never auto-turns a page.
            if (buttonPressed)
            {
                AdvancePage();
            }

            return;
        }

        var canClose = autoTimerElapsed || (buttonPressed && (_closeMask & CloseMaskButtonBit) != 0);
        if (canClose)
        {
            Close();
        }
    }

    private bool HasMorePages() => _pageIndex + 1 < _pageCount;

    /// <summary>Turns the page: only ever called while a real Yarn dialogue is running (the empty-box
    /// path of <see cref="Open"/> sets <see cref="_pageCount"/> to 1, so <see cref="HasMorePages"/> is
    /// never true there). Item 2: the runner is asked to continue ONLY when the page actually turns, so
    /// each page's own commands/functions run exactly at its display - never earlier.</summary>
    private void AdvancePage()
    {
        _pageIndex++;
        _ticksSinceOpenOrPage = 0;
        _runner?.Continue();
    }

    /// <summary>
    /// The UI closed the box out of band - the window's own close control, not Alundra's interact
    /// button. Brings the LOGICAL box down with it so the control flags it posted are cleared and the
    /// world resumes: without this the window vanished while this director still held the box open,
    /// leaving MenuOpen posted with no visible box left to dismiss it, which froze NPCs and the player
    /// until the interact button was pressed as well (reported in play, 2026-09-02).
    ///
    /// No-op when no box is open, which is also the re-entry guard: <see cref="Close"/> calls the
    /// presenter's own Close, and a presenter that answers by calling back into here finds
    /// <see cref="_isOpen"/> already false.
    /// </summary>
    internal void NotifyPresenterClosed()
    {
        if (!_isOpen)
        {
            return;
        }

        Close();
    }

    private void Close()
    {
        _isOpen = false;
        _pageCount = 0;
        _pageIndex = 0;
        ClearControlFlags();
        // Item 2: closing the box calls the runner's own Stop - which stops any Yarn dialogue still
        // active and unconditionally closes the capture presenter, which forwards to the world's own
        // presenter (the same effect _presenter?.Close() had before E15.c, for the empty-box path too:
        // Open shows DialogueLine.Empty on the SAME capture presenter, never _presenter directly).
        _runner?.Stop();
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
    /// dialogue screen, while the private <see cref="Close"/> would clear <c>MessageBox</c>/<c>MenuOpen</c>, which the
    /// save screen keeps until its state <c>0x63</c>. So this clears the choice in waiting and any result not yet
    /// taken, stops listening to the presenter, and closes the presenter (<c>_presenter?.Close()</c>, which removes
    /// the screen). It never touches <see cref="AlundraGameState.PlayerControlFlags"/> and never goes through
    /// <see cref="Close"/>: the presenter's call back (<see cref="NotifyPresenterClosed"/>) finds no box open.
    /// Returns false, doing nothing, while a box is open (<see cref="IsOpen"/>): that choice is not a lone one.
    /// </summary>
    internal bool CloseStandaloneChoice()
    {
        if (_isOpen)
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
        _capturePresenter = null;
        _runner = null;
        _boundGameState = null;
        _isOpen = false;
        _closeMask = DefaultCloseMask;
        _pageCount = 0;
        _pageIndex = 0;
        _ticksSinceOpenOrPage = 0;
        _awaitingChoice = false;
        _pendingChoiceResult = null;
        _swallowOpeningButtonPress = false;
    }

    /// <summary>Test-only accessor (T3): the close-mode mask currently in effect.</summary>
    internal int CloseMaskForTests => _closeMask;

    /// <summary>Test-only accessor: how many pages the currently open dialogue was split into (0 when
    /// closed).</summary>
    internal int PageCountForTests => _pageCount;

    /// <summary>Test-only accessor: the zero-based index of the page currently shown.</summary>
    internal int PageIndexForTests => _pageIndex;

    /// <summary>Test-only accessor: the attached presenter's own current line, or null if none is
    /// attached - avoids reflection in tests that need to see what was actually shown.</summary>
    internal DialogueLine? CurrentLineForTests => _presenter?.CurrentLine;

    /// <summary>Test-only accessor: the attached presenter's own currently displayed choice labels (empty
    /// when none is awaiting selection).</summary>
    internal IReadOnlyList<string> ChoicesForTests => _presenter?.Choices ?? Array.Empty<string>();

    /// <summary>Test-only: drives the attached presenter's own <c>SelectChoice</c> directly - simulates
    /// the player picking an option without needing a live UI.</summary>
    internal bool SelectChoiceForTests(int index) => _presenter?.SelectChoice(index) ?? false;
}
