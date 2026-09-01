#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;

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
    /// mask to 3 (§1.2/T3), splits <paramref name="rawText"/> into pages (<see cref="AlundraDialogueTextParser"/>),
    /// applies <paramref name="controlMode"/>'s <see cref="AlundraGameState.PlayerControlBits.MessageBox"/>/
    /// <see cref="AlundraGameState.PlayerControlBits.MenuOpen"/> bit, and shows the first page (applying
    /// its own numeric control-code flags immediately, D-E12-4).
    /// </summary>
    void Open(string rawText, int controlMode);

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

    private bool _isOpen;
    private int _closeMask = DefaultCloseMask;
    private IReadOnlyList<AlundraDialoguePage>? _pages;
    private int _pageIndex;
    private uint _ticksSinceOpenOrPage;

    private bool _awaitingChoice;
    private int? _pendingChoiceResult;
    private EventHandler<DialogueChoiceSelectedEventArgs>? _choiceHandler;

    public bool HasPresenter => _presenter != null;
    public bool IsOpen => _isOpen;
    public bool IsAwaitingChoice => _awaitingChoice;

    /// <summary>Re-points this session-scoped instance at the current world's own presenter/game state -
    /// called by <see cref="AlundraWorldProxy.InstallDialogueSystems"/> on every world install. Deliberately
    /// does NOT touch <see cref="_isOpen"/>/<see cref="_closeMask"/>/<see cref="_pages"/>/choice state (same
    /// contract as <see cref="AlundraMusicPlayer.AttachToWorld"/>/<see cref="AlundraScreenFadeDirector.AttachToWorld"/>)
    /// - only <see cref="InstallForMapEntry"/> does that. <paramref name="presenter"/> null is a valid,
    /// tolerated value (no UI view available for this world's active render view) - <see cref="HasPresenter"/>
    /// then drives every opcode's own degraded fallback.</summary>
    public void AttachToWorld(IDialoguePresenter? presenter, AlundraGameState? gameState)
    {
        _presenter = presenter;
        _gameState = gameState;
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
        _pages = null;
        _pageIndex = 0;
        _ticksSinceOpenOrPage = 0;
        _awaitingChoice = false;
        _pendingChoiceResult = null;
    }

    /// <inheritdoc/>
    public void Open(string rawText, int controlMode)
    {
        _closeMask = DefaultCloseMask; // §1.2/T3: every open resets the close-mode mask to 3.
        _pages = AlundraDialogueTextParser.SplitIntoPages(rawText);
        _pageIndex = 0;
        _ticksSinceOpenOrPage = 0;
        _isOpen = true;
        _awaitingChoice = false;
        _pendingChoiceResult = null;

        ApplyControlMode(controlMode);
        ShowCurrentPage();
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

    private bool HasMorePages() => _pages != null && _pageIndex + 1 < _pages.Count;

    private void AdvancePage()
    {
        _pageIndex++;
        _ticksSinceOpenOrPage = 0;
        ShowCurrentPage();
    }

    private void ShowCurrentPage()
    {
        if (_pages == null || _pageIndex >= _pages.Count)
        {
            return;
        }

        var page = _pages[_pageIndex];

        // D-E12-4: apply THIS page's numeric control-code flags the moment it is displayed - proven
        // equivalent to the decompiled TextDecoder.cs:259-307 write (index=((n>>3)&0xffc)>>2,
        // bit=n&0x1f) via AddFlag(n | 0x8000, 1 << (n & 0x1f)): AddFlag's own IndexOf is (flag>>5)&0x3ff,
        // and (n|0x8000)>>5 == (n>>5) with bit 10 forced then masked back off by &0x3ff, i.e. exactly
        // (n>>5)&0x3ff == ((n>>3)&0xffc)>>2 for every n - and 0x8000 never touches the low 5 bits the
        // mask itself reads.
        foreach (var n in page.NumericCodes)
        {
            _gameState?.AddFlag((uint)(n | 0x8000), 1u << (n & 0x1f));
        }

        _presenter?.ShowLine(new DialogueLine(page.DisplayText));
    }

    private void Close()
    {
        _isOpen = false;
        _pages = null;
        _pageIndex = 0;
        ClearControlFlags();
        _presenter?.Close();
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
        _isOpen = false;
        _closeMask = DefaultCloseMask;
        _pages = null;
        _pageIndex = 0;
        _ticksSinceOpenOrPage = 0;
        _awaitingChoice = false;
        _pendingChoiceResult = null;
    }

    /// <summary>Test-only accessor (T3): the close-mode mask currently in effect.</summary>
    internal int CloseMaskForTests => _closeMask;

    /// <summary>Test-only accessor: how many pages the currently open dialogue was split into (0 when
    /// closed).</summary>
    internal int PageCountForTests => _pages?.Count ?? 0;

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
