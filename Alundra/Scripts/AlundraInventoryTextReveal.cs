#nullable enable
using System;
using CasaEngine.Engine.Environment;

namespace Alundra.Scripts;

/// <summary>
/// E13.d SI11 (docs/plan-e13d-sous-inventaire.md, D-E13D-35): the item text reveal both inventories run - the
/// name, then the first description line, then the second, one decoded character every third tick
/// (D-E13D-31). The original has two copies of this machine with their own globals, <c>DisplayInventoryTexts</c>
/// (<c>0x80055fe8</c>, <c>MainInventoryManager.cs:929-1064</c>, <c>g_inventoryCursorText</c>) for the main
/// inventory and <c>FUN_80053fdc</c> (<c>SubInventoryManager.cs:468-629</c>, <c>INT_8017f788</c>) for the
/// sub-inventory; the author chose one machine, each director owning its own instance (its own state, as in the
/// original). Each director keeps what is its own: which item is described, and when the reveal restarts.
/// <para>The sub-inventory's copy read the second line one byte early in the executable (<c>0x80054314</c>),
/// never showing it; the shared machine reads it where the main inventory's does (D-E13D-30, SI7).</para>
/// </summary>
internal sealed class AlundraInventoryTextReveal
{
    /// <summary>The raw state value: 0 name setup, 1..0x10 name reveal, 0x11..0x4c hold, 0x4d desc-line-0
    /// setup, 0x4e..0x8d desc-line-0 reveal, 0x8e desc-line-1 setup, 0x8f..0xce desc-line-1 reveal, 0xcf
    /// done.</summary>
    public int State { get; private set; }

    public string NameVisiblePrefix { get; private set; } = string.Empty;
    public string Description0VisiblePrefix { get; private set; } = string.Empty;
    public string Description1VisiblePrefix { get; private set; } = string.Empty;

    /// <summary>What <c>DisplayInventoryDescription(0)</c> drew THIS tick - the name during its reveal and
    /// hold (states 1..0x4c), the first description line from 0x4e - and empty on every tick that draws nothing
    /// on line 0: state 0, state 0x4d, nothing to describe. The raw prefixes above keep their values across
    /// ticks that draw nothing, which is why they are not what a screen reads.</summary>
    public string DrawnLine0 { get; private set; } = string.Empty;

    /// <summary>What <c>DisplayInventoryDescription(1)</c> drew THIS tick - the second description line from
    /// state 0x8f - and empty otherwise.</summary>
    public string DrawnLine1 { get; private set; } = string.Empty;

    /// <summary>The 3-tick countdown (<c>INT_8017fef0</c>/<c>INT_8017f78c</c>, <c>FUN_80055f48</c>/
    /// <c>FUN_80053f3c</c>): one character committed every third tick, shared by the three lines.</summary>
    private int _countdown;

    /// <summary>Everything back to construction state - an inventory's setup (the main's <c>FUN_80054f1c</c>,
    /// the sub's <c>InitializeSubInventory</c>) and the session reset.</summary>
    public void Reset()
    {
        State = 0;
        NameVisiblePrefix = string.Empty;
        Description0VisiblePrefix = string.Empty;
        Description1VisiblePrefix = string.Empty;
        DrawnLine0 = string.Empty;
        DrawnLine1 = string.Empty;
        _countdown = 0;
    }

    /// <summary>A cursor move: the state alone goes back to 0 (<c>g_inventoryCursorText = 0</c>); state 0
    /// itself clears what it needs on the next <see cref="Tick"/>.</summary>
    public void Restart() => State = 0;

    /// <summary>One tick of the machine. <paramref name="itemId"/> null - an empty or unowned slot - draws
    /// nothing and leaves the state frozen, as both originals return before touching it.</summary>
    public void Tick(int? itemId)
    {
        DrawnLine0 = string.Empty;
        DrawnLine1 = string.Empty;

        if (itemId is not { } item)
        {
            return;
        }

        var cursor = State;
        var projectPath = EngineEnvironment.ProjectPath;

        // Main :959-967 / sub :522-530 - state 0: load the name, nothing revealed yet.
        if (cursor == 0)
        {
            NameVisiblePrefix = string.Empty;
            _countdown = 0;
            State = cursor + 1;
            return;
        }

        // Main :970-986 / sub :533-549 - states 1..0x10: reveal the name one character at a time.
        if ((uint)(cursor - 1) < 0x10)
        {
            AlundraEtcStringTable.TryResolveItemName(projectPath, item, out var name);

            if (cursor - 1 >= name.Length)
            {
                State = 0x11;
            }
            else
            {
                Advance();
                NameVisiblePrefix = VisiblePrefix(name, State - 1);
            }

            DrawnLine0 = NameVisiblePrefix; // main :983 DisplayInventoryDescription(0)
            return;
        }

        // Main :989-994 / sub :552-559 - states 0x11..0x4c: hold the name on screen.
        if ((uint)(cursor - 0x11) < 0x3c)
        {
            State = cursor + 1;
            DrawnLine0 = NameVisiblePrefix; // main :992 DisplayInventoryDescription(0)
            return;
        }

        // Main :997-1005 / sub :562-570 - state 0x4d: switch line 0 from the name to the first description line.
        if (cursor == 0x4d)
        {
            Description0VisiblePrefix = string.Empty;
            _countdown = 0;
            State = cursor + 1;
            return;
        }

        // Main :1008-1024 / sub :573-589 - states 0x4e..0x8d: reveal the first description line.
        if ((uint)(cursor - 0x4e) < 0x40)
        {
            AlundraEtcStringTable.TryResolveItemDescriptionLine0(projectPath, item, out var firstLine);

            if (cursor - 0x4e >= firstLine.Length)
            {
                State = 0x8e;
            }
            else
            {
                Advance();
                Description0VisiblePrefix = VisiblePrefix(firstLine, State - 0x4e);
            }

            DrawnLine0 = Description0VisiblePrefix; // main :1022 DisplayInventoryDescription(0)
            return;
        }

        // Main :1027-1036 / sub :592-601 - state 0x8e: switch to the second description line.
        if (cursor == 0x8e)
        {
            Description1VisiblePrefix = string.Empty;
            _countdown = 0;
            State = cursor + 1;
            DrawnLine0 = Description0VisiblePrefix; // main :1032 DisplayInventoryDescription(0)
            return;
        }

        // Main :1039-1056 / sub :604-621 - states 0x8f..0xce: reveal the second description line, read at
        // c - 0x8f (main 0x80056274; the sub's executable reads c - 0x90, a defect corrected, D-E13D-30).
        if ((uint)(cursor - 0x8f) < 0x40)
        {
            AlundraEtcStringTable.TryResolveItemDescriptionLine1(projectPath, item, out var secondLine);

            if (cursor - 0x8f >= secondLine.Length)
            {
                State = 0xcf;
            }
            else
            {
                Advance();
                Description1VisiblePrefix = VisiblePrefix(secondLine, State - 0x8f);
            }

            DrawnLine0 = Description0VisiblePrefix; // main :1053 DisplayInventoryDescription(0)
            DrawnLine1 = Description1VisiblePrefix; // main :1054 DisplayInventoryDescription(1)
            return;
        }

        // Main :1059-1062 / sub :624-628 - 0xcf: both lines complete and drawn, nothing more to advance.
        if (cursor == 0xcf)
        {
            DrawnLine0 = Description0VisiblePrefix;
            DrawnLine1 = Description1VisiblePrefix;
        }
    }

    /// <summary>Port of <c>FUN_80055f48</c>/<c>FUN_80053f3c</c>: commits one character every third tick. The
    /// originals also widen the glyph strip by the character's pixel width - irrelevant here, the screen
    /// measures its own font.</summary>
    private void Advance()
    {
        if (_countdown != 0)
        {
            _countdown -= 1;
            return;
        }

        _countdown = 2;
        State += 1;
    }

    // The originals' clamp (MainInventoryManager.cs:1069-1094) also backed off an accent escape pair ({x, }x)
    // cut in half; the port reveals decoded strings, which hold none (0 of the 354 exported strings), so the
    // clamp alone remains (plan E13.d SI9, finding 11).
    private static string VisiblePrefix(string text, int visibleLength) =>
        text.Substring(0, Math.Clamp(visibleLength, 0, text.Length));
}
