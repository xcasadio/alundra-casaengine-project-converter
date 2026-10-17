#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.Yarn;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Audio;
using Alundra.Transitions;
using Alundra.Scripts;

namespace Alundra.Dialogues;

/// <summary>
/// Opcode-facing seam over <see cref="DialogueDirector"/> (docs/plan-e12-dialogues.md, slice
/// E12.a, D-E12-5: "the moteur is MECHANISM, the DLL is POLICY" - the engine's own
/// <see cref="IDialoguePresenter"/> knows nothing about the close-mode mask, numeric control-code flags,
/// paging on <c>\A</c>, or <see cref="GameState.PlayerControlFlags"/>; all of that lives here).
/// Backs opcodes 0x0D/0x39/0x44/0x4C-0x51/0x5C in <see cref="EventProgramRunner.Dispatch"/> via
/// <see cref="IEntityWorldContext.DialogueDirector"/> - a default interface member (same shape as
/// <see cref="IEntityWorldContext.ScreenFadeDirector"/>), so every EXISTING implementer keeps compiling
/// unmodified, degrading to null (skip-by-size, once-logged) exactly like that seam.
/// </summary>
public interface IDialogueDirector
{
    /// <summary>True once a real <see cref="IDialoguePresenter"/> has been attached
    /// (<see cref="DialogueDirector.AttachToWorld"/>) - the actual real/degraded switch every
    /// opcode case in <see cref="EventProgramRunner.Dispatch"/> tests (NOT whether this interface
    /// member itself is null - see that interface member's own doc).</summary>
    bool HasPresenter { get; }

    /// <summary>Port of the original's own "is a dialog box currently open" state (<c>g_dialog_flags &amp; 4</c>) - true from a
    /// successful <see cref="Open"/> until the box is RELEASED, 18 passes after its close was triggered (E19.f2a, F2-R2): the
    /// slide of the exit is part of the open box, and opcode 0x39 waits for the release.</summary>
    bool IsOpen { get; }

    /// <summary>True while a choice list (opcode 0x44) is open and unresolved.</summary>
    bool IsAwaitingChoice { get; }

    /// <summary>
    /// E19.f4b (docs/plan-e19-opcodes.md, F4B-R1): the speaker of a dialogue - opcodes 0x0D, 0x5C and 0xC4 call it at EVERY attempt, right before the box's own opening and its "already open"
    /// test, as the binary does (portrait first, then name). <paramref name="speaker"/> null (the search found nobody) opens neither; <paramref name="nameId"/> is the ETC index of the name
    /// (0xC4's operands), the sprite type of the speaker when null. A default member that does nothing, like the other seams of this interface, so every implementer keeps compiling.
    /// </summary>
    void OpenSpeaker(AlundraEntityScriptProxy? speaker, int? nameId = null) { }

    /// <summary>
    /// Opcode 0x0D/0x5C/0xC4's own "open" half (Dispatch itself owns the reentrancy guard - see that method's
    /// own doc on why 0x0D checks <see cref="IsOpen"/> BEFORE calling this, T2): resets the box to the state of
    /// <c>InitializeDialogMessage</c> (text flags 3, close mode 3, scroll mode 3, the latches off), applies
    /// <paramref name="controlMode"/>'s <see cref="GameState.PlayerControlBits.MessageBox"/> (1) or
    /// <see cref="GameState.PlayerControlBits.MenuOpen"/> (anything else) bit, plays sound 6, and starts
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
    /// <see cref="GameState.LastPadState"/> (<c>ButtonsHold</c>, the rising edge being taken against the previous call). The world proxy
    /// does not call this: it calls <see cref="DialogueDirector.Pass"/> itself, on the square of its own pad pass (F2-R1).</summary>
    void Tick();

    /// <summary>Opcode 0x44's own first-entry half: opens a generic choice list (labels already resolved
    /// by the caller - <see cref="EtcStringTable"/> for the OUI/NON pair, D-E12-6) through the
    /// attached presenter and starts waiting for <see cref="TakeChoiceResult"/> to report a selection.</summary>
    void OpenChoice(IReadOnlyList<string> labels);

    /// <summary>Opcode 0x44's own polling half: <see langword="null"/> while no selection has been made
    /// yet (still <see cref="IsAwaitingChoice"/>) - the caller returns 0 (suspend) in that case, exactly
    /// like <see cref="IsOpen"/>'s own gate. Once a selection lands, returns 1 iff it was the FIRST option
    /// (§1.3's own <c>Result = 1 ssi la PREMIÈRE option</c>) and clears the awaiting state so a later call
    /// does not re-report the same selection.</summary>
    int? TakeChoiceResult();
}
