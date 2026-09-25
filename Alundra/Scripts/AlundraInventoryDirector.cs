#nullable enable
using System;
using CasaEngine.Engine.Environment;

namespace Alundra.Scripts;

/// <summary>
/// E13.d D4 (docs/plan-e13d-inventaire.md, D-E13D-1/D-E13D-6): a pure, tick-driven port of the
/// original's main inventory (<c>MainInventoryManager</c>) - the DIRECTOR half of the director/
/// presenter/screen split D-E13D-6 chose (same shape as <see cref="AlundraDialogueDirector"/> and
/// <see cref="AlundraHudDirector"/>). No MGUI dependency at all: every piece of state D5's screen will
/// need to draw is exposed as plain, read-only properties.
///
/// <para><b>The trigger</b> - ported from <c>GameEngine.cs:1567-1576</c>, checked once per logic tick
/// while the inventory is idle. Three of its six guards have no equivalent in this port and are
/// TREATED AS ALWAYS ZERO (never block the trigger), each declared where it is tested below:
/// <c>g_warpLockTimer</c> (an item-use/magic-sequence lock, <c>PlayerManager.cs:1929-4013</c> - no such
/// system is ported here at all), <c>g_warpDelayFrames</c> (<see cref="AlundraWarpDirector.WarpDelayFramesForTests"/>
/// exists but is never decremented anywhere in this DLL - see that field's own doc, which already says
/// its two original consumers, this trigger among them, were never wired up before this chantier), and
/// <c>g_globalTransitionState</c> (the memory-card/save-menu state machine, <c>UI/MemoryCardManager.cs</c>
/// - not ported at all).</para>
///
/// <para><b>The setup callback's timing</b> (D4's own open point): <c>DisplayInventory</c>
/// (<c>MainInventoryManager.cs:443-499</c>) only ARMS the setup callback, through
/// <c>GraphicManager.SetTransitionType(6)</c> (<c>GraphicManager.cs:1710-1742</c>): that call copies the
/// slot's <c>RenderFunc</c> (<c>FUN_80054f1c</c>, <c>StaticVariables.cs:11432</c>) and sets its
/// <c>Flags |= 1</c>, but slot 6's <c>InitializeFunc</c> is <c>null</c>
/// (<c>StaticVariables.cs:11432</c>), so <c>SetTransitionType</c> itself never invokes anything. The
/// callback only runs from <c>GraphicManager.UpdateUserInterface</c>'s own callback-table loop
/// (<c>GraphicManager.cs:1672-1685</c>), called once per rendered frame from <c>RenderScene</c>
/// (<c>GraphicManager.cs:63</c>). The original's main loop runs <c>RenderScene()</c> BEFORE
/// <c>Update(0)</c> (<c>GameEngine.cs:225-229</c>), and the trigger lives in <c>Update</c>
/// (<c>:1567-1576</c>): the trigger fires in <c>Update</c> N, <c>FUN_80054f1c</c> runs in the render of
/// frame N+1, and the first <c>FUN_80056598</c> (the per-frame handler <c>FUN_80054f1c</c> installs as the
/// callback's new <c>RenderFunc</c>, <c>MainInventoryManager.cs:775</c>) in the render of frame N+2. Taking
/// one port tick as one original <c>Update</c> followed by the next frame's render, that is: trigger and
/// setup on tick N, the per-frame handler from tick N+1 - and MenuOpen, posed by the setup, is seen by the
/// logic of tick N+1 in both. This port reproduces that: <see cref="Tick"/> runs the trigger,
/// <c>DisplayInventory</c> and <c>FUN_80054f1c</c> bodies all synchronously on the SAME call when the
/// trigger fires, and the box-slide/input dispatch (<c>FUN_80056598</c>) only from the NEXT call.</para>
///
/// <para><b>Not ported</b> (D-E13D-12 amended, portrait reporter): <c>InitializeHudTransitionVariablesAndSetStart</c>/
/// <c>InitializeHudTransitionVariables</c>/<c>UpdateHudTransitionVariables</c>/<c>UpdateHudTransitionState</c>
/// and the portrait quad draw (<c>MainInventoryManager.cs:197-440</c>) - the opening portrait is absent
/// from the export (plan §1.4, D0.9) and its own slices D3.c/D5.p were retired. Whether skipping it
/// delayed anything in the original's own timeline: NO - <c>InitializeHudTransitionVariables</c>
/// (:209-299) only ever WRITES portrait-local state (the polygon corners, <c>g_hudTransitionState</c>,
/// <c>g_hudCurrentX/Y</c>, ...) and is never read by anything this director's own state machine reads;
/// <c>UpdateHudTransitionState</c> (:403-418, called from <c>FUN_80056598</c> on close) is the SAME -
/// portrait-local only. Neither one touches <c>g_forbiddenWarpFlag</c>, the box tweens, or
/// <c>g_inventoryCursorText</c>, so the seven-box slide and the text reveal run on their own clock,
/// unaffected by the portrait's absence.</para>
///
/// <para><b>L1/R1 (the sub-inventory switch, <c>MainInventoryManager.cs:853-859</c>)</b> (E13.d SI3,
/// docs/plan-e13d-sous-inventaire.md, D-E13D-21/22): closes the main inventory (<see cref="RunCloseSetup"/>,
/// sound 5) and poses <see cref="AlundraInventoryPostProcess.State"/> = 1 - it does NOT call
/// <see cref="AlundraHudDirector.InitializeHudPositionBeforeHide"/> (the gauge stays hidden, plan §1.1).
/// <see cref="AlundraInventoryPostProcess.Run"/> then opens the sub-inventory once this closing slide
/// settles (<see cref="IsCallbackArmed"/> false) - see that class' own doc for the two-tick clock (plan
/// §1.2). The reverse switch (<see cref="AlundraSubInventoryDirector"/>'s own L1/R1) hands control back
/// through <see cref="RunDisplayInventoryHeadFromPostProcess"/>: the HEAD of <c>DisplayInventory</c> runs
/// on the post-process's own tick, and the SETUP (<c>FUN_80054f1c</c>) only on the NEXT one
/// (<see cref="_setupPending"/>, D-E13D-22) - unlike the ordinary trigger path, where <see cref="Tick"/>
/// runs both on the SAME tick (this class' own doc above).</para>
/// </summary>
public sealed class AlundraInventoryDirector
{
    public static readonly AlundraInventoryDirector Instance = new();

    private AlundraInventoryDirector()
    {
    }

    // ---- g_forbiddenWarpFlag (MainInventoryManager.cs:30/445/505/783/873/875/878/880/1903) ----
    // Grepped for readers outside MainInventoryManager.cs: none found - it is inventory-private in the
    // decompilation too, so it stays private state here (plan D4's own instruction: "keep it in the
    // director unless you find a reader outside the inventory").
    private const uint SlideOpenBit = 4;  // bit 2 - set while the OPENING slide's tween is still active.
    private const uint SlideCloseBit = 2; // bit 1 - set once FUN_800556dc has armed the CLOSING slide.
    private const uint SetupBit = 1;      // bit 0 - the residual bit FUN_80054f1c poses (5 = 4|1) that
                                           // outlives the opening slide (never cleared except by the
                                           // close-completion's explicit "= 0", MainInventoryManager.cs:880).

    /// <summary>Port of <c>g_forbiddenWarpFlag</c> - 0 idle; 5 (bits 0+2) immediately after
    /// <c>FUN_80054f1c</c>'s own setup; loses bit 2 when the opening slide's tween reaches its target
    /// (<c>&amp;= ~4</c>, :875), leaving 1; gains bit 1 when <c>FUN_800556dc</c> arms the closing slide
    /// (<c>|= 2</c>, :1903 - flag becomes 3); reset to the literal 0 when the closing slide's tween
    /// reaches ITS target (:880). D5 reads this to tell "sliding" from "settled".</summary>
    public uint ForbiddenWarpFlag { get; private set; }

    /// <summary>True whenever the inventory owns the screen at all (setup through the last tick of the
    /// closing slide) - <see cref="ForbiddenWarpFlag"/> != 0. The complement of "idle, waiting for the
    /// trigger".</summary>
    public bool IsActive => ForbiddenWarpFlag != 0;

    /// <summary>E13.d SI3 (docs/plan-e13d-sous-inventaire.md, D-E13D-21): "is the main inventory's own
    /// callback slot 6 armed" - what <see cref="AlundraInventoryPostProcess.Run"/> tests as "slot 6 free"
    /// before opening the sub-inventory (state 1). Ported as <see cref="ForbiddenWarpFlag"/> != 0 (the
    /// slide/residual bits) OR <see cref="_setupPending"/> (the callback is armed the instant
    /// <c>SetTransitionType(6)</c> runs, inside <see cref="RunDisplayInventoryHead"/>/
    /// <see cref="RunDisplayInventoryHeadFromPostProcess"/>, one tick BEFORE <see cref="ForbiddenWarpFlag"/>
    /// itself is set by the setup - see <see cref="RunDisplayInventoryHeadFromPostProcess"/>'s own doc).</summary>
    internal bool IsCallbackArmed => ForbiddenWarpFlag != 0 || _setupPending;

    /// <summary>D5 addition (docs/plan-e13d-inventaire.md, "Ce que D5 lit"): a minimal read-only fact
    /// this class did not expose before - whether <see cref="RunPerFrame"/> has run at least once since
    /// the current <see cref="RunDisplayInventory"/> setup. Added rather than changing any existing
    /// logic: <see cref="RunDisplayInventory"/> now also clears this flag and <see cref="RunPerFrame"/>
    /// sets it on entry, nothing else changes. Ports this class' own doc note ("At the setup tick,
    /// IsActive is already true but the boxes are still at their origin: the original draws nothing at
    /// this tick") into something D5's screen can read directly instead of re-deriving it: the screen
    /// shows nothing while <see cref="IsActive"/> is true but this is still false, and hides again the
    /// moment <see cref="IsActive"/> goes back to false (the AND below then evaluates false on its
    /// own, with no extra write needed at close).</summary>
    public bool IsDrawn => IsActive && _hasRunPerFrameSinceSetup;

    private bool _hasRunPerFrameSinceSetup;

    private AlundraGameState? _gameState;
    private AlundraItemTables? _itemTables;
    private IAlundraSoundPlayer? _soundPlayer;

    /// <summary>Re-points this session-scoped instance, same "re-point without touching state" contract
    /// as <see cref="AlundraHudDirector.AttachToWorld"/>/<see cref="AlundraDialogueDirector.AttachToWorld"/>.
    /// Every argument null is tolerated (no live session yet) - <see cref="Tick"/> then degrades to a
    /// no-op, same shape as every other missing-system seam in this DLL.</summary>
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

        ForbiddenWarpFlag = 0;
        SelectedSlotId = 0;
        CursorFrameDelay = 0;
        EquippedWeaponName = string.Empty;
        EquippedItemName = string.Empty;
        TextRevealState = 0;
        NameVisiblePrefix = string.Empty;
        Description0VisiblePrefix = string.Empty;
        Description1VisiblePrefix = string.Empty;
        DrawnDescriptionLine0 = string.Empty;
        DrawnDescriptionLine1 = string.Empty;
        _textRevealCountdown = 0;
        _setupPending = false;
        _hasRunPerFrameSinceSetup = false;

        for (var i = 0; i < BoxLayout.Length; i++)
        {
            _boxes[i] = new BoxState(BoxLayout[i].OriginX, BoxLayout[i].OriginY);
            _tweens[i] = default;
        }
    }

    // =====================================================================================
    // The seven UI boxes (StaticVariables.cs:11196-11267, D-E13D-13/14's own six sprites plus the
    // 0x0 spacer, UIBoxConfiguration_800b9a10) - order = the seven DisplayUiBoxes calls at
    // MainInventoryManager.cs:914-920, identical to TextToDisplay_ARRAY_8017f920's own index order.
    // =====================================================================================
    private const int BoxWeapon = 0;
    private const int BoxItem = 1;
    private const int BoxWeaponName = 2;
    private const int BoxItemName = 3;
    private const int BoxSpacer = 4;
    private const int BoxMoneyFalconKey = 5;
    private const int BoxDescription = 6;
    private const int BoxCount = 7;

    private static readonly (int OriginX, int OriginY, int WidthCells, int HeightCells)[] BoxLayout =
    {
        (0x08, 0x10, 0x15, 0x06), // weapon background      - StaticVariables.cs:11207-11214
        (0x08, 0x40, 0x15, 0x0D), // item background         - StaticVariables.cs:11216-11224
        (0xB0, 0x10, 0x12, 0x04), // weapon name background  - StaticVariables.cs:11226-11233
        (0xB0, 0x40, 0x12, 0x04), // item name background    - StaticVariables.cs:11235-11242
        (0xF0, 0x70, 0x00, 0x00), // spacer (0x0)             - StaticVariables.cs:11244-11251
        (0xB0, 0x60, 0x03, 0x09), // money/falcon/key icons   - StaticVariables.cs:11253-11260
        (0x10, 0xA8, 0x24, 0x07), // description background   - StaticVariables.cs:11196-11203
    };

    private const int TweenSpeed = 0xf; // 15 - MainInventoryManager.cs:509,551,591,629,667,705,742 (all seven boxes).

    // The literal seven-space blank DisplayIconNames builds for "no item" (0x80026850, "       \0").
    private const string BlankName = "       ";
    private const short SlideInFromRightX = 0x140; // 320 - MainInventoryManager.cs:583,621,659,699 (boxes 2,3,4,5).
    private const short SlideOffscreenBelowY = 0xf0; // 240 - MainInventoryManager.cs:757 (box 6, open source / close target).

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

    // Port of TextToDisplay (StaticVariables.cs, TextToDisplay_ARRAY_8017f920's own element shape):
    // mode/tick/speed plus the tween's source (x/y) and target (startX/startY) - "origin" lives on
    // BoxState instead, since it never changes once a box is laid out (D3.a/D3.b's own fixed layout).
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

    /// <summary>The seven boxes' current native (X, Y), in <c>DisplayUiBoxes</c> order
    /// (<c>MainInventoryManager.cs:914-920</c>) - box 4 is the 0x0 spacer, nothing to draw there.</summary>
    public (int X, int Y) BoxPosition(int boxIndex) => (_boxes[boxIndex].CurrentX, _boxes[boxIndex].CurrentY);

    /// <summary>Port of <c>UIManager.UpdateUiBoxesPosition</c> (<c>UIManager.cs:968-993</c>), same
    /// simplification <see cref="AlundraHudDirector"/>'s own <c>AdvancePositionTween</c> makes (X and Y
    /// both, here, instead of Y alone): the sprite-grid loop that follows the tween in the original
    /// always contributes 0 to its own returned <c>result</c> for a box with a nonzero size (the loop
    /// unconditionally reassigns <c>result = 0</c> every row, UIManager.cs:1021) and the spacer box
    /// (0x0) never enters that loop at all - so the ONLY case this port's own <c>true</c> return covers,
    /// <c>mode == 0</c> on entry, is the only case the original's <c>return 1</c> at UIManager.cs:975
    /// ever reaches too. Returns true exactly when <see cref="BoxTween.Mode"/> was ALREADY 0 on entry -
    /// two calls after the interpolation reached its target (one call snaps to the target and decrements
    /// mode 2-&gt;1, the next call re-snaps and decrements 1-&gt;0, only the call after THAT returns
    /// true) - same "18-call" shape <see cref="AlundraHudDirector"/>'s own tween has.</summary>
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
    // Selection / cursor
    // =====================================================================================

    /// <summary>Port of <c>g_inventorySelectedSlotId</c> - 0..23, row-major over the 6x4 grid
    /// (<c>MainInventoryManager.cs:1292-1456</c>): row 0 is the five weapon slots plus the sword-class
    /// item slot, rows 1-3 are items.</summary>
    public int SelectedSlotId { get; private set; }

    /// <summary>Port of <c>g_inventoryCursorAnimation.FrameDelay</c> (<c>MainInventoryManager.cs:1591-1615</c>,
    /// <c>DisplayInventoryCursor</c>) - 0..0x27, incremented once per active tick (drawn or not sliding),
    /// wraps at 0x28. D5's own 4-frame cursor animation reads <c>FrameDelay / 10</c>.</summary>
    public int CursorFrameDelay { get; private set; }

    // =====================================================================================
    // Equipped weapon/item names (DisplayIconNames, MainInventoryManager.cs:1750-1793) - D5 draws these
    // as text through font3, so only the resolved STRINGS are exposed, never sprite lists.
    // =====================================================================================
    public string EquippedWeaponName { get; private set; } = string.Empty;
    public string EquippedItemName { get; private set; } = string.Empty;

    // =====================================================================================
    // Text reveal (DisplayInventoryTexts, MainInventoryManager.cs:929-1064)
    // =====================================================================================

    /// <summary>Port of <c>g_inventoryCursorText</c> - the raw state value: 0 name setup, 1..0x10 name
    /// reveal, 0x11..0x4c hold, 0x4d desc-line-0 setup, 0x4e..0x8d desc-line-0 reveal, 0x8e desc-line-1
    /// setup, 0x8f..0xce desc-line-1 reveal, 0xcf done.</summary>
    public int TextRevealState { get; private set; }

    public string NameVisiblePrefix { get; private set; } = string.Empty;
    public string Description0VisiblePrefix { get; private set; } = string.Empty;
    public string Description1VisiblePrefix { get; private set; } = string.Empty;

    /// <summary>D5: the text the original's <c>DisplayInventoryDescription(0)</c> drew on THIS tick - the
    /// name during its reveal and hold (states 1..0x4c), the first description line from 0x4e - and empty on
    /// every tick it draws nothing on line 0: state 0, state 0x4d, an empty or unowned slot
    /// (<c>MainInventoryManager.cs:929-1064</c>). The screen shows exactly this; the raw prefixes above keep
    /// their values across ticks that draw nothing, which is why they are not what the screen reads.</summary>
    public string DrawnDescriptionLine0 { get; private set; } = string.Empty;

    /// <summary>D5: the text <c>DisplayInventoryDescription(1)</c> drew on THIS tick - the second description
    /// line from state 0x8f - and empty otherwise (<c>MainInventoryManager.cs:1053-1062</c>).</summary>
    public string DrawnDescriptionLine1 { get; private set; } = string.Empty;

    /// <summary>Port of <c>INT_8017fef0</c> (<c>MainInventoryManager.cs:1139-1154</c>, <c>FUN_80055f48</c>) -
    /// the shared 3-tick countdown (one character committed every 3rd tick), shared by both description
    /// lines exactly like the original's own single global.</summary>
    private int _textRevealCountdown;

    /// <summary>E13.d SI3 (D-E13D-22): armed by <see cref="RunDisplayInventoryHeadFromPostProcess"/> when
    /// the HEAD it just ran was not stopped by its own guard - <see cref="Tick"/> checks this FIRST, before
    /// the trigger/idle branch, and runs the SETUP (<see cref="RunDisplayInventorySetup"/>) alone on that
    /// next tick, no trigger and no per-frame work on it (see that method's own doc for why, plan §1.2's
    /// own clock table).</summary>
    private bool _setupPending;

    // =====================================================================================
    // Tick
    // =====================================================================================

    /// <summary>One LOGIC tick - see this class' own doc for why the trigger, <c>DisplayInventory</c> and
    /// <c>FUN_80054f1c</c> all run synchronously on the SAME call, and why the box-slide/input dispatch
    /// (<c>FUN_80056598</c>) only starts on the next one. <paramref name="player"/> is the hero's own
    /// proxy (for the <c>BlockedByEntity</c> guard, GameEngine.cs:1568) - null is tolerated (no live
    /// session/player yet), same degraded shape as every other missing-system seam in this DLL.</summary>
    public void Tick(AlundraEntityScriptProxy? player)
    {
        if (_gameState == null)
        {
            return;
        }

        // D-E13D-22: a HEAD run by the post-process last tick has a SETUP still pending - run it alone,
        // before the trigger/idle check (RunDisplayInventoryHeadFromPostProcess's own doc), no trigger and
        // no per-frame work on this tick.
        if (_setupPending)
        {
            _setupPending = false;
            RunDisplayInventorySetup(_gameState);
            return;
        }

        if (!IsActive)
        {
            if (TryTrigger(_gameState, player))
            {
                RunDisplayInventory(_gameState);
            }

            return;
        }

        RunPerFrame(_gameState);
    }

    /// <summary>Port of the trigger (<c>GameEngine.cs:1567-1576</c>) - see this class' own doc for the
    /// three guards with no port equivalent, each declared absent/always-0 right where it is tested.</summary>
    private static bool TryTrigger(AlundraGameState state, AlundraEntityScriptProxy? player)
    {
        // GameEngine.cs:1568 - StaticVariables.g_playerControlFlags == 0.
        if (state.PlayerControlFlags != 0)
        {
            return false;
        }

        // GameEngine.cs:1569 - StaticVariables.PlayerEntity.BlockedByEntity == null.
        if (player != null && player.BlockedByEntity != null)
        {
            return false;
        }

        // GameEngine.cs:1570 - StaticVariables.g_warpLockTimer == 0. NO PORT EQUIVALENT: g_warpLockTimer
        // is PlayerManager's own item-use/magic-sequence lock (Gameplay/PlayerManager.cs:1929-4013, e.g.
        // ":800/900/1994/2409" - a state machine for chained item animations), and no such system exists
        // in this DLL at all. Declared absent, treated as always 0 (never blocks).

        // GameEngine.cs:1572 - (StaticVariables.g_padState1.ButtonsJustPressed & PadState.OpenInventory) != 0.
        // NOT ByInterval - a straight edge, read from the per-tick pad D1 built (AlundraTickPad), never
        // AlundraGameState.LastPadState (plan §1.3, D0.10 - the inventory needs the tick-exact edge).
        if ((state.TickPad.ButtonsJustPressed & (AlundraPadState.Start | AlundraPadState.L2 | AlundraPadState.R2)) == 0)
        {
            return false;
        }

        // GameEngine.cs:1573 - StaticVariables.g_warpDelayFrames == 0. NO PORT EQUIVALENT with real
        // effect: AlundraWarpDirector.WarpDelayFramesForTests is set to 10 at every map entry
        // (AlundraWarpDirector.cs:237) but is NEVER decremented anywhere in this DLL (confirmed by grep) -
        // the original decrements it every single frame, unconditionally (GameEngine.cs:1562-1564),
        // reaching 0 within 10 frames of any map entry. Reading the port's own stub as a real gate would
        // introduce a NEW bug (the inventory permanently locked out after every map load, since nothing
        // would ever bring it back to 0) rather than reproduce the original's brief 10-frame cooldown.
        // AlundraWarpDirector's own doc (:230-238) already calls this field's only two original consumers,
        // this trigger among them, unwired "for structural fidelity only" - so this trigger keeps it that
        // way and treats the guard as always 0 (never blocks), per the brief's own allowance for an
        // absent guard. BEHAVIOURAL DIFFERENCE this leaves (docs/plan-e13d-inventaire.md §6): the original
        // refuses to open the inventory during the first 10 frames after a map entry (0.2 s); the port
        // opens it. It disappears once the warp director decrements the field like GameEngine.cs:1562-1564.

        // GameEngine.cs:1574 - (StaticVariables.g_padState1.ButtonsHold & PadState.Select) == 0.
        if ((state.TickPad.ButtonsHold & AlundraPadState.Select) != 0)
        {
            return false;
        }

        // GameEngine.cs:1575 - StaticVariables.g_globalTransitionState == 0. NO PORT EQUIVALENT:
        // g_globalTransitionState (StaticVariables.cs:12484) is the memory-card/save-menu state machine
        // (UI/MemoryCardManager.cs, ~90 distinct assigned values) - not ported in this DLL at all (no
        // MemoryCardManager port exists). Declared absent, treated as always 0 (never blocks).

        // GameEngine.cs:1576 - MainInventoryManager.DisplayInventory() == 0, then g_isGameEnding = 1
        // (0x8002bcf4-0x8002bd00): not ported. DisplayInventory returns 0 on one path only, the debug
        // branch's Left (:470, 0x800555f0), and that branch is dead in play (g_cdIsReady, see
        // RunDisplayInventoryHead) and not ported - so this comparison never holds here.
        return true;
    }

    /// <summary>Port of <c>DisplayInventory</c> (<c>MainInventoryManager.cs:443-499</c>) followed
    /// immediately by <c>FUN_80054f1c</c> (<c>:503-776</c>) - see this class' own doc for why both run on
    /// the same tick as the trigger. The ORDINARY trigger path (<see cref="Tick"/>'s idle branch): the HEAD
    /// and the SETUP always run together, unchanged since before E13.d SI3.</summary>
    private void RunDisplayInventory(AlundraGameState state)
    {
        if (RunDisplayInventoryHead(state))
        {
            RunDisplayInventorySetup(state);
        }
    }

    /// <summary>E13.d SI3 (docs/plan-e13d-sous-inventaire.md, D-E13D-22): the HEAD half of
    /// <c>DisplayInventory</c> only (<c>MainInventoryManager.cs:443-495</c>) - called from the post-process
    /// (<see cref="RunDisplayInventoryHeadFromPostProcess"/>) on its own tick, and from
    /// <see cref="RunDisplayInventory"/> (the ordinary trigger path) on the SAME tick as the setup. Returns
    /// false when a guard stopped it - the inventory already running, or a dialogue open - and the caller
    /// must not run the setup either, on either path.</summary>
    private bool RunDisplayInventoryHead(AlundraGameState state)
    {
        // MainInventoryManager.cs:445 - g_forbiddenWarpFlag == 0, tested first on every entry (0x80055574,
        // bnez 0x8005557c: return 1, nothing done). Guaranteed on the trigger path (Tick's Idle branch), but
        // not from the post-process (RunDisplayInventoryHeadFromPostProcess) - tested here for both (plan
        // E13.d SI9.d).
        if (ForbiddenWarpFlag != 0)
        {
            return false;
        }

        // MainInventoryManager.cs:447-452 - CheckSpecialWarpCondition(0): callback slot 0 is the dialogue
        // box (GameEngine.cs:1590-1593 reads g_callbackTable[0].Flags & 1, posed by SetTransitionType(0)
        // whenever a dialogue opens) - ported as "a dialogue box is open".
        if (AlundraDialogueDirector.Instance.IsOpen)
        {
            return false;
        }

        // MainInventoryManager.cs:454-458 - CheckSpecialWarpCondition(0xb): callback slot 0xb is the
        // debug flags menu (StaticVariables.cs:11388-11470, UIDebugManager.InitializeFlagsDebugMenu) -
        // NOT PORTED (no debug menu exists in this DLL), so this condition is always false/inactive.

        // MainInventoryManager.cs:460-482 - the g_cdIsReady == 0 debug branch: DEAD IN PLAY - g_cdIsReady
        // is set to 1 at the end of the CD init (0x8004e85c, run at boot), long before any player input is
        // possible - not ported, per the plan's own §1.1 finding. (The C# InitializeSoundSystem's own
        // "g_cdIsReady = 1", SoundManager.cs:331, is not in the executable: its CD-reset path stores 0 at
        // 0x80048514 after the CD init, and no writer of the reset request 0x8009a858 was found.)

        // MainInventoryManager.cs:484 - HudManager.InitializeHudPosition().
        AlundraHudDirector.Instance.InitializeHudPosition();

        // MainInventoryManager.cs:485-493 - SetTransitionType(6) (the setup callback's own arming,
        // reproduced by this class' own Tick ordering, not by a stored callback) and the portrait's own
        // InitializeHudTransitionVariablesAndSetStart: NOT PORTED (D-E13D-12 amended - see class doc).

        // MainInventoryManager.cs:494 - DisplayIconNames().
        RunDisplayIconNames(state);

        // MainInventoryManager.cs:495 - SoundManager.PlaySoundEffect(4).
        _soundPlayer?.PlaySfx(4);

        return true;
    }

    /// <summary>E13.d SI3 (D-E13D-22): the post-process's own call site - runs the HEAD only
    /// (<see cref="RunDisplayInventoryHead"/>) and, if it was not stopped by its own guard, arms
    /// <see cref="_setupPending"/> so the NEXT <see cref="Tick"/> runs the SETUP alone (plan §1.2's own
    /// clock table: "FUN_80054f1c (mise en place du principal) ... rien de dessiné" on the tick AFTER the
    /// head). Called from <see cref="AlundraInventoryPostProcess.Run"/> only.</summary>
    internal void RunDisplayInventoryHeadFromPostProcess()
    {
        if (_gameState == null)
        {
            return;
        }

        if (RunDisplayInventoryHead(_gameState))
        {
            _setupPending = true;
        }
    }

    /// <summary>Port of <c>FUN_80054f1c</c> (<c>MainInventoryManager.cs:503-776</c>) - the setup half only
    /// (arms <see cref="ForbiddenWarpFlag"/>, the text reset, <c>MenuOpen</c> and the seven box tweens).</summary>
    private void RunDisplayInventorySetup(AlundraGameState state)
    {
        // :505 - g_forbiddenWarpFlag = 5 (bits 0 + 2 - SetupBit | SlideOpenBit).
        ForbiddenWarpFlag = SetupBit | SlideOpenBit;

        // D5 addition (this class' own IsDrawn doc): nothing is drawn on THIS tick - RunPerFrame (the
        // per-frame handler FUN_80054f1c installs, this class' own doc on why it only starts NEXT tick)
        // has not run yet for this open.
        _hasRunPerFrameSinceSetup = false;

        // :506 - g_inventoryCursorText = 0 (name reveal restarts for whatever slot is selected).
        TextRevealState = 0;
        NameVisiblePrefix = string.Empty;
        Description0VisiblePrefix = string.Empty;
        Description1VisiblePrefix = string.Empty;
        DrawnDescriptionLine0 = string.Empty;
        DrawnDescriptionLine1 = string.Empty;
        _textRevealCountdown = 0;

        // :507 - g_playerControlFlags |= MenuOpen.
        state.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;

        // :508-773 - arm the seven boxes' TextToDisplay entries for the 15-tick opening slide (this
        // class' own doc: boxes 0/1 slide in from the left, 2/3/4/5 from the right, 6 from below).
        for (var i = 0; i < BoxCount; i++)
        {
            var layout = BoxLayout[i];
            int sourceX, sourceY, targetX, targetY;

            if (i == BoxWeapon || i == BoxItem)
            {
                sourceX = ~(layout.WidthCells << 3); // :516/:558 - (short)~(Width << 3).
                sourceY = layout.OriginY;
                targetX = layout.OriginX;
                targetY = layout.OriginY;
            }
            else if (i == BoxDescription)
            {
                sourceX = layout.OriginX;
                sourceY = SlideOffscreenBelowY; // :758 - y = 0xf0.
                targetX = layout.OriginX;
                targetY = layout.OriginY;
            }
            else
            {
                sourceX = SlideInFromRightX; // :594/:632/:670/:708 - x = 0x140.
                sourceY = layout.OriginY;
                targetX = layout.OriginX;
                targetY = layout.OriginY;
            }

            _tweens[i] = new BoxTween { Mode = 2, Tick = 0, SourceX = sourceX, SourceY = sourceY, TargetX = targetX, TargetY = targetY };
            // Box position itself is untouched here, exactly like the original (the tween's own source/
            // target describe where UpdateUiBoxesPosition will move it FROM, starting next tick) - it
            // stays wherever it was left (origin, from the last close, or construction default).
        }

        // :775 - callBackInfo.RenderFunc = FUN_80056598: reproduced by ForbiddenWarpFlag != 0 now routing
        // the NEXT Tick() call to RunPerFrame instead of the trigger check.
    }

    /// <summary>Port of <c>FUN_80056598</c> (<c>MainInventoryManager.cs:779-923</c>).</summary>
    private void RunPerFrame(AlundraGameState state)
    {
        // D5 addition (this class' own IsDrawn doc): from this call on, this open's boxes/cursor/text are
        // drawn - set unconditionally on entry, every call, cheap and idempotent.
        _hasRunPerFrameSinceSetup = true;

        // :783 - (g_forbiddenWarpFlag & 6) == 0 -> read pad input; else -> advance the box slide.
        if ((ForbiddenWarpFlag & (SlideCloseBit | SlideOpenBit)) == 0)
        {
            RunInput(state);
        }
        else
        {
            if (!RunBoxSlide(state))
            {
                // The close-completion branch already returned (skips the tail below), matching
                // MainInventoryManager.cs:901-902's own early return.
                return;
            }
        }

        // :907-922 - the "always" tail: cursor animation frame, text reveal. Drawing itself (cursor
        // sprite, money/falcon/key, icons, the six boxes, names) is D5's own job.
        CursorFrameDelay += 1;
        if (CursorFrameDelay == 0x28)
        {
            CursorFrameDelay = 0;
        }

        RunDisplayInventoryTexts(state);
    }

    /// <summary>Port of the pad-input half of <c>FUN_80056598</c> (<c>:785-859</c>).</summary>
    private void RunInput(AlundraGameState state)
    {
        var pad = state.TickPad;

        if ((pad.ButtonsJustPressedByInterval & AlundraPadState.Down) != 0)
        {
            var slot = SelectedSlotId + 6;
            var wrapped = SelectedSlotId - 0x12;
            SelectedSlotId = slot > 0x17 ? wrapped : slot;
            _soundPlayer?.PlaySfx(1);
            TextRevealState = 0;
        }

        if ((pad.ButtonsJustPressedByInterval & AlundraPadState.Up) != 0)
        {
            var slot = SelectedSlotId - 6;
            SelectedSlotId = slot < 0 ? SelectedSlotId + 0x12 : slot;
            _soundPlayer?.PlaySfx(1);
            TextRevealState = 0;
        }

        if ((pad.ButtonsJustPressedByInterval & AlundraPadState.Right) != 0)
        {
            var slot = SelectedSlotId + 1;
            SelectedSlotId = slot == slot / 6 * 6 ? SelectedSlotId - 5 : slot;
            _soundPlayer?.PlaySfx(1);
            TextRevealState = 0;
        }

        if ((pad.ButtonsJustPressedByInterval & AlundraPadState.Left) != 0)
        {
            var slot = SelectedSlotId - 1;
            SelectedSlotId = SelectedSlotId == SelectedSlotId / 6 * 6 ? SelectedSlotId + 5 : slot;
            _soundPlayer?.PlaySfx(1);
            TextRevealState = 0;
        }

        if ((pad.ButtonsJustPressedByInterval & AlundraPadState.Cross) != 0)
        {
            if (SelectedSlotId < 6)
            {
                RunEquipWeapon(state);
            }
            else
            {
                RunEquipItem(state);
            }
        }

        // :846-851 - the close branch. The executable's mask is 0x813 = Start | Triangle | R2 | L2
        // (ALUN_CD.EXE France, `andi $v0, $v0, 0x813` at 0x80056924, and the same at 0x80053634 in the
        // sub-inventory), not the decompilation's PadState.OpenInventory = 0x803 (PadState.cs:22), which
        // lost Triangle. The OPENING trigger does test 0x803 (0x8002bcac): Triangle closes, never opens
        // (docs/plan-e13d-sous-inventaire.md §1.1, D-E13D-29).
        if ((pad.ButtonsJustPressedByInterval & (AlundraPadState.Start | AlundraPadState.Triangle | AlundraPadState.L2 | AlundraPadState.R2)) != 0)
        {
            RunCloseSetup(state);
            AlundraHudDirector.Instance.InitializeHudPositionBeforeHide();
        }

        // :853-859 - L1/R1 (OpenSubInventory): close (same FUN_800556dc/sound 5 as the branch above), but
        // WITHOUT InitializeHudPositionBeforeHide (the gauge stays hidden, plan §1.1) - MenuOpen re-armed
        // and AlundraInventoryPostProcess.State = 1 instead: AlundraInventoryPostProcess.Run opens the
        // sub-inventory once this closing slide settles (this class' own IsCallbackArmed).
        if ((pad.ButtonsJustPressedByInterval & (AlundraPadState.L1 | AlundraPadState.R1)) != 0)
        {
            RunCloseSetup(state);
            state.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;
            AlundraInventoryPostProcess.Instance.State = 1;
        }
    }

    /// <summary>Port of the box-slide half of <c>FUN_80056598</c> (<c>:862-905</c>) - the seven
    /// <c>UpdateUiBoxesPosition</c> calls in their own literal order (description BEFORE money/falcon/key,
    /// :914-920 vs :863-869 - only the LAST call's return value, the money box's, gates completion,
    /// exactly like the original). Returns false when the close-completion branch already ran (the
    /// caller must skip the tail, matching the original's own early <c>return</c> at :902).</summary>
    private bool RunBoxSlide(AlundraGameState state)
    {
        AdvanceBoxTween(ref _boxes[BoxWeapon], ref _tweens[BoxWeapon]);
        AdvanceBoxTween(ref _boxes[BoxItem], ref _tweens[BoxItem]);
        AdvanceBoxTween(ref _boxes[BoxWeaponName], ref _tweens[BoxWeaponName]);
        AdvanceBoxTween(ref _boxes[BoxItemName], ref _tweens[BoxItemName]);
        AdvanceBoxTween(ref _boxes[BoxSpacer], ref _tweens[BoxSpacer]);
        AdvanceBoxTween(ref _boxes[BoxDescription], ref _tweens[BoxDescription]);
        var moneyBoxSettled = AdvanceBoxTween(ref _boxes[BoxMoneyFalconKey], ref _tweens[BoxMoneyFalconKey]);

        if (!moneyBoxSettled)
        {
            return true;
        }

        // :873-875 - the opening slide's own completion: drop bit 2, keep bit 0 (5 -> 1).
        if ((ForbiddenWarpFlag & SlideOpenBit) != 0)
        {
            ForbiddenWarpFlag &= ~SlideOpenBit;
        }

        // :878-902 - the closing slide's own completion.
        if ((ForbiddenWarpFlag & SlideCloseBit) != 0)
        {
            ForbiddenWarpFlag = 0;

            for (var i = 0; i < BoxCount; i++)
            {
                _boxes[i] = new BoxState(_boxes[i].OriginX, _boxes[i].OriginY);
            }

            // :896-899 - MenuOpen cleared ONLY if the post-process is not about to open the sub-inventory
            // (E13.d SI3, D-E13D-21/22 - replaces the former _pendingSubInventoryTransition hook).
            if ((AlundraInventoryPostProcess.Instance.State & 1) == 0)
            {
                state.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.MenuOpen;
            }

            // :901 - FUN_80047cb0(callbackInfo) cleanup: nothing to port (no callback table here).
            return false; // :902 - return (skip the tail this same tick).
        }

        return true;
    }

    /// <summary>Port of <c>FUN_800556dc</c> (<c>MainInventoryManager.cs:1901-2158</c>) - arms the
    /// seven-box reverse slide. Box origins/sizes are the SAME table <see cref="RunDisplayInventory"/>
    /// uses (D3.a/D3.b's own fixed layout); only the tween's source/target swap ends.</summary>
    private void RunCloseSetup(AlundraGameState state)
    {
        // :1903 - g_forbiddenWarpFlag |= 2.
        ForbiddenWarpFlag |= SlideCloseBit;

        // :1904 - SoundManager.PlaySoundEffect(5).
        _soundPlayer?.PlaySfx(5);

        for (var i = 0; i < BoxCount; i++)
        {
            var layout = BoxLayout[i];
            int sourceX, sourceY, targetX, targetY;

            if (i == BoxWeapon || i == BoxItem)
            {
                sourceX = layout.OriginX; // :1909/1916 - current (docked) position.
                sourceY = layout.OriginY;
                targetX = ~(layout.WidthCells << 3); // :1929/1966 - slide back out to the left.
                targetY = layout.OriginY;
            }
            else if (i == BoxDescription)
            {
                sourceX = layout.OriginX;
                sourceY = layout.OriginY;
                targetX = layout.OriginX;
                targetY = SlideOffscreenBelowY; // :2157 - startY = 0xf0, slide back out below.
            }
            else
            {
                sourceX = layout.OriginX;
                sourceY = layout.OriginY;
                targetX = SlideInFromRightX; // :2003/2039/2075/2111 - slide back out to the right.
                targetY = layout.OriginY;
            }

            _tweens[i] = new BoxTween { Mode = 2, Tick = 0, SourceX = sourceX, SourceY = sourceY, TargetX = targetX, TargetY = targetY };
        }
    }

    /// <summary>Port of <c>FUN_8005795c</c> (<c>MainInventoryManager.cs:1618-1747</c>) - equips a weapon
    /// slot (grid columns 0..4) or the sword-class item slot (column 5). Ported using
    /// <see cref="AlundraPlayerManager.GetItemIdFromSlotId"/> directly for the five weapon-slot lookups
    /// (NOT <see cref="AlundraPlayerManager.GetWeaponIdBySlotId"/>: that helper's own case order is a
    /// DIFFERENT, sequential 0-&gt;1,1-&gt;2,... mapping built for <c>GetItemIdFromCurrentWeapon</c>, not
    /// for this switch, whose own case order is 1,3,2,4,5 - <c>GetWeaponIdFromSlot1/3/2/4/5</c>,
    /// :1627/1640/1661/1682/1702).
    /// <para>A defect of the original, corrected (plan E13.d SI9.b, D-E13D-30): the executable tests "already
    /// equipped" (0x800579b0...) BEFORE "empty slot" (0x800579b8...), so with no weapon resolving (weapon id -1 or
    /// 0, or no owned item in its slot - a New Game before the first sword) an empty weapon slot compares equal
    /// (-1 == -1) and stays silent instead of sounding the error. Validity is tested first here, the order
    /// FUN_80057854 already uses for items (0x800578b8, then 0x800578c8); the equipped weapon never resolves to
    /// -1, so its own silent path is unchanged.</para></summary>
    private void RunEquipWeapon(AlundraGameState state)
    {
        if (_itemTables == null)
        {
            return;
        }

        var currentWeaponItem = AlundraPlayerManager.GetItemIdFromCurrentWeapon(state, _itemTables);
        uint slotWeaponItem;
        ushort weaponIdToSet;
        bool alreadyEquipped;
        bool valid;

        switch (SelectedSlotId)
        {
            case 0: // :1624-1638 - GetWeaponIdFromSlot1 = GetItemIdFromSlotId(1).
                slotWeaponItem = AlundraPlayerManager.GetItemIdFromSlotId(state, _itemTables, 1);
                weaponIdToSet = 1;
                alreadyEquipped = currentWeaponItem == slotWeaponItem;
                valid = slotWeaponItem != AlundraPlayerManager.NoItem;
                break;
            case 1: // :1639-1657 - GetWeaponIdFromSlot3.
                slotWeaponItem = AlundraPlayerManager.GetItemIdFromSlotId(state, _itemTables, 3);
                weaponIdToSet = 3;
                alreadyEquipped = currentWeaponItem == slotWeaponItem;
                valid = slotWeaponItem != AlundraPlayerManager.NoItem;
                break;
            case 2: // :1660-1679 - GetWeaponIdFromSlot2.
                slotWeaponItem = AlundraPlayerManager.GetItemIdFromSlotId(state, _itemTables, 2);
                weaponIdToSet = 2;
                alreadyEquipped = currentWeaponItem == slotWeaponItem;
                valid = slotWeaponItem != AlundraPlayerManager.NoItem;
                break;
            case 3: // :1681-1699 - GetWeaponIdFromSlot4.
                slotWeaponItem = AlundraPlayerManager.GetItemIdFromSlotId(state, _itemTables, 4);
                weaponIdToSet = 4;
                alreadyEquipped = currentWeaponItem == slotWeaponItem;
                valid = slotWeaponItem != AlundraPlayerManager.NoItem;
                break;
            case 4: // :1701-1718 - GetWeaponIdFromSlot5.
                slotWeaponItem = AlundraPlayerManager.GetItemIdFromSlotId(state, _itemTables, 5);
                weaponIdToSet = 5;
                alreadyEquipped = currentWeaponItem == slotWeaponItem;
                valid = slotWeaponItem != AlundraPlayerManager.NoItem;
                break;
            case 5: // :1721-1739 - the sword-class item slot: g_ItemIdBySlotIndex[5] = 7, owned check
                     // via GetNumberOfItem, weapon id to set is the literal 6.
                var fixedItemId = GItemIdBySlotIndex[5];
                slotWeaponItem = (uint)fixedItemId;
                weaponIdToSet = 6;
                alreadyEquipped = currentWeaponItem == slotWeaponItem;
                valid = AlundraPlayerManager.GetNumberOfItem(state, fixedItemId) != 0;
                break;
            default:
                slotWeaponItem = AlundraPlayerManager.NoItem;
                weaponIdToSet = 0;
                alreadyEquipped = false;
                valid = false;
                break;
        }

        if (!valid)
        {
            // :1745-1746 - the fall-through: an empty/invalid slot, tested first (see this method's own doc).
            _soundPlayer?.PlaySfx(3);
            RunDisplayIconNames(state);
            return;
        }

        if (alreadyEquipped)
        {
            RunDisplayIconNames(state);
            return;
        }

        AlundraPlayerManager.SetPlayerWeaponId(state, _itemTables, weaponIdToSet);
        _soundPlayer?.PlaySfx(2);
        RunDisplayIconNames(state);
    }

    /// <summary>Port of <c>g_ItemIdBySlotIndex</c> (<c>StaticVariables.cs:12355-12361</c>) - -1 means
    /// "resolve through <see cref="SlotIdByInventorySlotIndex"/>", 0 means empty, anything else is a
    /// fixed item id.</summary>
    private static readonly int[] GItemIdBySlotIndex =
    {
        -1, -1, -1, -1, -1, 7,
        0x24, 0x29, 0x25, 0x26, 0x27, -1,
        -1, -1, -1, -1, 0x20, 0x28,
        0x36, 0x3B, 0x1F, -1, -1, -1,
    };

    /// <summary>Port of <c>SlotIdByInventorySlotIndex</c> (<c>MainInventoryManager.cs:1464-1470</c>).</summary>
    private static readonly int[] SlotIdByInventorySlotIndex =
    {
         1,  3,  2,  4,  5, -1,
        -1, -1, -1, -1, -1, 11,
        16, 17, 18, 19, -1, -1,
        -1, -1, -1, 20, 21, 22,
    };

    /// <summary>Port of <c>FUN_80057854</c> (<c>MainInventoryManager.cs:1795-1855</c>) - equips an item
    /// slot (grid rows 1-3, columns 0-5, minus the sword-class slot already handled by
    /// <see cref="RunEquipWeapon"/>).</summary>
    private void RunEquipItem(AlundraGameState state)
    {
        if (_itemTables == null)
        {
            return;
        }

        var slotItemId = GItemIdBySlotIndex[SelectedSlotId];

        if (slotItemId == 0)
        {
            RunDisplayIconNames(state);
            RunPostEquipItemCleanup();
            return;
        }

        uint resolvedItemId;

        if (slotItemId == -1)
        {
            // :1810-1811 - the slot accessor already resolves to an item id, no second indirection.
            var value = AlundraPlayerManager.GetItemIdFromSlotId(state, _itemTables, (uint)SlotIdByInventorySlotIndex[SelectedSlotId]);

            if (value == AlundraPlayerManager.NoItem)
            {
                _soundPlayer?.PlaySfx(3);
                return; // :1818 - no DisplayIconNames/FUN_8005ac90 on this early path either.
            }

            var currentItemId = AlundraPlayerManager.SetItemIdFromCurrentItemId(state, _itemTables);
            if (currentItemId == value)
            {
                return;
            }

            resolvedItemId = value;
        }
        else
        {
            if (AlundraPlayerManager.GetNumberOfItem(state, slotItemId) == 0)
            {
                _soundPlayer?.PlaySfx(3);
                return;
            }

            var currentItemId = AlundraPlayerManager.SetItemIdFromCurrentItemId(state, _itemTables);
            if (currentItemId == slotItemId)
            {
                return;
            }

            resolvedItemId = (uint)slotItemId;
        }

        AlundraPlayerManager.SetCurrentItemId(state, resolvedItemId);
        _soundPlayer?.PlaySfx(2);

        RunDisplayIconNames(state);
        RunPostEquipItemCleanup();
    }

    /// <summary>D5 addition (docs/plan-e13d-inventaire.md, "Ce que D5 lit"): a minimal read-only query
    /// the director did not expose before, reusing the EXACT same two tables and resolution rule
    /// <see cref="RunEquipItem"/>/<see cref="RunDisplayInventoryTexts"/> already use (no logic changed
    /// on either of them) - what item, if any, grid slot <paramref name="slotIndex"/> (0..23) currently
    /// shows: null for an empty slot (<c>g_ItemIdBySlotIndex</c> == 0) or an unowned one
    /// (<c>GetNumberOfItem</c> == 0 for a fixed id, or the resolved slot id is
    /// <see cref="AlundraPlayerManager.NoItem"/>), otherwise the resolved item id. D5's composer uses
    /// this to draw the 24 grid icons and to find which slot currently holds the equipped weapon/item
    /// (for the two selection frames), exactly the same resolution <c>DisplayWeaponAndItemIcons</c>
    /// (<c>MainInventoryManager.cs:1267-1457</c>) performs inline, per slot, every frame.</summary>
    public int? ResolveSlotItemId(int slotIndex)
    {
        if (_gameState == null || _itemTables == null)
        {
            return null;
        }

        var fixedId = GItemIdBySlotIndex[slotIndex];

        if (fixedId == 0)
        {
            return null;
        }

        if (fixedId == -1)
        {
            var value = AlundraPlayerManager.GetItemIdFromSlotId(_gameState, _itemTables, (uint)SlotIdByInventorySlotIndex[slotIndex]);
            return value == AlundraPlayerManager.NoItem ? null : (int)value;
        }

        return AlundraPlayerManager.GetNumberOfItem(_gameState, fixedId) == 0 ? null : fixedId;
    }

    /// <summary>Port of <c>FUN_8005ac90</c> (<c>MainInventoryManager.cs:1858-1898</c>) - the CD read
    /// position for music-box-like items. HAS NO EFFECT IN THE PORT: the original's own C# already marks
    /// <c>SetCdReadPosition</c> unported ("PARTIAL: ... not ported in the current C# CD/audio backend",
    /// :1895), and this DLL has no CD/audio-track backend at all - kept as a documented no-op rather than
    /// a silent omission.</summary>
    private static void RunPostEquipItemCleanup()
    {
        // Intentionally empty - see this method's own doc.
    }

    /// <summary>Port of <c>DisplayIconNames</c> (<c>MainInventoryManager.cs:1750-1793</c>) - resolves the
    /// equipped weapon/item id to a display string.
    /// <para>A defect of the original, corrected (plan E13.d SI9.b, D-E13D-30): when no weapon resolves, the
    /// executable skips the whole weapon block (0x80055c9c) and leaves glyph row 0 as it was - the previous
    /// weapon's name, or text another screen built there (0x80059538) - while the item gets its seven-space
    /// blank (0x80026850). The weapon gets the same blank here.</para></summary>
    private void RunDisplayIconNames(AlundraGameState state)
    {
        if (_itemTables == null)
        {
            return;
        }

        var currentWeaponItem = AlundraPlayerManager.GetItemIdFromCurrentWeapon(state, _itemTables);
        if (currentWeaponItem == AlundraPlayerManager.NoItem)
        {
            EquippedWeaponName = BlankName; // corrected defect, see this method's own doc.
        }
        else if (AlundraEtcStringTable.TryResolveItemName(EngineEnvironment.ProjectPath, (int)currentWeaponItem, out var weaponName))
        {
            EquippedWeaponName = weaponName;
        }

        var currentItemId = AlundraPlayerManager.SetItemIdFromCurrentItemId(state, _itemTables);
        if (currentItemId == AlundraPlayerManager.NoItem)
        {
            EquippedItemName = BlankName; // :1774-1777
        }
        else if (AlundraEtcStringTable.TryResolveItemName(EngineEnvironment.ProjectPath, (int)currentItemId, out var itemName))
        {
            EquippedItemName = itemName;
        }
    }

    /// <summary>Port of <c>DisplayInventoryTexts</c> (<c>MainInventoryManager.cs:929-1064</c>) - the text
    /// reveal state machine, ticked once per active frame (see <see cref="RunPerFrame"/>'s own tail).</summary>
    private void RunDisplayInventoryTexts(AlundraGameState state)
    {
        // What DisplayInventoryDescription draws THIS tick, set by the branches that call it and left
        // empty by the ones that do not (see DrawnDescriptionLine0/1).
        DrawnDescriptionLine0 = string.Empty;
        DrawnDescriptionLine1 = string.Empty;

        if (_itemTables == null)
        {
            return;
        }

        var slotItemId = GItemIdBySlotIndex[SelectedSlotId];

        if (slotItemId == 0)
        {
            return;
        }

        int itemId;

        if (slotItemId == -1)
        {
            var value = AlundraPlayerManager.GetItemIdFromSlotId(state, _itemTables, (uint)SlotIdByInventorySlotIndex[SelectedSlotId]);
            if (value == AlundraPlayerManager.NoItem)
            {
                return;
            }

            itemId = (int)value;
        }
        else if (AlundraPlayerManager.GetNumberOfItem(state, slotItemId) == 0)
        {
            return;
        }
        else
        {
            itemId = slotItemId;
        }

        var cursor = TextRevealState;
        var projectPath = EngineEnvironment.ProjectPath;

        // :959-967 - state 0: load the name, nothing revealed yet.
        if (cursor == 0)
        {
            NameVisiblePrefix = string.Empty;
            _textRevealCountdown = 0;
            TextRevealState = cursor + 1;
            return;
        }

        // :970-986 - states 1..0x10: reveal the name one character at a time.
        if ((uint)(cursor - 1) < 0x10)
        {
            AlundraEtcStringTable.TryResolveItemName(projectPath, itemId, out var name);

            if (cursor - 1 >= name.Length)
            {
                TextRevealState = 0x11;
            }
            else
            {
                AdvanceTextReveal(name[cursor - 1]);
                NameVisiblePrefix = RevealedPrefix(name, TextRevealState - 1);
            }

            DrawnDescriptionLine0 = NameVisiblePrefix; // :983 DisplayInventoryDescription(0)
            return;
        }

        // :989-994 - states 0x11..0x4c: hold the name on screen.
        if ((uint)(cursor - 0x11) < 0x3c)
        {
            TextRevealState = cursor + 1;
            DrawnDescriptionLine0 = NameVisiblePrefix; // :992 DisplayInventoryDescription(0)
            return;
        }

        // :997-1005 - state 0x4d: switch line 0 from the name to the first description line.
        if (cursor == 0x4d)
        {
            Description0VisiblePrefix = string.Empty;
            _textRevealCountdown = 0;
            TextRevealState = cursor + 1;
            return;
        }

        // :1008-1024 - states 0x4e..0x8d: reveal the first description line.
        if ((uint)(cursor - 0x4e) < 0x40)
        {
            AlundraEtcStringTable.TryResolveItemDescriptionLine0(projectPath, itemId, out var firstLine);

            if (cursor - 0x4e >= firstLine.Length)
            {
                TextRevealState = 0x8e;
            }
            else
            {
                AdvanceTextReveal(firstLine[cursor - 0x4e]);
                Description0VisiblePrefix = RevealedPrefix(firstLine, TextRevealState - 0x4e);
            }

            DrawnDescriptionLine0 = Description0VisiblePrefix; // :1022 DisplayInventoryDescription(0)
            return;
        }

        // :1027-1036 - state 0x8e: switch to the second description line.
        if (cursor == 0x8e)
        {
            Description1VisiblePrefix = string.Empty;
            _textRevealCountdown = 0;
            TextRevealState = cursor + 1;
            DrawnDescriptionLine0 = Description0VisiblePrefix; // :1032 DisplayInventoryDescription(0)
            return;
        }

        // :1039-1056 - states 0x8f..0xce: reveal the second description line.
        if ((uint)(cursor - 0x8f) < 0x40)
        {
            AlundraEtcStringTable.TryResolveItemDescriptionLine1(projectPath, itemId, out var secondLine);

            if (cursor - 0x8f >= secondLine.Length)
            {
                TextRevealState = 0xcf;
            }
            else
            {
                AdvanceTextReveal(secondLine[cursor - 0x8f]);
                Description1VisiblePrefix = RevealedPrefix(secondLine, TextRevealState - 0x8f);
            }

            DrawnDescriptionLine0 = Description0VisiblePrefix; // :1053 DisplayInventoryDescription(0)
            DrawnDescriptionLine1 = Description1VisiblePrefix; // :1054 DisplayInventoryDescription(1)
            return;
        }

        // :1059-1062 - cursor == 0xcf: both lines are complete and drawn, nothing more to advance.
        if (cursor == 0xcf)
        {
            DrawnDescriptionLine0 = Description0VisiblePrefix;
            DrawnDescriptionLine1 = Description1VisiblePrefix;
        }
    }

    /// <summary>Port of <c>FUN_80055f48</c> (<c>MainInventoryManager.cs:1139-1154</c>) - commits one
    /// character every third tick. The original also widens the glyph-strip sprite by the character's
    /// pixel width (<c>g_fontCharWidthTable</c>) - irrelevant here, D5 measures its own font.</summary>
    private void AdvanceTextReveal(char c)
    {
        _ = c; // kept as a parameter for the port's own signature symmetry with the original.

        if (_textRevealCountdown != 0)
        {
            _textRevealCountdown -= 1;
            return;
        }

        _textRevealCountdown = 2;
        TextRevealState += 1;
    }

    /// <summary>Port of <c>RenderRevealedLine</c>'s own clamp (<c>MainInventoryManager.cs:1069-1094</c>) -
    /// never split an escape pair (<c>{x</c>/<c>}x</c>) across the visible/hidden boundary.</summary>
    private static string RevealedPrefix(string text, int visibleLength)
    {
        visibleLength = Math.Clamp(visibleLength, 0, text.Length);

        if (visibleLength > 0 && (text[visibleLength - 1] == '{' || text[visibleLength - 1] == '}'))
        {
            visibleLength -= 1;
        }

        return text.Substring(0, visibleLength);
    }
}
