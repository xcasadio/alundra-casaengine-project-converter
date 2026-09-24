#nullable enable
using System;
using CasaEngine.Engine.Environment;

namespace Alundra.Scripts;

/// <summary>
/// E13.d SI3 (docs/plan-e13d-sous-inventaire.md, D-E13D-20): a pure, tick-driven port of the original's
/// sub-inventory (<c>SubInventoryManager</c>) - same director/presenter/screen split as
/// <see cref="AlundraInventoryDirector"/>, no MGUI dependency: every piece of state D5's future screen
/// (SI4) will need to draw is exposed as plain, read-only properties.
///
/// <para><b>No trigger of its own</b>: unlike <see cref="AlundraInventoryDirector"/>, this director never
/// reads the pad to OPEN itself - it only opens through <see cref="OpenFromPostProcess"/>, called by
/// <see cref="AlundraInventoryPostProcess.Run"/> once the main inventory's closing slide has settled
/// (plan §1.1/§1.2). Its own L1/R1 (<see cref="RunInput"/>) closes it and hands control BACK to the main
/// inventory the same way, through <see cref="AlundraInventoryPostProcess.State"/> = 2.</para>
///
/// <para><b>The text-reveal machine has its own copy</b> (D-E13D-25): the original has two distinct
/// functions and two distinct sets of globals (<c>DisplayInventoryTexts</c>/<c>g_inventoryCursorText</c>
/// for the main inventory, <c>FUN_80053fdc</c>/<c>INT_8017f788</c> here) - this port keeps that split
/// rather than factoring a shared machine (plan §6 point 2: a future reflection point, not done here).</para>
///
/// <para><b>The second description line is never revealed</b> (D-E13D-28, <c>[binaire]</c>): at states
/// <c>0x8f..0xce</c> the executable reads <c>line2[c - 0x90]</c> (<c>0x80054314</c>/<c>0x80054330</c>), one
/// byte EARLIER than the correct <c>c - 0x8f</c> the main inventory's own machine uses (and the
/// decompilation of BOTH machines writes, <c>SubInventoryManager.cs:608/614</c>). At <c>c == 0x8f</c> that
/// reads the byte immediately BEFORE the string - the previous string's own null terminator - measured 0
/// for all 98 items of <c>DATA/ETC_RES.R</c> (plan §7's own script). So the very first tick of this state
/// range already sees "index &lt; 0", which this port treats the same way the original's own byte-early
/// read effectively behaves: end of string, state jumps straight to <c>0xcf</c>, nothing is ever committed
/// to line 1. <see cref="DrawnDescriptionLine1"/> is therefore NEVER written anywhere in this class and
/// always reads empty - the French game itself never shows a second description line in the
/// sub-inventory.</para>
/// </summary>
public sealed class AlundraSubInventoryDirector
{
    public static readonly AlundraSubInventoryDirector Instance = new();

    private AlundraSubInventoryDirector()
    {
    }

    // ---- g_subInventoryState (SubInventoryManager.cs, e.g. :23/334/391/393/396/398/414) ----
    private const int StateResidual = 1; // bit 0 - the residual bit InitializeSubInventory poses (5 = 4|1).
    private const int StateClosing = 2;  // bit 1 - set once FUN_800526cc has armed the CLOSING slide.
    private const int StateOpening = 4;  // bit 2 - set while the OPENING slide's tween is still active.

    /// <summary>Port of <c>g_subInventoryState</c> - 0 idle; 5 (bits 0+2) immediately after
    /// <see cref="OpenFromPostProcess"/>'s own setup; loses bit 2 when the opening slide's tween reaches
    /// its target, leaving 1; gains bit 1 when <see cref="RunInput"/>'s close branches arm the closing
    /// slide (flag becomes 3); reset to the literal 0 when the closing slide's tween reaches ITS target.</summary>
    public int State { get; private set; }

    /// <summary>True whenever the sub-inventory owns the screen at all (open through the last tick of the
    /// closing slide) - the complement of "idle, waiting for <see cref="OpenFromPostProcess"/>".</summary>
    public bool IsActive => State != 0;

    /// <summary>Same "AND" shape as <see cref="AlundraInventoryDirector.IsDrawn"/>: false on the tick
    /// <see cref="OpenFromPostProcess"/> itself runs (the post-process runs AFTER this director's own
    /// <see cref="Tick"/> for that same tick, plan §1.2 - so nothing is drawn by either inventory that
    /// tick) and false again from the close-completion tick on (this AND evaluates false on its own the
    /// moment <see cref="IsActive"/> goes back to false, no extra write needed).</summary>
    public bool IsDrawn => IsActive && _hasTickedSinceOpen;

    private bool _hasTickedSinceOpen;

    private AlundraGameState? _gameState;
    private AlundraItemTables? _itemTables;
    private IAlundraSoundPlayer? _soundPlayer;

    /// <summary>Re-points this session-scoped instance, same "re-point without touching state" contract as
    /// <see cref="AlundraInventoryDirector.AttachToWorld"/>. Every argument null is tolerated (no live
    /// session yet) - <see cref="Tick"/>/<see cref="OpenFromPostProcess"/> then degrade to no-ops.</summary>
    public void AttachToWorld(AlundraGameState? gameState, AlundraItemTables? itemTables, IAlundraSoundPlayer? soundPlayer)
    {
        _gameState = gameState;
        _itemTables = itemTables;
        _soundPlayer = soundPlayer;
    }

    /// <summary>Test-only: restores this session singleton to construction-equivalent state.</summary>
    internal void ResetForTests()
    {
        _gameState = null;
        _itemTables = null;
        _soundPlayer = null;

        State = 0;
        _hasTickedSinceOpen = false;
        SelectedPosition = 0; // plan §1.3: not reset between openings, but 0 at session start.
        ArmorName = null;
        BootsName = null;
        TextRevealState = 0;
        NameVisiblePrefix = string.Empty;
        Description0VisiblePrefix = string.Empty;
        Description1VisiblePrefix = string.Empty;
        DrawnDescriptionLine0 = string.Empty;
        DrawnDescriptionLine1 = string.Empty;
        _textRevealCountdown = 0;

        for (var i = 0; i < KeyItemIds.Length; i++)
        {
            KeyItemIds[i] = -1;
        }

        for (var i = 0; i < BoxCount; i++)
        {
            _boxes[i] = new BoxState(BoxLayout[i].OriginX, BoxLayout[i].OriginY);
            _tweens[i] = default;
        }
    }

    // =====================================================================================
    // The seven boxes (plan §1.3's own table) - PUBLIC index order (SI4's own contract, plan point 31):
    // 0 armory, 1 key items, 2 armor name, 3 boots name, 4 icons, 5 description, 6 money/falcon/key.
    // =====================================================================================
    private const int BoxArmory = 0;         // UIBoxConfiguration_800af664 - StaticVariables.cs:11279-11288
    private const int BoxKeyItems = 1;       // UIBoxConfiguration_800b06dc - :11289-11298
    private const int BoxArmorName = 2;      // UIBoxConfiguration_800b122c - :11299-11308
    private const int BoxBootsName = 3;      // UIBoxConfiguration_800b1d7c - :11309-11318
    private const int BoxIcons = 4;          // UIBoxConfiguration_800b287c - :11319-11328
    private const int BoxDescription = 5;    // g_uiBoxesInventoryDescriptionBackground (shared) - :11196-11203
    private const int BoxMoneyFalconKey = 6; // g_UiBoxesInventoryMoneyFalconKeyIcons (shared) - :11259-11266
    private const int BoxCount = 7;

    // (OriginX, OriginY, WidthCells, HeightCells) - plan §1.3's own table, StaticVariables.cs literals.
    private static readonly (int OriginX, int OriginY, int WidthCells, int HeightCells)[] BoxLayout =
    {
        (0x08, 0x10, 0x0F, 0x0E), // armory     - X=8 Y=16 W=15 H=14
        (0x08, 0x80, 0x15, 0x05), // key items  - X=8 Y=128 W=21 H=5
        (0xB0, 0x10, 0x12, 0x04), // armor name - X=176 Y=16 W=18 H=4
        (0xB0, 0x40, 0x12, 0x04), // boots name - X=176 Y=64 W=18 H=4
        (0x88, 0x10, 0x05, 0x0E), // icons      - X=136 Y=16 W=5 H=14
        (0x10, 0xA8, 0x24, 0x07), // description- X=16 Y=168 W=36 H=7
        (0xB0, 0x60, 0x03, 0x09), // money/etc  - X=176 Y=96 W=3 H=9
    };

    private const int TweenSpeed = 0xf; // 15 - InitializeSubInventory/FUN_800526cc (all seven boxes).
    private const short SlideInFromRightX = 0x140; // 320 - armor name/boots name/icons/money boxes.
    private const short SlideOffscreenBelowY = 0xf0; // 240 - description box (open source / close target).

    private readonly struct BoxState
    {
        public readonly int OriginX;
        public readonly int OriginY;
        public readonly int CurrentX;
        public readonly int CurrentY;

        public BoxState(int originX, int originY)
        {
            OriginX = originX;
            OriginY = originY;
            CurrentX = originX;
            CurrentY = originY;
        }

        public BoxState(int originX, int originY, int currentX, int currentY)
        {
            OriginX = originX;
            OriginY = originY;
            CurrentX = currentX;
            CurrentY = currentY;
        }
    }

    private struct BoxTween
    {
        public int Mode;
        public int Tick;
        public int SourceX;
        public int SourceY;
        public int TargetX;
        public int TargetY;
    }

    private readonly BoxState[] _boxes = new BoxState[BoxCount];
    private readonly BoxTween[] _tweens = new BoxTween[BoxCount];

    /// <summary>The seven boxes' current native (X, Y), in the PUBLIC index order documented above (plan
    /// point 31).</summary>
    public (int X, int Y) BoxPosition(int boxIndex) => (_boxes[boxIndex].CurrentX, _boxes[boxIndex].CurrentY);

    /// <summary>Own copy of <see cref="AlundraInventoryDirector"/>'s own <c>AdvanceBoxTween</c> (D-E13D-25's
    /// "own copy" philosophy applied to the box tween too, not just the text machine) - port of
    /// <c>UIManager.UpdateUiBoxesPosition</c>, same 18-call shape: returns true exactly when
    /// <see cref="BoxTween.Mode"/> was ALREADY 0 on entry.</summary>
    private static bool AdvanceBoxTween(ref BoxState box, ref BoxTween tween)
    {
        if (tween.Mode == 0)
        {
            return true;
        }

        int x, y;

        if (tween.Tick == TweenSpeed)
        {
            x = tween.TargetX;
            y = tween.TargetY;
            tween.Mode -= 1;
        }
        else
        {
            x = tween.SourceX + (tween.TargetX - tween.SourceX) * tween.Tick / TweenSpeed;
            y = tween.SourceY + (tween.TargetY - tween.SourceY) * tween.Tick / TweenSpeed;
            tween.Tick += 1;
        }

        box = new BoxState(box.OriginX, box.OriginY, x, y);
        return false;
    }

    // =====================================================================================
    // Selection / cursor (INT_8017f734 - StaticVariables.cs, plan §1.5's own table)
    // =====================================================================================

    /// <summary>Port of <c>INT_8017f734</c> - 0..13. NOT reset between openings (plan §1.3) - only by
    /// <see cref="ResetForTests"/>, same "0 at session start" the original's own zero-initialised global
    /// gives it.</summary>
    public int SelectedPosition { get; private set; }

    // Navigation tables (StaticVariables.cs:12321-12340), [binaire] confirmed equal in the executable
    // (plan §7, d7_tables_binary.py) - the arrival position for each of the 14 starting positions.
    private static readonly int[] NavRight = { 0x02, 0x00, 0x07, 0x06, 0x08, 0x04, 0x02, 0x01, 0x03, 0x0A, 0x0B, 0x0C, 0x0D, 0x09 };
    private static readonly int[] NavLeft = { 0x01, 0x07, 0x00, 0x08, 0x05, 0x03, 0x01, 0x02, 0x04, 0x0D, 0x09, 0x0A, 0x0B, 0x0C };
    private static readonly int[] NavUp = { 0x0A, 0x09, 0x0B, 0x01, 0x02, 0x06, 0x00, 0x0D, 0x07, 0x03, 0x05, 0x04, 0x04, 0x08 };
    private static readonly int[] NavDown = { 0x06, 0x03, 0x04, 0x09, 0x0B, 0x0A, 0x05, 0x08, 0x0D, 0x01, 0x00, 0x02, 0x02, 0x07 };

    // Cursor reference box per position (PTR_ARRAY_800b44b8) and its offset (INT_ARRAY_800b4368/800b43a0) -
    // for SI4's composer (plan §1.5). 0-6 armory, 7-8 icons, 9-13 key items.
    private static readonly int[] CursorBoxByPosition = { BoxArmory, BoxArmory, BoxArmory, BoxArmory, BoxArmory, BoxArmory, BoxArmory, BoxIcons, BoxIcons, BoxKeyItems, BoxKeyItems, BoxKeyItems, BoxKeyItems, BoxKeyItems };
    private static readonly int[] CursorOffsetX = { 0x42, 0x1A, 0x6A, 0x1A, 0x6A, 0x42, 0x42, 0x1C, 0x1C, 0x1C, 0x3C, 0x5C, 0x72, 0x92 };
    private static readonly int[] CursorOffsetY = { -4, 0x0C, 0x0C, 0x3C, 0x3C, 0x4C, 0x24, 0x10, 0x34, 0x00, 0x00, 0x00, 0x00, 0x00 };

    /// <summary>SI4: the cursor's own box index (this class' public <see cref="BoxPosition"/> order) and
    /// offset for <see cref="SelectedPosition"/> - <c>PTR_ARRAY_800b44b8</c>/<c>INT_ARRAY_800b4368</c>/
    /// <c>800b43a0</c> (plan §1.5).</summary>
    public (int BoxIndex, int OffsetX, int OffsetY) CursorReference(int position) =>
        (CursorBoxByPosition[position], CursorOffsetX[position], CursorOffsetY[position]);

    // Item described at each of the 14 positions (INT_ARRAY_800b4330) - 0x3E..0x44 armory, -1/-2
    // armor/boots (through GetItemIdFromSlotId(7)/(9)), -3..-7 the i-th key item found this tick.
    private static readonly int[] PositionItemTable = { 0x3E, 0x3F, 0x40, 0x41, 0x42, 0x43, 0x44, -1, -2, -3, -4, -5, -6, -7 };

    // =====================================================================================
    // The armory (FUN_80052dd8, SubInventoryManager.cs:1202-1236) - INT_ARRAY_800b42dc's own 7 item ids.
    // =====================================================================================
    private static readonly int[] ArmoryItemIds = { 0x3E, 0x3F, 0x40, 0x41, 0x42, 0x43, 0x44 };

    /// <summary>Port of <c>FUN_80052dd8</c>'s own owned-guard - whether armory position <paramref
    /// name="index"/> (0..6, Rubis..Diamant) is owned and therefore drawn.</summary>
    public bool ArmoryOwned(int index) =>
        _gameState != null && AlundraPlayerManager.GetNumberOfItem(_gameState, ArmoryItemIds[index]) != 0;

    // =====================================================================================
    // Armor / boots (FUN_80052f24 at open - names; FUN_80052fb4/FUN_80053144 every tick - icons)
    // =====================================================================================

    /// <summary>Port of <c>FUN_80052f24(0)</c>'s own resolved name - computed ONCE, at
    /// <see cref="OpenFromPostProcess"/> (plan §1.3), null when <c>GetItemIdFromSlotId(7)</c> resolves to
    /// <see cref="AlundraPlayerManager.NoItem"/> (nothing is drawn on that path either, in the original).</summary>
    public string? ArmorName { get; private set; }

    /// <summary>Same as <see cref="ArmorName"/>, slot 9 (<c>FUN_80052f24(1)</c>).</summary>
    public string? BootsName { get; private set; }

    /// <summary>Port of the armor icon's own item resolution (<c>FUN_80052fb4(0, ...)</c>,
    /// <c>GetItemIdFromSlotId(7)</c>) - re-resolved on every read (the original re-resolves it every
    /// drawn frame too), null when nothing is equipped there.</summary>
    public int? ArmorItemId => ResolveEquippedSlot(7);

    /// <summary>Same as <see cref="ArmorItemId"/>, slot 9 (<c>FUN_80053144(1, ...)</c>).</summary>
    public int? BootsItemId => ResolveEquippedSlot(9);

    private int? ResolveEquippedSlot(uint slotId)
    {
        if (_gameState == null || _itemTables == null)
        {
            return null;
        }

        var resolved = AlundraPlayerManager.GetItemIdFromSlotId(_gameState, _itemTables, slotId);
        return resolved == AlundraPlayerManager.NoItem ? null : (int)resolved;
    }

    // =====================================================================================
    // Key items (FUN_80052c64/FUN_8004e640, D-E13D-24 - the executable's own lost guard)
    // =====================================================================================

    /// <summary>Port of <c>INT_ARRAY_8017f628</c> - the (up to) five key items found this tick, -1 for an
    /// unfilled position. Recomputed from scratch every tick (plan §1.5: "recalculée à chaque image"),
    /// following the EXECUTABLE's own guard (D-E13D-24): once the search comes up empty, it is not
    /// restarted for the remaining positions - the decompilation's own loop, read literally, would
    /// restart the search from item 0 and duplicate key items into the later positions.</summary>
    public int[] KeyItemIds { get; } = { -1, -1, -1, -1, -1 };

    private void RecomputeKeyItems()
    {
        var itemId = 0;

        for (var i = 0; i < KeyItemIds.Length; i++)
        {
            KeyItemIds[i] = -1;

            // D-E13D-24: the executable tests itemId != -1 BEFORE calling the search again
            // (0x80052cc0: beq $s0, $s6, $s6 = -1) - the decompilation lost this guard and would call
            // FindItemWithSlot(0, 0x1c) again here, restarting from item 0 and duplicating entries.
            if (itemId != -1)
            {
                itemId = FindItemWithSlot(itemId + 1, 0x1c);

                if (itemId != -1)
                {
                    KeyItemIds[i] = itemId;
                }
            }
        }
    }

    /// <summary>Port of <c>FUN_8004e640</c> (<c>SubInventoryManager.cs:1284-1310</c>) - the first item id
    /// &gt;= <paramref name="startIndex"/>, &lt; the item count, whose inventory-slot column equals
    /// <paramref name="wantedSlot"/> and that is owned (<c>GetNumberOfItem &gt; 0</c>), else -1.</summary>
    private int FindItemWithSlot(int startIndex, int wantedSlot)
    {
        if (_gameState == null || _itemTables == null)
        {
            return -1;
        }

        for (var id = startIndex; id < AlundraItemTables.ItemRowCount; id++)
        {
            if (_itemTables.ItemsProperties[id * AlundraItemTables.ItemColumnCount] == wantedSlot
                && AlundraPlayerManager.GetNumberOfItem(_gameState, id) > 0)
            {
                return id;
            }
        }

        return -1;
    }

    // =====================================================================================
    // Text reveal (FUN_80053fdc, SubInventoryManager.cs:468-629) - own copy, D-E13D-25.
    // =====================================================================================

    /// <summary>Port of <c>INT_8017f788</c> - same state shape as
    /// <see cref="AlundraInventoryDirector.TextRevealState"/> (0 name setup, 1..0x10 name reveal,
    /// 0x11..0x4c hold, 0x4d desc-line-0 setup, 0x4e..0x8d desc-line-0 reveal, 0x8e desc-line-1 setup,
    /// 0x8f..0xce desc-line-1 "reveal" - see this class' own doc, D-E13D-28, 0xcf done).</summary>
    public int TextRevealState { get; private set; }

    public string NameVisiblePrefix { get; private set; } = string.Empty;
    public string Description0VisiblePrefix { get; private set; } = string.Empty;

    /// <summary>D-E13D-28: NEVER written to anything but the empty string - see this class' own doc.</summary>
    public string Description1VisiblePrefix { get; private set; } = string.Empty;

    /// <summary>SI4: what this tick drew on line 0 - the name during its reveal/hold, the first
    /// description line from state 0x4e, empty otherwise.</summary>
    public string DrawnDescriptionLine0 { get; private set; } = string.Empty;

    /// <summary>D-E13D-28: ALWAYS empty - the sub-inventory never draws a second description line.</summary>
    public string DrawnDescriptionLine1 { get; private set; } = string.Empty;

    /// <summary>Port of <c>INT_8017f78c</c> (<c>FUN_80053f3c</c>) - this machine's OWN 3-tick countdown,
    /// not shared with <see cref="AlundraInventoryDirector"/>'s own (D-E13D-25).</summary>
    private int _textRevealCountdown;

    /// <summary>Port of <c>FUN_80053fdc</c>'s own item resolution (<c>:474-516</c>) - see this class' own
    /// doc for the 14-entry table. <c>value &lt; -7</c> is UNREACHABLE with this table (the decompiled
    /// "goto LAB_80054088" branch it guards is only ever reached, in practice, through the <c>value &gt;=
    /// 0</c> path already handled below) - documented here rather than ported, per the brief's own
    /// allowance.</summary>
    private bool TryResolveDescribedItem(out int itemId)
    {
        itemId = 0;
        var value = PositionItemTable[SelectedPosition];

        if (value >= 0)
        {
            if (_gameState == null || AlundraPlayerManager.GetNumberOfItem(_gameState, value) == 0)
            {
                return false;
            }

            itemId = value;
            return true;
        }

        if (value == -1 || value == -2)
        {
            if (_gameState == null || _itemTables == null)
            {
                return false;
            }

            var slotId = value == -1 ? 7u : 9u;
            var resolved = AlundraPlayerManager.GetItemIdFromSlotId(_gameState, _itemTables, slotId);

            if (resolved == AlundraPlayerManager.NoItem)
            {
                return false;
            }

            itemId = (int)resolved;
            return true;
        }

        // -3..-7: the i-th key item found this tick (RecomputeKeyItems runs before this in the draw tail).
        var keyIndex = -3 - value;
        var keyItemId = KeyItemIds[keyIndex];

        if (keyItemId == -1)
        {
            return false;
        }

        itemId = keyItemId;
        return true;
    }

    private void RunTextReveal()
    {
        DrawnDescriptionLine0 = string.Empty;

        if (!TryResolveDescribedItem(out var itemId))
        {
            return; // :479-486/501-505/512-515 - unowned/empty position: nothing drawn, text state frozen.
        }

        var cursor = TextRevealState;
        var projectPath = EngineEnvironment.ProjectPath;

        // :522-530 - state 0: load the name, nothing revealed yet.
        if (cursor == 0)
        {
            NameVisiblePrefix = string.Empty;
            _textRevealCountdown = 0;
            TextRevealState = cursor + 1;
            return;
        }

        // :533-549 - states 1..0x10: reveal the name one character at a time.
        if ((uint)(cursor - 1) < 0x10)
        {
            AlundraEtcStringTable.TryResolveItemName(projectPath, itemId, out var name);

            if (cursor - 1 >= name.Length)
            {
                TextRevealState = 0x11;
            }
            else
            {
                AdvanceTextReveal();
                NameVisiblePrefix = RevealedPrefix(name, TextRevealState - 1);
            }

            DrawnDescriptionLine0 = NameVisiblePrefix;
            return;
        }

        // :552-559 - states 0x11..0x4c: hold the name on screen.
        if ((uint)(cursor - 0x11) < 0x3c)
        {
            TextRevealState = cursor + 1;
            DrawnDescriptionLine0 = NameVisiblePrefix;
            return;
        }

        // :562-570 - state 0x4d: switch line 0 from the name to the first description line.
        if (cursor == 0x4d)
        {
            Description0VisiblePrefix = string.Empty;
            _textRevealCountdown = 0;
            TextRevealState = cursor + 1;
            return;
        }

        // :573-589 - states 0x4e..0x8d: reveal the first description line.
        if ((uint)(cursor - 0x4e) < 0x40)
        {
            AlundraEtcStringTable.TryResolveItemDescriptionLine0(projectPath, itemId, out var firstLine);

            if (cursor - 0x4e >= firstLine.Length)
            {
                TextRevealState = 0x8e;
            }
            else
            {
                AdvanceTextReveal();
                Description0VisiblePrefix = RevealedPrefix(firstLine, TextRevealState - 0x4e);
            }

            DrawnDescriptionLine0 = Description0VisiblePrefix;
            return;
        }

        // :592-601 - state 0x8e: switch to the second description line (never actually revealed - below).
        if (cursor == 0x8e)
        {
            Description1VisiblePrefix = string.Empty;
            _textRevealCountdown = 0;
            TextRevealState = cursor + 1;
            DrawnDescriptionLine0 = Description0VisiblePrefix;
            return;
        }

        // :604-621 - states 0x8f..0xce: D-E13D-28 - the executable reads line2[c - 0x90], one byte before
        // the intended c - 0x8f. At c == 0x8f that index is -1 (the previous string's own null
        // terminator, measured 0 for all 98 items - plan §7): treated as immediate end of string, so the
        // state jumps straight to 0xcf and DrawnDescriptionLine1/Description1VisiblePrefix are never
        // written to anything but empty. The rest of this branch (c > 0x8f) is dead code in practice, same
        // as in the original: once c == 0x8f jumps to 0xcf, this range is never entered again for this
        // item's reveal.
        if ((uint)(cursor - 0x8f) < 0x40)
        {
            var index = cursor - 0x90;

            if (index < 0
                || !AlundraEtcStringTable.TryResolveItemDescriptionLine1(projectPath, itemId, out var secondLine)
                || index >= secondLine.Length)
            {
                TextRevealState = 0xcf;
            }
            else
            {
                AdvanceTextReveal();
                // Intentionally NOT assigned to Description1VisiblePrefix/DrawnDescriptionLine1 - D-E13D-28:
                // the sub-inventory never shows a second description line in the French game.
            }

            DrawnDescriptionLine0 = Description0VisiblePrefix;
            return;
        }

        // :624-628 - cursor == 0xcf: line 0 is complete and drawn; line 1 stays empty (D-E13D-28).
        if (cursor == 0xcf)
        {
            DrawnDescriptionLine0 = Description0VisiblePrefix;
        }
    }

    /// <summary>Port of <c>FUN_80053f3c</c>'s own countdown (<c>SubInventoryManager.cs:662-691</c>) -
    /// commits one character every third tick, this machine's own countdown (D-E13D-25).</summary>
    private void AdvanceTextReveal()
    {
        if (_textRevealCountdown != 0)
        {
            _textRevealCountdown -= 1;
            return;
        }

        _textRevealCountdown = 2;
        TextRevealState += 1;
    }

    /// <summary>Same clamp as <see cref="AlundraInventoryDirector"/>'s own <c>RevealedPrefix</c> - never
    /// split an escape pair across the visible/hidden boundary.</summary>
    private static string RevealedPrefix(string text, int visibleLength)
    {
        visibleLength = Math.Clamp(visibleLength, 0, text.Length);

        if (visibleLength > 0 && (text[visibleLength - 1] == '{' || text[visibleLength - 1] == '}'))
        {
            visibleLength -= 1;
        }

        return text.Substring(0, visibleLength);
    }

    // =====================================================================================
    // Open (post-process only) / Tick
    // =====================================================================================

    /// <summary>Port of <c>StartFadeOut</c> (<c>GraphicManager.cs:1767-1783</c>) WITHOUT the portrait
    /// (D-E13D-12 amended, same choice <see cref="AlundraInventoryDirector"/> already made) - called ONLY
    /// from <see cref="AlundraInventoryPostProcess.Run"/>, never directly. <see cref="AlundraHudDirector.InitializeHudPosition"/>
    /// first (a guarded no-op while the gauge is hidden, same call <c>DisplayInventory</c>'s own head
    /// makes), then <c>InitializeSubInventory</c> (<c>SubInventoryManager.cs:21-294</c>, plan §1.3): state
    /// 5, text state reset, MenuOpen raised, the seven boxes' opening tweens armed, then the armor/boots
    /// names resolved ONCE (<c>FUN_80052f24(0)</c>/<c>(1)</c>), then sound 4.</summary>
    internal void OpenFromPostProcess()
    {
        if (_gameState == null)
        {
            return;
        }

        AlundraHudDirector.Instance.InitializeHudPosition();

        State = StateResidual | StateOpening;
        TextRevealState = 0;
        NameVisiblePrefix = string.Empty;
        Description0VisiblePrefix = string.Empty;
        Description1VisiblePrefix = string.Empty;
        DrawnDescriptionLine0 = string.Empty;
        DrawnDescriptionLine1 = string.Empty;
        _textRevealCountdown = 0;
        _hasTickedSinceOpen = false;

        _gameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;

        ArmTweensForOpen();

        var armorId = ResolveEquippedSlot(7);
        ArmorName = armorId.HasValue && AlundraEtcStringTable.TryResolveItemName(EngineEnvironment.ProjectPath, armorId.Value, out var armorName)
            ? armorName
            : null;

        var bootsId = ResolveEquippedSlot(9);
        BootsName = bootsId.HasValue && AlundraEtcStringTable.TryResolveItemName(EngineEnvironment.ProjectPath, bootsId.Value, out var bootsName)
            ? bootsName
            : null;

        _soundPlayer?.PlaySfx(4);
    }

    /// <summary>One LOGIC tick - only meaningful while <see cref="IsActive"/> (this director never reads
    /// the pad to open itself, only <see cref="OpenFromPostProcess"/> does that).</summary>
    public void Tick()
    {
        if (_gameState == null || !IsActive)
        {
            return;
        }

        // D-E13D (same shape as AlundraInventoryDirector's own _hasRunPerFrameSinceSetup): set
        // unconditionally on entry, so IsDrawn is true from this tick on - the AND with IsActive alone
        // hides it again at the close-completion tick (see this class' own doc on IsDrawn).
        _hasTickedSinceOpen = true;

        if ((State & (StateClosing | StateOpening)) != 0)
        {
            if (!RunBoxSlide())
            {
                return; // close completion this tick - nothing drawn (SubInventoryManager.cs:420).
            }
        }
        else
        {
            RunInput();
        }

        // :425-464 - the draw tail, every tick except the one a closing slide just completed on.
        RecomputeKeyItems();
        RunTextReveal();
    }

    /// <summary>Port of the pad-input half of <c>DisplaySubInventory</c> (<c>:336-377</c>).</summary>
    private void RunInput()
    {
        var pad = _gameState!.TickPad;

        if ((pad.ButtonsJustPressedByInterval & AlundraPadState.Right) != 0)
        {
            SelectedPosition = NavRight[SelectedPosition];
            TextRevealState = 0;
            _soundPlayer?.PlaySfx(1);
        }

        if ((pad.ButtonsJustPressedByInterval & AlundraPadState.Left) != 0)
        {
            SelectedPosition = NavLeft[SelectedPosition];
            TextRevealState = 0;
            _soundPlayer?.PlaySfx(1);
        }

        if ((pad.ButtonsJustPressedByInterval & AlundraPadState.Up) != 0)
        {
            SelectedPosition = NavUp[SelectedPosition];
            TextRevealState = 0;
            _soundPlayer?.PlaySfx(1);
        }

        if ((pad.ButtonsJustPressedByInterval & AlundraPadState.Down) != 0)
        {
            SelectedPosition = NavDown[SelectedPosition];
            TextRevealState = 0;
            _soundPlayer?.PlaySfx(1);
        }

        // :364-369 - Start/Triangle/L2/R2 (mask 0x813, the executable's own - plan §1.1/D-E13D-29, same
        // mask AlundraInventoryDirector.RunInput uses): close, hand the gauge back.
        if ((pad.ButtonsJustPressedByInterval & (AlundraPadState.Start | AlundraPadState.Triangle | AlundraPadState.L2 | AlundraPadState.R2)) != 0)
        {
            RunCloseSetup();
            AlundraHudDirector.Instance.InitializeHudPositionBeforeHide();
        }

        // :371-377 - L1/R1: close, hand control back to the MAIN inventory through the post-process.
        if ((pad.ButtonsJustPressedByInterval & (AlundraPadState.L1 | AlundraPadState.R1)) != 0)
        {
            RunCloseSetup();
            _gameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;
            AlundraInventoryPostProcess.Instance.State = 2;
        }
    }

    /// <summary>Port of the seven <c>UpdateUiBoxesPosition</c> calls (<c>:381-421</c>) in their own literal
    /// order - key items, armor name, boots name, icons, description, money/falcon/key, THEN the armory,
    /// LAST, whose return alone gates completion. Returns false when the close-completion branch already
    /// ran (caller skips the draw tail, matching the original's own early <c>return</c> at <c>:420</c>).</summary>
    private bool RunBoxSlide()
    {
        AdvanceBoxTween(ref _boxes[BoxKeyItems], ref _tweens[BoxKeyItems]);
        AdvanceBoxTween(ref _boxes[BoxArmorName], ref _tweens[BoxArmorName]);
        AdvanceBoxTween(ref _boxes[BoxBootsName], ref _tweens[BoxBootsName]);
        AdvanceBoxTween(ref _boxes[BoxIcons], ref _tweens[BoxIcons]);
        AdvanceBoxTween(ref _boxes[BoxDescription], ref _tweens[BoxDescription]);
        AdvanceBoxTween(ref _boxes[BoxMoneyFalconKey], ref _tweens[BoxMoneyFalconKey]);
        var armorySettled = AdvanceBoxTween(ref _boxes[BoxArmory], ref _tweens[BoxArmory]);

        if (!armorySettled)
        {
            return true; // still sliding - the tail still runs every tick during the slide.
        }

        // :391-394 - the opening slide's own completion: drop bit 2 (4 -> ... -> keep bit 0, 5 -> 1).
        if ((State & StateOpening) != 0)
        {
            State &= ~StateOpening;
        }

        // :396-420 - the closing slide's own completion.
        if ((State & StateClosing) != 0)
        {
            State = 0;

            for (var i = 0; i < BoxCount; i++)
            {
                _boxes[i] = new BoxState(_boxes[i].OriginX, _boxes[i].OriginY);
            }

            // :414-417 - MenuOpen cleared ONLY if the post-process is not about to open the main inventory.
            if ((AlundraInventoryPostProcess.Instance.State & 2) == 0)
            {
                _gameState!.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.MenuOpen;
            }

            return false; // :420 - return (skip the draw tail this same tick).
        }

        return true;
    }

    /// <summary>Port of <c>FUN_800526cc</c> (<c>SubInventoryManager.cs:856-1116</c>) - arms the seven-box
    /// reverse slide. Same origins/sizes <see cref="OpenFromPostProcess"/> uses; only the tween's
    /// source/target swap ends (plan §1.3's own "Sortie" column - same side as the entry, reversed).</summary>
    private void RunCloseSetup()
    {
        State |= StateClosing;
        _soundPlayer?.PlaySfx(5);
        ArmTweensForClose();
    }

    /// <summary>Arms the seven boxes' opening tweens - left for armory/key items, right for armor
    /// name/boots name/icons/money, bottom for description (plan §1.3's own "Entrée" column).</summary>
    private void ArmTweensForOpen()
    {
        for (var i = 0; i < BoxCount; i++)
        {
            var layout = BoxLayout[i];
            int sourceX, sourceY, targetX, targetY;

            if (i == BoxArmory || i == BoxKeyItems)
            {
                sourceX = ~(layout.WidthCells << 3);
                sourceY = layout.OriginY;
                targetX = layout.OriginX;
                targetY = layout.OriginY;
            }
            else if (i == BoxDescription)
            {
                sourceX = layout.OriginX;
                sourceY = SlideOffscreenBelowY;
                targetX = layout.OriginX;
                targetY = layout.OriginY;
            }
            else
            {
                sourceX = SlideInFromRightX;
                sourceY = layout.OriginY;
                targetX = layout.OriginX;
                targetY = layout.OriginY;
            }

            _tweens[i] = new BoxTween { Mode = 2, Tick = 0, SourceX = sourceX, SourceY = sourceY, TargetX = targetX, TargetY = targetY };
        }
    }

    /// <summary>Arms the seven boxes' closing tweens - the reverse of <see cref="ArmTweensForOpen"/>,
    /// same side each box entered by.</summary>
    private void ArmTweensForClose()
    {
        for (var i = 0; i < BoxCount; i++)
        {
            var layout = BoxLayout[i];
            int targetX, targetY;

            if (i == BoxArmory || i == BoxKeyItems)
            {
                targetX = ~(layout.WidthCells << 3);
                targetY = layout.OriginY;
            }
            else if (i == BoxDescription)
            {
                targetX = layout.OriginX;
                targetY = SlideOffscreenBelowY;
            }
            else
            {
                targetX = SlideInFromRightX;
                targetY = layout.OriginY;
            }

            _tweens[i] = new BoxTween { Mode = 2, Tick = 0, SourceX = layout.OriginX, SourceY = layout.OriginY, TargetX = targetX, TargetY = targetY };
        }
    }
}
